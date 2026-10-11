# オンライン協力（Unity Relay）の準備 — お願いしたい作業

ブラウザ版・PC版でルームコードを使ってネット越しに協力するには、Unity のクラウドに
このゲーム用の「プロジェクト」を作って Relay（中継サーバー）を有効にする必要があります。
スマホのブラウザからでもできます（5分ほど）。

## 手順

1. https://cloud.unity.com を開き、Unity エディタで使っている Unity ID でログイン
2. 左上のプロジェクト一覧から **「Create project」（プロジェクトを作成）**
   - 名前は何でも OK（例：`roguelike-hackslash`）
3. 作ったプロジェクトを開き、左メニューの **「Products」→「Relay」**（または「Multiplayer」→「Relay」）を開いて
   **「Get started」/「Set up Relay」/「Enable」** を押して有効化
   - 無料枠の範囲で使えます（規約への同意を求められたら内容を確認して同意してください）
4. プロジェクトの **「Settings」（歯車）** を開き、**Project ID**（`xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx` の形）をコピー
5. その Project ID をこのチャットに貼ってください

## 補足
- Project ID は秘密の鍵ではありません（ゲームの中に入る識別子です）
- ログインやパスワード入力は私からはできないため、この部分だけお願いしています
- 届くまでの間に、ブラウザ版（まずソロ）とRelay対応のコードは先に作っておきます
