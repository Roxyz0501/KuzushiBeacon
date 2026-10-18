using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace KuzushiBeacon;

public sealed partial class Plugin
{
    private readonly List<Observation> observations=[];
    private readonly List<Coverage> areaCandidates=[];
    private Observation[] attackCandidates=[];
    private Coverage currentCoverage;
    private int supportLbCount;
    private readonly Dictionary<ulong,(uint Hp,double At)> hpHistory=[];
    private readonly Dictionary<ulong,float> approachDistances=[];
    private readonly Dictionary<int,(string Text,double Until,int Priority)> alerts=[];
    private readonly AlertLatch alertLatch=new();
    private readonly LocalSound localSound=new();
    private readonly Queue<DeathState> preDeath=[];
    private readonly List<ReviewRecord> reviewHistory=[];
    private readonly Dictionary<string,long> commandCounts=[], failureCounts=[];
    private readonly Dictionary<string,(double Ms,long Calls)> costs=[];
    private double nextSupport, supportAt, muteUntil, fullSeconds, lastSupportTime, lowHpSince;
    private double nextPreDeath;
    private bool previousFull, previousOpportunity, previousDead, largeContent;
    private int previousOwn, previousNear, previousFocus, opportunities, peakCoverage;
    private HashSet<ulong> previousMarked=[];
    private ulong trackedAlly;
    private int nearbyEnemies,nearbyAllies,focusEnemies,sharedTarget;
    private float nearestAlly=float.PositiveInfinity, chiten,guardTime,resilience,selfHpRate;
    private bool fullLb,guardReady,purifyReady,healingCast;
    private List<(uint Id,float Seconds,bool Purify)> selfControls=[];
    private Vector3 allyCenter,retreatDirection;
    private Observation? focusedEnemy, currentObservation;
    private Actor? recommended;
    private int requestPhase;
    private string requestState=>requestPhase switch {1=>L("要求済み","Requested"),2=>L("LB消費確認","LB consumption observed"),_=>L("待機","Idle")};
    private DateTimeOffset? reviewStarted;
    private Task reviewWrite=Task.CompletedTask;
    private readonly List<DeathRecord> deathRecords=[];
    private string reviewStatus="";
    private long supportScans, supportReads, supportDraws;
    private bool coveragePartial;
    private bool sharedSnapshot;
    private SupportOptions S=>config.Support;
    private bool On(int id)=>S.On(id,largeContent);
    private bool NotificationsAllowed=>S.Notifications&&!S.DisabledGroups.Contains(5);
    private string L(string ja,string en)=>config.Language=="ja"?ja:en;
    private sealed record ReviewRecord(DateTimeOffset Started,DateTimeOffset Ended,string Note,Dictionary<string,float>? Settings,double? FullSeconds,int? Opportunities,
        int? Peak,Dictionary<string,long>? Commands,Dictionary<string,long>? Failures,DeathRecord[] Deaths);
    private sealed record DeathState(uint Hp,uint Mp,uint[] Controls);
    private sealed record DeathRecord(DateTimeOffset At,int? Enemies,int? Allies,double? Survival,DeathState[] States);

    private unsafe static bool Ready(uint action,ulong target)
    {var manager=ActionManager.Instance();return manager!=null&&manager->GetActionStatus(ActionType.Action,action,target)==0;}

