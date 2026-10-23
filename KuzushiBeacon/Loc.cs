using Dalamud.Game;

namespace KuzushiBeacon;

public static class Loc
{
    public static readonly (string Code, string Native)[] Languages =
    [
        ("ja", "日本語"), ("en", "English"), ("de", "Deutsch"), ("fr", "Français"),
        ("ko", "한국어"), ("zh-Hans", "简体中文"), ("zh-Hant", "繁體中文")
    ];

    private static readonly Dictionary<string, string[]> Text = new()
    {
        ["PurifyFirst"] = ["実行を妨げる状態異常があるとき浄化を先に使う", "Use Purify first for removable disabling effects", "Bei entfernbaren Einschränkungen zuerst Läuterung nutzen", "Utiliser Purification avant une action bloquée", "해제 가능한 행동 불가 효과가 있으면 정화 먼저 사용", "存在可解除的行动限制时先使用净化", "存在可解除的行動限制時先使用淨化"],
        ["PurifyFirstHelp"] = ["斬鉄剣コマンドを押した時だけ、使用可能な浄化を要求します。解除と硬直終了を最大2秒待ち、条件を再確認して斬鉄剣を1回要求します。自動再試行はしません。対象選択だけ・検証モードでは使いません。", "Only on an explicit LB command: request available Purify, wait up to two seconds for removal and animation lock, then revalidate and request LB once. No retries; target-only and dry run never use Purify.", "Nur beim LB-Befehl: verfügbare Läuterung anfordern, bis zu 2 Sekunden auf Entfernung und Ende der Animationssperre warten, Bedingungen erneut prüfen und LB einmal anfordern. Keine Wiederholung; keine Aktion bei Zielwahl oder Prüfung.", "Sur commande LB uniquement : demander Purification si disponible, attendre au plus 2 secondes la dissipation et la fin du verrouillage, vérifier les conditions puis demander la LB une fois. Aucun nouvel essai ; aucune action en ciblage seul ou simulation.", "LB 명령을 눌렀을 때만 정화를 요청합니다. 해제와 경직 종료를 최대 2초 기다린 후 조건을 다시 확인하고 LB를 한 번 요청합니다. 재시도하지 않으며 대상 선택 전용 및 검증 모드에서는 사용하지 않습니다.", "仅在按下LB命令时请求可用的净化。最多等待2秒，确认状态解除和动作锁结束后重新检查条件，再请求一次LB。不自动重试；仅选目标和验证模式不会使用净化。", "僅在按下LB命令時請求可用的淨化。最多等待2秒，確認狀態解除和動作鎖結束後重新檢查條件，再請求一次LB。不自動重試；僅選目標和驗證模式不會使用淨化。"],
        ["FocusHud"] = ["HUDに人数を表示", "Show count in HUD", "Anzahl im HUD anzeigen", "Afficher le nombre dans le HUD", "HUD에 인원수 표시", "在HUD显示人数", "在HUD顯示人數"],
        ["FocusOverhead"] = ["自分の頭上に人数を表示", "Show count above yourself", "Anzahl über dir anzeigen", "Afficher le nombre au-dessus de vous", "자신의 머리 위에 인원수 표시", "在自己头顶显示人数", "在自己頭頂顯示人數"],
        ["FocusCountText"] = ["自分を狙う敵 {0}人", "Targeting you: {0}", "Gegner mit dir im Ziel: {0}", "Ennemis vous ciblant : {0}", "자신을 조준하는 적: {0}", "以自己为目标的敌人：{0}", "以自己為目標的敵人：{0}"],
        ["PurifyWaiting"] = ["浄化の解除待ち", "Waiting for Purify", "Warte auf Läuterung", "Attente de Purification", "정화 해제 대기 중", "等待净化解除", "等待淨化解除"],
        ["LowHpHelp"] = ["/pvplowhp : 射程内で現在HPの実数が最も少ない敵プレイヤーをターゲット（攻撃なし）。", "/pvplowhp: target the enemy player in range with the lowest current HP (no attack).", "/pvplowhp: Gegner in Reichweite mit den wenigsten aktuellen LP anvisieren (kein Angriff).", "/pvplowhp : cibler le joueur ennemi à portée avec le moins de PV actuels (sans attaque).", "/pvplowhp: 사거리 내 현재 HP가 가장 적은 적 플레이어 선택(공격 없음).", "/pvplowhp：选中范围内当前HP数值最低的敌方玩家（不攻击）。", "/pvplowhp：選取範圍內目前HP數值最低的敵方玩家（不攻擊）。"],
        ["LowHpRange"] = ["最低HPターゲットの射程", "Lowest-HP target range", "Reichweite für niedrigste LP", "Portée de ciblage des PV minimum", "최저 HP 대상 사거리", "最低HP目标选择范围", "最低HP目標選取範圍"],
        ["LowHpUnavailable"] = ["PvP Supporter: 射程内に選択できる敵がいません。", "PvP Supporter: no selectable enemy in range.", "PvP Supporter: Kein anwählbarer Gegner in Reichweite.", "PvP Supporter : aucun ennemi ciblable à portée.", "PvP Supporter: 사거리 내 선택 가능한 적이 없습니다.", "PvP Supporter：范围内没有可选择的敌人。", "PvP Supporter：範圍內沒有可選取的敵人。"],
        ["CountColor"] = ["人数の色・透明度", "Count color and opacity", "Zahlenfarbe und Deckkraft", "Couleur et opacité du nombre", "인원수 색상 및 불투명도", "人数颜色及透明度", "人數顏色及透明度"],
        ["CountSize"] = ["人数の文字サイズ", "Count text size", "Zahlengröße", "Taille du nombre", "인원수 글자 크기", "人数文字大小", "人數文字大小"],
        ["CountHeight"] = ["人数の高さ（足元から）", "Count height above feet", "Zahlenhöhe über den Füßen", "Hauteur du nombre depuis les pieds", "인원수 높이(발밑 기준)", "人数高度（从脚下）", "人數高度（從腳下）"],
        ["CountOffset"] = ["人数の上下位置（画面上）", "Count vertical screen offset", "Vertikaler Bildschirmversatz", "Décalage vertical à l’écran", "인원수 화면 세로 위치", "人数屏幕垂直偏移", "人數畫面垂直偏移"],
        ["CountPreview"] = ["頭上の人数をプレビュー（設定を開いている間のみ）", "Preview overhead count while settings are open", "Kopfzahl bei offenen Einstellungen anzeigen", "Aperçu au-dessus de la tête pendant les réglages", "설정이 열린 동안 머리 위 인원수 미리보기", "设置打开时预览头顶人数", "設定開啟時預覽頭頂人數"],
        ["ShowMaxCount"] = ["LB満タン時、自分の頭上に最大巻き込み人数を表示", "Show maximum Kuzushi count above you when LB is full", "Bei vollem LB maximale Kuzushi-Anzahl über dir anzeigen", "Afficher au-dessus de vous le maximum de cibles Kuzushi quand la LB est pleine", "LB가 가득 차면 자신의 머리 위에 최대 Kuzushi 대상 수 표시", "LB蓄满时在自己头顶显示最多可命中的崩破人数", "LB蓄滿時在自己頭頂顯示最多可命中的崩破人數"],
        ["ExecuteHelp"] = ["/kuzushizantetsuken : 自分の崩し対象を最も多く巻き込める相手を選び、斬鉄剣を1回試行します（マクロ使用可）。", "/kuzushizantetsuken: select the target covering the most of your Kuzushi targets and attempt Zantetsuken once (macros supported).", "/kuzushizantetsuken: Ziel mit den meisten eigenen Kuzushi-Treffern wählen und Zantetsuken einmal versuchen (Makros unterstützt).", "/kuzushizantetsuken : choisir la cible touchant le plus de vos cibles Kuzushi et tenter Zantetsuken une fois (macros acceptées).", "/kuzushizantetsuken: 자신의 Kuzushi 대상을 가장 많이 포함하는 적을 선택하고 참철검을 한 번 시도합니다(매크로 가능).", "/kuzushizantetsuken：选择能覆盖最多自身崩破目标的敌人，并尝试一次斩铁剑（支持宏）。", "/kuzushizantetsuken：選擇能涵蓋最多自身崩破目標的敵人，並嘗試一次斬鐵劍（支援巨集）。"],
        ["ExecuteUnavailable"] = ["PvP Supporter: 発動できる自分の崩し対象がいないか、現在は斬鉄剣を使用できません。", "PvP Supporter: no usable own-Kuzushi target, or Zantetsuken is unavailable.", "PvP Supporter: Kein gültiges eigenes Kuzushi-Ziel oder Zantetsuken nicht verfügbar.", "PvP Supporter : aucune cible Kuzushi personnelle valide, ou Zantetsuken indisponible.", "PvP Supporter: 사용 가능한 자신의 Kuzushi 대상이 없거나 참철검을 사용할 수 없습니다.", "PvP Supporter：没有可用的自身崩破目标，或当前无法使用斩铁剑。", "PvP Supporter：沒有可用的自身崩破目標，或目前無法使用斬鐵劍。"],
        ["StatusDiagnostics"] = ["累計：崩しの観測 {0} / 自分の付与者IDと一致 {1} / うち残り時間が無効・0以下 {2}", "Totals: Kuzushi observations {0} / own source matches {1} / of these, invalid or nonpositive durations {2}", "Gesamt: Kuzushi-Beobachtungen {0} / eigene Quelle {1} / davon ungültige oder nicht positive Restzeiten {2}", "Totaux : observations de Kuzushi {0} / source personnelle {1} / dont durées invalides ou non positives {2}", "누적: 붕괴 관측 {0} / 자신의 부여자 ID 일치 {1} / 그중 남은 시간이 무효 또는 0 이하 {2}", "累计：崩破观测 {0} / 自己的施加者 ID 匹配 {1} / 其中剩余时间无效或不大于 0 {2}", "累計：崩破觀測 {0} / 自己的施加者 ID 符合 {1} / 其中剩餘時間無效或不大於 0 {2}"],
        ["Explanation"] = ["PvP中、自分が付与した「崩し」の相手だけに表示します。他の侍の崩しは対象外です。", "In PvP, highlight only players with Kuzushi applied by you. Other samurai's effects are excluded.", "Im PvP werden nur Spieler mit deinem Kuzushi markiert. Effekte anderer Samurai werden ausgeschlossen.", "En JcJ, seuls les joueurs affectés par votre Kuzushi sont signalés. Les effets des autres samouraïs sont exclus.", "PvP에서 자신이 부여한 Kuzushi 대상만 표시합니다. 다른 사무라이의 효과는 제외됩니다.", "仅在 PvP 中标记被自己施加Kuzushi的玩家，不显示其他武士施加的效果。", "僅在 PvP 中標記被自己施加Kuzushi的玩家，不顯示其他武士施加的效果。"],
        ["Enabled"] = ["プラグインを有効にする", "Enable plugin", "Plugin aktivieren", "Activer le plugin", "플러그인 사용", "启用插件", "啟用外掛"],
        ["ShowLine"] = ["自分から敵への線", "Line from you to the enemy", "Linie von dir zum Gegner", "Ligne entre vous et l’ennemi", "자신과 적을 잇는 선", "从自己连接到敌人的线", "從自己連接到敵人的線"],
        ["LineColor"] = ["線の色・透明度", "Line color and opacity", "Linienfarbe und Deckkraft", "Couleur et opacité de la ligne", "선 색상 및 불투명도", "连线颜色及不透明度", "連線顏色及不透明度"],
        ["LineWidth"] = ["線の太さ", "Line width", "Linienbreite", "Épaisseur de la ligne", "선 두께", "线宽", "線寬"],
        ["ShowMarker"] = ["敵の頭上マーカー", "Marker above the enemy", "Markierung über dem Gegner", "Repère au-dessus de l’ennemi", "적 머리 위 표식", "敌人头顶标记", "敵人頭頂標記"],
        ["MarkerColor"] = ["マーカーの色・透明度", "Marker color and opacity", "Markierungsfarbe und Deckkraft", "Couleur et opacité du repère", "표식 색상 및 불투명도", "标记颜色及不透明度", "標記顏色及不透明度"],
        ["MarkerSize"] = ["マーカーの大きさ", "Marker size", "Markierungsgröße", "Taille du repère", "표식 크기", "标记大小", "標記大小"],
        ["MarkerHeight"] = ["マーカーの高さ", "Marker height", "Markierungshöhe", "Hauteur du repère", "표식 높이", "标记高度", "標記高度"],
        ["HeightHelp"] = ["足元からの高さです。種族やカメラに合わせて調整できます。表示は自分の画面だけで、ゲーム内の共有マーカーは操作しません。", "Height is measured from the feet. Adjust for character size and camera. Markers are local overlays; shared game markers are untouched.", "Die Höhe wird ab den Füßen gemessen. Passe sie an Größe und Kamera an. Die Anzeige ist lokal; gemeinsame Spielmarkierungen bleiben unverändert.", "La hauteur part des pieds. Ajustez-la selon la taille et la caméra. L’affichage est local et ne modifie pas les marqueurs partagés du jeu.", "발밑 기준 높이입니다. 체격과 카메라에 맞게 조절하세요. 자신의 화면에만 표시되며 게임의 공유 표식은 변경하지 않습니다.", "高度从脚下计算，可根据角色体型和镜头调整。仅在本机显示，不修改游戏内的共享标记。", "高度從腳下計算，可依角色體型和鏡頭調整。僅在本機顯示，不修改遊戲內的共享標記。"],
        ["Distance"] = ["表示距離の上限", "Maximum distance", "Maximale Entfernung", "Distance maximale", "최대 표시 거리", "最大显示距离", "最大顯示距離"],
        ["Interval"] = ["デバフの確認間隔", "Status check interval", "Intervall der Statusprüfung", "Intervalle de vérification", "상태 확인 간격", "状态检查间隔", "狀態檢查間隔"],
        ["PerformanceHelp"] = ["標準は100 ms。間隔を広げると負荷が減り、付与・解除の反映が遅くなります。PvP外・ウルヴズジェイル・侍以外では監視を停止します。", "Default: 100 ms. Longer intervals reduce work but delay application/removal detection. Monitoring stops outside PvP, in Wolves' Den, and on other jobs.", "Standard: 100 ms. Größere Intervalle senken die Last, verzögern aber die Erkennung. Außerhalb von PvP, in der Wolfshöhle und bei anderen Jobs ist die Überwachung aus.", "Par défaut : 100 ms. Un intervalle plus long réduit la charge mais retarde la détection. La surveillance s’arrête hors JcJ, à la Jetée de l’Antre des loups et avec les autres jobs.", "기본값은 100ms입니다. 간격이 길수록 부하가 줄지만 적용 및 해제 감지가 늦어집니다. PvP 밖, 늑대우리 부두, 다른 직업에서는 감시를 중지합니다.", "默认 100 毫秒。增大间隔可降低负载，但会延迟检测状态变化。PvP 外、狼狱停船场及非武士职业时停止监测。", "預設 100 毫秒。增大間隔可降低負載，但會延遲偵測狀態變化。PvP 外、狼獄停船場及非武士職業時停止監測。"],
        ["Preview"] = ["表示プレビュー", "Preview", "Vorschau", "Aperçu", "미리 보기", "预览", "預覽"],
        ["Active"] = ["状態：PvP監視中", "Status: monitoring PvP", "Status: PvP-Überwachung aktiv", "État : surveillance JcJ active", "상태: PvP 감시 중", "状态：正在监测 PvP", "狀態：正在監測 PvP"],
        ["Idle"] = ["状態：監視停止（対象の探索なし）", "Status: idle (no target scanning)", "Status: inaktiv (keine Zielsuche)", "État : inactif (aucune recherche de cible)", "상태: 중지됨 (대상 검색 없음)", "状态：已停止（不搜索目标）", "狀態：已停止（不搜尋目標）"],
        ["Faulted"] = ["状態：エラーのため停止。有効設定を切り替えると再開を試みます。", "Status: stopped after an error. Toggle Enable to retry.", "Status: nach einem Fehler gestoppt. Zum Wiederholen die Aktivierung umschalten.", "État : arrêt après une erreur. Désactivez puis réactivez pour réessayer.", "상태: 오류로 중지됨. 사용 설정을 전환하여 다시 시도하세요.", "状态：因错误停止。切换启用设置以重试。", "狀態：因錯誤停止。切換啟用設定以重試。"],
        ["DetectionMetrics"] = ["検知・描画の累計診断", "Cumulative detection and drawing diagnostics", "Kumulative Erkennungs- und Zeichendiagnose", "Diagnostic cumulatif de détection et de dessin", "감지 및 표시 누적 진단", "检测与绘制累计诊断", "偵測與繪製累計診斷"],
        ["DetectionMetricsValue"] = ["走査 {0} / プレイヤー候補 {1} / 状態一覧 {2} / 検知対象 {3} / 線描画 {4} / マーカー描画 {5} / 投影失敗 {6}", "Scans {0} / player candidates {1} / status lists {2} / detected targets {3} / lines drawn {4} / markers drawn {5} / projection failures {6}", "Suchen {0} / Spielerkandidaten {1} / Statuslisten {2} / erkannte Ziele {3} / gezeichnete Linien {4} / Markierungen {5} / Projektionsfehler {6}", "Recherches {0} / joueurs candidats {1} / listes d’états {2} / cibles détectées {3} / lignes tracées {4} / repères tracés {5} / échecs de projection {6}", "검색 {0} / 플레이어 후보 {1} / 상태 목록 {2} / 감지 대상 {3} / 선 표시 {4} / 표식 표시 {5} / 투영 실패 {6}", "扫描 {0} / 玩家候选 {1} / 状态列表 {2} / 检测目标 {3} / 绘制线条 {4} / 绘制标记 {5} / 投影失败 {6}", "掃描 {0} / 玩家候選 {1} / 狀態列表 {2} / 偵測目標 {3} / 繪製線條 {4} / 繪製標記 {5} / 投影失敗 {6}"],
        ["Metrics"] = ["累計：探索 {0} 回 / 候補 {1} 件 / 状態一覧 {2} 回｜直近 {3:F3} ms", "Totals: {0} scans / {1} candidates / {2} status lists | Last: {3:F3} ms", "Gesamt: {0} Suchen / {1} Kandidaten / {2} Statuslisten | Zuletzt: {3:F3} ms", "Total : {0} recherches / {1} candidats / {2} listes d’états | Dernière : {3:F3} ms", "누적: 검색 {0}회 / 후보 {1}명 / 상태 목록 {2}회 | 최근: {3:F3}ms", "累计：扫描 {0} 次 / 候选 {1} 个 / 状态列表 {2} 次｜最近 {3:F3} 毫秒", "累計：掃描 {0} 次 / 候選 {1} 個 / 狀態列表 {2} 次｜最近 {3:F3} 毫秒"],
        ["CommandHelp"] = ["PvP Supporter を開きます。", "Open PvP Supporter.", "PvP Supporter öffnen.", "Ouvrir PvP Supporter.", "PvP Supporter을 엽니다.", "打开 PvP Supporter。", "開啟 PvP Supporter。"],
        ["Settings"] = ["設定", "Settings", "Einstellungen", "Paramètres", "설정", "设置", "設定"],
        ["LanguageSettings"] = ["言語設定", "Language settings", "Spracheinstellungen", "Paramètres de langue", "언어 설정", "语言设置", "語言設定"],
        ["Support"] = ["支援", "Support", "Unterstützen", "Soutien", "후원", "支持", "支援"],
        ["Language"] = ["言語 / Language", "Language / 言語", "Sprache / Language", "Langue / Language", "언어 / Language", "语言 / Language", "語言 / Language"],
        ["LanguageHelp"] = ["表示言語を選択します。変更はすぐに反映され、次回起動後も保持されます。", "Choose the display language. Changes apply immediately and remain after restarting.", "Wähle die Anzeigesprache. Die Änderung gilt sofort und bleibt nach dem Neustart erhalten.", "Choisissez la langue d’affichage. Le changement s’applique aussitôt et reste enregistré après le redémarrage.", "표시 언어를 선택하세요. 변경 사항은 즉시 적용되며 다시 시작해도 유지됩니다.", "选择显示语言。更改会立即生效，并在重新启动后保留。", "選擇顯示語言。變更會立即生效，重新啟動後仍會保留。"],
        ["SupportText"] = ["Roxyz0501 を任意で支援できます。", "You can optionally support Roxyz0501.", "Du kannst Roxyz0501 freiwillig unterstützen.", "Vous pouvez soutenir Roxyz0501 si vous le souhaitez.", "원한다면 Roxyz0501을 후원할 수 있습니다.", "你可以自愿支持 Roxyz0501。", "你可以自願支持 Roxyz0501。"],
        ["SupportHeading"] = ["任意の支援", "Optional support", "Freiwillige Unterstützung", "Soutien facultatif", "선택적 후원", "自愿支持", "自願支持"],
        ["SupportRecipient"] = ["支援先: Roxyz0501", "Recipient: Roxyz0501", "Empfänger: Roxyz0501", "Bénéficiaire : Roxyz0501", "후원 받는 사람: Roxyz0501", "收款人：Roxyz0501", "受款人：Roxyz0501"],
        ["OpenKoFi"] = ["Ko-fi を開く", "Open Ko-fi", "Ko-fi öffnen", "Ouvrir Ko-fi", "Ko-fi 열기", "打开 Ko-fi", "開啟 Ko-fi"],
    };

