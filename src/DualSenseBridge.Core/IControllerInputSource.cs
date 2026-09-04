namespace DualSenseBridge.Core;

public interface IControllerInputSource
{
    ValueTask<IControllerInputSession?> TryConnectAsync(
        CancellationToken cancellationToken = default);
}

public interface IControllerInputSession : IAsyncDisposable
{
    string DisplayName { get; }

    IAsyncEnumerable<ControllerState> ReadStatesAsync(
        CancellationToken cancellationToken = default);
}
