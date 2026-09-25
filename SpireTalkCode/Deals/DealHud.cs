using Godot;
using MegaCrit.Sts2.Core.Nodes;
using SpireTalk.Npc;
using SpireTalk.Store;
using SpireTalk.Ui;

namespace SpireTalk.Deals;

/// <summary>
/// The line under the top bar that lists the deals still open this run: "涅奥 · 不喝药水，直到第 2 章".
/// Lives in the run's global UI so it survives room changes; gone with the run. Hidden when empty.
/// </summary>
public sealed partial class DealHud : PanelContainer
{
    private static DealHud? _instance;
    private MegaCrit.Sts2.addons.mega_text.MegaRichTextLabel _label = null!;

    /// <summary>Re-read the store and redraw. Safe to call from any point in a frame; the work is deferred.</summary>
    public static void Refresh()
    {
        try
        {
            Callable.From(RefreshNow).CallDeferred();
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[Deal] hud refresh failed: {ex.Message}", 1);
        }
    }

    private static void RefreshNow()
    {
        try
        {
            var ui = NGame.Instance?.GetNodeOrNull<Control>("RootSceneContainer/Run/GlobalUi");
            if (ui == null)
            {
                Drop();
                return;
            }
            var player = Merchant.MerchantTalk.ResolveLocalPlayer();
            if (player == null)
            {
                Drop();
                return;
            }
            var runKey = Rewards.RewardExecutor.RunKey(player);
            var lines = DealService.PendingInRun(runKey)
                .Select(d => $"{NpcCatalog.Get(d.Npc)?.DisplayName ?? d.Npc} · {DealService.Describe(d)}")
                .ToList();

            if (_instance == null || !GodotObject.IsInstanceValid(_instance) || _instance.GetParent() != ui)
            {
                Drop();
                _instance = new DealHud();
                _instance.Build();
                ui.AddChild(_instance);
            }
            _instance.Visible = lines.Count > 0;
            if (lines.Count > 0)
            {
                _instance._label.Text = "[b]" + Loc.EventText("dealHudTitle") + "[/b]  " + string.Join("   ·   ", lines.Select(Loc.Sanitize));
            }
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[Deal] hud failed: {ex.Message}", 1);
        }
    }

    private static void Drop()
    {
        if (_instance != null && GodotObject.IsInstanceValid(_instance)) _instance.QueueFree();
        _instance = null;
    }

    private void Build()
    {
        Name = "SpireTalkDealHud";
        MouseFilter = MouseFilterEnum.Ignore;
        // Left, just under the relic row, out of the way of the map and the event art.
        SetAnchorsPreset(LayoutPreset.TopLeft);
        OffsetLeft = 12;
        OffsetTop = RelicRowBottom;
        AddThemeStyleboxOverride("panel", GameArt.Navy(16, 6));
        _label = GameArt.Label("", 20, GameArt.Gold, wrap: false);
        _label.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_label);
    }

    // The relic bar starts at y=82 with 68px icons and wraps into more rows as relics pile up.
    private const float RelicRowBottom = 82 + 68 + 6;
    private int _frame;

    public override void _Process(double delta)
    {
        if (!Visible || ++_frame % 20 != 0) return;
        var relics = GetParent()?.GetNodeOrNull<Control>("RelicInventory");
        if (relics == null) return;
        var bottom = RelicRowBottom;
        foreach (var child in relics.GetChildren())
        {
            if (child is Control c && c.Visible) bottom = Math.Max(bottom, c.Position.Y + relics.Position.Y + c.Size.Y + 6);
        }
        if (Math.Abs(OffsetTop - bottom) > 0.5f) OffsetTop = bottom;
    }
}
