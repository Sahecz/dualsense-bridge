namespace DualSenseBridge.Core;

public static class ControllerSimulator
{
    public static IEnumerable<(string Label, ControllerState State)> States()
    {
        var sequence = new (string Label, ControllerState State)[]
        {
            ("neutral", State()),
            ("X", State(buttons: GamepadButtons.Cross)),
            ("stick izquierdo", State(leftStick: new StickState(255, 32))),
            ("gatillos", State(triggers: new TriggerState(96, 220), buttons: GamepadButtons.L2 | GamepadButtons.R2)),
            ("cruceta + botones", State(dPad: DPadDirection.UpRight, buttons: GamepadButtons.Square | GamepadButtons.L1)),
        };

        while (true)
        {
            foreach (var item in sequence)
            {
                yield return item;
            }
        }
    }

    private static ControllerState State(
        StickState? leftStick = null,
        StickState? rightStick = null,
        TriggerState? triggers = null,
        DPadDirection dPad = DPadDirection.Neutral,
        GamepadButtons buttons = GamepadButtons.None) =>
        new(
            ConnectionKind.Simulated,
            leftStick ?? new StickState(128, 128),
            rightStick ?? new StickState(128, 128),
            triggers ?? new TriggerState(0, 0),
            dPad,
            buttons);
}
