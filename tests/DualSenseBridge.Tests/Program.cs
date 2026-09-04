using DualSenseBridge.Core;

var tests = new (string Name, Action Run)[]
{
    ("identifica dispositivos compatibles", IdentifiesSupportedDevices),
    ("interpreta reporte USB neutral", ParsesNeutralUsbReport),
    ("interpreta botones y gatillos USB", ParsesUsbButtonsAndTriggers),
    ("interpreta Bluetooth extendido", ParsesExtendedBluetoothReport),
    ("interpreta Bluetooth básico", ParsesBasicBluetoothReport),
    ("el simulador repite su secuencia", SimulatorRepeatsItsSequence),
    ("mapea DualSense a Xbox 360", MapsDualSenseToXbox),
};

var failures = 0;
foreach (var (name, run) in tests)
{
    try
    {
        run();
        Console.WriteLine($"PASS  {name}");
    }
    catch (Exception error)
    {
        failures++;
        Console.Error.WriteLine($"FAIL  {name}: {error.Message}");
    }
}

var asyncTests = new (string Name, Func<Task> Run)[]
{
    ("el puente transmite, reconecta y limpia recursos", BridgeTransmitsReconnectsAndCleansUp),
    ("el puente espera sin crear una salida virtual", BridgeWaitsWithoutCreatingOutput),
};

foreach (var (name, run) in asyncTests)
{
    try
    {
        await run();
        Console.WriteLine($"PASS  {name}");
    }
    catch (Exception error)
    {
        failures++;
        Console.Error.WriteLine($"FAIL  {name}: {error.Message}");
    }
}

var total = tests.Length + asyncTests.Length;
Console.WriteLine($"\n{total - failures}/{total} pruebas correctas.");
return failures == 0 ? 0 : 1;

static void IdentifiesSupportedDevices()
{
    Equal(true, DualSenseDevice.IsSupported(0x054C, 0x0CE6));
    Equal(true, DualSenseDevice.IsSupported(0x054C, 0x0DF2));
    Equal(false, DualSenseDevice.IsSupported(0x045E, 0x0CE6));
}

static void ParsesNeutralUsbReport()
{
    var report = new byte[64];
    report[0] = 0x01;
    report[1] = report[2] = report[3] = report[4] = 128;
    report[8] = 0x08;
    var state = DualSenseReportParser.Parse(report);

    Equal(ConnectionKind.Usb, state.Connection);
    Equal(DPadDirection.Neutral, state.DPad);
    Equal(new StickState(128, 128), state.LeftStick);
    Equal(GamepadButtons.None, state.Buttons);
}

static void ParsesUsbButtonsAndTriggers()
{
    var report = new byte[64];
    report[0] = 0x01;
    report[5] = 80;
    report[6] = 200;
    report[8] = 0x20 | 0x02;
    report[9] = 0x01 | 0x20;
    report[10] = 0x01;
    var state = DualSenseReportParser.Parse(report);

    Equal(DPadDirection.Right, state.DPad);
    HasFlag(state.Buttons, GamepadButtons.Cross);
    HasFlag(state.Buttons, GamepadButtons.L1);
    HasFlag(state.Buttons, GamepadButtons.Options);
    HasFlag(state.Buttons, GamepadButtons.Ps);
    Equal(new TriggerState(80, 200), state.Triggers);
}

static void ParsesExtendedBluetoothReport()
{
    var report = new byte[78];
    report[0] = 0x31;
    report[3] = 10;
    report[4] = 20;
    report[5] = 30;
    report[6] = 40;
    report[10] = 0x40 | 0x08;
    report[11] = 0x02;
    var state = DualSenseReportParser.Parse(report);

    Equal(ConnectionKind.BluetoothExtended, state.Connection);
    Equal(new StickState(10, 20), state.LeftStick);
    HasFlag(state.Buttons, GamepadButtons.Circle);
    HasFlag(state.Buttons, GamepadButtons.R1);
}

static void ParsesBasicBluetoothReport()
{
    byte[] report = [0x01, 11, 22, 33, 44, 0x86, 0x04, 0x02, 0, 0];
    var state = DualSenseReportParser.Parse(report);

    Equal(ConnectionKind.BluetoothBasic, state.Connection);
    Equal(new StickState(11, 22), state.LeftStick);
    Equal(DPadDirection.Left, state.DPad);
    HasFlag(state.Buttons, GamepadButtons.Triangle);
    HasFlag(state.Buttons, GamepadButtons.L2);
    HasFlag(state.Buttons, GamepadButtons.Touchpad);
    Equal((byte)255, state.Triggers.Left);
}

static void SimulatorRepeatsItsSequence()
{
    using var states = ControllerSimulator.States().GetEnumerator();
    var samples = Enumerable.Range(0, 6).Select(_ =>
    {
        states.MoveNext();
        return states.Current;
    }).ToArray();

    Equal("neutral", samples[0].Label);
    HasFlag(samples[1].State.Buttons, GamepadButtons.Cross);
    Equal((byte)255, samples[2].State.LeftStick.X);
    Equal((byte)220, samples[3].State.Triggers.Right);
    Equal(DPadDirection.UpRight, samples[4].State.DPad);
    Equal("neutral", samples[5].Label);
}

