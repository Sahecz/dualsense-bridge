namespace DualSenseBridge.Core;

public static class DualSenseReportParser
{
    private static readonly DPadDirection[] DPadDirections =
    [
        DPadDirection.Up,
        DPadDirection.UpRight,
        DPadDirection.Right,
        DPadDirection.DownRight,
        DPadDirection.Down,
        DPadDirection.DownLeft,
        DPadDirection.Left,
        DPadDirection.UpLeft,
        DPadDirection.Neutral,
    ];

    public static ControllerState Parse(ReadOnlySpan<byte> report)
    {
        if (report.Length < 10)
        {
            throw new ArgumentException("El reporte HID debe contener al menos 10 bytes.", nameof(report));
        }

        var connection = DetectConnection(report);
        var layout = connection switch
        {
            ConnectionKind.BluetoothExtended => new ReportLayout(3, 7, 10),
            ConnectionKind.BluetoothBasic => new ReportLayout(1, null, 5),
            ConnectionKind.Usb => new ReportLayout(1, 5, 8),
            _ => throw new ArgumentOutOfRangeException(nameof(report)),
        };

        var buttons0 = report[layout.Buttons];
        var buttons1 = report[layout.Buttons + 1];
        var buttons2 = report[layout.Buttons + 2];
        var dPadValue = buttons0 & 0x0F;
        var leftTrigger = layout.Triggers is int triggerIndex
            ? report[triggerIndex]
            : (byte)((buttons1 & 0x04) != 0 ? 255 : 0);
        var rightTrigger = layout.Triggers is int triggerIndex2
            ? report[triggerIndex2 + 1]
            : (byte)((buttons1 & 0x08) != 0 ? 255 : 0);

        return new ControllerState(
            connection,
            new StickState(report[layout.Axes], report[layout.Axes + 1]),
            new StickState(report[layout.Axes + 2], report[layout.Axes + 3]),
            new TriggerState(leftTrigger, rightTrigger),
            dPadValue < DPadDirections.Length ? DPadDirections[dPadValue] : DPadDirection.Neutral,
            ParseButtons(buttons0, buttons1, buttons2));
    }

    private static ConnectionKind DetectConnection(ReadOnlySpan<byte> report) => report[0] switch
    {
        0x31 => ConnectionKind.BluetoothExtended,
        0x01 when report.Length >= 60 => ConnectionKind.Usb,
        0x01 => ConnectionKind.BluetoothBasic,
        _ => throw new NotSupportedException($"Reporte HID 0x{report[0]:X2} no reconocido."),
    };

    private static GamepadButtons ParseButtons(byte buttons0, byte buttons1, byte buttons2)
    {
        var buttons = GamepadButtons.None;

        Add(buttons0, 0x10, GamepadButtons.Square, ref buttons);
        Add(buttons0, 0x20, GamepadButtons.Cross, ref buttons);
        Add(buttons0, 0x40, GamepadButtons.Circle, ref buttons);
        Add(buttons0, 0x80, GamepadButtons.Triangle, ref buttons);
        Add(buttons1, 0x01, GamepadButtons.L1, ref buttons);
        Add(buttons1, 0x02, GamepadButtons.R1, ref buttons);
        Add(buttons1, 0x04, GamepadButtons.L2, ref buttons);
        Add(buttons1, 0x08, GamepadButtons.R2, ref buttons);
        Add(buttons1, 0x10, GamepadButtons.Create, ref buttons);
        Add(buttons1, 0x20, GamepadButtons.Options, ref buttons);
        Add(buttons1, 0x40, GamepadButtons.L3, ref buttons);
        Add(buttons1, 0x80, GamepadButtons.R3, ref buttons);
        Add(buttons2, 0x01, GamepadButtons.Ps, ref buttons);
        Add(buttons2, 0x02, GamepadButtons.Touchpad, ref buttons);
        Add(buttons2, 0x04, GamepadButtons.Mute, ref buttons);

        return buttons;
    }

    private static void Add(byte source, byte mask, GamepadButtons value, ref GamepadButtons result)
    {
        if ((source & mask) != 0)
        {
            result |= value;
        }
    }

    private readonly record struct ReportLayout(int Axes, int? Triggers, int Buttons);
}
