using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace KuzushiBeacon.Persistence;

// UI thread supplies immutable snapshots. One writer, no game/UI callbacks from the worker.
public sealed class SettingsStore : IDisposable
{
    private readonly string path;
    private readonly Func<string, bool> valid;
    private readonly object sync = new();
    private Task writer = Task.CompletedTask;
    private string? saved, pending;
    private long revision, captured;
    private double next;
    private bool stopped;
    public int Failures { get; private set; }
    public int Writes { get; private set; }
    public long WrittenBytes { get; private set; }
    public bool Recovered { get; private set; }
    public string Error { get; private set; } = "";
    public bool Dirty => revision != captured || pending != null;
    public Action<string>? BeforeReplace { get; set; } // Fault injection; unset in production.
    public SettingsStore(string path, Func<string,bool> valid, double now)
    { this.path=path;this.valid=valid;next=now+60; }
    public string? Load()
    {
        if (TryRead(path) is {} current) { saved=current; return current; }
        IEnumerable<string> backups=Directory.Exists(Path.GetDirectoryName(path))
            ? Directory.EnumerateFiles(Path.GetDirectoryName(path)!,Path.GetFileName(path)+".backup-*").OrderDescending(StringComparer.Ordinal) : [];
        foreach(var backup in backups)
            if(TryRead(backup) is {} recovered)
            {
                Recovered=true;
                try {Commit(recovered);saved=recovered;}
                catch(Exception ex){pending=recovered;Failures=1;Error=ex.GetType().Name+": "+ex.Message;}
                return recovered;
            }
        if(File.Exists(path)) Error="No valid settings backup; damaged file preserved.";
        return null;
    }
    private string? TryRead(string file)
    {
        try { if(!File.Exists(file)||new FileInfo(file).Length>8*1024*1024)return null;
            var json=File.ReadAllText(file);return valid(json)?json:null; }
        catch(IOException){return null;} catch(UnauthorizedAccessException){return null;}
    }
    public void SetBaseline(string json) { if(pending==null)saved=json; }
    public void MarkChanged() { if(!stopped)revision++; }
    public void Tick(double now, Func<string> snapshot)
    {
        if(stopped||now<next)return;
        next=now+60;
        if(!writer.IsCompleted)return;
        Capture(snapshot);
        if(pending!=null&&Failures<3)writer=Task.Run(Attempt);
    }
    private void Capture(Func<string> snapshot)
    {
        if(revision==captured)return;
        var value=snapshot();captured=revision;
        pending=value==saved?null:value;
    }
    private void Attempt()
    {
        lock(sync)
        {
            if(pending==null||Failures>=3)return;
            try { Commit(pending);saved=pending;pending=null;Failures=0;Error=""; }
            catch(Exception ex) { Failures++;Error=ex.GetType().Name+": "+ex.Message; }
        }
    }
    public void Flush(Func<string> snapshot)
    { if(stopped)return;writer.GetAwaiter().GetResult();Capture(snapshot);Attempt(); }
    public void Retry(Func<string> snapshot) { writer.GetAwaiter().GetResult();Failures=0;Flush(snapshot); }
    private void Commit(string json)
    {
        if(!valid(json))throw new InvalidDataException("Invalid settings snapshot");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp=path+".tmp";
        var bytes=Encoding.UTF8.GetBytes(json);
        using(var stream=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)) {stream.Write(bytes);stream.Flush(true);}
        if(TryRead(temp)==null)throw new InvalidDataException("Settings verification failed");
        BeforeReplace?.Invoke(path);
        var prior=TryRead(path);
        if(File.Exists(path))
        {
            var backup=path+(prior!=null?".backup-":".corrupt-")+DateTime.UtcNow.Ticks.ToString("D19")+"-"+Guid.NewGuid().ToString("N");
            File.Replace(temp,path,backup);
        }
        else File.Move(temp,path);
        Writes++;WrittenBytes+=bytes.Length;
        // Only after successful replacement. An invalid primary is never promoted into valid generations.
        foreach(var old in Directory.EnumerateFiles(Path.GetDirectoryName(path)!,Path.GetFileName(path)+".backup-*").OrderDescending(StringComparer.Ordinal).Skip(3))
            try{File.Delete(old);}catch(IOException){}catch(UnauthorizedAccessException){}
    }
    public void Dispose(){stopped=true;writer.GetAwaiter().GetResult();}
    public static bool ValidConfiguration(string json)
    {
        try {var o=JObject.Parse(json);return o["Version"]?.Type==JTokenType.Integer && JsonConvert.DeserializeObject<Configuration>(json) is not null;}
        catch{return false;}
    }
}
