# CefSharp.fastBOT

チケット自動購入用BOTの基盤アプリケーション

## 主要機能

- **動的Proxy変更**: 実行時のProxy設定変更
- **RequestContext分離**: タブ毎の独立セッション
- **認証対応**: Proxy認証の自動処理
- **ローテーション**: 自動Proxy切り替え
- **設定永続化**: ユーザー設定の保存
- **HTMLパーサー**: CSSセレクター・XPath対応の高度なHTML解析機能
- **フォーム操作**: 自動入力・パラメータ抽出機能
- **10個のアカウントスロット**: アカウント1～10までの固定スロット管理
- **即座のアカウント切り替え**: コンボボックス選択で瞬時に切り替え
- **包括的なデータ管理**: 1つのアカウントで以下の情報を一括管理
  - ログイン情報（ID・パスワード）
  - ネットワーク設定（Proxy設定・ローテーション設定）
  - 購入者情報（氏名・カナ・電話・メール）
  - クレジットカード情報（番号・CVV・有効期限・名義）
- **自動保存機能**: 設定変更時の自動保存とアプリ再起動時の復元
- **視覚的な状態表示**: アカウントの設定状況を一目で確認
- **データ完整性チェック**: ログイン情報・購入者情報・決済情報の完全性を個別チェック
- **アカウントクローン**: 既存アカウントの複製作成機能

## システム要件

- Windows 10/11 (x64)
- .NET 6.0 Runtime
- Visual C++ 2022 Redistributable (x64)
- 最低 4GB RAM
- 最低 1GB 空きディスク容量

## 必要なNuGetパッケージ

```xml
<PackageReference Include="HtmlAgilityPack" Version="1.11.54" />
<PackageReference Include="Fizzler.Systems.HtmlAgilityPack" Version="1.2.1" />
<PackageReference Include="CefSharp.Wpf" Version="120.1.110" />
```

## アカウント管理の使い方（改良版）

### 基本操作
1. **アカウント選択**: コンボボックスから「アカウント1～10」を選択
2. **即座の切り替え**: 選択と同時に該当アカウントのデータがフォームに反映
3. **データ入力**: 各セクションに必要な情報を入力
   - **ログイン情報**: ID・パスワード
   - **ネットワーク設定**: Proxy設定・ローテーション設定
   - **購入者情報**: 氏名・カナ・電話・メール
   - **クレジットカード**: 番号・CVV・有効期限・名義
4. **保存**: 「保存」ボタンでアカウントに設定を保存・有効化
5. **切り替え**: 別のアカウント番号を選択して即座に切り替え

### アカウント表示ステータス
```
アカウント1 - 未設定              # データが未入力
アカウント2 - user@mail.com       # ログイン情報設定済み
アカウント3 - 設定中              # 一部データ入力済み
アカウント4 - taro@mail.com ●     # 完全設定済み（●マーク）
アカウント5 - 未設定 (無効)        # 削除済みアカウント
```

### ボタン機能
- **保存** (緑): 現在の入力内容をアカウントに保存し、有効化
- **クリア** (黄): フォームの内容をクリア（アカウントデータは変更なし）
- **削除** (赤): 選択したアカウントのデータを完全削除・無効化

### アカウントデータの構造
各アカウントは以下の4つのカテゴリを一括管理：

#### 1. ログイン情報
- ログインID
- パスワード

#### 2. ネットワーク設定
- Proxyホスト・ポート
- Proxy認証（ユーザー名・パスワード）
- ローテーション設定（毎回/時間間隔）

#### 3. 購入者情報
- 氏名（姓・名）
- カナ（セイ・メイ）
- 電話番号（3分割）
- メールアドレス

#### 4. クレジットカード情報
- カード番号
- CVVコード
- 有効期限（月・年）
- カード名義人

### データ完整性チェック機能
```csharp
// 各カテゴリの完全性を個別チェック
bool hasCompleteLogin = account.HasCompleteLoginInfo();          // ID+パスワード
bool hasCompletePurchaser = account.HasCompletePurchaserInfo();  // 氏名+メール
bool hasCompleteCard = account.HasCompleteCreditCardInfo();      // カード+CVV+期限
bool hasAnyData = account.HasData();                             // 何らかのデータ
```

## インストール

1. リリースページから最新版をダウンロード
2. アーカイブを展開
3. CefSharp.fastBOT.exe を実行

## 開発環境セットアップ

1. .NET 6 SDK をインストール
2. Visual Studio 2022 (Community 以上)
3. Visual C++ 2022 Redistributable
4. PowerShell で setup.ps1 を実行

```powershell
.\Scripts\setup.ps1 -OpenVS
```

---

## 機能別ヘルプドキュメント

### 1. JavaScript実行機能

#### 非同期JavaScript実行（コールバック付き）
```csharp
// MainWindowから実行
mainWindow.ExecuteJavaScript("document.title", result => {
    if (result.Success) {
        Console.WriteLine($"タイトル: {result.Result}");
    } else {
        Console.WriteLine($"エラー: {result.ErrorMessage}");
    }
});

// BrowserTabManagerから実行
_tabManager.ExecuteJavaScriptAsync("alert('Hello World');", result => {
    Console.WriteLine($"実行結果: {result.Success}");
});
```

#### 同期JavaScript実行
```csharp
// MainWindowから実行
var result = await mainWindow.ExecuteJavaScriptSync("document.readyState");
if (result.Success) {
    Console.WriteLine($"ページ状態: {result.Result}");
}

// BrowserTabManagerから実行
var result = await _tabManager.ExecuteJavaScriptSync("window.location.href");
```

#### ヘルパーメソッド
```csharp
// 要素の存在確認
_tabManager.CheckElementExists("#login-button", exists => {
    Console.WriteLine($"ログインボタン存在: {exists}");
});

// 要素のテキスト取得
_tabManager.GetElementText(".title", text => {
    Console.WriteLine($"タイトル: {text}");
});

// 要素をクリック
_tabManager.ClickElement("#submit-btn", success => {
    Console.WriteLine($"クリック成功: {success}");
});

// フォーム入力
_tabManager.SetElementValue("#username", "your-username", success => {
    Console.WriteLine($"入力成功: {success}");
});
```

### 2. HTMLデータのロード・取得・解析機能

#### HTMLコンテンツをブラウザに読み込み
```csharp
using CefSharp.fastBOT.Utils;

// HTMLファイルからロード
string htmlContent = await File.ReadAllTextAsync("sample.html");
await BrowserAutomationUtils.LoadHtmlAsync(browser, htmlContent, "file:///sample.html");

// HTML文字列を直接ロード
string html = "<html><body><h1>Hello World</h1></body></html>";
await BrowserAutomationUtils.LoadHtmlAsync(browser, html, "about:blank");
```

