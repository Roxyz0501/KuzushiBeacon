namespace KuzushiBeacon;

internal static class CommandFeedback
{
    private static readonly Dictionary<string,string[]> Text = new()
    {
        ["conditions"] = ["実行条件を満たしていません。", "Execution conditions are not met.", "Ausführungsbedingungen nicht erfüllt.", "Conditions d’exécution non remplies.", "실행 조건을 충족하지 않습니다.", "未满足执行条件。", "未滿足執行條件。"],
        ["disabled"] = ["プラグインまたは斬鉄剣サポートがオフです。", "Plugin or Zantetsuken support is disabled.", "Plugin oder Zantetsuken-Hilfe deaktiviert.", "Plugin ou aide Zantetsuken désactivé.", "플러그인 또는 참철검 지원이 꺼져 있습니다.", "插件或斩铁剑支持已关闭。", "外掛或斬鐵劍支援已關閉。"],
        ["not pvp"] = ["PvPコンテンツ内ではありません。", "Not inside a PvP duty.", "Nicht in einem PvP-Inhalt.", "Hors d’un contenu JcJ.", "PvP 콘텐츠 내부가 아닙니다.", "当前不在PvP副本内。", "目前不在PvP副本內。"],
        ["player unavailable"] = ["自分の情報を取得できません。", "Player information is unavailable.", "Spielerinformationen fehlen.", "Informations du joueur indisponibles.", "자신의 정보를 가져올 수 없습니다.", "无法获取自身信息。", "無法取得自身資訊。"],
        ["dead"] = ["戦闘不能状態です。", "You are KO'd.", "Du bist kampfunfähig.", "Vous êtes K.O.", "전투 불능 상태입니다.", "当前处于无法战斗状态。", "目前處於無法戰鬥狀態。"],
        ["not samurai"] = ["侍ではありません。", "Current job is not Samurai.", "Aktueller Job ist nicht Samurai.", "Le job actuel n’est pas Samouraï.", "현재 직업이 사무라이가 아닙니다.", "当前职业不是武士。", "目前職業不是武士。"],
        ["gauge unavailable"] = ["LBゲージを確認できません。", "Cannot read the LB gauge.", "LB-Leiste nicht lesbar.", "Jauge LB illisible.", "LB 게이지를 확인할 수 없습니다.", "无法确认LB量谱。", "無法確認LB量譜。"],
        ["LB not full"] = ["LBが満タンではありません。", "LB gauge is not full.", "LB-Leiste nicht voll.", "Jauge LB non pleine.", "LB 게이지가 가득 차지 않았습니다.", "LB尚未蓄满。", "LB尚未蓄滿。"],
        ["no eligible Kuzushi"] = ["観測範囲に自分の崩しが付いた有効な敵がいません。", "No valid enemy with your Kuzushi was observed.", "Kein gültiger Gegner mit deinem Kuzushi beobachtet.", "Aucun ennemi valide avec votre Kuzushi observé.", "관측 범위에 자신의 무너짐이 적용된 유효한 적이 없습니다.", "观测范围内没有带有自身崩破的有效敌人。", "觀測範圍內沒有帶有自身崩破的有效敵人。"],
        ["excluded"] = ["自分の崩し対象が除外設定などで候補から外れました。", "Your Kuzushi targets were excluded by filters or became invalid.", "Deine Kuzushi-Ziele wurden ausgeschlossen oder ungültig.", "Vos cibles Kuzushi sont exclues ou devenues invalides.", "자신의 무너짐 대상이 제외 설정 등으로 후보에서 제외되었습니다.", "自身崩破目标被筛选设置排除或已失效。", "自身崩破目標被篩選設定排除或已失效。"],
        ["range"] = ["射程内に選択可能な自分の崩し対象がいません。", "No selectable own-Kuzushi target is in range.", "Kein eigenes Kuzushi-Ziel in Reichweite.", "Aucune cible avec votre Kuzushi à portée.", "사거리 내 선택 가능한 자신의 무너짐 대상이 없습니다.", "射程内没有可选择的自身崩破目标。", "射程內沒有可選取的自身崩破目標。"],
        ["line of sight or target"] = ["射程付近の崩し対象に対し、遮蔽または対象条件の判定が通りません。", "Nearby Kuzushi targets failed native range/line-of-sight or target checks.", "Nahe Kuzushi-Ziele scheitern an Reichweite, Sichtlinie oder Zielprüfung.", "Les cibles proches échouent au contrôle de portée, visibilité ou validité.", "근처 무너짐 대상이 사거리·시야 또는 대상 조건을 충족하지 않습니다.", "附近崩破目标未通过射程、视线或目标条件判定。", "附近崩破目標未通過射程、視線或目標條件判定。"],
        ["minimum coverage"] = ["巻き込み人数が設定した最低人数に達していません。", "Coverage is below the configured minimum.", "Zielanzahl unter dem eingestellten Minimum.", "Nombre de cibles inférieur au minimum configuré.", "범위 내 인원이 설정한 최소 인원보다 적습니다.", "覆盖人数未达到设置的最低人数。", "涵蓋人數未達到設定的最低人數。"],
        ["target changed"] = ["対象が消失または選択不可になりました。", "Target disappeared or became unavailable.", "Ziel verschwunden oder nicht mehr anwählbar.", "Cible disparue ou indisponible.", "대상이 사라졌거나 선택할 수 없습니다.", "目标消失或已无法选择。", "目標消失或已無法選取。"],
        ["native unavailable"] = ["ゲームのアクション処理を取得できません。", "Game action manager is unavailable.", "Aktionsverwaltung des Spiels nicht verfügbar.", "Gestionnaire d’actions du jeu indisponible.", "게임 행동 처리 정보를 가져올 수 없습니다.", "无法获取游戏动作处理模块。", "無法取得遊戲動作處理模組。"],
        ["bind"] = ["バインド状態を確認しました。", "Bind was present.", "Fesselung festgestellt.", "Entrave présente.", "속박 상태가 확인되었습니다.", "检测到止步状态。", "偵測到止步狀態。"],
        ["silence"] = ["沈黙状態を確認しました。", "Silence was present.", "Schweigen festgestellt.", "Silence présent.", "침묵 상태가 확인되었습니다.", "检测到沉默状态。", "偵測到沉默狀態。"],
        ["stun"] = ["スタン状態を確認しました。", "Stun was present.", "Betäubung festgestellt.", "Étourdissement présent.", "기절 상태가 확인되었습니다.", "检测到眩晕状态。", "偵測到眩暈狀態。"],
        ["sleep"] = ["睡眠状態を確認しました。", "Sleep was present.", "Schlaf festgestellt.", "Sommeil présent.", "수면 상태가 확인되었습니다.", "检测到睡眠状态。", "偵測到睡眠狀態。"],
        ["freeze"] = ["氷結状態を確認しました。", "Deep Freeze was present.", "Tiefgefroren festgestellt.", "Gel présent.", "빙결 상태가 확인되었습니다.", "检测到冻结状态。", "偵測到凍結狀態。"],
        ["transform"] = ["変身状態を確認しました。", "Miracle of Nature was present.", "Verwandlung festgestellt.", "Métamorphose présente.", "변신 상태가 확인되었습니다.", "检测到变身状态。", "偵測到變身狀態。"],
        ["control"] = ["行動制限状態を確認しました。", "An action-restricting effect was present.", "Handlungseinschränkung festgestellt.", "Restriction d’action présente.", "행동 제한 상태가 확인되었습니다.", "检测到行动限制状态。", "偵測到行動限制狀態。"],
        ["casting"] = ["詠唱中です。", "Casting is active.", "Zauber wird gewirkt.", "Incantation en cours.", "시전 중입니다.", "正在咏唱。", "正在詠唱。"],
        ["animation lock"] = ["アクション硬直中です。", "Animation lock is active.", "Animationssperre aktiv.", "Verrouillage d’animation actif.", "행동 경직 중입니다.", "正处于动作锁定中。", "正處於動作鎖定中。"],
        ["native"] = ["ゲーム側の使用不可コード: {0}。", "Game unavailability code: {0}.", "Spiel-Fehlercode: {0}.", "Code d’indisponibilité du jeu : {0}.", "게임 사용 불가 코드: {0}.", "游戏侧不可用代码：{0}。", "遊戲端不可用代碼：{0}。"],
        ["action rejected"] = ["ゲーム側が実行要求を受け付けませんでした（詳細理由は取得できません）。", "Game did not accept the request; detailed reason is unavailable.", "Anfrage vom Spiel nicht angenommen; genaue Ursache unbekannt.", "Demande non acceptée par le jeu ; raison détaillée inconnue.", "게임이 실행 요청을 수락하지 않았습니다. 자세한 이유는 알 수 없습니다.", "游戏未接受执行请求，无法获取详细原因。", "遊戲未接受執行請求，無法取得詳細原因。"],
        ["queue cancelled"] = ["LBの先行入力が解除され、発動を確認できませんでした。", "LB queue cleared without observed execution.", "LB-Warteschlange ohne bestätigte Ausführung beendet.", "File LB annulée sans exécution observée.", "LB 선입력이 해제되어 발동을 확인하지 못했습니다.", "LB预输入已取消，未确认发动。", "LB預輸入已取消，未確認發動。"],
        ["unconfirmed"] = ["要求後3秒以内にLB消費を確認できませんでした。発動失敗とは断定できません。", "LB consumption was not observed within 3 seconds; failure is not confirmed.", "LB-Verbrauch binnen 3 Sekunden nicht beobachtet; Fehlschlag nicht bestätigt.", "Consommation LB non observée sous 3 secondes ; échec non confirmé.", "요청 후 3초 이내 LB 소모를 확인하지 못했습니다. 실패로 단정할 수 없습니다.", "请求后3秒内未观察到LB消耗，不能确定发动失败。", "請求後3秒內未觀察到LB消耗，不能確定發動失敗。"],
        ["internal error"] = ["処理中にエラーが発生しました。詳細はDalamudログを確認してください。", "Internal error; see the Dalamud log.", "Interner Fehler; siehe Dalamud-Protokoll.", "Erreur interne ; consultez le journal Dalamud.", "처리 오류가 발생했습니다. Dalamud 로그를 확인하세요.", "处理发生错误，请查看Dalamud日志。", "處理發生錯誤，請查看Dalamud日誌。"],
    };
    public static string FontText => string.Concat(Text.Values.SelectMany(v=>v));
    public static string StatusReason(uint id) => id switch
    {
        1343 => "stun", 1345 or 3134 => "bind", 1347 => "silence", 1348 => "sleep",
        3219 => "freeze", 3085 => "transform", 3174 => "control", _ => ""
    };
    public static string Format(string? language, string reason)
    {
        if(string.IsNullOrWhiteSpace(reason))reason="action rejected";
        var index = Array.FindIndex(Loc.Languages,l=>l.Code==language);
        if(index<0)index=1;
        return string.Join(" ",reason.Split('|',StringSplitOptions.RemoveEmptyEntries).Distinct().Select(r=>
        {
            var native = r.StartsWith("native:",StringComparison.Ordinal);
            var key = native ? "native" : r;
            if(!Text.TryGetValue(key,out var values))values=Text["action rejected"];
            return native ? string.Format(values[index],r[7..]) : values[index];
        }));
    }
}
