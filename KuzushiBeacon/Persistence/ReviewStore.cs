using System.Security.Cryptography;
using System.Text.Json;

namespace KuzushiBeacon.Persistence;

// Bounded, per-match records, UTC event-start partitions. Legacy source is never deleted.
public sealed class ReviewStore(string root)
{
    public const int MaxRecords=100, MaxBytes=4*1024*1024;
    private readonly object sync=new();
    public int Writes {get;private set;}
    public long WrittenBytes {get;private set;}
    public int Revision {get;private set;}
    public bool ReadError {get;private set;}
    private string[] Inventory()=>Directory.Exists(root)?Directory.EnumerateFiles(root,"*.review.json",SearchOption.AllDirectories)
        .OrderByDescending(Path.GetFileName,StringComparer.Ordinal).ToArray():[];
    private void Atomic(string path,byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using(var f=new FileStream(path+".tmp",FileMode.Create,FileAccess.Write,FileShare.None)){f.Write(bytes);f.Flush(true);}
        File.Move(path+".tmp",path,true);Writes++;WrittenBytes+=bytes.Length;Revision++;
    }
    private void Put(byte[] bytes,DateTimeOffset started)
    {
        if(bytes.Length>MaxBytes)throw new InvalidDataException("Review exceeds 4 MiB");
        // Normalize escaping identically for typed records and legacy JSON elements before hashing.
        using(var document=JsonDocument.Parse(bytes))bytes=JsonSerializer.SerializeToUtf8Bytes(document.RootElement);
        if(bytes.Length>MaxBytes)throw new InvalidDataException("Review exceeds 4 MiB");
        var id=Convert.ToHexString(SHA256.HashData(bytes));
        var file=Path.Combine(root,started.UtcDateTime.ToString("yyyyMMdd"),$"{started.UtcDateTime:yyyyMMddTHHmmssfffffff}-{id}.review.json");
        if(File.Exists(file))
        {if(!File.ReadAllBytes(file).AsSpan().SequenceEqual(bytes))throw new InvalidDataException("Review identity collision");return;}
        Atomic(file,bytes);
    }
    private void Migrate()
    {
        var legacy=Path.Combine(root,"matches.json");var marker=Path.Combine(root,"migration-v1.complete");
        if(!File.Exists(legacy)||File.Exists(marker))return;
        if(new FileInfo(legacy).Length>MaxBytes)throw new InvalidDataException("Legacy review exceeds limit; preserved");
        var bytes=File.ReadAllBytes(legacy);
        using var doc=JsonDocument.Parse(bytes);
        foreach(var row in doc.RootElement.EnumerateArray())
            Put(JsonSerializer.SerializeToUtf8Bytes(row),row.GetProperty("Started").GetDateTimeOffset());
        Atomic(marker,System.Text.Encoding.UTF8.GetBytes(Convert.ToHexString(SHA256.HashData(bytes))));
        Prune();
    }
    private void Prune()
    {
        long bytes=0;int n=0;
        foreach(var path in Inventory())
        {bytes+=new FileInfo(path).Length;if(++n>MaxRecords||bytes>MaxBytes){File.Delete(path);Revision++;}}
    }
    public T[] Load<T>()
    {
        lock(sync)
        {
            Migrate();var rows=new List<T>();long bytes=0;
            foreach(var path in Inventory().Take(MaxRecords))
            {var length=new FileInfo(path).Length;if(length>MaxBytes||bytes+length>MaxBytes)continue;bytes+=length;
                try{var row=JsonSerializer.Deserialize<T>(File.ReadAllBytes(path));if(row!=null)rows.Add(row);}
                catch(Exception ex) when(ex is IOException or JsonException){ReadError=true;}}
            return rows.AsEnumerable().Reverse().ToArray();
        }
    }
    public void Save<T>(T record,DateTimeOffset started)
    {lock(sync){Migrate();Put(JsonSerializer.SerializeToUtf8Bytes(record),started);Prune();}}
    public void Export<T>(IEnumerable<T> rows)
    {
        lock(sync)
        {
            var bytes=JsonSerializer.SerializeToUtf8Bytes(rows.TakeLast(20));
            if(bytes.Length>MaxBytes)throw new InvalidDataException("Export exceeds 4 MiB");
            Atomic(Path.Combine(root,$"anonymous-{DateTime.UtcNow:yyyyMMdd-HHmmssfffffff}-{Guid.NewGuid():N}.json"),bytes);
            foreach(var old in Directory.EnumerateFiles(root,"anonymous-*.json").OrderDescending(StringComparer.Ordinal).Skip(20))File.Delete(old);
        }
    }
}