    private void SupportUpdate(double now)
    {
        if(!S.NeedsMonitor){if(supportAt!=0||sharedSnapshot)ClearSupport(false);return;}
        if(now<nextSupport)return;
        var watch=Stopwatch.StartNew();
        var player=objects.LocalPlayer;
        if(player is null || !player.IsValid()){ClearSupport();return;}
        if(reviewStarted==null&&On(86)&&duty.IsDutyStarted&&!reviewCompleted)reviewStarted=DateTimeOffset.UtcNow;
        var elapsed=lastSupportTime==0?0:Math.Clamp(now-lastSupportTime,0,1);
        lastSupportTime=now;
        var legacy=player.ClassJob.RowId==Rules.SamuraiJob&&(OwnHighlightsEnabled||CountHighlightEnabled);
        nextSupport=now+S.SnapshotInterval(legacy,config.ScanIntervalMs)/1000d;
        var anyStatus=S.NeedsSelfStatuses;
        var needOthers=S.NeedsOtherActors(legacy);
        var needRates=On(27)||On(28)||On(47);
        observations.Clear();areaCandidates.Clear(); selfControls.Clear();
        chiten=guardTime=resilience=0;
        var wasHp=hpHistory.GetValueOrDefault(player.GameObjectId);
        selfHpRate=wasHp.At>0&&now>wasHp.At?(float)((player.CurrentHp-(double)wasHp.Hp)/(now-wasHp.At)/(Math.Max(1,player.MaxHp))*100):0;
        if(needRates)hpHistory[player.GameObjectId]=(player.CurrentHp,now);
        if(anyStatus)
            foreach(var status in player.StatusList)
            {
                if(status.RemainingTime<=0)continue;
                if(status.StatusId==1240)chiten=status.RemainingTime;
                if(StatusRules.Guard(status.StatusId))guardTime=status.RemainingTime;
                if(status.StatusId==3248)resilience=status.RemainingTime;
                if(StatusRules.Control(status.StatusId))selfControls.Add((status.StatusId,status.RemainingTime,StatusRules.Purifiable(status.StatusId)));
            }
        var readinessStart=watch.Elapsed.TotalMilliseconds;
        var needLb=CountHighlightEnabled||S.KuzushiDisplay&&S.KuzushiFullLbOnly||Enumerable.Range(39,7).Any(On)||On(87)||On(88)||On(89);
        fullLb=needLb&&GameZantetsukenAction.IsLimitBreakFull();
        guardReady=On(50)&&Ready(29054,player.GameObjectId);
        purifyReady=On(52)&&Ready(29056,player.GameObjectId);
        healingCast=On(59)&&player.IsCasting&&player.CastActionId==29055;
        RecordCost("39/50/52 LB + readiness",watch.Elapsed.TotalMilliseconds-readinessStart);
        int visited=0; bool approachingEnemy=false; nearbyEnemies=nearbyAllies=focusEnemies=sharedTarget=0;nearestAlly=float.PositiveInfinity;allyCenter=Vector3.Zero;
        var needsEnemyStatus=S.NeedsEnemyStatuses(legacy);
        sharedSnapshot=legacy;
        if(needOthers)
        foreach(var entry in objects.PlayerObjects)
        {
            if(visited++ >= (On(97)?S.MaxActors:Tracker.MaxCandidates))break;
            if(entry is not IPlayerCharacter p || !p.IsValid() || p.EntityId==player.EntityId||p.IsDead||p.CurrentHp==0||!p.IsTargetable)continue;
            if(p.GameObjectId!=trackedAlly&&!LowHpTarget.InRange(player.Position,player.HitboxRadius,p.Position,p.HitboxRadius,Math.Max(35,Math.Max(config.MaxDistance,S.NearbyRange))))continue;
            bool enemy=GameLowHpTarget.Enemy(p),ally=!enemy&&GameLowHpTarget.Ally(p);
            if(!enemy&&!ally)continue;
            float own=0,other=0;bool guard=false,invulnerable=false;
            if(enemy&&needsEnemyStatus)
                foreach(var st in p.StatusList)
                {
                    if(st.RemainingTime<=0||!float.IsFinite(st.RemainingTime))continue;
                    if(st.StatusId==Rules.KuzushiStatus){if(st.SourceId==player.EntityId)own=Math.Max(own,st.RemainingTime);else if(Rules.ValidId(st.SourceId))other=Math.Max(other,st.RemainingTime);}
                    guard|=StatusRules.Guard(st.StatusId);invulnerable|=StatusRules.Invulnerable(st.StatusId);
                }
            var old=hpHistory.GetValueOrDefault(p.GameObjectId);
            var rate=needRates&&old.At>0&&now>old.At?(float)((p.CurrentHp-(double)old.Hp)/(now-old.At)):0;
            if(needRates)hpHistory[p.GameObjectId]=(p.CurrentHp,now);
            var item=new Observation(new(p.ObjectIndex,p.EntityId,p.GameObjectId,p.Position,true,true),p.CurrentHp,p.MaxHp,p.HitboxRadius,enemy,ally,
                p.TargetObjectId,own,other,guard,invulnerable,rate,rate>0);
            observations.Add(item);
            var distance=item.Distance(player.Position,player.HitboxRadius);
            if(On(59)&&enemy)
            {
                var previous=approachDistances.GetValueOrDefault(p.GameObjectId,float.PositiveInfinity);
                approachingEnemy|=distance<=10&&distance+.2f<previous;
                approachDistances[p.GameObjectId]=distance;
            }
            if(enemy){if(distance<=S.NearbyRange)nearbyEnemies++;if(p.TargetObjectId==player.GameObjectId)focusEnemies++;}
            if(ally)
            {
                nearestAlly=Math.Min(nearestAlly,distance);
                if(distance<=S.NearbyRange){nearbyAllies++;allyCenter+=p.Position;}
                if(p.TargetObjectId!=0&&p.TargetObjectId==targets.Target?.GameObjectId)sharedTarget++;
            }
        }
        if(nearbyAllies>0)allyCenter/=nearbyAllies;
        // Bounded history: remove actors not observed this tick. No names/IDs persist to disk.
        foreach(var key in hpHistory.Keys.Where(k=>k!=player.GameObjectId&&!observations.Any(o=>o.Actor.ObjectId==k)).ToArray())hpHistory.Remove(key);
        foreach(var key in approachDistances.Keys.Where(k=>!On(59)||!observations.Any(o=>o.Actor.ObjectId==k)).ToArray())approachDistances.Remove(key);
        currentObservation=observations.Where(o=>o.Actor.ObjectId==targets.Target?.GameObjectId).Select(o=>(Observation?)o).FirstOrDefault();
        currentCoverage=currentObservation is {} current&&(On(20)||On(21)||On(23))?SupportMath.Count(current,observations,S.AreaRadius,S.LowHpPercent):default;
        var attackRange=SupportOptions.JobRange(player.ClassJob.RowId,S.MeleeRange,S.RangedRange);
        attackCandidates=On(29)?observations.Where(o=>o.Enemy&&o.Eligible(S.ExcludeGuard,S.ExcludeInvulnerable)&&o.Distance(player.Position,player.HitboxRadius)<=attackRange).OrderBy(o=>o.Hp).Take(3).ToArray():[];
        var focusStart=watch.Elapsed.TotalMilliseconds;
        focusedEnemy=On(74)?observations.Where(o=>o.Enemy).OrderByDescending(o=>observations.Count(a=>a.Ally&&a.Target==o.Actor.ObjectId)).ThenBy(o=>o.Actor.EntityId).Select(o=>(Observation?)o).FirstOrDefault():null;
        if(focusedEnemy is {} focused&&!observations.Any(a=>a.Ally&&a.Target==focused.Actor.ObjectId))focusedEnemy=null;
        RecordCost("74 ally focus",watch.Elapsed.TotalMilliseconds-focusStart);
        var retreatStart=watch.Elapsed.TotalMilliseconds;
        retreatDirection=On(75)&&nearbyEnemies>0?SupportMath.Retreat(player.Position,observations,S.NearbyRange):Vector3.Zero;
        RecordCost("75 retreat direction",watch.Elapsed.TotalMilliseconds-retreatStart);
        RecordCost("snapshot",watch.Elapsed.TotalMilliseconds);
        var geometryStart=watch.Elapsed.TotalMilliseconds;coveragePartial=false;
        if(Enumerable.Range(20,5).Any(On))
        {
            var range=SupportOptions.JobRange(player.ClassJob.RowId,S.MeleeRange,S.RangedRange);
            foreach(var center in observations)
            {
                if(!center.Enemy||!center.Eligible(S.ExcludeGuard,S.ExcludeInvulnerable)||center.Distance(player.Position,player.HitboxRadius)>range)continue;
                areaCandidates.Add(SupportMath.Count(center,observations,S.AreaRadius,S.LowHpPercent));
                if(On(97)&&watch.Elapsed.TotalMilliseconds-geometryStart>=S.BudgetMs){coveragePartial=true;break;}
            }
            areaCandidates.Sort((a,b)=>b.Count!=a.Count?b.Count.CompareTo(a.Count):a.Center.Actor.EntityId.CompareTo(b.Center.Actor.EntityId));
        }
        RecordCost("area",watch.Elapsed.TotalMilliseconds-geometryStart);
        var marked=observations.Where(o=>o.Enemy&&o.OwnKuzushi>0&&o.Eligible(S.ZantetsukenExcludeGuard,S.ZantetsukenExcludeInvulnerable)).ToArray();
        var opportunity=fullLb&&player.ClassJob.RowId==Rules.SamuraiJob&&marked.Length>0;
        recommended=null;supportLbCount=0;
        var lbStart=watch.Elapsed.TotalMilliseconds;
        if(opportunity&&(CountHighlightEnabled||On(41)&&(S.BestMarker||S.BestLine)||On(45)||On(89)))
        {
            var candidates=marked.Where(o=>o.Eligible(S.ZantetsukenExcludeGuard,S.ZantetsukenExcludeInvulnerable)).Select(o=>o.Actor).ToArray();
            var best=Zantetsuken.FindBest(candidates,player.Position,targets.Target?.GameObjectId??0,zantetsukenAction.CanReach);
            recommended=best.Target;supportLbCount=best.Count;if(On(89))peakCoverage=Math.Max(peakCoverage,best.Count);
        }
        RecordCost("41/45/89 LB coverage",watch.Elapsed.TotalMilliseconds-lbStart);
        if(On(87)&&previousFull&&fullLb)fullSeconds+=elapsed;
        if(On(88)&&opportunity&&!previousOpportunity)opportunities++;
        var hpPercent=player.MaxHp==0?100:100f*player.CurrentHp/player.MaxHp;
        var low=hpPercent<=S.LowHpPercent&&!player.IsDead;
        if(On(93)&&!low&&!player.IsDead)lowHpSince=0;
        Alert(24,areaCandidates.Count>0&&areaCandidates[0].Count>=S.AlertCount,now,2);
        Alert(33,marked.Any(o=>!previousMarked.Contains(o.Actor.ObjectId)),now,2,false);
        Alert(35,previousOwn>0&&marked.Length==0,now,1,false);
        Alert(39,fullLb,now,2);Alert(40,opportunity,now,3);
        Alert(46,low,now,10);Alert(47,!player.IsDead&&selfHpRate<=-S.RapidLossPercent,now,9);
        if(On(93)&&On(46)&&NotificationsAllowed&&low&&alerts.ContainsKey(46)&&lowHpSince==0)lowHpSince=now;
        if(!On(93))lowHpSince=0;
        Alert(48,!player.IsDead&&player.CurrentMp<S.LowMp,now,5);
        Alert(57,focusEnemies>=S.FocusCount,now,8);
        Alert(58,nearbyEnemies-previousNear>=S.NearbyIncrease,now,7,false);
        Alert(59,healingCast&&approachingEnemy,now,8,false);
        Alert(63,nearbyEnemies-nearbyAllies-1>=S.Disadvantage,now,6);
        Alert(65,nearbyEnemies>0&&nearbyAllies==0,now,7);
        var tracked=observations.Where(o=>o.Ally&&o.Actor.ObjectId==trackedAlly).Select(o=>(Observation?)o).FirstOrDefault();
        Alert(68,tracked is {} t&&t.Distance(player.Position,player.HitboxRadius)>S.Separation,now,4);
        if(On(60)&&!player.IsDead&&now>=nextPreDeath)
        {nextPreDeath=now+.5;preDeath.Enqueue(new(player.CurrentHp,player.CurrentMp,selfControls.Select(c=>c.Id).ToArray()));while(preDeath.Count>10)preDeath.Dequeue();}
        if(player.IsDead&&!previousDead&&(On(60)||On(92)||On(93)))
        {
            deathRecords.Add(new(DateTimeOffset.UtcNow,On(92)?nearbyEnemies:null,On(92)?nearbyAllies:null,
                On(93)&&lowHpSince>0?now-lowHpSince:null,On(60)?preDeath.ToArray():[]));
            if(deathRecords.Count>50)deathRecords.RemoveAt(0);preDeath.Clear();lowHpSince=0;
        }
        previousDead=player.IsDead;previousFull=fullLb;previousOpportunity=opportunity;previousOwn=marked.Length;
        previousMarked=marked.Select(o=>o.Actor.ObjectId).ToHashSet();previousNear=nearbyEnemies;previousFocus=focusEnemies;

        supportAt=now;supportScans++;supportReads+=visited;RecordCost("total",watch.Elapsed.TotalMilliseconds);
    }
    private void RecordCost(string name,double ms)
    {if(!On(100))return;var prev=costs.GetValueOrDefault(name);costs[name]=(ms,prev.Calls+1);}
    private void Alert(int id,bool condition,double now,int priority,bool stable=true)
    {
        if(!On(id)||!NotificationsAllowed){alertLatch.Observe(id,false,now,0);alerts.Remove(id);return;}
        if(!condition&&stable&&On(26))alerts.Remove(id);
        if(!stable)alertLatch.Observe(id,false,now,0);
        if(!alertLatch.Observe(id,condition,now,stable&&On(25)?S.StableSeconds:0))return;
        alerts[id]=(FeatureCatalog.Name(id,config.Language),now+3,priority);
        // Select the audible notification after every condition has been evaluated.
    }
    private double lastSoundAlert;
    private void PlayAlerts(double now)
    {
        if(!NotificationsAllowed||!S.Sound||now<muteUntil)return;
        var candidates=alerts.Where(a=>On(a.Key)&&a.Value.Until>now&&a.Value.Until>lastSoundAlert);
        var selected=On(79)?candidates.OrderByDescending(a=>a.Value.Priority).FirstOrDefault():candidates.OrderBy(a=>a.Key).FirstOrDefault();
        if(selected.Value.Until<=0)return;
        lastSoundAlert=alerts.Values.Max(a=>a.Until);
        var tone=On(80)?selected.Key is >=33 and <=35?S.KuzushiTone:selected.Key is 39 or 40?S.LbTone:S.DangerTone:0;
        localSound.Play(now,S.Volume,tone);
    }
    private void ClearSupport(bool clearTracking=true)
    {
        observations.Clear();areaCandidates.Clear();hpHistory.Clear();approachDistances.Clear();alerts.Clear();alertLatch.Clear();previousMarked.Clear();
        previousFull=previousOpportunity=previousDead=false;previousOwn=previousNear=previousFocus=0;
        supportAt=nextSupport=lastSupportTime=lowHpSince=0;recommended=null;if(clearTracking)trackedAlly=0;sharedSnapshot=false;supportLbCount=0;attackCandidates=[];
        localSound.Stop();lastSoundAlert=0;currentObservation=null;currentCoverage=default;coveragePartial=false;selfControls.Clear();
    }
    private bool reviewCompleted;
    private void SupportDutyStarted(Dalamud.Game.DutyState.IDutyStateEventArgs _)
    { FinishReview(); reviewCompleted=false; Reconcile(); if(config.Enabled&&client.IsPvPExcludingDen&&On(86))reviewStarted=DateTimeOffset.UtcNow; }
    private void SupportDutyEnded(Dalamud.Game.DutyState.IDutyStateEventArgs _)
    { FinishReview(); reviewCompleted=true; }
    private void FinishReview()
    {
        if(config.Enabled&&On(86)&&reviewStarted is {} started)
        {
            var record=new ReviewRecord(started,DateTimeOffset.UtcNow,On(94)?S.MatchNote:"",On(94)?new Dictionary<string,float>{{"MeleeRange",S.MeleeRange},{"RangedRange",S.RangedRange},{"AreaRadius",S.AreaRadius},{"LowHpPercent",S.LowHpPercent},{"AlertCount",S.AlertCount}}:null,On(87)?fullSeconds:null,
                On(88)?opportunities:null,On(89)?peakCoverage:null,On(90)?new(commandCounts):null,On(91)?new(failureCounts):null,deathRecords.ToArray());
            reviewHistory.Add(record);if(reviewHistory.Count>100)reviewHistory.RemoveAt(0);
            QueueReviewWrite(false);
        }
        reviewStarted=null;fullSeconds=0;opportunities=peakCoverage=0;commandCounts.Clear();failureCounts.Clear();deathRecords.Clear();preDeath.Clear();
    }
    private void QueueReviewWrite(bool export)
    {
        var snapshot=(export?reviewHistory.TakeLast(20):reviewHistory).ToArray();var dir=Path.Combine(pi.GetPluginConfigDirectory(),"support-review");
        var file=export?$"anonymous-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}.json":"matches.json";
        reviewWrite=reviewWrite.ContinueWith(_=>
        {
            try
            {
                Directory.CreateDirectory(dir);var path=Path.Combine(dir,file);var temp=path+".tmp";
                var rows=snapshot;var json=JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true});
                while(System.Text.Encoding.UTF8.GetByteCount(json)>4*1024*1024&&rows.Length>1)
                {rows=rows[1..];json=JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true});}
                File.WriteAllText(temp,json);File.Move(temp,path,true);
                if(export)foreach(var old in Directory.EnumerateFiles(dir,"anonymous-*.json").OrderByDescending(File.GetLastWriteTimeUtc).Skip(20))File.Delete(old);
            }
            catch(Exception ex){log.Error(ex,"Could not save anonymous review; no automatic retry.");}
        },TaskScheduler.Default);
        reviewStatus=export?L("匿名集計の保存を予約しました","Anonymous export queued"):"";
    }
    private void CountCommand(string name,bool success,string reason)
    {
        if(On(90))commandCounts[name]=commandCounts.GetValueOrDefault(name)+1;
        if(!success&&On(91))failureCounts[reason]=failureCounts.GetValueOrDefault(reason)+1;
    }
}
