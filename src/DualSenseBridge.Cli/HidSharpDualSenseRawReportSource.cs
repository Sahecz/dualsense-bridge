using System.Diagnostics;
using System.Runtime.CompilerServices;
using DualSenseBridge.Core;
using HidSharp;

internal sealed class HidSharpDualSenseRawReportSource : IRawHidReportSource
{
    public ValueTask<IRawHidReportSession?> TryConnectAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        foreach (var device in DeviceList.Local.GetHidDevices(DualSenseDevice.SonyVendorId)
            .Where(candidate => DualSenseDevice.IsSupported(candidate.VendorID, candidate.ProductID)))
        {
            if (device.TryOpen(out var stream))
            {
                IRawHidReportSession session = new Session(device, stream);
                return ValueTask.FromResult<IRawHidReportSession?>(session);
            }
        }

        return ValueTask.FromResult<IRawHidReportSession?>(null);
    }

    private sealed class Session : IRawHidReportSession
    {
        private readonly HidDevice _device;
        private readonly HidStream _stream;

        public Session(HidDevice device, HidStream stream)
        {
            _device = device;
            _stream = stream;
            Device = new RawHidDeviceInfo(
                device.VendorID,
                device.ProductID,
                DualSenseDevice.ModelName(device.ProductID),
                device.GetMaxInputReportLength(),
                TryReadFirmwareFeatureReport(stream));
        }

        public RawHidDeviceInfo Device { get; }

        public async IAsyncEnumerable<RawHidReport> ReadReportsAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var buffer = new byte[Math.Max(_device.GetMaxInputReportLength(), 78)];
            var stopwatch = Stopwatch.StartNew();
            long sequence = 0;

            while (!cancellationToken.IsCancellationRequested)
            {
                var bytesRead = await _stream.ReadAsync(buffer.AsMemory(), cancellationToken);
                if (bytesRead == 0)
                {
                    yield break;
                }

                yield return new RawHidReport(
                    sequence++,
                    stopwatch.ElapsedTicks * 1_000_000L / Stopwatch.Frequency,
                    buffer.AsSpan(0, bytesRead).ToArray());
            }
        }

        public ValueTask DisposeAsync()
        {
            _stream.Dispose();
            return ValueTask.CompletedTask;
        }

        private static byte[]? TryReadFirmwareFeatureReport(HidStream stream)
        {
            try
            {
                var report = new byte[64];
                report[0] = 0x20;
                stream.GetFeature(report);
                return report;
            }
            catch
            {
                return null;
            }
        }
    }
}
