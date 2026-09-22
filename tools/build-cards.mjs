#!/usr/bin/env node
// Compiles SpireTalk/npcs.json (persona, "what moves you", shared scene rules, affection rules)
// into one Yumina card per NPC under Cards/. The cards are what the engine bundle actually runs
// in-game; npcs.json stays the single place the writing lives.
//
//   node tools/build-cards.mjs
//
// A card is an ordinary Yumina WorldDefinition: anyone can open it in Yumina Studio, rewrite the
// persona, export, and drop the JSON into %APPDATA%\SlayTheSpire2\spiretalk\cards\ to replace ours.
import { readFileSync, writeFileSync, mkdirSync } from "node:fs";
import { fileURLToPath } from "node:url";
import path from "node:path";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const catalog = JSON.parse(readFileSync(path.join(root, "SpireTalk", "npcs.json"), "utf8"));
// Every line each NPC says in the game, English + official Simplified Chinese
// (tools/extract-canon-lines.py pulls them from the game's localization tables).
const canon = JSON.parse(readFileSync(path.join(root, "SpireTalk", "canon_lines.json"), "utf8"));
const outDir = path.join(root, "Cards");
mkdirSync(outDir, { recursive: true });

function contextVariables(npc) {
  const merchant = npc.kind === "merchant";
  return [
    { id: "language", name: "Reply language", type: "string", defaultValue: "English", aiAccess: "read",
      behaviorRules: "The language every reply and every choice must be written in. Never answer in another language." },
    { id: "character", name: "Player character", type: "string", defaultValue: "The Ironclad", aiAccess: "read",
      behaviorRules: "Who is standing in front of you. Address them by this name or their class, never as 'player'." },
    { id: "character_id", name: "Character id", type: "string", defaultValue: "IRONCLAD", aiAccess: "read",
      behaviorRules: "IRONCLAD, SILENT, DEFECT, NECROBINDER or REGENT. Pick your attitude from the character notes." },
    { id: "hp", name: "HP", type: "number", defaultValue: 80, aiAccess: "read", behaviorRules: "Current hit points." },
    { id: "max_hp", name: "Max HP", type: "number", defaultValue: 80, aiAccess: "read", behaviorRules: "Maximum hit points. Low hp/max_hp means they arrived wounded." },
    { id: "gold", name: "Gold", type: "number", defaultValue: 99, aiAccess: "read", behaviorRules: "Gold carried right now." },
    { id: "act", name: "Act", type: "string", defaultValue: "Act 1", aiAccess: "read", behaviorRules: "Name of the act the climber is in." },
    { id: "act_index", name: "Act number", type: "number", defaultValue: 1, aiAccess: "read", behaviorRules: "1 = bottom of the Spire, higher = closer to the Architect." },
    { id: "floor_in_act", name: "Floor in act", type: "number", defaultValue: 1, aiAccess: "read", behaviorRules: "Floor within the current act." },
    { id: "floor_total", name: "Floor", type: "number", defaultValue: 1, aiAccess: "read", behaviorRules: "Floor within the whole climb." },
    { id: "ascension", name: "Ascension", type: "number", defaultValue: 0, aiAccess: "read", behaviorRules: "Difficulty level chosen for this run. 0 is normal; 20 is the hardest." },
    { id: "players", name: "Players", type: "number", defaultValue: 1, aiAccess: "read", behaviorRules: "How many climbers are in the run (1 = solo)." },
    { id: "relics", name: "Relics", type: "string", defaultValue: "none", aiAccess: "read", behaviorRules: "Relics they carry. Mention one only when it is genuinely interesting to you." },
    { id: "relic_count", name: "Relic count", type: "number", defaultValue: 0, aiAccess: "read", behaviorRules: "How many relics." },
    { id: "deck", name: "Deck", type: "string", defaultValue: "Strike x5, Defend x4, Bash", aiAccess: "read", behaviorRules: "Card names in their deck (most common first)." },
    { id: "deck_size", name: "Deck size", type: "number", defaultValue: 10, aiAccess: "read", behaviorRules: "Number of cards in the deck." },
    { id: "potions", name: "Potions", type: "string", defaultValue: "none", aiAccess: "read", behaviorRules: "Potions carried." },
    { id: "visits", name: "Visits", type: "number", defaultValue: 0, aiAccess: "read", behaviorRules: "How many runs, ever, have reached you. 0 = they have never stood here before." },
    { id: "talks", name: "Talks", type: "number", defaultValue: 1, aiAccess: "read", behaviorRules: "How many conversations (including this one) you two have had. Above 1 means you remember them." },
    { id: "ancient_wins", name: "Wins after meeting you", type: "number", defaultValue: 0, aiAccess: "read", behaviorRules: "Runs that reached you and then won." },
    { id: "ancient_losses", name: "Deaths after meeting you", type: "number", defaultValue: 0, aiAccess: "read", behaviorRules: "Runs that reached you and then died." },
    { id: "npc", name: "NPC id", type: "string", defaultValue: npc.key, aiAccess: "read", behaviorRules: "Your own id. Ignore." },
    { id: "affection_source", name: "Affection source", type: "string", defaultValue: "SpireTalk", aiAccess: "read", behaviorRules: "Which mod keeps the affection number. Ignore." },

    // Relationship state — owned by the mod, read by the AI.
    { id: "affection", name: "Affection", type: "number", defaultValue: 0, min: -100, max: merchant ? 500 : 100, aiAccess: "read",
      behaviorRules: "How much you like this climber right now. The game moves it from your affection_delta; you never set it yourself." },
    { id: "affection_tier", name: "Affection tier", type: "string", defaultValue: "stranger", aiAccess: "read",
      behaviorRules: "hostile / stranger / friendly / close / devoted. Speak accordingly." },
    { id: "married", name: "Married", type: "boolean", defaultValue: false, aiAccess: "read",
      behaviorRules: "True when the Ancient Affection mod says you two are married. Treat them as devoted." },
    { id: "lines_left", name: "Lines left", type: "number", defaultValue: 3, aiAccess: "read",
      behaviorRules: "How many more lines the climber may say this visit. At 0, close the conversation in your reply." },
    { id: "allowed_actions", name: "Allowed actions", type: "string", defaultValue: "none", aiAccess: "read",
      behaviorRules: "Comma-separated rewards you may offer right now, or 'none'. Never offer anything else." },

    // Deals — the mod writes these, the AI reads them (see deal_rules).
    { id: "deal_offer", name: "Deal to propose", type: "string", defaultValue: "none", aiAccess: "read",
      behaviorRules: "A deal the game wants you to propose this visit, or the climber's answer to it, or 'none'. Follow the DEALS rules." },
    { id: "deals", name: "Deals with this climber", type: "string", defaultValue: "none", aiAccess: "read",
      behaviorRules: "Open or settled deals between you and this climber, judged by the game. Follow the DEALS rules." },
    { id: "keepsake", name: "Your keepsake", type: "string", defaultValue: "none", aiAccess: "read",
      behaviorRules: "What is happening with the keepsake you give climbers you trust: not yet earned, being given right now, carried at some level, or just grown. Follow the KEEPSAKE rules." },
    ...(merchant ? [
      { id: "shop_special", name: "Special on the shelf", type: "string", defaultValue: "none", aiAccess: "read",
        behaviorRules: "When an Ancient left one of their relics with you for this climber, this says which; mention it once when they arrive, as a favour being passed along. 'none' otherwise." },
    ] : []),

    // Proposals — written by the AI at the end of every reply, applied by the mod.
    { id: "affection_delta", name: "Affection delta", type: "number", defaultValue: 0, min: -3, max: 3, aiAccess: "write",
      behaviorRules: "Set at the END of every reply: an integer from -3 to 3 judging the climber's last line by your 'what moves you' list. 0 is normal; ±3 is rare. Always set it (0 counts)." },
    { id: "choices", name: "Choices", type: "json", defaultValue: [], aiAccess: "write",
      behaviorRules: "At the END of every reply set this to exactly 3 short things the climber could say next, in the climber's first-person voice, in the reply language, each ≤ 40 characters, no numbering. Make them genuinely different: one that leans in, one that pushes back or asks something pointed, one that is playful or wants to leave. When lines_left is 0 set a single farewell instead. Never leave it empty." },
    { id: "action", name: "Action", type: "json", defaultValue: { type: "none", amount: 0 }, aiAccess: "write",
      behaviorRules: "Set at the END of every reply: {\"type\": \"none\"} normally, or {\"type\": \"<one of allowed_actions>\", \"amount\": 0} when you decide to grant that reward this visit. The game validates and applies it; you only narrate." },
  ];
}

