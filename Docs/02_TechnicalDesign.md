# 技術設計書

> ステータス: ドラフト v0.1（2026-10-06）
> 企画書: [01_GameDesign.md](01_GameDesign.md)

---

## 1. 環境・パッケージ

| 項目 | 選定 | 理由 |
|---|---|---|
| Unity | 6000.6.2f1 | インストール済み |
| テンプレート | Universal 2D（URP 2D） | 2D ライト・スプライト最適化 |
| 入力 | Input System | PC / パッド / タッチを 1 つのアクション定義で扱える。`OnScreenStick` でバーチャルスティック |
| カメラ | Cinemachine | 追従・画面揺れ（Impulse）・部屋境界の Confiner |
| マップ | Tilemap ＋ 2D Tilemap Extras | 部屋テンプレートを Tilemap で作る |
| UI | uGUI ＋ TextMeshPro | スマホのタッチ UI 実績が多い |
| アセット管理 | 最初は直接参照 → 規模が増えたら Addressables | 早すぎる最適化を避ける |
| テスト | Unity Test Framework（EditMode） | ダメージ計算・ドロップ抽選・生成ロジックをテスト |
| バージョン管理 | Git ＋ Git LFS（画像・音） | Unity 用 .gitignore |

---

## 2. フォルダ構成

```
Assets/
├── _Project/
│   ├── Art/            スプライト・アニメーション・タイル
│   ├── Audio/
│   ├── Data/           ScriptableObject アセット（武器・敵・アフィックス・ドロップ表）
│   ├── Prefabs/
│   │   ├── Player/
│   │   ├── Enemies/
│   │   ├── Rooms/      部屋テンプレート
│   │   └── UI/
│   ├── Scenes/         Boot / Title / Hub / Dungeon
│   ├── Settings/       Input Actions・URP 設定
│   └── Scripts/
│       ├── Core/       共通基盤（RNG・プール・イベント・ステートマシン・セーブ）
│       ├── Combat/     ダメージ・HP・攻撃・投射物・状態異常
│       ├── Stats/      ステータスと修飾子
│       ├── Player/     入力・移動・オートエイム
│       ├── Enemies/    敵 AI
│       ├── Items/      装備・アフィックス・ドロップ・インベントリ
│       ├── Dungeon/    ダンジョン生成・部屋制御
│       ├── Run/        ラン進行・レベルアップ・レリック
│       ├── Meta/       拠点・恒久強化
│       └── UI/
└── Tests/
    └── EditMode/
```

- `Scripts/` 以下はフォルダごとに **Assembly Definition** を切る（コンパイル時間短縮・依存方向の強制）
- 依存方向: `Core ← Stats ← Combat ← Items/Enemies/Player ← Dungeon/Run ← UI`

---

## 3. 全体アーキテクチャ

### 3.1 データとランタイムの分離
- **定義データ = ScriptableObject**（エディタで編集、実行中は不変）
  - `WeaponDefinition`, `ArmorDefinition`, `AffixDefinition`, `EnemyDefinition`, `LootTable`, `RelicDefinition`, `RoomTemplate`, `BiomeDefinition`
- **実行時インスタンス = 普通の C# クラス**（シリアライズ可能、セーブ対象）
  - `ItemInstance`（定義 ID ＋ レアリティ ＋ 抽選済みアフィックス値 ＋ アイテムレベル）

```
WeaponDefinition (SO) ──┐
AffixDefinition (SO) ───┼─▶ ItemGenerator.Roll(itemLevel, rng) ─▶ ItemInstance
LootTable (SO) ─────────┘
```

### 3.2 シーン構成
| シーン | 役割 |
|---|---|
| Boot | サービス初期化（セーブ・設定・オーディオ）。常駐 |
| Title | タイトル |
| Hub | 拠点・恒久強化 |
| Dungeon | ダンジョン。フロアはシーン内で生成し直す（シーン遷移しない） |

### 3.3 サービスの扱い
- シングルトン乱立を避け、`Boot` で生成する小さなサービス群（`SaveService`, `AudioService`, `RunState`）を静的な `Services` から参照する程度に留める
- ゲーム内イベント（敵撃破・アイテム取得・部屋クリア）は C# `event` / 型付きイベントバスで通知し、UI・実績・レリック効果が購読する

---

## 4. 主要システム設計

### 4.1 ステータス（Stats）
ハクスラの中核。装備・レリック・レベルアップ・バフ全てを「修飾子」として同じ仕組みで扱う。

```csharp
public enum StatType { MaxHealth, Attack, AttackSpeed, MoveSpeed, CritChance, CritDamage, Armor, /* ... */ }
public enum ModifierKind { Flat, PercentAdd, PercentMult }

public readonly struct StatModifier
{
    public readonly StatType Stat;
    public readonly ModifierKind Kind;
    public readonly float Value;
    public readonly object Source; // 装備を外したとき、この Source の修飾子をまとめて除去
}
```

- 最終値 = `(基本値 + ΣFlat) × (1 + ΣPercentAdd) × Π(1 + PercentMult)`
- `StatSheet` が修飾子リストを持ち、変更時のみ再計算（ダーティフラグ）

