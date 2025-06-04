# LoLClipCharp

LoLClipCharp は League of Legends のライブデータを監視し、特定のゲームイベントが発生した際に OBS のリプレイバッファを自動保存するツールです。主に以下のイベントに反応します。

- 集団戦 (死亡者数が設定値以上)
- 自分のマルチキル
- 自分のチャンピオンのデス

OBS WebSocket を利用してリプレイを保存するため、OBS 側で WebSocket サーバーを有効にしておく必要があります。

## 必要環境

- .NET 9.0 以上
- OBS 28 以降 (WebSocket 機能が有効であること)
- League of Legends クライアント (Live Client Data API が有効)

## ビルドと実行

```bash
# ビルド
$ dotnet build

# 実行
$ dotnet run --project LoLClip
```

実行すると LoL の試合開始を待機し、試合中に上記のイベントを検知すると自動で OBS にリプレイ保存を指示します。

## 設定ファイル

`LoLClip/config.json` で各種設定を変更できます。

- `AllGameDataUrl`: LoL Live Client Data API のエンドポイント
- `ObsWebSocketUrl`: OBS WebSocket の接続先 (例: `ws://localhost:4455`)
- `HistoryLimit`: 判定に用いる履歴数
- `MainLoopDelayMs`: ゲーム進行中のポーリング間隔 (ミリ秒)
- `GameStartDelayMs`: ゲーム開始待機中のポーリング間隔 (ミリ秒)
- `GameEndDelayMs`: ゲーム終了検知中のポーリング間隔 (ミリ秒)
- `TeamfightDeathThreshold`: 集団戦とみなす死亡者数の閾値
- `ObsBufferSize`: OBS WebSocket 受信バッファサイズ

## コード構成

- `Program.cs`: エントリーポイント。設定読み込みとメインループを実装
- `GameAsyncs.cs`: OBS 連携やゲーム状態取得などの非同期処理
- `CustomEventManager.cs`: イベント検知ロジックとハンドラ登録
- `AppConfig.cs`: 設定項目の定義

## 貢献方法

1. このリポジトリをフォークし、ローカルにクローンします。
2. ブランチを切って変更を加え、`dotnet build` でビルドが通ることを確認します。
3. プルリクエストを作成し、変更点を説明してください。

不具合報告や機能提案も歓迎します。

