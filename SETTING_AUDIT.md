# 1.1.0.3 follow-up

- Current catalog has 80 switches: legacy 34 merged into 33 in every saved profile. 39 moved to the shared job section (group 8).
- KuzushiDisplay / OwnKuzushiDisplay / KuzushiFullLbOnly gate own legacy highlights and support timer/other-Samurai labels. TimerColor / TimerSize / TimerHeight / ExpiringColor feed LabelAt; expiry is conditional on the timer being enabled.
- ZantetsukenSupport gates recommendation, head count and explicit LB commands. BestMarker / BestLine and their colors, sizes, widths, heights feed the recommendation drawing.
- SamuraiNotifications gates 33/35/40, independently from shared LB alert 39. Global notification/audio settings still apply.
- Shared exclusions feed marked snapshots, Zantetsuken.Included, timers, other-Samurai labels and eligibleHighlights cached at tracker scan time. Legacy line height is now configurable.
- New fields are sanitized. Old colors, styles and disabled category state migrate; old duplicate notification choices merge without enabling additional duplicate sounds.
- Other sidebar groups use named functional sections; details remain attached to their active feature. All editable colors use a picker without numeric input fields.

# 設定・処理の対応監査 — 1.1.0.2 / 2026-10-19

対象は全81支援スイッチ、共通数値・色設定、既存侍表示、コマンド、統合Ledger。以下のIDは監査用内部IDで、設定画面には表示しない。コードから処理接続を確認し、特定の回帰を自動テストした。実ゲームの全組合せを動作確認したという意味ではない。

## 修正した反映漏れ

- 使用中のコンテンツ別プロファイルと、画面のチェック変更先を一致させた。全オン・全オフも保存済み両プロファイルへ反映。
- 画面外追跡だけをオンにしても監視を開始。不要な自己・敵ステータス走査を分離。
- 死亡前メモ単独でも自己状態異常を収集。過去の匿名JSONとの読込互換性を維持。
- コマンド回数・失敗理由・死亡時人数・警告後生存時間・状態メモを振り返り画面で閲覧可能にした。
- エクスポートだけを使う場合も既存履歴を読み、最新20試合だけ出力。記録オフ時の保留記録の追加を防止。
- 低HPの生存計測を警告発生後に開始。
- LB要求状態を表示更新間隔から独立させ、要求・ゲージ消費の観測箇所で更新。
- 共通文字サイズをHUD一覧にも適用。
- 侍の監視間隔を共有スナップショットに反映し、侍表示全オフ時の余分な走査を停止。
- 画面内の敵だけを選ぶ設定が味方まで除外していた点を修正。
- 共通言語変更をLedgerへ即時反映。Ledger数値の直接入力を保存前に補正。
- 通知オフ・無効機能の古い通知を即時除去し、音を停止。
- 画面外方向表示の通常線分経路を画面内の場合だけに限定。
- 機能の直下に関連設定を配置し、オフ時は非表示。常時使えるコマンド設定と複数機能共有設定は明示。
- 全81機能に日英の詳細ツールチップ。既存の線・人数・マーカーにも説明を追加。

## 支援スイッチ