### 4.2 戦闘
- `Health` コンポーネント: HP・無敵時間・死亡イベント
- ダメージは `DamageInfo`（量・属性・クリティカル・ノックバック・発生源・付与状態異常）を `IDamageable.TakeDamage()` に渡す
- ダメージ計算は MonoBehaviour から切り離した純粋関数 `DamageCalculator` にして EditMode テスト可能にする
- 攻撃判定: 近接は `Physics2D.OverlapCircle/Box` を攻撃フレームで実行、遠距離は `Projectile`（プール管理）
- 「攻撃時/撃破時/被弾時」系のアフィックス・レリックは、戦闘イベントを購読する `ITriggeredEffect` として実装

### 4.3 プレイヤー
- `PlayerInputReader`: Input Actions を読み取り、移動ベクトル・照準方向・ボタンを公開（入力デバイスの違いをここで吸収）
- `AimProvider`: マウス / 右スティック / オートエイム（最寄り敵）を切り替え
- 移動は `Rigidbody2D.linearVelocity` ベース。回避中は無敵＋当たり判定レイヤー変更

### 4.4 敵 AI
- 軽量な **有限ステートマシン**（Idle / Chase / Attack / Stagger / Dead）
- 行動パラメータは `EnemyDefinition` に置き、行動ロジックはタイプ別クラス（突進・射撃・自爆…）
- 敵数が増えても重くならないよう、経路探索は最初は「直進＋壁回避」で済ませ、必要になったら部屋単位のグリッド A* を導入

### 4.5 ダンジョン生成
1. **グラフ生成**: スタート → ボスまでのメインパスを作り、分岐に宝箱・ショップ・イベントを配置
2. **配置**: グリッド上に部屋を割り当て（隣接 = 扉でつながる）
3. **実体化**: 各ノードに条件に合う `RoomTemplate` プレハブをランダムに選んで生成。使わない扉は壁で塞ぐ
4. **入室制御**: `RoomController` がプレイヤー入室で扉を閉じ、敵ウェーブを出し、全滅で開放＋報酬

- 部屋は「1部屋ずつ画面に収まるサイズ」＋カメラを部屋境界に Confine（Enter the Gungeon 方式）

### 4.6 乱数（シード）
- `UnityEngine.Random` は使わず、独自の `GameRandom`（xorshift 等）をシステムごとに分けて持つ
  - `dungeonRng`, `lootRng`, `combatRng`
- ランのシードから派生させることで **同じシード＝同じダンジョン**（デバッグ・デイリーチャレンジ用）

### 4.7 ドロップ
- `LootTable`: 重み付き抽選（何もドロップしない枠を含む）
- `ItemGenerator`: アイテムレベル → レアリティ抽選 → ベースアイテム抽選 → アフィックス数抽選 → アフィックス（重複不可・アイテムレベルで値の幅が変化）

### 4.8 セーブ
- `JsonUtility`（必要なら Newtonsoft JSON）で `Application.persistentDataPath` に保存
- **メタセーブ**（恒久進行）と **ランセーブ**（中断再開用: シード・フロア番号・所持品・ステータス）を分離
- アイテム・定義は文字列 ID で保存（ScriptableObject の参照を直接保存しない）
- セーブデータにバージョン番号を持たせ、将来の形式変更に備える

### 4.9 オブジェクトプール
- 弾・エフェクト・ダメージ数字・ドロップ品は `UnityEngine.Pool.ObjectPool<T>` でプール

---

## 5. PC / スマホ両対応の方針

| 項目 | 方針 |
|---|---|
| 入力 | Input Actions に Keyboard&Mouse / Gamepad のコントロールスキームを定義 |
| タッチ UI | `OnScreenStick` / `OnScreenButton`（仮想ゲームパッドとして入力）を使い、スマホ実行時のみ表示。ゲーム側はタッチを意識しない |
| 画面 | 横画面固定。Canvas Scaler（基準 1920×1080, Match 0.5）、ノッチ対策で Safe Area 対応 |
| 性能目標 | スマホ 60fps（低スペック機 30fps）。敵 50 体＋弾 200 発を目安に計測 |
| 描画 | Sprite Atlas でドローコール削減。2D ライトはスマホで品質設定を下げられるようにする |
| GC | 毎フレームの new / LINQ / 文字列結合を避ける。`OverlapCircle` は NonAlloc 系 API を使う |

---

## 6. 開発の進め方

- 各マイルストーン（企画書 §8）ごとに、まず仮素材で「遊べる状態」を作ってから作り込む
- ロジック（ダメージ計算・抽選・生成）は EditMode テストを書く
- デバッグ用: シード固定・無敵・アイテム生成・フロアスキップのデバッグメニューを早めに用意

---

## 7. 次のステップ

1. 企画書の【要決定】項目を決める
2. Unity Hub で **Universal 2D** テンプレートからプロジェクト作成（このフォルダ直下）
3. Git 初期化・.gitignore・パッケージ導入（Input System / Cinemachine / Test Framework）
4. M0: フォルダ構成・Assembly Definition・入力・プレイヤー移動・カメラ