#### CEFからの直接HTML取得
```csharp
using CefSharp.fastBOT.Core;

// HTML抽出サービスを使用（CEFの GetSourceAsync() を使用）
var htmlExtractor = new HtmlExtractionService(browser);

// ページ全体のHTML取得（CEF直接取得）
string fullHtml = await htmlExtractor.GetPageHtmlAsync();

// Body部分のHTML取得
string bodyHtml = await htmlExtractor.GetPageBodyHtmlAsync();

// 特定要素のHTML取得（CSSセレクター使用）
string elementHtml = await htmlExtractor.GetElementHtmlAsync("#content");
string elementInnerHtml = await htmlExtractor.GetElementInnerHtmlAsync(".article-content");

// テキストのみ取得
string textContent = await htmlExtractor.GetPageTextAsync();

// ページ情報の取得
var pageInfo = await htmlExtractor.GetPageInfoAsync();
Console.WriteLine($"タイトル: {pageInfo.Title}");
Console.WriteLine($"URL: {pageInfo.Url}");
```

### 3. HTMLパーサー機能（Fizzler CSS Selectors使用）

#### CSSセレクターを使った要素検索
```csharp
using CefSharp.fastBOT.Core;

var htmlExtractor = new HtmlExtractionService(browser);

// CSSセレクターで要素検索（jQuery風）
var menuItems = await htmlExtractor.SelectNodesByCssSelectorAsync(".menu-item");
var firstTitle = await htmlExtractor.SelectSingleNodeByCssSelectorAsync("h1.main-title");

// 複雑なCSSセレクター
var specificElements = await htmlExtractor.SelectNodesByCssSelectorAsync(
    "div.content > ul.list li:nth-child(2) a[href*='product']"
);

// フォーム要素の検索
var inputFields = await htmlExtractor.SelectNodesByCssSelectorAsync(
    "form#checkout input[type='text'], form#checkout select"
);
```

#### XPathを使った要素検索
```csharp
// XPathで要素検索（libxml互換）
var elements = await htmlExtractor.SelectNodesByXPathAsync("//div[@class='product']//span[@class='price']");

// 属性値での検索
var linkElements = await htmlExtractor.SelectNodesByXPathAsync("//a[contains(@href, 'product')]");

// テキスト内容での検索
var textElements = await htmlExtractor.SelectNodesByXPathAsync("//p[contains(text(), '在庫あり')]");
```

#### タグ名と属性での検索（CHtmlParser互換）
```csharp
// タグ名で検索
var allImages = await htmlExtractor.GetElementsByTagNameAsync("img");

// タグ名と属性値で検索
var specificInputs = await htmlExtractor.GetElementsByTagNameAsync("input", "type", "submit");

// 属性値の部分一致検索
var partialMatches = await htmlExtractor.GetElementsByTagNameContainAsync("div", "class", "product");
```

### 4. フォーム操作・パラメータ抽出

#### フォームパラメータの抽出
```csharp
// 全フォームのパラメータを抽出
var allFormParams = await htmlExtractor.GetFormParametersAsync();

// 特定フォームのパラメータを抽出
var loginFormParams = await htmlExtractor.GetFormParametersAsync("#login-form");

// パラメータの表示
htmlExtractor.TraceParameters(loginFormParams);

foreach (var param in loginFormParams)
{
    Console.WriteLine($"Name: {param.Key}, Value: {param.Value}");
}
```

#### フォームの自動入力
```csharp
// フォームデータの準備
var formData = new Dictionary<string, string>
{
    ["username"] = "user@example.com",
    ["password"] = "password123",
    ["remember"] = "1"
};

// BrowserTabManagerを使用した自動入力
bool success = await _tabManager.AutoFillFormAsync(formData);
Console.WriteLine($"フォーム入力成功: {success}");
```

### 5. 高度なHTMLデータ抽出

#### テーブルデータの抽出
```csharp
// テーブルを2次元配列として抽出
var tableData = await htmlExtractor.ExtractTableDataAsync("table.price-list");

// CSV形式で取得
string csvData = await _tabManager.ExtractTableAsCsvAsync("table.product-list");
Console.WriteLine("CSV出力:");
Console.WriteLine(csvData);

// テーブルの内容を表示
foreach (var row in tableData)
{
    Console.WriteLine(string.Join(" | ", row));
}
```

#### リンク情報の抽出
```csharp
// ページ内の全リンクを抽出
var allLinks = await htmlExtractor.ExtractLinksAsync();

// 特定のリンクを抽出
var productLinks = await htmlExtractor.ExtractLinksAsync("a[href*='product']");

foreach (var link in productLinks)
{
    Console.WriteLine($"URL: {link.Url}");
    Console.WriteLine($"テキスト: {link.Text}");
    Console.WriteLine($"タイトル: {link.Title}");
}
```

#### 画像情報の抽出
```csharp
// ページ内の全画像を抽出
var allImages = await htmlExtractor.ExtractImagesAsync();

// 商品画像のみを抽出
var productImages = await htmlExtractor.ExtractImagesAsync("img.product-image");

foreach (var image in productImages)
{
    Console.WriteLine($"ソース: {image.Src}");
    Console.WriteLine($"Alt: {image.Alt}");
    Console.WriteLine($"タイトル: {image.Title}");
}
```

#### メタタグ情報の抽出
```csharp
// ページのメタタグ情報を抽出
var metaTags = await htmlExtractor.ExtractMetaTagsAsync();

foreach (var meta in metaTags)
{
    Console.WriteLine($"{meta.Key}: {meta.Value}");
}

// 特定のメタタグを確認
if (metaTags.TryGetValue("name:description", out string description))
{
    Console.WriteLine($"ページ説明: {description}");
}

if (metaTags.TryGetValue("property:og:title", out string ogTitle))
{
    Console.WriteLine($"OGタイトル: {ogTitle}");
}
```

### 6. 商品情報抽出の実用例

#### ECサイトから商品情報を抽出
```csharp
// 商品情報の抽出
var products = await _tabManager.ExtractProductsAsync();

foreach (var product in products)
{
    Console.WriteLine($"商品名: {product.Name}");
    Console.WriteLine($"価格: {product.Price}");
    Console.WriteLine($"画像URL: {product.ImageUrl}");
    Console.WriteLine($"説明: {product.Description}");
    Console.WriteLine("---");
}
```

