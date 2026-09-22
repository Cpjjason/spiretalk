# SpireTalk: Living Ancients

*Slay the Spire 2* mod — talk to the eight Ancients and the Merchant. Every NPC answers in character, remembers what you said (even across runs), and a good relationship can earn you something real: a fresh set of relics, gold, healing, a potion, a shop discount, or one of Ancient Affection's keepsake relics.

[中文说明在下面](#中文说明)

## What it does

- **Talk** — a new option on every Ancient's page and a **Talk** button in the shop. Three lines per visit: pick one of three suggested replies or type your own. The AI is only called when you actually talk (the shop's own greeting stays vanilla).
- **They remember** — each NPC keeps its own memory, per save profile: what you said to Neow stays with Neow. By default they remember across runs; switch *NPCs remember past runs* off and every run starts fresh.
- **Affection** — every line you say is judged by that NPC's own values (−3 … +3 per line, at most ±8 per visit). Tiers: hostile / stranger / friendly / close / devoted.
- **Rewards** — asked nicely at the right tier, an NPC may:
  | reward | who | tier | limit |
  |---|---|---|---|
  | swap the relics on offer | Ancients | friendly+ | once per visit |
  | gold (30 / 50 / 75) | Ancients | friendly+ | once per run per NPC |
  | heal (12 / 20 / 25 % of max HP) | Ancients | friendly+, only if hurt | once per run per NPC |
  | a potion | Ancients | close+, needs a free slot | once per run per NPC |
  | a keepsake relic (Ancient Affection's bond relics) | Ancients | close+, needs Ancient Affection | once per run per NPC |
  | shop discount (10 / 20 / 30 %) | Merchant | friendly+ | once per visit |
  | a potion on the house | Merchant | devoted, needs a free slot | once per run |

  One reward per visit, never on the greeting, and the AI only *narrates* — the mod validates every reward against these rules and applies it through the game's own commands.
- **Deals** — an NPC may ask something of you on arrival: *reach act 2 alive*, *no potions until the next act*, *spend 150 gold at the shop this act*… You answer with a button; the terms come from the mod, never from the AI, so they are always something the game can check. Kept: affection +6 and 50 gold. Broken: nothing but a disappointed line next time and no more deals from that NPC this run. One per NPC at a time, three per run; open ones are listed under the top bar.
- **Epilogue** — when a run ends, the NPC who knew you best says a few words about it on the game-over screen: the deals, where you fell or how you won. Kept in the profile.
- **Ancient relics on the shelf** — once an Ancient likes you (friendly or better), they may leave one of *their own* relics with the Merchant for you — relics otherwise only found in their halls — priced like a rare. Chance rises with the tier; once per run.
- **Temperament** — a setting: lenient / normal / strict. Strict caps each line at ±2, a visit at ±5, hands rewards out only from *close*, and sizes the deals harder.
- **With Ancient Affection** — if that mod is installed, SpireTalk reads and writes *its* affection value, so conversation moves the same number that unlocks its bond relics and marriage. Without it, SpireTalk keeps its own affection per profile, and showing up to talk earns +2 by itself.
- **Single player only.** In multiplayer the Talk option is not offered.

## Setup

Click the mushie in the top-right corner of the main menu (or Mods → SpireTalk → *Open SpireTalk settings*). Enter an API endpoint, your key and a model id, press **Test**; the result shows right underneath. Works with OpenRouter (default), OpenAI, or a local Ollama (`http://localhost:11434/v1`, leave the key empty) — anything that speaks the OpenAI chat-completions dialect.

That is the whole setup. There is no account: the NPCs run on the Yumina engine *inside the game*, and the only thing the mod ever connects to is the endpoint you configure. No AI-generated art ships with it.

## Cards

Each NPC is a Yumina card — an ordinary Yumina world (persona, rules, variables) as JSON. Nine ship inside the mod. To change how an NPC talks, drop a card into `%APPDATA%\SlayTheSpire2\spiretalk\cards\`: it replaces the built-in one for the NPC it names (`spiretalk.npc`, or an id like `sts2-neow`). Cards can be written in Yumina Studio and exported, or copied from `Cards/` and edited by hand. Sharing a persona means sharing a file.

## Building

Requirements: .NET 9 SDK, Godot 4.5.1 mono (Mega Crit fork, see `Sts2PathDiscovery.props`), the game installed via Steam.

```
dotnet publish -c Release                     # builds SpireTalk.dll, copies it to <game>/mods/SpireTalk/, exports SpireTalk.pck
dotnet build -c Release -p:SpireTalkDev=true   # dev build: compiles in the file-driven test bridge (never ship this one)
```

Delete the old `SpireTalk.pck` before publishing if the export step is skipped as up-to-date.

NPC personas and the affection rules live in `SpireTalk/npcs.json`; `node tools/build-cards.mjs` compiles them into the cards under `Cards/`. The engine itself is `Engine/yumina-runtime.js` — `packages/engine` from the Yumina repo bundled into one file (`pnpm build` there produces `dist/yumina-runtime.js`; copy it over to update). It runs in-game through Jint. Localization: `SpireTalk/localization/{eng,zhs,zht,jpn,kor}/`.

## Source

Open source. The engine is Yumina's: https://github.com/lovetimo0421/yumina-oss

---

## 中文说明

《杀戮尖塔 2》模组：和八位远古先民、以及商人聊天。每个 NPC 都有自己的性格，记得你说过的话（跨局也记得），关系够好还能换来实打实的东西：换一批遗物、金币、治疗、药水、商店折扣，或者 Ancient Affection 的信物遗物。

### 功能

- **交谈** — 每位先民的选项里多一个「交谈」，商店右上多一个「聊聊」。每次最多三句：从三个建议回复里选，或自己打字。只有真开口才会调用 AI（商店原本的招呼还是游戏自己的）。
- **有记忆** — 每个 NPC 各有各的记忆（按存档档案存）：跟涅奥说的话只有涅奥记得。默认跨局记得；把「NPC 记得之前几局的事」关掉，每局都从头认识。
- **好感度** — 你说的每一句都按该 NPC 自己的价值观打分（每句 −3…+3，每次最多 ±8）。分档：敌意 / 陌生 / 友好 / 亲近 / 挚爱。
- **奖励** — 在对的档位好好开口，NPC 可能会：
  | 奖励 | 谁 | 档位 | 上限 |
  |---|---|---|---|
  | 换一批待选遗物 | 先民 | 友好以上 | 每次拜访一次 |
  | 金币（30 / 50 / 75） | 先民 | 友好以上 | 每局每位一次 |
  | 治疗（最大生命 12 / 20 / 25 %） | 先民 | 友好以上，且受伤 | 每局每位一次 |
  | 一瓶药水 | 先民 | 亲近以上，需有空位 | 每局每位一次 |
  | 信物遗物（Ancient Affection 的好感遗物） | 先民 | 亲近以上，需装 Ancient Affection | 每局每位一次 |
  | 商店折扣（10 / 20 / 30 %） | 商人 | 友好以上 | 每次拜访一次 |
  | 免费药水 | 商人 | 挚爱，需有空位 | 每局一次 |

  每次拜访最多一个奖励，开场白不给奖励；AI 只负责「说」，每个奖励都由模组按上面的规则校验后，用游戏自己的指令发放。
- **约定** — NPC 见面时可能向你提个要求：「活着走到第 2 章」「到下一章之前不喝药水」「这一章在商店花掉 150 金币」……你按按钮答应或拒绝；题目全部由模组出、不是 AI 编的，所以一定是游戏能核验的事。做到了：好感 +6，另有 50 金币。没做到：只有下次见面一句失望的话，这局这位不再派活。每位同时一条，每局最多三条；进行中的约定挂在顶栏下面。
- **旅途回顾** — 一局结束时，和你关系最深的那位 NPC 会在结算画面上写几句：立过的约、倒在哪一层或怎么赢的。存在档案里。
- **先民遗物上架** — 某位先民对你到了友好以上，就可能把**自己的**遗物托给商人留给你——平时只在先民殿堂里才有的那种——按稀有遗物的价格卖。档位越高几率越大；每局一次。
- **先民脾气** — 一个设置：宽容 / 正常 / 严苛。严苛档每句最多 ±2、每次拜访最多 ±5、只有亲近以上才给奖励、约定也出得更狠。
- **和 Ancient Affection 联动** — 装了那个模组时，SpireTalk 直接读写它的好感度，聊天推动的就是它解锁好感遗物和结婚的那个数字。没装时，SpireTalk 自己按档案记好感度，来聊一次本身就 +2。
- **仅单人模式。** 多人局不提供交谈选项。

### 设置

点主菜单右上角的 mushie（或 模组配置 → SpireTalk → *打开 SpireTalk 设置*）。填接口地址、Key、模型 id，点「测试」，结果就写在下面一行。支持 OpenRouter（默认）、OpenAI、本机 Ollama（地址 `http://localhost:11434/v1`，Key 留空）——凡是 OpenAI 聊天接口那一套都行。

设置就这些，不需要任何账号：NPC 由游戏内置的 Yumina 引擎驱动，模组除了你自己填的接口之外不连任何服务器。模组不含任何 AI 生成的美术。

### 卡

每个 NPC 就是一张 Yumina 卡——普通的 Yumina 世界（人设、规则、变量），JSON 格式，模组自带九张。想换一位 NPC 的说话方式，把卡放进 `%APPDATA%\SlayTheSpire2\spiretalk\cards\`，它会顶替内置的那张（按卡里的 `spiretalk.npc`，或者 `sts2-neow` 这样的 id 认 NPC）。卡可以在 Yumina Studio 里写好导出，也可以从 `Cards/` 复制一张手改。分享人设就是分享一个文件。

### 开源

引擎是 Yumina 的：https://github.com/lovetimo0421/yumina-oss
