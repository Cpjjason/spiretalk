using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;

namespace SpireTalk.Merchant;

/// <summary>
/// When something moves a shop price (the Merchant's discount, the cart's favour or grudge, the
/// ledger keepsake), the old price sits struck through above the new one, so the climber can see
/// what they are saving or paying extra. Hooks every shop slot's UpdateVisual, card removal included.
/// </summary>
[HarmonyPatch]
internal static class PriceTags
{
    private const string TagName = "SpireTalkWasPrice";
    private static readonly FieldInfo? BaseCost = AccessTools.Field(typeof(MerchantEntry), "_cost");
    private static readonly FieldInfo? CostLabel = AccessTools.Field(typeof(NMerchantSlot), "_costLabel");

    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var type in typeof(NMerchantSlot).Assembly.GetTypes())
        {
            if (!typeof(NMerchantSlot).IsAssignableFrom(type)) continue;
            var method = type.GetMethod("UpdateVisual", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (method != null && !method.IsAbstract) yield return method;
        }
    }

    [HarmonyPostfix]
    private static void AfterUpdateVisual(NMerchantSlot __instance)
    {
        try
        {
            if (CostLabel?.GetValue(__instance) is not Label label || !GodotObject.IsInstanceValid(label)) return;
            var entry = __instance.Entry;
            var was = BaseCost?.GetValue(entry) is int c ? c : entry.Cost;
            var now = entry.Cost;
            var tag = __instance.GetNodeOrNull<Control>(TagName);
            var show = entry.IsStocked && label.IsVisibleInTree() && was != now && was > 0;
            if (!show)
            {
                if (tag != null) tag.Visible = false;
                return;
            }
            tag ??= Create(__instance, label);
            var text = tag.GetNode<Label>("Text");
            text.Text = was.ToString();
            tag.Visible = true;
            // Wait a frame for the price label to size itself, then sit centred above it.
            Callable.From(() => Place(__instance, label, tag)).CallDeferred();
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[Shop] price tag failed: {ex.Message}", 1);
        }
    }

    /// <summary>
    /// A child of the slot, not of the price: the price label is tinted green/red by the game and
    /// a child would take the tint. Old price in parchment, a red line struck through it.
    /// </summary>
    private static Control Create(NMerchantSlot slot, Label price)
    {
        var size = Math.Max(18, (int)(price.GetThemeFontSize("font_size") * 0.72f));
        var tag = new Control { Name = TagName, MouseFilter = Control.MouseFilterEnum.Ignore };
        var text = new Label { Name = "Text", MouseFilter = Control.MouseFilterEnum.Ignore };
        var font = price.GetThemeFont("font");
        if (font != null) text.AddThemeFontOverride("font", font);
        text.AddThemeFontSizeOverride("font_size", size);
        text.AddThemeColorOverride("font_color", new Color(0.93f, 0.88f, 0.78f));
        text.AddThemeColorOverride("font_outline_color", new Color(0.1f, 0.07f, 0.05f));
        text.AddThemeConstantOverride("outline_size", 6);
        var strike = new ColorRect { Name = "Strike", Color = new Color(0.88f, 0.25f, 0.22f), MouseFilter = Control.MouseFilterEnum.Ignore };
        tag.AddChild(text);
        tag.AddChild(strike);
        slot.AddChild(tag);
        return tag;
    }

    private static void Place(NMerchantSlot slot, Label price, Control tag)
    {
        if (!GodotObject.IsInstanceValid(slot) || !GodotObject.IsInstanceValid(price) || !GodotObject.IsInstanceValid(tag)) return;
        var text = tag.GetNode<Label>("Text");
        var strike = tag.GetNode<ColorRect>("Strike");
        var textSize = text.GetMinimumSize();
        text.Size = textSize;
        strike.Size = new Vector2(textSize.X + 6, 3);
        strike.Position = new Vector2(-3, textSize.Y * 0.52f);
        var topCentre = slot.GetGlobalTransform().AffineInverse() * (price.GlobalPosition + new Vector2(price.Size.X * price.Scale.X / 2, 0));
        tag.Position = topCentre + new Vector2(-textSize.X / 2, -textSize.Y + 6);
    }
}
