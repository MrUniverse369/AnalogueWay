using UnityEngine;

/// <summary>
/// MENUS: gives Harvey a talisman when this object switches on. Put it on a boss's treasure (YokaiBoss
/// revealOnDefeat), so beating the boss earns the talisman. It goes into a free holder slot if there is one.
/// </summary>
public class TalismanUnlock : MonoBehaviour
{
    [SerializeField] private Talisman talisman;

    private void OnEnable()
    {
        if (!Application.isPlaying || talisman == null) return;
        if (!Inventory.Unlock(talisman)) return;
        bool equipped = Inventory.SlotOf(talisman) >= 0;
        HarveyMenus.Toast(talisman.Icon, "NEW TALISMAN: " + talisman.DisplayName.ToUpperInvariant(),
            equipped ? "EQUIPPED IN A FREE SLOT" : "EQUIP IT IN THE TALISMAN HOLDER (PAUSE)", 4f);
        HarveyMenus.PlayUiSound(Inventory.Catalog != null ? Inventory.Catalog.equipSound : null);
    }
}
