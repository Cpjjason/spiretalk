// Uploads the screenshots for a Workshop item through the running Steam client.
// steamcmd can only set the cover; the gallery images go through ISteamUGC, so this tool does that.
//
//   WorkshopPreviews <publishedfileid> <folder> [--replace] [--note "change note"]
//   WorkshopPreviews <publishedfileid> --list        (just report what the item has)
//
// Every *.jpg / *.png in <folder> (sorted by name) is added to the item's gallery. With --replace,
// the previews already on the item are removed first. Needs the Steam client running and online,
// logged in as the item's owner. Images must be under 1 MB each.
using System.Globalization;
using Steamworks;

const uint AppIdValue = 2868840;
const long MaxPreviewBytes = 1024 * 1024;

if (args.Length < 2)
{
    Console.Error.WriteLine("usage: WorkshopPreviews <publishedfileid> <folder> [--replace] [--note \"text\"]");
    return 2;
}

var fileId = new PublishedFileId_t(ulong.Parse(args[0], CultureInfo.InvariantCulture));
var listOnly = args[1] == "--list";
var folder = listOnly ? "" : Path.GetFullPath(args[1]);
var replace = args.Contains("--replace");
var noteIdx = Array.IndexOf(args, "--note");
var note = noteIdx >= 0 && noteIdx + 1 < args.Length ? args[noteIdx + 1] : "screenshots";

var images = listOnly ? new List<string>() : Directory.GetFiles(folder)
    .Where(f => f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
    .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
    .ToList();
if (images.Count == 0 && !listOnly)
{
    Console.Error.WriteLine($"no .jpg/.png in {folder}");
    return 2;
}
foreach (var img in images)
{
    var len = new FileInfo(img).Length;
    if (len > MaxPreviewBytes)
    {
        Console.Error.WriteLine($"{Path.GetFileName(img)} is {len / 1024} KB; Steam caps preview images at 1024 KB");
        return 2;
    }
}

// Steam needs to know which app we are; the file next to the exe is how steam_api finds out.
File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "steam_appid.txt"), AppIdValue.ToString(CultureInfo.InvariantCulture));
if (!SteamAPI.Init())
{
    Console.Error.WriteLine("SteamAPI.Init failed: is the Steam client running and logged in?");
    return 1;
}
try
{
    Console.WriteLine($"Steam user {SteamFriends.GetPersonaName()} ({SteamUser.GetSteamID()}), item {fileId.m_PublishedFileId}");

    // 1. what is on the item now
    var existing = QueryPreviewCount(fileId);
    Console.WriteLine($"item currently has {existing} extra preview(s)");
    if (listOnly) return 0;

    // 2. build the update
    var handle = SteamUGC.StartItemUpdate(new AppId_t(AppIdValue), fileId);
    if (replace)
    {
        for (var i = (uint)existing; i > 0; i--)
        {
            if (!SteamUGC.RemoveItemPreview(handle, i - 1)) Console.WriteLine($"  could not queue removal of preview {i - 1}");
        }
        Console.WriteLine($"  queued removal of {existing} preview(s)");
    }
    foreach (var img in images)
    {
        if (!SteamUGC.AddItemPreviewFile(handle, img, EItemPreviewType.k_EItemPreviewType_Image))
        {
            Console.Error.WriteLine($"  AddItemPreviewFile refused {Path.GetFileName(img)}");
            return 1;
        }
        Console.WriteLine($"  + {Path.GetFileName(img)} ({new FileInfo(img).Length / 1024} KB)");
    }

    // 3. submit and wait
    var done = false;
    EResult result = EResult.k_EResultFail;
    var call = SteamUGC.SubmitItemUpdate(handle, note);
    var callResult = CallResult<SubmitItemUpdateResult_t>.Create((r, ioFailure) =>
    {
        result = ioFailure ? EResult.k_EResultIOFailure : r.m_eResult;
        done = true;
    });
    callResult.Set(call);
    var lastStatus = EItemUpdateStatus.k_EItemUpdateStatusInvalid;
    var started = DateTime.UtcNow;
    while (!done)
    {
        SteamAPI.RunCallbacks();
        var status = SteamUGC.GetItemUpdateProgress(handle, out var processed, out var total);
        if (status != lastStatus)
        {
            Console.WriteLine($"  {status}");
            lastStatus = status;
        }
        if (DateTime.UtcNow - started > TimeSpan.FromMinutes(10))
        {
            Console.Error.WriteLine("timed out waiting for Steam");
            return 1;
        }
        Thread.Sleep(200);
    }
    Console.WriteLine(result == EResult.k_EResultOK ? $"Success: {images.Count} screenshot(s) on the item." : $"Steam answered {result}");
    return result == EResult.k_EResultOK ? 0 : 1;
}
finally
{
    SteamAPI.Shutdown();
}

static int QueryPreviewCount(PublishedFileId_t fileId)
{
    var q = SteamUGC.CreateQueryUGCDetailsRequest(new[] { fileId }, 1);
    SteamUGC.SetReturnAdditionalPreviews(q, true);
    var call = SteamUGC.SendQueryUGCRequest(q);
    var done = false;
    var count = 0;
    var cr = CallResult<SteamUGCQueryCompleted_t>.Create((r, ioFailure) =>
    {
        if (!ioFailure && r.m_eResult == EResult.k_EResultOK && r.m_unNumResultsReturned > 0)
        {
            count = (int)SteamUGC.GetQueryUGCNumAdditionalPreviews(r.m_handle, 0);
            if (SteamUGC.GetQueryUGCPreviewURL(r.m_handle, 0, out var cover, 1024))
                Console.WriteLine($"  cover: {cover}");
            for (uint i = 0; i < count; i++)
            {
                if (SteamUGC.GetQueryUGCAdditionalPreview(r.m_handle, 0, i, out var url, 1024, out var name, 260, out var type))
                    Console.WriteLine($"  [{i}] {type} {name} {url}");
            }
        }
        else
        {
            Console.WriteLine($"  query failed: io={ioFailure} result={r.m_eResult}");
        }
        SteamUGC.ReleaseQueryUGCRequest(r.m_handle);
        done = true;
    });
    cr.Set(call);
    var started = DateTime.UtcNow;
    while (!done && DateTime.UtcNow - started < TimeSpan.FromSeconds(30))
    {
        SteamAPI.RunCallbacks();
        Thread.Sleep(100);
    }
    return count;
}