#### ナビゲーション情報の抽出
```csharp
// サイトのナビゲーション構造を抽出
var navigation = await _tabManager.ExtractNavigationAsync();

Console.WriteLine("メインメニュー:");
foreach (var link in navigation.MainMenuLinks)
{
    Console.WriteLine($"  {link.Text} -> {link.Url}");
}

Console.WriteLine("パンくずリスト:");
foreach (var breadcrumb in navigation.Breadcrumbs)
{
    Console.WriteLine($"  {breadcrumb.Text} -> {breadcrumb.Url}");
}

Console.WriteLine("ページネーション:");
foreach (var pagination in navigation.PaginationLinks)
{
    string status = pagination.IsActive ? "[現在]" : "";
    Console.WriteLine($"  {pagination.Text} -> {pagination.Url} {status}");
}
```

### 7. 構造化データの一括抽出

#### ページの全体構造を抽出
```csharp
// ページの構造化データを一括取得
var structuredData = await _tabManager.ExtractStructuredDataAsync();

Console.WriteLine("=== ページ情報 ===");
Console.WriteLine($"タイトル: {structuredData.PageInfo.Title}");
Console.WriteLine($"URL: {structuredData.PageInfo.Url}");

Console.WriteLine("=== メタタグ ===");
foreach (var meta in structuredData.MetaTags)
{
    Console.WriteLine($"{meta.Key}: {meta.Value}");
}

Console.WriteLine("=== リンク ===");
foreach (var link in structuredData.Links.Take(5)) // 最初の5件のみ表示
{
    Console.WriteLine($"{link.Text} -> {link.Url}");
}

Console.WriteLine("=== 画像 ===");
foreach (var image in structuredData.Images.Take(3)) // 最初の3件のみ表示
{
    Console.WriteLine($"{image.Alt} -> {image.Src}");
}

Console.WriteLine("=== テーブル ===");
for (int i = 0; i < structuredData.Tables.Count; i++)
{
    Console.WriteLine($"テーブル {i + 1}: {structuredData.Tables[i].Count}行");
}
```

### 8. HTMLデータの保存と管理

#### HTMLデータの保存
```csharp
// タブマネージャーを使用したHTML取得・保存
var htmlData = await _tabManager.ExtractHtmlAsync(
    HtmlDataType.FullPage,  // データタイプ
    null,                   // セレクター（Element取得時のみ）
    true                    // 自動保存
);

Console.WriteLine($"HTMLサイズ: {htmlData.Size} bytes");
Console.WriteLine($"保存先: {htmlData.FilePath}");

// 複数タイプを一括取得
var dataTypes = new List<HtmlDataType>
{
    HtmlDataType.FullPage,
    HtmlDataType.BodyOnly,
    HtmlDataType.TextOnly
};
var htmlDataList = await _tabManager.ExtractMultipleHtmlAsync(dataTypes, true);

// 特定要素のHTML取得
var elementData = await _tabManager.ExtractElementHtmlAsync("#content", true);
```

#### HTMLファイルの管理
```csharp
// 保存されたHTMLファイル一覧
var savedFiles = _tabManager.GetSavedHtmlFiles();

foreach (var file in savedFiles)
{
    Console.WriteLine($"ファイル: {file.FileName}");
    Console.WriteLine($"サイズ: {file.Size} bytes");
    Console.WriteLine($"作成日時: {file.CreatedAt}");
}

// HTMLデータの比較
var comparison = _tabManager.CompareHtmlData(htmlData1, htmlData2);
Console.WriteLine($"類似度: {comparison.SimilarityPercentage}%");
```

#### HTMLExtractorからの保存
```csharp
// HTML抽出サービスからの直接保存
bool saved = await htmlExtractor.SaveHtmlToFileAsync("page.html");
bool parsed = await htmlExtractor.SaveParsedHtmlToFileAsync("parsed.html");

// ページスナップショット（HTML + 情報JSON）
bool snapshot = await htmlExtractor.SavePageSnapshotAsync("snapshot_20241216");

// 解析結果のデバッグ出力
bool debug = await htmlExtractor.WriteAnalysisResultAsync("C:\\temp\\");
```

### 9. URLのロード・ナビゲーション

#### URLをブラウザにロード
```csharp
// 直接URLを設定
browser.Address = "https://www.example.com";

// MainWindowのナビゲーション機能を使用
mainWindow.UrlLineEdit.Text = "https://www.example.com";
mainWindow.NavigateToUrl(); // private method - Goボタンクリックで実行

// AutomationServiceを使用
var automation = new AutomationService(browser);
string currentUrl = automation.GetCurrentUrl();
```

### 10. Cookieの取得

#### 現在のページのCookie取得
```csharp
using CefSharp.fastBOT.Utils;

// 全Cookieを取得
var allCookies = await BrowserAutomationUtils.GetCookiesAsync(browser.Address);

// 特定のCookieを取得
var sessionCookie = await BrowserAutomationUtils.GetCookiesAsync(browser.Address, "sessionId");

// AutomationServiceを使用
var automation = new AutomationService(browser);
var cookies = await automation.GetCurrentPageCookiesAsync("PHPSESSID");
```

### 11. Cookieの更新・設定

#### Cookieの設定
```csharp
// BrowserAutomationUtilsを使用
bool success = BrowserAutomationUtils.UpdateCookieAsync(
    "https://www.example.com",
    "sessionId",
    "abc123",
    "example.com",
    "/",
    false, // HttpOnly
    true   // Secure
);

// AutomationServiceを使用
var automation = new AutomationService(browser);
bool success = automation.SetCurrentPageCookieAsync(
    "user_preference",
    "dark_mode",
    false, // HttpOnly
    true   // Secure
);
```

### 12. Proxyの適用

#### Proxyの設定
```csharp
using CefSharp.fastBOT.Core;
using CefSharp.fastBOT.Models;

var proxyManager = new ProxyManager();

// Proxy設定を作成
var proxyConfig = new ProxyConfig
{
    Host = "proxy.example.com",
    Port = 8080,
    Scheme = "http",
    Username = "proxyuser",
    Password = "proxypass"
};

// Proxyを適用
bool success = await proxyManager.SetProxyAsync(browser, proxyConfig);

// Proxyを無効化
bool disabled = await proxyManager.DisableProxyAsync(browser);

// 現在のProxy設定を取得
var currentProxy = proxyManager.GetProxyConfig(browser);
```

### 13. マウスクリックのPostMessage

#### 座標を指定してクリック
```csharp
using CefSharp.fastBOT.Utils;

// 左クリック
bool success = BrowserAutomationUtils.SendMouseClick(browser, 100, 200, false);

// 右クリック
bool success = BrowserAutomationUtils.SendMouseClick(browser, 100, 200, true);

// AutomationServiceを使用
var automation = new AutomationService(browser);
bool success = automation.ClickCoordinate(150, 250, false); // 左クリック
bool success = automation.ClickCoordinate(150, 250, true);  // 右クリック
```

### 14. キーボード入力のPostMessage

