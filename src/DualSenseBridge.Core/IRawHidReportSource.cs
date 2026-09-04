namespace DualSenseBridge.Core;

public sealed record RawHidDeviceInfo(
    int VendorId,
    int ProductId,
    string Model,
    int MaximumInputReportLength,
    byte[]? FirmwareFeatureReport);

public readonly record struct RawHidReport(
    long Sequence,
    long ElapsedMicroseconds,
    byte[] Bytes);

public interface IRawHidReportSource
{
    ValueTask<IRawHidReportSession?> TryConnectAsync(
        CancellationToken cancellationToken = default);
}

public interface IRawHidReportSession : IAsyncDisposable
{
    RawHidDeviceInfo Device { get; }

    IAsyncEnumerable<RawHidReport> ReadReportsAsync(
        CancellationToken cancellationToken = default);
}
