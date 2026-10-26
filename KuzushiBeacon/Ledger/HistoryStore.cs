using System.Text;
using System.Text.Json;
using System.Security.Cryptography;

namespace KuzushiBeacon.Ledger;

// All public I/O methods are called by background workers; never by Draw.
public sealed class HistoryStore(string root)
{
    public const int PageSize = 25, MaxMatches = 1000, MaxRecordBytes = 1_048_576;
    public const long MaxBytes = 128 * 1024 * 1024;
    private readonly object gate = new();
    private readonly Dictionary<(string Owner, int Page), MatchSummary[]> pages = [];
    private readonly Queue<(string Owner, int Page)> pageOrder = [];
    private string inventoryOwner = "";
    private string[] inventory = [];
    private DateTimeOffset cachedAt;
    public int Revision { get; private set; }
    public int Reads { get; private set; }
    public int Writes { get; private set; }
    public long ReadBytes { get; private set; }
    public long WrittenBytes { get; private set; }
    public static string OwnerKey(ulong contentId) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("PvPLedger/v1/" + contentId)))[..24];
    private string OwnerPath(string owner)
    {
        if (owner.Length != 24 || owner.Any(c => !Uri.IsHexDigit(c))) throw new ArgumentException("Invalid owner key");
        return Path.Combine(root, owner);
    }
    private static string Stem(MatchSummary s) => $"{s.Started.UtcDateTime:yyyyMMddTHHmmssfff}-{s.Id:N}";
    private string RecordStem(string owner, MatchSummary summary)
    {
        var dir=OwnerPath(owner);var legacy=Path.Combine(dir,Stem(summary));
        if(File.Exists(legacy+".summary.json")||File.Exists(legacy+".match.json")||File.Exists(legacy+".delivery"))return legacy;
        return Path.Combine(dir,summary.Started.UtcDateTime.ToString("yyyyMMdd"),Stem(summary));
    }
    private static IEnumerable<string> Summaries(string dir)=>Directory.EnumerateFiles(dir,"*.summary.json",SearchOption.AllDirectories)
        .OrderByDescending(Path.GetFileName,StringComparer.Ordinal);
    private void Atomic(string path, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        using (var file = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None)) { file.Write(bytes); file.Flush(true); }
        File.Move(temp, path, true); Writes++; WrittenBytes += bytes.Length; Revision++;
    }
    private T Read<T>(string path, int limit)
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (file.Length > limit) throw new InvalidDataException("History file exceeds limit");
        Reads++; ReadBytes += file.Length;
        return JsonSerializer.Deserialize<T>(file) ?? throw new InvalidDataException("Invalid history");
    }
    private static bool ValidSummary(MatchSummary? s) => s != null && s.Id != Guid.Empty
        && s.Started != default && s.Ended != default && Enum.IsDefined(s.Mode) && Enum.IsDefined(s.Outcome)
        && s.Arena != null && s.Player != null && s.Arena.Length <= 128 && s.Player.Length <= 128
        && s.Kills >= 0 && s.Deaths >= 0 && (s.Assists == null || s.Assists >= 0);
    public void Save(string owner, MatchRecord record)
    {
        lock (gate)
        {
            var dir = OwnerPath(owner); var stem = RecordStem(owner, record.Summary);
            if (File.Exists(stem + ".summary.json")) return; // idempotent same snapshot retry.
            var bytes = JsonSerializer.SerializeToUtf8Bytes(record);
            if (bytes.Length > MaxRecordBytes) throw new InvalidDataException("Match exceeds history limit");
            Atomic(stem + ".match.json", bytes);
            Atomic(stem + ".summary.json", JsonSerializer.SerializeToUtf8Bytes(record.Summary));
            Prune(dir);
            inventoryOwner = ""; pages.Clear(); pageOrder.Clear(); Revision++;
        }
    }
    private void Prune(string dir)
    {
        var files = Summaries(dir).ToArray();
        long bytes = 0; var cutoff = DateTimeOffset.UtcNow.AddDays(-365).ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
        for (int i = 0; i < files.Length; i++)
        {
            var stem = files[i][..^13];
            var body = stem + ".match.json";
            bytes += new FileInfo(files[i]).Length + (File.Exists(body) ? new FileInfo(body).Length : 0);
            if (i < MaxMatches && bytes <= MaxBytes && string.CompareOrdinal(Path.GetFileName(files[i])[..8], cutoff) >= 0) continue;
            File.Delete(files[i]); File.Delete(body); File.Delete(stem + ".delivery");
        }
    }
    public (MatchSummary[] Rows, int Count, int Revision) Page(string owner, int page)
    {
        lock (gate)
        {
            var dir = OwnerPath(owner);
            if (inventoryOwner != owner || DateTimeOffset.UtcNow - cachedAt > TimeSpan.FromMinutes(5))
            {
                inventory = Directory.Exists(dir) ? Summaries(dir).Take(MaxMatches).ToArray() : [];
                inventoryOwner = owner; cachedAt = DateTimeOffset.UtcNow; pages.Clear(); pageOrder.Clear();
            }
            page = Math.Max(0, page); var key = (owner, page);
            if (!pages.TryGetValue(key, out var rows))
            {
                var items = new List<MatchSummary>();
                foreach (var path in inventory.Skip(page * PageSize).Take(PageSize))
                {
                    // A corrupt entry cannot prevent access to other matches.
                    try
                    {
                        var summary = Read<MatchSummary>(path, 4096);
                        if (ValidSummary(summary)) items.Add(summary);
                    }
                    catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException) { }
                }
                rows = items.ToArray();
                if (pages.Count == 4) pages.Remove(pageOrder.Dequeue());
                pages.Add(key, rows); pageOrder.Enqueue(key);
            }
            return (rows, inventory.Length, Revision);
        }
    }
    public MatchRecord Detail(string owner, MatchSummary summary)
    {
        lock (gate)
        {
            var record = Read<MatchRecord>(RecordStem(owner,summary) + ".match.json", MaxRecordBytes);
            if (record.Schema != 1 || !ValidSummary(record.Summary) || record.Summary.Id != summary.Id
                || record.Events == null || record.Events.Length > MatchSession.MaxEvents
                || record.Events.Any(e => e == null || e.Other == null || e.Other.Length > 128 || !Enum.IsDefined(e.Kind))) throw new InvalidDataException("Unsupported history");
            return record;
        }
    }
    public string Export(string owner, MatchSummary[] summaries, MatchRecord? detail)
    {
        lock (gate)
        {
            var dir = Path.Combine(OwnerPath(owner), "exports"); Directory.CreateDirectory(dir);
            // No silent overwrite and no automatic deletion of explicit exports.
            var path = Path.Combine(dir, $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.csv");
            var csv = detail == null ? CsvExport.SummaryHeader + string.Concat(summaries.Take(PageSize).Select(CsvExport.SummaryRow)) : CsvExport.Events(detail);
            using (var writer = new StreamWriter(new FileStream(path, FileMode.CreateNew, FileAccess.Write), new UTF8Encoding(true))) writer.Write(csv);
            Writes++;WrittenBytes+=new FileInfo(path).Length;Revision++;
            return path;
        }
    }
    // Claim is durably persisted BEFORE POST. Never retry unknown outcomes automatically.
    public bool ClaimDelivery(string owner, MatchSummary summary)
    {
        lock (gate)
        {
            var path = RecordStem(owner,summary) + ".delivery";
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            try
            {
                using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                file.Write("Attempted"u8); file.Flush(true); Writes++; WrittenBytes += 9; Revision++; return true;
            }
            catch (IOException) when (File.Exists(path)) { return false; }
        }
    }
    public void DeliveryResult(string owner, MatchSummary summary, string result)
    { lock (gate) Atomic(RecordStem(owner,summary) + ".delivery", Encoding.UTF8.GetBytes(result)); }
}
