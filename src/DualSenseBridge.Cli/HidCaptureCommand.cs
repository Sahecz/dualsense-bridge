using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using DualSenseBridge.Core;

internal static class HidCaptureCommand
{
    private static readonly CaptureStep[] DefaultPlan =
    [
        new("neutral", "Suelta todos los botones y deja ambos sticks en reposo."),
        new("cross_pressed", "Mantén presionado Cross."),
        new("circle_pressed", "Mantén presionado Circle."),
        new("square_pressed", "Mantén presionado Square."),
        new("triangle_pressed", "Mantén presionado Triangle."),
        new("dpad_up", "Mantén la cruceta hacia arriba."),
        new("dpad_up_right", "Mantén la cruceta arriba + derecha."),
        new("dpad_right", "Mantén la cruceta hacia la derecha."),
        new("dpad_down_right", "Mantén la cruceta abajo + derecha."),
        new("dpad_down", "Mantén la cruceta hacia abajo."),
        new("dpad_down_left", "Mantén la cruceta abajo + izquierda."),
        new("dpad_left", "Mantén la cruceta hacia la izquierda."),
        new("dpad_up_left", "Mantén la cruceta arriba + izquierda."),
        new("l1_pressed", "Mantén presionado L1."),
        new("r1_pressed", "Mantén presionado R1."),
        new("l2_full", "Mantén L2 presionado completamente."),
        new("r2_full", "Mantén R2 presionado completamente."),
        new("create_pressed", "Mantén presionado Create."),
        new("options_pressed", "Mantén presionado Options."),
        new("l3_pressed", "Mantén presionado L3 sin inclinar el stick."),
        new("r3_pressed", "Mantén presionado R3 sin inclinar el stick."),
        new("ps_pressed", "Mantén presionado brevemente el botón PS."),
        new("touchpad_pressed", "Mantén presionado el touchpad."),
        new("mute_pressed", "Mantén presionado el botón de silencio."),
        new("left_stick_up", "Mantén el stick izquierdo completamente arriba."),
        new("left_stick_right", "Mantén el stick izquierdo completamente a la derecha."),
        new("left_stick_down", "Mantén el stick izquierdo completamente abajo."),
        new("left_stick_left", "Mantén el stick izquierdo completamente a la izquierda."),
        new("right_stick_up", "Mantén el stick derecho completamente arriba."),
        new("right_stick_right", "Mantén el stick derecho completamente a la derecha."),
        new("right_stick_down", "Mantén el stick derecho completamente abajo."),
        new("right_stick_left", "Mantén el stick derecho completamente a la izquierda."),
    ];

