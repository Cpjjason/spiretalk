using BaseLib.Config.UI;
using BaseLib.Utils;
using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;

namespace SpireTalk.Ui;

/// <summary>
/// The game's settings dropdown with a list the caller owns and can replace while it is open
/// (BaseLib's own only binds enum properties). Built exactly like BaseLib's NConfigDropdown, so it
/// looks and sounds like every other dropdown on the settings screens.
/// </summary>
public sealed partial class ChoiceDropdown : NSettingsDropdown
{
    public sealed record Choice(string Text, string Value);

    private List<Choice> _choices = new();
    private int _selected = -1;
    private Action<Choice>? _onSelected;
    private readonly NodePath _self = new(".");

    // Private in sts2.dll; BaseLib reaches them through a publicized reference, we through reflection.
    private static readonly System.Reflection.FieldInfo? DropdownNodeField =
        typeof(NDropdownPositioner).GetField("_dropdownNode", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
    private static readonly System.Reflection.FieldInfo? ContainerField =
        typeof(NDropdown).GetField("_dropdownContainer", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);

    private Control? Container => ContainerField?.GetValue(this) as Control;

    public ChoiceDropdown()
    {
        SetCustomMinimumSize(new Vector2(324f, 64f));
        SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        SizeFlagsVertical = SizeFlags.Fill;
        FocusMode = FocusModeEnum.All;
        this.TransferAllNodes<ChoiceDropdown>(SceneHelper.GetScenePath("screens/settings_dropdown"));
    }

    /// <summary>Wraps the dropdown in the positioner the settings screens use (it keeps the open list on top).</summary>
    public static NDropdownPositioner Create(float width, IEnumerable<Choice> choices, string? selectedValue, Action<Choice> onSelected, out ChoiceDropdown dropdown)
    {
        dropdown = new ChoiceDropdown { _onSelected = onSelected };
        dropdown.SetCustomMinimumSize(new Vector2(width, 64f));
        dropdown.SetChoices(choices, selectedValue);
        var positioner = new NDropdownPositioner();
        positioner.SetCustomMinimumSize(new Vector2(width, 64f));
        positioner.FocusMode = FocusModeEnum.None;
        positioner.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        positioner.SizeFlagsVertical = SizeFlags.Fill;
        DropdownNodeField?.SetValue(positioner, dropdown);
        positioner.AddChild(dropdown);
        positioner.MouseFilter = MouseFilterEnum.Ignore;
        return positioner;
    }

    public string? SelectedValue => _selected >= 0 && _selected < _choices.Count ? _choices[_selected].Value : null;

    /// <summary>Replace the list. The selection follows <paramref name="selectedValue"/>, else the first entry.</summary>
    public void SetChoices(IEnumerable<Choice> choices, string? selectedValue)
    {
        _choices = choices.ToList();
        _selected = Math.Max(0, _choices.FindIndex(c => c.Value == selectedValue));
        if (IsNodeReady()) Rebuild();
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        if (FocusNeighborLeft != _self || FocusNeighborRight != _self)
        {
            FocusNeighborLeft = _self;
            FocusNeighborRight = _self;
        }
        if (IsNodeReady() && Container is { Visible: true } open)
        {
            open.GlobalPosition = GlobalPosition + new Vector2(0f, Size.Y);
        }
    }

    public override void _Ready()
    {
        ConnectSignals();
        Rebuild();
        var container = Container;
        if (container == null) return;
        container.VisibilityChanged += () =>
        {
            container.TopLevel = container.Visible;
            container.GlobalPosition = GlobalPosition + new Vector2(0f, Size.Y);
            // The scene's list is sized for the 324px settings dropdown; match ours.
            if (container.Visible) container.Size = new Vector2(Size.X, container.Size.Y);
            if (_dropdownItems.GetChildOrNull<NConfigDropdownItem>(_selected) is { } current) NodeUtil.TryGrabFocus(current);
        };
    }

    private void Rebuild()
    {
        ClearDropdownItems();
        for (var i = 0; i < _choices.Count; i++)
        {
            var choice = _choices[i];
            var item = NConfigDropdownItem.Create(new NConfigDropdownItem.ItemData(choice.Text, choice.Value, () => { }));
            item.SetCustomMinimumSize(new Vector2(CustomMinimumSize.X - 36f, 44f));
            _dropdownItems.AddChildSafely(item);
            item.Connect(NDropdownItem.SignalName.Selected, Callable.From<NDropdownItem>(OnItemSelected));
            item.Init(i);
        }
        _currentOptionLabel.SetTextAutoSize(_selected >= 0 && _selected < _choices.Count ? _choices[_selected].Text : "");
        _dropdownItems.GetParent<NDropdownContainer>().RefreshLayout();
    }

    private void OnItemSelected(NDropdownItem item)
    {
        if (item is not NConfigDropdownItem configItem) return;
        CloseDropdown();
        _selected = configItem.DisplayIndex;
        _currentOptionLabel.SetTextAutoSize(configItem.Data.Text);
        if (_selected >= 0 && _selected < _choices.Count) _onSelected?.Invoke(_choices[_selected]);
    }
}