    public static IEnumerable<string> AllStrings => Text.Values.SelectMany(x => x);
    public static string FontText => string.Concat(AllStrings) + string.Concat(Languages.Select(x => x.Native))
        + "—…\u202F\u00A0";
    public static ClientLanguage GameDataLanguage(string? code) => code switch
    {
        "ja" => ClientLanguage.Japanese,
        "de" => ClientLanguage.German,
        "fr" => ClientLanguage.French,
        _ => ClientLanguage.English,
    };

    public static bool IsSupported(string? code) => Languages.Any(x => x.Code == code);
    public static string? FromGame(ClientLanguage? value) => value switch
    {
        ClientLanguage.Japanese => "ja",
        ClientLanguage.English => "en",
        ClientLanguage.German => "de",
        ClientLanguage.French => "fr",
        _ => null,
    };

    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var value = raw.Trim().Replace('_', '-').ToLowerInvariant();
        if (value is "ja" or "ja-jp") return "ja";
        if (value is "en" or "en-us" or "en-gb") return "en";
        if (value is "de" or "de-de") return "de";
        if (value is "fr" or "fr-fr") return "fr";
        if (value is "ko" or "ko-kr") return "ko";
        if (value is "zh-hans" or "zh-cn" or "zh-sg") return "zh-Hans";
        if (value is "zh-hant" or "zh-tw" or "zh-hk" or "zh-mo") return "zh-Hant";
        return null;
    }

    public static string Get(string? code, string key)
    {
        if (!Text.TryGetValue(key, out var values)) return key;
        var index = Array.FindIndex(Languages, x => x.Code == code);
        return values[index < 0 ? 1 : index];
    }

    public static void CheckCompleteness()
    {
        foreach (var (key, values) in Text)
        {
            if (values.Length != Languages.Length || values.Any(string.IsNullOrWhiteSpace))
                throw new InvalidOperationException($"Incomplete localization: {key}");
            var expected = CountParameters(values[1]);
            if (values.Any(x => CountParameters(x) != expected))
                throw new InvalidOperationException($"Localization format mismatch: {key}");
        }
    }

    private static int CountParameters(string value)
        => System.Text.RegularExpressions.Regex.Matches(value, @"\{\d+(?::[^}]+)?\}").Count;
}


