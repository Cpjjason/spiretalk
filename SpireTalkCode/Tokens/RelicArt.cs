using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Relics;
using SpireTalk.Encounters;

namespace SpireTalk.Tokens;

/// <summary>
/// The mod's own relic icons: a chibi of the NPC, in the game's own style, for every relic the mod
/// hands out. A relic whose picture is missing falls back to the vanilla atlas sprite it names.
/// </summary>
public static class RelicArt
{
    private const string Folder = "res://SpireTalk/images/relics/";

    /// <summary>
    /// res:// path of the icon <paramref name="name"/> (e.g. "keepsake_neow_2"), or null when the mod
    /// ships no picture for it.
    /// </summary>
    public static string? Icon(string name) => Find(name, "");

    /// <summary>The larger picture the relic's inspect page shows; falls back to the bar icon.</summary>
    public static string? Big(string name) => Find(name, "_big") ?? Icon(name);

    /// <summary>The white silhouette the relic bar draws behind the icon.</summary>
    public static string? Outline(string name) => Find(name, "_outline");

    private static string? Find(string name, string suffix)
    {
        var path = $"{Folder}{name}_vanilla{suffix}.png";
        return ResourceLoader.Exists(path) ? path : null;
    }

    /// <summary>Redraw every icon of <paramref name="relic"/> that is on screen (after its picture changed).</summary>
    public static void Redraw(RelicModel relic)
    {
        try
        {
            var root = NGame.Instance;
            if (root == null) return;
            Visit(root);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[Relic] redraw failed: {ex.Message}", 1);
        }

        void Visit(Node node)
        {
            if (node is NRelic n && GodotObject.IsInstanceValid(n))
            {
                try
                {
                    if (ReferenceEquals(n.Model, relic)) n.Call("Reload");
                }
                catch (InvalidOperationException)
                {
                    // a relic node with no model yet
                }
            }
            foreach (var child in node.GetChildren()) Visit(child);
        }
    }
}
