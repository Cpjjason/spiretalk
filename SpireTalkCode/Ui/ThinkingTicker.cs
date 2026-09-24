using Godot;

namespace SpireTalk.Ui;

/// <summary>
/// Makes a waiting "……" line look alive while the model writes: the dots grow one at a time,
/// and after a while a small "still thinking (12s)" joins them so a slow model never reads as a
/// frozen game. Hangs off the area that holds the line, finds the line by its text (the Ancient
/// page builds its own bubbles), and bows out the moment anything else writes to that line.
/// </summary>
public sealed partial class ThinkingTicker : Node
{
    private const double StepSeconds = 0.4;
    private const double HintAfterSeconds = 10;
    private const double GiveUpSeconds = 300;

    private Control _root = null!;
    private string _waitingText = "";
    private bool _asSuffix;
    private string _prefix = "";
    private RichTextLabel? _label;
    private string? _written;
    private double _elapsed;
    private int _shown = -1;

    /// <summary>Animate the line under <paramref name="root"/> that currently reads <paramref name="waitingText"/>.</summary>
    /// <param name="asSuffix">The waiting marks end a longer text (a result with the reply still to come); only they move.</param>
    public static void Attach(Control? root, string waitingText, bool asSuffix = false)
    {
        if (root == null || !IsInstanceValid(root) || string.IsNullOrWhiteSpace(waitingText)) return;
        foreach (var old in root.GetChildren().OfType<ThinkingTicker>()) old.QueueFree();
        root.AddChild(new ThinkingTicker { Name = "SpireTalkThinking", _root = root, _waitingText = waitingText.Trim(), _asSuffix = asSuffix });
    }

    public override void _Process(double delta)
    {
        _elapsed += delta;
        if (_elapsed > GiveUpSeconds || !IsInstanceValid(_root))
        {
            QueueFree();
            return;
        }

        if (_label == null)
        {
            // The bubble may appear a frame or two after the call that asked for it.
            _label = FindWaitingLabel(_root);
            if (_label == null)
            {
                if (_elapsed > 3) QueueFree();
                return;
            }
            _written = _label.Text;
            if (_asSuffix)
            {
                var raw = _label.Text.TrimEnd();
                _prefix = raw.EndsWith(_waitingText) ? raw[..^_waitingText.Length] : raw + " ";
            }
        }

        if (!IsInstanceValid(_label) || _label.Text != _written)
        {
            // The reply (or an error) replaced the line: our job is done.
            QueueFree();
            return;
        }

        var step = (int)(_elapsed / StepSeconds);
        if (step == _shown) return;
        _shown = step;
        _written = _prefix + Compose(step);
        _label.Text = _written;
    }

    private string Compose(int step)
    {
        // "…" / "……" grow to three marks and start over, keeping the waiting text's own glyph.
        var unit = _waitingText.Length > 0 ? _waitingText[0].ToString() : "…";
        var perMark = Math.Max(1, _waitingText.Length / 2);
        var dots = string.Concat(Enumerable.Repeat(unit, perMark * (step % 3 + 1)));
        if (_elapsed < HintAfterSeconds) return dots;
        return dots + "  " + string.Format(Loc.EventText("thinkingLong"), (int)_elapsed);
    }

    private bool Matches(string shown) => _asSuffix ? shown.Length > _waitingText.Length && shown.EndsWith(_waitingText) : shown == _waitingText;

    private RichTextLabel? FindWaitingLabel(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is RichTextLabel label && label.IsVisibleInTree() && Matches(label.GetParsedText().Trim())) return label;
            if (child is Node inner && FindWaitingLabel(inner) is { } found) return found;
        }
        return null;
    }
}
