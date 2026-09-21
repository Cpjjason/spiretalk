"use strict";
var Yumina = (() => {
  var __create = Object.create;
  var __defProp = Object.defineProperty;
  var __getOwnPropDesc = Object.getOwnPropertyDescriptor;
  var __getOwnPropNames = Object.getOwnPropertyNames;
  var __getProtoOf = Object.getPrototypeOf;
  var __hasOwnProp = Object.prototype.hasOwnProperty;
  var __defNormalProp = (obj, key, value) => key in obj ? __defProp(obj, key, { enumerable: true, configurable: true, writable: true, value }) : obj[key] = value;
  var __esm = (fn, res) => function __init() {
    return fn && (res = (0, fn[__getOwnPropNames(fn)[0]])(fn = 0)), res;
  };
  var __commonJS = (cb, mod) => function __require() {
    return mod || (0, cb[__getOwnPropNames(cb)[0]])((mod = { exports: {} }).exports, mod), mod.exports;
  };
  var __export = (target, all) => {
    for (var name in all)
      __defProp(target, name, { get: all[name], enumerable: true });
  };
  var __copyProps = (to, from, except, desc) => {
    if (from && typeof from === "object" || typeof from === "function") {
      for (let key of __getOwnPropNames(from))
        if (!__hasOwnProp.call(to, key) && key !== except)
          __defProp(to, key, { get: () => from[key], enumerable: !(desc = __getOwnPropDesc(from, key)) || desc.enumerable });
    }
    return to;
  };
  var __toESM = (mod, isNodeMode, target) => (target = mod != null ? __create(__getProtoOf(mod)) : {}, __copyProps(
    // If the importer is in node compatibility mode or this is not an ESM
    // file that has been converted to a CommonJS file using a Babel-
    // compatible transform (i.e. "__esModule" has not been set), then set
    // "default" to the CommonJS "module.exports" for node compatibility.
    isNodeMode || !mod || !mod.__esModule ? __defProp(target, "default", { value: mod, enumerable: true }) : target,
    mod
  ));
  var __toCommonJS = (mod) => __copyProps(__defProp({}, "__esModule", { value: true }), mod);
  var __publicField = (obj, key, value) => __defNormalProp(obj, typeof key !== "symbol" ? key + "" : key, value);

  // ../../node_modules/.pnpm/base64-js@1.5.1/node_modules/base64-js/index.js
  var require_base64_js = __commonJS({
    "../../node_modules/.pnpm/base64-js@1.5.1/node_modules/base64-js/index.js"(exports) {
      "use strict";
      exports.byteLength = byteLength;
      exports.toByteArray = toByteArray;
      exports.fromByteArray = fromByteArray;
      var lookup = [];
      var revLookup = [];
      var Arr = typeof Uint8Array !== "undefined" ? Uint8Array : Array;
      var code = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
      for (i = 0, len = code.length; i < len; ++i) {
        lookup[i] = code[i];
        revLookup[code.charCodeAt(i)] = i;
      }
      var i;
      var len;
      revLookup["-".charCodeAt(0)] = 62;
      revLookup["_".charCodeAt(0)] = 63;
      function getLens(b64) {
        var len2 = b64.length;
        if (len2 % 4 > 0) {
          throw new Error("Invalid string. Length must be a multiple of 4");
        }
        var validLen = b64.indexOf("=");
        if (validLen === -1) validLen = len2;
        var placeHoldersLen = validLen === len2 ? 0 : 4 - validLen % 4;
        return [validLen, placeHoldersLen];
      }
      function byteLength(b64) {
        var lens = getLens(b64);
        var validLen = lens[0];
        var placeHoldersLen = lens[1];
        return (validLen + placeHoldersLen) * 3 / 4 - placeHoldersLen;
      }
      function _byteLength(b64, validLen, placeHoldersLen) {
        return (validLen + placeHoldersLen) * 3 / 4 - placeHoldersLen;
      }
      function toByteArray(b64) {
        var tmp;
        var lens = getLens(b64);
        var validLen = lens[0];
        var placeHoldersLen = lens[1];
        var arr = new Arr(_byteLength(b64, validLen, placeHoldersLen));
        var curByte = 0;
        var len2 = placeHoldersLen > 0 ? validLen - 4 : validLen;
        var i2;
        for (i2 = 0; i2 < len2; i2 += 4) {
          tmp = revLookup[b64.charCodeAt(i2)] << 18 | revLookup[b64.charCodeAt(i2 + 1)] << 12 | revLookup[b64.charCodeAt(i2 + 2)] << 6 | revLookup[b64.charCodeAt(i2 + 3)];
          arr[curByte++] = tmp >> 16 & 255;
          arr[curByte++] = tmp >> 8 & 255;
          arr[curByte++] = tmp & 255;
        }
        if (placeHoldersLen === 2) {
          tmp = revLookup[b64.charCodeAt(i2)] << 2 | revLookup[b64.charCodeAt(i2 + 1)] >> 4;
          arr[curByte++] = tmp & 255;
        }
        if (placeHoldersLen === 1) {
          tmp = revLookup[b64.charCodeAt(i2)] << 10 | revLookup[b64.charCodeAt(i2 + 1)] << 4 | revLookup[b64.charCodeAt(i2 + 2)] >> 2;
          arr[curByte++] = tmp >> 8 & 255;
          arr[curByte++] = tmp & 255;
        }
        return arr;
      }
      function tripletToBase64(num) {
        return lookup[num >> 18 & 63] + lookup[num >> 12 & 63] + lookup[num >> 6 & 63] + lookup[num & 63];
      }
      function encodeChunk(uint8, start, end) {
        var tmp;
        var output = [];
        for (var i2 = start; i2 < end; i2 += 3) {
          tmp = (uint8[i2] << 16 & 16711680) + (uint8[i2 + 1] << 8 & 65280) + (uint8[i2 + 2] & 255);
          output.push(tripletToBase64(tmp));
        }
        return output.join("");
      }
      function fromByteArray(uint8) {
        var tmp;
        var len2 = uint8.length;
        var extraBytes = len2 % 3;
        var parts = [];
        var maxChunkLength = 16383;
        for (var i2 = 0, len22 = len2 - extraBytes; i2 < len22; i2 += maxChunkLength) {
          parts.push(encodeChunk(uint8, i2, i2 + maxChunkLength > len22 ? len22 : i2 + maxChunkLength));
        }
        if (extraBytes === 1) {
          tmp = uint8[len2 - 1];
          parts.push(
            lookup[tmp >> 2] + lookup[tmp << 4 & 63] + "=="
          );
        } else if (extraBytes === 2) {
          tmp = (uint8[len2 - 2] << 8) + uint8[len2 - 1];
          parts.push(
            lookup[tmp >> 10] + lookup[tmp >> 4 & 63] + lookup[tmp << 2 & 63] + "="
          );
        }
        return parts.join("");
      }
    }
  });

  // src/runtime/shims/no-ranks.ts
  var no_ranks_exports = {};
  __export(no_ranks_exports, {
    default: () => no_ranks_default
  });
  var no_ranks_default;
  var init_no_ranks = __esm({
    "src/runtime/shims/no-ranks.ts"() {
      "use strict";
      no_ranks_default = void 0;
    }
  });

  // src/runtime/bundle-entry.ts
  var bundle_entry_exports = {};
  __export(bundle_entry_exports, {
    GameStateManager: () => GameStateManager,
    PromptBuilder: () => PromptBuilder,
    RUNTIME_VERSION: () => RUNTIME_VERSION,
    ResponseParser: () => ResponseParser,
    applyVariables: () => applyVariables,
    buildTurnPrompt: () => buildTurnPrompt,
    finishTurn: () => finishTurn,
    migrateWorldDefinition: () => migrateWorldDefinition,
    runTurn: () => runTurn,
    startSession: () => startSession,
    validateWorld: () => validateWorld
  });

  // src/rules/rule-state.ts
  function createEmptyRuleState() {
    return {
      disabledRules: [],
      activeDirectives: [],
      cooldowns: {},
      fireCounts: {},
      prevVars: {},
      toggledEntries: {},
      toggledVariables: {}
    };
  }

  // src/state/path-utils.ts
  function getByPath(variables, path) {
    if (!path) return void 0;
    if (Object.prototype.hasOwnProperty.call(variables, path)) return variables[path];
    let current = variables;
    for (const part of path.split(".")) {
      if (current === null || typeof current !== "object") return void 0;
      current = current[part];
    }
    return current;
  }

  // src/systems/poison-survival.ts
  var POISON_SYSTEM = "kochuu-survival-v1";
  var POISON_ROSTER = "\u4F50\u5009\u4E95\u541B\u83EF,\u9CF3\u60E0\u4ECB,\u6CA2\u6751\u9032,\u690E\u8449\u68A8\u82B1,\u6709\u6CA2\u60A0\u5E73,,\u9928\u6797\u98AF\u592A,\u52A0\u9808\u4E00\u8F1D,\u4F50\u91CE\u7409,\u6817\u5712\u7A42\u82B1,\u524D\u7530\u84EE,\u658E\u85E4\u6643,\u6D45\u4E95\u51CC,\u5C0F\u91CE\u5BFA\u96BC,\u6C38\u702C\u6850\u4EBA,\u4E09\u5B85\u7D50\u8863,\u5CB8\u672C\u6714,\u5BAE\u4E0B\u82B9\u5948,\u7530\u6751\u58EE\u99AC,\u4E0A\u539F\u5343\u5C0B,\u9ED2\u6728\u84BC,\u4E2D\u5CF6\u9065,\u677E\u672C\u96C4\u54C9,\u7DBE\u702C\u7F8E\u6708,\u85E4\u539F\u98AF,\u6CB3\u91CE\u6674\u9999,\u6850\u5CF6\u62D3\u6D77,\u897F\u6751\u67DA\u5E0C,\u5927\u6CA2\u5F8B,\u9577\u8C37\u5DDD\u51EA,\u6728\u6751\u86CD,\u77F3\u4E95\u6E4A,\u8352\u6728\u7D17\u826F,\u5742\u672C\u4E98,\u91CE\u53E3\u967D\u83DC,\u571F\u5C4B\u67CA,\u516B\u6728\u6CBC\u51DB,\u4E38\u5C71\u900F,\u798F\u7530\u4F0A\u5439,\u93D1\u6728\u9633\u4E00".split(",");
  var POISON_BAGS = {
    "\u6C89\u91CD\u5E06\u5E03\u5305": ["\u624B\u65A7", "\u7EF3\u7D22", "\u624B\u7535\u7B52", "\u538B\u7F29\u5E72\u7CAE\xD76", "\u6C34\xD73"],
    "\u533B\u7597\u6025\u6551\u7BB1": ["\u533B\u7528\u526A\u5200", "\u7EF7\u5E26\xD73", "\u6D88\u6BD2\u6DB2", "\u6B62\u75DB\u836F\xD72", "\u538B\u7F29\u5E72\u7CAE\xD76", "\u6C34\xD73"],
    "\u5B66\u751F\u4F1A\u516C\u6587\u5305": ["\u4FE1\u53F7\u67AA", "\u7167\u660E\u5F39\xD72", "\u624B\u7ED8\u533A\u57DF\u5730\u56FE", "\u538B\u7F29\u5E72\u7CAE\xD76", "\u6C34\xD73"]
  };
  function poisonName(value) {
    const variants = { "\u6075": "\u60E0", "\u8535": "\u4ED3", "\u5009": "\u4ED3", "\u83EF": "\u534E", "\u9CF3": "\u51E4", "\u6FA4": "\u6CFD", "\u6CA2": "\u6CFD", "\u9032": "\u8FDB", "\u8449": "\u53F6", "\u9928": "\u9986", "\u98AF": "\u98D2", "\u9808": "\u987B", "\u8F1D": "\u8F89", "\u5712": "\u56ED", "\u7A42": "\u7A57", "\u84EE": "\u83B2", "\u658E": "\u658B", "\u9F4B": "\u658B", "\u6DFA": "\u6D45", "\u702C": "\u6FD1", "\u7028": "\u6FD1", "\u7D50": "\u7ED3", "\u5BAE": "\u5BAB", "\u58EF": "\u58EE", "\u99AC": "\u9A6C", "\u5C0B": "\u5BFB", "\u9ED2": "\u9ED1", "\u84BC": "\u82CD", "\u5CF6": "\u5C9B", "\u5D8B": "\u5C9B", "\u9059": "\u9065", "\u7DBE": "\u7EEB", "\u9577": "\u957F", "\u86CD": "\u8424", "\u87A2": "\u8424", "\u6E4A": "\u51D1", "\u7D17": "\u7EB1", "\u4E99": "\u4E98", "\u967D": "\u9633", "\u51DC": "\u51DB", "\u93D1": "\u955D" };
    return String(value != null ? value : "").trim().replace(/[（(].*?[）)]/g, "").replace(/\s/g, "").split("").map((c) => variants[c] || c).join("");
  }
  var POISON_PERIOD_ALIASES = [
    ["\u4E0A\u5348", "\u4E0A\u5348"],
    ["\u65E9\u4E0A", "\u4E0A\u5348"],
    ["\u65E9\u6668", "\u4E0A\u5348"],
    ["\u6E05\u6668", "\u4E0A\u5348"],
    ["\u9ECE\u660E", "\u4E0A\u5348"],
    ["\u62C2\u6653", "\u4E0A\u5348"],
    ["\u7834\u6653", "\u4E0A\u5348"],
    ["\u767D\u5929", "\u4E0A\u5348"],
    ["\u4E2D\u5348", "\u4E0A\u5348"],
    ["\u6B63\u5348", "\u4E0A\u5348"],
    ["morning", "\u4E0A\u5348"],
    ["dawn", "\u4E0A\u5348"],
    ["noon", "\u4E0A\u5348"],
    ["daytime", "\u4E0A\u5348"],
    ["\u4E0B\u5348", "\u4E0B\u5348"],
    ["\u5348\u540E", "\u4E0B\u5348"],
    ["\u508D\u665A", "\u4E0B\u5348"],
    ["\u9EC4\u660F", "\u4E0B\u5348"],
    ["afternoon", "\u4E0B\u5348"],
    ["evening", "\u4E0B\u5348"],
    ["dusk", "\u4E0B\u5348"],
    ["\u591C\u665A", "\u591C\u665A"],
    ["\u665A\u4E0A", "\u591C\u665A"],
    ["\u6DF1\u591C", "\u591C\u665A"],
    ["\u534A\u591C", "\u591C\u665A"],
    ["\u51CC\u6668", "\u591C\u665A"],
    ["\u591C\u95F4", "\u591C\u665A"],
    ["\u591C", "\u591C\u665A"],
    ["night", "\u591C\u665A"],
    ["midnight", "\u591C\u665A"]
  ];
  var POISON_NEXT_PERIOD = { "\u4E0A\u5348": "\u4E0B\u5348", "\u4E0B\u5348": "\u591C\u665A", "\u591C\u665A": "\u4E0A\u5348" };
  function poisonPeriod(value) {
    const text = String(value != null ? value : "").toLowerCase();
    let best = null;
    for (const [alias, period] of POISON_PERIOD_ALIASES) {
      const index = text.lastIndexOf(alias.toLowerCase());
      if (index < 0) continue;
      const end = index + alias.length;
      if (!best || end > best.end || end === best.end && alias.length > best.length) {
        best = { end, length: alias.length, period };
      }
    }
    return best ? best.period : null;
  }
  function poisonPeriodAdvance(oldValue, newValue) {
    const to = poisonPeriod(newValue);
    if (to === null) return null;
    const from = poisonPeriod(oldValue);
    if (from === null) return to;
    return POISON_NEXT_PERIOD[from] === to ? to : null;
  }
  function record(value) {
    return value !== null && typeof value === "object" && !Array.isArray(value) ? value : {};
  }
  function array(value) {
    try {
      const parsed = typeof value === "string" ? JSON.parse(value) : value;
      return Array.isArray(parsed) ? parsed : [];
    } catch {
      return [];
    }
  }
  function points(value) {
    const n = Number(value);
    return Number.isFinite(n) ? Math.max(0, Math.floor(n)) : 0;
  }
  function settlePoisonState(input) {
    var _a;
    const state = { ...input, variables: { ...input.variables }, metadata: { ...input.metadata } };
    const v = state.variables;
    const prior = record(state.metadata.poison);
    const setup = record(v["player-setup"]);
    let initialized = prior.initialized === true;
    if (!v["player-name"] && state.turnCount > 0) v["player-name"] = "\u7121\u540D\u306E\u751F\u5F92";
    if (!initialized && !v["player-name"] && state.turnCount === 0 && typeof setup.name === "string" && setup.name.trim()) {
      const name = setup.name.trim().slice(0, 80);
      if (POISON_ROSTER.some((n) => n && poisonName(n) === poisonName(name)) || /権藤|权藤/.test(name)) return input;
      const bag = typeof setup.bag === "string" && Object.hasOwn(POISON_BAGS, setup.bag) ? setup.bag : "\u6C89\u91CD\u5E06\u5E03\u5305";
      v["player-name"] = name;
      for (const field of ["gender", "class", "personality", "relation", "weakness", "desc"]) v["player-" + field] = String((_a = setup[field]) != null ? _a : "").slice(0, 2e3);
      v["player-weapon-pref"] = bag;
      v.inventory = POISON_BAGS[bag].slice(1);
      v["user-weapon"] = POISON_BAGS[bag][0];
      v["day-count"] = 1;
      v["time-period"] = "\u4E0A\u5348";
      v.health = 100;
      v.hunger = 100;
      initialized = true;
    }
    const player = String(v["player-name"] || "");
    const roster = POISON_ROSTER.map((n) => n || player);
    const names = new Map(roster.filter(Boolean).map((n) => [poisonName(n), n]));
    const canonical = (value) => {
      const key = poisonName(value);
      if (!key) return void 0;
      const exact = names.get(key);
      if (exact) return exact;
      if (key.length < 2) return void 0;
      const partial = [...names.keys()].filter((n) => n.startsWith(key) || n.endsWith(key));
      return partial.length === 1 ? names.get(partial[0]) : void 0;
    };
    const ledger = array(prior.ledger).map(record).filter((e) => canonical(e.victim));
    const seen = new Set(ledger.map((e) => poisonName(e.victim)));
    const kp = Object.fromEntries(Object.entries(record(prior.kp)).map(([n, p]) => [n, points(p)]));
    const migrating = prior.version !== 1;
    const unresolved = new Map(array(prior.unresolved).map((item) => [JSON.stringify(item), item]));
    if (migrating) {
      for (const item of String(v["npc-kp"] || "").split("|")) {
        const [n, p] = item.split(/[:：]/);
        const who = canonical(n);
        if (who) kp[who] = Math.max(kp[who] || 0, points(p));
      }
      if (player) kp[player] = points(v["user-kp"]);
    }
    const add = (raw, award) => {
      const victim = canonical(raw.victim);
      if (!victim) {
        if (String(raw.victim || "").trim() && !/権藤|权藤/.test(String(raw.victim))) unresolved.set(JSON.stringify(raw), raw);
        return;
      }
      if (seen.has(poisonName(victim))) return;
      const killer = canonical(raw.killer);
      const gain = award && killer && killer !== victim && !seen.has(poisonName(killer)) ? 1 + (kp[victim] || 0) : 0;
      if (killer && gain) kp[killer] = (kp[killer] || 0) + gain;
      seen.add(poisonName(victim));
      ledger.push({ victim, killer: killer || "\u4E0D\u660E", method: String(raw.method || "\u6B7B\u4EA1\u8BB0\u5F55").slice(0, 300), day: Math.min(7, Math.max(1, points(raw.day || v["day-count"]))), kp: gain, survivors: 40 - seen.size });
    };
    for (const item of array(v["kill-log"])) add(record(item), !migrating);
    for (const victim of String(v["dead-names"] || "").split(/[,，、;；\n]/)) add({ victim }, false);
    let status = prior.status === "dead" || prior.status === "won" ? prior.status : "active";
    const started = initialized || !!player || state.turnCount > 0;
    const vitalsDown = Number(v.health) <= 0 || Number(v.hunger) <= 0;
    let vitalsGrace = prior.vitalsGrace === true;
    if (migrating && started && vitalsDown) vitalsGrace = true;
    if (!vitalsDown) vitalsGrace = false;
    if (started && status === "active") {
      if (player && seen.has(poisonName(player))) status = "dead";
      else if (vitalsDown && !vitalsGrace) status = "dead";
      else if (Number(v["day-count"]) >= 8) status = "won";
    }
    if (status === "dead") {
      v.health = 0;
      if (player) add({ victim: player, method: Number(v.hunger) <= 0 ? "\u9965\u997F" : "\u4F53\u529B\u8017\u5C3D" }, false);
    }
    if (started && Number(v["day-count"]) < 1) v["day-count"] = 1;
    const period = poisonPeriod(v["time-period"]);
    if (period !== null && v["time-period"] !== period) v["time-period"] = period;
    v["game-status"] = status;
    v["dead-names"] = ledger.map((e) => e.victim).join(",");
    v["survivors"] = 40 - seen.size;
    v["kill-log"] = ledger;
    v["user-kp"] = kp[player] || 0;
    v["npc-kp"] = Object.entries(kp).filter(([n]) => n !== player).map(([n, p]) => n + ":" + p).join("|");
    v["unresolved-deaths"] = [...unresolved.values()];
    state.metadata.poison = { version: 1, initialized: started, status, ledger, kp, vitalsGrace, unresolved: [...unresolved.values()] };
    if (state.ruleState) state.ruleState = { ...state.ruleState, activeDirectives: state.ruleState.activeDirectives.filter((d) => d.id !== "force_death") };
    return state;
  }

  // src/state/game-state-manager.ts
  var GameStateManager = class {
    constructor(world, existingState) {
      __publicField(this, "state");
      __publicField(this, "variables");
      __publicField(this, "nameToId");
      __publicField(this, "listeners", /* @__PURE__ */ new Set());
      __publicField(this, "worldId");
      __publicField(this, "defaultVariables");
      __publicField(this, "poisonSurvival");
      /**
       * Writes the engine refused. Drained by the caller (the message route) so a
       * refusal is logged rather than silent — silence is how one malformed
       * directive turned a card's entire inventory into the string
       * "delete 1, delete 0" and the player's items were gone for good.
       */
      __publicField(this, "rejectedWrites", []);
      var _a;
      this.poisonSurvival = ((_a = world.systems) == null ? void 0 : _a.includes(POISON_SYSTEM)) === true;
      this.variables = new Map(world.variables.map((v) => [v.id, v]));
      this.nameToId = new Map(world.variables.map((v) => [v.name, v.id]));
      this.worldId = world.id;
      this.defaultVariables = Object.fromEntries(
        world.variables.map((v) => [v.id, v.defaultValue])
      );
      this.state = this.normalizeState(existingState);
    }
    get(variableId) {
      return this.state.variables[variableId];
    }
    set(variableId, value) {
      const resolved = this.resolveVariable(variableId);
      if (!resolved) return;
      const { variable, resolvedId } = resolved;
      const oldValue = this.state.variables[resolvedId];
      if (oldValue === void 0) return;
      const coerced = this.coerceJsonWrite(variable, oldValue, value);
      if (!coerced.ok) return;
      const validated = this.validateValue(variable, coerced.value);
      if (oldValue === validated) return;
      this.state = {
        ...this.state,
        variables: { ...this.state.variables, [resolvedId]: validated }
      };
      for (const listener of this.listeners) {
        listener(resolvedId, oldValue, validated);
      }
    }
    applyEffects(effects) {
      const changes = [];
      for (const rawEffect of effects) {
        let effect = rawEffect;
        if (rawEffect.valueRef !== void 0) {
          const operand = this.resolveOperand(rawEffect.valueRef);
          if (operand === void 0) continue;
          effect = { ...rawEffect, value: operand };
        }
        if (effect.variableId.includes(".")) {
          const { rootId, applied, oldValue: oldValue2, newValue: newValue2 } = this.resolvePathEffect(effect);
          if (applied) {
            changes.push({ variableId: rootId, oldValue: oldValue2, newValue: newValue2 });
          }
          continue;
        }
        const resolved = this.resolveVariable(effect.variableId);
        if (!resolved) continue;
        const { variable, resolvedId } = resolved;
        const oldValue = this.state.variables[resolvedId];
        if (oldValue === void 0) continue;
        if (this.poisonSurvival && resolvedId === "time-period") {
          if (effect.operation !== "set" || this.state.metadata.poisonTimeTurn === this.state.turnCount) continue;
          const advanced = poisonPeriodAdvance(oldValue, effect.value);
          if (advanced === null) continue;
          effect = { ...effect, value: advanced };
          this.state = { ...this.state, metadata: { ...this.state.metadata, poisonTimeTurn: this.state.turnCount } };
        }
        let newValue;
        switch (effect.operation) {
          case "set":
            newValue = effect.value;
            break;
          case "add":
            newValue = typeof oldValue === "number" && typeof effect.value === "number" ? oldValue + effect.value : oldValue;
            break;
          case "subtract":
            newValue = typeof oldValue === "number" && typeof effect.value === "number" ? oldValue - effect.value : oldValue;
            break;
          case "multiply":
            newValue = typeof oldValue === "number" && typeof effect.value === "number" ? oldValue * effect.value : oldValue;
            break;
          case "toggle":
            newValue = typeof oldValue === "boolean" ? !oldValue : oldValue;
            break;
          case "append":
            newValue = typeof oldValue === "string" && typeof effect.value === "string" ? oldValue + effect.value : oldValue;
            break;
          case "merge": {
            if (typeof oldValue === "object" && oldValue !== null && !Array.isArray(oldValue) && typeof effect.value === "object" && effect.value !== null && !Array.isArray(effect.value)) {
              newValue = { ...oldValue, ...effect.value };
            } else {
              continue;
            }
            break;
          }
          case "push": {
            if (Array.isArray(oldValue)) {
              newValue = [...oldValue, effect.value];
            } else {
              continue;
            }
            break;
          }
          case "delete": {
            if (typeof oldValue === "object" && oldValue !== null && !Array.isArray(oldValue) && typeof effect.value === "string") {
              const copy = { ...oldValue };
              delete copy[effect.value];
              newValue = copy;
            } else if (Array.isArray(oldValue) && typeof effect.value === "number") {
              newValue = oldValue.filter((_, i) => i !== effect.value);
            } else {
              continue;
            }
            break;
          }
          default:
            continue;
        }
        const coerced = this.coerceJsonWrite(variable, oldValue, newValue);
        if (!coerced.ok) continue;
        const validated = this.validateValue(variable, coerced.value);
        if (oldValue !== validated) {
          changes.push({
            variableId: resolvedId,
            oldValue,
            newValue: validated
          });
        }
        this.state = {
          ...this.state,
          variables: {
            ...this.state.variables,
            [resolvedId]: validated
          }
        };
      }
      for (const change of changes) {
        for (const listener of this.listeners) {
          listener(change.variableId, change.oldValue, change.newValue);
        }
      }
      return changes;
    }
    getSnapshot() {
      return {
        ...this.state,
        variables: { ...this.state.variables },
        metadata: { ...this.state.metadata },
        ruleState: this.cloneRuleState(this.state.ruleState)
      };
    }
    /** Settle opted-in deterministic rules after the complete reaction chain. */
    settleSystems() {
      if (!this.poisonSurvival) return [];
      const before = this.state;
      this.state = settlePoisonState(before);
      return Object.entries(this.state.variables).flatMap(([variableId, newValue]) => {
        const oldValue = before.variables[variableId];
        return oldValue !== void 0 && JSON.stringify(oldValue) !== JSON.stringify(newValue) ? [{ variableId, oldValue, newValue }] : [];
      });
    }
    loadSnapshot(state) {
      this.state = this.normalizeState(state);
    }
    incrementTurn() {
      this.state = { ...this.state, turnCount: this.state.turnCount + 1 };
    }
    getMetadata(key) {
      return this.state.metadata[key];
    }
    setMetadata(key, value) {
      this.state = {
        ...this.state,
        metadata: { ...this.state.metadata, [key]: value }
      };
    }
    onChange(callback) {
      this.listeners.add(callback);
      return () => {
        this.listeners.delete(callback);
      };
    }
    // ── RuleState Management ──
    /** Get the current rule runtime state */
    getRuleState() {
      var _a;
      return (_a = this.state.ruleState) != null ? _a : createEmptyRuleState();
    }
    /** Add or replace a directive */
    injectDirective(directive) {
      const ruleState = this.getRuleState();
      const filtered = ruleState.activeDirectives.filter((d) => d.id !== directive.id);
      this.state = {
        ...this.state,
        ruleState: {
          ...ruleState,
          activeDirectives: [...filtered, directive]
        }
      };
    }
    /** Remove a directive by ID */
    removeDirective(directiveId) {
      const ruleState = this.getRuleState();
      this.state = {
        ...this.state,
        ruleState: {
          ...ruleState,
          activeDirectives: ruleState.activeDirectives.filter((d) => d.id !== directiveId)
        }
      };
    }
    /** Expire directives whose duration has been exceeded */
    expireDirectives(currentTurn) {
      const ruleState = this.getRuleState();
      const expired = [];
      const remaining = [];
      for (const d of ruleState.activeDirectives) {
        if (d.duration !== void 0 && currentTurn - d.injectedAtTurn >= d.duration) {
          expired.push(d);
        } else {
          remaining.push(d);
        }
      }
      if (expired.length > 0) {
        this.state = {
          ...this.state,
          ruleState: { ...ruleState, activeDirectives: remaining }
        };
      }
      return expired;
    }
    /** Toggle a rule's enabled state at runtime */
    toggleRule(ruleId, enabled) {
      const ruleState = this.getRuleState();
      const disabledRules = enabled ? ruleState.disabledRules.filter((id) => id !== ruleId) : [.../* @__PURE__ */ new Set([...ruleState.disabledRules, ruleId])];
      this.state = {
        ...this.state,
        ruleState: { ...ruleState, disabledRules }
      };
    }
    /** Toggle an entry's enabled state at runtime */
    toggleEntry(entryId, enabled) {
      const ruleState = this.getRuleState();
      this.state = {
        ...this.state,
        ruleState: {
          ...ruleState,
          toggledEntries: { ...ruleState.toggledEntries, [entryId]: enabled }
        }
      };
    }
    /** Override a variable's enable gate at runtime (via @vars.enabled.<id>).
     *  Beats the variable's authored `enabled` default in both directions —
     *  see state/variable-activation.ts. */
    toggleVariable(variableId, enabled) {
      var _a;
      const ruleState = this.getRuleState();
      this.state = {
        ...this.state,
        ruleState: {
          ...ruleState,
          toggledVariables: { ...(_a = ruleState.toggledVariables) != null ? _a : {}, [variableId]: enabled }
        }
      };
    }
    /** Record that a rule fired (for cooldown and fire count tracking) */
    recordRuleFired(ruleId, currentTurn, cooldownTurns) {
      var _a;
      const ruleState = this.getRuleState();
      const fireCounts = { ...ruleState.fireCounts };
      fireCounts[ruleId] = ((_a = fireCounts[ruleId]) != null ? _a : 0) + 1;
      const cooldowns = { ...ruleState.cooldowns };
      if (cooldownTurns !== void 0 && cooldownTurns > 0) {
        cooldowns[ruleId] = currentTurn + cooldownTurns;
      }
      this.state = {
        ...this.state,
        ruleState: { ...ruleState, fireCounts, cooldowns }
      };
    }
    /** Snapshot current variables for next turn's variable-crossed detection */
    snapshotPrevVars() {
      const ruleState = this.getRuleState();
      this.state = {
        ...this.state,
        ruleState: {
          ...ruleState,
          prevVars: { ...this.state.variables }
        }
      };
    }
    /**
     * Apply rule actions to this state manager.
     * Handles all action types: modify-variable, inject-directive, remove-directive,
     * toggle-entry, toggle-rule. Returns notifications and send-context messages.
     */
    applyRuleActions(actions, firedRuleIds, rules) {
      var _a, _b, _c;
      const notifications = [];
      const contextMessages = [];
      for (const action of actions) {
        switch (action.type) {
          case "modify-variable": {
            this.applyEffects([{
              variableId: action.variableId,
              operation: action.operation,
              value: action.value
            }]);
            break;
          }
          case "inject-directive": {
            this.injectDirective({
              id: action.directiveId,
              content: action.content,
              position: (_a = action.position) != null ? _a : "auto",
              sourceRuleId: "",
              // Will be set by caller if needed
              persistent: action.persistent !== false,
              injectedAtTurn: this.state.turnCount,
              duration: action.duration
            });
            break;
          }
          case "remove-directive": {
            this.removeDirective(action.directiveId);
            break;
          }
          case "toggle-entry": {
            this.toggleEntry(action.entryId, action.enabled);
            break;
          }
          case "toggle-rule": {
            this.toggleRule(action.ruleId, action.enabled);
            break;
          }
          case "notify-player": {
            notifications.push({
              message: action.message,
              style: (_b = action.style) != null ? _b : "info"
            });
            break;
          }
          case "send-context": {
            contextMessages.push({
              message: action.message,
              role: (_c = action.role) != null ? _c : "system"
            });
            break;
          }
          case "play-audio": {
            break;
          }
        }
      }
      const ruleMap = new Map(rules.map((r) => [r.id, r]));
      for (const ruleId of firedRuleIds) {
        const ruleDef = ruleMap.get(ruleId);
        this.recordRuleFired(ruleId, this.state.turnCount, ruleDef == null ? void 0 : ruleDef.cooldownTurns);
      }
      return { notifications, contextMessages };
    }
    resolveVariable(idOrName) {
      const variable = this.variables.get(idOrName);
      if (variable) return { variable, resolvedId: idOrName };
      const mappedId = this.nameToId.get(idOrName);
      if (mappedId) {
        const mapped = this.variables.get(mappedId);
        if (mapped) return { variable: mapped, resolvedId: mappedId };
      }
      return null;
    }
    /** Resolve an effect operand reference to its current value — by variable id,
     *  display name, or nested dot-path. Returns undefined if unresolved. */
    resolveOperand(ref) {
      if (!ref.includes(".")) {
        const resolved = this.resolveVariable(ref);
        if (resolved) return this.state.variables[resolved.resolvedId];
      }
      return getByPath(this.state.variables, ref);
    }
    resolvePathEffect(effect) {
      const dotIndex = effect.variableId.indexOf(".");
      if (dotIndex === -1) return { rootId: effect.variableId, applied: false, oldValue: 0, newValue: 0 };
      const rootId = effect.variableId.substring(0, dotIndex);
      const path = effect.variableId.substring(dotIndex + 1);
      const rootVar = this.variables.get(rootId);
      if (!rootVar || rootVar.type !== "json") return { rootId: effect.variableId, applied: false, oldValue: 0, newValue: 0 };
      const rootValue = this.state.variables[rootId];
      if (typeof rootValue !== "object" || rootValue === null) return { rootId, applied: false, oldValue: 0, newValue: 0 };
      const cloned = JSON.parse(JSON.stringify(rootValue));
      const oldRoot = JSON.parse(JSON.stringify(rootValue));
      const parts = path.split(".");
      if (parts.length === 0) return { rootId, applied: false, oldValue: 0, newValue: 0 };
      let current = cloned;
      for (let i = 0; i < parts.length - 1; i++) {
        const key = parts[i];
        if (current[key] === void 0 || current[key] === null) {
          current[key] = {};
        }
        current = current[key];
      }
      const leafKey = parts[parts.length - 1];
      switch (effect.operation) {
        case "set":
          current[leafKey] = effect.value;
          break;
        case "add":
          if (typeof current[leafKey] === "number" && typeof effect.value === "number") {
            current[leafKey] = current[leafKey] + effect.value;
          }
          break;
        case "subtract":
          if (typeof current[leafKey] === "number" && typeof effect.value === "number") {
            current[leafKey] = current[leafKey] - effect.value;
          }
          break;
        case "delete":
          if (Array.isArray(current)) {
            const idx = parseInt(leafKey);
            if (!isNaN(idx)) current.splice(idx, 1);
          } else {
            delete current[leafKey];
          }
          break;
        case "merge":
          if (typeof current[leafKey] === "object" && current[leafKey] !== null && typeof effect.value === "object" && effect.value !== null) {
            current[leafKey] = { ...current[leafKey], ...effect.value };
          }
          break;
        case "push":
          if (Array.isArray(current[leafKey])) {
            current[leafKey] = [...current[leafKey], effect.value];
          }
          break;
        default:
          current[leafKey] = effect.value;
      }
      this.state = {
        ...this.state,
        variables: { ...this.state.variables, [rootId]: cloned }
      };
      for (const listener of this.listeners) {
        listener(rootId, oldRoot, cloned);
      }
      return { rootId, applied: true, oldValue: oldRoot, newValue: cloned };
    }
    drainRejectedWrites() {
      const out = this.rejectedWrites;
      this.rejectedWrites = [];
      return out;
    }
    /**
     * A `json` variable currently holding a list/object must not be replaced
     * wholesale by a bare scalar: `[items: set delete 1, delete 0]` is a model
     * fumbling the delete syntax, not an instruction to store that text over the
     * whole array. A string that PARSES to a list/object is repaired rather than
     * refused — models quote their JSON often enough to be worth healing.
     *
     * Deliberately narrow: the guard only fires when the value being replaced is
     * itself a list/object, so a card that legitimately keeps a scalar in a json
     * variable keeps working, and every non-`set` operation (push/merge/delete)
     * already has its own shape check above.
     */
    coerceJsonWrite(variable, oldValue, newValue) {
      if (variable.type !== "json") return { ok: true, value: newValue };
      if (typeof newValue === "object" && newValue !== null) return { ok: true, value: newValue };
      if (typeof oldValue !== "object" || oldValue === null) return { ok: true, value: newValue };
      if (typeof newValue === "string") {
        try {
          const parsed = JSON.parse(newValue);
          if (typeof parsed === "object" && parsed !== null) return { ok: true, value: parsed };
        } catch {
        }
      }
      this.rejectedWrites.push({ variableId: variable.id, value: newValue });
      return { ok: false };
    }
    validateValue(variable, value) {
      if (variable.type === "json") return value;
      if (variable.type === "number" && typeof value === "number") {
        let v = value;
        if (variable.min !== void 0) v = Math.max(variable.min, v);
        if (variable.max !== void 0) v = Math.min(variable.max, v);
        return v;
      }
      return value;
    }
    normalizeState(state) {
      var _a, _b;
      const providedVariables = (_a = state == null ? void 0 : state.variables) != null ? _a : {};
      const knownVariables = Object.fromEntries(
        Object.entries(providedVariables).filter(
          ([variableId]) => (
            // Keep declared variables, plus the `__lore_{slotId}` flags that the
            // frontend LoreButton/LoreSwitch/LoreGroup persist. They are NOT declared
            // in world.variables (so they never render into <game-state>, which only
            // iterates declared vars), but they ARE the source of truth for which
            // frontend lore slot is on — stripping them made the toggle reset itself
            // and the bound entry vanish from the prompt after a reply.
            this.variables.has(variableId) || variableId.startsWith("__lore_")
          )
        )
      );
      const normalized = {
        worldId: this.worldId,
        variables: {
          ...this.defaultVariables,
          ...knownVariables
        },
        turnCount: typeof (state == null ? void 0 : state.turnCount) === "number" && Number.isFinite(state.turnCount) ? state.turnCount : 0,
        metadata: { ...(_b = state == null ? void 0 : state.metadata) != null ? _b : {} },
        ruleState: this.cloneRuleState(state == null ? void 0 : state.ruleState)
      };
      if ("activeCharacterId" in (state != null ? state : {})) {
        normalized.activeCharacterId = state == null ? void 0 : state.activeCharacterId;
      }
      if ("activeGreetingId" in (state != null ? state : {})) {
        normalized.activeGreetingId = state == null ? void 0 : state.activeGreetingId;
      }
      return this.poisonSurvival ? settlePoisonState(normalized) : normalized;
    }
    cloneRuleState(ruleState) {
      var _a, _b, _c, _d, _e, _f, _g;
      const safeRuleState = ruleState != null ? ruleState : createEmptyRuleState();
      return {
        disabledRules: [...(_a = safeRuleState.disabledRules) != null ? _a : []],
        activeDirectives: ((_b = safeRuleState.activeDirectives) != null ? _b : []).map(
          (directive) => ({ ...directive })
        ),
        cooldowns: { ...(_c = safeRuleState.cooldowns) != null ? _c : {} },
        fireCounts: { ...(_d = safeRuleState.fireCounts) != null ? _d : {} },
        prevVars: { ...(_e = safeRuleState.prevVars) != null ? _e : {} },
        toggledEntries: { ...(_f = safeRuleState.toggledEntries) != null ? _f : {} },
        toggledVariables: { ...(_g = safeRuleState.toggledVariables) != null ? _g : {} }
      };
    }
  };

  // src/prompts/macros.ts
  var commentHandler = {
    match(inner) {
      return inner.startsWith("//") ? [inner] : null;
    },
    resolve() {
      return "";
    }
  };
  var trimHandler = {
    match(inner) {
      return inner === "trim" ? [] : null;
    },
    resolve() {
      return "\0TRIM\0";
    }
  };
  var charHandler = {
    match(inner) {
      return inner === "char" ? [] : null;
    },
    resolve(_g, ctx) {
      return ctx.charName;
    }
  };
  var userHandler = {
    match(inner) {
      return inner === "user" ? [] : null;
    },
    resolve(_g, ctx) {
      var _a, _b;
      return (_b = (_a = ctx.state.metadata) == null ? void 0 : _a.personaName) != null ? _b : ctx.userName;
    }
  };
  var turnCountHandler = {
    match(inner) {
      return inner === "turnCount" ? [] : null;
    },
    resolve(_g, ctx) {
      var _a;
      return String((_a = ctx.state.turnCount) != null ? _a : 0);
    }
  };
  var randomHandler = {
    match(inner) {
      if (!inner.startsWith("random::")) return null;
      const items = inner.slice(8).split("::");
      return items.length >= 1 ? items : null;
    },
    resolve(items) {
      var _a;
      return (_a = items[Math.floor(Math.random() * items.length)]) != null ? _a : "";
    }
  };
  var pickHandler = {
    match(inner) {
      if (!inner.startsWith("pick::")) return null;
      const items = inner.slice(6).split("::");
      return items.length >= 1 ? items : null;
    },
    resolve(items, ctx) {
      var _a, _b;
      const turnCount = (_a = ctx.state.turnCount) != null ? _a : 0;
      const hash = ctx.macroIndex * 2654435761 + turnCount * 40503 >>> 0;
      const index = hash % items.length;
      return (_b = items[index]) != null ? _b : "";
    }
  };
  var rollHandler = {
    match(inner) {
      var _a;
      if (!inner.startsWith("roll::")) return null;
      const expr = inner.slice(6).trim();
      const m = expr.match(/^(\d+)d(\d+)([+-]\d+)?$/i);
      if (!m) return null;
      return [m[1], m[2], (_a = m[3]) != null ? _a : ""];
    },
    resolve([countStr, sidesStr, modStr]) {
      const count = parseInt(countStr, 10);
      const sides = parseInt(sidesStr, 10);
      const mod = modStr ? parseInt(modStr, 10) : 0;
      let total = mod;
      for (let i = 0; i < count; i++) {
        total += Math.floor(Math.random() * sides) + 1;
      }
      return String(total);
    }
  };
  var timeHandler = {
    match(inner) {
      return inner === "time" ? [] : null;
    },
    resolve() {
      const now = /* @__PURE__ */ new Date();
      return `${String(now.getHours()).padStart(2, "0")}:${String(now.getMinutes()).padStart(2, "0")}`;
    }
  };
  var dateHandler = {
    match(inner) {
      return inner === "date" ? [] : null;
    },
    resolve() {
      return (/* @__PURE__ */ new Date()).toLocaleDateString();
    }
  };
  var weekdayHandler = {
    match(inner) {
      return inner === "weekday" ? [] : null;
    },
    resolve() {
      return (/* @__PURE__ */ new Date()).toLocaleDateString("en-US", { weekday: "long" });
    }
  };
  var isodateHandler = {
    match(inner) {
      return inner === "isodate" ? [] : null;
    },
    resolve() {
      return (/* @__PURE__ */ new Date()).toISOString().slice(0, 10);
    }
  };
  var isotimeHandler = {
    match(inner) {
      return inner === "isotime" ? [] : null;
    },
    resolve() {
      return (/* @__PURE__ */ new Date()).toISOString().slice(11, 19);
    }
  };
  var idleHandler = {
    match(inner) {
      return inner === "idle" ? [] : null;
    },
    resolve(_g, ctx) {
      var _a;
      const lastAt = (_a = ctx.state.metadata) == null ? void 0 : _a.lastUserMessageAt;
      if (!lastAt) return "unknown";
      const diffMs = Date.now() - new Date(lastAt).getTime();
      if (diffMs < 0 || isNaN(diffMs)) return "just now";
      const seconds = Math.floor(diffMs / 1e3);
      if (seconds < 60) return "just now";
      const minutes = Math.floor(seconds / 60);
      if (minutes < 60) return minutes === 1 ? "1 minute" : `${minutes} minutes`;
      const hours = Math.floor(minutes / 60);
      if (hours < 24) return hours === 1 ? "1 hour" : `${hours} hours`;
      const days = Math.floor(hours / 24);
      return days === 1 ? "1 day" : `${days} days`;
    }
  };
  var lastMessageHandler = {
    match(inner) {
      return inner === "lastMessage" ? [] : null;
    },
    resolve(_g, ctx) {
      var _a, _b;
      return (_b = (_a = ctx.state.metadata) == null ? void 0 : _a.lastMessage) != null ? _b : "";
    }
  };
  var lastUserMessageHandler = {
    match(inner) {
      return inner === "lastUserMessage" ? [] : null;
    },
    resolve(_g, ctx) {
      var _a, _b;
      return (_b = (_a = ctx.state.metadata) == null ? void 0 : _a.lastUserMessage) != null ? _b : "";
    }
  };
  var lastCharMessageHandler = {
    match(inner) {
      return inner === "lastCharMessage" ? [] : null;
    },
    resolve(_g, ctx) {
      var _a, _b;
      return (_b = (_a = ctx.state.metadata) == null ? void 0 : _a.lastCharMessage) != null ? _b : "";
    }
  };
  var modelHandler = {
    match(inner) {
      return inner === "model" ? [] : null;
    },
    resolve(_g, ctx) {
      var _a, _b;
      return (_b = (_a = ctx.state.metadata) == null ? void 0 : _a.model) != null ? _b : "";
    }
  };
  function personaIsDisabled(ctx) {
    var _a;
    return ((_a = ctx.state.metadata) == null ? void 0 : _a.personaActive) === false;
  }
  var personaNameHandler = {
    match(inner) {
      return inner === "persona_name" ? [] : null;
    },
    resolve(_g, ctx) {
      var _a, _b;
      if (personaIsDisabled(ctx)) return "";
      return (_b = (_a = ctx.state.metadata) == null ? void 0 : _a.personaName) != null ? _b : ctx.userName;
    }
  };
  var personaAppearanceHandler = {
    match(inner) {
      return inner === "persona_appearance" ? [] : null;
    },
    resolve(_g, ctx) {
      var _a, _b;
      if (personaIsDisabled(ctx)) return "";
      return (_b = (_a = ctx.state.metadata) == null ? void 0 : _a.personaAppearance) != null ? _b : "";
    }
  };
  var personaPersonalityHandler = {
    match(inner) {
      return inner === "persona_personality" ? [] : null;
    },
    resolve(_g, ctx) {
      var _a, _b;
      if (personaIsDisabled(ctx)) return "";
      return (_b = (_a = ctx.state.metadata) == null ? void 0 : _a.personaPersonality) != null ? _b : "";
    }
  };
  var personaBackstoryHandler = {
    match(inner) {
      return inner === "persona_backstory" ? [] : null;
    },
    resolve(_g, ctx) {
      var _a, _b;
      if (personaIsDisabled(ctx)) return "";
      return (_b = (_a = ctx.state.metadata) == null ? void 0 : _a.personaBackstory) != null ? _b : "";
    }
  };
  var personaHandler = {
    match(inner) {
      return inner === "persona" ? [] : null;
    },
    resolve(_g, ctx) {
      var _a, _b, _c, _d;
      if (personaIsDisabled(ctx)) return "";
      const parts = [];
      const name = (_a = ctx.state.metadata) == null ? void 0 : _a.personaName;
      const appearance = (_b = ctx.state.metadata) == null ? void 0 : _b.personaAppearance;
      const personality = (_c = ctx.state.metadata) == null ? void 0 : _c.personaPersonality;
      const backstory = (_d = ctx.state.metadata) == null ? void 0 : _d.personaBackstory;
      if (name) parts.push(`Name: ${name}`);
      if (appearance) parts.push(`Appearance: ${appearance}`);
      if (personality) parts.push(`Personality: ${personality}`);
      if (backstory) parts.push(`Backstory: ${backstory}`);
      return parts.join("\n");
    }
  };
  var HANDLERS = [
    commentHandler,
    trimHandler,
    charHandler,
    userHandler,
    turnCountHandler,
    randomHandler,
    pickHandler,
    rollHandler,
    timeHandler,
    dateHandler,
    weekdayHandler,
    isodateHandler,
    isotimeHandler,
    idleHandler,
    lastMessageHandler,
    lastUserMessageHandler,
    lastCharMessageHandler,
    personaHandler,
    personaNameHandler,
    personaAppearanceHandler,
    personaPersonalityHandler,
    personaBackstoryHandler,
    modelHandler
  ];
  var MACRO_RE = /\{\{((?:[^{}]|\{(?!\{)|\}(?!\}))*)\}\}/g;
  function expandMacros(template, world, state) {
    var _a, _b;
    const charEntry = world.entries.find((e) => e.role === "character" && e.enabled);
    const charName = (_a = charEntry == null ? void 0 : charEntry.name) != null ? _a : "Assistant";
    const userName = ((_b = world.settings) == null ? void 0 : _b.playerName) || "Player";
    let macroIndex = 0;
    const result = template.replace(MACRO_RE, (_match, inner) => {
      const currentIndex = macroIndex++;
      const ctx = {
        charName,
        userName,
        world,
        state,
        macroIndex: currentIndex
      };
      for (const handler of HANDLERS) {
        const groups = handler.match(inner);
        if (groups !== null) {
          return handler.resolve(groups, ctx);
        }
      }
      if (/^\w+$/.test(inner)) {
        const value = state.variables[inner];
        if (value !== void 0) return String(value);
      }
      return `{{${inner}}}`;
    });
    return applyTrim(result);
  }
  function applyTrim(text) {
    return text.replace(/\s*\x00TRIM\x00\s*/g, "");
  }

  // ../../node_modules/.pnpm/js-tiktoken@1.0.21/node_modules/js-tiktoken/dist/chunk-VL2OQCWN.js
  var import_base64_js = __toESM(require_base64_js(), 1);
  var __defProp2 = Object.defineProperty;
  var __defNormalProp2 = (obj, key, value) => key in obj ? __defProp2(obj, key, { enumerable: true, configurable: true, writable: true, value }) : obj[key] = value;
  var __publicField2 = (obj, key, value) => {
    __defNormalProp2(obj, typeof key !== "symbol" ? key + "" : key, value);
    return value;
  };
  function bytePairMerge(piece, ranks) {
    let parts = Array.from(
      { length: piece.length },
      (_, i) => ({ start: i, end: i + 1 })
    );
    while (parts.length > 1) {
      let minRank = null;
      for (let i = 0; i < parts.length - 1; i++) {
        const slice = piece.slice(parts[i].start, parts[i + 1].end);
        const rank = ranks.get(slice.join(","));
        if (rank == null)
          continue;
        if (minRank == null || rank < minRank[0]) {
          minRank = [rank, i];
        }
      }
      if (minRank != null) {
        const i = minRank[1];
        parts[i] = { start: parts[i].start, end: parts[i + 1].end };
        parts.splice(i + 1, 1);
      } else {
        break;
      }
    }
    return parts;
  }
  function bytePairEncode(piece, ranks) {
    if (piece.length === 1)
      return [ranks.get(piece.join(","))];
    return bytePairMerge(piece, ranks).map((p) => ranks.get(piece.slice(p.start, p.end).join(","))).filter((x) => x != null);
  }
  function escapeRegex(str) {
    return str.replace(/[\\^$*+?.()|[\]{}]/g, "\\$&");
  }
  var _Tiktoken = class {
    constructor(ranks, extendedSpecialTokens) {
      /** @internal */
      __publicField(this, "specialTokens");
      /** @internal */
      __publicField(this, "inverseSpecialTokens");
      /** @internal */
      __publicField(this, "patStr");
      /** @internal */
      __publicField(this, "textEncoder", new TextEncoder());
      /** @internal */
      __publicField(this, "textDecoder", new TextDecoder("utf-8"));
      /** @internal */
      __publicField(this, "rankMap", /* @__PURE__ */ new Map());
      /** @internal */
      __publicField(this, "textMap", /* @__PURE__ */ new Map());
      this.patStr = ranks.pat_str;
      const uncompressed = ranks.bpe_ranks.split("\n").filter(Boolean).reduce((memo, x) => {
        const [_, offsetStr, ...tokens] = x.split(" ");
        const offset = Number.parseInt(offsetStr, 10);
        tokens.forEach((token, i) => memo[token] = offset + i);
        return memo;
      }, {});
      for (const [token, rank] of Object.entries(uncompressed)) {
        const bytes = import_base64_js.default.toByteArray(token);
        this.rankMap.set(bytes.join(","), rank);
        this.textMap.set(rank, bytes);
      }
      this.specialTokens = { ...ranks.special_tokens, ...extendedSpecialTokens };
      this.inverseSpecialTokens = Object.entries(this.specialTokens).reduce((memo, [text, rank]) => {
        memo[rank] = this.textEncoder.encode(text);
        return memo;
      }, {});
    }
    encode(text, allowedSpecial = [], disallowedSpecial = "all") {
      var _a;
      const regexes = new RegExp(this.patStr, "ug");
      const specialRegex = _Tiktoken.specialTokenRegex(
        Object.keys(this.specialTokens)
      );
      const ret = [];
      const allowedSpecialSet = new Set(
        allowedSpecial === "all" ? Object.keys(this.specialTokens) : allowedSpecial
      );
      const disallowedSpecialSet = new Set(
        disallowedSpecial === "all" ? Object.keys(this.specialTokens).filter(
          (x) => !allowedSpecialSet.has(x)
        ) : disallowedSpecial
      );
      if (disallowedSpecialSet.size > 0) {
        const disallowedSpecialRegex = _Tiktoken.specialTokenRegex([
          ...disallowedSpecialSet
        ]);
        const specialMatch = text.match(disallowedSpecialRegex);
        if (specialMatch != null) {
          throw new Error(
            `The text contains a special token that is not allowed: ${specialMatch[0]}`
          );
        }
      }
      let start = 0;
      while (true) {
        let nextSpecial = null;
        let startFind = start;
        while (true) {
          specialRegex.lastIndex = startFind;
          nextSpecial = specialRegex.exec(text);
          if (nextSpecial == null || allowedSpecialSet.has(nextSpecial[0]))
            break;
          startFind = nextSpecial.index + 1;
        }
        const end = (_a = nextSpecial == null ? void 0 : nextSpecial.index) != null ? _a : text.length;
        for (const match of text.substring(start, end).matchAll(regexes)) {
          const piece = this.textEncoder.encode(match[0]);
          const token2 = this.rankMap.get(piece.join(","));
          if (token2 != null) {
            ret.push(token2);
            continue;
          }
          ret.push(...bytePairEncode(piece, this.rankMap));
        }
        if (nextSpecial == null)
          break;
        let token = this.specialTokens[nextSpecial[0]];
        ret.push(token);
        start = nextSpecial.index + nextSpecial[0].length;
      }
      return ret;
    }
    decode(tokens) {
      var _a;
      const res = [];
      let length = 0;
      for (let i2 = 0; i2 < tokens.length; ++i2) {
        const token = tokens[i2];
        const bytes = (_a = this.textMap.get(token)) != null ? _a : this.inverseSpecialTokens[token];
        if (bytes != null) {
          res.push(bytes);
          length += bytes.length;
        }
      }
      const mergedArray = new Uint8Array(length);
      let i = 0;
      for (const bytes of res) {
        mergedArray.set(bytes, i);
        i += bytes.length;
      }
      return this.textDecoder.decode(mergedArray);
    }
  };
  var Tiktoken = _Tiktoken;
  __publicField2(Tiktoken, "specialTokenRegex", (tokens) => {
    return new RegExp(tokens.map((i) => escapeRegex(i)).join("|"), "g");
  });

  // src/prompts/token-utils.ts
  var encoder = null;
  var preTokenPattern = null;
  var utf8 = null;
  var MAX_BPE_PIECE_BYTES = 256;
  var tokenCache = /* @__PURE__ */ new Map();
  var cachedChars = 0;
  var MAX_CACHED_CHARS = 2e6;
  var MAX_CACHE_ENTRIES = 1024;
  var initFailed = false;
  var loadPromise = null;
  function preloadTokenizer() {
    if (!loadPromise) {
      loadPromise = Promise.resolve().then(() => (init_no_ranks(), no_ranks_exports)).then((m) => {
        encoder = new Tiktoken(m.default);
        preTokenPattern = m.default.pat_str;
      }).catch(() => {
        initFailed = true;
      });
    }
    return loadPromise;
  }
  function tryGetEncoder() {
    if (encoder) return encoder;
    if (!initFailed) void preloadTokenizer();
    return null;
  }
  function boundedTokenCount(text, enc) {
    if (!preTokenPattern || text.length <= MAX_BPE_PIECE_BYTES / 4) {
      return enc.encode(text).length;
    }
    let cursor = 0, tokens = 0;
    for (const match of text.matchAll(new RegExp(preTokenPattern, "ug"))) {
      const piece = match[0];
      if (piece.length <= MAX_BPE_PIECE_BYTES / 4) continue;
      const bytes = (utf8 != null ? utf8 : utf8 = new TextEncoder()).encode(piece).length;
      if (bytes <= MAX_BPE_PIECE_BYTES) continue;
      if (match.index > cursor) tokens += enc.encode(text.slice(cursor, match.index)).length;
      tokens += bytes;
      cursor = match.index + piece.length;
    }
    return tokens + enc.encode(text.slice(cursor)).length;
  }
  function isCjkAwareModel(modelId) {
    if (!modelId) return false;
    const id = modelId.toLowerCase();
    return id.includes("gemini") || id.includes("claude") || id.includes("deepseek");
  }
  function estimateCjkAware(text) {
    let cjk = 0;
    for (let i = 0; i < text.length; i++) {
      const c = text.charCodeAt(i);
      if (c >= 12288 && c <= 40959 || c >= 44032 && c <= 55215 || c >= 63744 && c <= 64255) {
        cjk++;
      }
    }
    const ascii = text.length - cjk;
    return Math.ceil(cjk + ascii / 4);
  }
  function estimateTokens(text, modelId) {
    if (!text) return 0;
    if (isCjkAwareModel(modelId)) {
      return estimateCjkAware(text);
    }
    const enc = tryGetEncoder();
    if (enc) {
      try {
        const cached = tokenCache.get(text);
        if (cached !== void 0) {
          tokenCache.delete(text);
          tokenCache.set(text, cached);
          return cached;
        }
        const tokens = boundedTokenCount(text, enc);
        if (text.length <= 131072) {
          tokenCache.set(text, tokens);
          cachedChars += text.length;
          while (cachedChars > MAX_CACHED_CHARS || tokenCache.size > MAX_CACHE_ENTRIES) {
            const oldest = tokenCache.keys().next().value;
            if (oldest === void 0) break;
            cachedChars -= oldest.length;
            tokenCache.delete(oldest);
          }
        }
        return tokens;
      } catch {
      }
    }
    return Math.ceil(text.length / 3);
  }

  // src/state/condition-evaluator.ts
  function valuesEqual(a, b) {
    if (a === b) return true;
    if (a === null || b === null || a === void 0 || b === void 0) return false;
    if (typeof a === "object" && typeof b === "object") {
      return JSON.stringify(a) === JSON.stringify(b);
    }
    return false;
  }
  function evaluateCondition(state, condition) {
    const current = getByPath(state.variables, condition.variableId);
    if (current === void 0) return false;
    const target = condition.valueRef !== void 0 ? getByPath(state.variables, condition.valueRef) : condition.value;
    if (condition.valueRef !== void 0 && target === void 0) return false;
    switch (condition.operator) {
      case "eq":
        return valuesEqual(current, target);
      case "neq":
        return !valuesEqual(current, target);
      case "gt":
        return typeof current === "number" && typeof target === "number" && current > target;
      case "gte":
        return typeof current === "number" && typeof target === "number" && current >= target;
      case "lt":
        return typeof current === "number" && typeof target === "number" && current < target;
      case "lte":
        return typeof current === "number" && typeof target === "number" && current <= target;
      case "contains":
        if (Array.isArray(current)) {
          return current.some((item) => valuesEqual(item, target) || item === target);
        }
        return typeof current === "string" && typeof target === "string" && current.includes(target);
      default:
        return false;
    }
  }
  function checkConditions(state, conditions, logic) {
    if (conditions.length === 0) return true;
    if (logic === "all") {
      return conditions.every((c) => evaluateCondition(state, c));
    }
    return conditions.some((c) => evaluateCondition(state, c));
  }

  // src/lorebook/worldbook.ts
  function computeActiveWorldbookIds(worldbooks, state) {
    var _a;
    const active = /* @__PURE__ */ new Set();
    if (!Array.isArray(worldbooks)) return active;
    for (const wb of worldbooks) {
      if (wb.enabled === false) continue;
      if (wb.activation.mode === "conditions") {
        if (checkConditions(state, wb.activation.conditions, wb.activation.conditionLogic)) {
          active.add(wb.id);
        }
      } else if (wb.activation.mode === "greeting") {
        const current = String((_a = state.activeGreetingId) != null ? _a : "");
        if (current && wb.activation.greetingIds.includes(current)) {
          active.add(wb.id);
        }
      } else {
        active.add(wb.id);
      }
    }
    return active;
  }
  function filterEntriesByActiveWorldbooks(entries, worldbooks, state) {
    if (!Array.isArray(worldbooks) || worldbooks.length === 0) return entries;
    const known = new Set(worldbooks.map((w) => w.id));
    const active = computeActiveWorldbookIds(worldbooks, state);
    return entries.filter((e) => {
      if (!e.worldbookId) return true;
      if (!known.has(e.worldbookId)) return true;
      return active.has(e.worldbookId);
    });
  }

  // src/lorebook/entry-triggers.ts
  function isVariableBoundEntry(entry) {
    var _a, _b;
    if (entry.variableBound === true) return true;
    return ((_b = (_a = entry.conditions) == null ? void 0 : _a.length) != null ? _b : 0) > 0;
  }
  function getUiBoundEntryIds(bindings) {
    const ids = /* @__PURE__ */ new Set();
    if (!Array.isArray(bindings)) return ids;
    for (const binding of bindings) {
      if (binding.entryId) ids.add(binding.entryId);
    }
    return ids;
  }
  function isUiBoundEntry(entryId, bindings) {
    return getUiBoundEntryIds(bindings).has(entryId);
  }
  function getEntryBoundSlotId(entryId, bindings) {
    var _a, _b;
    if (!Array.isArray(bindings)) return null;
    return (_b = (_a = bindings.find((b) => b.entryId === entryId)) == null ? void 0 : _a.slotId) != null ? _b : null;
  }
  function getActiveLoreSlots(state) {
    var _a;
    const raw = (_a = state.metadata) == null ? void 0 : _a.activeLoreSlots;
    if (!Array.isArray(raw)) return [];
    return raw.filter((id) => typeof id === "string" && id.length > 0);
  }
  function isLoreSlotActive(slotId, state) {
    return getActiveLoreSlots(state).includes(slotId);
  }

  // src/lorebook/lore-slot.ts
  function filterEntriesByActiveLoreSlots(entries, bindings, state) {
    var _a;
    if (!Array.isArray(bindings) || bindings.length === 0) return entries;
    const byEntry = /* @__PURE__ */ new Map();
    for (const b of bindings) {
      if (b.entryId && !byEntry.has(b.entryId)) byEntry.set(b.entryId, b);
    }
    if (byEntry.size === 0) return entries;
    const activeSlots = new Set(getActiveLoreSlots(state));
    const vars = (_a = state.variables) != null ? _a : {};
    const isSlotActive = (slotId) => activeSlots.has(slotId) || vars[`__lore_${slotId}`] === true || vars[`__lore_${slotId}`] === "true";
    return entries.filter((e) => {
      const binding = byEntry.get(e.id);
      if (!binding) return true;
      if (!isSlotActive(binding.slotId)) return false;
      if (binding.conditions.length > 0 && !checkConditions(state, binding.conditions, binding.conditionLogic)) {
        return false;
      }
      return true;
    });
  }

  // src/state/variable-activation.ts
  function isVariableActive(variable, state) {
    var _a, _b, _c;
    const toggle = (_b = (_a = state.ruleState) == null ? void 0 : _a.toggledVariables) == null ? void 0 : _b[variable.id];
    const gateOn = toggle != null ? toggle : variable.enabled !== false;
    if (!gateOn) return false;
    const activation = variable.activation;
    if (!activation) return true;
    switch (activation.mode) {
      case "always":
      case "manual":
        return true;
      case "conditions":
        return checkConditions(state, activation.conditions, activation.conditionLogic);
      case "greeting": {
        const current = String((_c = state.activeGreetingId) != null ? _c : "");
        return current !== "" && activation.greetingIds.includes(current);
      }
      default:
        return true;
    }
  }
  function isAiReadable(variable, state) {
    var _a;
    if (variable.internal) return false;
    if (!isVariableActive(variable, state)) return false;
    return ((_a = variable.aiAccess) != null ? _a : "write") !== "none";
  }
  function isAiWritable(variable, state) {
    var _a;
    if (variable.internal) return false;
    if (!isVariableActive(variable, state)) return false;
    return ((_a = variable.aiAccess) != null ? _a : "write") === "write";
  }
  function filterAiEffects(world, state, effects) {
    const kept = [];
    const dropped = [];
    for (const effect of effects) {
      const rootId = effect.variableId.split(".")[0];
      const variable = world.variables.find((v) => v.id === rootId);
      if (!variable || isAiWritable(variable, state)) {
        kept.push(effect);
      } else {
        dropped.push(effect);
      }
    }
    return { kept, dropped };
  }

  // src/audio/ai-audio.ts
  function getAiAudioTracks(tracks) {
    return tracks.filter((track) => track.allowAiControl !== false);
  }

  // src/prompts/speaker-tag.ts
  var NARRATOR_SPEAKER = "narrator";
  var LEADING_SPEAKER_TAG = /^\s*\[\s*speaker\s*:\s*([^\]\n]*?)\s*\]/i;
  function portraitCharacters(entries) {
    return (entries != null ? entries : []).filter(
      (e) => e.role === "character" && e.enabled !== false && typeof e.portrait === "string" && e.portrait.length > 0
    );
  }
  function displayCharacterName(raw) {
    const trimmed = raw.trim();
    const m = /^[^:：]{1,12}[:：]\s*(.+)$/s.exec(trimmed);
    return m && m[1].trim() ? m[1].trim() : trimmed;
  }
  function speakerTagEnabled(world) {
    var _a;
    if (portraitCharacters(world.entries).length < 2) return false;
    if (((_a = world.variables) != null ? _a : []).some((v) => v.id.toLowerCase() === "speaker")) return false;
    return true;
  }
  function parseLeadingSpeakerTag(text) {
    const m = LEADING_SPEAKER_TAG.exec(text);
    if (!m) return { speaker: null, text };
    const raw = m[1].trim();
    const speaker = raw.toLowerCase() === NARRATOR_SPEAKER ? NARRATOR_SPEAKER : raw;
    return { speaker: speaker || null, text: text.slice(m[0].length).replace(/^[ \t]*\n/, "") };
  }
  function buildSpeakerFormatBlock(world) {
    if (!speakerTagEnabled(world)) return "";
    const names = portraitCharacters(world.entries).map((e) => displayCharacterName(e.name));
    return [
      "<speaker-format>",
      `Characters with portraits: ${names.join(", ")}.`,
      "Begin EVERY reply with a speaker tag as the very first thing, before any prose:",
      "  [speaker: Name]",
      "Name is exactly one of the names above \u2014 the character whose voice the reply is mainly in.",
      `For pure narration, or when no listed character is speaking, write [speaker: ${NARRATOR_SPEAKER}].`,
      "The tag is stripped before the player sees the reply; never mention it in the text.",
      "</speaker-format>"
    ].join("\n");
  }

  // src/prompts/prompt-builder.ts
  function isAiExposedStatic(v) {
    var _a;
    return !v.internal && ((_a = v.aiAccess) != null ? _a : "write") !== "none";
  }
  function isAiWritableStatic(v) {
    var _a;
    return !v.internal && ((_a = v.aiAccess) != null ? _a : "write") === "write";
  }
  function escapeRegex2(s) {
    return s.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
  }
  function entrySort(a, b) {
    var _a, _b;
    const aPos = (_a = a.position) != null ? _a : Infinity;
    const bPos = (_b = b.position) != null ? _b : Infinity;
    return aPos - bPos;
  }
  var PROMPT_STRING_MAX_CHARS = 2e4;
  var PROMPT_VAR_MAX_CHARS = 5e4;
  var DATA_URI_RE = /data:[\w.+-]+\/[\w.+-]+;base64,[A-Za-z0-9+/=\s]*/g;
  function sanitizePromptString(s) {
    const stripped = s.replace(DATA_URI_RE, "[image omitted]");
    if (stripped.length > PROMPT_STRING_MAX_CHARS) {
      return `[omitted ${stripped.length} chars]`;
    }
    return stripped;
  }
  function sanitizePromptValue(value) {
    if (typeof value === "string") return sanitizePromptString(value);
    if (Array.isArray(value)) return value.map(sanitizePromptValue);
    if (value && typeof value === "object") {
      const out = {};
      for (const [k, v] of Object.entries(value)) out[k] = sanitizePromptValue(v);
      return out;
    }
    return value;
  }
  function renderVariableValueForPrompt(value) {
    let rendered;
    if (value !== null && typeof value === "object") {
      rendered = JSON.stringify(sanitizePromptValue(value));
    } else if (typeof value === "string") {
      rendered = sanitizePromptString(value);
    } else {
      rendered = String(value);
    }
    if (rendered.length > PROMPT_VAR_MAX_CHARS) {
      return `[large value omitted: ${rendered.length} chars]`;
    }
    return rendered;
  }
  var CHANGE_VALUE_MAX_CHARS = 160;
  var MAX_CHANGE_LINES = 12;
  function renderChangeValueForPrompt(value) {
    const rendered = renderVariableValueForPrompt(value);
    return rendered.length > CHANGE_VALUE_MAX_CHARS ? `${rendered.slice(0, CHANGE_VALUE_MAX_CHARS)}\u2026` : rendered;
  }
  var PromptBuilder = class {
    /**
     * Build the system prompt from world entries.
     * @param world The world definition with entries[]
     * @param state Current game state
     * @param matchedEntries Triggered entries from the matcher (keyword/vector matched)
     * @param userPrompts User-defined global prompts
     * @param directives Active directives from rules (injected at configured positions)
     * @param toggledEntries Entry enabled/disabled overrides from rules
     */
    buildSystemPrompt(world, state, matchedEntries, userPrompts, directives, toggledEntries) {
      const entries = this.collectEntries(world, matchedEntries, userPrompts, toggledEntries, state);
      return this.assemblePrompt(world, state, entries, directives);
    }
    /**
     * Find and interpolate the greeting entry.
     */
    buildGreeting(world, state) {
      const greetingEntry = world.entries.find(
        (e) => e.role === "greeting" && e.enabled
      );
      if (!greetingEntry) return "";
      return this.interpolate(greetingEntry.content, world, state);
    }
    /**
     * Find and interpolate ALL enabled greeting entries, sorted by order.
     * Returns an array of interpolated greeting strings.
     */
    buildGreetings(world, state) {
      return world.entries.filter((e) => e.role === "greeting" && e.enabled).sort(entrySort).map((e) => this.interpolate(e.content, world, state));
    }
    /** The ordered greeting ENTRIES (same filter + entrySort order as
     *  buildGreetings), so callers can read per-greeting metadata — e.g.
     *  initialVariables for "scenario presets" — aligned index-for-index with
     *  the strings buildGreetings returns. */
    buildGreetingEntries(world) {
      return world.entries.filter((e) => e.role === "greeting" && e.enabled).sort(entrySort);
    }
    /**
     * Get depth entries for in-chat injection.
     * Returns entries with their depth value and apiRole for the message router to place.
     */
    buildDepthEntries(world, state, matchedEntries, userPrompts, toggledEntries, preCollected) {
      const allEntries = preCollected != null ? preCollected : this.collectEntries(world, matchedEntries, userPrompts, toggledEntries, state);
      return allEntries.filter((e) => e.section === "chat-history" && e.depth !== void 0).sort(entrySort).map((e) => {
        var _a;
        return {
          content: this.interpolate(e.content, world, state),
          depth: e.depth,
          apiRole: (_a = e.apiRole) != null ? _a : "system"
        };
      });
    }
    /**
     * Get post-history entries (jailbreak / post-history instructions).
     * These go AFTER all chat history as the final messages before AI responds.
     * Returns content with apiRole so entries can be sent as assistant/user prefill.
     */
    buildPostHistoryEntries(world, state, matchedEntries, userPrompts, preCollected) {
      const allEntries = preCollected != null ? preCollected : this.collectEntries(world, matchedEntries, userPrompts, void 0, state);
      return allEntries.filter((e) => e.section === "post-history").sort(entrySort).map((e) => {
        var _a;
        return {
          content: this.interpolate(e.content, world, state),
          apiRole: (_a = e.apiRole) != null ? _a : "system"
        };
      });
    }
    /**
     * Build the STATIC format reference block — behavior rules, directive syntax, audio tracks.
     * These are per-world constants that never change between turns.
     * Designed to be placed in the system prefix (cacheable).
     */
    buildStaticFormatBlock(world) {
      var _a;
      const hasVariables = world.variables.length > 0;
      const audioTracks = getAiAudioTracks((_a = world.audioTracks) != null ? _a : []);
      const hasAudio = audioTracks.length > 0;
      const speakerBlock = buildSpeakerFormatBlock(world);
      if (!hasVariables && !hasAudio && !speakerBlock) return "";
      const parts = [];
      if (speakerBlock) parts.push(speakerBlock);
      const behaviorRulesSection = this.buildBehaviorRulesSection(world);
      if (behaviorRulesSection) {
        parts.push(`<behavior-rules>
${behaviorRulesSection}
</behavior-rules>`);
      }
      const hasWritableVariables = world.variables.some(isAiWritableStatic);
      if (hasWritableVariables) {
        const hasJsonVar = world.variables.some(
          (v) => v.type === "json" && isAiWritableStatic(v)
        );
        const lines = [
          "<directive-format>",
          "State changes ONLY happen through directives. Describing a change in prose alone does nothing.",
          "",
          "Format: [variableId: operation value]",
          "",
          "Numbers:   [hp: -10]   [gold: +50]   [score: *2]",
          'Strings:   [location: set "forest"]   [log: append ", found the key"]',
          "Booleans:  [hasKey: toggle]   [armed: set true]",
          "",
          // `append` was supported by the engine but absent here, so a card whose
          // string variable is a running list left the model no legal way to add
          // to it. It reached for `add`, which is a silent no-op on strings, and
          // the variable simply never moved. Spell both halves out.
          "To extend a string, use `append` \u2014 `add` only works on numbers and is",
          "ignored on a string. `append` concatenates exactly what you pass, so put",
          'any separator inside the value: [items: append ", rope"].'
        ];
        if (hasJsonVar) {
          lines.push(
            "",
            "JSON variables \u2014 use dot-paths to target nested fields:",
            "  [npcs.aria.affinity: +3]               number at a nested path",
            '  [npcs.aria.mood: set "happy"]          string at a nested path',
            '  [npcs.aria: merge {"trust": 80}]       patch several fields at once',
            '  [inventory: push {"name": "Sword"}]    append to a JSON array',
            '  [npcs: delete "aria"]                  remove a key from an object',
            "  [inventory: delete 0]                  remove element at index 0",
            "",
            "JSON values must use double quotes around keys and strings."
          );
        }
        lines.push(
          "",
          "Put all directives at the END of your reply, after the narrative.",
          "</directive-format>"
        );
        parts.push(lines.join("\n"));
      }
      if (hasAudio) {
        const trackList = audioTracks.map((t) => `  - ${t.id} (${t.type}): ${t.name}`).join("\n");
        parts.push(
          `<audio>
Available audio tracks:
${trackList}

To trigger audio, use: [audio: trackId action]
Examples: [audio: battle_bgm play], [audio: tavern_ambient stop]
</audio>`
        );
      }
      return parts.join("\n\n");
    }
    /**
     * Build the DYNAMIC game-state block — current variable values plus a short
     * per-turn reminder to emit update directives. Sits in the dynamic tail
     * (uncached), nearest the generation point, so the call-to-action lands where
     * the model's recall is strongest.
     *
     * The directive SYNTAX (and all `[...]` examples) stays in the cached prefix
     * (buildStaticFormatBlock); this block only re-states the *action*. With many
     * variables the model tends to skip updates because the "how/when" instructions
     * are thousands of tokens up in the cached prefix and the tail shows only a
     * passive wall of values — this anchors a fresh "now update what changed" nudge
     * right next to those values.
     *
     * Two deliberate safety choices:
     *  - The reminder contains NO literal `[id: op value]` examples. Models echo the
     *    <game-state> block verbatim (see ThinkingTagFilter ECHOED_TAGS); a literal
     *    bracket example could be mirrored back and then parsed as a real directive
     *    by ResponseParser — and a dot-path example like `[npcs.x: set ...]` would
     *    bypass the unknown-variable guard in GameStateManager and corrupt state.
     *    Prose-only here means nothing parseable can ever survive an echo.
     *  - The reminder is wrapped in its own <state-reminder> tag, also registered in
     *    ECHOED_TAGS, so if a model mirrors the tail it gets stripped from the reply
     *    instead of leaking instruction text into the narrative.
     */
    buildFormatBlock(world, state, lastTurnChanges) {
      if (world.variables.length === 0) return "";
      const varSummary = this.buildVariableSummary(world, state);
      if (!varSummary) return "";
      const hasJsonVar = world.variables.some(
        (v) => v.type === "json" && isAiReadable(v, state)
      );
      const jsonClause = hasJsonVar ? " For JSON variables, target the specific changed sub-field with a dot-path rather than rewriting the whole object." : "";
      const changesBlock = this.buildLastTurnChangesBlock(world, state, lastTurnChanges);
      const changesClause = changesBlock ? " The last-turn-changes block lists what ALREADY changed on the previous turn \u2014 treat those as done; do not re-apply them." : "";
      return `<game-state>
${varSummary}
</game-state>
` + changesBlock + "<state-reminder>\nThe block above is the current state. After your narrative, output a state-change directive for every value that changed this turn \u2014 and only those." + jsonClause + changesClause + "\n</state-reminder>";
    }
    /**
     * Render persisted last-turn changes as `id: old → new` lines. Collapses
     * multiple writes to the same variable into first-old → last-new, hides
     * engine-internal variables, and drops lines whose rendered old/new are
     * identical (sub-field churn inside an omitted blob). Returns "" when
     * nothing survives so callers pay zero tokens on quiet turns.
     */
    buildLastTurnChangesBlock(world, state, changes) {
      if (!changes || changes.length === 0) return "";
      const byId = /* @__PURE__ */ new Map();
      for (const ch of changes) {
        if (!ch || typeof ch.variableId !== "string") continue;
        const rootId = ch.variableId.split(".")[0];
        const variable = world.variables.find((v) => v.id === rootId);
        if (!variable || !isAiReadable(variable, state)) continue;
        const prev = byId.get(rootId);
        byId.set(rootId, {
          oldValue: prev ? prev.oldValue : ch.oldValue,
          newValue: ch.newValue
        });
      }
      const lines = [];
      for (const [id, { oldValue, newValue }] of byId) {
        const oldRendered = renderChangeValueForPrompt(oldValue);
        const newRendered = renderChangeValueForPrompt(newValue);
        if (oldRendered === newRendered) continue;
        lines.push(`${id}: ${oldRendered} \u2192 ${newRendered}`);
      }
      if (lines.length === 0) return "";
      const shown = lines.slice(0, MAX_CHANGE_LINES);
      if (lines.length > shown.length) {
        shown.push(`(+${lines.length - shown.length} more)`);
      }
      return `<last-turn-changes>
${shown.join("\n")}
</last-turn-changes>
`;
    }
    /**
     * Build system prompt as SEPARATE messages — one per position slot.
     * Matches SillyTavern's multi-message approach for better LLM attention.
     * Each position slot (top, before_char, character, after_char, persona, bottom)
     * becomes its own system message instead of being concatenated into one blob.
     */
    buildSystemMessages(world, state, matchedEntries, userPrompts, directives, toggledEntries, preCollected) {
      var _a;
      const entries = preCollected != null ? preCollected : this.collectEntries(world, matchedEntries, userPrompts, toggledEntries, state);
      const result = [];
      const activeDirectives = directives != null ? directives : [];
      const getDirectivesForPosition = (pos) => activeDirectives.filter((d) => d.position === pos).map((d) => this.interpolate(d.content, world, state));
      const autoDirectives = activeDirectives.filter((d) => d.position === "auto").map((d) => this.interpolate(d.content, world, state));
      const systemEntries = entries.filter((e) => {
        if (e.section === "chat-history" || e.section === "post-history") return false;
        if (e.role === "example" || e.role === "greeting") return false;
        return true;
      }).sort(entrySort);
      const systemParts = [];
      systemParts.push(...getDirectivesForPosition("top"));
      systemParts.push(...getDirectivesForPosition("before_char"));
      for (const entry of systemEntries) {
        const content = this.interpolate(entry.content, world, state);
        const effectiveApiRole = (_a = entry.apiRole) != null ? _a : "system";
        if (effectiveApiRole === "system") {
          systemParts.push(content);
        } else {
          if (systemParts.length > 0) {
            result.push({ role: "system", content: systemParts.join("\n\n") });
            systemParts.length = 0;
          }
          result.push({ role: effectiveApiRole, content });
        }
      }
      systemParts.push(...getDirectivesForPosition("after_char"));
      systemParts.push(...autoDirectives);
      systemParts.push(...getDirectivesForPosition("bottom"));
      if (systemParts.length > 0) {
        result.push({ role: "system", content: systemParts.join("\n\n") });
      }
      return result;
    }
    /**
     * Build system messages from KEYWORD-TRIGGERED entries only (alwaysSend=false).
     * Produces messages for entries whose section is neither chat-history nor post-history,
     * and whose role is neither example nor greeting — i.e. entries that belong in the
     * system block but were gated by keyword matching.
     *
     * These messages are dynamic (change when different keywords fire), so callers should
     * place them AFTER the stable alwaysSend prefix to preserve prompt caching on the prefix.
     */
    buildTriggeredSystemMessages(world, state, triggeredEntries, toggledEntries) {
      var _a;
      const isEnabled = (e) => {
        if (toggledEntries && e.id in toggledEntries) return toggledEntries[e.id];
        return e.enabled;
      };
      const systemTriggered = triggeredEntries.filter((e) => {
        if (!isEnabled(e)) return false;
        if (e.alwaysSend && !isVariableBoundEntry(e)) return false;
        if (e.section === "chat-history" || e.section === "post-history") return false;
        if (e.role === "example" || e.role === "greeting") return false;
        return true;
      }).sort(entrySort);
      if (systemTriggered.length === 0) return [];
      const result = [];
      const systemParts = [];
      for (const entry of systemTriggered) {
        const content = this.interpolate(entry.content, world, state);
        const effectiveApiRole = (_a = entry.apiRole) != null ? _a : "system";
        if (effectiveApiRole === "system") {
          systemParts.push(content);
        } else {
          if (systemParts.length > 0) {
            result.push({ role: "system", content: systemParts.join("\n\n") });
            systemParts.length = 0;
          }
          result.push({ role: effectiveApiRole, content });
        }
      }
      if (systemParts.length > 0) {
        result.push({ role: "system", content: systemParts.join("\n\n") });
      }
      return result;
    }
    /**
     * Build example dialogue messages from entries with role "example".
     * Parses SillyTavern's mes_example format (<START>\n{{user}}: ...\n{{char}}: ...)
     * into actual user/assistant message pairs with [Example Chat] markers.
     */
    buildExampleMessages(world, state, matchedEntries, userPrompts, toggledEntries, preCollected) {
      var _a;
      const entries = preCollected != null ? preCollected : this.collectEntries(world, matchedEntries, userPrompts, toggledEntries, state);
      const exampleEntries = entries.filter((e) => e.role === "example").sort(entrySort);
      if (exampleEntries.length === 0) return [];
      const result = [];
      const charName = this.resolveCharName(world);
      const userName = ((_a = world.settings) == null ? void 0 : _a.playerName) || "User";
      for (const entry of exampleEntries) {
        const text = this.interpolate(entry.content, world, state);
        const parsed = this.parseExampleDialogue(text, charName, userName);
        if (parsed.length > 0) {
          result.push({ role: "system", content: "[Example Chat]" });
          result.push(...parsed);
        }
      }
      return result;
    }
    /**
     * Parse a mes_example-format string into user/assistant message pairs.
     * Handles: <START>\n{{user}}: text\n{{char}}: text
     * Also handles: {{user}}: text\n{{char}}: text (no <START>)
     */
    parseExampleDialogue(text, charName, userName) {
      const result = [];
      const blocks = text.split(/<START>/i).filter((b) => b.trim());
      for (const block of blocks) {
        const lines = block.split("\n");
        let currentRole = null;
        let currentContent = [];
        const flush = () => {
          if (currentRole && currentContent.length > 0) {
            result.push({ role: currentRole, content: currentContent.join("\n").trim() });
          }
          currentContent = [];
        };
        for (const line of lines) {
          const trimmed = line.trim();
          if (!trimmed) continue;
          const userMatch = trimmed.match(new RegExp(`^(?:\\{\\{user\\}\\}|${escapeRegex2(userName)}):\\s*(.*)`, "i"));
          const charMatch = trimmed.match(new RegExp(`^(?:\\{\\{char\\}\\}|${escapeRegex2(charName)}):\\s*(.*)`, "i"));
          if (userMatch) {
            flush();
            currentRole = "user";
            if (userMatch[1]) currentContent.push(userMatch[1]);
          } else if (charMatch) {
            flush();
            currentRole = "assistant";
            if (charMatch[1]) currentContent.push(charMatch[1]);
          } else if (currentRole) {
            currentContent.push(trimmed);
          }
        }
        flush();
      }
      return result;
    }
    /** Resolve the character name from world entries */
    resolveCharName(world) {
      const charEntry = world.entries.find(
        (e) => e.role === "character" && e.enabled
      );
      return (charEntry == null ? void 0 : charEntry.name) || world.name || "Character";
    }
    /**
     * Trim messages to fit within a token budget.
     * Pinned prefix (system prompts, persona, examples) and pinned suffix
     * (format block, post-history) are always kept. Only the middle portion
     * (chat history + depth entries) is trimmed from oldest first.
     *
     * @param pinnedPrefix Number of messages at the start to always keep
     * @param pinnedSuffix Number of messages at the end to always keep
     * @param modelId Optional model id so the budget uses the right tokenizer
     *                (CJK-aware for Gemini/Claude, cl100k_base otherwise)
     */
    buildMessageHistory(messages, maxTokens, pinnedPrefix, pinnedSuffix, modelId) {
      const steps = this.messageHistorySteps(messages, maxTokens, pinnedPrefix, pinnedSuffix, modelId);
      let step = steps.next();
      while (!step.done) step = steps.next();
      return step.value;
    }
    /** Same trimming decisions as the synchronous API, with a host-supplied
     * scheduler so long histories cannot monopolize the server's request loop. */
    async buildMessageHistoryAsync(messages, yieldControl, maxTokens, pinnedPrefix, pinnedSuffix, modelId) {
      const steps = this.messageHistorySteps(messages, maxTokens, pinnedPrefix, pinnedSuffix, modelId);
      let deadline = performance.now() + 8;
      let step = steps.next();
      while (!step.done) {
        if (performance.now() >= deadline) {
          await yieldControl();
          deadline = performance.now() + 8;
        }
        step = steps.next();
      }
      return step.value;
    }
    *messageHistorySteps(messages, maxTokens, pinnedPrefix, pinnedSuffix, modelId) {
      if (!maxTokens || messages.length === 0) return [...messages];
      const pPrefix = pinnedPrefix != null ? pinnedPrefix : 0;
      const pSuffix = pinnedSuffix != null ? pinnedSuffix : 0;
      const prefix = messages.slice(0, pPrefix);
      const suffix = pSuffix > 0 ? messages.slice(messages.length - pSuffix) : [];
      const history = messages.slice(pPrefix, pSuffix > 0 ? messages.length - pSuffix : void 0);
      let pinnedTokens = 0;
      for (const msg of prefix) {
        pinnedTokens += estimateTokens(msg.content, modelId);
        yield;
      }
      for (const msg of suffix) {
        pinnedTokens += estimateTokens(msg.content, modelId);
        yield;
      }
      const historyBudget = maxTokens - pinnedTokens;
      if (historyBudget <= 0 || history.length === 0) {
        return [...prefix, ...suffix];
      }
      const trimmed = [];
      let totalTokens = 0;
      for (let i = history.length - 1; i >= 0; i--) {
        const msg = history[i];
        const msgTokens = estimateTokens(msg.content, modelId);
        yield;
        if (totalTokens + msgTokens > historyBudget && trimmed.length > 0) break;
        totalTokens += msgTokens;
        trimmed.unshift(msg);
      }
      return [...prefix, ...trimmed, ...suffix];
    }
    /**
     * Collect all relevant entries: alwaysSend + matched.
     * Includes all positions — callers filter by position as needed.
     * @param toggledEntries Runtime overrides from toggle-entry rule actions
     */
    collectEntries(world, matchedEntries, userPrompts, toggledEntries, state) {
      const isEnabled = (e) => {
        if (toggledEntries && e.id in toggledEntries) return toggledEntries[e.id];
        return e.enabled;
      };
      const alwaysSend = world.entries.filter(
        (e) => isEnabled(e) && e.alwaysSend && // Variable-bound entries are gated by their conditions (evaluated in
        // the lorebook matcher) and reach the prompt via matchedEntries only.
        // Without this guard an entry whose alwaysSend was clobbered to true
        // (bundle import used to do this) bypasses its conditions every turn
        // — mirror of lorebook-matcher.ts's alwaysSend bucket check.
        !isVariableBoundEntry(e) && e.role !== "greeting"
      );
      const seenIds = new Set(alwaysSend.map((e) => e.id));
      const matched = (matchedEntries != null ? matchedEntries : []).filter(
        (e) => isEnabled(e) && e.role !== "greeting" && !seenIds.has(e.id)
      );
      const userEntries = (userPrompts != null ? userPrompts : []).filter((p) => p.enabled && p.content.trim()).map((p, i) => {
        var _a;
        return {
          id: `user-prompt-${p.id}`,
          name: p.name,
          content: p.content,
          role: "custom",
          position: (_a = p.position) != null ? _a : 1e4 + i,
          alwaysSend: true,
          keywords: [],
          conditions: [],
          conditionLogic: "all",
          enabled: true,
          section: p.section,
          ...p.section === "chat-history" && p.depth !== void 0 && { depth: p.depth }
        };
      });
      const merged = [...alwaysSend, ...matched, ...userEntries];
      if (!state) return merged;
      const afterWorldbooks = filterEntriesByActiveWorldbooks(merged, world.worldbooks, state);
      return filterEntriesByActiveLoreSlots(afterWorldbooks, world.loreUiBindings, state);
    }
    /**
     * Assemble the final prompt string from collected entries.
     */
    assemblePrompt(world, state, entries, directives) {
      const parts = [];
      const activeDirectives = directives != null ? directives : [];
      const getDirectivesForPosition = (pos) => activeDirectives.filter((d) => d.position === pos).map((d) => this.interpolate(d.content, world, state));
      const autoDirectives = activeDirectives.filter((d) => d.position === "auto").map((d) => this.interpolate(d.content, world, state));
      parts.push(...getDirectivesForPosition("top"));
      parts.push(...getDirectivesForPosition("before_char"));
      const systemEntries = entries.filter((e) => {
        if (e.section === "chat-history" || e.section === "post-history") return false;
        if (e.role === "greeting" || e.role === "example") return false;
        return true;
      }).sort(entrySort);
      for (const entry of systemEntries) {
        parts.push(this.interpolate(entry.content, world, state));
      }
      parts.push(...getDirectivesForPosition("after_char"));
      parts.push(...autoDirectives);
      parts.push(...getDirectivesForPosition("bottom"));
      return parts.join("\n\n");
    }
    interpolate(template, world, state) {
      return expandMacros(template, world, state);
    }
    /**
     * Build a compact variable summary for the dynamic game-state block.
     * Shows ALL variables as ID: value pairs — gives the AI a complete picture
     * of current state every turn. Behavior-rules in the cached prefix teaches
     * what each ID means; this block just shows the values.
     */
    buildVariableSummary(world, state) {
      if (world.variables.length === 0) return "";
      return world.variables.filter((v) => isAiReadable(v, state)).map((v) => {
        var _a;
        const value = (_a = state.variables[v.id]) != null ? _a : v.defaultValue;
        const suffix = v.aiAccess === "read" ? " (read-only)" : "";
        return `${v.id}: ${renderVariableValueForPrompt(value)}${suffix}`;
      }).join("\n");
    }
    /**
     * Build the "Variable behavior rules" section for variables that have rules.
     * `updateHints` is the pre-rename field name; we read it as a fallback so
     * worlds exported before the rename still expose their rules to the LLM.
     */
    buildBehaviorRulesSection(world) {
      const lines = [];
      for (const v of world.variables) {
        if (!isAiExposedStatic(v)) continue;
        const rules = v.behaviorRules || v.updateHints;
        if (rules) {
          const header = v.id !== v.name ? `[${v.id} | ${v.name}]` : `[${v.id}]`;
          lines.push(header);
          lines.push(rules);
        }
      }
      if (lines.length === 0) return "";
      return lines.join("\n");
    }
    /**
     * Build a static cost breakdown of the world's prompt.
     * Only includes always-send + enabled entries (no matched entries —
     * this is a design-time analysis, not a runtime one).
     */
    buildPromptCostBreakdown(world, state) {
      var _a;
      const blocks = [];
      const alwaysSendEntries = world.entries.filter(
        (e) => e.enabled && e.alwaysSend && !isVariableBoundEntry(e) && e.role !== "greeting"
      );
      const systemEntries = alwaysSendEntries.filter((e) => e.section !== "chat-history" && e.section !== "post-history" && e.role !== "example").sort(entrySort);
      for (const entry of systemEntries) {
        const text = this.interpolate(entry.content, world, state);
        blocks.push({
          label: entry.name || `Entry (${entry.section})`,
          category: "entry",
          tokens: estimateTokens(text),
          chars: text.length
        });
      }
      const depthEntries = alwaysSendEntries.filter((e) => e.section === "chat-history").sort(entrySort);
      for (const entry of depthEntries) {
        const text = this.interpolate(entry.content, world, state);
        blocks.push({
          label: entry.name || "Depth Entry",
          category: "entry",
          tokens: estimateTokens(text),
          chars: text.length
        });
      }
      const postHistoryEntries = alwaysSendEntries.filter((e) => e.section === "post-history").sort(entrySort);
      for (const entry of postHistoryEntries) {
        const text = this.interpolate(entry.content, world, state);
        blocks.push({
          label: entry.name || "Post-History",
          category: "entry",
          tokens: estimateTokens(text),
          chars: text.length
        });
      }
      const varSummary = this.buildVariableSummary(world, state);
      if (varSummary) {
        const fullText = `Current game state:
${varSummary}`;
        blocks.push({
          label: "Variable Summary",
          category: "variable-summary",
          tokens: estimateTokens(fullText),
          chars: fullText.length
        });
      }
      const formatText = 'IMPORTANT \u2014 Variable directives are the ONLY way to change game state.\nDirective format: [variableId: operation value]\nExamples: [health: -10], [gold: +50], [location: set "forest"], [hasKey: toggle]';
      blocks.push({
        label: "Directive Format Instructions",
        category: "format-instructions",
        tokens: estimateTokens(formatText),
        chars: formatText.length
      });
      const behaviorRulesSection = this.buildBehaviorRulesSection(world);
      if (behaviorRulesSection) {
        blocks.push({
          label: "Variable Behavior Rules",
          category: "variable-summary",
          tokens: estimateTokens(behaviorRulesSection),
          chars: behaviorRulesSection.length
        });
      }
      const audioTracks = getAiAudioTracks((_a = world.audioTracks) != null ? _a : []);
      if (audioTracks.length > 0) {
        const trackList = audioTracks.map((t) => `  - ${t.id} (${t.type}): ${t.name}`).join("\n");
        const audioText = `Available audio tracks:
${trackList}`;
        blocks.push({
          label: "Audio Instructions",
          category: "audio-instructions",
          tokens: estimateTokens(audioText),
          chars: audioText.length
        });
      }
      const totalTokens = blocks.reduce((sum, b) => sum + b.tokens, 0);
      const totalChars = blocks.reduce((sum, b) => sum + b.chars, 0);
      return { blocks, totalTokens, totalChars };
    }
  };

  // src/runtime/start-session.ts
  var promptBuilder = new PromptBuilder();
  function startSession(input) {
    var _a, _b, _c, _d;
    const { world } = input;
    const greetingEntries = promptBuilder.buildGreetingEntries(world);
    const index = Math.min(Math.max((_a = input.greetingIndex) != null ? _a : 0, 0), Math.max(greetingEntries.length - 1, 0));
    const chosen = greetingEntries[index];
    const base = new GameStateManager(world);
    for (const [k, v] of Object.entries((_b = input.variables) != null ? _b : {})) base.set(k, v);
    const baseState = base.getSnapshot();
    let state = baseState;
    if (chosen) {
      const sm = new GameStateManager(world, baseState);
      for (const [k, v] of Object.entries((_c = chosen.initialVariables) != null ? _c : {})) sm.set(k, v);
      state = sm.getSnapshot();
      state.activeGreetingId = chosen.id;
    }
    const greetings = promptBuilder.buildGreetings(world, baseState);
    return {
      state,
      greeting: (_d = greetings[index]) != null ? _d : null,
      greetings
    };
  }
  function applyVariables(world, state, variables) {
    const sm = new GameStateManager(world, state);
    for (const [k, v] of Object.entries(variables)) sm.set(k, v);
    return sm.getSnapshot();
  }

  // src/lorebook/keyword-matcher.ts
  var USER_REGEX_RE = /^\/(.+)\/([gimsuy]*)$/;
  function keywordMatches(text, keyword, wholeWord) {
    const lowerText = text.toLowerCase();
    const lowerKeyword = keyword.toLowerCase();
    const regexMatch = USER_REGEX_RE.exec(keyword);
    if (regexMatch) {
      try {
        const flags = regexMatch[2].includes("i") ? regexMatch[2] : regexMatch[2] + "i";
        const re = new RegExp(regexMatch[1], flags);
        if (re.test(text)) return true;
      } catch {
      }
    }
    if (wholeWord) {
      try {
        const escaped = lowerKeyword.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
        const re = new RegExp(`\\b${escaped}\\b`, "i");
        if (re.test(text)) return true;
      } catch {
      }
      return false;
    }
    return lowerText.includes(lowerKeyword);
  }

  // src/lorebook/lorebook-matcher.ts
  var LorebookMatcher = class {
    /**
     * Full match with token budgeting.
     * @param entries All entries for this world (filters out greeting/disabled internally)
     * @param recentMessages Recent message texts to scan for keywords
     * @param state Current game state for condition evaluation
     * @param tokenBudget Max tokens for triggered entries (default Infinity — include all)
     * @param settings Optional world settings (for recursion depth)
     * @param modelId Optional model id so the budget uses the right tokenizer
     *                (CJK-aware for Gemini/Claude, cl100k_base otherwise)
     */
    matchWithBudget(entries, recentMessages, state, tokenBudget = Infinity, settings, modelId, loreUiBindings) {
      var _a, _b, _c, _d;
      const recursionDepth = Math.min(
        Math.max((_a = settings == null ? void 0 : settings.lorebookRecursionDepth) != null ? _a : 0, 0),
        10
      );
      const alwaysSend = [];
      const candidates = [];
      for (const entry of entries) {
        if (entry.role === "greeting") continue;
        const variableBound = isVariableBoundEntry(entry);
        if (!entry.enabled && !variableBound) continue;
        const uiBound = isUiBoundEntry(entry.id, loreUiBindings);
        if (entry.alwaysSend && !variableBound && !uiBound) {
          alwaysSend.push(entry);
        } else {
          candidates.push(entry);
        }
      }
      if (candidates.length === 0) {
        return { alwaysSend, triggered: [], triggeredTokens: 0 };
      }
      let textToScan = recentMessages.join(" ");
      const activatedIds = /* @__PURE__ */ new Set();
      const scoredEntries = [];
      for (let depth = 0; depth <= recursionDepth; depth++) {
        const newlyActivated = [];
        for (const entry of candidates) {
          if (activatedIds.has(entry.id)) continue;
          if (depth > 0 && entry.excludeRecursion) continue;
          const boundSlotId = getEntryBoundSlotId(entry.id, loreUiBindings);
          if (boundSlotId && !isLoreSlotActive(boundSlotId, state)) continue;
          if (entry.conditions.length > 0 && !this.checkConditions(state, entry.conditions, entry.conditionLogic)) {
            continue;
          }
          const wholeWord = (_b = entry.matchWholeWords) != null ? _b : false;
          if (entry.keywords.length === 0) {
            if (entry.conditions.length === 0 && !boundSlotId) continue;
            newlyActivated.push({ entry, score: 0 });
            continue;
          }
          let matchedPrimaryCount = 0;
          for (const kw of entry.keywords) {
            if (keywordMatches(textToScan, kw, wholeWord)) {
              matchedPrimaryCount++;
            }
          }
          if (matchedPrimaryCount === 0) continue;
          const secondaryKws = (_c = entry.secondaryKeywords) != null ? _c : [];
          const secondaryLogic = (_d = entry.secondaryKeywordLogic) != null ? _d : "AND_ANY";
          if (secondaryKws.length > 0) {
            const secondaryMatches = secondaryKws.filter(
              (kw) => keywordMatches(textToScan, kw, wholeWord)
            );
            const matchedCount = secondaryMatches.length;
            const totalCount = secondaryKws.length;
            let secondaryPass = false;
            switch (secondaryLogic) {
              case "AND_ANY":
                secondaryPass = matchedCount > 0;
                break;
              case "AND_ALL":
                secondaryPass = matchedCount === totalCount;
                break;
              case "NOT_ANY":
                secondaryPass = matchedCount === 0;
                break;
              case "NOT_ALL":
                secondaryPass = matchedCount < totalCount;
                break;
            }
            if (!secondaryPass) continue;
          }
          const secondaryScore = secondaryKws.length > 0 ? 1 : 0;
          const score = matchedPrimaryCount + secondaryScore;
          newlyActivated.push({ entry, score });
        }
        if (newlyActivated.length === 0) break;
        for (const scored of newlyActivated) {
          activatedIds.add(scored.entry.id);
          scoredEntries.push(scored);
        }
        if (depth < recursionDepth) {
          const newContent = newlyActivated.filter((s) => !s.entry.preventRecursion).map((s) => s.entry.content).join(" ");
          if (newContent) {
            textToScan = textToScan + " " + newContent;
          }
        }
      }
      const resolved = scoredEntries;
      resolved.sort((a, b) => {
        var _a2, _b2;
        const aPos = (_a2 = a.entry.position) != null ? _a2 : 0;
        const bPos = (_b2 = b.entry.position) != null ? _b2 : 0;
        if (aPos !== bPos) return aPos - bPos;
        return b.score - a.score;
      });
      const budgeted = [];
      let usedTokens = 0;
      for (const { entry } of resolved) {
        const entryTokens = estimateTokens(entry.content, modelId);
        if (usedTokens + entryTokens > tokenBudget && budgeted.length > 0) {
          break;
        }
        budgeted.push(entry);
        usedTokens += entryTokens;
      }
      return {
        alwaysSend,
        triggered: budgeted,
        triggeredTokens: usedTokens
      };
    }
    /**
     * Legacy match method — returns flat array (always-send + triggered combined).
     */
    match(entries, recentMessages, state) {
      const result = this.matchWithBudget(entries, recentMessages, state);
      return [...result.alwaysSend, ...result.triggered];
    }
    checkConditions(state, conditions, logic) {
      return checkConditions(state, conditions, logic);
    }
    /** @internal Exposed for tests that assert on single-condition evaluation. */
    evaluateCondition(state, condition) {
      return evaluateCondition(state, condition);
    }
  };

  // src/parser/thinking-tag-filter.ts
  var THINKING_TAGS = /* @__PURE__ */ new Set([
    "think",
    "thinking",
    "fiction-mode",
    "reasoning",
    "reflection",
    "inner-thought",
    "brainstorming"
  ]);
  var ECHOED_TAGS = /* @__PURE__ */ new Set([
    "game-state",
    // The per-turn update reminder PromptBuilder.buildFormatBlock appends right after
    // <game-state>. Models that mirror the state block tend to mirror this too — strip
    // it so the instruction text never leaks into the narrative.
    "state-reminder",
    // Last-turn variable changes, rendered between the two blocks above by
    // buildFormatBlock. Same mirroring risk, same treatment.
    "last-turn-changes"
  ]);
  var SUPPRESSED_TAGS = /* @__PURE__ */ new Set([...THINKING_TAGS, ...ECHOED_TAGS]);
  var ThinkingTagFilter = class {
    constructor() {
      __publicField(this, "state", "normal");
      /** Buffer for characters after `<` while we determine if it's a thinking tag */
      __publicField(this, "tagBuffer", "");
      /** The thinking tag we're currently suppressing (lowercase) */
      __publicField(this, "blockTag", "");
      /** Accumulates content inside the block so we can detect the closing tag */
      __publicField(this, "closeBuffer", "");
    }
    push(chunk) {
      let output = "";
      let i = 0;
      while (i < chunk.length) {
        const ch = chunk[i];
        switch (this.state) {
          case "normal":
            if (ch === "<") {
              this.state = "in_tag";
              this.tagBuffer = "<";
            } else {
              output += ch;
            }
            break;
          case "in_tag":
            this.tagBuffer += ch;
            if (ch === ">") {
              const match = this.tagBuffer.match(/^<([\w-]+)>$/i);
              if (match && SUPPRESSED_TAGS.has(match[1].toLowerCase())) {
                this.state = "in_block";
                this.blockTag = match[1].toLowerCase();
                this.closeBuffer = "";
              } else {
                output += this.tagBuffer;
                this.state = "normal";
              }
              this.tagBuffer = "";
            } else if (this.tagBuffer.length > 30) {
              output += this.tagBuffer;
              this.tagBuffer = "";
              this.state = "normal";
            }
            break;
          case "in_block": {
            this.closeBuffer += ch;
            const closeTag = `</${this.blockTag}>`;
            if (this.closeBuffer.toLowerCase().endsWith(closeTag)) {
              this.state = "normal";
              this.blockTag = "";
              this.closeBuffer = "";
            } else if (this.closeBuffer.length > 2e4) {
              this.closeBuffer = this.closeBuffer.slice(-closeTag.length);
            }
            break;
          }
        }
        i++;
      }
      return output;
    }
    /** Flush any buffered content at end of stream. */
    flush() {
      if (this.state === "in_tag") {
        const result = this.tagBuffer;
        this.tagBuffer = "";
        this.state = "normal";
        return result;
      }
      return "";
    }
    reset() {
      this.state = "normal";
      this.tagBuffer = "";
      this.blockTag = "";
      this.closeBuffer = "";
    }
    /** One-shot: strip all thinking tag blocks from a complete string. */
    static strip(text) {
      let result = text;
      for (const tag of SUPPRESSED_TAGS) {
        const pattern = new RegExp(`<${tag}>[\\s\\S]*?<\\/${tag}>`, "gi");
        result = result.replace(pattern, "");
      }
      return result.replace(/\n{3,}/g, "\n\n").trim();
    }
  };

  // src/parser/state-receipt.ts
  function stripStateReceipts(text) {
    let fence = false;
    return text.split("\n").filter((line) => {
      if (/^\s*(?:```|~~~)/.test(line)) fence = !fence;
      return fence || !/^[\t ]*<yumina-state(?:\s|\/|>|$)/.test(line);
    }).join("\n").trimEnd();
  }

  // src/parser/response-parser.ts
  var ResponseParser = class {
    constructor(pattern) {
      __publicField(this, "pattern");
      __publicField(this, "audioPattern");
      // JSON directives use a bracket-balancing scanner, not a regex — non-greedy
      // regexes mis-terminate on nested `[]`/`{}` inside the value (e.g. `"comments":[]`).
      __publicField(this, "jsonVarIdPattern", /[\w\p{L}\p{N}.$-]+/u);
      __publicField(this, "jsonOps", ["set", "merge", "push", "delete"]);
      this.pattern = pattern != null ? pattern : /\[\s*([\w\p{L}\p{N}.$-]+):\s*(set|add|subtract|multiply|toggle|append|\+|-|\*)?\s*("(?:[^"\\]|\\.)*"|[^\]"]+)?\s*\]/gu;
      this.audioPattern = /\[\s*audio:\s*([\w\p{L}\p{N}-]+)\s+(play|stop|crossfade|volume)(?:\s+([\d.]+))?(?:\s+chain:([\w\p{L}\p{N}-]+))?\s*\]/gu;
    }
    parse(responseText, jsonVarId) {
      const effects = [];
      const audioEffects = [];
      this.pattern.lastIndex = 0;
      this.audioPattern.lastIndex = 0;
      const stripped = stripStateReceipts(ThinkingTagFilter.strip(responseText));
      const speakerTag = parseLeadingSpeakerTag(stripped);
      let text = this.extractJsonPatch(speakerTag.text, effects, jsonVarId != null ? jsonVarId : "game_state");
      text = this.extractFencedDirectives(text, effects);
      text = text.replace(this.audioPattern, (_match, trackId, action, value, chainTo) => {
        const effect = { trackId, action };
        if (value !== void 0) {
          const num = parseFloat(value);
          if (!isNaN(num)) {
            if (action === "volume") effect.volume = num;
            else if (action === "crossfade") effect.fadeDuration = num;
          }
        }
        if (chainTo) effect.chainTo = chainTo;
        audioEffects.push(effect);
        return "";
      });
      text = this.extractJsonDirectives(text, effects);
      const cleanText = text.replace(this.pattern, (_match, variableId, op, rawValue) => {
        const effect = this.parseDirective(variableId, op, rawValue);
        if (effect) effects.push(effect);
        return "";
      }).replace(/^\s*<\/?(maintext|option|sum|Analysis|UpdateVariable|JSONPatch|status_current_variable)\s*>\s*$/gim, "").replace(/[^\S\n]+/g, " ").replace(/\n{3,}/g, "\n\n").trim();
      return speakerTag.speaker ? { cleanText, effects, audioEffects, speaker: speakerTag.speaker } : { cleanText, effects, audioEffects };
    }
    parseDirective(variableId, op, rawValue) {
      if (rawValue) rawValue = rawValue.trim();
      if (op === "toggle") {
        return { variableId, operation: "toggle", value: true };
      }
      if (op === "+" && rawValue) {
        const num = Number(rawValue);
        if (!isNaN(num)) {
          return { variableId, operation: "add", value: num };
        }
      }
      if (op === "-" && rawValue) {
        const num = Number(rawValue);
        if (!isNaN(num)) {
          return { variableId, operation: "subtract", value: num };
        }
      }
      if (op === "*" && rawValue) {
        const num = Number(rawValue);
        if (!isNaN(num)) {
          return { variableId, operation: "multiply", value: num };
        }
      }
      if (op && rawValue) {
        const operation = op;
        const value = this.parseValue(rawValue);
        return { variableId, operation, value };
      }
      if (!op && rawValue) {
        const value = this.parseValue(rawValue);
        return { variableId, operation: "set", value };
      }
      if (op && !rawValue) {
        const num = Number(op);
        if (!isNaN(num)) {
          return { variableId, operation: "set", value: num };
        }
      }
      return null;
    }
    /**
     * Extract `[varId: set|merge|push|delete <json>]` directives. The value is
     * any JSON value — object/array (requires brace-balancing because regex
     * mis-terminates on nested `[]`/`{}`, e.g. `"comments":[]` inside a pushed
     * object), or a scalar (string/number/boolean/null).
     *
     * Without scalar support, `[truths: push "new truth"]` was silently
     * dropped — `push` isn't in the standard regex's op list, and the JSON
     * branch insisted on object/array openers — so well-formed AI output
     * landed in the void.
     */
    extractJsonDirectives(text, effects) {
      const out = [];
      const n = text.length;
      let i = 0;
      while (i < n) {
        const lb = text.indexOf("[", i);
        if (lb === -1) {
          out.push(text.slice(i));
          break;
        }
        out.push(text.slice(i, lb));
        let p = lb + 1;
        while (p < n && /\s/.test(text[p])) p++;
        const varMatch = this.jsonVarIdPattern.exec(text.slice(p));
        if (!varMatch || varMatch.index !== 0) {
          out.push("[");
          i = lb + 1;
          continue;
        }
        const variableId = varMatch[0];
        p += variableId.length;
        if (text[p] !== ":") {
          out.push("[");
          i = lb + 1;
          continue;
        }
        p++;
        while (p < n && /\s/.test(text[p])) p++;
        let op = null;
        for (const candidate of this.jsonOps) {
          if (text.slice(p, p + candidate.length) === candidate) {
            const after = text[p + candidate.length];
            if (after && /\s/.test(after)) {
              op = candidate;
              p += candidate.length;
              break;
            }
          }
        }
        if (!op) {
          out.push("[");
          i = lb + 1;
          continue;
        }
        while (p < n && /\s/.test(text[p])) p++;
        const q = this.scanJsonValue(text, p, n);
        if (q === -1) {
          out.push("[");
          i = lb + 1;
          continue;
        }
        const jsonStr = text.slice(p, q);
        let r = q;
        while (r < n && /\s/.test(text[r])) r++;
        if (text[r] !== "]") {
          out.push("[");
          i = lb + 1;
          continue;
        }
        r++;
        try {
          const parsed = JSON.parse(jsonStr);
          effects.push({
            variableId,
            operation: op,
            value: parsed
          });
        } catch {
        }
        i = r;
      }
      return out.join("");
    }
    /**
     * Find the end of a JSON value starting at `p` in `text`. Returns the
     * index one past the last character of the value, or -1 if no recognizable
     * JSON value begins at `p`.
     *
     * Supports objects, arrays, strings, numbers, booleans, and null. Bare
     * identifiers (e.g. `forest` in `[location: set forest]`) return -1 so
     * the caller can fall through to the standard non-JSON directive path.
     */
    scanJsonValue(text, p, n) {
      if (p >= n) return -1;
      const ch = text[p];
      if (ch === "{" || ch === "[") {
        let brace = 0;
        let bracket = 0;
        let inString = false;
        let escape = false;
        let q = p;
        for (; q < n; q++) {
          const c = text[q];
          if (escape) {
            escape = false;
            continue;
          }
          if (c === "\\") {
            escape = true;
            continue;
          }
          if (inString) {
            if (c === '"') inString = false;
            continue;
          }
          if (c === '"') {
            inString = true;
            continue;
          }
          if (c === "{") brace++;
          else if (c === "}") brace--;
          else if (c === "[") bracket++;
          else if (c === "]") bracket--;
          if (brace === 0 && bracket === 0) {
            q++;
            break;
          }
        }
        if (brace !== 0 || bracket !== 0) return -1;
        return q;
      }
      if (ch === '"') {
        let q = p + 1;
        let escape = false;
        for (; q < n; q++) {
          const c = text[q];
          if (escape) {
            escape = false;
            continue;
          }
          if (c === "\\") {
            escape = true;
            continue;
          }
          if (c === '"') return q + 1;
        }
        return -1;
      }
      if (ch === "-" || ch >= "0" && ch <= "9") {
        let q = p;
        if (text[q] === "-") q++;
        if (q >= n) return -1;
        const first = text[q];
        if (first === "0") {
          q++;
        } else if (first >= "1" && first <= "9") {
          while (q < n && text[q] >= "0" && text[q] <= "9") q++;
        } else {
          return -1;
        }
        if (text[q] === ".") {
          q++;
          if (q >= n || text[q] < "0" || text[q] > "9") return -1;
          while (q < n && text[q] >= "0" && text[q] <= "9") q++;
        }
        if (text[q] === "e" || text[q] === "E") {
          q++;
          if (text[q] === "+" || text[q] === "-") q++;
          if (q >= n || text[q] < "0" || text[q] > "9") return -1;
          while (q < n && text[q] >= "0" && text[q] <= "9") q++;
        }
        return q;
      }
      if (text.slice(p, p + 4) === "true" || text.slice(p, p + 4) === "null") return p + 4;
      if (text.slice(p, p + 5) === "false") return p + 5;
      return -1;
    }
    /**
     * Extract fenced code blocks whose entire content is a directive
     * object/array — the shape some models (observed: Gemini 3.1 Pro) drift
     * into when they rewrite inline directives as JSON:
     *
     *   ```json
     *   [
     *    {"place": "set 第一层-福利中心"},
     *    {"mission": "set 休假体验福利中心"}
     *   ]
     *   ```
     *
     * Without this, the block leaks into the visible narrative AND the
     * variable changes are silently lost.
     *
     * All-or-nothing gate: the block is consumed only if every key is
     * variable-id-shaped and every value is a string that parses as a known
     * operation ("set X", "add 5", "toggle", "+5", ...). Anything else —
     * story JSON, code samples, numeric values — leaves the fence untouched
     * so legitimate fenced JSON in narrative is never eaten. Unknown
     * variable ids in a consumed block are still no-ops downstream
     * (GameStateManager skips unresolved variables).
     */
    extractFencedDirectives(text, effects) {
      const fencePattern = /```[ \t]*[A-Za-z]*[ \t]*\r?\n?([\s\S]*?)```/g;
      return text.replace(fencePattern, (match, body) => {
        const parsed = this.tryParseDirectiveBlock(body);
        if (!parsed) return match;
        effects.push(...parsed);
        return "";
      });
    }
    /** Parse a fence body as a directive object/array, or null if it isn't one. */
    tryParseDirectiveBlock(body) {
      const trimmed = body.trim();
      if (!trimmed.startsWith("{") && !trimmed.startsWith("[")) return null;
      let parsed;
      try {
        parsed = JSON.parse(trimmed);
      } catch {
        return null;
      }
      const objs = Array.isArray(parsed) ? parsed : [parsed];
      if (objs.length === 0) return null;
      const collected = [];
      for (const obj of objs) {
        if (typeof obj !== "object" || obj === null || Array.isArray(obj)) return null;
        const entries = Object.entries(obj);
        if (entries.length === 0) return null;
        for (const [key, value] of entries) {
          if (!this.isVarIdShaped(key)) return null;
          if (typeof value !== "string") return null;
          const effect = this.parseOpString(key, value.trim());
          if (!effect) return null;
          collected.push(effect);
        }
      }
      return collected;
    }
    /** True when the whole key matches the variable-id character set. */
    isVarIdShaped(key) {
      const m = this.jsonVarIdPattern.exec(key);
      return m !== null && m.index === 0 && m[0].length === key.length;
    }
    /**
     * Parse an "operation value" string ("set 第一层", "add 5", "toggle",
     * "+5") into an Effect. Returns null when the string doesn't start with
     * a recognized operation — the caller treats that as "not a directive".
     */
    parseOpString(variableId, raw) {
      if (raw === "toggle") {
        return { variableId, operation: "toggle", value: true };
      }
      const short = /^([+\-*])\s*(\d+(?:\.\d+)?)$/.exec(raw);
      if (short) {
        return this.parseDirective(variableId, short[1], short[2]);
      }
      const m = /^(set|add|subtract|multiply|append|merge|push|delete)\s+([\s\S]+)$/.exec(raw);
      if (!m) return null;
      const op = m[1];
      const rest = m[2].trim();
      if (op === "merge" || op === "push" || op === "delete") {
        try {
          return {
            variableId,
            operation: op,
            value: JSON.parse(rest)
          };
        } catch {
          return { variableId, operation: op, value: this.parseValue(rest) };
        }
      }
      return this.parseDirective(variableId, op, rest);
    }
    /**
     * Extract <UpdateVariable> blocks containing JSON Patch operations.
     * Converts each patch op into a Yumina Effect targeting a JSON variable.
     *
     * Supported ops:
     *   replace → set at dot-path
     *   delta   → add (positive) or subtract (negative) at dot-path
     *   insert  → set at dot-path (creates new key)
     *   remove  → delete at dot-path
     */
    extractJsonPatch(text, effects, rootVarId) {
      const updateVarPattern = /<UpdateVariable>[\s\S]*?<JSONPatch>\s*([\s\S]*?)\s*<\/JSONPatch>[\s\S]*?<\/UpdateVariable>/gi;
      return text.replace(updateVarPattern, (_match, patchJson) => {
        try {
          const patches = JSON.parse(patchJson);
          for (const patch of patches) {
            const dotPath = patch.path.replace(/^\//, "").replace(/\//g, ".");
            const fullPath = `${rootVarId}.${dotPath}`;
            switch (patch.op) {
              case "replace":
              case "insert":
                effects.push({
                  variableId: fullPath,
                  operation: "set",
                  value: patch.value
                });
                break;
              case "delta": {
                const num = Number(patch.value);
                if (!isNaN(num)) {
                  effects.push({
                    variableId: fullPath,
                    operation: num >= 0 ? "add" : "subtract",
                    value: Math.abs(num)
                  });
                }
                break;
              }
              case "remove":
                effects.push({
                  variableId: fullPath,
                  operation: "delete",
                  value: true
                });
                break;
            }
          }
        } catch {
        }
        return "";
      });
    }
    parseValue(raw) {
      if (raw.startsWith('"') && raw.endsWith('"')) {
        return raw.slice(1, -1).replace(/\\"/g, '"');
      }
      if (raw === "true") return true;
      if (raw === "false") return false;
      const num = Number(raw);
      if (!isNaN(num)) return num;
      return raw;
    }
  };

  // src/parser/structured-response-parser.ts
  var StructuredResponseParser = class {
    /** Strip markdown code fences (```json ... ``` or ``` ... ```) if present. */
    stripCodeFence(text) {
      const trimmed = text.trim();
      const match = trimmed.match(/^```(?:json|JSON)?\s*\n?([\s\S]*?)\n?\s*```\s*$/);
      return match ? match[1] : trimmed;
    }
    /**
     * Checks whether the given text is a structured JSON response with a `narrative` field.
     */
    isStructuredResponse(text) {
      const trimmed = this.stripCodeFence(text);
      if (!trimmed.startsWith("{")) return false;
      try {
        const parsed = JSON.parse(trimmed);
        return typeof parsed === "object" && parsed !== null && typeof parsed.narrative === "string";
      } catch {
        return /"narrative"\s*:\s*"/.test(trimmed);
      }
    }
    /**
     * Parses a structured JSON response into a ParseResult.
     * Falls back to returning the raw text as cleanText if parsing fails.
     */
    parse(text) {
      const trimmed = this.stripCodeFence(text);
      let parsed;
      try {
        parsed = JSON.parse(trimmed);
      } catch {
        const repaired = this.tryRepairJSON(trimmed);
        if (!repaired) {
          return { cleanText: text, effects: [], audioEffects: [] };
        }
        parsed = repaired;
      }
      const cleanText = typeof parsed.narrative === "string" ? parsed.narrative : "";
      const effects = [];
      const rawChanges = parsed.stateChanges;
      if (Array.isArray(rawChanges)) {
        for (const sc of rawChanges) {
          if (typeof sc === "object" && sc !== null && typeof sc.variableId === "string" && typeof sc.operation === "string") {
            effects.push({
              variableId: sc.variableId,
              operation: sc.operation,
              value: sc.value
            });
          }
        }
      } else if (typeof rawChanges === "object" && rawChanges !== null && !Array.isArray(rawChanges)) {
        for (const [key, value] of Object.entries(rawChanges)) {
          effects.push({
            variableId: key,
            operation: "set",
            value
          });
        }
      }
      const audioEffects = [];
      if (Array.isArray(parsed.audioEffects)) {
        for (const ae of parsed.audioEffects) {
          if (typeof ae === "object" && ae !== null && typeof ae.trackId === "string" && typeof ae.action === "string") {
            const effect = {
              trackId: ae.trackId,
              action: ae.action
            };
            if (typeof ae.volume === "number") effect.volume = ae.volume;
            if (typeof ae.fadeDuration === "number") effect.fadeDuration = ae.fadeDuration;
            audioEffects.push(effect);
          }
        }
      }
      return { cleanText, effects, audioEffects };
    }
    /**
     * Attempts to repair truncated JSON by closing unmatched braces/brackets.
     * Common when LLM output hits max token limits mid-JSON.
     */
    tryRepairJSON(text) {
      if (!text.startsWith("{")) return null;
      let repaired = this.trimToLastComplete(text);
      let closers = this.findMissingClosers(repaired);
      if (closers) {
        try {
          const parsed = JSON.parse(repaired + closers);
          if (typeof parsed === "object" && parsed !== null && typeof parsed.narrative === "string") {
            return parsed;
          }
        } catch {
        }
      }
      closers = this.findMissingClosers(text);
      if (closers) {
        try {
          const parsed = JSON.parse(text + closers);
          if (typeof parsed === "object" && parsed !== null && typeof parsed.narrative === "string") {
            return parsed;
          }
        } catch {
        }
      }
      return null;
    }
    /**
     * Trim text to the last position that could be a complete JSON value
     * (after a closing `}`, `]`, `"`, digit, true/false/null).
     */
    trimToLastComplete(text) {
      let inString = false;
      let lastGoodIdx = -1;
      for (let i = 0; i < text.length; i++) {
        const ch = text[i];
        if (inString) {
          if (ch === "\\" && i + 1 < text.length) {
            i++;
            continue;
          }
          if (ch === '"') {
            inString = false;
            lastGoodIdx = i;
          }
          continue;
        }
        if (ch === '"') {
          inString = true;
          continue;
        }
        if (ch === "}" || ch === "]") {
          lastGoodIdx = i;
        }
        if (ch === ",") {
          lastGoodIdx = i - 1;
        }
      }
      if (inString) {
        let depth = 0;
        for (let i = text.length - 1; i >= 0; i--) {
          if (text[i] === '"' && (i === 0 || text[i - 1] !== "\\")) {
            depth++;
            if (depth === 1) {
              let trimIdx = i;
              while (trimIdx > 0 && text[trimIdx - 1] === " ") trimIdx--;
              if (trimIdx > 0 && text[trimIdx - 1] === ":") trimIdx--;
              while (trimIdx > 0 && text[trimIdx - 1] === " ") trimIdx--;
              if (trimIdx > 0 && text[trimIdx - 1] === '"') {
                const keyStart = text.lastIndexOf('"', trimIdx - 2);
                if (keyStart >= 0) trimIdx = keyStart;
              }
              while (trimIdx > 0 && (text[trimIdx - 1] === "," || text[trimIdx - 1] === " " || text[trimIdx - 1] === "\n")) trimIdx--;
              return text.slice(0, trimIdx);
            }
          }
        }
      }
      return lastGoodIdx > 0 ? text.slice(0, lastGoodIdx + 1) : text;
    }
    /**
     * Scan text for unmatched `{` and `[` (respecting string literals)
     * and return the closing characters needed, or null if nothing to close.
     */
    findMissingClosers(text) {
      const stack = [];
      let inString = false;
      for (let i = 0; i < text.length; i++) {
        const ch = text[i];
        if (inString) {
          if (ch === "\\" && i + 1 < text.length) {
            i++;
            continue;
          }
          if (ch === '"') inString = false;
          continue;
        }
        if (ch === '"') {
          inString = true;
          continue;
        }
        if (ch === "{") stack.push("}");
        else if (ch === "[") stack.push("]");
        else if (ch === "}" || ch === "]") {
          if (stack.length > 0 && stack[stack.length - 1] === ch) {
            stack.pop();
          }
        }
      }
      if (stack.length === 0) return null;
      return stack.reverse().join("");
    }
  };

  // src/events/event-matcher.ts
  function matchesEventPattern(event, pattern) {
    if (!matchesEventType(event.type, pattern.eventType)) {
      return false;
    }
    if (pattern.match) {
      for (const [field, condition] of Object.entries(pattern.match)) {
        const eventValue = event[field];
        if (!matchesCondition(eventValue, condition)) {
          return false;
        }
      }
    }
    return true;
  }
  function matchesEventType(eventType, patternType) {
    if (patternType === eventType) return true;
    if (patternType.endsWith(":*")) {
      const prefix = patternType.slice(0, -1);
      return eventType.startsWith(prefix);
    }
    return false;
  }
  function matchesCondition(value, condition) {
    const target = condition.value;
    switch (condition.operator) {
      case "eq":
        return value === target;
      case "neq":
        return value !== target;
      case "gt":
        return typeof value === "number" && typeof target === "number" && value > target;
      case "gte":
        return typeof value === "number" && typeof target === "number" && value >= target;
      case "lt":
        return typeof value === "number" && typeof target === "number" && value < target;
      case "lte":
        return typeof value === "number" && typeof target === "number" && value <= target;
      case "contains": {
        if (typeof value !== "string" || typeof target !== "string") return false;
        const haystack = value.toLowerCase();
        const keywords = target.toLowerCase().split(/[,，、]/).map((s) => s.trim()).filter(Boolean);
        if (keywords.length === 0) return false;
        return keywords.some((kw) => haystack.includes(kw));
      }
      default:
        return false;
    }
  }

  // src/reactions/compile-rule.ts
  function compileRuleToReaction(rule) {
    return {
      id: rule.id,
      name: rule.name,
      description: rule.description,
      when: compileTriggerToPattern(rule.trigger),
      conditions: rule.conditions,
      conditionLogic: rule.conditionLogic,
      then: compileActionsToEffects(rule.actions),
      priority: rule.priority,
      cooldownTurns: rule.cooldownTurns,
      maxFireCount: rule.maxFireCount,
      chance: rule.chance,
      enabled: rule.enabled
    };
  }
  function compileTriggerToPattern(trigger) {
    switch (trigger.type) {
      case "state-change":
        return { eventType: "state:changed" };
      case "variable-crossed":
        return {
          eventType: "state:changed",
          _legacyTrigger: trigger
        };
      case "turn-count": {
        const pattern = { eventType: "turn:complete" };
        if (trigger.atTurn !== void 0) {
          pattern.match = { turnCount: { operator: "eq", value: trigger.atTurn } };
        }
        if (trigger.everyNTurns !== void 0) {
          pattern._legacyTrigger = trigger;
        }
        return pattern;
      }
      case "session-start":
        return { eventType: "session:start" };
      case "keyword":
        return {
          eventType: "message:user",
          _legacyTrigger: trigger
        };
      case "ai-keyword":
        return {
          eventType: "message:ai",
          _legacyTrigger: trigger
        };
      case "action":
        return {
          eventType: "action:fired",
          match: trigger.actionId ? { actionId: { operator: "eq", value: trigger.actionId } } : void 0
        };
      case "manual":
        return { eventType: "state:changed" };
      case "every-turn":
        return { eventType: "turn:complete" };
      default:
        return { eventType: "state:changed" };
    }
  }
  function compileActionsToEffects(actions) {
    var _a, _b, _c;
    const effects = [];
    for (const action of actions) {
      switch (action.type) {
        case "modify-variable":
          effects.push({
            type: "set",
            path: action.variableId,
            value: action.value,
            operation: action.operation,
            valueRef: action.valueRef
          });
          break;
        case "inject-directive":
          effects.push({
            type: "set",
            path: `@prompt.directive.${action.directiveId}`,
            value: {
              content: action.content,
              position: (_a = action.position) != null ? _a : "auto",
              persistent: action.persistent !== false,
              duration: action.duration
            },
            operation: "set"
          });
          break;
        case "remove-directive":
          effects.push({
            type: "set",
            path: `@prompt.directive.${action.directiveId}`,
            value: false,
            operation: "set"
          });
          break;
        case "send-context":
          effects.push({
            type: "emit",
            event: {
              type: "ai:context",
              message: action.message,
              role: (_b = action.role) != null ? _b : "system"
            }
          });
          break;
        case "toggle-entry":
          effects.push({
            type: "set",
            path: `@prompt.entry.${action.entryId}`,
            value: action.enabled,
            operation: "set"
          });
          break;
        case "toggle-rule":
          effects.push({
            type: "set",
            path: `@rules.disabled.${action.ruleId}`,
            value: !action.enabled,
            operation: "set"
          });
          break;
        case "notify-player":
          effects.push({
            type: "emit",
            event: {
              type: "ui:notification",
              message: action.message,
              style: (_c = action.style) != null ? _c : "info"
            }
          });
          break;
        case "play-audio":
          effects.push({
            type: "emit",
            event: {
              type: "audio:play",
              trackId: action.trackId,
              action: action.action,
              volume: action.volume,
              fadeDuration: action.fadeDuration
            }
          });
          break;
      }
    }
    return effects;
  }
  function buildTurnCompleteEvent(turnCount) {
    return { type: "turn:complete", turnCount };
  }
  function buildMessageUserEvent(content) {
    return { type: "message:user", content };
  }
  function buildMessageAIEvent(content) {
    return { type: "message:ai", content };
  }
  function buildSessionStartEvent() {
    return { type: "session:start" };
  }

  // src/reactions/reaction-evaluator.ts
  var EMPTY_RESULT = {
    effects: [],
    firedIds: [],
    legacyEffects: [],
    legacyAudioEffects: [],
    emittedEvents: [],
    notifications: [],
    contextMessages: []
  };
  var ReactionEvaluator = class {
    /**
     * Evaluate reactions for a given event.
     *
     * @param event - The event that occurred
     * @param reactions - Reactions to evaluate (new format)
     * @param rules - Legacy rules to evaluate (auto-compiled to reactions)
     * @param state - Current game state
     * @param chainDepth - Current chain depth (for preventing infinite loops)
     */
    evaluate(event, reactions, rules, state, chainDepth = 0) {
      var _a, _b, _c;
      const maxChain = 5;
      if (chainDepth >= maxChain) return EMPTY_RESULT;
      const compiled = rules.map(compileRuleToReaction);
      const allReactions = [...compiled, ...reactions];
      const ruleState = (_a = state.ruleState) != null ? _a : createEmptyRuleState();
      const sorted = [...allReactions].sort((a, b) => b.priority - a.priority);
      const allEffects = [];
      const firedIds = [];
      for (const reaction of sorted) {
        if (!reaction.enabled) continue;
        if (ruleState.disabledRules.includes(reaction.id)) continue;
        if (!this.matchesEvent(event, reaction.when, state)) continue;
        if (!this.checkConditions(state, reaction.conditions, reaction.conditionLogic)) continue;
        if (reaction.stopConditions && reaction.stopConditions.length > 0) {
          if (reaction.stopConditions.some((c) => this.evaluateCondition(state, c))) continue;
        }
        if (ruleState.cooldowns[reaction.id] !== void 0) {
          const turnCount = (_b = state.turnCount) != null ? _b : 0;
          if (ruleState.cooldowns[reaction.id] > turnCount) continue;
        }
        if (reaction.maxFireCount !== void 0) {
          const count = (_c = ruleState.fireCounts[reaction.id]) != null ? _c : 0;
          if (count >= reaction.maxFireCount) continue;
        }
        if (reaction.chance !== void 0 && reaction.chance < 100) {
          if (Math.random() * 100 >= reaction.chance) continue;
        }
        for (const effect of reaction.then) {
          allEffects.push(...resolveDynamicEffect(effect, state));
        }
        firedIds.push(reaction.id);
      }
      return this.classifyEffects(allEffects, firedIds);
    }
    /**
     * Evaluate reactions for multiple events at once (convenience for turn-end processing).
     * Events are processed in order; results are merged.
     */
    evaluateMultiple(events, reactions, rules, state, options) {
      const merged = {
        effects: [],
        firedIds: [],
        legacyEffects: [],
        legacyAudioEffects: [],
        emittedEvents: [],
        notifications: [],
        contextMessages: []
      };
      const fired = /* @__PURE__ */ new Set();
      for (const event of events) {
        const activeReactions = (options == null ? void 0 : options.oncePerReaction) ? reactions.filter((r) => !fired.has(r.id)) : reactions;
        const activeRules = (options == null ? void 0 : options.oncePerReaction) ? rules.filter((r) => !fired.has(r.id)) : rules;
        const result = this.evaluate(event, activeReactions, activeRules, state);
        for (const id of result.firedIds) fired.add(id);
        merged.effects.push(...result.effects);
        merged.firedIds.push(...result.firedIds);
        merged.legacyEffects.push(...result.legacyEffects);
        merged.legacyAudioEffects.push(...result.legacyAudioEffects);
        merged.emittedEvents.push(...result.emittedEvents);
        merged.notifications.push(...result.notifications);
        merged.contextMessages.push(...result.contextMessages);
      }
      return merged;
    }
    /** Check if an event matches a reaction's EventPattern, including legacy trigger support */
    matchesEvent(event, pattern, state) {
      var _a, _b, _c;
      const legacyTrigger = pattern == null ? void 0 : pattern._legacyTrigger;
      if (legacyTrigger) {
        return this.checkLegacyTrigger(event, legacyTrigger, state);
      }
      if (pattern.eventType === "state:crossed" && event.type === "state:changed") {
        return this.matchesCrossing(event, pattern);
      }
      if (pattern.eventType === "turn:complete" && event.type === "turn:complete") {
        const turnCountCond = (_a = pattern.match) == null ? void 0 : _a.turnCount;
        if (turnCountCond && turnCountCond.operator === "every") {
          const n = Number(turnCountCond.value);
          const turnCount = (_c = (_b = event.turnCount) != null ? _b : state.turnCount) != null ? _c : 0;
          if (!Number.isFinite(n) || n <= 0) return false;
          return turnCount > 0 && turnCount % n === 0;
        }
      }
      return matchesEventPattern(event, pattern);
    }
    /** Detect a threshold crossing from a state:changed event */
    matchesCrossing(event, pattern) {
      var _a, _b, _c;
      const match = pattern.match;
      if (!match) return false;
      const expectedVarId = (_a = match.variableId) == null ? void 0 : _a.value;
      if (expectedVarId !== void 0 && event.variableId !== expectedVarId) return false;
      const threshold = (_b = match.threshold) == null ? void 0 : _b.value;
      if (typeof threshold !== "number") return false;
      const oldVal = event.oldValue;
      const newVal = event.newValue;
      if (typeof oldVal !== "number" || typeof newVal !== "number") return false;
      const direction = (_c = match.direction) == null ? void 0 : _c.value;
      if (direction === "rises-above") {
        return oldVal <= threshold && newVal > threshold;
      }
      if (direction === "drops-below") {
        return oldVal >= threshold && newVal < threshold;
      }
      return oldVal <= threshold && newVal > threshold || oldVal >= threshold && newVal < threshold;
    }
    /** Legacy trigger matching — preserves full keyword/turn-count/variable-crossed behavior */
    checkLegacyTrigger(event, trigger, state) {
      var _a, _b;
      switch (trigger.type) {
        case "keyword": {
          if (event.type !== "message:user") return false;
          const text = event.content;
          if (!text || !trigger.keywords || trigger.keywords.length === 0) return false;
          return this.matchesKeywords(text, trigger);
        }
        case "ai-keyword": {
          if (event.type !== "message:ai") return false;
          const text = event.content;
          if (!text || !trigger.keywords || trigger.keywords.length === 0) return false;
          return this.matchesKeywords(text, trigger);
        }
        case "turn-count": {
          if (event.type !== "turn:complete") return false;
          const turnCount = (_b = (_a = event.turnCount) != null ? _a : state.turnCount) != null ? _b : 0;
          if (trigger.atTurn !== void 0 && turnCount === trigger.atTurn) return true;
          if (trigger.everyNTurns !== void 0 && trigger.everyNTurns > 0) {
            return turnCount > 0 && turnCount % trigger.everyNTurns === 0;
          }
          return false;
        }
        case "variable-crossed": {
          if (event.type !== "state:changed") return false;
          if (trigger.variableId && event.variableId !== trigger.variableId) return false;
          const oldVal = event.oldValue;
          const newVal = event.newValue;
          if (typeof oldVal !== "number" || typeof newVal !== "number") return false;
          if (trigger.threshold === void 0) return false;
          if (trigger.direction === "rises-above") {
            return oldVal <= trigger.threshold && newVal > trigger.threshold;
          }
          if (trigger.direction === "drops-below") {
            return oldVal >= trigger.threshold && newVal < trigger.threshold;
          }
          return false;
        }
        default:
          return false;
      }
    }
    /** Keyword matching using the lorebook keyword engine (preserves whole-word/secondary) */
    matchesKeywords(text, trigger) {
      var _a, _b, _c, _d;
      const keywords = (_a = trigger.keywords) != null ? _a : [];
      if (keywords.length === 0) return false;
      const wholeWord = (_b = trigger.matchWholeWords) != null ? _b : false;
      const primaryMatch = keywords.some(
        (kw) => keywordMatches(text, kw, wholeWord)
      );
      if (!primaryMatch) return false;
      const secondary = (_c = trigger.secondaryKeywords) != null ? _c : [];
      if (secondary.length === 0) return true;
      const logic = (_d = trigger.secondaryKeywordLogic) != null ? _d : "AND_ANY";
      const secondaryMatches = secondary.map(
        (kw) => keywordMatches(text, kw, wholeWord)
      );
      switch (logic) {
        case "AND_ANY":
          return secondaryMatches.some(Boolean);
        case "AND_ALL":
          return secondaryMatches.every(Boolean);
        case "NOT_ANY":
          return !secondaryMatches.some(Boolean);
        case "NOT_ALL":
          return !secondaryMatches.every(Boolean);
        default:
          return true;
      }
    }
    /** Check IF conditions against state (same logic as RulesEngine) */
    checkConditions(state, conditions, logic) {
      if (conditions.length === 0) return true;
      if (logic === "all") return conditions.every((c) => this.evaluateCondition(state, c));
      return conditions.some((c) => this.evaluateCondition(state, c));
    }
    evaluateCondition(state, condition) {
      const current = getByPath(state.variables, condition.variableId);
      if (current === void 0) return false;
      const target = condition.valueRef !== void 0 ? getByPath(state.variables, condition.valueRef) : condition.value;
      if (target === void 0) return false;
      switch (condition.operator) {
        case "eq":
          return current === target;
        case "neq":
          return current !== target;
        case "gt":
          return typeof current === "number" && typeof target === "number" && current > target;
        case "gte":
          return typeof current === "number" && typeof target === "number" && current >= target;
        case "lt":
          return typeof current === "number" && typeof target === "number" && current < target;
        case "lte":
          return typeof current === "number" && typeof target === "number" && current <= target;
        case "contains":
          if (Array.isArray(current)) return current.includes(target);
          return typeof current === "string" && typeof target === "string" && current.includes(target);
        default:
          return false;
      }
    }
    /** Classify effects into legacy compat buckets and emitted events */
    classifyEffects(effects, firedIds) {
      var _a, _b, _c;
      const legacyEffects = [];
      const legacyAudioEffects = [];
      const emittedEvents = [];
      const notifications = [];
      const contextMessages = [];
      for (const effect of effects) {
        if (effect.type === "set") {
          if (!effect.path.startsWith("@")) {
            legacyEffects.push({
              variableId: effect.path,
              operation: (_a = effect.operation) != null ? _a : "set",
              value: effect.value
            });
          }
        } else if (effect.type === "emit") {
          emittedEvents.push(effect.event);
          if (effect.event.type === "audio:play") {
            legacyAudioEffects.push({
              trackId: effect.event.trackId,
              action: effect.event.action,
              volume: effect.event.volume,
              fadeDuration: effect.event.fadeDuration
            });
          } else if (effect.event.type === "ui:notification") {
            notifications.push({
              message: effect.event.message,
              style: (_b = effect.event.style) != null ? _b : "info"
            });
          } else if (effect.event.type === "ai:context") {
            contextMessages.push({
              message: effect.event.message,
              role: (_c = effect.event.role) != null ? _c : "system"
            });
          }
        }
      }
      return { effects, firedIds, legacyEffects, legacyAudioEffects, emittedEvents, notifications, contextMessages };
    }
  };
  function resolveDynamicEffect(effect, state) {
    var _a, _b, _c, _d;
    if (effect.type !== "set" || !effect.valueRandom) return [effect];
    const spec = effect.valueRandom;
    if (spec.kind === "range") {
      const value = rollRange(spec.min, spec.max, spec.integer !== false);
      return [{ ...effect, value, valueRandom: void 0 }];
    }
    if (spec.kind === "dice") {
      const value = rollDice(spec.count, spec.sides, (_a = spec.modifier) != null ? _a : 0);
      return [{ ...effect, value, valueRandom: void 0 }];
    }
    let candidates;
    let weights;
    if (spec.candidatesVar) {
      const raw = getByPath(state.variables, spec.candidatesVar);
      candidates = Array.isArray(raw) ? raw.map(String).filter((s) => s.length > 0) : [];
    } else {
      candidates = ((_b = spec.candidates) != null ? _b : []).map(String).filter((s) => s.length > 0);
      weights = spec.weights;
    }
    if (candidates.length === 0) return [];
    const cooldown = (_c = spec.cooldown) != null ? _c : 0;
    let recent = [];
    if (cooldown > 0 && spec.historyVar) {
      const raw = getByPath(state.variables, spec.historyVar);
      if (Array.isArray(raw)) recent = raw.map(String);
    }
    const recentSet = new Set(recent.slice(-cooldown));
    let eligible = candidates.filter((c) => !recentSet.has(c));
    if (eligible.length === 0) {
      if (((_d = spec.onExhausted) != null ? _d : "full") === "keep") return [];
      eligible = candidates;
    }
    const haveWeights = weights !== void 0 && weights.length === candidates.length && weights.every((w) => typeof w === "number" && w >= 0);
    const eligibleWeights = haveWeights ? eligible.map((c) => weights[candidates.indexOf(c)]) : void 0;
    const picked = weightedPick(eligible, eligibleWeights);
    const out = [{ ...effect, value: picked, valueRandom: void 0 }];
    if (cooldown > 0 && spec.historyVar) {
      const nextHistory = [...recent, picked].slice(-cooldown);
      out.push({ type: "set", path: spec.historyVar, value: nextHistory, operation: "set" });
    }
    return out;
  }
  function rollRange(min, max, integer) {
    const lo = Math.min(min, max);
    const hi = Math.max(min, max);
    if (integer) return lo + Math.floor(Math.random() * (hi - lo + 1));
    return lo + Math.random() * (hi - lo);
  }
  function rollDice(count, sides, modifier) {
    let total = modifier;
    const n = Math.max(0, Math.floor(count));
    const s = Math.max(1, Math.floor(sides));
    for (let i = 0; i < n; i++) total += 1 + Math.floor(Math.random() * s);
    return total;
  }
  function weightedPick(items, weights) {
    if (items.length === 1) return items[0];
    if (weights && weights.length === items.length) {
      const total = weights.reduce((a, b) => a + (b > 0 ? b : 0), 0);
      if (total > 0) {
        let r = Math.random() * total;
        for (let i = 0; i < items.length; i++) {
          r -= weights[i] > 0 ? weights[i] : 0;
          if (r < 0) return items[i];
        }
      }
    }
    return items[Math.floor(Math.random() * items.length)];
  }

  // src/systems/effect-processor.ts
  function processSystemEffects(effects) {
    const result = {
      variableEffects: [],
      audioEffects: [],
      directives: [],
      directiveRemovals: [],
      entryToggles: [],
      ruleToggles: [],
      variableToggles: [],
      notifications: [],
      contextMessages: []
    };
    for (const effect of effects) {
      if (effect.type === "set") {
        processSetEffect(effect, result);
      } else if (effect.type === "emit") {
        processEmitEffect(effect.event, result);
      }
    }
    return result;
  }
  function processSetEffect(effect, result) {
    var _a;
    const { path, value, operation, valueRef } = effect;
    if (!path.startsWith("@")) {
      result.variableEffects.push({
        variableId: path,
        operation: operation != null ? operation : "set",
        value,
        valueRef
      });
      return;
    }
    if (path === "@audio.bgm" || path === "@audio.sfx" || path === "@audio.ambient") {
      if (typeof value === "string" && value) {
        result.audioEffects.push({ trackId: value, action: "play" });
      }
      return;
    }
    if (path === "@audio.stop") {
      if (typeof value === "string" && value) {
        result.audioEffects.push({ trackId: value, action: "stop" });
      }
      return;
    }
    if (path.startsWith("@prompt.directive.")) {
      const directiveId = path.slice("@prompt.directive.".length);
      if (!directiveId) return;
      if (value === false) {
        result.directiveRemovals.push(directiveId);
        return;
      }
      if (typeof value === "string") {
        result.directives.push({
          id: directiveId,
          content: value,
          position: "auto",
          persistent: true
        });
        return;
      }
      if (typeof value === "object" && value !== null && !Array.isArray(value)) {
        const obj = value;
        if (typeof obj.content === "string") {
          result.directives.push({
            id: directiveId,
            content: obj.content,
            position: (_a = obj.position) != null ? _a : "auto",
            persistent: obj.persistent !== false,
            duration: typeof obj.duration === "number" ? obj.duration : void 0
          });
        }
        return;
      }
      return;
    }
    if (path.startsWith("@prompt.entry.")) {
      const entryId = path.slice("@prompt.entry.".length);
      if (entryId && typeof value === "boolean") {
        result.entryToggles.push({ entryId, enabled: value });
      }
      return;
    }
    if (path === "@prompt.context") {
      if (typeof value === "string" && value) {
        result.contextMessages.push({ message: value, role: "system" });
      }
      return;
    }
    if (path.startsWith("@vars.enabled.")) {
      const variableId = path.slice("@vars.enabled.".length);
      if (variableId && typeof value === "boolean") {
        result.variableToggles.push({ variableId, enabled: value });
      }
      return;
    }
    if (path.startsWith("@rules.disabled.")) {
      const ruleId = path.slice("@rules.disabled.".length);
      if (ruleId && typeof value === "boolean") {
        result.ruleToggles.push({ ruleId, enabled: !value });
      }
      return;
    }
    if (path === "@ui.notification") {
      if (typeof value === "string" && value) {
        result.notifications.push({ message: value, style: "info" });
      }
      return;
    }
    if (path === "@ai.context") {
      if (typeof value === "string" && value) {
        result.contextMessages.push({ message: value, role: "system" });
      }
      return;
    }
  }
  function processEmitEffect(event, result) {
    var _a, _b, _c;
    switch (event.type) {
      case "audio:play":
        if (typeof event.trackId === "string") {
          result.audioEffects.push({
            trackId: event.trackId,
            action: (_a = event.action) != null ? _a : "play",
            volume: typeof event.volume === "number" ? event.volume : void 0,
            fadeDuration: typeof event.fadeDuration === "number" ? event.fadeDuration : void 0,
            chainTo: typeof event.chainTo === "string" ? event.chainTo : void 0,
            maxDuration: typeof event.maxDuration === "number" ? event.maxDuration : void 0
          });
        }
        break;
      case "ui:notification":
        if (typeof event.message === "string") {
          result.notifications.push({
            message: event.message,
            style: (_b = event.style) != null ? _b : "info"
          });
        }
        break;
      case "ai:context":
        if (typeof event.message === "string") {
          result.contextMessages.push({
            message: event.message,
            role: (_c = event.role) != null ? _c : "system"
          });
        }
        break;
    }
  }
  function applySystemEffects(stateManager, systemResult) {
    for (const directive of systemResult.directives) {
      stateManager.injectDirective({
        id: directive.id,
        content: directive.content,
        position: directive.position,
        sourceRuleId: "",
        persistent: directive.persistent,
        injectedAtTurn: stateManager.getSnapshot().turnCount,
        duration: directive.duration
      });
    }
    for (const directiveId of systemResult.directiveRemovals) {
      stateManager.removeDirective(directiveId);
    }
    for (const toggle of systemResult.entryToggles) {
      stateManager.toggleEntry(toggle.entryId, toggle.enabled);
    }
    for (const toggle of systemResult.ruleToggles) {
      stateManager.toggleRule(toggle.ruleId, toggle.enabled);
    }
    for (const toggle of systemResult.variableToggles) {
      stateManager.toggleVariable(toggle.variableId, toggle.enabled);
    }
    return stateManager.applyEffects(systemResult.variableEffects);
  }

  // src/reactions/reaction-runner.ts
  var DEFAULT_MAX_DEPTH = 5;
  function runReactionChain(evaluator, stateManager, initialEvents, reactions, rules, options) {
    var _a;
    const maxDepth = (_a = options == null ? void 0 : options.maxDepth) != null ? _a : DEFAULT_MAX_DEPTH;
    const cooldownById = /* @__PURE__ */ new Map();
    for (const r of rules) cooldownById.set(r.id, r.cooldownTurns);
    for (const r of reactions) cooldownById.set(r.id, r.cooldownTurns);
    const out = {
      changes: [],
      audioEffects: [],
      notifications: [],
      contextMessages: [],
      firedIds: []
    };
    let activeReactions = reactions;
    let activeRules = rules;
    let events = initialEvents;
    let depth = 0;
    while (events.length > 0 && depth < maxDepth && (activeReactions.length > 0 || activeRules.length > 0)) {
      const result = evaluator.evaluateMultiple(events, activeReactions, activeRules, stateManager.getSnapshot(), { oncePerReaction: true });
      if (result.firedIds.length === 0) break;
      const systemResult = processSystemEffects(result.effects);
      const changes = applySystemEffects(stateManager, systemResult);
      const firedThisHop = new Set(result.firedIds);
      const turnCount = stateManager.getSnapshot().turnCount;
      for (const id of firedThisHop) {
        stateManager.recordRuleFired(id, turnCount, cooldownById.get(id));
      }
      out.changes.push(...changes);
      out.audioEffects.push(...systemResult.audioEffects);
      out.notifications.push(...systemResult.notifications);
      out.contextMessages.push(...systemResult.contextMessages);
      out.firedIds.push(...firedThisHop);
      activeReactions = activeReactions.filter((r) => !firedThisHop.has(r.id));
      activeRules = activeRules.filter((r) => !firedThisHop.has(r.id));
      events = [
        ...result.emittedEvents,
        ...changes.map((c) => ({
          type: "state:changed",
          variableId: c.variableId,
          oldValue: c.oldValue,
          newValue: c.newValue
        }))
      ];
      depth++;
    }
    const settledChanges = stateManager.settleSystems();
    out.changes.push(...settledChanges);
    if (settledChanges.length && depth < maxDepth) {
      const settled = runReactionChain(
        evaluator,
        stateManager,
        settledChanges.map((change) => ({ type: "state:changed", ...change })),
        activeReactions,
        activeRules,
        { maxDepth: maxDepth - depth - 1 }
      );
      out.changes.push(...settled.changes);
      out.audioEffects.push(...settled.audioEffects);
      out.notifications.push(...settled.notifications);
      out.contextMessages.push(...settled.contextMessages);
      out.firedIds.push(...settled.firedIds);
    }
    return out;
  }

  // src/runtime/run-turn.ts
  var promptBuilder2 = new PromptBuilder();
  var lorebookMatcher = new LorebookMatcher();
  var responseParser = new ResponseParser();
  var structuredParser = new StructuredResponseParser();
  var reactionEvaluator = new ReactionEvaluator();
  var DEFAULT_MAX_CONTEXT = 32e3;
  var DEFAULT_MAX_TOKENS = 1024;
  var DEFAULT_TEMPERATURE = 0.8;
  function populateMacroContext(stateManager, history, model) {
    let lastMessage;
    let lastUserMessage;
    let lastCharMessage;
    let lastUserMessageAt;
    for (let i = history.length - 1; i >= 0; i--) {
      const row = history[i];
      if (!lastMessage) lastMessage = row.content;
      if (!lastUserMessage && row.role === "user") {
        lastUserMessage = row.content;
        lastUserMessageAt = row.createdAt;
      }
      if (!lastCharMessage && row.role === "assistant") lastCharMessage = row.content;
      if (lastMessage && lastUserMessage && lastCharMessage) break;
    }
    if (lastMessage) stateManager.setMetadata("lastMessage", lastMessage);
    if (lastUserMessage) stateManager.setMetadata("lastUserMessage", lastUserMessage);
    if (lastCharMessage) stateManager.setMetadata("lastCharMessage", lastCharMessage);
    if (lastUserMessageAt) stateManager.setMetadata("lastUserMessageAt", lastUserMessageAt);
    stateManager.setMetadata("model", model);
  }
  function buildTurnPrompt(input) {
    var _a, _b, _c, _d, _e, _f, _g, _h, _i, _j, _k, _l, _m, _n, _o, _p, _q, _r, _s, _t;
    const { world, history, userText } = input;
    const model = (_a = input.model) != null ? _a : "unknown";
    const maxContext = (_d = (_c = input.maxContext) != null ? _c : (_b = world.settings) == null ? void 0 : _b.maxContext) != null ? _d : DEFAULT_MAX_CONTEXT;
    const userPrompts = (_e = input.userPrompts) != null ? _e : [];
    const stateManager = new GameStateManager(world, input.state);
    stateManager.incrementTurn();
    populateMacroContext(stateManager, history, model);
    const snapshot = stateManager.getSnapshot();
    const toggled = (_g = (_f = snapshot.ruleState) == null ? void 0 : _f.toggledEntries) != null ? _g : {};
    const scanDepth = (_i = (_h = world.settings) == null ? void 0 : _h.lorebookScanDepth) != null ? _i : 2;
    const tokenBudget = maxContext;
    const recentTexts = [...history.map((m) => m.content), userText].slice(-scanDepth);
    const activeEntries = ((_j = world.worldbooks) == null ? void 0 : _j.length) ? filterEntriesByActiveWorldbooks(world.entries, world.worldbooks, snapshot) : world.entries;
    const lorebook = lorebookMatcher.matchWithBudget(
      activeEntries,
      recentTexts,
      snapshot,
      tokenBudget,
      { lorebookRecursionDepth: (_k = world.settings) == null ? void 0 : _k.lorebookRecursionDepth },
      model,
      world.loreUiBindings
    );
    const matchedEntries = [...lorebook.alwaysSend, ...lorebook.triggered];
    const context = [];
    context.push(...promptBuilder2.buildSystemMessages(world, snapshot, void 0, userPrompts, (_m = (_l = snapshot.ruleState) == null ? void 0 : _l.activeDirectives) != null ? _m : [], toggled));
    const staticFormatBlock = promptBuilder2.buildStaticFormatBlock(world);
    if (staticFormatBlock) context.push({ role: "system", content: staticFormatBlock });
    context.push(...promptBuilder2.buildExampleMessages(world, snapshot, void 0, userPrompts, toggled));
    context.push(...promptBuilder2.buildTriggeredSystemMessages(world, snapshot, lorebook.triggered, toggled));
    if (history.length > 0) context.push({ role: "system", content: "[Start a new Chat]" });
    const depthEntries = promptBuilder2.buildDepthEntries(world, snapshot, matchedEntries, userPrompts, toggled);
    const historyMessages = history.map((m) => ({ role: m.role, content: m.content }));
    historyMessages.push({ role: "user", content: userText });
    for (const de of depthEntries) {
      const insertIdx = Math.max(0, historyMessages.length - de.depth);
      historyMessages.splice(insertIdx, 0, { role: de.apiRole, content: de.content });
    }
    const historyStart = context.length;
    context.push(...historyMessages);
    const pendingContext = (_o = (_n = snapshot.metadata) == null ? void 0 : _n.pendingContext) != null ? _o : [];
    for (const ctx of pendingContext) {
      context.push({ role: (_p = ctx.role) != null ? _p : "system", content: ctx.message });
    }
    if (pendingContext.length > 0) stateManager.setMetadata("pendingContext", void 0);
    const formatBlock = promptBuilder2.buildFormatBlock(world, snapshot, input.lastTurnChanges);
    if (formatBlock) context.push({ role: "system", content: formatBlock });
    const postHistory = promptBuilder2.buildPostHistoryEntries(world, snapshot, matchedEntries, userPrompts);
    for (const entry of postHistory) context.push({ role: entry.apiRole, content: entry.content });
    const suffixCount = postHistory.length + (formatBlock ? 1 : 0);
    const messages = promptBuilder2.buildMessageHistory(context, maxContext, historyStart, suffixCount, model);
    return {
      messages,
      state: stateManager.getSnapshot(),
      matchedEntryIds: matchedEntries.map((e) => e.id),
      maxTokens: (_r = (_q = world.settings) == null ? void 0 : _q.maxTokens) != null ? _r : DEFAULT_MAX_TOKENS,
      temperature: (_t = (_s = world.settings) == null ? void 0 : _s.temperature) != null ? _t : DEFAULT_TEMPERATURE
    };
  }
  function finishTurn(input) {
    var _a, _b;
    const { world, userText } = input;
    const thinking = new ThinkingTagFilter();
    const visible = thinking.push(input.rawText) + thinking.flush();
    const parsed = structuredParser.isStructuredResponse(visible) ? structuredParser.parse(visible) : responseParser.parse(visible);
    const cleanText = parsed.cleanText;
    const stateManager = new GameStateManager(world, input.state);
    const writeFilter = filterAiEffects(world, stateManager.getSnapshot(), parsed.effects);
    const changes = stateManager.applyEffects(writeFilter.kept);
    const events = [
      buildMessageUserEvent(userText),
      buildMessageAIEvent(cleanText),
      buildTurnCompleteEvent(stateManager.getSnapshot().turnCount)
    ];
    if (stateManager.getSnapshot().turnCount === 1) events.push(buildSessionStartEvent());
    for (const change of changes) {
      events.push({ type: "state:changed", variableId: change.variableId, oldValue: change.oldValue, newValue: change.newValue });
    }
    const reactions = runReactionChain(reactionEvaluator, stateManager, events, (_a = world.reactions) != null ? _a : [], (_b = world.rules) != null ? _b : []);
    const audioEffects = [...parsed.audioEffects, ...reactions.audioEffects];
    if (audioEffects.length > 0) stateManager.setMetadata("activeAudio", audioEffects);
    if (reactions.contextMessages.length > 0) stateManager.setMetadata("pendingContext", reactions.contextMessages);
    return {
      cleanText,
      effects: writeFilter.kept,
      droppedEffects: writeFilter.dropped,
      changes: [...changes, ...reactions.changes],
      audioEffects,
      notifications: reactions.notifications,
      firedReactionIds: reactions.firedIds,
      state: stateManager.getSnapshot()
    };
  }
  async function runTurn(input) {
    const prompt = buildTurnPrompt(input);
    const rawText = await input.llm(prompt.messages, { maxTokens: prompt.maxTokens, temperature: prompt.temperature });
    const result = finishTurn({ world: input.world, state: prompt.state, userText: input.userText, rawText });
    return { ...result, prompt, rawText };
  }

  // src/validation/world-validator.ts
  function validateWorld(world) {
    var _a, _b, _c, _d, _e, _f;
    const warnings = [];
    const variableIds = new Set(world.variables.map((v) => v.id));
    const referencedVarIds = /* @__PURE__ */ new Set();
    for (const rule of world.rules) {
      for (const cond of rule.conditions) {
        if (!variableIds.has(cond.variableId)) {
          warnings.push({
            type: "rule-refs-deleted-var",
            severity: "warning",
            message: `Rule "${rule.name}" condition references non-existent variable "${cond.variableId}"`,
            entityId: rule.id,
            entityName: rule.name
          });
        } else {
          referencedVarIds.add(cond.variableId);
        }
      }
      for (const action of (_a = rule.actions) != null ? _a : []) {
        if (action.type === "modify-variable") {
          if (!variableIds.has(action.variableId)) {
            warnings.push({
              type: "rule-refs-deleted-var",
              severity: "warning",
              message: `Rule "${rule.name}" action references non-existent variable "${action.variableId}"`,
              entityId: rule.id,
              entityName: rule.name
            });
          } else {
            referencedVarIds.add(action.variableId);
          }
        }
      }
      if ((_b = rule.trigger) == null ? void 0 : _b.variableId) {
        if (!variableIds.has(rule.trigger.variableId)) {
          warnings.push({
            type: "rule-refs-deleted-var",
            severity: "warning",
            message: `Rule "${rule.name}" trigger references non-existent variable "${rule.trigger.variableId}"`,
            entityId: rule.id,
            entityName: rule.name
          });
        } else {
          referencedVarIds.add(rule.trigger.variableId);
        }
      }
    }
    for (const comp of world.components) {
      const config = comp.config;
      if (config.variableId && typeof config.variableId === "string") {
        referencedVarIds.add(config.variableId);
      }
    }
    if (world.uiBlueprint) {
      for (const trigger of (_c = world.uiBlueprint.triggers) != null ? _c : []) {
        for (const cond of (_d = trigger.conditions) != null ? _d : []) {
          if (!variableIds.has(cond.variableId)) {
            warnings.push({
              type: "orphaned-var-ref",
              severity: "warning",
              message: `uiBlueprint trigger "${trigger.id}" references non-existent variable "${cond.variableId}"`,
              entityId: trigger.id,
              entityName: (_e = trigger.name) != null ? _e : trigger.id
            });
          } else {
            referencedVarIds.add(cond.variableId);
          }
        }
      }
      for (const binding of (_f = world.uiBlueprint.bindings) != null ? _f : []) {
        const referenced = extractVarIdsFromPath(binding.path);
        for (const variableId of referenced) {
          if (!variableIds.has(variableId)) {
            warnings.push({
              type: "orphaned-var-ref",
              severity: "warning",
              message: `uiBlueprint binding "${binding.id}" references non-existent variable "${variableId}"`,
              entityId: binding.id,
              entityName: binding.id
            });
          } else {
            referencedVarIds.add(variableId);
          }
        }
      }
    }
    for (const entry of world.entries) {
      if (entry.alwaysSend && entry.keywords.length > 0 && entry.enabled) {
        warnings.push({
          type: "keywords-on-always-send",
          severity: "info",
          message: `Entry "${entry.name}" has keywords but alwaysSend is enabled \u2014 keywords are ignored`,
          entityId: entry.id,
          entityName: entry.name
        });
      }
      if (entry.enabled && !entry.content.trim()) {
        warnings.push({
          type: "empty-content",
          severity: "warning",
          message: `Entry "${entry.name}" is enabled but has empty content`,
          entityId: entry.id,
          entityName: entry.name
        });
      }
      if (entry.conditions) {
        for (const cond of entry.conditions) {
          if (!variableIds.has(cond.variableId)) {
            warnings.push({
              type: "orphaned-var-ref",
              severity: "warning",
              message: `Entry "${entry.name}" condition references non-existent variable "${cond.variableId}"`,
              entityId: entry.id,
              entityName: entry.name
            });
          } else {
            referencedVarIds.add(cond.variableId);
          }
        }
      }
    }
    for (const variable of world.variables) {
      if (!referencedVarIds.has(variable.id)) {
        warnings.push({
          type: "unused-variable",
          severity: "info",
          message: `Variable "${variable.name}" is not referenced by any rule, component, or entry condition`,
          entityId: variable.id,
          entityName: variable.name
        });
      }
    }
    return warnings;
  }
  function extractVarIdsFromPath(path) {
    const text = path.trim();
    if (!text.startsWith("vars")) return [];
    const bracket = text.match(/^vars\[['"]([^'"]+)['"]\]/);
    if (bracket == null ? void 0 : bracket[1]) return [bracket[1]];
    const dot = text.match(/^vars\.([A-Za-z0-9_-]+)/);
    if (dot == null ? void 0 : dot[1]) return [dot[1]];
    return [];
  }

  // src/migration/migrate-v1-to-v2.ts
  function migrateWorldDefinition(raw) {
    const version = raw.version ? parseFloat(raw.version) : 0;
    if (version >= 21) {
      return normalizeEntryRoles(raw);
    }
    if (version >= 20) {
      return normalizeEntryRoles(migrateV20ToV21(raw));
    }
    if (version >= 19) {
      return normalizeEntryRoles(migrateV20ToV21(migrateV19ToV20(raw)));
    }
    if (version >= 18) {
      return normalizeEntryRoles(migrateV20ToV21(migrateV19ToV20(migrateV18ToV19(raw))));
    }
    if (version >= 17) {
      return normalizeEntryRoles(migrateV20ToV21(migrateV19ToV20(migrateV18ToV19(migrateV17ToV18(raw)))));
    }
    return normalizeEntryRoles(migrateV20ToV21(migrateV19ToV20(migrateV18ToV19(migrateV17ToV18({ ...raw, version: "17.0.0" })))));
  }
  var CORE_WORLDBOOK_KEY = "\0core";
  function worldbookKey(worldbookId) {
    return worldbookId != null ? worldbookId : CORE_WORLDBOOK_KEY;
  }
  function folderScopeKey(folderId, worldbookId) {
    return `${folderId}\0${worldbookKey(worldbookId)}`;
  }
  function stableIdHash(value) {
    let hash = 2166136261;
    for (let i = 0; i < value.length; i++) {
      hash ^= value.charCodeAt(i);
      hash = Math.imul(hash, 16777619);
    }
    return (hash >>> 0).toString(36);
  }
  function derivedFolderId(baseId, scopeKey, usedIds) {
    const suffix = scopeKey === CORE_WORLDBOOK_KEY ? "core" : stableIdHash(scopeKey);
    const stem = `${baseId}--${suffix}`;
    let candidate = stem;
    let n = 2;
    while (usedIds.has(candidate)) candidate = `${stem}-${n++}`;
    usedIds.add(candidate);
    return candidate;
  }
  function migrateV20ToV21(raw) {
    var _a, _b, _c, _d, _e, _f;
    const legacyFolders = (_a = raw.entryFolders) != null ? _a : [];
    if (legacyFolders.length === 0) return { ...raw, version: "21.0.0" };
    const worldbookOrder = new Map(((_b = raw.worldbooks) != null ? _b : []).map((book, index) => [book.id, index]));
    const entriesByFolder = /* @__PURE__ */ new Map();
    for (const entry of (_c = raw.entries) != null ? _c : []) {
      if (!entry.folderId) continue;
      const list = (_d = entriesByFolder.get(entry.folderId)) != null ? _d : [];
      list.push(entry);
      entriesByFolder.set(entry.folderId, list);
    }
    const compareScope = (a, b) => {
      var _a2, _b2;
      if (a === CORE_WORLDBOOK_KEY) return b === CORE_WORLDBOOK_KEY ? 0 : -1;
      if (b === CORE_WORLDBOOK_KEY) return 1;
      const ao = (_a2 = worldbookOrder.get(a)) != null ? _a2 : Number.MAX_SAFE_INTEGER;
      const bo = (_b2 = worldbookOrder.get(b)) != null ? _b2 : Number.MAX_SAFE_INTEGER;
      return ao - bo || a.localeCompare(b);
    };
    const usedIds = new Set(legacyFolders.map((folder) => folder.id));
    const remappedIds = /* @__PURE__ */ new Map();
    const replacementsByOriginal = /* @__PURE__ */ new Map();
    const entryFolders = [];
    for (const folder of legacyFolders) {
      const referencingEntries = (_e = entriesByFolder.get(folder.id)) != null ? _e : [];
      const inferredScopes = new Set(referencingEntries.map((entry) => worldbookKey(entry.worldbookId)));
      const scopes = folder.worldbookId ? [worldbookKey(folder.worldbookId)] : inferredScopes.size > 0 ? [...inferredScopes].sort(compareScope) : [CORE_WORLDBOOK_KEY];
      const replacementIds = [];
      scopes.forEach((scope, index) => {
        const id = index === 0 ? folder.id : derivedFolderId(folder.id, scope, usedIds);
        const worldbookId = scope === CORE_WORLDBOOK_KEY ? void 0 : scope;
        remappedIds.set(folderScopeKey(folder.id, worldbookId), id);
        replacementIds.push(id);
        entryFolders.push({ ...folder, id, worldbookId });
      });
      replacementsByOriginal.set(folder.id, replacementIds);
    }
    const entries = raw.entries.map((entry) => {
      if (!entry.folderId) return entry;
      const remapped = remappedIds.get(folderScopeKey(entry.folderId, entry.worldbookId));
      return remapped && remapped !== entry.folderId ? { ...entry, folderId: remapped } : entry;
    });
    const installedBundles = (_f = raw.installedBundles) == null ? void 0 : _f.map((bundle) => {
      const folderIds = [...new Set(bundle.folderIds.flatMap((id) => {
        var _a2;
        return (_a2 = replacementsByOriginal.get(id)) != null ? _a2 : [id];
      }))];
      return folderIds.length === bundle.folderIds.length && folderIds.every((id, index) => id === bundle.folderIds[index]) ? bundle : { ...bundle, folderIds };
    });
    return {
      ...raw,
      version: "21.0.0",
      entries,
      entryFolders,
      ...installedBundles ? { installedBundles } : {}
    };
  }
  function migrateV17ToV18(raw) {
    var _a;
    const entries = raw.entries.map((entry) => {
      var _a2;
      if (entry.section === "world-info") {
        return { ...entry, section: "chat-history", depth: (_a2 = entry.depth) != null ? _a2 : 4 };
      }
      if (entry.presetId === "instructions") {
        return { ...entry, section: "system-presets", position: 2 };
      }
      if (entry.presetId === "style") {
        return { ...entry, position: 3 };
      }
      if (entry.presetId === "cot-bypass") {
        return { ...entry, position: 0 };
      }
      return entry;
    });
    const entryFolders = ((_a = raw.entryFolders) != null ? _a : []).map((folder) => {
      if (folder.section === "world-info") {
        return { ...folder, section: "chat-history" };
      }
      return folder;
    });
    return { ...raw, entries, entryFolders, version: "18.0.0" };
  }
  function migrateV18ToV19(raw) {
    var _a, _b, _c, _d, _e, _f, _g, _h, _i, _j, _k;
    if (((_a = raw.customUI) == null ? void 0 : _a.length) > 0) {
      return { ...raw, version: "19.0.0" };
    }
    const customUI = [];
    const usedIds = /* @__PURE__ */ new Set();
    const renderer = raw.messageRenderer;
    if (renderer == null ? void 0 : renderer.tsxCode) {
      const id = (_b = renderer.id) != null ? _b : "message-renderer";
      usedIds.add(id);
      customUI.push({
        id,
        name: (_c = renderer.name) != null ? _c : "Message Renderer",
        surface: "message",
        tsxCode: renderer.tsxCode,
        description: (_d = renderer.description) != null ? _d : "",
        order: 0,
        visible: true,
        updatedAt: (_e = renderer.updatedAt) != null ? _e : (/* @__PURE__ */ new Date()).toISOString()
      });
    }
    const comps = [...(_f = raw.customComponents) != null ? _f : []].sort(
      (a, b) => {
        var _a2, _b2;
        return ((_a2 = a.order) != null ? _a2 : 0) - ((_b2 = b.order) != null ? _b2 : 0);
      }
    );
    for (let i = 0; i < comps.length; i++) {
      const comp = comps[i];
      let id = comp.id;
      if (usedIds.has(id)) {
        id = `${id}-${Date.now().toString(36)}`;
      }
      usedIds.add(id);
      customUI.push({
        id,
        name: comp.name,
        surface: "app",
        tsxCode: (_g = comp.tsxCode) != null ? _g : "",
        description: (_h = comp.description) != null ? _h : "",
        order: (_i = comp.order) != null ? _i : i,
        visible: (_j = comp.visible) != null ? _j : true,
        updatedAt: (_k = comp.updatedAt) != null ? _k : (/* @__PURE__ */ new Date()).toISOString()
      });
    }
    return { ...raw, customUI, version: "19.0.0" };
  }
  function migrateV19ToV20(raw) {
    var _a, _b;
    if (raw.rootComponent) {
      return {
        ...raw,
        customUI: [],
        version: "20.0.0"
      };
    }
    const customUI = (_a = raw.customUI) != null ? _a : [];
    const now = (/* @__PURE__ */ new Date()).toISOString();
    const rootId = ((_b = raw.id) != null ? _b : "") + ":root";
    return {
      ...raw,
      rootComponent: {
        id: rootId,
        name: "Root",
        entryFile: "index.tsx",
        files: customUIToRootFiles(customUI),
        updatedAt: now
      },
      customUI: [],
      version: "20.0.0"
    };
  }
  function customUIToRootFiles(customUI) {
    var _a;
    if (customUI.length === 0) {
      return { "index.tsx": DEFAULT_CHAT_ROOT };
    }
    const visible = customUI.filter((c) => c.visible !== false);
    if (visible.length === 0) {
      return { "index.tsx": DEFAULT_CHAT_ROOT };
    }
    const messageComps = visible.filter((c) => c.surface === "message");
    const appComps = visible.filter((c) => c.surface === "app").sort((a, b) => {
      var _a2, _b;
      return ((_a2 = a.order) != null ? _a2 : 0) - ((_b = b.order) != null ? _b : 0);
    });
    const messageComp = messageComps[0];
    const files = {};
    const imports = [];
    const jsxElements = [];
    if (messageComp && (!messageComp.language || messageComp.language === "tsx")) {
      files["bubble.tsx"] = (_a = messageComp.tsxCode) != null ? _a : "";
      imports.push(`import Bubble from "./bubble";`);
      jsxElements.push(`React.createElement(Chat, { renderBubble: Bubble })`);
    }
    appComps.forEach((app, i) => {
      var _a2;
      const filename = `app-${i}.tsx`;
      files[filename] = (_a2 = app.tsxCode) != null ? _a2 : "";
      const safeName = `App${i}`;
      imports.push(`import ${safeName} from "./${filename.replace(/\.tsx$/, "")}";`);
      jsxElements.push(`React.createElement(${safeName})`);
    });
    if (jsxElements.length === 0) {
      jsxElements.push(`React.createElement(Chat)`);
    }
    files["index.tsx"] = `${imports.join("\n")}

export default function App() {
  return React.createElement(React.Fragment, null, ${jsxElements.join(", ")});
}
`;
    return files;
  }
  var DEFAULT_CHAT_ROOT = `export default function App() {
  return React.createElement(Chat);
}
`;
  function normalizeEntryRoles(raw) {
    let changed = false;
    const entries = raw.entries.map((entry) => {
      let role = entry.role;
      if (role === "personality") {
        role = "character";
      } else if (role === "lore") {
        role = "system";
      }
      const needsKeywords = !Array.isArray(entry.keywords);
      const needsEnabled = typeof entry.enabled !== "boolean";
      if (role === entry.role && !needsKeywords && !needsEnabled) return entry;
      changed = true;
      return {
        ...entry,
        role,
        ...needsKeywords ? { keywords: [] } : {},
        ...needsEnabled ? { enabled: true } : {}
      };
    });
    return changed ? { ...raw, entries } : raw;
  }

  // src/runtime/bundle-entry.ts
  var RUNTIME_VERSION = "0.1.0";
  return __toCommonJS(bundle_entry_exports);
})();