| 内部ID | 設定 | 消費する処理 |
|---|---|---|
| 4 | 画面内の敵だけを選択 | GameLowHpTarget.Eligible / SelectSupportTargetAsync（敵コマンドのみ） |
| 16 | 現在のターゲットまでの距離を表示 | SupportUpdateの観測値 → DrawSupportOverlayの個別On分岐 |
| 17 | 射程内・射程外で表示色を変える | SupportUpdateの観測値 → DrawSupportOverlayの個別On分岐 |
| 18 | 自分の攻撃範囲を円で表示 | SupportUpdateの観測値 → DrawSupportOverlayの個別On分岐 |
| 19 | ターゲット中心の範囲を円で表示 | SupportUpdateの観測値 → DrawSupportOverlayの個別On分岐 |
| 20 | 範囲内の敵人数 | SupportUpdateの観測値 → DrawSupportOverlayの個別On分岐 |
| 21 | 範囲内の低HP人数 | SupportUpdateの観測値 → DrawSupportOverlayの個別On分岐 |
| 22 | 巻き込み上位3候補 | SupportUpdateの観測値 → DrawSupportOverlayの個別On分岐 |
| 23 | 現在対象と最適対象の比較 | SupportUpdateの観測値 → DrawSupportOverlayの個別On分岐 |
| 24 | 人数条件の通知 | SupportUpdate → Alert → DrawSupportOverlay / PlayAlerts |
| 25 | 通知前の条件安定を確認 | Alert → AlertLatch.Observe（継続条件のみ） |
| 26 | 条件解除で通知を消す | Alert（条件解除時に表示削除） |
| 27 | 対象のHP減少速度 | SupportUpdateの観測値 → DrawSupportOverlayの個別On分岐 |
| 28 | 回復した対象を表示 | SupportUpdateの観測値 → DrawSupportOverlayの個別On分岐 |
| 29 | 攻撃候補一覧 | SupportUpdateの観測値 → DrawSupportOverlayの個別On分岐 |
| 30 | 候補を選んだ理由 | DrawSupportOverlay → attackCandidates（29が必要） |
| 31 | 敵の頭上に崩しの残り秒数を表示 | SupportUpdateの観測値 → DrawSupportOverlayの個別On分岐 |
| 32 | 崩しが切れる直前に色を変える | SupportUpdateの観測値 → DrawSupportOverlayの個別On分岐 |
| 33 | 新しく崩しが付いたら音で通知 | SupportUpdate → Alert → DrawSupportOverlay / PlayAlerts |
| 34 | 崩しが付いた敵の人数が増えたら通知 | SupportUpdate → Alert → DrawSupportOverlay / PlayAlerts |
| 35 | 崩しがすべて消えたら通知 | SupportUpdate → Alert → DrawSupportOverlay / PlayAlerts |
| 36 | 他の侍の崩しを区別 | SupportUpdateの観測値 → DrawSupportOverlayの個別On分岐 |
| 37 | 地天の残り時間 | SupportUpdateの観測値 → DrawSupportOverlayの個別On分岐 |
| 38 | 地天終了後の崩し残存 | SupportUpdateの観測値 → DrawSupportOverlayの個別On分岐 |
| 39 | LBが満タンになったら通知 | SupportUpdate → Alert → DrawSupportOverlay / PlayAlerts |
| 40 | LB満タンで崩しが付いたら通知 | SupportUpdate → Alert → DrawSupportOverlay / PlayAlerts |
| 41 | 最も多く巻き込める敵にマーカーを表示 | SupportUpdateの観測値 → DrawSupportOverlayの個別On分岐 |
| 43 | 指定人数以上を巻き込めるときだけ斬鉄剣を要求 | ExecuteZantetsukenAsync → Zantetsuken.Execute(minimum) |
| 44 | 斬鉄剣の待機・要求済み状態を表示 | ExecuteZantetsukenAsync / ObserveRequest → requestPhase → Row |
| 45 | 斬鉄剣の人数予測に関する注意を表示 | SupportUpdateの観測値 → DrawSupportOverlayの個別On分岐 |
| 46 | 低HP警告 | SupportUpdate → Alert → DrawSupportOverlay / PlayAlerts |
| 47 | HP急減警告 | SupportUpdate → Alert → DrawSupportOverlay / PlayAlerts |
| 48 | 低MP警告 | SupportUpdate → Alert → DrawSupportOverlay / PlayAlerts |
| 49 | 快気の残り回数目安 | SupportUpdateの観測値 → DrawSupportOverlayの個別On分岐 |
| 50 | 防御の使用可能表示 | SupportUpdateの観測値 → DrawSupportOverlayの個別On分岐 |
| 51 | 防御終了カウントダウン | SupportUpdateの観測値 → DrawSupportOverlayの個別On分岐 |
| 52 | 浄化の使用可能表示 | SupportUpdateの観測値 → DrawSupportOverlayの個別On分岐 |
| 53 | 浄化対象の状態を強調 | SupportUpdateの観測値 → DrawSupportOverlayの個別On分岐 |
| 54 | 状態異常の残り時間 | SupportUpdateの観測値 → DrawSupportOverlayの個別On分岐 |
| 55 | 耐性の残り時間 | SupportUpdateの観測値 → DrawSupportOverlayの個別On分岐 |
| 56 | 自分を狙う敵の観測数 | SupportUpdateの観測値 → DrawSupportOverlayの個別On分岐 |
| 57 | 集中攻撃の可能性 | SupportUpdate → Alert → DrawSupportOverlay / PlayAlerts |
| 58 | 近距離の敵増加警告 | SupportUpdate → Alert → DrawSupportOverlay / PlayAlerts |
| 59 | 回復詠唱中の接近警告 | SupportUpdate → Alert → DrawSupportOverlay / PlayAlerts |
| 60 | 死亡直前の状態メモ | NeedsSelfStatuses → preDeath → DeathRecord → DrawDeathRecords |
| 61 | 周囲の味方人数 | SupportUpdateの観測値 → DrawSupportOverlayの個別On分岐 |
| 62 | 周囲の敵味方人数 | SupportUpdateの観測値 → DrawSupportOverlayの個別On分岐 |
| 63 | 人数不利警告 | SupportUpdate → Alert → DrawSupportOverlay / PlayAlerts |
| 64 | 最寄りの味方までの距離 | SupportUpdateの観測値 → DrawSupportOverlayの個別On分岐 |
| 65 | 孤立警告 | SupportUpdate → Alert → DrawSupportOverlay / PlayAlerts |
| 66 | 味方集団の中心方向 | SupportUpdateの観測値 → DrawSupportOverlayの個別On分岐 |
| 67 | 指定した味方のマーカー | SupportUpdateの観測値 → DrawSupportOverlayの個別On分岐 |
| 68 | 追跡する味方との距離警告 | SupportUpdate → Alert → DrawSupportOverlay / PlayAlerts |
| 69 | 低HPの味方を強調 | SupportUpdateの観測値 → DrawSupportOverlayの個別On分岐 |
| 71 | 味方への支援射程 | SupportUpdateの観測値 → DrawSupportOverlayの個別On分岐 |
| 73 | 味方との対象一致人数 | SupportUpdateの観測値 → DrawSupportOverlayの個別On分岐 |
| 74 | 味方に狙われている敵 | SupportUpdateの観測値 → DrawSupportOverlayの個別On分岐 |
| 75 | 退避方向の目安 | SupportUpdateの観測値 → DrawSupportOverlayの個別On分岐 |
| 77 | コンテンツ別設定 | ReconcileLedger → CurrentLarge / ActiveFeatures / SetFeature |
| 78 | 戦闘中だけ詳細表示 | DrawSupportOverlay → detail（通知は維持） |
| 79 | 重要通知を優先 | PlayAlerts / 通知行の優先順位 |
| 80 | 通知の種類別SE | PlayAlerts → tone → LocalSound.Play |
| 82 | 色と記号を併用 | DrawSupportOverlayの距離記号 / 配色ボタン |
| 83 | 画面外の追跡対象の方向 | NeedsMonitor / NeedsOtherActors → trackedAlly → DrawDirection |
| 84 | 重なる表示をずらす | LabelAt → occupiedLabels |
| 85 | 遠い対象の表示を縮小 | LabelAt → 距離に応じたsize |
| 86 | 試合ごとのローカル記録 | Dutyイベント / FinishReview → reviewHistory → QueueReviewWrite |
| 87 | LB満タンの保持時間 | SupportUpdate → fullSeconds → 振り返り・ReviewRecord |
| 88 | 崩しとLBが揃った回数 | SupportUpdate → opportunities → 振り返り・ReviewRecord |
| 89 | 最大巻き込み人数 | LB満タン時FindBest → peakCoverage → 振り返り・ReviewRecord |
| 90 | 対象選択コマンドを使った回数を記録 | CountCommand → commandCounts → DrawCountDictionary / ReviewRecord |
| 91 | 対象なしの理由集計 | CountCommand → failureCounts → DrawCountDictionary / ReviewRecord |
| 92 | 死亡時の近隣人数 | SupportUpdate → DeathRecord近隣人数 → DrawDeathRecords |
| 93 | 低HP警告後の生存時間 | 低HP Alert発生 → lowHpSince → DeathRecord.Survival（46と通知が必要） |
| 94 | 設定比較用の試合メモ | FinishReview → Note / Settings（86が必要） |
| 95 | 匿名集計の書き出し | ReconcileLedgerで履歴読込 → QueueReviewWrite(export=true) |
| 96 | 使っていない機能の計算を省く | NeedsSelfStatuses / NeedsEnemyStatuses / NeedsOtherActors |
| 97 | 混雑時の処理予算 | SupportUpdateのMaxActors・BudgetMs / LabelAtのMaxDraw |
| 98 | 更新が止まった古い情報を表示から消す | DrawSupportOverlayのsupportAt・StaleSeconds判定 |
| 99 | 検証モード：候補を調べるだけで操作しない | GameLowHpTarget.Select / GameZantetsukenAction.TryExecute / assistの操作分岐 |
| 100 | 処理別の負荷表示 | RecordCost → costs / supportScans・Reads・Draws → DrawFeatureParameters |

