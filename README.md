# Presso

Discord の動画送信制限 (10MB) を突破するために、動画を「とにかく」目標サイズに収めて圧縮する Windows 用ツールです。FFmpeg の 2-pass エンコードに比例フィードバック収束を組み合わせて、目標サイズ未満になるまで自動で再試行します。

## できること

- ドラッグ&ドロップで複数本まとめて圧縮（進捗バー表示・キャンセル可）
- mp4 / mov / mkv / webm / avi に対応、出力は常に `<元名>_presso.mp4`
- エクスプローラーの **右クリック → 「Pressoで圧縮」** で同じフォルダに圧縮版を生成
- 目標サイズ・コーデック (HEVC / H.264) ・出力先を GUI から設定して保存

## インストール

[Releases](../../releases) から最新の `PressoSetup-x.y.z.exe` をダウンロードして実行してください。`C:\Program Files\Presso\` にインストールされ、対応動画拡張子の右クリックメニューに「Pressoで圧縮」が登録されます。FFmpeg は同梱済みなので別途インストールは不要です。

## 使い方

- スタートメニューから **Presso** を起動 → ウィンドウに動画をドロップ
- またはエクスプローラーで動画を右クリック → **Pressoで圧縮**
- 設定は `Presso` ウィンドウの「設定」ボタンから変更可能（既定: 目標 10MB / HEVC ON / 同フォルダ出力）

## ソースからビルド

### 必要なもの

- Windows 10/11 (x64)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Inno Setup 6](https://jrsoftware.org/isdl.php)
- FFmpeg バイナリ (Windows 64-bit GPL shared build)
  - 例: [BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds/releases) の `ffmpeg-master-latest-win64-gpl-shared.zip`
  - リポジトリには同梱していないので、各自でダウンロードして配置してください

### 配置

```
Presso\
├── ffmpeg\
│   └── bin\
│       ├── ffmpeg.exe
│       ├── ffprobe.exe
│       └── *.dll
├── src\Presso\...
└── installer\Presso.iss
```

### ビルド

```powershell
.\build.ps1
```

成果物は `installer\output\PressoSetup-x.y.z.exe` に出力されます。

## ライセンス

Presso 本体のソースコードは [MIT License](LICENSE) です。

ただしインストーラと配布物には FFmpeg バイナリ (libx264 / libx265 を含む GPLv3 ビルド) を同梱しています。FFmpeg 部分は GPLv3 が適用されます。詳細は [`LICENSES/THIRD_PARTY_NOTICES.txt`](LICENSES/THIRD_PARTY_NOTICES.txt) を参照してください。

H.264 / HEVC は特許対象のフォーマットです。商用配布や業務利用の際はそれぞれのパテントプール (MPEG-LA / HEVC Advance / Velos Media など) のライセンス条件を確認してください。
