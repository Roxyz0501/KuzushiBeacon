using KuzushiBeacon.Persistence;
using System.Text.Json;

static class PersistenceChecks
{
    public static int Run()
    {
        int n=0;void Check(bool value,string label){if(!value)throw new Exception(label);n++;}
        var root=Path.GetFullPath("artifacts/persistence-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        var file=Path.Combine(root,"settings.json");
        bool Valid(string s){try{return JsonDocument.Parse(s).RootElement.GetProperty("Version").GetInt32()>0;}catch{return false;}}
        var value="{\"Version\":1}";
        using(var store=new SettingsStore(file,Valid,0))
        {
            store.SetBaseline(value);
            for(int i=0;i<100;i++)store.Tick(i*60,()=>throw new Exception("Idle serialized"));
            Check(store.Writes==0,"unchanged periods write zero");
            value="{\"Version\":2}";store.MarkChanged();value="{\"Version\":1}";store.MarkChanged();store.Flush(()=>value);
            Check(store.Writes==0,"reverted edit writes zero");
            value="{\"Version\":2}";for(int i=0;i<100;i++)store.MarkChanged();
            store.Tick(5999,()=>value);Check(store.Writes==0,"no early settings write");store.Tick(6000,()=>value);store.Flush(()=>value);
            Check(store.Writes==1,"100 edits coalesced to one write");
            for(int version=3;version<=6;version++){value=$"{{\"Version\":{version}}}";store.MarkChanged();store.Flush(()=>value);}
            Check(Directory.GetFiles(root,"settings.json.backup-*").Length==3,"three previous valid generations");
            Check(store.Writes==5&&store.WrittenBytes==65,"exact payload write count and logical bytes");
            File.WriteAllText(file,"broken");
        }
        using(var restored=new SettingsStore(file,Valid,0))
        {Check(restored.Load()=="{\"Version\":5}"&&restored.Recovered,"newest valid backup restores damaged primary");
         Check(Directory.GetFiles(root,"settings.json.corrupt-*").Length==1,"damaged primary retained outside valid backups");}
        using(var healthy=new SettingsStore(file,Valid,0))
        {Check(healthy.Load()=="{\"Version\":5}"&&!healthy.Recovered&&healthy.Writes==0,"valid main never replaced by older backup");}
        using(var locked=new SettingsStore(file,Valid,0))
        {
            locked.Load();locked.MarkChanged();var before=Directory.GetFiles(root,"settings.json.backup-*").Length;
            using(var held=new FileStream(file,FileMode.Open,FileAccess.ReadWrite,FileShare.None))
                locked.Flush(()=>"{\"Version\":9}");
            Check(locked.Failures==1&&locked.Writes==0&&Directory.GetFiles(root,"settings.json.backup-*").Length==before,"locked primary does not rotate valid backups");
            locked.Retry(()=>"{\"Version\":9}");Check(locked.Writes==1,"locked file can be retried explicitly after release");
        }
        var failFile=Path.Combine(root,"fail.json");
        using(var failed=new SettingsStore(failFile,Valid,0))
        {
            int attempts=0;failed.BeforeReplace=_=>{attempts++;throw new IOException("simulated full disk / replacement failure");};
            failed.MarkChanged();
            for(int i=1;i<=8;i++){failed.Tick(i*60,()=>value);SpinWait.SpinUntil(()=>failed.Failures>=Math.Min(i,3),1000);}
            Check(attempts==3&&failed.Writes==0&&failed.Dirty,"failure stops after three spaced attempts and retains pending state");
            failed.BeforeReplace=null;failed.Retry(()=>value);Check(failed.Writes==1&&!failed.Dirty,"explicit retry recovers pending snapshot");
        }
        var concurrentFile=Path.Combine(root,"concurrent.json");
        using(var concurrent=new SettingsStore(concurrentFile,Valid,0))
        {
            using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();
            int active=0,max=0;
            concurrent.BeforeReplace=_=>{var current=Interlocked.Increment(ref active);max=Math.Max(max,current);entered.Set();release.Wait();Interlocked.Decrement(ref active);};
            value="{\"Version\":7}";concurrent.MarkChanged();concurrent.Tick(60,()=>value);Check(entered.Wait(5000),"background write started");
            value="{\"Version\":8}";concurrent.MarkChanged();concurrent.Tick(120,()=>value);release.Set();concurrent.Flush(()=>value);
            Check(max==1&&File.ReadAllText(concurrentFile)==value&&concurrent.Writes==2,"single writer preserves edit made during pending write and flushes final edit");
            concurrent.Dispose();concurrent.MarkChanged();concurrent.Tick(999,()=>throw new Exception("after disposal"));Check(concurrent.Writes==2,"shutdown leaves no scheduled writes");
        }
        var reviewRoot=Path.Combine(root,"review");var review=new ReviewStore(reviewRoot);
        var first=new Row(DateTimeOffset.Parse("2026-10-20T23:59:59Z"),"a");
        var second=new Row(DateTimeOffset.Parse("2026-10-21T00:00:01Z"),"b");
        review.Save(second,second.Started);review.Save(first,first.Started);var savedBytes=review.WrittenBytes;review.Save(first,first.Started);
        Check(review.Writes==2&&review.WrittenBytes==savedBytes,"delayed event and duplicate save do not rewrite entire history");
        Check(Directory.Exists(Path.Combine(reviewRoot,"20261020"))&&Directory.Exists(Path.Combine(reviewRoot,"20261021")),"UTC date partitions across midnight");
        Check(new ReviewStore(reviewRoot).Load<Row>().Select(x=>x.Value).SequenceEqual(new[]{"a","b"}),"all dates survive restart and remain chronologically accessible");
        var legacyRoot=Path.Combine(root,"legacy");Directory.CreateDirectory(legacyRoot);
        var legacy=Path.Combine(legacyRoot,"matches.json");var original=JsonSerializer.Serialize(new[]{first,second});File.WriteAllText(legacy,original);
        var partialRoot=Path.Combine(root,"partial");var partial=new ReviewStore(partialRoot);partial.Save(first,first.Started);
        File.WriteAllText(Path.Combine(partialRoot,"matches.json"),original);
        var resumed=new ReviewStore(partialRoot);Check(resumed.Load<Row>().Length==2&&resumed.Writes==2,"partial migration resumes without rewriting existing verified record");
        var migrated=new ReviewStore(legacyRoot);Check(migrated.Load<Row>().Length==2,"legacy migration covers every record");
        var count=migrated.Writes;migrated.Load<Row>();Check(migrated.Writes==count&&File.ReadAllText(legacy)==original,"migration restart is idempotent and source retained");
        var large=new Row(first.Started,new string('x',ReviewStore.MaxBytes));
        try{review.Save(large,large.Started);throw new Exception("oversized review accepted");}catch(InvalidDataException){n++;}
        Check(review.Load<Row>().Length==2,"oversized record cannot replace valid history");
        Console.WriteLine($"PERSISTENCE: settings 5 commits / 65 payload bytes; review {review.Writes} commits / {review.WrittenBytes} payload bytes; idle/reverted writes=0, max writers=1. Synthetic root: {root}");
        return n;
    }
    private sealed record Row(DateTimeOffset Started,string Value);
}
