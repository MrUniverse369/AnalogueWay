using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// SKILLS (2026-10-20): the Skill Tree menu (pause menu > SKILLS). Two small trees side by side, Natural Abilities
/// and Talisman Skills, each a root skill that opens two more, with deeper skills under them (COMBOLOCK: Combo Mastery
/// sits under Dash Power, beside Double Jump). Each node is a round
/// plate with the skill's icon and a pip per level (yellow = learned). Lines between nodes turn yellow once the skill
/// they lead to is learned; a skill that can't be learned yet is dimmed. The note on the right says what the skill
/// under the cursor does now and next, and what the next level costs or still needs. Cross/Enter learns it, paid in
/// crystals (gems from treasure chests: SkillTree has the rules). HNL UI style: MenuUI palette and helpers only.
/// </summary>
public class SkillTreeView : MenuView
{
    private const float RootY = 36f, ChildY = 4f, GrandY = -28f, Spread = 19f;
    private static readonly float[] BranchX = { -104f, -26f };
    private static readonly string[] BranchNames = { "NATURAL ABILITIES", "TALISMAN SKILLS" };

    private sealed class Node
    {
        public SkillDef def;
        public Vector2 pos;
        public Button button;
        public Image icon;
        public Image[] pips;
        public readonly List<Image> lineIn = new List<Image>(); // the lines leading into this node
    }

    private readonly List<Node> nodes = new List<Node>();
    private readonly Sprite plate, plateSelected;
    private readonly Image detailIcon;
    private readonly TMP_Text crystalsText, detailName, detailLevel, detailText, detailNow, detailNext, detailCost;
    private int cursor;

    public SkillTreeView(RectTransform parent) : base("SkillTree", parent, Inventory.Catalog.bagPanel)
    {
        var panel = Panel;
        plate = Resources.Load<Sprite>("UI/Skills/SkillNode");
        plateSelected = Resources.Load<Sprite>("UI/Skills/SkillNode_Selected");

        MenuUI.Title(panel, 71, 160, "SKILL TREE");
        MenuUI.Image("CrystalPlate", panel, new Vector2(110, 71), new Vector2(46, 13), cat.frame, true);
        MenuUI.Image("CrystalIcon", panel, new Vector2(95, 71), new Vector2(10, 10), SkillTree.CrystalIcon);
        crystalsText = MenuUI.Text("Crystals", panel, new Vector2(114, 71), new Vector2(26, 8), 5, MenuUI.Yellow, TextAlignmentOptions.Right, "");

        for (int b = 0; b < BranchNames.Length; b++)
            MenuUI.Text("Branch" + b, panel, new Vector2(BranchX[b], 54), new Vector2(80, 8), MenuUI.SmallSize, MenuUI.Muted, TextAlignmentOptions.Center, BranchNames[b]);

        // the layout: [root, left child, right child, the deeper skill] per branch
        Place(SkillTree.StompPower, 0, 0f, RootY);
        Place(SkillTree.DashPower, 0, -Spread, ChildY);
        Place(SkillTree.JumpPower, 0, Spread, ChildY);
        Place(SkillTree.DoubleJump, 0, Spread, GrandY);
        Place(SkillTree.ComboMastery, 0, -Spread, GrandY); // COMBOLOCK:
        Place(SkillTree.ChiFocus, 1, 0f, RootY);
        Place(SkillTree.LastingCharms, 1, -Spread, ChildY);
        Place(SkillTree.QuickRecharge, 1, Spread, ChildY);
        Place(SkillTree.SkyLeap, 1, -Spread, GrandY);

        // lines first (under the nodes), then the nodes
        var lines = MenuUI.Rect("Lines", panel, Vector2.zero, Vector2.zero);
        foreach (Node n in nodes) if (n.def.requires != null) Connect(lines, NodeOf(n.def.requires), n);
        for (int i = 0; i < nodes.Count; i++) BuildNode(panel, nodes[i], i);

        MenuUI.Image("Note", panel, new Vector2(82, -4), new Vector2(118, 104), cat.frame, true);
        detailIcon = MenuUI.Image("Icon", panel, new Vector2(36, 36), new Vector2(16, 16), null);
        detailName = MenuUI.Text("Name", panel, new Vector2(96, 39), new Vector2(84, 8), 5, MenuUI.Yellow, TextAlignmentOptions.Left, "");
        detailLevel = MenuUI.Text("Level", panel, new Vector2(96, 31), new Vector2(84, 7), 5, MenuUI.Muted, TextAlignmentOptions.Left, "");
        detailText = MenuUI.Text("Text", panel, new Vector2(82, 13), new Vector2(106, 22), 5, MenuUI.Cream, TextAlignmentOptions.TopLeft, "", true);
        detailNow = MenuUI.Text("Now", panel, new Vector2(82, -9), new Vector2(106, 14), 5, MenuUI.Cream, TextAlignmentOptions.TopLeft, "", true);
        detailNext = MenuUI.Text("Next", panel, new Vector2(82, -25), new Vector2(106, 14), 5, MenuUI.Yellow, TextAlignmentOptions.TopLeft, "", true);
        detailCost = MenuUI.Text("Cost", panel, new Vector2(82, -46), new Vector2(106, 14), 5, MenuUI.Yellow, TextAlignmentOptions.Center, "", true);
        Hint("CROSS/ENTER: LEARN   CIRCLE/ESC: BACK   CRYSTALS: GEMS FROM TREASURE CHESTS", new Vector2(0, -71));

        WireNavigation();
    }

