using DualSenseBridge.Core;
using DualSenseBridge.HidMaestro;
using HidSharp;

Console.OutputEncoding = System.Text.Encoding.UTF8;
using var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    shutdown.Cancel();
};

if (args.Contains("--install-driver", StringComparer.OrdinalIgnoreCase))
{
    Console.WriteLine("Instalando los componentes de HIDMaestro...");
    HidMaestroVirtualGamepadOutput.InstallDriver();
    Console.WriteLine("HIDMaestro quedó instalado correctamente.");
    return;
}

if (args.Contains("--uninstall-driver", StringComparer.OrdinalIgnoreCase))
{
    Console.WriteLine("Retirando dispositivos y paquetes de HIDMaestro...");
    HidMaestroVirtualGamepadOutput.UninstallDriver();
    Console.WriteLine("Los dispositivos y paquetes HIDMaestro fueron retirados.");
    return;
}

if (args.Contains("--capture-hid", StringComparer.OrdinalIgnoreCase))
{
    try
    {
        Environment.ExitCode = await HidCaptureCommand.RunAsync(
            args,
            new HidSharpDualSenseRawReportSource(),
            shutdown.Token);
    }
    catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
    {
        Console.WriteLine("\nCaptura cancelada.");
    }
    catch (Exception error)
    {
        Console.Error.WriteLine($"La captura falló: {error.Message}");
        Environment.ExitCode = 1;
    }

    return;
}

if (args.Contains("--bridge", StringComparer.OrdinalIgnoreCase))
{
    await RunBridgeAsync(
        new ParsedControllerInputSource(new HidSharpDualSenseRawReportSource()),
        simulated: false,
        shutdown.Token);
    return;
}

if (args.Contains("--simulate", StringComparer.OrdinalIgnoreCase))
{
    if (args.Contains("--virtual", StringComparer.OrdinalIgnoreCase))
    {
        await RunBridgeAsync(
            new SimulatedControllerInputSource(),
            simulated: true,
            shutdown.Token);
        return;
    }

    var previewXbox = args.Contains("--preview-xbox", StringComparer.OrdinalIgnoreCase);
    await RunSimulatorAsync(previewXbox, shutdown.Token);
    return;
}

await RunControllerAsync(shutdown.Token);

static async Task RunBridgeAsync(
    IControllerInputSource inputSource,
    bool simulated,
    CancellationToken cancellationToken)
{
    Console.WriteLine(simulated
        ? "Puente de prueba: la conexión y desconexión del DualSense serán simuladas."
        : "Puente real: esperando un DualSense por USB o Bluetooth.");
    Console.WriteLine("Presiona Ctrl+C para terminar limpiamente.\n");

    var options = simulated
        ? new BridgeWorkerOptions(TimeSpan.FromMilliseconds(250), TimeSpan.FromSeconds(1))
        : BridgeWorkerOptions.Default;
    var worker = new BridgeWorker(
        inputSource,
        static () => new HidMaestroVirtualGamepadOutput(),
        options);
    worker.StatusChanged += update =>
    {
        var prefix = update.Status switch
        {
            BridgeStatus.Bridging => "OK",
            BridgeStatus.Recovering => "AVISO",
            BridgeStatus.Stopped => "FIN",
            _ => "INFO",
        };

        Console.WriteLine($"[{prefix}] {update.Message}");
        if (update.Error is not null)
        {
            Console.WriteLine($"        {update.Error.Message}");
        }
    };

    await worker.RunAsync(cancellationToken);
}

static async Task RunSimulatorAsync(bool previewXbox, CancellationToken cancellationToken)
{
    Console.WriteLine("Modo simulador: generando entradas sin un DualSense físico.");
    Console.WriteLine(previewXbox
        ? "Salida: vista previa del mapeo Xbox."
        : "Salida: estado interno del DualSense.");
    Console.WriteLine("Presiona Ctrl+C para terminar.\n");

    try
    {
        foreach (var (label, state) in ControllerSimulator.States())
        {
            if (previewXbox)
            {
                RenderXbox(XboxControllerMapper.Map(state), $"prueba={label,-20}  ");
            }
            else
            {
                Render(state, $"prueba={label,-20}  ");
            }
            await Task.Delay(700, cancellationToken);
        }
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("\nSimulación terminada.");
    }
}

static void RenderXbox(XboxControllerState state, string prefix = "")
{
    var line = string.Join("  ",
        "salida=Xbox360",
        $"LX={state.LeftStick.X:F3}",
        $"LY={state.LeftStick.Y:F3}",
        $"RX={state.RightStick.X:F3}",
        $"RY={state.RightStick.Y:F3}",
        $"LT={state.Triggers.Left:F3}",
        $"RT={state.Triggers.Right:F3}",
        $"dpad={state.DPad}",
        $"botones={(state.Buttons == XboxButtons.None ? "-" : state.Buttons)}");

    Console.Write($"\r{prefix}{line}".PadRight(190));
}

static async Task RunControllerAsync(CancellationToken cancellationToken)
{
    var device = DeviceList.Local.GetHidDevices(DualSenseDevice.SonyVendorId)
        .FirstOrDefault(candidate => DualSenseDevice.IsSupported(candidate.VendorID, candidate.ProductID));

    if (device is null)
    {
        Console.Error.WriteLine("No encontré un DualSense conectado. Usa --simulate o conecta el control por USB/Bluetooth.");
        Environment.ExitCode = 2;
        return;
    }

    if (!device.TryOpen(out var stream))
    {
        Console.Error.WriteLine("Encontré el DualSense, pero Windows no permitió abrir su interfaz HID.");
        Environment.ExitCode = 3;
        return;
    }

    using (stream)
    {
        Console.WriteLine($"Encontrado: {DualSenseDevice.ModelName(device.ProductID)}");
        Console.WriteLine("Leyendo entradas. Presiona Ctrl+C para terminar.\n");
        var buffer = new byte[device.GetMaxInputReportLength()];

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var bytesRead = await stream.ReadAsync(buffer, cancellationToken);
                Render(DualSenseReportParser.Parse(buffer.AsSpan(0, bytesRead)));
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("\nLectura terminada.");
        }
        catch (Exception error)
        {
            Console.Error.WriteLine($"\nSe perdió la conexión con el control: {error.Message}");
            Environment.ExitCode = 1;
        }
    }
}

static void Render(ControllerState state, string prefix = "")
{
    var line = string.Join("  ",
        $"conexión={state.Connection}",
        $"LX={state.LeftStick.X,3}",
        $"LY={state.LeftStick.Y,3}",
        $"RX={state.RightStick.X,3}",
        $"RY={state.RightStick.Y,3}",
        $"L2={state.Triggers.Left,3}",
        $"R2={state.Triggers.Right,3}",
        $"dpad={state.DPad}",
        $"botones={(state.Buttons == GamepadButtons.None ? "-" : state.Buttons)}");

    Console.Write($"\r{prefix}{line}".PadRight(190));
}
