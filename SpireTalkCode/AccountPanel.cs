using BaseLib.Config;
using Godot;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.addons.mega_text;
using SpireTalk.Yumina;

namespace SpireTalk;

/// <summary>
/// Live account block appended to the mod's settings page: who is signed in, credits, plan,
/// the model in use, and a model picker filled from the server. Read-only display — buying
/// credits or changing plans happens on the website, never in the game.
/// </summary>
public static class AccountPanel
{
    private static VBoxContainer? _root;
    private static MegaRichTextLabel? _status;
    private static MegaRichTextLabel? _modelLine;
    private static OptionButton? _picker;
    private static List<ModelInfo> _models = new();
    private static bool _refreshing;

    public static void Install(SpireTalkConfig config, Control container)
    {
        _root = new VBoxContainer { Name = "SpireTalkAccountPanel" };
        _root.AddThemeConstantOverride("separation", 10);

        var header = ModConfig.CreateRawLabelControl("[b]" + Loc.Ui("AccountHeader") + "[/b]", 30);
        _root.AddChild(header);

        _status = ModConfig.CreateRawLabelControl(Loc.Ui("AccountNotSignedIn"), 26);
        _root.AddChild(_status);

        _modelLine = ModConfig.CreateRawLabelControl("", 26);
        _root.AddChild(_modelLine);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 16);
        _picker = new OptionButton { CustomMinimumSize = new Vector2(520, 56), FocusMode = Control.FocusModeEnum.All };
        _picker.AddThemeFontSizeOverride("font_size", 24);
        _picker.ItemSelected += OnModelPicked;
        row.AddChild(_picker);
        row.AddChild(config.MakeButton(Loc.Ui("RefreshBtn"), () => Refresh()));
        _root.AddChild(row);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_top", 24);
        margin.AddThemeConstantOverride("margin_bottom", 24);
        margin.AddThemeConstantOverride("margin_left", 48);
        margin.AddChild(_root);
        container.AddChild(margin);

        UpdateVisibility();
        Refresh();
    }

    public static void UpdateVisibility()
    {
        if (_root != null && GodotObject.IsInstanceValid(_root))
        {
            _root.GetParent<Control>().Visible = SpireTalkConfig.Mode == BackendMode.Yumina;
        }
    }

    public static void Refresh()
    {
        if (_root == null || !GodotObject.IsInstanceValid(_root) || _refreshing) return;
        _ = RefreshAsync();
    }

    private static async Task RefreshAsync()
    {
        _refreshing = true;
        try
        {
            UpdateVisibility();
            SpireTalkConfig.ApplyToClient();
            var client = MainFile.Client;
            if (!client.HasToken)
            {
                SetStatus(Loc.Ui("AccountNotSignedIn"));
                SetModelLine("");
                FillPicker(new List<ModelInfo>());
                return;
            }
            SetStatus(Loc.Ui("AccountLoading"));
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var me = await client.WhoAmIAsync(cts.Token);
            var credits = me.Credits.HasValue ? me.Credits.Value.ToString("N0") : "?";
            SetStatus(string.Format(Loc.Ui("AccountStatus"), me.Name, credits, me.Plan ?? "-"));

            try
            {
                _models = (await client.ListModelsAsync(cts.Token)).ToList();
            }
            catch (Exception ex)
            {
                MainFile.Logger.Warn($"[Account] model list failed: {ex.Message}", 1);
                _models = new List<ModelInfo>();
            }
            FillPicker(_models);
            SetModelLine(string.Format(Loc.Ui("AccountModel"), CurrentModelLabel()));
        }
        catch (Exception ex)
        {
            SetStatus(Loc.DescribeError(ex));
        }
        finally
        {
            _refreshing = false;
        }
    }

    private static string CurrentModelLabel()
    {
        var chosen = SpireTalkConfig.YuminaModel?.Trim();
        if (string.IsNullOrEmpty(chosen)) chosen = MainFile.Manifest?.DefaultModel;
        if (string.IsNullOrEmpty(chosen)) return Loc.Ui("AccountModelDefault");
        var m = _models.FirstOrDefault(x => x.Id == chosen);
        return m != null ? $"{m.Name} ({m.Id})" : chosen;
    }

    private static void FillPicker(List<ModelInfo> models)
    {
        if (_picker == null || !GodotObject.IsInstanceValid(_picker)) return;
        _picker.Clear();
        _picker.AddItem(Loc.Ui("AccountModelDefault"), 0);
        var current = SpireTalkConfig.YuminaModel?.Trim() ?? "";
        var selected = 0;
        for (var i = 0; i < models.Count; i++)
        {
            var m = models[i];
            var cost = m.AvgCost.HasValue ? $" · ~{m.AvgCost.Value:0.#}" : "";
            _picker.AddItem($"{m.Name}{cost}", i + 1);
            if (m.Id == current) selected = i + 1;
        }
        _picker.Selected = selected;
        _picker.Disabled = models.Count == 0;
    }

    private static void OnModelPicked(long index)
    {
        var i = (int)index;
        SpireTalkConfig.YuminaModel = i <= 0 || i > _models.Count ? "" : _models[i - 1].Id;
        SetModelLine(string.Format(Loc.Ui("AccountModel"), CurrentModelLabel()));
        try
        {
            ModConfig.SaveDebounced<SpireTalkConfig>();
        }
        catch { /* config save is best effort */ }
    }

    private static void SetStatus(string text)
    {
        if (_status != null && GodotObject.IsInstanceValid(_status)) _status.Text = text;
    }

    private static void SetModelLine(string text)
    {
        if (_modelLine != null && GodotObject.IsInstanceValid(_modelLine)) _modelLine.Text = text;
    }
}