#### テキスト入力
```csharp
using CefSharp.fastBOT.Utils;

// 文字列を送信
bool success = BrowserAutomationUtils.SendKeyboardInput(browser, "Hello World");

// AutomationServiceを使用
var automation = new AutomationService(browser);
bool success = automation.SendText("テキスト入力");
```

### 15. キーコード入力のPostMessage

#### 特定キーの送信
```csharp
using System.Windows.Input;
using CefSharp.fastBOT.Utils;

// Enterキーを送信
bool success = BrowserAutomationUtils.SendKeyCode(browser, (int)Key.Enter, true);

// AutomationServiceを使用
var automation = new AutomationService(browser);
bool success = automation.SendKey(Key.Enter, true);    // キーダウン
bool success = automation.SendKey(Key.Enter, false);   // キーアップ

// ヘルパーメソッド
bool success = automation.SendEnter();  // Enterキー
bool success = automation.SendTab();    // Tabキー
```

## HTMLパーサーを活用した高度な自動化機能

### スマートログイン（フォーム解析付き）
```csharp
var automation = new AutomationService(browser);
var htmlExtractor = new HtmlExtractionService(browser);

// フォーム構造を解析してからログイン
var formParams = await htmlExtractor.GetFormParametersAsync("form");
var loginButton = await htmlExtractor.SelectSingleNodeByCssSelectorAsync("input[type='submit'], button[type='submit']");

if (formParams.ContainsKey("username") && formParams.ContainsKey("password"))
{
    bool success = await automation.AutoLoginAsync(
        "your-username",
        "your-password",
        "input[type='submit'], button[type='submit']",
        "input[name='username']",
        "input[name='password']"
    );
}
```

### インテリジェントチケット購入
```csharp
// ページの構造を解析してからチケット選択
var structuredData = await _tabManager.ExtractStructuredDataAsync();

// 商品情報から最適なチケットを選択
var products = await _tabManager.ExtractProductsAsync();
var premiumTickets = products.Where(p => p.Name.Contains("プレミア") && 
                                        !p.Price.Contains("売り切れ")).ToList();

if (premiumTickets.Any())
{
    var ticketSelectors = new List<string>
    {
        ".ticket-type-premium",
        ".seat-selection-a1",
        ".quantity-2"
    };

    bool success = await automation.AutoPurchaseTicketsAsync(
        ticketSelectors,
        ".purchase-button"
    );
}
```

### 動的フォーム自動入力
```csharp
// ページの構造を解析してフォームフィールドを特定
var formParams = await htmlExtractor.GetFormParametersAsync();

var smartFormData = new Dictionary<string, string>();

// フィールドの種類を推測して適切な値を設定
foreach (var param in formParams.Keys)
{
    if (param.ToLower().Contains("email"))
        smartFormData[param] = "user@example.com";
    else if (param.ToLower().Contains("name"))
        smartFormData[param] = "田中太郎";
    else if (param.ToLower().Contains("phone"))
        smartFormData[param] = "090-1234-5678";
}

bool success = await _tabManager.AutoFillFormAsync(smartFormData);
```

### ページ変化の監視
```csharp
// 初期状態のHTMLを保存
var initialHtml = await htmlExtractor.GetPageHtmlAsync();

// 一定時間後に再取得して比較
await Task.Delay(5000);
var currentHtml = await htmlExtractor.GetPageHtmlAsync();

// HTMLデータとして比較
var initialData = new HtmlData { Content = initialHtml };
var currentData = new HtmlData { Content = currentHtml };
var comparison = _tabManager.CompareHtmlData(initialData, currentData);

if (comparison.SimilarityPercentage < 95)
{
    Console.WriteLine($"ページが変更されました（類似度: {comparison.SimilarityPercentage}%）");
    
    // 変更された要素を特定
    var newElements = await htmlExtractor.SelectNodesByCssSelectorAsync(".new-content, .updated");
    Console.WriteLine($"新しい要素: {newElements.Count}個");
}
```

### 要素の高度な待機
```csharp
// 複数条件での要素待機
bool elementReady = await WaitForComplexElementAsync(htmlExtractor, 30);

async Task<bool> WaitForComplexElementAsync(HtmlExtractionService extractor, int timeoutSeconds)
{
    var startTime = DateTime.Now;
    var timeout = TimeSpan.FromSeconds(timeoutSeconds);

    while (DateTime.Now - startTime < timeout)
    {
        // 複数の条件をチェック
        var submitButton = await extractor.SelectSingleNodeByCssSelectorAsync("input[type='submit']:not(:disabled)");
        var requiredFields = await extractor.SelectNodesByCssSelectorAsync("input[required]:invalid");
        var loadingElements = await extractor.SelectNodesByCssSelectorAsync(".loading, .spinner");

        // 送信ボタンが有効で、必須フィールドがすべて入力済み、ローディング要素がない
        if (submitButton != null && !requiredFields.Any() && !loadingElements.Any())
        {
            return true;
        }

        await Task.Delay(500);
    }

    return false;
}
```

## HTTPS通信機能

### APIリクエスト
```csharp
var automation = new AutomationService(browser);

// GET リクエスト
var response = await automation.GetAsync("https://api.example.com/data");

// POST リクエスト
var postData = "param1=value1&param2=value2";
var response = await automation.PostAsync(
    "https://api.example.com/submit",
    postData,
    "application/x-www-form-urlencoded"
);

// JSON API リクエスト
var jsonObject = new { name = "太郎", age = 30 };
var response = await automation.PostJsonAsync("https://api.example.com/user", jsonObject);

if (response.IsSuccess)
{
    Console.WriteLine($"レスポンス: {response.Content}");
}
```

### 基本操作
1. **アカウント選択**: コンボボックスから「アカウント1～10」を選択
2. **即座の切り替え**: 選択と同時に該当アカウントのデータがフォームに反映
3. **データ入力**: 各セクションに必要な情報を入力
   - **ログイン情報**: ID・パスワード
   - **ネットワーク設定**: Proxy設定・ローテーション設定
   - **購入者情報**: 氏名・カナ・電話・メール
   - **クレジットカード**: 番号・CVV・有効期限・名義
4. **保存**: 「保存」ボタンでアカウントに設定を保存・有効化
5. **切り替え**: 別のアカウント番号を選択して即座に切り替え

### アカウント表示ステータス
```
アカウント1 - 未設定              # データが未入力
アカウント2 - user@mail.com       # ログイン情報設定済み
アカウント3 - 設定中              # 一部データ入力済み
アカウント4 - taro@mail.com ●     # 完全設定済み（●マーク）
アカウント5 - 未設定 (無効)        # 削除済みアカウント
```

### ボタン機能
- **保存** (緑): 現在の入力内容をアカウントに保存し、有効化
- **クリア** (黄): フォームの内容をクリア（アカウントデータは変更なし）
- **削除** (赤): 選択したアカウントのデータを完全削除・無効化

