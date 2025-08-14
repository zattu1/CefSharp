# CefSharp.fastBOT

チケット自動購入用BOTの基盤アプリケーション

## 主要機能

- **動的Proxy変更**: 実行時のProxy設定変更
- **RequestContext分離**: タブ毎の独立セッション
- **認証対応**: Proxy認証の自動処理
- **ローテーション**: 自動Proxy切り替え
- **設定永続化**: ユーザー設定の保存

## システム要件

- Windows 10/11 (x64)
- .NET 8.0 Runtime
- Visual C++ 2022 Redistributable (x64)
- 最低 4GB RAM
- 最低 1GB 空きディスク容量

## インストール

1. リリースページから最新版をダウンロード
2. アーカイブを展開
3. CefSharp.fastBOT.exe を実行

## 開発環境セットアップ

1. .NET 8 SDK をインストール
2. Visual Studio 2022 (Community 以上)
3. Visual C++ 2022 Redistributable
4. PowerShell で setup.ps1 を実行

```powershell
.\Scripts\setup.ps1 -OpenVS
