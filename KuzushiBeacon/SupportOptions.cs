using System.Numerics;

namespace KuzushiBeacon;

public sealed record Feature(int Id, int Group, string Japanese, string English);

public static class FeatureCatalog
{
    public static readonly Feature[] All = [
        new(4,0,"画面内の敵だけを選択","Select on-screen enemies only"),
        new(16,1,"現在のターゲットまでの距離を表示","Target distance"), new(17,1,"射程内・射程外で表示色を変える","Range colors"),
        new(18,1,"自分の攻撃範囲を円で表示","Player range ring"),new(19,1,"ターゲット中心の範囲を円で表示","Target area preview"),
        new(20,1,"範囲内の敵人数","Enemies in area"),new(21,1,"範囲内の低HP人数","Low-HP enemies in area"),
        new(22,1,"巻き込み上位3候補","Top three area candidates"),new(23,1,"現在対象と最適対象の比較","Current versus best coverage"),
        new(24,1,"人数条件の通知","Coverage threshold alert"),new(25,1,"通知前の条件安定を確認","Require stable alert conditions"),
        new(26,1,"条件解除で通知を消す","Clear alerts when conditions end"),new(27,1,"対象のHP減少速度","Target HP loss rate"),
        new(28,1,"回復した対象を表示","Highlight healed targets"),new(29,1,"攻撃候補一覧","Attack candidate list"),new(30,1,"候補を選んだ理由","Selection reasons"),
        new(31,2,"敵の頭上に崩しの残り秒数を表示","Kuzushi remaining time"),new(32,2,"崩しが切れる直前に色を変える","Expiring Kuzushi color"),
        new(33,2,"新しく崩しが付いたら音で通知","New Kuzushi sound"),new(34,2,"崩しが付いた敵の人数が増えたら通知","Kuzushi count increase"),
        new(35,2,"崩しがすべて消えたら通知","All Kuzushi ended"),new(36,2,"他の侍の崩しを区別","Distinguish other samurai Kuzushi"),
        new(37,2,"地天の残り時間","Chiten timer"),new(38,2,"地天終了後の崩し残存","Kuzushi remaining after Chiten"),
        new(39,2,"LBが満タンになったら通知","LB ready alert"),new(40,2,"LB満タンで崩しが付いたら通知","LB and Kuzushi opportunity"),
        new(41,2,"最も多く巻き込める敵にマーカーを表示","Recommended Zantetsuken target"),
        new(43,2,"指定人数以上を巻き込めるときだけ斬鉄剣を要求","Minimum Zantetsuken coverage"),new(44,2,"斬鉄剣の待機・要求済み状態を表示","Zantetsuken request state"),
        new(45,2,"斬鉄剣の人数予測に関する注意を表示","Estimated hit conditions"),
        new(46,3,"低HP警告","Low HP alert"),new(47,3,"HP急減警告","Rapid HP loss"),new(48,3,"低MP警告","Low MP alert"),
        new(49,3,"快気の残り回数目安","Estimated Recuperate uses"),new(50,3,"防御の使用可能表示","Guard readiness"),
        new(51,3,"防御終了カウントダウン","Guard countdown"),new(52,3,"浄化の使用可能表示","Purify readiness"),
        new(53,3,"浄化対象の状態を強調","Highlight purifiable effects"),new(54,3,"状態異常の残り時間","Crowd-control timers"),
        new(55,3,"耐性の残り時間","Resilience timer"),new(56,3,"自分を狙う敵の観測数","Observed enemies targeting you"),
        new(57,3,"集中攻撃の可能性","Possible focus fire"),new(58,3,"近距離の敵増加警告","Nearby enemy increase"),
        new(59,3,"回復詠唱中の接近警告","Enemies approaching during Elixir"),new(60,3,"死亡直前の状態メモ","Pre-death state history"),
        new(61,4,"周囲の味方人数","Nearby allies"),new(62,4,"周囲の敵味方人数","Local team counts"),
        new(63,4,"人数不利警告","Local disadvantage alert"),new(64,4,"最寄りの味方までの距離","Nearest ally distance"),
        new(65,4,"孤立警告","Isolation alert"),new(66,4,"味方集団の中心方向","Direction to ally centroid"),
        new(67,4,"指定した味方のマーカー","Tracked ally marker"),new(68,4,"追跡する味方との距離警告","Tracked ally separation"),
        new(69,4,"低HPの味方を強調","Highlight low-HP allies"),
        new(71,4,"味方への支援射程","Friendly target support range"),
        new(73,4,"味方との対象一致人数","Allies sharing your target"),new(74,4,"味方に狙われている敵","Most focused enemy"),
        new(75,4,"退避方向の目安","Approximate retreat direction"),
        new(77,5,"コンテンツ別設定","Content-specific profiles"),new(78,5,"戦闘中だけ詳細表示","Detailed display only in combat"),
        new(79,5,"重要通知を優先","Prioritize critical alerts"),new(80,5,"通知の種類別SE","Per-category sounds"),
        new(82,5,"色と記号を併用","Colorblind symbols"),
        new(83,5,"画面外の追跡対象の方向","Offscreen tracked ally direction"),new(84,5,"重なる表示をずらす","Separate overlapping labels"),
        new(85,5,"遠い対象の表示を縮小","Scale distant labels"),
        new(86,6,"試合ごとのローカル記録","Local match summaries"),new(87,6,"LB満タンの保持時間","Time holding full LB"),
        new(88,6,"崩しとLBが揃った回数","Kuzushi and LB opportunities"),new(89,6,"最大巻き込み人数","Maximum observed coverage"),
        new(90,6,"対象選択コマンドを使った回数を記録","Target command counts"),new(91,6,"対象なしの理由集計","Unavailable target reasons"),
        new(92,6,"死亡時の近隣人数","Nearby counts at death"),new(93,6,"低HP警告後の生存時間","Survival after low-HP warning"),
        new(94,6,"設定比較用の試合メモ","Match settings note"),new(95,6,"匿名集計の書き出し","Anonymous summary export"),
        new(96,7,"使っていない機能の計算を省く","Scan only required features"),new(97,7,"混雑時の処理予算","Processing budgets"),
        new(98,7,"更新が止まった古い情報を表示から消す","Expire stale display data"),new(99,7,"検証モード：候補を調べるだけで操作しない","Dry run without targeting or actions"),
        new(100,7,"処理別の負荷表示","Processing-stage performance metrics")
    ];
    public static readonly IReadOnlyDictionary<int,Feature> ById=All.ToDictionary(f=>f.Id);
    public static string Name(int id, string? language) => All.FirstOrDefault(f => f.Id == id) is { } f ? language == "ja" ? f.Japanese : f.English : id.ToString();
    public static string Group(int id, string? language) => (language == "ja"
        ? new[] { "対象選択", "攻撃機会", "崩し・侍", "生存支援", "味方・位置取り", "通知・表示", "振り返り", "軽量化・検証" }
        : new[] { "Targeting", "Opportunities", "Samurai", "Survival", "Allies", "Alerts / display", "Review", "Performance" })[id];
    public static string FontText => string.Join("", All.Select(f => f.Japanese + f.English));
}