    private void Place(string id, int branch, float dx, float y)
    {
        SkillDef def = SkillTree.Def(id);
        if (def != null) nodes.Add(new Node { def = def, pos = new Vector2(BranchX[branch] + dx, y) });
    }

    private Node NodeOf(string id)
    {
        foreach (Node n in nodes) if (n.def.id == id) return n;
        return null;
    }

    // an elbow: down from the parent, across, and down into the child (straight down when they line up)
    private void Connect(RectTransform parent, Node from, Node to)
    {
        if (from == null) return;
        float bend = Mathf.Round((from.pos.y - 11f - 4f + to.pos.y + 11f) / 2f); // halfway between the parent's pips and the child's top
        to.lineIn.Add(Line(parent, new Vector2(from.pos.x, from.pos.y - 16f), new Vector2(from.pos.x, bend))); // starts under the pips
        if (!Mathf.Approximately(from.pos.x, to.pos.x)) to.lineIn.Add(Line(parent, new Vector2(from.pos.x, bend), new Vector2(to.pos.x, bend)));
        to.lineIn.Add(Line(parent, new Vector2(to.pos.x, bend), new Vector2(to.pos.x, to.pos.y)));
    }

    private static Image Line(RectTransform parent, Vector2 a, Vector2 b)
    {
        Vector2 size = new Vector2(Mathf.Abs(b.x - a.x) + 1f, Mathf.Abs(b.y - a.y) + 1f);
        Image line = MenuUI.Image("Line", parent, (a + b) / 2f, size, null);
        line.color = MenuUI.Line;
        return line;
    }

    private void BuildNode(RectTransform panel, Node n, int index)
    {
        var rt = MenuUI.Rect(n.def.id, panel, n.pos, new Vector2(22, 22));
        n.button = MenuUI.Button(rt, plate, plateSelected, false, () => ShowNode(index), () => Learn(index));
        n.icon = MenuUI.Image("Icon", rt, Vector2.zero, new Vector2(16, 16), Resources.Load<Sprite>("UI/Skills/" + n.def.icon));
        n.pips = new Image[n.def.MaxLevel];
        float x0 = -(n.def.MaxLevel - 1) * 2f;
        for (int i = 0; i < n.pips.Length; i++)
        {
            n.pips[i] = MenuUI.Image("Pip" + i, rt, new Vector2(x0 + 4f * i, -14f), new Vector2(3, 2), null);
            n.pips[i].color = MenuUI.Line;
        }
    }

    // each direction goes to the nearest node that way (straight lines count for more than diagonals)
    private void WireNavigation()
    {
        foreach (Node n in nodes)
            MenuUI.Link(n.button, Nearest(n, Vector2.up), Nearest(n, Vector2.down), Nearest(n, Vector2.left), Nearest(n, Vector2.right));
    }