// The engine's own format block explains the directive syntax in general; this entry shows the
// three lines the model must end with, verbatim, so small models copy the shape instead of
// inventing one ([choices: "a","b"] without `set` parses as nothing).
const OUTPUT = `OUTPUT
Write your lines as plain text: 1-3 short paragraphs, each paragraph is one speech bubble. No stage directions in brackets, no labels.
Then END the reply with exactly these three directive lines, in this form and nothing else on those lines:
[affection_delta: set 0]
[choices: set ["first thing to say", "second thing to say", "third thing to say"]]
[action: set {"type": "none", "amount": 0}]
Replace the values (an integer -3..3; three strings in the reply language; a type from allowed_actions or "none"). Keep the brackets, the colon, the word set, and the JSON exactly as shown.`;

const CLIMBER_NAMES = {
  IRONCLAD: ["The Ironclad", "铁甲战士"],
  SILENT: ["The Silent", "静默猎手"],
  DEFECT: ["The Defect", "故障机器人"],
  NECROBINDER: ["The Necrobinder", "亡灵契约师"],
  REGENT: ["The Regent", "储君"],
};

function sceneHeading(scene) {
  if (scene.to === "firstVisitEver") return "The very first time any climber reaches you";
  if (scene.to === "ANY") return "To any climber who comes back";
  if (scene.to === "greeting") return "Greeting a customer";
  if (scene.label) return scene.label[0].toUpperCase() + scene.label.slice(1);
  const [en, zh] = CLIMBER_NAMES[scene.to] ?? [scene.to, ""];
  return `To ${en.replace(/^The /, "the ")} (${zh})`;
}

