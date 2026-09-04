namespace DualSenseBridge.Core;

public interface IVirtualGamepadOutput : IAsyncDisposable
{
    bool IsConnected { get; }

    ValueTask ConnectAsync(CancellationToken cancellationToken = default);

    ValueTask SubmitAsync(XboxControllerState state, CancellationToken cancellationToken = default);
}