## 値・色・既存設定

| 設定 | 消費する処理・条件 |
|---|---|
| MeleeRange / RangedRange / AllyRange | コマンド・距離色・候補・円。引数なし/autoで使用、明示引数は上限内で上書き |
| ExcludeGuard / ExcludeInvulnerable | GameLowHpTarget・assist・一般範囲候補 |
| ZantetsukenExcludeGuard / ZantetsukenExcludeInvulnerable | GameZantetsukenAction.Included、表示用FindBest候補 |
| AreaRadius / LowHpPercent | 一般範囲円・人数・低HP、自己警告・味方強調。共有設定と明記 |
| ExpiringSeconds / MinimumZantetsuken | 32の色分岐 / 43のコマンド条件 |
| LowMp / RapidLossPercent / FocusCount / NearbyIncrease | 48 / 47 / 57 / 58の通知条件 |
| NearbyRange / Separation / Disadvantage | 近隣集計・退避 / 68 / 63 |
| Notifications / Sound / Volume / MuteSeconds | 通知表示・再生 / LocalSound / 明示muteコマンド |
| KuzushiTone / LbTone / DangerTone | 80有効時のPlayAlerts。通知・SEが必要 |
| StableSeconds | 25有効時のAlertLatch |
| GoodColor / BadColor / OtherColor / TextSize | 支援の頭上ラベル・HUD・範囲・方向描画 |
| UpdateMs / MaxActors / MaxDraw / BudgetMs / StaleSeconds | 監視周期 / 97の走査上限・頭上表示上限・一般範囲予算 / 98期限 |
| MatchNote / SmallFeatures / LargeFeatures | 94の保存メモ / 77の有効プロファイル |
| Enabled / DisabledGroups | 監視・通知ゲート。明示コマンドは全体Enabledを尊重しカテゴリの表示停止と分離 |
| ShowLine / LineColor / LineWidth | 侍の線表示とプレビュー。LineWidthは一般範囲円にも使用 |
| ShowMarker / MarkerColor / MarkerSize / MarkerHeight | 侍マーカーとプレビュー |
| ShowMaxCount / CountColor / CountSize / CountHeight / CountOffsetY | LB満タン時の人数とプレビュー |
| MaxDistance / ScanIntervalMs | 侍の線・マーカーの表示距離 / 侍走査間隔。人数計算は表示距離に制限しない |
| LowHpRange / Version | 旧保存形式からの移行専用。現行の設定画面には表示しない |
| Language | 共通UI・旧UI・Ledgerは7言語。新支援・説明は日本語/英語、他言語では英語 |

