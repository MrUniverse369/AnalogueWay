using UnityEngine;

public enum ZoneNodeKind { Level, Village, Boss, Goal }

/// <summary>
/// ZONEMAP: one stop on the zone-select map. Nodes are joined in the order ZoneSelectMap lists them;
/// bendsToNext holds the path corners (world positions) between this node and the next one.
/// </summary>
public class ZoneMapNode : MonoBehaviour
{
    [Tooltip("Scene loaded when this node is picked. Must be in Build Settings.")]
    public string sceneName;
    public string zoneName;
    public string levelName;
    public ZoneNodeKind kind = ZoneNodeKind.Level;
    [Tooltip("Colours the level name in the label.")]
    public Color zoneColour = Color.white;
    [Tooltip("Icon while the level is open (the zone's gem). Empty = ZoneSelectMap's default, tinted by zoneColour.")]
    public Sprite openIcon;
    [Tooltip("Path corners between this node and the next one, in world space. Must match the map art.")]
    public Vector2[] bendsToNext = new Vector2[0];

    [HideInInspector] public SpriteRenderer icon;

    private void Awake()
    {
        icon = GetComponent<SpriteRenderer>();
    }
}