    public static async Task<int> RunAsync(
        string[] args,
        IRawHidReportSource source,
        CancellationToken cancellationToken)
    {
        var samplesPerAction = ReadIntOption(args, "--samples", 128, 8, 4096);
        var warmupSamples = ReadIntOption(args, "--warmup", 16, 0, 1024);
        var requestedOutput = ReadStringOption(args, "--output");
        var singleLabel = ReadStringOption(args, "--label");
        var singleInstructions = ReadStringOption(args, "--instructions")
            ?? "Coloca el control en el estado indicado y mantenlo así.";
        var plan = singleLabel is null
            ? DefaultPlan
            : [new CaptureStep(singleLabel, singleInstructions)];
        var outputPath = ResolveOutputPath(requestedOutput);

        Console.WriteLine("Captura HID cruda de DualSense");
        Console.WriteLine("No se guardarán rutas HID, MAC, usuario, nombre del equipo ni número de serie.");
        Console.WriteLine($"Muestras por acción: {samplesPerAction}; descarte inicial: {warmupSamples}.");
        Console.WriteLine("Presiona Ctrl+C para cancelar.\n");

        IRawHidReportSession? session = null;
        while (session is null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            session = await source.TryConnectAsync(cancellationToken);
            if (session is null)
            {
                Console.WriteLine("Esperando un DualSense por USB o Bluetooth...");
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
        }

        await using (session)
        {
            var actions = new List<HidActionCapture>();
            var document = CreateDocument(session.Device, actions);
            await SaveAsync(outputPath, document, cancellationToken);

            Console.WriteLine($"Detectado: {session.Device.Model} " +
                $"({session.Device.VendorId:X4}:{session.Device.ProductId:X4}).");
            Console.WriteLine($"Guardado incremental: {outputPath}\n");

            await using var reports = session.ReadReportsAsync(cancellationToken)
                .GetAsyncEnumerator(cancellationToken);

            for (var index = 0; index < plan.Length; index++)
            {
                var step = plan[index];
                Console.WriteLine($"[{index + 1}/{plan.Length}] {step.Label}");
                Console.WriteLine(step.Instructions);
                Console.Write("Cuando estés listo, presiona Enter y mantén la posición... ");
                Console.ReadLine();

                for (var warmup = 0; warmup < warmupSamples; warmup++)
                {
                    await RequireNextAsync(reports);
                }

                var samples = new List<HidReportSample>(samplesPerAction);
                var connections = new HashSet<ConnectionKind>();
                for (var sampleIndex = 0; sampleIndex < samplesPerAction; sampleIndex++)
                {
                    var report = await RequireNextAsync(reports);
                    connections.Add(DualSenseReportParser.DetectConnection(report.Bytes));
                    samples.Add(new HidReportSample(
                        report.Sequence,
                        report.ElapsedMicroseconds,
                        report.Bytes.Length == 0 ? "" : report.Bytes[0].ToString("X2"),
                        report.Bytes.Length,
                        Convert.ToHexString(report.Bytes)));
                }

                var connection = connections.Count == 1
                    ? connections.Single().ToString()
                    : "Mixed";
                actions.Add(new HidActionCapture(step.Label, step.Instructions, connection, samples));
                document = document with { Actions = actions.ToArray() };
                await SaveAsync(outputPath, document, cancellationToken);
                Console.WriteLine($"Capturadas {samples.Count} muestras ({connection}). Ya puedes soltar.\n");
            }

            Console.WriteLine($"Captura terminada: {outputPath}");
            return 0;
        }
    }

    private static HidCaptureDocument CreateDocument(
        RawHidDeviceInfo device,
        IReadOnlyList<HidActionCapture> actions)
    {
        var toolVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
        return new HidCaptureDocument(
            HidCaptureDocument.CurrentSchemaVersion,
            toolVersion,
            new HidCaptureDevice(
                device.VendorId.ToString("X4"),
                device.ProductId.ToString("X4"),
                device.Model,
                device.MaximumInputReportLength,
                device.FirmwareFeatureReport is null
                    ? null
                    : Convert.ToHexString(device.FirmwareFeatureReport)),
            new HidCaptureEnvironment(
                Environment.OSVersion.VersionString,
                RuntimeInformation.FrameworkDescription),
            actions);
    }

    private static async ValueTask<RawHidReport> RequireNextAsync(
        IAsyncEnumerator<RawHidReport> reports)
    {
        if (!await reports.MoveNextAsync())
        {
            throw new IOException("El DualSense se desconectó durante la captura.");
        }

        return reports.Current;
    }

    private static async Task SaveAsync(
        string outputPath,
        HidCaptureDocument document,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(outputPath)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = outputPath + ".tmp";
        await using (var stream = File.Create(temporaryPath))
        {
            await JsonSerializer.SerializeAsync(
                stream,
                document,
                new JsonSerializerOptions { WriteIndented = true },
                cancellationToken);
        }

        File.Move(temporaryPath, outputPath, overwrite: true);
    }

    private static string ResolveOutputPath(string? requestedOutput)
    {
        var path = requestedOutput ?? Path.Combine(
            "captures",
            $"dualsense-capture-{Guid.NewGuid():N}.json");
        return Path.GetFullPath(path);
    }

    private static string? ReadStringOption(string[] args, string name)
    {
        var index = Array.FindIndex(args, value => value.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            return null;
        }

        if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
        {
            throw new ArgumentException($"Falta un valor para {name}.");
        }

        return args[index + 1];
    }

    private static int ReadIntOption(
        string[] args,
        string name,
        int defaultValue,
        int minimum,
        int maximum)
    {
        var raw = ReadStringOption(args, name);
        if (raw is null)
        {
            return defaultValue;
        }

        if (!int.TryParse(raw, out var value) || value < minimum || value > maximum)
        {
            throw new ArgumentOutOfRangeException(
                name,
                $"{name} debe ser un entero entre {minimum} y {maximum}.");
        }

        return value;
    }

    private sealed record CaptureStep(string Label, string Instructions);
}
