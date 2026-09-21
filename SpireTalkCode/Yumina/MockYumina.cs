using System.Text.Json.Nodes;

namespace SpireTalk.Yumina;

/// <summary>
/// Offline stand-in used when the server address is set to "mock". Lets the in-game UI be
/// exercised (talk option, thinking state, multi-line dialogue, choices, free text, ending)
/// without a Yumina account or network. Replies are canned and vary by turn.
/// </summary>
public static class MockYumina
{
    public const string Marker = "mock";

    private static int _turn;

    public static bool IsEnabled(string baseUrl)
    {
        var u = (baseUrl ?? "").Trim().ToLowerInvariant();
        return u == Marker || u == "mock://" || u.EndsWith("//mock");
    }

    public static Task<WhoAmI> WhoAmIAsync() => Task.FromResult(new WhoAmI("mock-user", "Mock Slayer", 9999, "mock"));

    public static Task<IntegrationManifest> ManifestAsync()
    {
        var worlds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var k in new[] { "NEOW", "OROBAS", "NONUPEIPE", "PAEL", "DARV", "TANX", "TEZCATARA", "VAKUU", "MERCHANT" })
            worlds[k] = "mock-" + k.ToLowerInvariant();
        return Task.FromResult(new IntegrationManifest(worlds, "mock-model", 0));
    }

    public static async Task<TurnResult> TurnAsync(string content, CancellationToken ct)
    {
        await Task.Delay(TimeSpan.FromSeconds(1.5), ct);
        _turn++;
        var zh = Loc.ReplyLanguageName().Contains("Chinese");

        string text;
        string[] choices;
        var affection = Math.Min(100, 10 + _turn * 7);

        if (content.StartsWith('['))
        {
            text = zh
                ? "……又一个爬塔的。\n\n你身上有股铁锈和血的味道。别误会，我并不讨厌这个味道。\n\n说吧，你想从我这里得到什么？"
                : "…Another climber.\n\nYou smell of rust and blood. Don't misunderstand — I don't dislike it.\n\nSpeak. What is it you want from me?";
            choices = zh
                ? new[] { "我只是想聊聊。", "你到底是什么？", "帮我一把，我会记住的。" }
                : new[] { "I just wanted to talk.", "What are you, exactly?", "Help me, and I'll remember it." };
        }
        else if (_turn % 3 == 0)
        {
            text = zh
                ? $"你说「{Trim(content)}」。\n\n有意思。上一个这么说的人，骨头现在还在第三幕的某个角落里发光。"
                : $"You say \"{Trim(content)}\".\n\nInteresting. The last one who said that — their bones still glow somewhere in Act 3.";
            choices = zh
                ? new[] { "那是威胁吗？", "换个话题吧。", "我不怕。" }
                : new[] { "Is that a threat?", "Let's change the subject.", "I'm not afraid." };
        }
        else
        {
            text = zh
                ? $"「{Trim(content)}」……嗯。\n\n你比上次来的时候话多了一点。这算进步。"
                : $"\"{Trim(content)}\"… Hm.\n\nYou talk a little more than last time. I'll call that progress.";
            choices = zh
                ? new[] { "上次？你记得我？", "说说这座塔。", "我该走了。" }
                : new[] { "Last time? You remember me?", "Tell me about the Spire.", "I should go." };
        }

        var vars = new JsonObject
        {
            ["affection"] = affection,
            ["choices"] = new JsonArray(choices.Select(c => (JsonNode)JsonValue.Create(c)!).ToArray()),
        };
        return new TurnResult(text, choices, vars, "mock-model", 9999 - _turn);
    }

    private static string Trim(string s)
    {
        s = s.Replace('\n', ' ').Trim();
        return s.Length > 40 ? s[..40] + "…" : s;
    }
}