### アカウントデータの構造
各アカウントは以下の4つのカテゴリを一括管理：

#### 1. ログイン情報
- ログインID
- パスワード

#### 2. ネットワーク設定
- Proxyホスト・ポート
- Proxy認証（ユーザー名・パスワード）
- ローテーション設定（毎回/時間間隔）

#### 3. 購入者情報
- 氏名（姓・名）
- カナ（セイ・メイ）
- 電話番号（3分割）
- メールアドレス

#### 4. クレジットカード情報
- カード番号
- CVVコード
- 有効期限（月・年）
- カード名義人

### データ完整性チェック機能
```csharp
// 各カテゴリの完全性を個別チェック
bool hasCompleteLogin = account.HasCompleteLoginInfo();          // ID+パスワード
bool hasCompletePurchaser = account.HasCompletePurchaserInfo();  // 氏名+メール
bool hasCompleteCard = account.HasCompleteCreditCardInfo();      // カード+CVV+期限
bool hasAnyData = account.HasData();                             // 何らかのデータ
```

### 高度な機能

#### アカウントクローン
```csharp
// 既存アカウントの複製作成
var originalAccount = accountManager.GetAccountByNumber(1);
var clonedAccount = originalAccount.Clone();
clonedAccount.AccountNumber = 2;
await accountManager.UpdateAccountAsync(clonedAccount);
```

#### プログラムからのアカウント操作
```csharp
// アカウント番号での直接切り替え
autoPurchaseControl.SelectAccount(3);

// 現在のアカウント番号取得
int currentAccountNumber = autoPurchaseControl.GetCurrentAccountNumber();

// 強制保存
bool saved = await autoPurchaseControl.SaveCurrentAccountAsync();
```

## 設定ファイル
- **アカウント情報**: `%APPDATA%/fastBOT/accounts.json` - 10個のアカウントスロットデータ
- **一般設定**: `%LOCALAPPDATA%/fastBOT/settings.json` - アプリケーション設定
- **HTMLログ**: `デスクトップ/fastBOT_HTML/` - デバッグ用HTML保存

### アカウントデータファイルの構造
```json
[
  {
    "Id": "unique-guid",
    "AccountNumber": 1,
    "AccountName": "アカウント1",
    "LoginId": "user@example.com",
    "Password": "encrypted-password",
    "ProxyHost": "proxy.example.com",
    "ProxyPort": 8080,
    "UseProxyRotation": true,
    "RotationIntervalSeconds": 30,
    "LastName": "田中",
    "FirstName": "太郎",
    "Email": "taro@example.com",
    "CardNumber": "1234-5678-9012-3456",
    "IsActive": true,
    "CreatedAt": "2025-01-01T00:00:00Z",
    "UpdatedAt": "2025-01-01T12:00:00Z"
  }
]
```

## ユーザー設定の永続化

### 設定の保存・読み込み
```csharp
// ユーザー設定の作成
var settings = new UserSettings
{
    LoginId = "user@example.com",
    Password = "password123",
    LastName = "田中",
    FirstName = "太郎",
    Email = "taro@example.com",
    DefaultProxy = "127.0.0.1:8080:user:pass",
    CheckInterval = 2000
};

// 設定を保存
settings.Save();

// 設定を読み込み
var loadedSettings = UserSettings.Load();
```

## RequestContextManager（インスタンス管理）

### インスタンス別のキャッシュ管理
```csharp
var contextManager = new RequestContextManager();

// 分離されたコンテキストを作成
var context1 = contextManager.CreateIsolatedContext("Session1");
var context2 = contextManager.CreateIsolatedContext("Session2");

// コンテキストを取得
var existingContext = contextManager.GetContext("Session1");

// デフォルトコンテキストを取得
var defaultContext = contextManager.GetDefaultContext();

// インスタンス情報を取得
int instanceNumber = contextManager.GetInstanceNumber();
string cachePath = contextManager.GetBaseCachePath();
long cacheSize = contextManager.GetCacheSize();

// キャッシュをクリア
bool success = contextManager.ClearCache("Session1"); // 特定のコンテキスト
bool allCleared = contextManager.ClearCache(); // 全体のキャッシュ

// 全インスタンスの情報を取得
var allInstances = RequestContextManager.GetAllInstancesInfo();
foreach (var instance in allInstances)
{
    Console.WriteLine($"Instance {instance.InstanceNumber}: {instance.GetFormattedCacheSize()}");
}

// 非アクティブなインスタンスをクリーンアップ
int cleanedCount = RequestContextManager.CleanupInactiveInstances();
```

## AutoPurchaseControl（自動購入UI）

### 自動購入コントロールの使用
```csharp
var autoPurchaseControl = new AutoPurchaseControl();

// ブラウザサービスを設定
autoPurchaseControl.SetBrowserServices(tabManager, contextManager);

// HTMLを取得
string currentHtml = await autoPurchaseControl.GetCurrentHtmlAsync();
string lastHtml = autoPurchaseControl.GetLastHtmlContent();
var pageInfo = autoPurchaseControl.GetLastPageInfo();

// 特定要素のHTMLを取得
string elementHtml = await autoPurchaseControl.GetElementHtmlAsync("#content");
```

## 完全な自動購入ワークフロー（HTMLパーサー活用版）

