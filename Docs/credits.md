# クレジット

## フォント（SIL Open Font License 1.1）

| ゲームでの用途 | フォント | 作者 | ライセンス |
|---|---|---|---|
| 本文（`Resources/Fonts/Body.ttf`） | Noto Serif JP Bold | Google / Adobe | SIL OFL 1.1 |
| 見出し・数字（`Resources/Fonts/Title.ttf`） | Kaisei Tokumin ExtraBold（解星 徳明） | Font-Kai | SIL OFL 1.1 |

- どちらもゲームで使う文字だけに絞ったサブセット（`Tools/SubsetFonts.ps1`）。OFL ではフォント名を変えずに改変版を名乗ることは
  禁止されている（予約フォント名）ため、販売時はファイル名・表示名の扱いを再確認すること。

## グラフィック・効果音
- ドット絵・エフェクト・タイルはすべて `Assets/_Project/Scripts/Editor/Art/` のコードで生成したオリジナル
- 効果音・環境音は `SfxSynth` で実行時に合成したオリジナル
