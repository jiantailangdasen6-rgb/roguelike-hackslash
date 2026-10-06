# 技術設計書

> ステータス: v0.2（2026-10-06）— バンサバ型・オンライン協力の試作（P1）に合わせて更新
> 企画書: [01_GameDesign.md](01_GameDesign.md)

---

## 1. 環境・パッケージ

| 項目 | 選定 | 備考 |
|---|---|---|
| Unity | 6000.6.2f1 | |
| 描画 | URP 2D | 2D テンプレートから作成 |
| 通信 | **Netcode for GameObjects 2.13.3** ＋ Unity Transport | 3.x は Unity 6.7 以降が必要なため 2.x 系 |
| 入力 | Input System | PC / パッド / タッチ（On-Screen Controls が仮想ゲームパッドとして入力） |
| カメラ | Cinemachine 3 | 自分のプレイヤーを追従、アリーナ境界で Confine |
| UI | uGUI（コードで組み立て） | 日本語は OS フォントを動的に使用（フォント同梱なし） |
| テスト | Unity Test Framework | EditMode（ロジック）/ PlayMode（ソロ実起動）/ ビルド版の 3 プロセス通信テスト |

---

## 2. 全体構成

```
┌──────────────── ホスト（= サーバー兼プレイヤー）────────────────┐
│  RunSimulation（純 C#・Unity 非依存）                              │
│    部屋進行 / 敵の出現と移動 / 武器 / 被ダメ / 経験値 / レベルアップ / ドロップ │
│        ▲ 各プレイヤーの位置                │ イベント列・敵の位置            │
│  RunDirector（NetworkBehaviour, 30Hz）     ▼                              │
└──────────────┬───────────────────────────────┬──────────────────┘
               │ NetworkTransform（位置はオーナー権限）│ 名前付きメッセージ
               ▼                                ▼
┌──────────── 各クライアント（ホスト自身も含む）─────────────────────┐
│  PlayerMover（自分の移動だけ計算）                                     │
│  EnemyView / EventPresenter / LootSystem / HUD / LevelUpView …（表示のみ）│
└──────────────────────────────────────────────────────────┘
```

- **ゲームのロジックはすべてホストの `RunSimulation` で計算**する（チート耐性より実装の単純さと同期ズレの無さを優先）
- `RunSimulation` は Unity にも通信にも依存しない純 C# → EditMode テストで高速に検証できる
- プレイヤーの**移動だけはオーナー権限**（各自の端末で即座に反映 → 操作が遅延しない）。ホストは受け取った位置で当たり判定する

### 2.1 同期の方式

| データ | 方式 | 理由 |
|---|---|---|
| プレイヤー位置 | `NetworkTransform`（Owner 権限・XY のみ） | 操作レスポンス重視 |
| HP・ダウン・移動速度・オーラ半径 | `NetPlayer` の NetworkVariable | 変化した時だけ送られる |
| 部屋・目標・チームLv・経験値・一時停止 | `RunDirector` の NetworkVariable | 同上 |
| 敵の位置（最大 350 体） | **スナップショット**: 1体 7 バイトに量子化、100 体ずつ分割して Unreliable で 15Hz | 敵 1 体ずつを NetworkObject にすると重すぎるため |
| 攻撃・被弾・撃破・ジェム・ドロップ・レベルアップ等 | **イベント列**（`SimEvent` を種類ごとに必要な項目だけ詰めて送る）Reliable で毎 tick | 表示は各端末で再現（斧の軌道などは開始位置と速度から計算） |
| 装備の効果 | オーナー → ホストへ RPC（`SubmitEquipmentRpc`） | 装備は各自の端末に保存されているため |
| レベルアップの選択 | クライアント → ホストへ RPC（`ChooseUpgradeRpc`） | |

- 装備ドロップは**乱数シードだけ**を送り、受け取った本人の端末で `ItemGenerator` が同じアイテムを再生成する（各自に個別ドロップ）

