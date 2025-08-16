```markdown
# セットアップガイド

## 前提条件

### システム要件
- Windows 10 バージョン 1809 以降、または Windows 11
- x64 アーキテクチャ
- 最低 4GB RAM（推奨 8GB）
- 最低 1GB 空きディスク容量

### 必要なランタイム
1. **.NET 6.0 Runtime**
   - ダウンロード: https://dotnet.microsoft.com/download/dotnet/6.0
   - Windows x64 Desktop Runtime をインストール

2. **Visual C++ 2022 Redistributable (x64)**
   - ダウンロード: https://aka.ms/vs/17/release/vc_redist.x64.exe
   - CefSharp v138+ で必須

## インストール手順

### エンドユーザー向け

1. リリースページから `CefSharp.fastBOT-win-x64.zip` をダウンロード
2. 適当なフォルダに展開
3. `CefSharp.fastBOT.exe` を実行

### 開発者向け

1. **開発環境の準備**
   ```bash
   # Git でクローン
   git clone https://github.com/zattu1/CefSharp.git
   cd CefSharp
   
   # セットアップスクリプト実行
   .\Scripts\setup.ps1 -OpenVS