    private Selectable Nearest(Node from, Vector2 dir)
    {
        Selectable best = null;
        float bestScore = float.MaxValue;
        foreach (Node n in nodes)
        {
            if (n == from) continue;
            Vector2 d = n.pos - from.pos;
            float along = Vector2.Dot(d, dir);
            if (along <= 1f) continue;
            float score = along + 2f * Mathf.Abs(Vector2.Dot(d, new Vector2(dir.y, -dir.x)));
            if (score < bestScore) { bestScore = score; best = n.button; }
        }
        return best;
    }

    protected override void OnShow()
    {
        Refresh();
        cursor = Mathf.Clamp(cursor, 0, nodes.Count - 1);
        MenuUI.Select(nodes[cursor].button);
        ShowNode(cursor);
    }

    private void Refresh()
    {
        crystalsText.text = SkillTree.Crystals.ToString();
        foreach (Node n in nodes)
        {
            int level = Inventory.SkillLevel(n.def.id);
            SkillTree.State state = SkillTree.StateOf(n.def);
            n.icon.color = state == SkillTree.State.Locked ? new Color(1f, 1f, 1f, 0.3f) : level == 0 ? new Color(1f, 1f, 1f, 0.7f) : Color.white;
            for (int i = 0; i < n.pips.Length; i++) n.pips[i].color = i < level ? MenuUI.Yellow : MenuUI.Line;
            foreach (Image line in n.lineIn) line.color = level > 0 ? MenuUI.Yellow : MenuUI.Line;
        }
    }

    private void ShowNode(int index)
    {
        cursor = index;
        SkillDef d = nodes[index].def;
        int level = Inventory.SkillLevel(d.id);
        detailIcon.sprite = nodes[index].icon.sprite;
        detailIcon.color = detailIcon.sprite != null ? Color.white : new Color(1, 1, 1, 0);
        detailName.text = d.name;
        detailLevel.text = (d.branch == SkillBranch.Natural ? "NATURAL" : "TALISMAN") + "   LEVEL " + level + "/" + d.MaxLevel;
        detailText.text = d.description;
        detailNow.text = level > 0 ? "NOW: " + d.levelText[level - 1] : "NOT LEARNED YET";
        detailNow.color = level > 0 ? MenuUI.Cream : MenuUI.Muted;
        detailNext.text = level < d.MaxLevel ? "NEXT: " + d.levelText[level] : "";
        switch (SkillTree.StateOf(d))
        {
            case SkillTree.State.Maxed: detailCost.text = "FULLY LEARNED"; detailCost.color = MenuUI.Yellow; break;
            case SkillTree.State.Locked: detailCost.text = SkillTree.RequirementText(d); detailCost.color = MenuUI.Muted; break;
            case SkillTree.State.TooExpensive: detailCost.text = SkillTree.NextCost(d) + " CRYSTALS (YOU HAVE " + SkillTree.Crystals + ")"; detailCost.color = MenuUI.Red; break;
            default: detailCost.text = "CROSS/ENTER: LEARN FOR " + SkillTree.NextCost(d) + " CRYSTALS"; detailCost.color = MenuUI.Yellow; break;
        }
    }

    private void Learn(int index)
    {
        SkillDef d = nodes[index].def;
        if (!SkillTree.TryLearn(d, out string message))
        {
            Sound(cat.blockedSound);
            Message(message, MenuUI.Red);
            return;
        }
        Sound(cat.buySound != null ? cat.buySound : cat.equipSound);
        Message(message, MenuUI.Yellow);
        Refresh();
        ShowNode(index);
        SkillFX(nodes[index]);
    }

    // the learned node pops (it runs on unscaled time: the game behind is paused)
    private void SkillFX(Node n)
    {
        RectTransform rt = (RectTransform)n.button.transform;
        rt.localScale = Vector3.one * 1.2f;
        bump = rt;
        bumpLeft = 1f;
    }

    private RectTransform bump;
    private float bumpLeft;

    protected override void OnTick()
    {
        if (bump == null || bumpLeft <= 0f) return;
        bumpLeft = Mathf.Max(0f, bumpLeft - Time.unscaledDeltaTime * 5f);
        bump.localScale = Vector3.one * (1f + 0.2f * bumpLeft);
    }
}
