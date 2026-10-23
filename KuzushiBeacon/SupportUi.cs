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
                if(ImGui.CollapsingHeader(L("ジョブ共通の通知","Shared job alerts"),ImGuiTreeNodeFlags.DefaultOpen))
                {if(DrawFeatureToggle(FeatureCatalog.ById[39]))MarkDirty();}
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
        if(group==2){DrawSamuraiSettings();return;}
        bool changed=false;
        ImGui.TextUnformatted(FeatureCatalog.Group(group,config.Language));
        ImGui.TextWrapped(group switch
        {
            0=>L("最低HP・最寄り・味方への対象変更で使う射程と除外条件です。コマンドは一覧ページからコピーできます。","Ranges and filters for lowest-HP, nearest and friendly targeting. Copy commands from the command page."),
            2=>L("自分が付けた崩しの表示、LBの通知、斬鉄剣の対象選択条件を調整します。","Configure your Kuzushi highlights, LB alerts and Zantetsuken target conditions."),
            _=>L("必要な表示・通知だけをオンにできます。詳しい動作や見た目は下の設定で調整します。","Enable the displays and alerts you need. Adjust their behavior and appearance below.")
        });
        ImGui.TextWrapped(S.Features.Contains(77)&&!S.DisabledGroups.Contains(5)
            ? L(S.CurrentLarge?"編集中：大人数戦の機能選択（未保存なら共通設定）":"編集中：少人数戦の機能選択（未保存なら共通設定）",S.CurrentLarge?"Editing large-match features (common fallback if unsaved)":"Editing small-match features (common fallback if unsaved)")
            : L("編集中：共通の機能選択","Editing common features"));
        ImGui.Separator();
        var enabled=!S.DisabledGroups.Contains(group);
        if(ImGui.Checkbox(L("このカテゴリの補助機能を使う","Enable this category"),ref enabled))
        {if(enabled)S.DisabledGroups.Remove(group);else S.DisabledGroups.Add(group);changed=true;}
        if(ImGui.Button(L("表示・通知をまとめてオン","Enable category features")))
        {S.SetCategoryFeatures(group,true);if(group==2)config.ShowLine=config.ShowMarker=config.ShowMaxCount=true;changed=true;}
        ImGui.SameLine();
        if(ImGui.Button(L("表示・通知をまとめてオフ","Disable category features")))
        {S.SetCategoryFeatures(group,false);if(group==2)config.ShowLine=config.ShowMarker=config.ShowMaxCount=false;changed=true;}
        changed|=DrawFeatureSections(group);
        ImGui.Separator();
        bool Any(params int[] ids)=>ids.Any(On);
        switch(group)
        {
            case 0:
                ImGui.TextUnformatted(L("コマンドの共通設定","Shared command settings"));
                changed|=ImGui.SliderFloat(L("近接・タンクの射程","Melee / tank range"),ref S.MeleeRange,1,3,"%.1f m");
                changed|=ImGui.SliderFloat(L("遠隔・ヒーラーの射程","Ranged / healer range"),ref S.RangedRange,1,20,"%.1f m");
                changed|=ImGui.SliderFloat(L("味方選択の射程","Ally selection range"),ref S.AllyRange,1,20,"%.1f m");
                changed|=ImGui.Checkbox(L("敵の選択：防御を除外","Enemy targeting: exclude Guard"),ref S.ExcludeGuard);
                changed|=ImGui.Checkbox(L("敵の選択：既知の無敵を除外","Enemy targeting: exclude known invulnerability"),ref S.ExcludeInvulnerable);
                ImGui.TextWrapped(L("コマンドは常に登録されます。射程は引数なし・autoで使用し、固定距離・数値引数を指定した場合はジョブ上限内でその値を使用します。最低HPは残りHP実数で比較します。","Commands are always registered. No argument or auto uses these ranges; fixed/numeric ranges override them within the job cap. Lowest HP compares absolute HP."));
                break;
            case 1:
                if(Any(19,20,21,22,23,24))
                {
                    ImGui.TextUnformatted(L("範囲表示・人数計算の共通設定","Shared area display / count settings"));
                    changed|=ImGui.SliderFloat(L("範囲の半径","Area radius"),ref S.AreaRadius,1,30,"%.1f m");
                }
                break;
            case 3: case 4:
                if(Any(58,61,62,63,65,66,75))
                {
                    ImGui.TextUnformatted(L("周囲の人数・位置取りの共通設定","Shared nearby counts / positioning settings"));
                    changed|=ImGui.SliderFloat(L("周囲の集計距離","Nearby count range"),ref S.NearbyRange,3,100,"%.1f m");
                }
                break;
            case 5:
                changed|=ImGui.Checkbox(L("通知をまとめてオン","Enable all notifications"),ref S.Notifications);
                if(S.Notifications)
                {
                    ImGui.Indent();
                    changed|=ImGui.Checkbox(L("通知SEをまとめてオン","Enable notification audio"),ref S.Sound);
                    if(S.Sound)
                    {
                        changed|=ImGui.SliderFloat(L("SE音量","Sound volume"),ref S.Volume,0,1,"%.2f");
                        changed|=ImGui.SliderInt(L("ミュートの既定秒数","Default mute seconds"),ref S.MuteSeconds,1,3600);
                    }
                    ImGui.Unindent();
                }
                if(S.NeedsMonitor)
                {
                    ImGui.TextUnformatted(L("支援表示の共通設定","Shared support display settings"));
                    changed|=ImGui.SliderFloat(L("文字サイズ","Text size"),ref S.TextSize,12,40,"%.0f px");
                    changed|=PickColor(L("射程内・味方の色","In-range / ally color"),ref S.GoodColor);
                    changed|=PickColor(L("射程外・危険の色","Out-of-range / danger color"),ref S.BadColor);
                    if(Any(19,36,67,74,83))changed|=PickColor(L("他者の崩し・追跡色","Other Kuzushi / tracking color"),ref S.OtherColor);
                }
                break;
            case 6:
                ImGui.TextWrapped(L("試合の保存には「試合ごとのローカル記録」が必要です。現在の集計は保存しなくても確認できます。","Local match summaries is required to save matches. Current counters can be inspected without saving."));
                DrawReviewDetails();break;
            case 7:
                if(S.NeedsMonitor)
                {
                    ImGui.TextUnformatted(L("支援機能の共通更新間隔","Shared support refresh interval"));
                    changed|=ImGui.SliderInt(L("更新間隔","Update interval"),ref S.UpdateMs,50,1000,"%d ms");
                }
                DrawLegacy(7);break;
        }
        if(changed){config.Sanitize();MarkDirty();}
    }
    private bool DrawFeatureToggle(Feature feature)
    {
        var value=S.Selected(feature.Id);
        var changed=ImGui.Checkbox($"{FeatureCatalog.Name(feature.Id,config.Language)}###feature{feature.Id}",ref value);
        if(changed)S.SetFeature(feature.Id,value);
        HoverHelp(FeatureHelp.Get(feature.Id,config.Language));
        if(On(feature.Id)){ImGui.Indent();changed|=DrawFeatureParameters(feature.Id);ImGui.Unindent();}
        return changed;
    }
    private static void HoverHelp(string help)
    {
        if(!ImGui.IsItemHovered())return;
        DrawHelpTooltip(help);
    }
    private static void DrawHelpTooltip(string help)
    {ImGui.BeginTooltip();ImGui.PushTextWrapPos(ImGui.GetFontSize()*28);ImGui.TextUnformatted(help);ImGui.PopTextWrapPos();ImGui.EndTooltip();}
    private bool DrawFeatureParameters(int id)
    {
        bool changed=false;
        switch(id)
        {
            case 21: case 46: case 69:
                ImGui.PushID(id);
                changed|=ImGui.SliderFloat(L("低HPの判定（%・共通）","Low HP threshold (%, shared)"),ref S.LowHpPercent,1,99);
                ImGui.PopID();break;
            case 24: changed|=ImGui.SliderInt(L("通知する巻き込み人数","Coverage alert count"),ref S.AlertCount,1,20);break;
            case 25: changed|=ImGui.SliderFloat(L("通知条件の安定時間","Stable condition duration"),ref S.StableSeconds,0,5,"%.1f s");break;
            case 32: changed|=ImGui.SliderFloat(L("崩し期限の警告秒数","Expiring Kuzushi seconds"),ref S.ExpiringSeconds,.1f,5);break;
            case 43: changed|=ImGui.SliderInt(L("斬鉄剣の最低人数","Minimum Zantetsuken targets"),ref S.MinimumZantetsuken,1,20);break;
            case 47: changed|=ImGui.SliderFloat(L("急減の判定（%/秒）","Rapid loss (%/s)"),ref S.RapidLossPercent,1,100);break;
            case 48: changed|=ImGui.SliderFloat(L("低MPの判定","Low MP threshold"),ref S.LowMp,0,10000,"%.0f");break;
            case 56:
                changed|=ImGui.Checkbox(T("FocusHud"),ref S.FocusHud);
                changed|=ImGui.Checkbox(T("FocusOverhead"),ref S.FocusOverhead);
                if(S.FocusOverhead)
                {
                    ImGui.Indent();
                    changed|=PickColor(T("CountColor")+"###FocusColor",ref S.FocusColor);
                    changed|=ImGui.SliderFloat(T("CountSize")+"###FocusSize",ref S.FocusSize,12,96,"%.0f px");
                    changed|=ImGui.SliderFloat(T("CountHeight")+"###FocusHeight",ref S.FocusHeight,0,6,"%.1f m");
                    ImGui.Unindent();
                }
                break;
            case 57: changed|=ImGui.SliderInt(L("集中攻撃の人数","Focus fire threshold"),ref S.FocusCount,1,20);break;
            case 58: changed|=ImGui.SliderInt(L("敵の増加人数","Enemy increase threshold"),ref S.NearbyIncrease,1,20);break;
            case 63: changed|=ImGui.SliderInt(L("人数不利の差","Disadvantage threshold"),ref S.Disadvantage,1,20);break;
            case 68: changed|=ImGui.SliderFloat(L("味方から離れた距離","Ally separation distance"),ref S.Separation,3,100,"%.1f m");break;
            case 77:
                if(ImGui.Button(L("現在の機能選択を少人数戦に保存","Save current features for small matches"))) {S.SmallFeatures=new(S.ActiveFeatures());changed=true;}
                if(ImGui.Button(L("現在の機能選択を大人数戦に保存","Save current features for large matches"))) {S.LargeFeatures=new(S.ActiveFeatures());changed=true;}
                if(ImGui.Button(L("コンテンツ別の上書きを解除","Clear content overrides"))) {S.SmallFeatures=S.LargeFeatures=null;changed=true;}
                break;
            case 80:
                if(S.Sound&&S.Notifications)
                {
                    changed|=ImGui.SliderInt(L("崩しの音色","Kuzushi tone"),ref S.KuzushiTone,0,2);
                    changed|=ImGui.SliderInt(L("LBの音色","LB tone"),ref S.LbTone,0,2);
                    changed|=ImGui.SliderInt(L("危険の音色","Danger tone"),ref S.DangerTone,0,2);
                }
                break;
            case 82:
                if(ImGui.Button(L("青・黄の配色セット","Blue / yellow palette")))
                {S.GoodColor=new(.2f,.7f,1,1);S.BadColor=new(1,.8f,.15f,1);changed=true;}
                break;
            case 94: changed|=ImGui.InputText(L("試合メモ","Match note"),ref S.MatchNote,200);break;
            case 97:
                changed|=ImGui.SliderInt(L("最大走査対象数","Maximum actors"),ref S.MaxActors,10,200);
                changed|=ImGui.SliderInt(L("頭上表示の最大件数","Maximum overhead labels"),ref S.MaxDraw,1,100);
                changed|=ImGui.SliderFloat(L("範囲計算の処理予算","Area calculation budget"),ref S.BudgetMs,.25f,10,"%.2f ms");break;
            case 98: changed|=ImGui.SliderFloat(L("表示情報の有効期間","Display expiry"),ref S.StaleSeconds,.1f,2,"%.1f s");break;
            case 100:
                ImGui.TextUnformatted($"Scans {supportScans} · actors {supportReads} · labels {supportDraws}");
                foreach(var cost in costs)ImGui.TextUnformatted($"{cost.Key}: {cost.Value.Ms:F3} ms / {cost.Value.Calls}");break;
        }
        return changed;
    }
    private void DrawReviewDetails()
    {
                if(On(87))ImGui.TextUnformatted($"LB: {fullSeconds:F1}s");
                if(On(88))ImGui.TextUnformatted(L("機会: ","Opportunities: ")+opportunities);
                if(On(89))ImGui.TextUnformatted(L("最大人数: ","Peak coverage: ")+peakCoverage);
                if(On(90))DrawCountDictionary(L("コマンド利用回数","Command counts"),commandCounts);
                if(On(91))DrawCountDictionary(L("対象が見つからなかった理由","Failure reasons"),failureCounts);
                if(On(60)||On(92)||On(93))DrawDeathRecords(deathRecords);
                if(On(95)&&ImGui.Button(L("匿名集計をJSON出力","Export anonymous JSON")))QueueReviewWrite(true);
                if(reviewStatus.Length>0)ImGui.TextWrapped(reviewStatus);
                if(reviewFailures>0&&ImGui.Button(HistoryFilesView.Text(config.Language,15)+"###RetryReview"))RetryReview();
                if(ImGui.Button("<###ReviewPrevious"))reviewPage=Math.Max(0,reviewPage-1);
                ImGui.SameLine();ImGui.TextUnformatted($"{reviewPage+1} / {Math.Max(1,(reviewHistory.Count+19)/20)}");ImGui.SameLine();
                if(ImGui.Button(">###ReviewNext"))reviewPage=Math.Min(Math.Max(0,(reviewHistory.Count-1)/20),reviewPage+1);
                foreach(var row in reviewHistory.AsEnumerable().Reverse().Skip(reviewPage*20).Take(20))
                {
                    ImGui.PushID(row.Started.ToString("O"));
                    if(ImGui.TreeNode($"{row.Started.ToLocalTime():MM/dd HH:mm} · {row.Note}###review"))
                    {
                        if(row.FullSeconds is {} held)ImGui.TextUnformatted($"LB: {held:F1}s");
                        if(row.Opportunities is {} opportunityCount)ImGui.TextUnformatted(L("機会: ","Opportunities: ")+opportunityCount);
                        if(row.Peak is {} peak)ImGui.TextUnformatted(L("最大人数: ","Peak coverage: ")+peak);
                        if(row.Settings!=null)foreach(var setting in row.Settings)ImGui.TextUnformatted($"{setting.Key}: {setting.Value}");
                        if(row.Commands!=null)DrawCountDictionary(L("コマンド利用回数","Command counts"),row.Commands);
                        if(row.Failures!=null)DrawCountDictionary(L("対象が見つからなかった理由","Failure reasons"),row.Failures);
                        DrawDeathRecords(row.Deaths);
                        ImGui.TreePop();
                    }
                    ImGui.PopID();
                }
    }
    private void DrawCountDictionary(string title,IReadOnlyDictionary<string,long> values)
    {
        ImGui.TextUnformatted(title);
        if(values.Count==0)ImGui.TextUnformatted(L("記録なし","No entries"));
        foreach(var value in values)ImGui.TextUnformatted($"{value.Key}: {value.Value}");
    }
    private void DrawDeathRecords(IEnumerable<DeathRecord> records)
    {
        foreach(var death in records.Reverse().Take(10))
        {
            ImGui.TextWrapped($"{death.At.ToLocalTime():HH:mm:ss} · "+L("死亡","Death")+
                (death.Enemies is {} enemies?L($" · 敵{enemies} / 味方{death.Allies}",$" · Enemies {enemies} / allies {death.Allies}"):"")+
                (death.Survival is {} survival?L($" · 警告後 {survival:F1}秒",$" · {survival:F1}s after warning"):""));
            foreach(var state in death.States??[])
                ImGui.TextWrapped($"HP {state.Hp} / MP {state.Mp} · "+string.Join(", ",state.Controls.Select(id=>StatusRules.Label(id,config.Language=="ja"))));
        }
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
        void LabelAt(Vector3 position,string text,uint color,float distance=0,float? textSize=null)
        {
            if(drawn>=(On(97)?S.MaxDraw:100)||!Project(position,out var anchor))return;
            if(On(84))for(int pass=0;pass<8&&occupiedLabels.Any(p=>Math.Abs(p.X-anchor.X)<100&&Math.Abs(p.Y-anchor.Y)<(textSize??S.TextSize)+4);pass++)anchor.Y-=(textSize??S.TextSize)+4;
            occupiedLabels.Add(anchor);drawn++;
            var size=(textSize??S.TextSize)*(On(85)?Math.Clamp(1-distance/150,.55f,1):1);
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
                var included=o.Eligible(S.ZantetsukenExcludeGuard,S.ZantetsukenExcludeInvulnerable);
                if(included&&S.KuzushiVisible(fullLb))
                {
                    if(o.OwnKuzushi>now-supportAt&&On(31))
                    {
                        var remaining=Math.Max(0,o.OwnKuzushi-(now-supportAt));
                        var color=ImGui.ColorConvertFloat4ToU32(On(32)&&remaining<=S.ExpiringSeconds?S.ExpiringColor:S.TimerColor);
                        LabelAt(o.Actor.Position+Vector3.UnitY*S.TimerHeight,$"{L("崩し","Kuzushi")} {remaining:F1}s",color,distance,S.TimerSize);
                    }
                    if(On(36)&&o.OtherKuzushi>now-supportAt)LabelAt(o.Actor.Position+Vector3.UnitY*3.6f,L("他の侍 ◇","Other SAM ◇"),other,distance);
                }
                if(included&&fullLb&&On(41)&&recommended?.ObjectId==o.Actor.ObjectId&&drawn<(On(97)?S.MaxDraw:100))
                {
                    if(S.BestMarker||S.BestLine)drawn++;
                    if(S.BestMarker&&Project(o.Actor.Position+Vector3.UnitY*S.BestMarkerHeight,out var bestHead))
                        DrawMarker(draw,bestHead,S.BestMarkerSize,ImGui.ColorConvertFloat4ToU32(S.BestMarkerColor),S.BestMarkerColor.W);
                    if(S.BestLine&&Project(player.Position+Vector3.UnitY*S.BestLineHeight,out var bestFrom)&&Project(o.Actor.Position+Vector3.UnitY*S.BestLineHeight,out var bestTo))
                        draw.AddLine(bestFrom,bestTo,ImGui.ColorConvertFloat4ToU32(S.BestLineColor),S.BestLineWidth);
                }
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
            Row(55,$"{resilience:F1}s");if(S.FocusHud)Row(56,focusEnemies.ToString());
            if(On(56)&&S.FocusOverhead)LabelAt(player.Position+Vector3.UnitY*S.FocusHeight,string.Format(T("FocusCountText"),focusEnemies),ImGui.ColorConvertFloat4ToU32(S.FocusColor),0,S.FocusSize);Row(61,nearbyAllies.ToString());Row(62,L($"味方 {nearbyAllies} / 敵 {nearbyEnemies}",$"Allies {nearbyAllies} / Enemies {nearbyEnemies}"));
            Row(64,float.IsPositiveInfinity(nearestAlly)?"—":$"{nearestAlly:F1}m");Row(73,sharedTarget.ToString());
            if(On(66)&&nearbyAllies>0)DrawDirection(draw,player.Position,allyCenter,good,"+");
            if(On(75)&&retreatDirection!=Vector3.Zero)DrawDirection(draw,player.Position,player.Position+retreatDirection*10,other,"~");
        }
        if(NotificationsAllowed)
            rows.InsertRange(0,(On(79)?alerts.OrderByDescending(a=>a.Value.Priority):alerts.OrderBy(a=>a.Key)).Where(a=>On(a.Key)&&a.Value.Until>now).Take(3).Select(a=>"! "+a.Value.Text));
        if(rows.Count>0)
        {
            ImGui.SetNextWindowBgAlpha(.65f);
            ImGui.SetNextWindowPos(new Vector2(30,350),ImGuiCond.FirstUseEver);
            if(ImGui.Begin("PvP Supporter HUD###SupportHud",ImGuiWindowFlags.AlwaysAutoResize|ImGuiWindowFlags.NoTitleBar|ImGuiWindowFlags.NoFocusOnAppearing|ImGuiWindowFlags.NoNav))
            {
                ImGui.SetWindowFontScale(S.TextSize/ImGui.GetFontSize());
                foreach(var row in rows.Take(30))ImGui.TextUnformatted(row);
                ImGui.SetWindowFontScale(1);
            }
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
        if(OnScreen(to+Vector3.UnitY)&&Project(from+Vector3.UnitY,out var origin)&&Project(to+Vector3.UnitY,out var target))
        {draw.AddLine(origin,target,color,1);draw.AddText(target,color,label);return;}
        // Projection can return valid coordinates outside the viewport. Never invent a bearing behind the camera.
        if(!gui.WorldToScreen(to+Vector3.UnitY,out var projected,out _))return;
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
