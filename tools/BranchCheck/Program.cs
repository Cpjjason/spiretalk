// Checks that a built SpireTalk.dll can run on a given Slay the Spire 2 install (one DLL for both Steam branches).
//
//   BranchCheck <SpireTalk.dll> <game data dir> [<BaseLib.dll>]
//
// Resolves, against that game's assemblies:
//   1. every type and member the mod references (a miss = MissingMethod/TypeLoad at runtime),
//   2. every override in the mod (a miss = the hook silently never fires),
//   3. every private member the mod reaches by name (Harmony targets, AccessTools, "____x" parameters).
// Exit code 1 when anything is missing.
using Mono.Cecil;

if (args.Length < 2)
{
    Console.Error.WriteLine("usage: BranchCheck <SpireTalk.dll> <game data dir> [<BaseLib.dll>]");
    return 2;
}

var modPath = args[0];
var gameDir = args[1];
var resolver = new DefaultAssemblyResolver();
resolver.AddSearchDirectory(gameDir);
resolver.AddSearchDirectory(Path.GetDirectoryName(Path.GetFullPath(modPath))!);
if (args.Length > 2) resolver.AddSearchDirectory(Path.GetDirectoryName(Path.GetFullPath(args[2]))!);

var mod = ModuleDefinition.ReadModule(modPath, new ReaderParameters { AssemblyResolver = resolver });
var gameAssemblies = new HashSet<string> { "sts2", "GodotSharp", "0Harmony" };
var sts2 = resolver.Resolve(AssemblyNameReference.Parse("sts2"));
var problems = new List<string>();

bool FromGame(IMetadataScope scope) => scope switch
{
    AssemblyNameReference a => gameAssemblies.Contains(a.Name),
    ModuleDefinition m => gameAssemblies.Contains(m.Assembly.Name.Name),
    _ => false,
};

// 1. references
foreach (var t in mod.GetTypeReferences())
{
    if (!FromGame(t.Scope)) continue;
    if (t.Resolve() == null) problems.Add($"missing type      {t.FullName}");
}
foreach (var m in mod.GetMemberReferences())
{
    var owner = m.DeclaringType.GetElementType();
    if (!FromGame(owner.Scope)) continue;
    object? resolved = m switch
    {
        MethodReference mr => SafeResolve(() => mr.Resolve()),
        FieldReference fr => SafeResolve(() => fr.Resolve()),
        _ => "n/a",
    };
    if (resolved == null) problems.Add($"missing member    {m.FullName}");
}

// 2. overrides of game virtuals
foreach (var type in mod.GetTypes())
{
    foreach (var method in type.Methods.Where(x => x.IsVirtual && !x.IsNewSlot && !x.IsAbstract))
    {
        var baseType = type.BaseType;
        var found = false;
        var touchesGame = false;
        while (baseType != null)
        {
            var bt = SafeResolve(() => baseType.Resolve());
            if (bt == null) break;
            if (gameAssemblies.Contains(bt.Module.Assembly.Name.Name)) touchesGame = true;
            if (bt.Methods.Any(b => b.IsVirtual && b.Name == method.Name && b.Parameters.Count == method.Parameters.Count
                                    && b.Parameters.Select(p => p.ParameterType.Name).SequenceEqual(method.Parameters.Select(p => p.ParameterType.Name))))
            {
                found = true;
                break;
            }
            baseType = bt.BaseType;
        }
        if (touchesGame && !found && method.Name != "ToString" && method.Name != "Equals" && method.Name != "GetHashCode")
            problems.Add($"override w/o base {type.FullName}::{method.Name}({string.Join(",", method.Parameters.Select(p => p.ParameterType.Name))})");
    }
}

// 3. members reached by name
var named = new List<(string Type, string Member)>();
foreach (var type in mod.GetTypes())
{
    foreach (var attr in type.CustomAttributes.Concat(type.Methods.SelectMany(m => m.CustomAttributes)))
    {
        if (attr.AttributeType.Name != "HarmonyPatch" || attr.ConstructorArguments.Count < 2) continue;
        if (attr.ConstructorArguments[0].Value is TypeReference tr && attr.ConstructorArguments[1].Value is string name)
            named.Add((tr.FullName, name));
    }
    // "____field" parameters on Harmony patch methods name private fields of the patched type
    var target = type.CustomAttributes.FirstOrDefault(a => a.AttributeType.Name == "HarmonyPatch" && a.ConstructorArguments.Count >= 1)
        ?.ConstructorArguments[0].Value as TypeReference;
    if (target != null)
        foreach (var m in type.Methods)
            foreach (var p in m.Parameters.Where(p => p.Name.StartsWith("___") ))
                named.Add((target.FullName, p.Name.TrimStart('_')));
}
// AccessTools.Field/Method(typeof(X), "name") and similar string lookups: ldtoken X; ... ldstr "name"
foreach (var type in mod.GetTypes())
foreach (var method in type.Methods.Where(m => m.HasBody))
{
    TypeReference? lastToken = null;
    foreach (var ins in method.Body.Instructions)
    {
        if (ins.OpCode.Code == Mono.Cecil.Cil.Code.Ldtoken && ins.Operand is TypeReference t) lastToken = t;
        else if (ins.OpCode.Code == Mono.Cecil.Cil.Code.Ldstr && lastToken != null && ins.Operand is string s
                 && System.Text.RegularExpressions.Regex.IsMatch(s, "^_?[A-Za-z][A-Za-z0-9_]*$") && FromGame(lastToken.Scope))
        {
            named.Add((lastToken.FullName, s));
            lastToken = null;
        }
        else if (ins.OpCode.Code is Mono.Cecil.Cil.Code.Call or Mono.Cecil.Cil.Code.Callvirt && ins.Operand is MethodReference c
                 && c.DeclaringType.Name == "Type" && c.Name == "GetTypeFromHandle") { /* keep lastToken */ }
    }
}
foreach (var (typeName, member) in named.Distinct())
{
    var td = FindType(typeName);
    if (td == null) { problems.Add($"missing type      {typeName} (by name)"); continue; }
    var ok = false;
    for (var t = td; t != null; t = SafeResolve(() => t.BaseType?.Resolve()))
    {
        if (t.Methods.Any(m => m.Name == member) || t.Fields.Any(f => f.Name == member) || t.Properties.Any(p => p.Name == member)) { ok = true; break; }
    }
    if (!ok) problems.Add($"missing by name   {typeName}::{member}");
}

var version = File.Exists(Path.Combine(gameDir, "..", "release_info.json"))
    ? File.ReadAllText(Path.Combine(gameDir, "..", "release_info.json")) : "";
var v = System.Text.RegularExpressions.Regex.Match(version, "\"version\":\\s*\"([^\"]+)\"").Groups[1].Value;
Console.WriteLine($"{Path.GetFileName(modPath)} vs game {(v == "" ? gameDir : v)}: {named.Distinct().Count()} named members, {problems.Count} problem(s)");
foreach (var p in problems.Distinct().OrderBy(x => x)) Console.WriteLine("  " + p);
return problems.Count == 0 ? 0 : 1;

TypeDefinition? FindType(string fullName)
{
    var clean = fullName.Split('<')[0].Replace('/', '+');
    foreach (var name in gameAssemblies)
    {
        var asm = SafeResolve(() => resolver.Resolve(AssemblyNameReference.Parse(name)));
        var t = asm?.MainModule.GetTypes().FirstOrDefault(x => x.FullName.Replace('/', '+') == clean);
        if (t != null) return t;
    }
    return null;
}

static T? SafeResolve<T>(Func<T?> f) where T : class
{
    try { return f(); } catch { return null; }
}
