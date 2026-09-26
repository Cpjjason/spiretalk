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
- **Deals** — an NPC may ask something of you on arrival: *reach act 2 alive*, *no potions until the next act*, *spend 150 gold at the shop this act*… You answer with a button; the terms come from the mod, never from the AI, so they are always something the game can check. Kept: affection +6 and 50 gold. Broken: nothing but a disappointed line next time and no more deals from that NPC this run. One per NPC at a time, three per run. An accepted deal sits in the relic bar as a relic of its own (hover it for the terms and progress; a shop deal counts the gold still to spend); kept, it flashes and leaves; broken, it greys out until the run ends.
- **Keepsakes** — after your first talk an NPC hands you their keepsake (unless you have made an enemy of them): a relic that grows with the relationship. Level I / II / III follow the tier (stranger / friendly / close); at devoted it becomes their **vow**, the strongest form. It rises mid-run the moment the number crosses, never falls, and changes its picture at every step; every deal you keep with that NPC engraves it (up to 5, remembered across runs). Nine of them, one per NPC, each in that NPC's manner:
  | keepsake | I (stranger) | II (friendly) | III (close) | vow (devoted) | engraving |
  |---|---|---|---|---|---|
  | Neow's | heal 2 after every combat | heal 3 | heal 6 | heal 8, and once per run a lethal hit leaves you at 1 HP | +1 heal |
  | Orobas' | +10 gold after every combat, 10% potion | +15, 20% | +25, 35% | +30, 40%, elites drop an extra relic | +5 gold, +5% |
  | Nonupeipe's | 2 Block at combat start | 3 Block | 5 Block | 6 Block and 1 Dexterity | +1 Block |
  | Pael's | +1 Max HP per victory, 6 per act | +1, 10 per act | +2 | +3 | +2 to the cap |
  | Darv's | +1 card on turn 1 | +2 cards | +2 cards and 1 energy | +3 cards and 1 energy | — |
  | Tanx's | first attack each combat +3 | +4 | +8 | +10, and 1 Strength vs elites and bosses | +1 damage |
  | Tezcatara's | rest heals +6 | +10 | +20 | +24 and 1 potion slot | +2 heal |
  | Vakuu's | +1 Strength at combat start, lose 3 HP | +1, lose 2 | +2, lose 2 | +2, the blood price is waived | — |
  | the Merchant's ledger | 3% off | 5% off | 10% off, card removal free | 20% off, card removal free | +2% |

  Keepsakes are only ever given in conversation; they are never rolled as rewards or sold.
