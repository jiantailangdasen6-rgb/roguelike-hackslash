# クレジット

## フォント（SIL Open Font License 1.1）

| ゲームでの用途 | フォント | 作者 | ライセンス |
|---|---|---|---|
| 本文（`Resources/Fonts/Body.ttf`） | Noto Serif JP Bold | Google / Adobe | SIL OFL 1.1 |
| 見出し・数字（`Resources/Fonts/Title.ttf`） | Kaisei Tokumin ExtraBold（解星 徳明） | Font-Kai | SIL OFL 1.1 |

- どちらもゲームで使う文字だけに絞ったサブセット（`Tools/SubsetFonts.ps1`）。OFL ではフォント名を変えずに改変版を名乗ることは
  禁止されている（予約フォント名）ため、販売時はファイル名・表示名の扱いを再確認すること。

## 音楽（CC-BY 4.0、https://creativecommons.org/licenses/by/4.0/）

素材は `Assets/_Project/Audio/Pack/music/`。音量をそろえ、敗北の曲は冒頭を切り出して使っている（`Editor/Audio/SoundPack.cs`）。
水没した地下墓地・凍てつく霊廟・燃え落ちた鍛冶場・迷宮の心臓・菌糸の洞窟・呪われた書庫の曲は、上の曲の速さ・音程・こもり・反響・歪みを変えた改変版（同じ CC-BY 4.0）。
エンディングのスタッフロールにも作者名を表示している（`UI/EndingView.cs`）。

| ゲームでの用途 | 曲 | 作者 | 入手元 |
|---|---|---|---|
| 拠点・各エリア・勝利・敗北 | LOOP BOX #3 より Pavane / In Darkness [v2] / Obscurium / Throne Room [v2] / Victory Stats / Zwischenwelt | Of Far Different Nature（https://fardifferent.carrd.co/） | https://opengameart.org/content/essentials-pack-for-fantasy-games-loop-box-3-orchestral-soundtracks-for-rpgs-and-adventures |
| ボス | Arcanos, Sorcerer of the Old Banished Woods（Dark souls type boss theme） | ProjectHelmet（Philémon Weber） | https://opengameart.org/content/dark-souls-type-boss-theme |

## 効果音

素材は `Assets/_Project/Audio/Pack/`。切り出し・重ね合わせ・音程と音量の調整をして使っている（`Editor/Audio/SoundPack.cs`）。

| 素材 | 作者 | ライセンス | 入手元 |
|---|---|---|---|
| Fantasy SFX Pack Vol 1 | JC Sounds | CC-BY 4.0 | https://opengameart.org/content/jc-sounds-fantasy-sfx-pack-vol-1 |
| 8 Magic Attacks | leohpaz | CC-BY 4.0 | https://opengameart.org/content/8-magic-attacks |
| Impact Sounds / RPG Audio | Kenney（www.kenney.nl） | CC0 | https://kenney.nl/assets/impact-sounds ・ https://kenney.nl/assets/rpg-audio |
| RPG Sound Pack | artisticdude | CC0 | https://opengameart.org/content/rpg-sound-pack |
| 20 sword sound effects (attacks and clashes) | StarNinjas | CC0 | https://opengameart.org/content/20-sword-sound-effects-attacks-and-clashes |
| Impact | Iwan 'qubodup' Gabovitch | CC0 | https://opengameart.org/content/impact |

ゲーム内では Esc メニューの下に作者名を表示している。

## グラフィック・その他
- ドット絵・エフェクト・タイルはすべて `Assets/_Project/Scripts/Editor/Art/` のコードで生成したオリジナル
- 環境音と、素材が読み込めないときの代わりの効果音は `SfxSynth` で実行時に合成したオリジナル
