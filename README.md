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
- **With Ancient Affection** — if that mod is installed, SpireTalk reads and writes *its* affection value, so conversation moves the same number that unlocks its bond relics and marriage. Without it, SpireTalk keeps its own affection per profile, and showing up to talk earns +2 by itself.
- **Single player only.** In multiplayer the Talk option is not offered.

## Setup (pick one)

Click the mushie in the top-right corner of the main menu (or Mods → SpireTalk → *Open SpireTalk settings*).

**A. Your own API key** — the sheet opens on this. Works with OpenRouter (default), OpenAI, or a local Ollama (`http://localhost:11434/v1`, leave the key empty). Enter the base URL, key and a model id, press **Test**; the result shows right underneath.

**B. Yumina account** — the small link in the corner, *Have a Yumina account?*, switches the sheet over. Press **Sign in**: your browser opens a page on yumina.io, you press *Allow*, and the game receives a token by itself (no password is typed in the game). The account line shows who is signed in, credits and plan; the model can be picked from the list. Tokens can be revoked any time at yumina.io → Settings → Connected games.

The mod ships no AI-generated art and phones home to nothing except the endpoint you configure.

## Building

Requirements: .NET 9 SDK, Godot 4.5.1 mono (Mega Crit fork, see `Sts2PathDiscovery.props`), the game installed via Steam.

```
dotnet publish -c Release                     # builds SpireTalk.dll, copies it to <game>/mods/SpireTalk/, exports SpireTalk.pck
dotnet build -c Release -p:SpireTalkDev=true   # dev build: compiles in the file-driven test bridge (never ship this one)
```

Delete the old `SpireTalk.pck` before publishing if the export step is skipped as up-to-date.

NPC personas and the affection rules live in `SpireTalk/npcs.json`; the Yumina worlds are generated from the same file, so both backends speak with one voice. Localization: `SpireTalk/localization/{eng,zhs,zht,jpn,kor}/`.

## Source

Open source. Server side (game tokens, device-code sign-in, the STS2 worlds) lives in the Yumina repo: https://github.com/lovetimo0421/yumina-oss

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
- **和 Ancient Affection 联动** — 装了那个模组时，SpireTalk 直接读写它的好感度，聊天推动的就是它解锁好感遗物和结婚的那个数字。没装时，SpireTalk 自己按档案记好感度，来聊一次本身就 +2。
- **仅单人模式。** 多人局不提供交谈选项。

### 设置（二选一）

点主菜单右上角的 mushie（或 模组配置 → SpireTalk → *打开 SpireTalk 设置*）。

**A. 自己的 API Key** — 设置页默认就是这个。支持 OpenRouter（默认）、OpenAI、本机 Ollama（地址 `http://localhost:11434/v1`，Key 留空）。填地址、Key、模型 id，点「测试」，结果就写在下面一行。

**B. Yumina 账号** — 角落的小字「有 Yumina 账号？」切过去。点「登录」：浏览器打开 yumina.io 的授权页，点「允许」，游戏自己拿到令牌（游戏里不输密码）。账号行显示登录者、积分、套餐，模型在列表里选。令牌随时可在 yumina.io → 设置 → 已连接的游戏 里吊销。

模组不含任何 AI 生成的美术，除了你自己配置的接口之外不连任何服务器。

### 开源

服务端（游戏令牌、设备码登录、STS2 世界）在 Yumina 仓库：https://github.com/lovetimo0421/yumina-oss