### HTMLパーサーを使ったインテリジェント自動購入
```csharp
var mainWindow = new MainWindow();
var automation = new AutomationService(browser);
var htmlExtractor = new HtmlExtractionService(browser);

// 1. Proxy設定
var proxyConfig = new ProxyConfig
{
    Host = "proxy.example.com",
    Port = 8080,
    Username = "proxyuser",
    Password = "proxypass"
};
await proxyManager.SetProxyAsync(browser, proxyConfig);

// 2. ページ読み込み待機
await automation.WaitForPageLoadAsync(30);

// 3. ページ構造の解析
var structuredData = await _tabManager.ExtractStructuredDataAsync();
Console.WriteLine($"解析完了: {structuredData.Links.Count}個のリンク, {structuredData.Images.Count}個の画像");

// 4. フォーム構造を解析してから自動ログイン
var formParams = await htmlExtractor.GetFormParametersAsync("form");
bool loginSuccess = false;

if (formParams.ContainsKey("username") || formParams.ContainsKey("email"))
{
    loginSuccess = await automation.AutoLoginAsync(
        "username",
        "password",
        "input[type='submit'], button[type='submit']",
        "input[name='username'], input[name='email']",
        "input[name='password']"
    );
}

if (!loginSuccess)
{
    Console.WriteLine("ログインに失敗しました");
    return;
}

// 5. 商品情報を解析してチケット選択
var products = await _tabManager.ExtractProductsAsync();
var availableTickets = products.Where(p => 
    !p.Price.Contains("売り切れ") && 
    !p.Price.Contains("完売") &&
    p.Name.Contains("プレミア")).ToList();

if (!availableTickets.Any())
{
    Console.WriteLine("利用可能なチケットがありません");
    return;
}

// 最初の利用可能なチケットを選択
var selectedTicket = availableTickets.First();
Console.WriteLine($"選択されたチケット: {selectedTicket.Name} - {selectedTicket.Price}");

// 6. 動的にセレクターを構築してチケット購入
var ticketSelectors = new List<string>();

// 商品名からセレクターを推測
var productElements = await htmlExtractor.SelectNodesByCssSelectorAsync(
    $"*[text()*='{selectedTicket.Name}'], *[title*='{selectedTicket.Name}']"
);

if (productElements.Any())
{
    // 最初の要素の親要素から購入ボタンを探す
    var purchaseButtons = await htmlExtractor.SelectNodesByCssSelectorAsync(
        ".purchase, .buy, input[value*='購入'], button:contains('購入')"
    );
    
    if (purchaseButtons.Any())
    {
        ticketSelectors.Add(".purchase, .buy, input[value*='購入'], button:contains('購入')");
    }
}

bool purchaseSuccess = await automation.AutoPurchaseTicketsAsync(
    ticketSelectors,
    ".purchase-button, .buy-button, input[type='submit']"
);

// 7. フォーム構造を解析してスマート入力
var checkoutFormParams = await htmlExtractor.GetFormParametersAsync();
var smartFormData = new Dictionary<string, string>();

// フィールドの種類を自動判別
foreach (var param in checkoutFormParams.Keys)
{
    var fieldName = param.ToLower();
    
    if (fieldName.Contains("lastname") || fieldName.Contains("family"))
        smartFormData[param] = "田中";
    else if (fieldName.Contains("firstname") || fieldName.Contains("given"))
        smartFormData[param] = "太郎";
    else if (fieldName.Contains("email"))
        smartFormData[param] = "taro@example.com";
    else if (fieldName.Contains("phone") || fieldName.Contains("tel"))
        smartFormData[param] = "090-1234-5678";
    else if (fieldName.Contains("zip") || fieldName.Contains("postal"))
        smartFormData[param] = "123-4567";
    else if (fieldName.Contains("address1") || fieldName.Contains("addr1"))
        smartFormData[param] = "東京都渋谷区";
    else if (fieldName.Contains("card") && fieldName.Contains("number"))
        smartFormData[param] = "1234567890123456";
    else if (fieldName.Contains("cvv") || fieldName.Contains("cvc"))
        smartFormData[param] = "123";
}

int filledCount = await _tabManager.AutoFillFormAsync(smartFormData);
Console.WriteLine($"{filledCount}個のフィールドに自動入力完了");

// 8. 確認ページの要素待機（高度な待機条件）
bool confirmationReady = await WaitForConfirmationPageAsync(htmlExtractor, 10);

if (confirmationReady)
{
    Console.WriteLine("確認ページが表示されました");
    
    // 確認ページの内容を解析
    var confirmationData = await htmlExtractor.ExtractStructuredDataAsync();
    var orderSummary = await htmlExtractor.SelectNodesByCssSelectorAsync(
        ".order-summary, .purchase-summary, .confirmation-details"
    );
    
    if (orderSummary.Any())
    {
        var summaryText = htmlExtractor.GetInnerText(orderSummary.First());
        Console.WriteLine($"注文概要: {summaryText}");
    }
}

// 9. HTMLの完全取得・保存
var htmlData = await _tabManager.ExtractHtmlAsync(HtmlDataType.FullPage, autoSave: true);
Console.WriteLine($"購入完了時のHTML保存: {htmlData.FilePath}");

// 10. 購入結果の検証
var successIndicators = await htmlExtractor.SelectNodesByCssSelectorAsync(
    ".success, .complete, .thank-you, *:contains('完了'), *:contains('ありがとう')"
);

if (successIndicators.Any())
{
    Console.WriteLine("購入が正常に完了しました");
    
    // 注文番号を抽出
    var orderNumbers = await htmlExtractor.SelectNodesByCssSelectorAsync(
        "*:contains('注文番号'), *:contains('オーダー'), *:contains('Order')"
    );
    
    foreach (var orderElement in orderNumbers.Take(3))
    {
        var orderText = htmlExtractor.GetInnerText(orderElement);
        Console.WriteLine($"注文情報: {orderText}");
    }
}
else
{
    Console.WriteLine("購入完了の確認ができませんでした");
}

// 高度な確認ページ待機の実装
async Task<bool> WaitForConfirmationPageAsync(HtmlExtractionService extractor, int timeoutSeconds)
{
    var startTime = DateTime.Now;
    var timeout = TimeSpan.FromSeconds(timeoutSeconds);

    while (DateTime.Now - startTime < timeout)
    {
        // 複数の条件をチェック
        var confirmationElements = await extractor.SelectNodesByCssSelectorAsync(
            ".confirmation, .complete, .thank-you, .success"
        );
        
        var loadingElements = await extractor.SelectNodesByCssSelectorAsync(
            ".loading, .spinner, .processing"
        );
        
        var errorElements = await extractor.SelectNodesByCssSelectorAsync(
            ".error, .alert-danger, .fail"
        );

        // 確認要素があり、ローディング要素がなく、エラー要素がない
        if (confirmationElements.Any() && !loadingElements.Any() && !errorElements.Any())
        {
            return true;
        }
        
        // エラーが発生した場合は即座に終了
        if (errorElements.Any())
        {
            var errorText = extractor.GetInnerText(errorElements.First());
            Console.WriteLine($"エラーが発生しました: {errorText}");
            return false;
        }

        await Task.Delay(500);
    }

    return false;
}
```

## HTMLパーサーを使った高度なデータ収集

