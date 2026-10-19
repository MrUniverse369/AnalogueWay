using System.Collections.Generic;
using System.Text.RegularExpressions;

/// <summary>
/// DIALOGUE: one dialogue file (a .txt TextAsset, e.g. Assets/Dialogue/JapanVillage.txt) parsed into nodes. The format
/// is plain text so conversations can be written in any editor:
///
///   // a comment                        (lines starting with // or # are ignored, blank lines too)
///   == start                            a node: a named part of a conversation (names ignore case)
///   Hana: Welcome to the village!       a line: "Name: text". The name shows in the box's name plate
///   It's cold up here.                  no name: the same speaker as the line before
///   > The wind howls.                   narration: a line with no speaker
///   * Ask about the temple -> temple    a choice; the choices in a row show together, after the line above them
///   * Goodbye -> end
///   -> other                            go to another node
///   -> random: rumour, weather, food    go to one of these at random
///   -> cycle: chat1, chat2, chat3       the next one each time Harvey talks to this villager
///   -> end                              end the conversation (reaching the end of a node does the same)
///
/// {name} is replaced with the villager's name and {player} with Harvey, so one file can serve many villagers.
/// Tools/HNL/Check Dialogue Files reports mistakes (unknown nodes, choices without a target) with line numbers.
/// </summary>
public class DialogueScript
{
    public enum StepKind { Line, Choices, Goto }

    public class Choice
    {
        public string text;
        public string target;
    }

    public class Step
    {
        public StepKind kind;
        public string speaker;    // Line: "" = narration
        public string text;       // Line
        public List<Choice> choices; // Choices
        public string mode;       // Goto: "go", "random" or "cycle"
        public string[] targets;  // Goto
        public int lineNumber;
    }

    public const string End = "end";

    public readonly string source;
    public readonly Dictionary<string, List<Step>> nodes = new Dictionary<string, List<Step>>();
    public readonly List<string> errors = new List<string>();

    // "Name: text" with a short name (letters, spaces, apostrophes, {name}); anything else is an ordinary line
    private static readonly Regex SpeakerLine = new Regex(@"^([\p{L}\p{N}' {}\-]{1,24}):\s+(.+)$");

    private DialogueScript(string source) => this.source = source;

    public bool HasNode(string node) => node != null && nodes.ContainsKey(node.Trim().ToLowerInvariant());

    public List<Step> Node(string node) =>
        node != null && nodes.TryGetValue(node.Trim().ToLowerInvariant(), out List<Step> steps) ? steps : null;

    public static DialogueScript Parse(string text, string source)
    {
        var script = new DialogueScript(source);
        List<Step> current = null;
        string lastSpeaker = "";
        string[] lines = (text ?? "").Replace("\r\n", "\n").Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            int number = i + 1;
            string line = lines[i].Trim();
            if (line.Length == 0 || line.StartsWith("//") || line.StartsWith("#")) continue;

            if (line.StartsWith("=="))
            {
                string name = line.TrimStart('=').Trim().ToLowerInvariant();
                if (name.Length == 0) { script.Error(number, "a node needs a name (== name)"); continue; }
                if (script.nodes.ContainsKey(name)) script.Error(number, $"node '{name}' is defined twice");
                current = new List<Step>();
                script.nodes[name] = current;
                lastSpeaker = "";
                continue;
            }
            if (current == null) { script.Error(number, "text before the first node (start the file with == start)"); continue; }

            if (line.StartsWith("*"))
            {
                int arrow = line.IndexOf("->");
                string label = (arrow >= 0 ? line.Substring(1, arrow - 1) : line.Substring(1)).Trim();
                string target = arrow >= 0 ? line.Substring(arrow + 2).Trim().ToLowerInvariant() : "";
                if (target.Length == 0) script.Error(number, "a choice needs a target: * text -> node (or -> end)");
                Step group = current.Count > 0 && current[current.Count - 1].kind == StepKind.Choices ? current[current.Count - 1] : null;
                if (group == null)
                {
                    group = new Step { kind = StepKind.Choices, choices = new List<Choice>(), lineNumber = number };
                    current.Add(group);
                }
                group.choices.Add(new Choice { text = label, target = target.Length > 0 ? target : End });
                continue;
            }

            if (line.StartsWith("->"))
            {
                string rest = line.Substring(2).Trim();
                string mode = "go";
                int colon = rest.IndexOf(':');
                if (colon > 0)
                {
                    string m = rest.Substring(0, colon).Trim().ToLowerInvariant();
                    if (m == "random" || m == "cycle") { mode = m; rest = rest.Substring(colon + 1); }
                }
                var targets = new List<string>();
                foreach (string t in rest.Split(','))
                    if (t.Trim().Length > 0) targets.Add(t.Trim().ToLowerInvariant());
                if (targets.Count == 0) { script.Error(number, "-> needs a node name (or end)"); continue; }
                current.Add(new Step { kind = StepKind.Goto, mode = mode, targets = targets.ToArray(), lineNumber = number });
                continue;
            }

            string speaker, words;
            if (line.StartsWith(">"))
            {
                speaker = "";
                words = line.Substring(1).Trim();
            }
            else
            {
                Match m = SpeakerLine.Match(line);
                if (m.Success)
                {
                    speaker = m.Groups[1].Value.Trim();
                    words = m.Groups[2].Value.Trim();
                }
                else
                {
                    speaker = lastSpeaker;
                    words = line;
                }
            }
            lastSpeaker = speaker;
            current.Add(new Step { kind = StepKind.Line, speaker = speaker, text = words, lineNumber = number });
        }
        script.CheckTargets();
        return script;
    }

    private void CheckTargets()
    {
        foreach (KeyValuePair<string, List<Step>> node in nodes)
            foreach (Step s in node.Value)
            {
                if (s.kind == StepKind.Goto)
                    foreach (string t in s.targets) CheckTarget(t, s.lineNumber);
                else if (s.kind == StepKind.Choices)
                    foreach (Choice c in s.choices) CheckTarget(c.target, s.lineNumber);
            }
    }

    private void CheckTarget(string target, int line)
    {
        if (target != End && !nodes.ContainsKey(target)) Error(line, $"no node called '{target}'");
    }

    private void Error(int line, string message) => errors.Add($"{source} line {line}: {message}");
}