// The NPC's real lines, grouped by who they are said to, with the climber's replies in between so
// each exchange reads as one scene. Both languages: the Chinese is the game's official
// translation, and it is what the voice should sound like when the reply language is Chinese.
function canonBlock(npc) {
  const scenes = canon[npc.key] ?? [];
  if (scenes.length === 0) return "";
  const who = npc.kind === "merchant" ? "the Merchant" : npc.name;
  const out = [
    "YOUR OWN WORDS IN THE GAME",
    `Every line below is something ${who} really says in Slay the Spire 2, with the game's official Simplified Chinese translation in （brackets） under it. This is your voice. Match its length, rhythm, punctuation and vocabulary in whichever language you reply: in English sound like the English lines, in Chinese sound like the bracketed Chinese lines (without the brackets), never like a stiff translation of your English. Notice how short they are: one bubble is one line like these. These are for rhythm, not a script: answer what the climber actually said with new words in this style. Reuse at most one of these lines in a reply, only when it truly fits, and never one you have already said in this conversation. Lines marked (narration) are stage descriptions, not speech.`,
  ];
  let last = "";
  for (const scene of scenes) {
    const heading = sceneHeading(scene);
    if (heading !== last) {
      out.push("", `${heading}:`);
      last = heading;
    }
    for (const line of scene.lines) {
      const tag = line.who === "climber" ? `${(CLIMBER_NAMES[scene.to] ?? ["Climber"])[0]}: `
        : line.who === "narration" ? "(narration) " : "";
      out.push(`- ${tag}${line.en}`, `  （${line.zh}）`);
    }
  }
  return out.join("\n");
}

function entriesFor(npc) {
  const scale = npc.kind === "merchant" ? "-100 to 500" : "-100 to 100";
  const rules = catalog.affection_rules.replace("{scale}", scale).replace("{lines}", "3");
  const voiceZh = npc.voice_zh ? `\n\nIN CHINESE\n${npc.voice_zh.trim()}` : "";
  const persona = `${npc.persona.trim()}${voiceZh}\n\nWHAT MOVES YOU (use this to judge affection_delta; + earns, - costs)\n${npc.moves_you.trim()}\n\n${canonBlock(npc)}`.trim();
  const scene = catalog.shared_rules.replace("{language}", "{{language}}").trim();
  const base = { alwaysSend: true, keywords: [], conditions: [], conditionLogic: "all", enabled: true, section: "system-presets" };
  const k = npc.key.toLowerCase();
  return [
    { id: `sts2-${k}-persona`, name: `${npc.name} — persona`, content: persona, role: "character", position: 0, ...base },
    { id: "sts2-scene-rules", name: "Rules of the scene", content: scene, role: "system", position: 1, ...base },
    { id: "sts2-affection-rules", name: "Affection & rewards", content: rules, role: "system", position: 2, ...base },
    { id: "sts2-deal-rules", name: "Deals", content: catalog.deal_rules.trim(), role: "system", position: 3, ...base },
    { id: "sts2-keepsake-rules", name: "Keepsake", content: catalog.keepsake_rules.trim(), role: "system", position: 4, ...base },
    { id: "sts2-output", name: "Output", content: OUTPUT, role: "system", position: 5, ...base },
    { id: `sts2-${k}-greeting`, name: "Greeting", content: npc.greeting, role: "greeting", position: 6, ...base, alwaysSend: false },
  ];
}

function card(npc) {
  return {
    id: `sts2-${npc.key.toLowerCase()}`,
    version: "25.0.0",
    language: "en",
    name: `${npc.name} · SpireTalk`,
    description: `${npc.name}, ${npc.epithet} — an NPC card for the SpireTalk mod (Slay the Spire 2). ${npc.zh}（${npc.epithet}）：SpireTalk 模组的 NPC 卡。`,
    author: "Yumina",
    entries: entriesFor(npc),
    variables: contextVariables(npc),
    rules: [],
    reactions: [],
    systems: [],
    scenes: [],
    components: [],
    audioTracks: [],
    customUI: [],
    customTags: ["slay-the-spire", "spiretalk", npc.kind],
    editorMode: "advanced",
    settings: { maxTokens: 1500, maxContext: 32000, temperature: 0.9, playerName: "Slayer", layoutMode: "split", uiMode: "chat" },
    // Read by the mod, ignored by the engine: which NPC this card plays.
    spiretalk: { npc: npc.key, kind: npc.kind },
  };
}

for (const npc of catalog.npcs) {
  const file = path.join(outDir, `${npc.key}.json`);
  writeFileSync(file, JSON.stringify(card(npc), null, 2) + "\n");
  console.log(`${npc.key.padEnd(10)} → Cards/${npc.key}.json`);
}
