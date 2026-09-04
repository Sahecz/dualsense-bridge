namespace DualSenseBridge.Core;

public sealed record HidCaptureDocument(
    int SchemaVersion,
    string CaptureToolVersion,
    HidCaptureDevice Device,
    HidCaptureEnvironment Environment,
    IReadOnlyList<HidActionCapture> Actions)
{
    public const int CurrentSchemaVersion = 1;
}

public sealed record HidCaptureDevice(
    string VendorId,
    string ProductId,
    string Model,
    int MaximumInputReportLength,
    string? FirmwareFeatureReportHex);

public sealed record HidCaptureEnvironment(
    string OperatingSystem,
    string RuntimeVersion);

public sealed record HidActionCapture(
    string Label,
    string Instructions,
    string Connection,
    IReadOnlyList<HidReportSample> Samples);

public sealed record HidReportSample(
    long Sequence,
    long ElapsedMicroseconds,
    string ReportId,
    int Length,
    string BytesHex);
