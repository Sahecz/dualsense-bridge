using System.Runtime.CompilerServices;
using DualSenseBridge.Core;
using HidSharp;

internal sealed class HidSharpDualSenseInputSource : IControllerInputSource
{
    public ValueTask<IControllerInputSession?> TryConnectAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        foreach (var device in DeviceList.Local.GetHidDevices(DualSenseDevice.SonyVendorId)
            .Where(candidate => DualSenseDevice.IsSupported(candidate.VendorID, candidate.ProductID)))
        {
            if (device.TryOpen(out var stream))
            {
                IControllerInputSession session = new Session(device, stream);
                return ValueTask.FromResult<IControllerInputSession?>(session);
            }
        }

        return ValueTask.FromResult<IControllerInputSession?>(null);
    }

    private sealed class Session(HidDevice device, HidStream stream) : IControllerInputSession
    {
        public string DisplayName => DualSenseDevice.ModelName(device.ProductID);

        public async IAsyncEnumerable<ControllerState> ReadStatesAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var buffer = new byte[Math.Max(device.GetMaxInputReportLength(), 78)];

            while (!cancellationToken.IsCancellationRequested)
            {
                var bytesRead = await stream.ReadAsync(buffer.AsMemory(), cancellationToken);
                if (bytesRead == 0)
                {
                    yield break;
                }

                yield return DualSenseReportParser.Parse(buffer.AsSpan(0, bytesRead));
            }
        }

        public ValueTask DisposeAsync()
        {
            stream.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
