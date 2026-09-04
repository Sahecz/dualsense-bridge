namespace DualSenseBridge.Core;

public enum XboxDPad
{
    Neutral,
    Up,
    UpRight,
    Right,
    DownRight,
    Down,
    DownLeft,
    Left,
    UpLeft,
}

[Flags]
public enum XboxButtons
{
    None = 0,
    A = 1 << 0,
    B = 1 << 1,
    X = 1 << 2,
    Y = 1 << 3,
    LeftBumper = 1 << 4,
    RightBumper = 1 << 5,
    Back = 1 << 6,
    Start = 1 << 7,
    LeftStick = 1 << 8,
    RightStick = 1 << 9,
    Guide = 1 << 10,
}

public readonly record struct NormalizedStick(float X, float Y);

public readonly record struct NormalizedTriggers(float Left, float Right);

public readonly record struct XboxControllerState(
    NormalizedStick LeftStick,
    NormalizedStick RightStick,
    NormalizedTriggers Triggers,
    XboxDPad DPad,
    XboxButtons Buttons);
