# SpireTalk localization brief

SpireTalk is a Slay the Spire 2 mod: the 8 Ancients (Neow, Orobas, Nonupeipe, Pael, Darv, Tanx,
Tezcatara, Vakuu) and the Merchant talk to the player ("the climber"). You translate the mod into
one game language. The English files are the source; the Simplified Chinese ones (src/zhs) are a
second reference for meaning.

## Inputs (read-only)
- `src/eng/{ancients,events,relics,settings_ui}.json` — interface strings, key → English text
- `src/zhs/...` — same keys in Chinese (reference only)
- `src/script.json` — written dialogue lines: id → {"en": ..., "zh": ...}
- `src/glossary.json` — the GAME'S OWN official terms per language code (NPC names, Strength,
  Block, Energy, Relic, Gold, Rest Site, Elite, The Architect…). Always use these.
- The complete official game localization for every language is in
  `C:/Users/peiji/AppData/Local/Temp/claude/C--Users-peiji-Downloads-yumina/cbfbf01b-660a-476e-8941-603bad00f136/scratchpad/vanilla_loc/localization/<code>/*.json`
  — grep it for any other game term (potion = `potions`, card names, character names in
  `characters.json`, "Act" in `acts.json`, etc.) and match the game's wording.

## Outputs (write exactly these)
- `out/<code>/ancients.json`, `out/<code>/events.json`, `out/<code>/relics.json`,
  `out/<code>/settings_ui.json` — SAME keys as English, values translated. UTF-8, valid JSON.
- `out/<code>/script.json` — flat object: the same ids as `src/script.json` → translated string.

Then run `python check.py <code>` in this folder and fix everything it reports until it prints
`0 error(s)`. Look at every "SAME" line too: translate it unless it truly must stay (brand names like
OpenRouter/DeepSeek/Ollama, URLs, "SpireTalk"). Do not stop before check.py passes.

## Hard rules
1. Keep every `{Placeholder}`, `{0}`, and color tag (`[gold]…[/gold]`, `[red]`, `[blue]`, `[green]`)
   exactly, same count. Move them where the grammar needs, never translate the name inside braces.
2. Script lines: keep the SAME number of `\n` line breaks (each line is one speech bubble).
3. Keep the voices. Neow speaks in broken, whispered fragments with lots of ".." and "..." —
   reproduce that rhythm in the target language. The Merchant is dry, curt shop talk. Tezcatara
   is a warm, doting grandmother. Orobas is a gentle, curious giant eye. Vakuu is a smooth, bargaining contract demon. Tanx is a hungry, simple beast. Darv is a folksy, chatty hoarder. Nonupeipe is a
   vain, sleepy, pampered creature. Pael is sleepy, bloated, lazy. If these one-line sketches disagree with the actual lines, follow the lines. Read the English and Chinese
   together to catch the tone; do not flatten it into neutral prose.
4. Choices (script ids ending in /choice) are the CLIMBER's words: plain, natural first person.
5. Interface text: short and plain, like the game's own menus. Buttons stay short (they sit in a
   row of four); prefer the game's own wording for common actions (Proceed, Leave, Settings…).
6. Mod-only terms: "Keepsake" (a relic an NPC gives you), "Vow" (its top level), "Deal" (a promise
   you make to an NPC), "Engravings", "Memories", "Written lines". Pick one natural word for each
   and use it consistently everywhere (relics, UI, script).
7. Address the player the way the game's own localization does in that language (formality level).
8. Do not add, drop or explain content. No translator notes in the files.
9. Output only the files; do not edit anything under src/.