static void MapsDualSenseToXbox()
{
    var source = new ControllerState(
        ConnectionKind.Usb,
        new StickState(0, 255),
        new StickState(128, 64),
        new TriggerState(51, 204),
        DPadDirection.UpRight,
        GamepadButtons.Cross |
        GamepadButtons.Square |
        GamepadButtons.L1 |
        GamepadButtons.Options |
        GamepadButtons.Ps |
        GamepadButtons.Touchpad);

    var result = XboxControllerMapper.Map(source);

    Equal(0f, result.LeftStick.X);
    Equal(1f, result.LeftStick.Y);
    Equal(0.2f, result.Triggers.Left);
    Equal(0.8f, result.Triggers.Right);
    Equal(XboxDPad.UpRight, result.DPad);
    HasXboxFlag(result.Buttons, XboxButtons.A);
    HasXboxFlag(result.Buttons, XboxButtons.X);
    HasXboxFlag(result.Buttons, XboxButtons.LeftBumper);
    HasXboxFlag(result.Buttons, XboxButtons.Start);
    HasXboxFlag(result.Buttons, XboxButtons.Guide);
    Equal(false, result.Buttons.HasFlag(XboxButtons.B));
}

static async Task BridgeTransmitsReconnectsAndCleansUp()
{
    using var shutdown = new CancellationTokenSource(TimeSpan.FromSeconds(5));
    var neutral = new ControllerState(
        ConnectionKind.Simulated,
        new StickState(128, 128),
        new StickState(128, 128),
        new TriggerState(0, 0),
        DPadDirection.Neutral,
        GamepadButtons.None);
    var pressed = neutral with { Buttons = GamepadButtons.Cross };
    var source = new QueueInputSource(
        new StubInputSession("simulado 1", neutral),
        new StubInputSession("simulado 2", pressed));
    var outputs = new List<StubOutput>();
    var submitted = new List<XboxControllerState>();
    var statuses = new List<BridgeStatus>();

    var worker = new BridgeWorker(
        source,
        () =>
        {
            var output = new StubOutput(submitted, () =>
            {
                if (submitted.Count == 2)
                {
                    shutdown.Cancel();
                }
            });
            outputs.Add(output);
            return output;
        },
        new BridgeWorkerOptions(TimeSpan.Zero, TimeSpan.Zero));
    worker.StatusChanged += update => statuses.Add(update.Status);

    await worker.RunAsync(shutdown.Token);

    Equal(2, source.ConnectionAttempts);
    Equal(2, outputs.Count);
    Equal(2, outputs.Count(output => output.Disposed));
    Equal(2, submitted.Count);
    HasXboxFlag(submitted[1].Buttons, XboxButtons.A);
    Equal(true, statuses.Contains(BridgeStatus.Recovering));
    Equal(BridgeStatus.Stopped, statuses[^1]);
}

static async Task BridgeWaitsWithoutCreatingOutput()
{
    using var shutdown = new CancellationTokenSource();
    var source = new EmptyInputSource(shutdown);
    var outputsCreated = 0;
    var statuses = new List<BridgeStatus>();
    var worker = new BridgeWorker(
        source,
        () =>
        {
            outputsCreated++;
            return new StubOutput([], null);
        },
        new BridgeWorkerOptions(TimeSpan.Zero, TimeSpan.Zero));
    worker.StatusChanged += update => statuses.Add(update.Status);

    await worker.RunAsync(shutdown.Token);

    Equal(0, outputsCreated);
    Equal(BridgeStatus.WaitingForController, statuses[0]);
    Equal(BridgeStatus.Stopped, statuses[^1]);
}

static void Equal<T>(T expected, T actual) where T : notnull
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"Esperado: {expected}; obtenido: {actual}.");
    }
}

static void HasFlag(GamepadButtons actual, GamepadButtons expected)
{
    if (!actual.HasFlag(expected))
    {
        throw new InvalidOperationException($"Falta el botón {expected}; obtenido: {actual}.");
    }
}

static void HasXboxFlag(XboxButtons actual, XboxButtons expected)
{
    if (!actual.HasFlag(expected))
    {
        throw new InvalidOperationException($"Falta el botón Xbox {expected}; obtenido: {actual}.");
    }
}

sealed class QueueInputSource(params IControllerInputSession[] sessions) : IControllerInputSource
{
    private readonly Queue<IControllerInputSession> _sessions = new(sessions);

    public int ConnectionAttempts { get; private set; }

    public ValueTask<IControllerInputSession?> TryConnectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ConnectionAttempts++;
        return ValueTask.FromResult<IControllerInputSession?>(
            _sessions.Count > 0 ? _sessions.Dequeue() : null);
    }
}

sealed class EmptyInputSource(CancellationTokenSource shutdown) : IControllerInputSource
{
    public ValueTask<IControllerInputSession?> TryConnectAsync(CancellationToken cancellationToken = default)
    {
        shutdown.Cancel();
        return ValueTask.FromResult<IControllerInputSession?>(null);
    }
}

sealed class StubInputSession(string displayName, params ControllerState[] states) : IControllerInputSession
{
    public string DisplayName => displayName;

    public async IAsyncEnumerable<ControllerState> ReadStatesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var state in states)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return state;
            await Task.Yield();
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

sealed class StubOutput(List<XboxControllerState> submitted, Action? afterSubmit) : IVirtualGamepadOutput
{
    public bool IsConnected { get; private set; }

    public bool Disposed { get; private set; }

    public ValueTask ConnectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IsConnected = true;
        return ValueTask.CompletedTask;
    }

    public ValueTask SubmitAsync(XboxControllerState state, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        submitted.Add(state);
        afterSubmit?.Invoke();
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        IsConnected = false;
        Disposed = true;
        return ValueTask.CompletedTask;
    }
}
