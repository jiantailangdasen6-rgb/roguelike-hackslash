# 技術設計書

> ステータス: v0.3（2026-10-11）— 手動アクション版（P2）
> 企画書: [01_GameDesign.md](01_GameDesign.md)

---

## 1. 環境・パッケージ

| 項目 | 選定 | 備考 |
|---|---|---|
| Unity | 6000.6.2f1 / URP 2D | 2D ライト、Pixel Perfect Camera（480×270 を整数倍拡大）、ポストエフェクト（Bloom・Vignette・色調） |
| 通信 | Netcode for GameObjects 2.13.3 ＋ Unity Transport | 3.x は Unity 6.7 以降が必要 |
| 入力 | Input System | KB+M（マウス照準）/ ゲームパッド（右スティック照準）/ 将来のタッチ |
| カメラ | Cinemachine 3 | 追従・境界・Pixel Perfect 拡張・画面揺れ（CameraOffset） |
| UI | uGUI（コード生成） | 本文=OS のゴシック、見出し=OS の明朝（フォント同梱なし） |
| テスト | Unity Test Framework | EditMode / PlayMode / ビルド版の 3 プロセス通信テスト |

---

## 2. 全体構成

```
┌────────────── ホスト ─────────────────────────────────────┐
│ RunSimulation（純 C#）                                       │
│   RunSimulation.cs          部屋・ウェーブ・魂/経験値・パーク選択     │
│   RunSimulation.Combat.cs   3クラスの技・弾・状態異常・連携（砕き等）  │
│   RunSimulation.Enemies.cs  敵 AI（予兆→攻撃→硬直）・被ダメージ       │
│        ▲ 位置（NetworkTransform）・入力（Unreliable RPC, 30Hz）      │
│ RunDirector（30Hz）── NetworkVariable / イベント列 / 敵スナップショット │
└─────────────────────────────────────────────────────────┘
          ▼                                  ▼
┌──────────── 全端末（ホスト含む）──────────────────────────────┐
│ PlayerMover（自キャラの移動・回避・突進・照準）                   │
│ NetPlayer / EnemyView / EventPresenter / HUD / PerkView …     │
└─────────────────────────────────────────────────────────┘
```

- **ロジックはすべてホストの `RunSimulation`**。Unity 非依存なので EditMode テストで検証
- **移動はオーナー権限**（回避・突進斬りの移動も自分の端末で即時）。ホストは届いた位置で判定
- 入力（照準ベクトル・攻撃押しっぱなし・スキル/回避の押下カウンタ）を毎 tick 送る。押下はカウンタ方式なので、パケットが落ちても押下は失われない

### 同期

| データ | 方式 |
|---|---|
| プレイヤー位置・向き | NetworkTransform（Owner）＋ Owner 書き込みの NetworkVariable（向き） |
| HP・シールド・クールダウン・ダウン・バフ | NetPlayer の NetworkVariable（ホストが書く） |
| 部屋・ウェーブ・チーム Lv・経験値・一時停止・討伐数 | RunDirector の NetworkVariable |
| 報酬の門（種類・試練・全員が立っている門・進捗）、試練部屋、門の報酬の魂の欠片 | RunDirector の NetworkVariable（`DoorsPacked` は `Door.Pack` の int、門の位置は `RunSimulation.DoorPosition` で各自計算） |
| 敵（位置・アニメ状態・向き・状態異常・HP 割合） | スナップショット：1 体 9 バイト、100 体ごとに分割、Unreliable 15Hz |
| 攻撃・弾・予兆・ヒット・撃破・範囲効果・魂・パーク提示（`Int` = 提示の理由 `ChoiceKind`）・報酬受け取りなど | イベント列（`SimEvent` を種類ごとに必要項目だけ詰める）Reliable 毎 tick |
| クラス・熟練・お守り | 接続要求のペイロード（`Loadout` の JSON）でホストへ |
| パーク選択 | クライアント → ホスト RPC |

---

## 3. グラフィック（ドット絵の自動生成）

すべてのドット絵は **`Editor/Art/` のコードから生成**する（`ArtGenerator`）。

| 種類 | 作り方 |
|---|---|
| キャラ・敵 | 体をパーツ（頭・胴・腕・脚・武器・マント）に分けて ASCII で描く → パーツに角度と位置を与えたポーズを並べてアニメ化 → 自前のラスタライザで合成。陰影は左上光源で自動、輪郭線・パーツ境界の陰も自動。剣の軌跡（三日月）も生成 |
| エフェクト | 手続き生成（ヒット、煙、魔弾、矢、魂、予兆の扇/円/線、範囲効果の輪、門、ろうそく） |
| 床・壁・小物 | 手続き生成（石畳 4 種、煉瓦の壁、壁上面、柱、旗、骨） |

