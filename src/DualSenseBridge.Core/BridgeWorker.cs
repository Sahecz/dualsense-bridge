namespace DualSenseBridge.Core;

public enum BridgeStatus
{
    WaitingForController,
    ConnectingVirtualController,
    Bridging,
    Recovering,
    Stopped,
}

public sealed record BridgeStatusUpdate(
    BridgeStatus Status,
    string Message,
    Exception? Error = null);

public sealed record BridgeWorkerOptions(
    TimeSpan SearchInterval,
    TimeSpan ReconnectDelay)
{
    public static BridgeWorkerOptions Default { get; } = new(
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(1));
}

public sealed class BridgeWorker
{
    private readonly IControllerInputSource _inputSource;
    private readonly Func<IVirtualGamepadOutput> _outputFactory;
    private readonly BridgeWorkerOptions _options;
    private BridgeStatus? _lastPublishedStatus;

    public BridgeWorker(
        IControllerInputSource inputSource,
        Func<IVirtualGamepadOutput> outputFactory,
        BridgeWorkerOptions? options = null)
    {
        _inputSource = inputSource ?? throw new ArgumentNullException(nameof(inputSource));
        _outputFactory = outputFactory ?? throw new ArgumentNullException(nameof(outputFactory));
        _options = options ?? BridgeWorkerOptions.Default;

        if (_options.SearchInterval < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "El intervalo de búsqueda no puede ser negativo.");
        }

        if (_options.ReconnectDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "La espera de reconexión no puede ser negativa.");
        }
    }

    public event Action<BridgeStatusUpdate>? StatusChanged;

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Publish(BridgeStatus.WaitingForController, "Esperando un DualSense...");

                IControllerInputSession? input;
                try
                {
                    input = await _inputSource.TryConnectAsync(cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception error)
                {
                    Publish(BridgeStatus.Recovering, "No se pudo buscar el DualSense; se reintentará.", error);
                    await DelayAsync(_options.ReconnectDelay, cancellationToken);
                    continue;
                }

                if (input is null)
                {
                    await DelayAsync(_options.SearchInterval, cancellationToken);
                    continue;
                }

                await using (input)
                {
                    IVirtualGamepadOutput? output = null;
                    try
                    {
                        Publish(
                            BridgeStatus.ConnectingVirtualController,
                            $"{input.DisplayName} detectado; creando el control Xbox virtual...");
                        output = _outputFactory();
                        await output.ConnectAsync(cancellationToken);
                        Publish(BridgeStatus.Bridging, $"Puente activo: {input.DisplayName} → Xbox 360.");

                        await foreach (var state in input.ReadStatesAsync(cancellationToken)
                            .WithCancellation(cancellationToken))
                        {
                            await output.SubmitAsync(XboxControllerMapper.Map(state), cancellationToken);
                        }

                        Publish(BridgeStatus.Recovering, "El DualSense se desconectó; esperando para reconectar.");
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception error)
                    {
                        Publish(BridgeStatus.Recovering, "El puente perdió la conexión; se reintentará.", error);
                    }
                    finally
                    {
                        if (output is not null)
                        {
                            await output.DisposeAsync();
                        }
                    }
                }

                await DelayAsync(_options.ReconnectDelay, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Cancellation is the normal, clean shutdown path.
        }
        finally
        {
            Publish(BridgeStatus.Stopped, "Puente detenido y control virtual retirado.");
        }
    }

    private void Publish(BridgeStatus status, string message, Exception? error = null)
    {
        if (_lastPublishedStatus == status && error is null)
        {
            return;
        }

        _lastPublishedStatus = status;
        StatusChanged?.Invoke(new BridgeStatusUpdate(status, message, error));
    }

    private static Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken) =>
        delay == TimeSpan.Zero
            ? Task.CompletedTask
            : Task.Delay(delay, cancellationToken);
}
