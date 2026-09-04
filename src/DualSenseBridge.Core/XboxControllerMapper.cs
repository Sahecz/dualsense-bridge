namespace DualSenseBridge.Core;

public static class XboxControllerMapper
{
    public static XboxControllerState Map(ControllerState source) =>
        new(
            new NormalizedStick(Normalize(source.LeftStick.X), Normalize(source.LeftStick.Y)),
            new NormalizedStick(Normalize(source.RightStick.X), Normalize(source.RightStick.Y)),
            new NormalizedTriggers(source.Triggers.Left / 255f, source.Triggers.Right / 255f),
            MapDPad(source.DPad),
            MapButtons(source.Buttons));

    private static float Normalize(byte value) => value / 255f;

    private static XboxDPad MapDPad(DPadDirection direction) => direction switch
    {
        DPadDirection.Up => XboxDPad.Up,
        DPadDirection.UpRight => XboxDPad.UpRight,
        DPadDirection.Right => XboxDPad.Right,
        DPadDirection.DownRight => XboxDPad.DownRight,
        DPadDirection.Down => XboxDPad.Down,
        DPadDirection.DownLeft => XboxDPad.DownLeft,
        DPadDirection.Left => XboxDPad.Left,
        DPadDirection.UpLeft => XboxDPad.UpLeft,
        _ => XboxDPad.Neutral,
    };

    private static XboxButtons MapButtons(GamepadButtons source)
    {
        var result = XboxButtons.None;
        Add(source, GamepadButtons.Cross, XboxButtons.A, ref result);
        Add(source, GamepadButtons.Circle, XboxButtons.B, ref result);
        Add(source, GamepadButtons.Square, XboxButtons.X, ref result);
        Add(source, GamepadButtons.Triangle, XboxButtons.Y, ref result);
        Add(source, GamepadButtons.L1, XboxButtons.LeftBumper, ref result);
        Add(source, GamepadButtons.R1, XboxButtons.RightBumper, ref result);
        Add(source, GamepadButtons.Create, XboxButtons.Back, ref result);
        Add(source, GamepadButtons.Options, XboxButtons.Start, ref result);
        Add(source, GamepadButtons.L3, XboxButtons.LeftStick, ref result);
        Add(source, GamepadButtons.R3, XboxButtons.RightStick, ref result);
        Add(source, GamepadButtons.Ps, XboxButtons.Guide, ref result);
        return result;
    }

    private static void Add(
        GamepadButtons source,
        GamepadButtons sourceButton,
        XboxButtons targetButton,
        ref XboxButtons result)
    {
        if (source.HasFlag(sourceButton))
        {
            result |= targetButton;
        }
    }
}
