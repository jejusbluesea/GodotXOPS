[English](README.en.md) | **日本語** | [한국어](README.md)

> この文書は AI が翻訳したものです。原文は韓国語版（[README.md](README.md)）です。

# GodotXOPS

日本のインディー FPS **XOPS**（X operations、2000年）を Godot エンジンに移植したプロジェクトです。オープンソース実装の [OpenXOPS](https://openxops.net/) と、その Unity 移植版である [UnityXOPS](https://github.com/dlwowlsgod/UnityXOPS) を参考にして作りました。

- エンジン: Godot 4.7.2 (.NET)
- プラットフォーム: Windows
- 最新リリース: [1.2.0](https://github.com/jejusbluesea/GodotXOPS/releases/tag/v1.2.0)

オリジナルの操作感をそのまま再現することが目標です。移動、当たり判定、弾の命中判定にはエンジンの物理機能を使わず、オリジナルの計算方法をそのまま移しました。ゲームの進行もオリジナルと同じ毎秒 33.33 ティックで動きます。

## できること

- オリジナルのミッションとアドオンミッションのプレイ（オープニング → メニュー → ブリーフィング → ゲーム → リザルト）
- オリジナルの AI、ミッションイベント、クリア・失敗判定
- メニューの OPTION でキー設定、解像度、明るさ・ガンマ、照準の形、音量を設定
- 人・武器・小物・エフェクトの数値を `godotdata/` の JSON で変更
- `addon.json` でアドオンフォルダを複数のページに分けて登録
- 拡張ファイル形式（BD2、PD2、MIF2）: テクスチャの数、ポイント番号、イベントラインの数に制限がなく、ミッションが独自の人・武器・小物・エフェクト・材質・サウンドを持ち込めます。オリジナルの形式（BD1、PD1、MIF）もそのまま読み込めます
- スクリプトイベント: オリジナルの 10 種類のイベントに加えて、変数と分岐、スポーン、画面の文字、小物やブロックの移動、サウンドの再生など 36 種類が入っており、自作のイベントパックも追加できます。スクリプトは隔離された環境でのみ動きます
- 画面スクリプト: オープニング、メニュー、ブリーフィング、ゲーム画面（HUD）、リザルト、設定画面をスクリプトで描き替えられます。標準の画面はそのままで、登録した画面だけが置き換わります。標準の 6 画面をそのまま書き直したサンプルが入っています。スクリプトは隔離された環境でのみ動きます
- [エディタ](#エディタ): ブロック、ポイントとイベント、ミッション、データを編集し、その場でテストプレイできます
- デバッグコンソール（`godotdata/config.json` の `AllowConsole` を `"true"` にすると F11 で開きます）

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
├─ GodotXOPS_Editor.bat             (エディタの起動)
├─ GodotXOPS.pck
├─ libgodot_riscv.windows.template_release.x86_64.dll   (スクリプトの実行)
├─ data_GodotXOPS_windows_x86_64/   (.NET ランタイム。ゲームデータではありません)
├─ godotdata/                       (設定とゲーム数値の JSON)
├─ addon.json
├─ LICENSE.txt
├─ THIRD_PARTY_NOTICES.txt
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
| インタラクト（ミッションのイベントが使うときのみ） | F |
| 視点切り替え（一人称 / 三人称） | F1 |
| HUD の表示方式 | F2 |
| ミッションをやり直す | F12 |
| メニューに戻る | ESC |

オリジナルのチートキー（F5 ～ F9）もそのまま使えます。

## エディタ

`GodotXOPS_Editor.bat` を起動します（`GodotXOPS.exe -- --scene editor` と同じです）。拡張形式のブロック（BD2）、ポイントとイベント（PD2）、ミッション（MIF2）、データ（JSON）を作成・編集します。オリジナル形式のマップとミッションは、File の Import で拡張形式に変換して開きます。

- 操作は Blender の初期設定に合わせています: 中ボタンで視点、左クリックで選択、G / R / S で移動・回転・拡大縮小。同じ機能がメニューとボタンにもあります。
- F5 で今の内容をすぐにテストプレイし、Esc でエディタに戻ります。
- 画面の文字は英語です。操作の一覧は[開発文書のエディタの節](docs/development.md#에디터)にあります（韓国語）。

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

- [モディング文書](docs/modding.md) — `godotdata/` の JSON で武器・人・小物・エフェクト・ミッションを変更する方法、拡張ファイル形式、スクリプトイベント、画面スクリプト、アドオンページ
- [開発文書](docs/development.md) — ソースからのビルド、コードの構成、点検ツール、エディタ、デバッグコンソール、オリジナルと異なる動作

## オリジナルとの違い

できるかぎりオリジナルのとおりに実装するよう努め、OpenXOPS を基準にしました。ただし OpenXOPS もオリジナルを完全に再現したものではないため、オリジナルの XOPS と違って感じられる部分があるかもしれません。違いを見つけたら [Issues](https://github.com/jejusbluesea/GodotXOPS/issues) でお知らせください。

## 今後の予定

1.0.0 で移植を終え、1.1.0 で拡張ファイル形式、スクリプトイベント、エディタを、1.2.0 で画面スクリプトを追加しました。次のバージョンの内容は、決まりしだい[ロードマップ](ROADMAP.md)に書きます（韓国語）。

## ライセンスと告知

- このリポジトリのコードは [MIT License](LICENSE) です。
- XOPS のアセット（`data`、`addon`）は原作者のものであり、このリポジトリには含まれていません。
- [Godot Engine](https://godotengine.org)（MIT）で作られており、スクリプト（イベント、画面）の実行には [Godot Sandbox](https://github.com/libriscv/godot-sandbox) 0.60（Alf-André Walla、BSD-3-Clause）を使用しています。ライセンスの全文はリリースファイルの `THIRD_PARTY_NOTICES.txt`（[リポジトリ内のもの](dist/THIRD_PARTY_NOTICES.txt)）にあります。
- コーディングと翻訳に AI を使用しました。2D・3D・サウンドのアセットは AI 生成物ではありません。

## 参考にしたプロジェクト

- XOPS — nine-two
- [OpenXOPS](https://openxops.net/) — OpenXOPS Project
- [UnityXOPS](https://github.com/dlwowlsgod/UnityXOPS)
