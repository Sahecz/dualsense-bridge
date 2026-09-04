using System.Runtime.CompilerServices;

namespace DualSenseBridge.Core;

public sealed class SimulatedControllerInputSource : IControllerInputSource
{
    private readonly TimeSpan _sampleInterval;
    private readonly int _samplesPerConnection;
    private int _connectionNumber;

    public SimulatedControllerInputSource(
        TimeSpan? sampleInterval = null,
        int samplesPerConnection = 10)
    {
        if (samplesPerConnection <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(samplesPerConnection));
        }

        _sampleInterval = sampleInterval ?? TimeSpan.FromMilliseconds(700);
        _samplesPerConnection = samplesPerConnection;
    }

    public ValueTask<IControllerInputSession?> TryConnectAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IControllerInputSession session = new Session(
            Interlocked.Increment(ref _connectionNumber),
            _sampleInterval,
            _samplesPerConnection);
        return ValueTask.FromResult<IControllerInputSession?>(session);
    }

    private sealed class Session(
        int connectionNumber,
        TimeSpan sampleInterval,
        int samplesPerConnection) : IControllerInputSession
    {
        public string DisplayName => $"DualSense simulado #{connectionNumber}";

        public async IAsyncEnumerable<ControllerState> ReadStatesAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var (_, state) in ControllerSimulator.States().Take(samplesPerConnection))
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return state;

                if (sampleInterval > TimeSpan.Zero)
                {
                    await Task.Delay(sampleInterval, cancellationToken);
                }
            }
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