### 競合サイトの価格監視
```csharp
public class PriceMonitor
{
    private readonly HtmlExtractionService _htmlExtractor;
    
    public async Task<List<PriceInfo>> MonitorPricesAsync(List<string> targetUrls)
    {
        var priceInfos = new List<PriceInfo>();
        
        foreach (var url in targetUrls)
        {
            try
            {
                // URLに移動
                browser.Address = url;
                await Task.Delay(3000); // ページ読み込み待機
                
                // 価格要素を様々なパターンで検索
                var priceSelectors = new[]
                {
                    ".price", ".cost", ".amount", ".fee",
                    "*[class*='price']", "*[class*='cost']",
                    "*:contains('¥')", "*:contains('円')", "*:contains(')"
                };
                
                foreach (var selector in priceSelectors)
                {
                    var priceElements = await _htmlExtractor.SelectNodesByCssSelectorAsync(selector);
                    
                    foreach (var element in priceElements.Take(3))
                    {
                        var priceText = _htmlExtractor.GetInnerText(element);
                        var cleanPrice = CleanPriceText(priceText);
                        
                        if (!string.IsNullOrEmpty(cleanPrice))
                        {
                            priceInfos.Add(new PriceInfo
                            {
                                Url = url,
                                Price = cleanPrice,
                                Selector = selector,
                                ExtractedAt = DateTime.Now
                            });
                            break; // 最初に見つかった有効な価格を使用
                        }
                    }
                    
                    if (priceInfos.Any(p => p.Url == url)) break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"価格監視エラー ({url}): {ex.Message}");
            }
        }
        
        return priceInfos;
    }
    
    private string CleanPriceText(string priceText)
    {
        if (string.IsNullOrEmpty(priceText)) return null;
        
        // 価格パターンの正規表現
        var pricePattern = @"[\¥$]?[\d,]+(?:\.\d{2})?";
        var match = System.Text.RegularExpressions.Regex.Match(priceText, pricePattern);
        
        return match.Success ? match.Value : null;
    }
}

public class PriceInfo
{
    public string Url { get; set; }
    public string Price { get; set; }
    public string Selector { get; set; }
    public DateTime ExtractedAt { get; set; }
}
```

### フォーム分析とバリデーションチェック
```csharp
public class FormAnalyzer
{
    private readonly HtmlExtractionService _htmlExtractor;
    
    public async Task<FormAnalysisResult> AnalyzeFormAsync(string formSelector = "form")
    {
        var result = new FormAnalysisResult();
        
        // フォーム要素を取得
        var formElement = await _htmlExtractor.SelectSingleNodeByCssSelectorAsync(formSelector);
        if (formElement == null)
        {
            result.IsValid = false;
            result.Errors.Add("指定されたフォームが見つかりません");
            return result;
        }
        
        // フォーム内の全入力要素を解析
        var inputElements = await _htmlExtractor.SelectNodesByCssSelectorAsync(
            $"{formSelector} input, {formSelector} select, {formSelector} textarea"
        );
        
        foreach (var input in inputElements)
        {
            var fieldInfo = new FormFieldInfo
            {
                Name = _htmlExtractor.GetAttributeValue(input, "name"),
                Type = _htmlExtractor.GetAttributeValue(input, "type"),
                IsRequired = !string.IsNullOrEmpty(_htmlExtractor.GetAttributeValue(input, "required")),
                Placeholder = _htmlExtractor.GetAttributeValue(input, "placeholder"),
                Value = _htmlExtractor.GetAttributeValue(input, "value")
            };
            
            result.Fields.Add(fieldInfo);
        }
        
        // 必須フィールドのチェック
        var requiredFields = result.Fields.Where(f => f.IsRequired).ToList();
        var emptyRequired = requiredFields.Where(f => string.IsNullOrEmpty(f.Value)).ToList();
        
        if (emptyRequired.Any())
        {
            result.Errors.Add($"必須フィールドが未入力: {string.Join(", ", emptyRequired.Select(f => f.Name))}");
        }
        
        // 送信ボタンの状態チェック
        var submitButtons = await _htmlExtractor.SelectNodesByCssSelectorAsync(
            $"{formSelector} input[type='submit'], {formSelector} button[type='submit']"
        );
        
        result.HasSubmitButton = submitButtons.Any();
        
        if (submitButtons.Any())
        {
            var firstSubmit = submitButtons.First();
            var isDisabled = !string.IsNullOrEmpty(_htmlExtractor.GetAttributeValue(firstSubmit, "disabled"));
            result.IsSubmitEnabled = !isDisabled;
        }
        
        result.IsValid = !result.Errors.Any() && result.HasSubmitButton && result.IsSubmitEnabled;
        
        return result;
    }
}

public class FormAnalysisResult
{
    public bool IsValid { get; set; }
    public List<FormFieldInfo> Fields { get; set; } = new List<FormFieldInfo>();
    public List<string> Errors { get; set; } = new List<string>();
    public bool HasSubmitButton { get; set; }
    public bool IsSubmitEnabled { get; set; }
}

public class FormFieldInfo
{
    public string Name { get; set; }
    public string Type { get; set; }
    public bool IsRequired { get; set; }
    public string Placeholder { get; set; }
    public string Value { get; set; }
}
```

## 高度なProxy管理

### 動的Proxy切り替え
```csharp
var proxyManager = new ProxyManager();

// 複数のProxyを順次適用
var proxies = new List<ProxyConfig>
{
    new ProxyConfig { Host = "proxy1.com", Port = 8080, Username = "user1", Password = "pass1" },
    new ProxyConfig { Host = "proxy2.com", Port = 8080, Username = "user2", Password = "pass2" },
    new ProxyConfig { Host = "proxy3.com", Port = 8080, Username = "user3", Password = "pass3" }
};

foreach (var proxy in proxies)
{
    await proxyManager.SetProxyAsync(browser, proxy);
    // 各Proxyで処理を実行
    await Task.Delay(5000);
}

// Proxyを無効化
await proxyManager.DisableProxyAsync(browser);
```

## エラーハンドリングとベストプラクティス

### HTMLパーサーのエラーハンドリング
```csharp
try
{
    // HTML取得・解析
    var htmlData = await htmlExtractor.GetPageHtmlAsync();
    if (string.IsNullOrEmpty(htmlData))
    {
        throw new InvalidOperationException("HTMLコンテンツが空です");
    }
    
    // CSSセレクターでの要素検索
    var elements = await htmlExtractor.SelectNodesByCssSelectorAsync(".target-element");
    if (!elements.Any())
    {
        Console.WriteLine("警告: 対象要素が見つかりませんでした");
        // フォールバック用の別セレクターを試行
        elements = await htmlExtractor.SelectNodesByCssSelectorAsync("*[class*='target']");
    }
    
    // フォームパラメータの抽出
    var formParams = await htmlExtractor.GetFormParametersAsync();
    if (!formParams.Any())
    {
        Console.WriteLine("警告: フォームパラメータが見つかりませんでした");
    }
    
    // 構造化データの抽出
    var structuredData = await _tabManager.ExtractStructuredDataAsync();
    Console.WriteLine($"解析完了: リンク{structuredData.Links.Count}個, 画像{structuredData.Images.Count}個");
    
}
catch (TimeoutException)
{
    Console.WriteLine("HTML取得がタイムアウトしました");
}
catch (InvalidOperationException ex)
{
    Console.WriteLine($"操作エラー: {ex.Message}");
}
catch (ArgumentException ex)
{
    Console.WriteLine($"引数エラー: {ex.Message}");
}
catch (Exception ex)
{
    Console.WriteLine($"予期しないエラー: {ex.Message}");
}
```

