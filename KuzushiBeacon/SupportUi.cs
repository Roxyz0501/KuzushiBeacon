using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Enums;

namespace KuzushiBeacon;

public sealed partial class Plugin
{
    private readonly List<Vector2> occupiedLabels=[];
    private int selectedSettingsPage;
    private void DrawSettingsNavigation()
    {
        var available=ImGui.GetContentRegionAvail().X;
        var width=Math.Clamp(available*.23f,150,230);
        if(ImGui.BeginChild("SettingsNavigation",new Vector2(width,0),true))
        {
            void Item(int page,string title)
            {
                if(ImGui.Selectable(title+"###page"+page,selectedSettingsPage==page,ImGuiSelectableFlags.None,new Vector2(0,26)))selectedSettingsPage=page;
            }
            for(int group=0;group<8;group++)
                Item(group,group==2?L("ジョブ設定","Job settings"):FeatureCatalog.Group(group,config.Language));
            Item(9,L("コマンド一覧・コピー","Commands / copy"));
            Item(8,L("戦績・キルログ","KDA / kill feed"));

        }
        ImGui.EndChild();ImGui.SameLine();
        if(ImGui.BeginChild("SettingsPage",Vector2.Zero))
        {
            if(selectedSettingsPage==2)
            {
                ImGui.TextUnformatted(L("ジョブ設定","Job settings"));
                if(ImGui.BeginTabBar("JobSettingsTabs"))
                {
                    if(ImGui.BeginTabItem(L("侍","Samurai")+"###Samurai"))
                    {DrawFeatureGroup(2);ImGui.EndTabItem();}
                    ImGui.EndTabBar();
                }
            }
            else if(selectedSettingsPage<8)DrawFeatureGroup(selectedSettingsPage);
            else if(selectedSettingsPage==8)DrawLedgerSettings();
            else DrawCommandReference();
        }
        ImGui.EndChild();
    }
    private void DrawCommandReference()
    {
        ImGui.TextWrapped(L("各ボタンでコマンドだけをコピーします。コピーでは実行しません。射程・除外条件は対象選択、斬鉄剣はジョブ設定、ミュート時間は通知・表示で変更できます。敵選択は近接・タンク3m、遠隔・ヒーラー20mが上限です。", "Each button copies a command without executing it. Configure ranges and filters in Targeting, LB in Job settings, and mute duration in Alerts. Enemy selection is capped at 3m for melee/tanks and 20m for ranged/healers."));
        ImGui.Separator();
        CopyCommand(L("設定","Settings"),"/pvpsupporter");
        CopyCommand(L("旧設定コマンド","Legacy settings"),"/kuzushibeacon");
        ImGui.Separator();
        CopyCommand(L("最低HP：ジョブ別","Lowest HP: job range"),"/pvplowhp");
        CopyCommand(L("最低HP：自動","Lowest HP: auto"),"/pvplowhp auto");
        CopyCommand(L("最低HP：3m","Lowest HP: 3m"),"/pvplowhp3");
        CopyCommand(L("最低HP：5m","Lowest HP: 5m"),"/pvplowhp5");
        CopyCommand(L("最低HP：20m","Lowest HP: 20m"),"/pvplowhp20");
        CopyCommand(L("最低HP：射程指定例","Lowest HP: custom range"),"/pvplowhp 5");
        CopyCommand(L("最寄りの敵","Nearest enemy"),"/pvpnearest");
        CopyCommand(L("最寄り：射程指定例","Nearest: custom range"),"/pvpnearest 20");
        ImGui.Separator();
        CopyCommand(L("最低HPの味方","Lowest HP ally"),"/pvpally");
        CopyCommand(L("味方：射程指定例","Ally: custom range"),"/pvpally 20");
        CopyCommand(L("選択中の味方を追跡","Track selected ally"),"/pvptrack");
        CopyCommand(L("味方の追跡を解除","Clear tracked ally"),"/pvptrack clear");
        CopyCommand(L("追跡味方の対象を選択","Assist tracked ally"),"/pvpassist");
        ImGui.Separator();
        CopyCommand(L("侍：最適対象を選択","Samurai: select best target"),"/pvpztarget");
        CopyCommand(L("侍：対象選択と斬鉄剣","Samurai: select and use LB"),"/kuzushizantetsuken");
        ImGui.Separator();
        CopyCommand(L("通知音を一時ミュート","Mute notifications"),"/pvpmute");
        CopyCommand(L("通知音を60秒ミュート","Mute for 60 seconds"),"/pvpmute 60");
        CopyCommand(L("ミュート解除","Unmute"),"/pvpmute 0");
    }
    private void CopyCommand(string title,string command)
    {
        ImGui.PushID(command);
        if(ImGui.Button(L("コピー","Copy")))ImGui.SetClipboardText(command);
        ImGui.SameLine();ImGui.TextUnformatted(command);
        ImGui.TextWrapped(title);
        ImGui.PopID();
    }
    private void DrawLedgerSettings()
    {
        var enabled=S.LedgerEnabled;
        if(ImGui.Checkbox(L("戦績記録を有効にする","Enable match ledger"),ref enabled))
        {S.LedgerEnabled=enabled;MarkDirty();ReconcileLedger();}
        if(ledger!=null)ledger.DrawEmbedded(config.Language);
        else ImGui.TextWrapped(L("PvP Ledgerの戦績・キルログ・履歴・CSV・任意のDiscord通知を統合。旧プラグインと同時に有効にすると記録が重複します。","Integrated PvP Ledger: KDA, kill feed, history, CSV and optional Discord. Disable the separate Ledger plugin to avoid duplicate recording."));
    }
    private void DrawFeatureGroup(int group)
    {
        bool changed=false;
        var enabled=!S.DisabledGroups.Contains(group);
        if(ImGui.Checkbox(L("このジャンルを有効にする","Enable this category"),ref enabled))
        {if(enabled)S.DisabledGroups.Remove(group);else S.DisabledGroups.Add(group);changed=true;}
        if(ImGui.Button(L("このジャンルを全てオン","Enable all in category")))
        {foreach(var f in FeatureCatalog.All.Where(f=>f.Group==group&&f.Id!=99))S.Features.Add(f.Id);changed=true;}
        ImGui.SameLine();
        if(ImGui.Button(L("このジャンルを全てオフ","Disable all in category")))
        {foreach(var f in FeatureCatalog.All.Where(f=>f.Group==group))S.Features.Remove(f.Id);changed=true;}
        foreach(var f in FeatureCatalog.All.Where(f=>f.Group==group))
        {
            var value=S.Features.Contains(f.Id);
            if(ImGui.Checkbox($"{f.Id}. {FeatureCatalog.Name(f.Id,config.Language)}###feature{f.Id}",ref value))
            {if(value)S.Features.Add(f.Id);else S.Features.Remove(f.Id);changed=true;}
        }
        ImGui.Separator();
        switch(group)
        {
            case 0:
                ImGui.TextWrapped("/pvplowhp [auto|3|5|20]  /pvplowhp3  /pvplowhp5  /pvplowhp20\n/pvpnearest [auto|3|5|20]\n/pvpally [20]  /pvptrack  /pvpassist  /pvpztarget");
                changed|=ImGui.SliderFloat(L("近接・タンクの射程","Melee / tank range"),ref S.MeleeRange,1,3,"%.1f m");
                changed|=ImGui.SliderFloat(L("遠隔・ヒーラーの射程","Ranged / healer range"),ref S.RangedRange,1,20,"%.1f m");
                changed|=ImGui.SliderFloat(L("味方選択の射程","Ally selection range"),ref S.AllyRange,1,20,"%.1f m");
                changed|=ImGui.Checkbox(L("通常選択：防御を除外","Targeting: exclude Guard"),ref S.ExcludeGuard);
                changed|=ImGui.Checkbox(L("通常選択：既知の無敵を除外","Targeting: exclude known invulnerability"),ref S.ExcludeInvulnerable);
                ImGui.TextWrapped(L("最低HPは残りHP実数で比較。autoまたは引数なしでジョブに合わせます。画面内限定は投影位置で判定し、壁の遮蔽判定とは異なります。","Lowest HP compares absolute HP. No argument or auto uses your job range. On-screen checks projection, not wall occlusion."));break;
            case 1:
                changed|=ImGui.SliderFloat(L("範囲の半径","Area radius"),ref S.AreaRadius,1,30,"%.1f m");
                changed|=ImGui.SliderFloat(L("低HPの判定（%）","Low HP threshold (%)"),ref S.LowHpPercent,1,99);
                changed|=ImGui.SliderInt(L("通知する巻き込み人数","Coverage alert count"),ref S.AlertCount,1,20);
                ImGui.TextWrapped(L("一般範囲の計算はこのジャンルの人数機能を有効にした場合だけ。斬鉄剣の計算はLB満タン時のみです。","General area analysis runs only for enabled count features. Zantetsuken geometry runs only with full LB."));break;
            case 2:
                changed|=ImGui.Checkbox(L("斬鉄剣：防御を除外（通常はオフ）","Zantetsuken: exclude Guard (normally off)"),ref S.ZantetsukenExcludeGuard);
                changed|=ImGui.Checkbox(L("斬鉄剣：既知の無敵を除外","Zantetsuken: exclude known invulnerability"),ref S.ZantetsukenExcludeInvulnerable);
                changed|=ImGui.SliderInt(L("斬鉄剣の最低人数","Minimum Zantetsuken targets"),ref S.MinimumZantetsuken,1,20);
                changed|=ImGui.SliderFloat(L("崩し期限の警告秒数","Expiring Kuzushi seconds"),ref S.ExpiringSeconds,.1f,5);
                DrawLegacy(2);break;
            case 3:
                changed|=ImGui.SliderFloat(L("低HPの判定（%）","Low HP threshold (%)"),ref S.LowHpPercent,1,99);
                changed|=ImGui.SliderFloat(L("急減の判定（%/秒）","Rapid loss (%/s)"),ref S.RapidLossPercent,1,100);
                changed|=ImGui.SliderFloat(L("低MPの判定","Low MP threshold"),ref S.LowMp,0,10000,"%.0f");
                changed|=ImGui.SliderInt(L("集中攻撃の人数","Focus fire threshold"),ref S.FocusCount,1,20);
                changed|=ImGui.SliderInt(L("敵の増加人数","Enemy increase threshold"),ref S.NearbyIncrease,1,20);break;
            case 4:
                changed|=ImGui.SliderFloat(L("周囲の集計距離","Nearby count range"),ref S.NearbyRange,3,100,"%.1f m");
                changed|=ImGui.SliderFloat(L("味方から離れた距離","Ally separation distance"),ref S.Separation,3,100,"%.1f m");
                changed|=ImGui.SliderInt(L("人数不利の差","Disadvantage threshold"),ref S.Disadvantage,1,20);
                ImGui.TextWrapped(L("味方を選択して /pvptrack、解除は /pvptrack clear。方向表示は地形・通行可否を保証しません。","Select an ally and run /pvptrack; /pvptrack clear removes tracking. Direction estimates do not verify terrain or a safe route."));break;
            case 5:
                changed|=ImGui.SliderInt(L("ミュートの既定秒数","Default mute seconds"),ref S.MuteSeconds,1,3600);
                changed|=ImGui.Checkbox(L("通知をまとめてオン","Enable all notifications"),ref S.Notifications);
                changed|=ImGui.Checkbox(L("通知SEをまとめてオン","Enable notification audio"),ref S.Sound);
                changed|=ImGui.SliderFloat(L("SE音量","Sound volume"),ref S.Volume,0,1,"%.2f");
                changed|=ImGui.SliderInt(L("崩しの音色","Kuzushi tone"),ref S.KuzushiTone,0,2);
                changed|=ImGui.SliderInt(L("LBの音色","LB tone"),ref S.LbTone,0,2);
                changed|=ImGui.SliderInt(L("危険の音色","Danger tone"),ref S.DangerTone,0,2);
                changed|=ImGui.SliderFloat(L("通知条件の安定時間","Stable condition duration"),ref S.StableSeconds,0,5,"%.1f s");
                changed|=ImGui.SliderFloat(L("文字サイズ","Text size"),ref S.TextSize,12,40,"%.0f px");
                changed|=ImGui.ColorEdit4(L("射程内・味方の色","In-range / ally color"),ref S.GoodColor);
                changed|=ImGui.ColorEdit4(L("射程外・危険の色","Out-of-range / danger color"),ref S.BadColor);
                changed|=ImGui.ColorEdit4(L("他者の崩し・追跡色","Other Kuzushi / tracking color"),ref S.OtherColor);
                if(ImGui.Button(L("青・黄の配色セット","Blue / yellow palette")))
                {S.GoodColor=new(.2f,.7f,1,1);S.BadColor=new(1,.8f,.15f,1);S.Features.Add(82);changed=true;}
                ImGui.TextUnformatted("/pvpmute [seconds]    /pvpmute 0");
                if(ImGui.Button(L("現在の機能選択を少人数戦に保存","Save current features for small matches"))) {S.SmallFeatures=new(S.Features);changed=true;}
                if(ImGui.Button(L("現在の機能選択を大人数戦に保存","Save current features for large matches"))) {S.LargeFeatures=new(S.Features);changed=true;}
                if(ImGui.Button(L("コンテンツ別の上書きを解除","Clear content overrides"))) {S.SmallFeatures=S.LargeFeatures=null;changed=true;}
                break;
            case 6:
                changed|=ImGui.InputText(L("試合メモ","Match note"),ref S.MatchNote,200);
                ImGui.TextWrapped(L("支援集計は最大100試合、匿名出力は最新20件。プレイヤー名・IDは保存しません。戦績履歴は別タブで管理します。","Support summaries retain 100 matches and 20 anonymous exports without player names or IDs. KDA history is managed in the ledger tab."));
                if(On(87))ImGui.TextUnformatted($"LB: {fullSeconds:F1}s");
                if(On(88))ImGui.TextUnformatted(L("機会: ","Opportunities: ")+opportunities);
                if(On(89))ImGui.TextUnformatted(L("最大人数: ","Peak coverage: ")+peakCoverage);
                if(On(60)||On(92))ImGui.TextUnformatted(L("死亡メモ: ","Death notes: ")+deathRecords.Count);
                if(On(95)&&ImGui.Button(L("匿名集計をJSON出力","Export anonymous JSON")))QueueReviewWrite(true);
                if(reviewStatus.Length>0)ImGui.TextWrapped(reviewStatus);
                foreach(var row in reviewHistory.AsEnumerable().Reverse().Take(20))
                    ImGui.TextWrapped($"{row.Started.ToLocalTime():MM/dd HH:mm} · LB {row.FullSeconds:F1}s · {row.Opportunities} / {row.Peak} · {row.Note}");
                break;
            case 7:
                changed|=ImGui.SliderInt(L("新機能の更新間隔","Support update interval"),ref S.UpdateMs,50,1000,"%d ms");
                changed|=ImGui.SliderInt(L("最大走査対象数","Maximum actors"),ref S.MaxActors,10,200);
                changed|=ImGui.SliderInt(L("最大描画件数","Maximum labels"),ref S.MaxDraw,1,100);
                changed|=ImGui.SliderFloat(L("範囲計算の処理予算","Area calculation budget"),ref S.BudgetMs,.25f,10,"%.2f ms");
                changed|=ImGui.SliderFloat(L("表示情報の有効期間","Display expiry"),ref S.StaleSeconds,.1f,2,"%.1f s");
                DrawLegacy(7);
                ImGui.TextWrapped(L("検証モードでも選択計算は行いますが、ターゲット変更・アクション要求は行いません。操作直前の再検証は常に有効です。","Dry run evaluates choices without changing targets or requesting actions. Final action revalidation is always enabled."));
                if(On(100))
                {
                    ImGui.TextUnformatted($"Scans {supportScans} · actors {supportReads} · labels {supportDraws}");
                    foreach(var cost in costs)ImGui.TextUnformatted($"{cost.Key}: {cost.Value.Ms:F3} ms / {cost.Value.Calls}");
                }
                break;
        }
        if(changed){config.Sanitize();MarkDirty();}
    }
    private void DrawSupportOverlay()
    {
        if(!MonitoringAllowed()||!S.NeedsMonitor)return;
        var now=Now;
        PlayAlerts(now);
        if(supportAt==0||now-supportAt>(On(98)?S.StaleSeconds:2))return;
        var player=objects.LocalPlayer;
        if(player==null||!player.IsValid())return;
        fonts??=new LocalizedFonts(pi.UiBuilder.FontAtlas);
        using var font=fonts.Push(config.Language);
        var draw=ImGui.GetForegroundDrawList();
        var good=ImGui.ColorConvertFloat4ToU32(S.GoodColor);var bad=ImGui.ColorConvertFloat4ToU32(S.BadColor);var other=ImGui.ColorConvertFloat4ToU32(S.OtherColor);
        var rows=new List<string>();occupiedLabels.Clear();int drawn=0;
        void Row(int id,string value){if(On(id))rows.Add(FeatureCatalog.Name(id,config.Language)+": "+value);}
        void LabelAt(Vector3 position,string text,uint color,float distance=0)
        {
            if(drawn>=(On(97)?S.MaxDraw:100)||!Project(position,out var anchor))return;
            if(On(84))for(int pass=0;pass<8&&occupiedLabels.Any(p=>Math.Abs(p.X-anchor.X)<100&&Math.Abs(p.Y-anchor.Y)<S.TextSize+4);pass++)anchor.Y-=S.TextSize+4;
            occupiedLabels.Add(anchor);drawn++;
            var size=S.TextSize*(On(85)?Math.Clamp(1-distance/150,.55f,1):1);
            draw.AddText(ImGui.GetFont(),size,anchor+Vector2.One,0xFF000000,text);draw.AddText(ImGui.GetFont(),size,anchor,color,text);
        }
        var detail=!On(78)||(player.StatusFlags&StatusFlags.InCombat)!=0;
        if(detail&&!player.IsDead)
        {
            var range=SupportOptions.JobRange(player.ClassJob.RowId,S.MeleeRange,S.RangedRange);
            if(On(18))DrawWorldRing(draw,player.Position,range+player.HitboxRadius,good);
            if(currentObservation is {} current)
            {
                var distance=current.Distance(player.Position,player.HitboxRadius);
                if(On(16)||On(17)||On(71)&&current.Ally)
                    LabelAt(current.Actor.Position+Vector3.UnitY*3,$"{distance:F1} m"+(On(82)?distance<=(current.Ally?S.AllyRange:range)?" [+]":" [!]":""),
                        (On(17)||On(71))&&distance>(current.Ally?S.AllyRange:range)?bad:good,distance);
                if(On(19))DrawWorldRing(draw,current.Actor.Position,S.AreaRadius,other);
                var coverage=currentCoverage;
                Row(20,coverage.Count.ToString());Row(21,coverage.LowCount.ToString());
                Row(23,$"{coverage.Count} / {(areaCandidates.Count>0?areaCandidates[0].Count:0)}"+(coveragePartial?" *":""));
                Row(27,$"{Math.Max(0,-current.HpRate):F0} HP/s");
                if(On(28)&&current.Healed)LabelAt(current.Actor.Position+Vector3.UnitY*3.5f,L("回復 ↑","Healed ↑"),good);
            }
            if(On(22))foreach(var c in areaCandidates.Take(3))
                LabelAt(c.Center.Actor.Position+Vector3.UnitY*3.5f,$"AOE {c.Count}"+(coveragePartial?" *":""),other);
            if(On(29))foreach(var c in attackCandidates)
                rows.Add($"HP {c.Hp} · {c.Distance(player.Position,player.HitboxRadius):F1}m · {(c.Guard?"Guard ":"")}{(c.Invulnerable?"Invulnerable":"")}"+
                    (On(30)?L(" · HP順 / 射程内"," · HP order / in range"):""));
            foreach(var o in observations)
            {
                var distance=o.Distance(player.Position,player.HitboxRadius);
                if(o.OwnKuzushi>now-supportAt&&(On(31)||On(32)))LabelAt(o.Actor.Position+Vector3.UnitY*3,
                    On(31)?$"{L("崩し","Kuzushi")} {Math.Max(0,o.OwnKuzushi-(now-supportAt)):F1}s":"◆",
                    On(32)&&o.OwnKuzushi-(now-supportAt)<=S.ExpiringSeconds?bad:good,distance);
                if(On(36)&&o.OtherKuzushi>now-supportAt)LabelAt(o.Actor.Position+Vector3.UnitY*3.6f,L("他の侍 ◇","Other SAM ◇"),other,distance);
                if(On(41)&&recommended?.ObjectId==o.Actor.ObjectId)LabelAt(o.Actor.Position+Vector3.UnitY*4,"★ LB",good,distance);
                if(On(69)&&o.Ally&&o.Percent<=S.LowHpPercent)LabelAt(o.Actor.Position+Vector3.UnitY*3,$"+ {o.Percent:F0}%",good,distance);
                if(On(74)&&focusedEnemy?.Actor.ObjectId==o.Actor.ObjectId)LabelAt(o.Actor.Position+Vector3.UnitY*4,L("集中対象 ◎","Focused ◎"),other,distance);
                if(o.Ally&&o.Actor.ObjectId==trackedAlly)
                {
                    if(On(67))LabelAt(o.Actor.Position+Vector3.UnitY*3,$"◇ {distance:F1}m",other,distance);
                    if(On(83)&&!OnScreen(o.Actor.Position))DrawDirection(draw,player.Position,o.Actor.Position,other,"◇");
                }
            }
            if(coveragePartial)rows.Add(L("* 処理予算内で調べた候補のみ（全対象の最適値ではありません）","* Partial candidates within the processing budget; not a global optimum"));
            Row(37,$"{Math.Max(0,chiten-(now-supportAt)):F1}s");if(chiten<=0&&previousOwn>0)Row(38,previousOwn.ToString());
            Row(44,requestState);
            if(On(45))rows.Add(L("LB人数は推定：既知の除外状態・対象の射程/遮蔽を確認。周辺全員の遮蔽・着弾は保証しません。","LB coverage is estimated: known exclusions and center range/LoS checked; other victims and impact are not guaranteed."));
            Row(49,(player.CurrentMp/2000).ToString());Row(50,guardReady?"✓":"—");Row(51,$"{Math.Max(0,guardTime-(now-supportAt)):F1}s");Row(52,purifyReady?"✓":"—");
            if(On(53)&&selfControls.Any(c=>c.Purify))rows.Add(L("浄化対象あり !","Purifiable effect !"));
            if(On(54))foreach(var control in selfControls)rows.Add($"{StatusRules.Label(control.Id,config.Language=="ja")}: {Math.Max(0,control.Seconds-(now-supportAt)):F1}s");
            Row(55,$"{resilience:F1}s");Row(56,focusEnemies.ToString());Row(61,nearbyAllies.ToString());Row(62,L($"味方 {nearbyAllies} / 敵 {nearbyEnemies}",$"Allies {nearbyAllies} / Enemies {nearbyEnemies}"));
            Row(64,float.IsPositiveInfinity(nearestAlly)?"—":$"{nearestAlly:F1}m");Row(73,sharedTarget.ToString());
            if(On(66)&&nearbyAllies>0)DrawDirection(draw,player.Position,allyCenter,good,"+");
            if(On(75)&&retreatDirection!=Vector3.Zero)DrawDirection(draw,player.Position,player.Position+retreatDirection*10,other,"~");
        }
        if(NotificationsAllowed)
            rows.InsertRange(0,(On(79)?alerts.OrderByDescending(a=>a.Value.Priority):alerts.OrderBy(a=>a.Key)).Where(a=>a.Value.Until>now).Take(3).Select(a=>"! "+a.Value.Text));
        if(rows.Count>0)
        {
            ImGui.SetNextWindowBgAlpha(.65f);
            ImGui.SetNextWindowPos(new Vector2(30,350),ImGuiCond.FirstUseEver);
            if(ImGui.Begin("PvP Supporter HUD###SupportHud",ImGuiWindowFlags.AlwaysAutoResize|ImGuiWindowFlags.NoTitleBar|ImGuiWindowFlags.NoFocusOnAppearing|ImGuiWindowFlags.NoNav))
                foreach(var row in rows.Take(30))ImGui.TextUnformatted(row);
            ImGui.End();
        }
        supportDraws+=drawn;
    }
    private void DrawWorldRing(ImDrawListPtr draw,Vector3 center,float radius,uint color)
    {
        const int segments=48;
        for(int i=0;i<segments;i++)
        {
            var a=i*MathF.Tau/segments;var b=(i+1)*MathF.Tau/segments;
            if(Project(center+new Vector3(MathF.Cos(a)*radius,.05f,MathF.Sin(a)*radius),out var p)
                &&Project(center+new Vector3(MathF.Cos(b)*radius,.05f,MathF.Sin(b)*radius),out var q))draw.AddLine(p,q,color,config.LineWidth);
        }
    }
    private void DrawDirection(ImDrawListPtr draw,Vector3 from,Vector3 to,uint color,string label)
    {
        if(Project(from+Vector3.UnitY,out var origin)&&Project(to+Vector3.UnitY,out var target))
        {draw.AddLine(origin,target,color,1);draw.AddText(target,color,label);return;}
        // Projection can return valid coordinates outside the viewport. Never invent a bearing behind the camera.
        gui.WorldToScreen(to+Vector3.UnitY,out var projected,out _);
        var viewport=ImGui.GetMainViewport();var center=viewport.Pos+viewport.Size/2;
        if(!float.IsFinite(projected.X)||!float.IsFinite(projected.Y))return;
        var delta=projected-center;if(delta.LengthSquared()<1)return;
        var scale=Math.Min((viewport.Size.X/2-30)/Math.Abs(delta.X),(viewport.Size.Y/2-30)/Math.Abs(delta.Y));
        var point=center+delta*scale;draw.AddCircle(point,10,color,16,2);draw.AddText(point,color,label);
    }
    private bool OnScreen(Vector3 worldPosition)
    {
        if(!gui.WorldToScreen(worldPosition,out var screen,out var inView)||!inView)return false;
        return float.IsFinite(screen.X)&&float.IsFinite(screen.Y);
    }
}
