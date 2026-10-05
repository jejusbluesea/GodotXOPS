[English](README.en.md) | **日本語** | [한국어](README.md)

> この文書は AI が翻訳したものです。原文は韓国語版（[README.md](README.md)）です。

# GodotXOPS

日本のインディー FPS **XOPS**（X operations、2000年）を Godot エンジンに移植したプロジェクトです。オープンソース実装の [OpenXOPS](https://openxops.net/) と、その Unity 移植版である [UnityXOPS](https://github.com/dlwowlsgod/UnityXOPS) を参考にして作りました。

- エンジン: Godot 4.7.2 (.NET)
- プラットフォーム: Windows
- 最新リリース: [1.0.0](https://github.com/jejusbluesea/GodotXOPS/releases/tag/v1.0.0)

オリジナルの操作感をそのまま再現することが目標です。移動、当たり判定、弾の命中判定にはエンジンの物理機能を使わず、オリジナルの計算方法をそのまま移しました。ゲームの進行もオリジナルと同じ毎秒 33.33 ティックで動きます。

## できること

- オリジナルのミッションとアドオンミッションのプレイ（オープニング → メニュー → ブリーフィング → ゲーム → リザルト）
- オリジナルの AI、ミッションイベント、クリア・失敗判定
- メニューの OPTION でキー設定、解像度、明るさ・ガンマ、照準の形、音量を設定
- 人・武器・小物・エフェクトの数値を `godotdata/` の JSON で変更
- `addon.json` でアドオンフォルダを複数のページに分けて登録

## インストールと起動

オリジナル XOPS のアセット（`data` フォルダ）は、著作権の都合でこのリポジトリにもリリースファイルにも含まれていません。オリジナルの XOPS からご自身で用意してください。

1. [リリースページ](https://github.com/jejusbluesea/GodotXOPS/releases)から `GodotXOPS_x.y.z.7z` をダウンロードして展開します。
2. オリジナル XOPS の `data` フォルダを、`GodotXOPS.exe` のあるフォルダにコピーします。
3. アドオンミッションを使う場合は、`addon` フォルダも同じ場所に置きます。
4. `GodotXOPS.exe` を起動します。

フォルダは次のようになります。

```
GodotXOPS/
├─ GodotXOPS.exe
├─ GodotXOPS.pck
├─ data_GodotXOPS_windows_x86_64/   (.NET ランタイム。ゲームデータではありません)
├─ godotdata/                       (設定とゲーム数値の JSON)
├─ addon.json
├─ data/                            (オリジナル XOPS からコピー)
└─ addon/                           (任意)
```

## 操作

初期設定です。メニューの OPTION → Input で変更できます。

| 動作 | キー |
|---|---|
| 移動 | W / A / S / D |
| 視点 | マウス（方向キーでも可） |
| 発射 | マウス左ボタン |
| ジャンプ | Space |
| 歩く | Tab |
| リロード | R |
| スコープ | 左 Shift |
| 武器スロット 1 / 2 | 1 / 2 |
| 発射方式の切り替え（単発 / 連発など、前 / 次） | Z / X |
| 武器を捨てる | G |
| 視点切り替え（一人称 / 三人称） | F1 |
| HUD の表示方式 | F2 |
| ミッションをやり直す | F12 |
| メニューに戻る | ESC |

オリジナルのチートキー（F5 ～ F9）もそのまま使えます。

## アドオンページ

`addon` フォルダは、初期状態でアドオン一覧の最初のページになります。フォルダを追加で登録するには、`addon.json` にパスとページ名を同じ数だけ書きます。パスは `GodotXOPS.exe` のあるフォルダが基準です。

```json
{
    "addonPath" : [
        "addon_pack1",
        "addon_pack2"
    ],
    "addonName" : [
        "Pack 1",
        "Pack 2"
    ]
}
```

## ドキュメント

ドキュメントは韓国語で書かれています。

- [モディング文書](docs/modding.md) — `godotdata/` の JSON で武器・人・小物・エフェクト・ミッションを変更する方法、アドオンページ
- [開発文書](docs/development.md) — ソースからのビルド、コードの構成、点検ツール、オリジナルと異なる動作

## オリジナルとの違い

できるかぎりオリジナルのとおりに実装するよう努め、OpenXOPS を基準にしました。ただし OpenXOPS もオリジナルを完全に再現したものではないため、オリジナルの XOPS と違って感じられる部分があるかもしれません。違いを見つけたら [Issues](https://github.com/jejusbluesea/GodotXOPS/issues) でお知らせください。

## 今後の予定

1.0.0 で移植を終えました。次はバグ修正（1.0.1）と、オリジナルのファイル形式の限界を超える拡張ファイル形式（1.1.0）です。バージョンごとの計画は[ロードマップ](ROADMAP.md)にあります（韓国語）。

## ライセンスと告知

- このリポジトリのコードは [MIT License](LICENSE) です。
- XOPS のアセット（`data`、`addon`）は原作者のものであり、このリポジトリには含まれていません。
- コーディングと翻訳に AI を使用しました。2D・3D・サウンドのアセットは AI 生成物ではありません。

## 参考にしたプロジェクト

- XOPS — nine-two
- [OpenXOPS](https://openxops.net/) — OpenXOPS Project
- [UnityXOPS](https://github.com/dlwowlsgod/UnityXOPS)