- 出力：`Art/Characters/*.png`（＋白シルエットのヒットフラッシュ用）、`Resources/Sheets/*.asset`（`SpriteSheet`：クリップ名・行・フレーム数・fps）
- 実行時：`PixelAnimator` がシートからスプライトを切り出して再生（idle/run/attack/attack2/skill1/skill2/dodge/hurt/death、敵は idle/walk/windup/strike/hurt/death）
- 確認用の拡大プレビュー：`Logs/art/*.png`
- 色：暗く彩度を抑えたランプ＋赤・金・魔法の光（`Palette`）

### 光と画面
- 環境光（青紫寄りで暗め）＋ろうそく（揺らぎ）＋プレイヤーの灯り＋魔法・矢・魂・精鋭/ボスの発光
- Bloom・Vignette・コントラスト強め・彩度控えめ
- 画面揺れ（強打、砕き、被弾、ボスの叩きつけ）

---

## 4. フォルダ

```
Assets/_Project/
├── Art/           Characters / Effects / Environment（生成物）
├── Resources/     Sheets/（SpriteSheet）, GameArt, NetPlayer.prefab, NetworkManager.prefab
├── Scenes/        Lobby, Game（PrototypeBuilder が生成）
├── Settings/      GameControls.inputactions, NetworkPrefabs, GamePost（ポストエフェクト）
└── Scripts/
    ├── Core/      純 C#：Stats, Items（保留中のハクスラ）, Run（クラス・パーク・熟練/お守り）, Sim（RunSimulation 他）
    ├── Gameplay/  Net / View / UI / Session
    ├── UI/        MobileControls
    └── Editor/    PrototypeBuilder, PlayerBuild, Art/（ドット絵生成）
```

---

## 5. テスト

| 種類 | 内容 | 実行 |
|---|---|---|
| EditMode（39 件） | ステータス、パーク抽選、熟練・お守り・プロフィール、各クラスの技、砕き・印・鬨の声、敵の予兆と回避、グールの突進、ウェーブ・部屋移動・ボス、レベルアップと一時停止、開始時パーク、蘇生（回復で加速）、全滅、不死鳥、ボット 3 人の通しプレイ | `Tools/RunTests.ps1 -Platform EditMode` |
| PlayMode | 拠点からソロ開始 → 敵表示・イベント受信 | `Tools/RunTests.ps1 -Platform PlayMode` |
| 通信 | ビルド版をホスト＋2 クライアント（戦士・魔法使い・狩人）でボット対戦 | `Tools/NetTest.ps1` |
| 画面 | ビルド版のスクリーンショット | `Tools/Screenshots.ps1 -Class mage` |

## 6. 追加（v0.4）
- **部屋レイアウト**：`Core/Sim/Arena.cs` に手作りの 11 レイアウト（障害物＝AABB、ギミック）。`Arena.Pick` で部屋ごとに選び、
  `RoomStarted.Id` と `RunDirector.LayoutId` で全員に伝える。当たり判定 `Arena.Resolve/Steer/BlocksShot` はホストの敵 AI・弾と、
  オーナー側の `PlayerMover`（＋ボット）で共通。見た目は `ArenaView`（エリアごとの床・壁の切替、障害物・罠のスプライト配置）
- **ギミック**：`RunSimulation.Arena.cs`。トゲ罠・炎は周期で `HazardWarn`→`HazardFire`。火薬樽は「動かない敵（Prop）」として
  シミュレーションに入れ、壊れると導火線→爆発。`EnemySimulation.HostileCount` は Prop を数えない
- **敵の攻撃種別**：Rain（全員の足元に円）、Blink（円の中へ瞬間移動して攻撃）、SelfDestruct、扇・全方位の弾（Count/Spread）。
  Armored（凍結・気絶・印がないと被ダメ×0.4）、Flying（地形無視）。エリアごとのボスは `EnemyCatalog.AreaBosses`
- **難易度**：`DifficultyCatalog`。ホストのプロフィール（または `-difficulty`）→ `RunSimulation` → `RunDirector.DifficultyLevel`
- **スキン**：`SkinCatalog`（Core）＋ `SkinArt`（色替えパレット）→ `warrior_s1` などのシート（被弾フラッシュはクラス共通）。
  `Loadout.skin` → `NetPlayer.Skin`
- **アイコン**：`IconArt` → `Resources/Sheets/icons_perk`（PerkId 順＋休息）、`icons_trinket`
- **音**：`Editor/Audio`（`Dsp` 合成部品、`SfxDesign` 効果音、`Music`＋`Compositions` 作曲データ）→ `AudioGenerator` が
  `Resources/Audio/*.wav` に書き出し（git 管理外、ビルダーが無ければ生成）。実行時は `SfxPlayer`（無ければ旧 `SfxSynth`）と
  `MusicPlayer`（シーンをまたぐ BGM、クロスフェード、勝敗ジングル）。音量は `AudioPrefs`
- **カメラ**：`CameraTarget` がプレイヤーを追い、画面が壁の外を映さないよう位置を制限（Confiner の代わり）