- **Epilogue** — when a run ends, the NPC who knew you best says a few words about it on the game-over screen: the deals, where you fell or how you won. Kept in the profile.
- **Ancient relics on the shelf** — once an Ancient likes you (friendly or better), they may leave one of *their own* relics with the Merchant for you — relics otherwise only found in their halls — priced like a rare. Chance rises with the tier; once per run.
- **On the road** — up to two new events per run, each with its own painting. *The Overturned Cart*: the Merchant's cart has tipped over; help (−5 HP, a potion now, and *The Merchant's Favour*: 15% off at the next shop), pocket a relic (*The Merchant's Grudge*: every shop costs 15% more for the rest of the run), or walk on. Both show in the relic bar. *An Ancient by the Road*: an Ancient you have talked to, or who likes you, sits by the path; a stranger asks for gold, a friend for a potion, someone close asks for nothing. Give it or sit with them, and a random card is upgraded. The NPC speaks in their own voice and remembers it next visit. With AncientWaifus / Merchant2CuteII installed, the paintings switch to match those skins.
- **Memories** — Settings → *Memories* shows, per NPC, the notes they read before every conversation (edit, delete, or add your own, e.g. your climber's name) and the recent conversation. Earlier visits are passed to the model as these short notes, not as the NPC's old lines, and phrases an NPC already said are cut from new replies, so they don't repeat themselves.
- **Temperament** — a setting: lenient / normal / strict. Strict caps each line at ±2, a visit at ±5, hands rewards out only from *close*, and sizes the deals harder.
- **With Ancient Affection** — if that mod is installed, SpireTalk reads and writes *its* affection value, so conversation moves the same number that unlocks its bond relics and marriage. Without it, SpireTalk keeps its own affection per profile, and showing up to talk earns +2 by itself.
- **Single player only.** In multiplayer the Talk option is not offered.

## Setup

Click the mushie in the top-right corner of the main menu (or Mods → SpireTalk → *Open SpireTalk settings*). Pick a source (OpenRouter, OpenAI, Anthropic, Google Gemini, DeepSeek, xAI, Mistral, Groq, Moonshot, SiliconFlow, Alibaba Bailian, Zhipu, a local Ollama, or any other OpenAI-compatible endpoint), paste your key (Ollama needs none), pick a model from the list (or *Other* to type one), and press **Test**; the result shows right underneath.

That is the whole setup. There is no account: the NPCs run on the Yumina engine *inside the game*, and the only thing the mod ever connects to is the endpoint you configure. The Workshop cover and the road-encounter paintings are generated art, redrawn from the game's own characters.

## Cards

Each NPC is a Yumina card — an ordinary Yumina world (persona, rules, variables) as JSON. Nine ship inside the mod. To change how an NPC talks, drop a card into `%APPDATA%\SlayTheSpire2\spiretalk\cards\`: it replaces the built-in one for the NPC it names (`spiretalk.npc`, or an id like `sts2-neow`). Cards can be written in Yumina Studio and exported, or copied from `Cards/` and edited by hand. Sharing a persona means sharing a file.

## Building

Requirements: .NET 9 SDK, Godot 4.5.1 mono (Mega Crit fork, see `Sts2PathDiscovery.props`), the game installed via Steam.

```
dotnet publish -c Release                     # builds SpireTalk.dll, copies it to <game>/mods/SpireTalk/, exports SpireTalk.pck
dotnet build -c Release -p:SpireTalkDev=true   # dev build: compiles in the file-driven test bridge (never ship this one)
```

Delete the old `SpireTalk.pck` before publishing if the export step is skipped as up-to-date.

NPC personas and the affection rules live in `SpireTalk/npcs.json`; `node tools/build-cards.mjs` compiles them into the cards under `Cards/`. The engine itself is `Engine/yumina-runtime.js` — `packages/engine` from the Yumina repo bundled into one file (`pnpm build` there produces `dist/yumina-runtime.js`; copy it over to update). It runs in-game through Jint. Localization: `SpireTalk/localization/{eng,zhs}/` (English and Simplified Chinese; other game languages fall back to English).

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
- **约定** — NPC 见面时可能向你提个要求：「活着走到第 2 章」「到下一章之前不喝药水」「这一章在商店花掉 150 金币」……你按按钮答应或拒绝；题目全部由模组出、不是 AI 编的，所以一定是游戏能核验的事。做到了：好感 +6，另有 50 金币。没做到：只有下次见面一句失望的话，这局这位不再派活。每位同时一条，每局最多三条。答应的约定会变成遗物栏里的一件遗物（鼠标移上去看条款和进度，商店类的约定在图标上显示还差多少金币）；守住了它闪一下消失，没守住它变灰留到这局结束。
- **信物** — 第一次聊过之后，NPC 就会把自己的信物交给你（除非你惹他们生气了）：一件跟关系一起长的遗物。Ⅰ / Ⅱ / Ⅲ 级对应陌生 / 友好 / 亲近，到了挚爱会变成 **誓约信物**，效果最强。一局里好感跨档的瞬间就原地升级，只升不降，图标也跟着变；每守住一次约定，信物上多一道刻痕（最多 5 道，跨局保留）。九件，每位一件，各按各的性子：
  | 信物 | Ⅰ（陌生） | Ⅱ（友好） | Ⅲ（亲近） | 誓约（挚爱） | 刻痕 |
  |---|---|---|---|---|---|
  | 涅奥 | 每场战斗后回 2 | 回 3 | 回 6 | 回 8，每局一次致命伤留 1 血 | +1 回血 |
  | 欧洛巴斯 | 每场战斗后 +10 金，10% 一瓶药水 | +15，20% | +25，35% | +30，40%，精英多掉一件遗物 | +5 金，+5% |
  | 诺奴佩普 | 开局 2 格挡 | 3 格挡 | 5 格挡 | 6 格挡，+1 敏捷 | +1 格挡 |
  | 佩尔 | 每次胜利最大生命 +1，每章封顶 6 | +1，封顶 10 | +2 | +3 | 封顶 +2 |
  | 达弗 | 首回合多抽 1 | 多抽 2 | 多抽 2，首回合 1 能量 | 多抽 3，首回合 1 能量 | — |
  | 坦克斯 | 每场第一次攻击 +3 伤 | +4 | +8 | +10，精英/Boss 战开局 1 力量 | +1 伤 |
  | 特兹卡塔拉 | 休息多回 6 | 多回 10 | 多回 20 | 多回 24，药水栏 +1 | +2 回血 |
  | 瓦库 | 开局 +1 力量、掉 3 血 | +1，掉 2 | +2，掉 2 | +2，免去血的代价 | — |
  | 商人的账本 | 全店 −3% | −5% | −10%，删牌免费 | −20%，删牌免费 | −2% |

  信物只在对话里给，永远不会被随机掉落或摆上货架。
- **旅途回顾** — 一局结束时，和你关系最深的那位 NPC 会在结算画面上写几句：立过的约、倒在哪一层或怎么赢的。存在档案里。
- **先民遗物上架** — 某位先民对你到了友好以上，就可能把**自己的**遗物托给商人留给你——平时只在先民殿堂里才有的那种——按稀有遗物的价格卖。档位越高几率越大；每局一次。
- **路上偶遇** — 每局最多两次新事件，各配一张插画。*翻倒的货车*：商人的货车翻了；帮忙捡（−5 血，现在给一瓶药水，外加遗物「商人的人情」：下次进店 −15%）、顺走一件遗物（遗物「商人的记恨」：这局之后每家店都贵 15%），或者走开。两件都会显示在遗物栏里。*路边的先民*：聊过或对你有好感的先民坐在路边；陌生时讨金币、友好时讨药水、亲近以上什么都不要。给了或坐下陪一会儿，随机升级一张牌。NPC 会用自己的口吻说话，下次见面还记得。装了 AncientWaifus / Merchant2CuteII 时插画自动换成对应画风。
- **角色记忆** — 设置页 →「角色记忆」可以看到每个 NPC 每次开口前会读的笔记（能改、能删、能自己加一句，比如攀登者的名字），以及最近的对话。以前的见面只以这些笔记交给模型、不再发它自己的原话，已经说过的句子也会从新回复里删掉，所以不会翻来覆去说同一句。
- **先民脾气** — 一个设置：宽容 / 正常 / 严苛。严苛档每句最多 ±2、每次拜访最多 ±5、只有亲近以上才给奖励、约定也出得更狠。
- **和 Ancient Affection 联动** — 装了那个模组时，SpireTalk 直接读写它的好感度，聊天推动的就是它解锁好感遗物和结婚的那个数字。没装时，SpireTalk 自己按档案记好感度，来聊一次本身就 +2。
- **仅单人模式。** 多人局不提供交谈选项。

### 设置

点主菜单右上角的 mushie（或 模组配置 → SpireTalk → *打开 SpireTalk 设置*）。选来源（OpenRouter、OpenAI、Anthropic、Google Gemini、DeepSeek、xAI、Mistral、Groq、月之暗面、硅基流动、阿里云百炼、智谱、本机 Ollama，或其他 OpenAI 兼容接口），贴密钥（Ollama 不用），从列表挑模型（或选「其他」手动填），点「测试连接」，结果就写在下面一行。

不填密钥也能玩：NPC 说 `SpireTalk/offline_script.json` 里写好的台词（每人按关系分档的开场白、15 组「回复→回答」、约定和送礼的台词，中英各一套），你从三个回复（热情/平常/冒犯）里选，好感按 +3/+1/−3 走同一套上限规则；约定、信物、偶遇照常，只是不能自己打字、偶遇里没有「搭话」、局末不回顾。免费密钥：国内智谱 glm-4.7-flash，国外 Google AI Studio 的 flash 模型。

设置就这些，不需要任何账号：NPC 由游戏内置的 Yumina 引擎驱动，模组除了你自己填的接口之外不连任何服务器。创意工坊封面和路上偶遇的插画是生成图，按游戏里的角色重绘。

### 卡

每个 NPC 就是一张 Yumina 卡——普通的 Yumina 世界（人设、规则、变量），JSON 格式，模组自带九张。想换一位 NPC 的说话方式，把卡放进 `%APPDATA%\SlayTheSpire2\spiretalk\cards\`，它会顶替内置的那张（按卡里的 `spiretalk.npc`，或者 `sts2-neow` 这样的 id 认 NPC）。卡可以在 Yumina Studio 里写好导出，也可以从 `Cards/` 复制一张手改。分享人设就是分享一个文件。

### 开源

引擎是 Yumina 的：https://github.com/lovetimo0421/yumina-oss
