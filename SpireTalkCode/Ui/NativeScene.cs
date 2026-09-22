using Godot;
using MegaCrit.Sts2.addons.mega_text;

namespace SpireTalk.Ui;

/// <summary>
/// Builds a copy of one of the game's own scenes (textures, nine-patch margins, fonts, shadows,
/// colours: everything the .tscn stores) without its game script, so the mod can show the exact
/// vanilla dialogue bubble or option button without the script's dependencies (an EventModel,
/// an EventOption...). Rich text labels keep their MegaRichTextLabel type so the game's colour
/// tags and CJK font fallbacks behave as they do in vanilla.
/// </summary>
public static class NativeScene
{
    public const string AncientOptionButton = "res://scenes/events/ancient_event_option_button.tscn";
    public const string AncientDialogueLine = "res://scenes/events/ancient_dialogue_line.tscn";
    public const string EventOptionButton = "res://scenes/events/event_option_button.tscn";
    public const string ContinueButton = "res://scenes/screens/game_over_screen/game_over_button.tscn";

    private const string MegaTextScript = "res://addons/mega_text/MegaRichTextLabel.cs";
    private const string MegaLabelScript = "res://addons/mega_text/MegaLabel.cs";

    public static Control? Build(string path)
    {
        try
        {
            var scene = ResourceLoader.Load<PackedScene>(path);
            if (scene == null) return null;
            return BuildFrom(scene.GetState()) as Control;
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[NativeScene] {path}: {ex.Message}", 1);
            return null;
        }
    }

    private static Node? BuildFrom(SceneState state)
    {
        Node? root = null;
        for (var i = 0; i < state.GetNodeCount(); i++)
        {
            Node? node;
            var instance = state.GetNodeInstance(i);
            if (instance != null)
            {
                node = BuildFrom(instance.GetState());
            }
            else
            {
                node = Create(state, i);
            }
            if (node == null) continue;
            node.Name = state.GetNodeName(i);

            for (var p = 0; p < state.GetNodePropertyCount(i); p++)
            {
                var name = state.GetNodePropertyName(i, p).ToString();
                if (name == "script") continue;
                node.Set(name, state.GetNodePropertyValue(i, p));
            }

            if (root == null)
            {
                root = node;
                continue;
            }
            var parent = root.GetNodeOrNull(state.GetNodePath(i, true));
            if (parent == null)
            {
                node.Free();
                continue;
            }
            parent.AddChild(node);
            node.Owner = root;
        }
        return root;
    }

    private static Node? Create(SceneState state, int index)
    {
        for (var p = 0; p < state.GetNodePropertyCount(index); p++)
        {
            if (state.GetNodePropertyName(index, p).ToString() != "script") continue;
            var script = state.GetNodePropertyValue(index, p).As<Resource>()?.ResourcePath;
            if (script == MegaTextScript) return new MegaRichTextLabel();
            if (script == MegaLabelScript) return new MegaLabel();
        }
        var type = state.GetNodeType(index).ToString();
        if (string.IsNullOrEmpty(type) || !ClassDB.CanInstantiate(type)) return null;
        return ClassDB.Instantiate(type).As<Node>();
    }
}
