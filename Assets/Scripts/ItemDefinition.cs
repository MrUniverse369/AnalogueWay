using UnityEngine;

/// <summary>MENUS: which Bag tab an item shows under.</summary>
public enum ItemCategory
{
    Collectible,  // books, maps, relics: Collectibles tab
    PowerUp,      // usable pick-me-ups: Power-ups & Misc tab
    Misc,         // keepsakes and odds and ends: Power-ups & Misc tab
    VillageGoods, // bought in a village shop: Village Goods tab
}

/// <summary>MENUS: what using an item from the Bag does.</summary>
public enum ItemEffect
{
    None,        // a keepsake; it can't be used
    RestoreChi,  // adds `amount` chi (up to the maximum)
    ExtraLife,   // adds `amount` lives (up to the maximum)
}

/// <summary>
/// MENUS: one kind of thing that goes in Harvey's Bag. Talismans are not items: they have their own Talisman
/// assets and the Talisman Holder. Items are saved by <see cref="Id"/>, so keep ids stable once released.
/// </summary>
[CreateAssetMenu(menuName = "HNL/Item", fileName = "NewItem")]
public class ItemDefinition : ScriptableObject
{
    [Tooltip("Saved in save files. Leave empty to use the asset name.")]
    [SerializeField] private string id;
    [SerializeField] private string displayName;
    [SerializeField, TextArea(2, 4)] private string description;
    [SerializeField] private Sprite icon;
    [SerializeField] private ItemCategory category;
    [Tooltip("Only one can ever be owned (books, maps, keepsakes). Pickups of an owned unique item don't appear.")]
    [SerializeField] private bool unique = true;
    [SerializeField] private ItemEffect effect;
    [SerializeField, Min(0)] private float amount;
    [Tooltip("Price in coins in a village shop.")]
    [SerializeField, Min(0)] private int price;

    public string Id => string.IsNullOrEmpty(id) ? name : id;
    public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
    public string Description => description;
    public Sprite Icon => icon;
    public ItemCategory Category => category;
    public bool Unique => unique;
    public ItemEffect Effect => effect;
    public float Amount => amount;
    public int Price => price;
    public bool Usable => effect != ItemEffect.None;

    /// <summary>Applies the effect. Returns false (and changes nothing) when it would do nothing, e.g. at full chi.</summary>
    public bool TryUse(out string message)
    {
        switch (effect)
        {
            case ItemEffect.RestoreChi:
                if (PlayerController.chi >= GameSession.StartingChi - 0.5f) { message = "CHI IS ALREADY FULL"; return false; }
                PlayerController.chi = Mathf.Min(GameSession.StartingChi, PlayerController.chi + amount);
                message = "CHI RESTORED";
                return true;
            case ItemEffect.ExtraLife:
                if (!GameSession.HealingAllowed) { message = "NO HEALING IN NINE LIVES"; return false; } // NINELIVES:
                if (HurtPlayer.pLivesCount >= GameSession.StartingLives) { message = "LIVES ARE ALREADY FULL"; return false; }
                HurtPlayer.pLivesCount = Mathf.Min(GameSession.StartingLives, HurtPlayer.pLivesCount + Mathf.Max(1, (int)amount));
                message = amount >= GameSession.StartingLives ? "HEALTH FULLY RESTORED" : amount >= 2 ? "HEALTH RESTORED" : "A LIFE RESTORED"; // SHOPFOOD:
                return true;
            default:
                message = "";
                return false;
        }
    }
}
