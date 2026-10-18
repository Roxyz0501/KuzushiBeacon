using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace KuzushiBeacon;

public sealed partial class Plugin
{
    private static bool PickColor(string label,ref Vector4 color)
    {
        var changed=ImGui.ColorEdit4(label,ref color,ImGuiColorEditFlags.NoInputs|ImGuiColorEditFlags.AlphaBar);
        ImGui.TextDisabled($"RGB: {Math.Clamp((int)(color.X*255),0,255)}, {Math.Clamp((int)(color.Y*255),0,255)}, {Math.Clamp((int)(color.Z*255),0,255)}");
        return changed;
    }
    private bool DrawFeatureSections(int group)
    {
        bool changed=false;
        void Section(string ja,string en,params int[] ids)
        {
            if(!ImGui.CollapsingHeader(L(ja,en),ImGuiTreeNodeFlags.DefaultOpen))return;
            foreach(var id in ids)changed|=DrawFeatureToggle(FeatureCatalog.ById[id]);
        }
        switch(group)
        {
            case 0: Section("対象の選び方","Target selection",4);break;
            case 1:
                Section("距離・射程の表示","Distance and range",16,17,18);
                Section("範囲表示・巻き込み人数","Area and coverage",19,20,21,22,23,24,25,26);
                Section("敵のHP・攻撃候補","Enemy HP and candidates",27,28,29,30);break;
            case 3:
                Section("自分のHP・MP","Your HP and MP",46,47,48,49);
                Section("防御・浄化・状態異常","Guard, Purify and statuses",50,51,52,53,54,55);
                Section("周囲の危険・死亡前の記録","Nearby threats and death snapshots",56,57,58,59,60);break;
            case 4:
                Section("周囲の人数・位置取り","Nearby teams and positioning",61,62,63,64,65,66,75);
                Section("味方の追跡・支援","Ally tracking and support",67,68,69,71);
                Section("味方との攻撃対象合わせ","Team target coordination",73,74);break;
            case 5:
                Section("通知の優先順位・音色","Alert priority and sounds",79,80);
                Section("表示の見やすさ","Display readability",78,82,83,84,85);
                Section("コンテンツ別の切り替え","Per-content profiles",77);break;
            case 6:
                Section("試合の保存・メモ・書き出し","Storage, notes and export",86,94,95);
                Section("LBの機会・コマンドの集計","LB opportunities and commands",87,88,89,90,91);
                Section("死亡時・低HP警告後の記録","Death and warning history",92,93);break;
            case 7:
                Section("収集・描画の負荷調整","Collection and display budgets",96,97,98);
                Section("操作しない検証・処理負荷の確認","Dry run and performance",99,100);break;
        }
        return changed;
    }
    private void DrawSamuraiSettings()
    {
        bool changed=false;
        ImGui.TextWrapped(L("表示・斬鉄剣・通知ごとに設定します。親項目をオフにすると、その配下の処理も停止します。", "Configure highlights, Zantetsuken and alerts separately. Turning a parent off stops its child features."));
        changed|=ImGui.Checkbox(L("崩しの表示","Kuzushi highlights"),ref S.KuzushiDisplay);
        HoverHelp(L("自分・他の侍の崩し表示をまとめて切り替えます。斬鉄剣の推奨表示と通知は別設定です。", "Controls own and other Samurai Kuzushi highlights. LB recommendations and alerts have separate controls."));
        if(S.KuzushiDisplay)
        {
            ImGui.Indent();
            ImGui.TextUnformatted(L("表示対象","Display targets"));
            changed|=ImGui.Checkbox(L("自分の崩し","Your Kuzushi"),ref S.OwnKuzushiDisplay);
            if(S.OwnKuzushiDisplay)
            {
                ImGui.Indent();ImGui.TextUnformatted(L("表示内容","Display content"));
                changed|=ImGui.Checkbox(L("マーカー###OwnMarker","Marker###OwnMarker"),ref config.ShowMarker);
                if(config.ShowMarker)
                {
                    ImGui.Indent();changed|=PickColor(L("マーカーの色###OwnMarkerColor","Marker color###OwnMarkerColor"),ref config.MarkerColor);
                    changed|=ImGui.SliderFloat(L("大きさ###OwnMarkerSize","Size###OwnMarkerSize"),ref config.MarkerSize,5,30,"%.0f px");
                    changed|=ImGui.SliderFloat(L("高さ###OwnMarkerHeight","Height###OwnMarkerHeight"),ref config.MarkerHeight,0,6,"%.1f m");ImGui.Unindent();
                }
                changed|=ImGui.Checkbox(L("線###OwnLine","Line###OwnLine"),ref config.ShowLine);
                if(config.ShowLine)
                {
                    ImGui.Indent();changed|=PickColor(L("線の色###OwnLineColor","Line color###OwnLineColor"),ref config.LineColor);
                    changed|=ImGui.SliderFloat(L("太さ###OwnLineWidth","Width###OwnLineWidth"),ref config.LineWidth,.5f,5,"%.1f px");
                    changed|=ImGui.SliderFloat(L("高さ###OwnLineHeight","Height###OwnLineHeight"),ref config.LineHeight,0,6,"%.1f m");ImGui.Unindent();
                }
                changed|=ToggleFeature(31,L("崩しの残り秒数","Kuzushi remaining seconds"));
                if(On(31))
                {
                    ImGui.Indent();changed|=PickColor(L("文字の色###TimerColor","Text color###TimerColor"),ref S.TimerColor);
                    changed|=ImGui.SliderFloat(L("文字サイズ###TimerSize","Text size###TimerSize"),ref S.TimerSize,12,96,"%.0f px");
                    changed|=ImGui.SliderFloat(L("高さ###TimerHeight","Height###TimerHeight"),ref S.TimerHeight,0,6,"%.1f m");
                    changed|=ToggleFeature(32,L("崩しが切れる直前に色を変更","Change color near expiry"));
                    if(On(32))
                    {
                        ImGui.Indent();changed|=ImGui.SliderFloat(L("残り秒数の条件","Remaining seconds threshold"),ref S.ExpiringSeconds,.1f,5,"%.1f s");
                        changed|=PickColor(L("期限直前の色","Expiring color"),ref S.ExpiringColor);ImGui.Unindent();
                    }
                    ImGui.Unindent();
                }
                ImGui.Unindent();
            }
            changed|=ToggleFeature(36,L("他の侍の崩しを区別して表示","Distinguish other Samurai Kuzushi"));
            if(On(36)){ImGui.Indent();changed|=PickColor(L("他の侍の表示色","Other Samurai color"),ref S.OtherColor);ImGui.Unindent();}
            ImGui.TextUnformatted(L("表示条件","Display conditions"));
            changed|=ImGui.Checkbox(L("LB満タン時だけ表示","Only show with full LB"),ref S.KuzushiFullLbOnly);
            ImGui.Unindent();
        }
        ImGui.Separator();
        changed|=ImGui.Checkbox(L("斬鉄剣サポート","Zantetsuken support"),ref S.ZantetsukenSupport);
        HoverHelp(L("推奨対象・人数表示と斬鉄剣コマンドをまとめて切り替えます。範囲計算はLB満タン時のみです。", "Controls recommendations, coverage count and LB commands. Coverage is calculated only with full LB."));
        if(S.ZantetsukenSupport)
        {
            ImGui.Indent();
            changed|=ToggleFeature(41,L("最も多く巻き込める敵を表示","Highlight the best coverage target"));
            if(On(41))
            {
                ImGui.Indent();
                changed|=ImGui.Checkbox(L("マーカー###BestMarker","Marker###BestMarker"),ref S.BestMarker);
                if(S.BestMarker)
                {
                    ImGui.Indent();changed|=PickColor(L("マーカーの色###BestMarkerColor","Marker color###BestMarkerColor"),ref S.BestMarkerColor);
                    changed|=ImGui.SliderFloat(L("大きさ###BestMarkerSize","Size###BestMarkerSize"),ref S.BestMarkerSize,5,30,"%.0f px");
                    changed|=ImGui.SliderFloat(L("高さ###BestMarkerHeight","Height###BestMarkerHeight"),ref S.BestMarkerHeight,0,6,"%.1f m");ImGui.Unindent();
                }
                changed|=ImGui.Checkbox(L("線###BestLine","Line###BestLine"),ref S.BestLine);
                if(S.BestLine)
                {
                    ImGui.Indent();changed|=PickColor(L("線の色###BestLineColor","Line color###BestLineColor"),ref S.BestLineColor);
                    changed|=ImGui.SliderFloat(L("太さ###BestLineWidth","Width###BestLineWidth"),ref S.BestLineWidth,.5f,5,"%.1f px");
                    changed|=ImGui.SliderFloat(L("高さ###BestLineHeight","Height###BestLineHeight"),ref S.BestLineHeight,0,6,"%.1f m");ImGui.Unindent();
                }
                ImGui.Unindent();
            }
            changed|=ImGui.Checkbox(L("最大巻き込み人数を自分の頭上に表示","Show maximum coverage above yourself"),ref config.ShowMaxCount);
            if(config.ShowMaxCount)
            {
                ImGui.Indent();changed|=PickColor(L("人数の色","Count color"),ref config.CountColor);
                changed|=ImGui.SliderFloat(L("文字サイズ###CountSize","Text size###CountSize"),ref config.CountSize,12,96,"%.0f px");
                changed|=ImGui.SliderFloat(L("高さ###CountHeight","Height###CountHeight"),ref config.CountHeight,0,6,"%.1f m");
                changed|=ImGui.SliderFloat(L("上下位置の微調整","Vertical screen offset"),ref config.CountOffsetY,-300,300,"%.0f px");
                ImGui.Checkbox(Label("CountPreview"),ref previewCount);
                if(previewCount)
                {
                    var player=objects.LocalPlayer;
                    if(player!=null&&player.IsValid()&&Project(player.Position+Vector3.UnitY*config.CountHeight,out var head))
                        DrawCount(ImGui.GetForegroundDrawList(),head+new Vector2(0,config.CountOffsetY),3);
                }
                ImGui.Unindent();
            }
            changed|=DrawFeatureToggle(FeatureCatalog.ById[43]);
            changed|=DrawFeatureToggle(FeatureCatalog.ById[44]);
            changed|=DrawFeatureToggle(FeatureCatalog.ById[45]);
            ImGui.Unindent();
        }
        if(S.KuzushiDisplay||S.ZantetsukenSupport||S.SamuraiNotifications)
        {
            ImGui.Separator();ImGui.TextUnformatted(L("除外設定（表示・人数・コマンド・崩し通知に共通）","Exclusions shared by highlights, counts, commands and Kuzushi alerts"));
            changed|=ImGui.Checkbox(L("防御を除外","Exclude Guard"),ref S.ZantetsukenExcludeGuard);
            changed|=ImGui.Checkbox(L("既知の無敵バフを除外","Exclude known invulnerability"),ref S.ZantetsukenExcludeInvulnerable);
        }
        ImGui.Separator();changed|=ImGui.Checkbox(L("通知（SEで知らせる）","Samurai alerts (sound)"),ref S.SamuraiNotifications);
        if(S.SamuraiNotifications)
        {
            ImGui.Indent();
            ImGui.TextWrapped(L("通知全体とSEの設定は全ジョブで共通です。", "Master notification and audio settings are shared by all jobs."));
            changed|=ImGui.Checkbox(L("通知を有効にする（全体）","Enable notifications (global)"),ref S.Notifications);
            changed|=ImGui.Checkbox(L("SEを有効にする（全体）","Enable sound (global)"),ref S.Sound);
            if(S.Sound)changed|=ImGui.SliderFloat(L("音量（全体）","Volume (global)"),ref S.Volume,0,1,"%.2f");
            changed|=ToggleFeature(33,L("崩しが付いたとき","When Kuzushi is applied"));
            changed|=ToggleFeature(35,L("崩しがすべて消えたとき","When all Kuzushi ends"));
            changed|=ToggleFeature(40,L("LB満タンと崩しが揃ったとき","When full LB and Kuzushi coincide"));
            ImGui.Unindent();
        }
        if(ImGui.CollapsingHeader(L("地天の状態表示","Chiten state displays")))
        {changed|=DrawFeatureToggle(FeatureCatalog.ById[37]);changed|=DrawFeatureToggle(FeatureCatalog.ById[38]);}
        if(changed){config.Sanitize();MarkDirty();}
    }
    private bool ToggleFeature(int id,string title)
    {
        var selected=S.Selected(id);var changed=ImGui.Checkbox(title+"###feature"+id,ref selected);
        if(changed)S.SetFeature(id,selected);
        HoverHelp(FeatureHelp.Get(id,config.Language));return changed;
    }
}
