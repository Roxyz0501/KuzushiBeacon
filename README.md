# Kuzushi Beacon

**PvP中、自分が付与した「崩し」の相手を、細い線と頭上マーカーで見つけやすくするDalamudプラグイン。**

作者: **Roxyz0501** / API 15 / バージョン 1.0.0.1。公開プレビューです。ゲーム内での見え方・実戦での付与者ID・実負荷は未確認です。

## 導入

Dalamudの `/xlsettings` → Experimental → Custom Plugin Repositories に、共通リポジトリを追加します。

```text
https://raw.githubusercontent.com/Roxyz0501/DalamudPluginRepo/main/repo.json
```

`/xlplugins` で **Kuzushi Beacon** を検索してインストールし、`/kuzushibeacon` またはプラグイン一覧の設定から開きます。

## 表示と設定

- 自分が侍で、PvPコンテンツ内にいる間だけ動作します。通常エリア・PvE・ウルヴズジェイル係船場・他ジョブでは停止します。
- デバフの付与者IDが自分と一致する「崩し」だけを表示します。自分の地天が切れていても、相手に自分の崩しが残っていれば対象です。
- 自分から相手への細い線、相手の頭上の下向き三角マーカーを、それぞれ個別にオン・オフできます。
- 線とマーカーそれぞれの色・透明度、線の太さ、マーカーの大きさ・高さ、表示距離、デバフ確認間隔を変更できます。
- 標準は線1.5 px、確認100 ms、表示距離50 mです。設定画面内のプレビューはPvP外でも利用できます。
- マーカーは足元に設定した高さを加えた位置へ描画します。骨格には追従しないため、種族やカメラに応じて高さを調整してください。
- 画面外・投影できない相手のマーカーは表示しません。線は自分と相手の両端が画面に投影できる場合に表示します。地形による遮蔽判定はありません。
- 表示は自分の画面だけです。共有ターゲットマーカーを設定したり、自動ターゲット・自動攻撃したりしません。

## 通信・負荷

ゲーム情報はDalamud公開APIから読み取り、ImGuiでローカル描画します。パケットの生成・送信・改変・再送、通信フック、ゲームメモリへの書き込み、ゲーム操作APIの実行はありません。プラグイン自身のHTTP通信・DBアクセス・テレメトリーもありません。支援リンクは明示クリック時だけブラウザーで開きます。

通常エリアではFrameworkの監視とオーバーレイ描画の購読を解除します。PvP中のデバフ走査は標準10回/秒。ゲーム全オブジェクトではなくプレイヤー用範囲を走査し、距離・生存状態で絞ってから状態一覧を読みます。描画時は一致した対象の位置だけを読み直し、状態一覧は再走査しません。最大200候補・200キャッシュ件。解除は次の確認時、期限切れは時間判定、退出・無効化・所有者変更ではキャッシュを消去します。

保存は変更時だけ600 ms遅延してまとめ、画面終了時にも確定します。閲覧・タブ移動・監視での書き込みはありません。計測結果と未確認事項は [VALIDATION.md](VALIDATION.md) を参照してください。

## 言語

日本語・English・Deutsch・Français・한국어・简体中文・繁體中文に対応。設定 → 言語設定で切り替えます。初回のみゲーム言語 → Dalamud UI言語 → Englishの順で決定し保存します。公開SDKに独立したランチャー言語APIがないため、その追加参照は行いません。手動で保存した言語は次回起動やキャラクター変更で上書きしません。Auto選択肢はありません。

7言語対応はプラグインの画面文字列に対するものです。ゲームデータの翻訳・変更は行いません。描画フォントはDalamud提供のNoto Sans CJKを使用し、フォントファイルは配布しません。

## 開発・出典

新規に開発した独立プラグインです。Dalamudと.NET 10 SDKを用意し、次のコマンドで検証できます。

```powershell
dotnet run --project Tests/Tests.csproj -c Release
dotnet build KuzushiBeacon/KuzushiBeacon.csproj -c Release
```

- [Dalamud](https://github.com/goatcorp/Dalamud) (goatcorp / AGPL-3.0): 公開ホストAPIのみを使用。ソース・バイナリーを同梱しません。
- [Bait Beacon](https://github.com/Roxyz0501/BaitBeacon) (Roxyz0501 / Apache-2.0): 言語正規化・翻訳辞書の基盤・フォント設定の一部を利用して本プラグイン向けに変更。共通タブはRoxyz0501の正式テンプレートをそのまま使用。
- [公式侍ジョブガイド](https://jp.finalfantasyxiv.com/jobguide/samurai/) のPvP説明を参照。
- [XIVAPI Status 3202](https://v2.xivapi.com/api/sheet/Status/3202?fields=Name,Description) でKuzushiの識別子を確認。実行時の外部API照会はありません。
- アイコンは `tools/Generate-Icon.ps1` による独自の幾何学図形です。ゲーム・第三者の画像素材を使用しません。

ライセンス: [Apache-2.0](KuzushiBeacon/LICENSE)。[NOTICE](KuzushiBeacon/NOTICE) を参照。

任意の支援: [Roxyz0501のKo-fi](https://ko-fi.com/roxyz0501)。支援の有無で機能は変わりません。