### パフォーマンス最適化（HTMLパーサー使用）
```csharp
// 並列処理での効率化
var htmlExtractor = new HtmlExtractionService(browser);

var extractionTasks = new List<Task>
{
    // 基本データの並列取得
    htmlExtractor.GetPageHtmlAsync(),
    htmlExtractor.GetPageInfoAsync(),
    htmlExtractor.ExtractMetaTagsAsync(),
    
    // 構造化データの並列取得
    htmlExtractor.ExtractLinksAsync(),
    htmlExtractor.ExtractImagesAsync(),
    htmlExtractor.ExtractTableDataAsync()
};

// すべての取得処理を並列実行
await Task.WhenAll(extractionTasks);

// 結果の取得
var pageHtml = ((Task<string>)extractionTasks[0]).Result;
var pageInfo = ((Task<PageInfo>)extractionTasks[1]).Result;
var metaTags = ((Task<Dictionary<string, string>>)extractionTasks[2]).Result;

Console.WriteLine($"並列処理完了: HTML {pageHtml.Length}文字, メタタグ {metaTags.Count}個");

// リソースの適切な解放
// HtmlExtractionServiceは使い回し可能
// htmlExtractor は BrowserTab の Dispose時に自動的にクリーンアップされる
```

## デバッグとモニタリング

### HTMLパーサーのデバッグ情報
```csharp
// HTMLパーサーの詳細ログ
var htmlExtractor = new HtmlExtractionService(browser);

// 解析結果をファイルに出力
bool debugSaved = await htmlExtractor.WriteAnalysisResultAsync("C:\\temp\\debug\\");
Console.WriteLine($"デバッグファイル保存: {debugSaved}");

// フォームパラメータのトレース
var formParams = await htmlExtractor.GetFormParametersAsync();
htmlExtractor.TraceParameters(formParams);

// パース済みHTMLの保存
bool parsedSaved = await htmlExtractor.SaveParsedHtmlToFileAsync("parsed_debug.html");

// 最後に取得したHTMLコンテンツの確認
string lastHtml = htmlExtractor.LastHtmlContent;
Console.WriteLine($"最後のHTML取得: {lastHtml?.Length ?? 0}文字");

// HTMLファイルの管理状況
var savedFiles = _tabManager.GetSavedHtmlFiles();
foreach (var file in savedFiles)
{
    Console.WriteLine($"保存済みファイル: {file.FileName} ({file.Size} bytes) - {file.CreatedAt}");
}

// インスタンス管理情報の表示
mainWindow.ShowInstanceManagementInfo();

// キャッシュサイズの確認
long cacheSize = mainWindow.GetCurrentInstanceCacheSize();
Console.WriteLine($"現在のキャッシュサイズ: {FormatBytes(cacheSize)}");
```

## 注意事項とベストプラクティス

### HTMLパーサー使用時の重要な注意点
- **CSSセレクター**: Fizzler.Systems.HtmlAgilityPackを使用してW3C標準準拠
- **要素検索**: 要素が見つからない場合は空のリストが返される（例外なし）
- **HTMLコンテンツ**: CEFの`GetSourceAsync()`で直接取得（JavaScript非依存）
- **フォーム解析**: name属性ベースでのパラメータ抽出
- **ファイル保存**: 自動タイムスタンプ付きファイル名生成
- **エラーハンドリング**: 各メソッドで適切なtry-catch実装

### 推奨パターン（HTMLパーサー使用）
```csharp
// 1. 初期化の順序
var htmlExtractor = new HtmlExtractionService(browser);
await browser.WaitForInitializationAsync(); // ブラウザ初期化待機

// 2. ページ読み込み完了後のHTML解析
await automation.WaitForPageLoadAsync(30);
var pageInfo = await htmlExtractor.GetPageInfoAsync();

// 3. 段階的な要素検索（フォールバック付き）
var primaryElements = await htmlExtractor.SelectNodesByCssSelectorAsync(".primary-target");
if (!primaryElements.Any())
{
    var fallbackElements = await htmlExtractor.SelectNodesByCssSelectorAsync("*[class*='target']");
    if (!fallbackElements.Any())
    {
        var xpathElements = await htmlExtractor.SelectNodesByXPathAsync("//div[contains(@class, 'target')]");
    }
}

// 4. フォーム解析からの自動入力
var formParams = await htmlExtractor.GetFormParametersAsync();
if (formParams.Any())
{
    var smartFormData = BuildSmartFormData(formParams);
    await _tabManager.AutoFillFormAsync(smartFormData);
}

// 5. 構造化データの活用
var structuredData = await _tabManager.ExtractStructuredDataAsync();
LogStructuredData(structuredData);

// 6. リソース管理
// HtmlExtractionServiceは BrowserTab のライフサイクルに依存
// 明示的なDisposeは不要（BrowserTabManager が管理）
```

## プロジェクト構造

```
CefSharp.fastBOT/
├── Core/
│   ├── AccountManager.cs          # 10スロットアカウント管理（改良版）
│   ├── ProxyManager.cs            # Proxy設定・ローテーション管理
│   ├── HtmlExtractionService.cs   # HTML解析・データ抽出
│   ├── AutomationService.cs       # ブラウザ自動化サービス
│   ├── BrowserTabManager.cs       # タブ管理・切り替え
│   └── RequestContextManager.cs   # コンテキスト・キャッシュ管理
├── Models/
│   ├── AccountInfo.cs             # アカウント情報モデル（改良版）
│   ├── ProxyConfig.cs             # Proxy設定モデル
│   ├── PageInfo.cs                # ページ情報モデル
│   ├── UserSettings.cs            # ユーザー設定モデル
│   └── HtmlData.cs                # HTML解析結果モデル
├── UI/
│   ├── MainWindow.xaml            # メインウィンドウ
│   ├── AutoPurchaseControl.xaml   # 自動購入コントロール（改良版）
│   └── BrowserControl.xaml        # ブラウザコントロール
├── Utils/
│   ├── BrowserAutomationUtils.cs  # ブラウザ操作ユーティリティ
│   ├── FileUtils.cs               # ファイル操作ユーティリティ
│   └── HtmlParserUtils.cs         # HTML解析ユーティリティ
└── Scripts/
    ├── setup.ps1                  # 開発環境セットアップ
    └── build.ps1                  # ビルドスクリプト
```

### アカウント管理機能の主要ファイル
- **AccountManager.cs**: 10個のアカウントスロット管理・永続化
- **AccountInfo.cs**: アカウントデータモデル・バリデーション・クローン機能
- **AutoPurchaseControl.xaml.cs**: UI統合・即座切り替え・自動保存
