using UnityEngine.InputSystem;

/// <summary>
/// CHEST: the action button for things Harvey uses in the world (open a chest, take a boss's treasure): W, Up or E
/// on the keyboard, the d-pad or left stick pushed up on a pad. The same keys as talking to villagers and the shop.
/// Space and Triangle are Jump, so they are never used here.
/// </summary>
public static class ActionButton
{
    public static bool Held()
    {
        Keyboard kb = Keyboard.current;
        if (kb != null && (kb.wKey.isPressed || kb.upArrowKey.isPressed || kb.eKey.isPressed)) return true;
        Gamepad pad = Gamepad.current;
        return pad != null && (pad.dpad.up.isPressed || pad.leftStick.ReadValue().y > 0.7f);
    }

    /// <summary>The prompt prefix that names the keys.</summary>
    public const string Prompt = "UP / E: ";
}