### 2.2 接続

- ロビーで「ソロ / 部屋を作る / IP で参加」。最大 3 人、ゲーム開始後の途中参加は不可（接続承認で拒否）
- ポート 7777（UDP）。試作は同じ Wi-Fi 内のみ。インターネット越しは P4 で Unity Relay ＋ ルームコード方式に切り替える予定

---

## 3. フォルダ構成

```
Assets/
├── _Project/
│   ├── Art/            仮素材（ビルダーが生成）
│   ├── Resources/      GameArt, NetPlayer.prefab, NetworkManager.prefab（実行時に読み込み）
│   ├── Scenes/         Lobby, Game（ビルダーが生成）
│   ├── Settings/       GameControls.inputactions, NetworkPrefabs
│   └── Scripts/
│       ├── Core/       Game.Core — 純 C#。Stats / Items / Run（武器・パッシブ・強化）/ Sim（RunSimulation 他）
│       ├── Gameplay/   Game.Gameplay — Net（RunDirector, NetPlayer, 同期コーデック）/ View / UI / Session
│       ├── UI/         Game.UI — MobileControls
│       └── Editor/     Game.Editor — PrototypeBuilder（シーン・プレハブ生成）, PlayerBuild
└── Tests/
    ├── EditMode/       ロジックのテスト
    └── PlayMode/       ソロで実際にランを起動するテスト
```

- シーン・プレハブ・仮素材は **`PrototypeBuilder` がコードから生成**する（手で YAML を編集しない）

---

## 4. 主要クラス

| クラス | 役割 |
|---|---|
| `RunSimulation` | ラン全体。`Step(dt)` で 1 tick 進め、`Events` に起きたことを積む |
| `EnemySimulation` | 敵の配列＋一様グリッドで近傍検索。Unity 物理を使わない |
| `SimPlayer` | ホスト側のプレイヤー状態（HP・ステータス・武器・パッシブ） |
| `StatSheet` | (基本値 + 加算) × (1 + %加算) × Π(1 + %乗算)。装備・パッシブ共通 |
| `ItemGenerator` / `Stash` | シードからのアイテム生成 / 所持品と装備（JSON で端末に保存） |
| `UpgradeRoller` | レベルアップ 3 択の抽選と適用 |
| `RunDirector` | ホストでシミュレーションを回し、状態とイベントを配信。全端末でイベントを受信して `EventReceived` を発火 |
| `NetPlayer` / `PlayerMover` | プレイヤーのアバター / オーナー側の移動・ダッシュ |
| `GameSession` | ソロ・ホスト・参加・ロビー復帰 |

---

## 5. PC / スマホ両対応

| 項目 | 方針 |
|---|---|
| 入力 | Input Actions（Keyboard&Mouse / Gamepad）。タッチは仮想ゲームパッドなのでゲーム側はタッチを意識しない |
| 画面 | 横画面固定。CanvasScaler 基準 1920×1080。Safe Area は ugui 標準の `SafeArea` |
| 性能 | 敵は物理なし・SpriteRenderer をプールで再利用。敵上限 350 |
| バックグラウンド | `runInBackground = true`（ホストが裏に回っても進行を止めない） |

---

## 6. テストと検証

| 種類 | 内容 | 実行 |
|---|---|---|
| EditMode | ステータス計算・アイテム生成・所持品・強化抽選・敵 AI・ラン進行（部屋クリア、ダウン/蘇生、全滅、レベルアップ待ち） | `Tools/RunTests.ps1 -Platform EditMode` |
| PlayMode | ロビーからソロ開始 → ラン開始・プレイヤー生成・敵表示・イベント受信 | `Tools/RunTests.ps1 -Platform PlayMode` |
| 通信 | Windows ビルドをホスト＋クライアント 2 で起動、ボット操作で 1 分プレイし状態を照合・スクショ | `Tools/NetTest.ps1` |
