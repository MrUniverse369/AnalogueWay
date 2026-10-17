using TMPro;
using UnityEngine;

/// <summary>
/// MENUS: every talisman and item in the game, plus the look of the Talisman Holder, Bag and shop. Lives at
/// Assets/Resources/HNLCatalog.asset so the save code can turn saved ids back into assets from any scene.
/// Made by Tools/HNL/Build Talisman and Bag Content.
/// </summary>
[CreateAssetMenu(menuName = "HNL/Catalog", fileName = "HNLCatalog")]
public class HNLCatalog : ScriptableObject
{
    public const string ResourcePath = "HNLCatalog";

    [Header("Talismans (Holder order)")]
    public Talisman[] talismans;
    [Tooltip("What a new game starts with equipped, in slot order (Z, X, C). These are also owned from the start.")]
    public Talisman[] startingLoadout;

    [Header("Items")]
    public ItemDefinition[] items;
    [Tooltip("Sold in village shops, in shelf order.")]
    public ItemDefinition[] villageStock;

    [Header("Menu art (Tools/UI/menus_art.py)")]
    public TMP_FontAsset font;
    public Sprite holderPanel;
    public Sprite bagPanel;
    public Sprite shopPanel;
    public Sprite frame;           // 9-slice HUD plate (border 4)
    public Sprite card;            // talisman card / shop row, 9-slice (border 4)
    public Sprite cardSelected;
    public Sprite socket;          // equipped slot
    public Sprite socketSelected;
    public Sprite pocket;          // bag slot
    public Sprite pocketSelected;
    public Sprite tab;
    public Sprite tabSelected;
    public Sprite lockedTalisman;
    public Sprite coin;

    [Header("Pause menu")]
    public Sprite pauseFrame;          // 9-slice card (border 10)
    public Sprite pausePlaque;         // 9-slice title plaque (border 5)
    public Sprite menuButton;          // the main menu's button plates (9-slice, border 5)
    public Sprite menuButtonSelected;
    public Sprite menuButtonPressed;
    public Sprite paw;                 // the main menu's paw pointer
    [Tooltip("Resume, Talismans, Bag, Main Menu, Quit.")]
    public Sprite[] pauseIcons;

    [Header("Sounds")]
    public AudioClip moveSound;
    public AudioClip selectSound;
    public AudioClip backSound;
    public AudioClip blockedSound;
    public AudioClip equipSound;
    public AudioClip buySound;
    public AudioClip pickupSound;

    public Talisman TalismanById(string id)
    {
        if (string.IsNullOrEmpty(id) || talismans == null) return null;
        foreach (Talisman t in talismans)
            if (t != null && t.name == id) return t;
        return null;
    }

    public ItemDefinition ItemById(string id)
    {
        if (string.IsNullOrEmpty(id) || items == null) return null;
        foreach (ItemDefinition item in items)
            if (item != null && item.Id == id) return item;
        return null;
    }
}
