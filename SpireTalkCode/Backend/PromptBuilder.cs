using System.Text;
using System.Text.Json.Nodes;
using SpireTalk.Npc;

namespace SpireTalk.Backend;

/// <summary>
/// Builds the prompt for the direct-LLM backend. The Yumina worlds carry the same persona,
/// shared rules and affection/reward rules; only the output-format section differs (Yumina uses
/// its own variable directives, the direct backend asks for one JSON object).
/// </summary>
public static class PromptBuilder
{
    public const int HistoryLinesToSend = 40; // 20 exchanges

    public static string SystemPrompt(VisitContext ctx)
    {
        var sb = new StringBuilder();
        sb.AppendLine(ctx.Npc.Persona.Trim());
        sb.AppendLine();
        sb.AppendLine("WHAT MOVES YOU (use this to judge affection_delta; + earns, - costs)");
        sb.AppendLine(ctx.Npc.MovesYou.Trim());
        sb.AppendLine();
        sb.AppendLine(NpcCatalog.SharedRules.Replace("{language}", ctx.Language).Trim());
        sb.AppendLine();
        sb.AppendLine(ConversationRules.PromptText(ctx.Kind));
        sb.AppendLine();
        sb.AppendLine("CURRENT SITUATION (read-only facts from the game)");
        foreach (var kv in ctx.Variables)
        {
            if (kv.Value == null) continue;
            sb.Append("- ").Append(kv.Key).Append(": ").AppendLine(Render(kv.Value));
        }
        sb.AppendLine();
        sb.AppendLine(OutputFormat());
        return sb.ToString();
    }

    private static string Render(JsonNode node)
    {
        return node is JsonValue v && v.TryGetValue<string>(out var s) ? s : node.ToJsonString();
    }

    public static string OutputFormat() =>
        @"OUTPUT FORMAT
Reply with ONE JSON object and nothing else - no prose before or after, no code fences:
{""lines"": [""first bubble"", ""second bubble""], ""choices"": [""choice 1"", ""choice 2"", ""choice 3""], ""affection_delta"": 0, ""reason"": ""one short clause"", ""action"": {""type"": ""none"", ""amount"": 0}}
- lines: 1-3 strings, each one speech bubble, in the reply language.
- choices: exactly 3 short things the climber could say next, first-person, in the reply language, each at most 40 characters, genuinely different from each other (one leans in, one pushes back or asks something pointed, one is playful or wants to leave). When lines_left is 0, choices must be a single farewell instead.
- affection_delta: integer -3..3.
- action.type: ""none"" or one of allowed_actions. action.amount: 0 unless you have a reason.";
}
