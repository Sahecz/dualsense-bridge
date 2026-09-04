namespace DualSenseBridge.Core;

public enum ConnectionKind
{
    Usb,
    BluetoothBasic,
    BluetoothExtended,
    Simulated,
}

public enum DPadDirection
{
    Up,
    UpRight,
    Right,
    DownRight,
    Down,
    DownLeft,
    Left,
    UpLeft,
    Neutral,
}

[Flags]
public enum GamepadButtons
{
    None = 0,
    Square = 1 << 0,
    Cross = 1 << 1,
    Circle = 1 << 2,
    Triangle = 1 << 3,
    L1 = 1 << 4,
    R1 = 1 << 5,
    L2 = 1 << 6,
    R2 = 1 << 7,
    Create = 1 << 8,
    Options = 1 << 9,
    L3 = 1 << 10,
    R3 = 1 << 11,
    Ps = 1 << 12,
    Touchpad = 1 << 13,
    Mute = 1 << 14,
}

public readonly record struct StickState(byte X, byte Y);

public readonly record struct TriggerState(byte Left, byte Right);

public readonly record struct ControllerState(
    ConnectionKind Connection,
    StickState LeftStick,
    StickState RightStick,
    TriggerState Triggers,
    DPadDirection DPad,
    GamepadButtons Buttons);
