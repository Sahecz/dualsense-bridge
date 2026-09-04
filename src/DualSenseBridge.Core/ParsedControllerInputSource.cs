using System.Runtime.CompilerServices;

namespace DualSenseBridge.Core;

public sealed class ParsedControllerInputSource(IRawHidReportSource source) : IControllerInputSource
{
    public async ValueTask<IControllerInputSession?> TryConnectAsync(
        CancellationToken cancellationToken = default)
    {
        var rawSession = await source.TryConnectAsync(cancellationToken);
        return rawSession is null ? null : new Session(rawSession);
    }

    private sealed class Session(IRawHidReportSession rawSession) : IControllerInputSession
    {
        public string DisplayName => rawSession.Device.Model;

        public async IAsyncEnumerable<ControllerState> ReadStatesAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var report in rawSession.ReadReportsAsync(cancellationToken)
                .WithCancellation(cancellationToken))
            {
                yield return DualSenseReportParser.Parse(report.Bytes);
            }
        }

        public ValueTask DisposeAsync() => rawSession.DisposeAsync();
    }
}
