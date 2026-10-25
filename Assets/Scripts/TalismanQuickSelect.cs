using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// L2CYCLE (2026-10-24): L2 (V on the keyboard) swaps talisman slot 1 (Z / Square, the red gem on the HUD) straight to
/// the next talisman, with no menu and nothing to confirm: from its home (the talisman the Talisman Holder put there)
/// through every owned talisman not equipped in slots 2 and 3, then back to the home (Inventory.CycleFirstSlot).
/// The new talisman is equipped at once through the usual equipment path (PowerUpManager ends the old one if it was
/// running and arms the new one; slots 2 and 3 are untouched) and the first gem on the HUD takes its colour
/// (TalismanHUD). Nothing to cycle to: a soft "blocked" sound and nothing changes.
/// Replaces the hold-L1 quick select of earlier the same day: L1 / Q is the quick item again (QuickItemHUD).
/// Added to the menus canvas by HarveyMenus in every level (not the tutorial, whose loadout is fixed).
/// </summary>
public class TalismanQuickSelect : MonoBehaviour
{
    public static TalismanQuickSelect Build(RectTransform canvas) => canvas.gameObject.AddComponent<TalismanQuickSelect>();

    private static bool Blocked =>
        HarveyMenus.IsOpen || MenueControl.isGamePaused || DialogueBox.BlocksPause || LevelLoader.Busy || Time.timeScale == 0f;

    private void Update()
    {
        if (!Pressed() || Blocked || !PowerUpManager.UsesHolderLoadout) return;
        HNLCatalog cat = Inventory.Catalog;
        HarveyMenus.PlayUiSound(Inventory.CycleFirstSlot() ? cat.moveSound : cat.blockedSound);
    }

    private static bool Pressed()
    {
        Keyboard kb = Keyboard.current;
        Gamepad pad = Gamepad.current;
        return (kb != null && kb.vKey.wasPressedThisFrame) || (pad != null && pad.leftTrigger.wasPressedThisFrame);
    }
}