public sealed class SupportOptions
{
    public HashSet<int> Features = [25,26,79,84,96,97,98];
    public HashSet<int> DisabledGroups = [];
    public bool Notifications = true, Sound;
    public float Volume = .25f;
    public int KuzushiTone = 1, LbTone = 2, DangerTone;
    public bool ExcludeGuard, ExcludeInvulnerable = true, ZantetsukenExcludeGuard, ZantetsukenExcludeInvulnerable = true;
    public float MeleeRange = 3, RangedRange = 20, AllyRange = 20, AreaRadius = 5, NearbyRange = 20;
    public float LowHpPercent = 30, LowMp = 2000, RapidLossPercent = 25, ExpiringSeconds = 1, StableSeconds = .5f;
    public int MuteSeconds = 60;
    public int MinimumZantetsuken = 1, AlertCount = 3, FocusCount = 3, NearbyIncrease = 3, Disadvantage = 2;
    public int UpdateMs = 200, MaxActors = 200, MaxDraw = 30;
    public float BudgetMs = 2, StaleSeconds = .8f, Separation = 20, TextSize = 18;
    public Vector4 GoodColor = new(.2f,1,.6f,1), BadColor = new(1,.25f,.2f,1), OtherColor = new(.5f,.7f,1,1);
    public string MatchNote = "";
    public bool LedgerEnabled;
    // Profiles override the new-feature set only; common language/support and legacy controls stay global.
    public HashSet<int>? SmallFeatures, LargeFeatures;
    [Newtonsoft.Json.JsonIgnore, System.Text.Json.Serialization.JsonIgnore]
    public bool CurrentLarge;
    public HashSet<int> ActiveFeatures(bool? large = null) => Features.Contains(77) && !DisabledGroups.Contains(5)
        ? ((large ?? CurrentLarge) ? LargeFeatures : SmallFeatures) ?? Features : Features;
    public bool Selected(int id) => (id == 77 ? Features : ActiveFeatures()).Contains(id);
    public void SetFeature(int id,bool enabled)
    {
        var set=id==77?Features:ActiveFeatures();
        if(enabled)set.Add(id);else set.Remove(id);
    }
    public void SetCategoryFeatures(int group,bool enabled)
    {
        // Capture before toggling profile selection (77), which changes ActiveFeatures.
        var active=ActiveFeatures();
        foreach(var feature in FeatureCatalog.All.Where(f=>f.Group==group&&(!enabled||f.Id!=99)))
        {
            var set=feature.Id==77?Features:active;
            if(enabled)set.Add(feature.Id);else set.Remove(feature.Id);
        }
    }
    public void SetAllFeatures(bool enabled)
    {
        foreach(var set in new[]{Features,SmallFeatures,LargeFeatures}.Where(s=>s!=null))
        {set!.Clear();if(enabled)set.UnionWith(FeatureCatalog.All.Where(f=>f.Id!=99).Select(f=>f.Id));}
        if(enabled)DisabledGroups.Clear();
    }
    public bool NeedsSelfStatuses => !On(96) || new[]{37,38,51,53,54,55,60}.Any(id=>On(id));
    public bool NeedsEnemyStatuses(bool legacy) => legacy || !On(96) || Enumerable.Range(31,6).Any(id=>On(id))
        || new[]{38,40,41,45,88,89}.Any(id=>On(id))
        || (ExcludeGuard||ExcludeInvulnerable)&&(Enumerable.Range(20,5).Any(id=>On(id))||On(29));
    public bool NeedsOtherActors(bool legacy) => legacy || !On(96)
        || Enumerable.Range(16,30).Any(id=>id is not (25 or 26 or 30 or 37 or 39 or 42 or 43 or 44)&&On(id))
        || Enumerable.Range(56,20).Any(id=>On(id)) || On(83) || On(88) || On(89) || On(92);
    public int SnapshotInterval(bool legacy,int legacyInterval) => legacy?Math.Min(UpdateMs,legacyInterval):UpdateMs;
    public bool On(int id, bool? large = null)
    {
        var feature = FeatureCatalog.ById.GetValueOrDefault(id);
        if (feature is null || DisabledGroups.Contains(feature.Group)) return false;
        var set = id == 77 ? Features : ActiveFeatures(large);
        return set.Contains(id);
    }
    public bool NeedsMonitor => FeatureCatalog.All.Any(f => f.Id is >=16 and <=75 && f.Id is not (25 or 26 or 30 or 42 or 43 or 70 or 72) && On(f.Id))
        || On(83)||On(87)||On(88)||On(89)||On(92)||On(93)||On(100);
    public static float JobRange(uint job, float melee, float ranged)
        => job is 1 or 2 or 3 or 4 or 19 or 20 or 21 or 22 or 29 or 30 or 32 or 34 or 37 or 39 or 41 ? melee : ranged;
    public static float CommandRange(uint job,float requested,bool ally=false) => Math.Min(requested,ally?20:JobRange(job,3,20));
    public void Sanitize()
    {
        Features ??= []; DisabledGroups ??= [];
        Features.RemoveWhere(id => !FeatureCatalog.All.Any(f=>f.Id==id)); DisabledGroups.RemoveWhere(g=>g<0||g>7);
        float F(float v,float lo,float hi,float def) => float.IsFinite(v)?Math.Clamp(v,lo,hi):def;
        MeleeRange=F(MeleeRange,1,3,3); RangedRange=F(RangedRange,1,20,20); AllyRange=F(AllyRange,1,20,20);
        MuteSeconds=Math.Clamp(MuteSeconds,1,3600);
        AreaRadius=F(AreaRadius,1,30,5); NearbyRange=F(NearbyRange,3,100,20); Separation=F(Separation,3,100,20);
        Volume=F(Volume,0,1,.25f); LowHpPercent=F(LowHpPercent,1,99,30); LowMp=F(LowMp,0,10000,2000);
        RapidLossPercent=F(RapidLossPercent,1,100,25); ExpiringSeconds=F(ExpiringSeconds,.1f,5,1);
        StableSeconds=F(StableSeconds,0,5,.5f); BudgetMs=F(BudgetMs,.25f,10,2); StaleSeconds=F(StaleSeconds,.1f,2,.8f); TextSize=F(TextSize,12,40,18);
        UpdateMs=Math.Clamp(UpdateMs,50,1000); MaxActors=Math.Clamp(MaxActors,10,200); MaxDraw=Math.Clamp(MaxDraw,1,100);
        MinimumZantetsuken=Math.Clamp(MinimumZantetsuken,1,200);AlertCount=Math.Clamp(AlertCount,1,200);
        FocusCount=Math.Clamp(FocusCount,1,50);NearbyIncrease=Math.Clamp(NearbyIncrease,1,50);Disadvantage=Math.Clamp(Disadvantage,1,50);
        KuzushiTone=Math.Clamp(KuzushiTone,0,2);LbTone=Math.Clamp(LbTone,0,2);DangerTone=Math.Clamp(DangerTone,0,2);
        Vector4 Color(Vector4 c)=>new(F(c.X,0,1,1),F(c.Y,0,1,1),F(c.Z,0,1,1),F(c.W,0,1,1));
        GoodColor=Color(GoodColor);BadColor=Color(BadColor);OtherColor=Color(OtherColor);
        MatchNote=(MatchNote??"")[..Math.Min(MatchNote?.Length??0,200)];
    }
}