## 統合Ledger

| 設定 | 消費する処理 |
|---|---|
| LedgerEnabled | ReconcileLedgerで生成・破棄 |
| OverlayEnabled / Locked / X / Y / Scale / Opacity | Ledger.Overlay.Draw |
| FeedRows / FeedSeconds | Overlayのイベント件数・経過時間判定 |
| EstimateAssists | Updateのゲージ観測・Overlayの推定表示 |
| DiscordEnabled / DiscordIncludeName / ProtectedWebhook | FinishのSaveJob・Worker・Webhook。テストは偽HTTP、実送信なし |
| プレビュー / 履歴ページ / CSV出力 | Draw、HistoryStore、Export。読み取りキャッシュの既存テストで確認 |

## 検証と限界

- 300 managed checks、56実ホストライフサイクル/接続チェック、2,781 Ledgerチェック。
- 477 headless UI frames：全カテゴリのオン/オフ、全81ツールチップの日英描画、左メニュー全ページ。実ImGuiクリックでプロファイル変更と詳細出現を確認。
- 無変更描画で設定書込0。PvP外でゲームオブジェクト読取0のテストを維持。
- 本番FFXIVの状態取得・投影・音声・アクション成功・実負荷は未検証。対応外の状態、未観測対象、遮蔽や命中は保証しない。
- 通常のクライアント操作経路を維持し、独自・不正パケットの送信処理を追加していない。
