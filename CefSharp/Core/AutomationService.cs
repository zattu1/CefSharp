using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Input;
using CefSharp.Wpf;
using CefSharp.fastBOT.Utils;
using CefSharp.fastBOT.Models;

namespace CefSharp.fastBOT.Core
{
    /// <summary>
    /// ブラウザ自動化サービス - MainWindowで使用する統合クラス
    /// </summary>
    public class AutomationService : IDisposable
    {
        private readonly ChromiumWebBrowser _browser;
        private readonly HttpsClient _httpsClient;
        private bool _disposed = false;

        public AutomationService(ChromiumWebBrowser browser)
        {
            _browser = browser ?? throw new ArgumentNullException(nameof(browser));
            _httpsClient = new HttpsClient();
        }

        #region Cookie操作

        /// <summary>
        /// 現在のページのCookieを取得
        /// </summary>
        /// <param name="cookieName">特定のCookie名（省略可）</param>
        /// <returns>Cookie一覧</returns>
        public async Task<List<Cookie>> GetCurrentPageCookiesAsync(string cookieName = null)
        {
            var currentUrl = _browser.Address;
            return await BrowserAutomationUtils.GetCookiesAsync(currentUrl, cookieName);
        }

        /// <summary>
        /// 現在のページにCookieを設定
        /// </summary>
        /// <param name="name">Cookie名</param>
        /// <param name="value">Cookie値</param>
        /// <param name="httpOnly">HttpOnlyフラグ</param>
        /// <param name="secure">Secureフラグ</param>
        /// <returns>設定成功/失敗</returns>
        public bool SetCurrentPageCookieAsync(string name, string value,
            bool httpOnly = false, bool secure = true)
        {
            var currentUrl = _browser.Address;
            var uri = new Uri(currentUrl);

            return BrowserAutomationUtils.UpdateCookieAsync(
                currentUrl, name, value, uri.Host, "/", httpOnly, secure);
        }

        #endregion

        #region 自動操作

        /// <summary>
        /// 要素をIDで検索してクリック
        /// </summary>
        /// <param name="elementId">要素のID</param>
        /// <returns>クリック成功/失敗</returns>
        public async Task<bool> ClickElementByIdAsync(string elementId)
        {
            var script = $"document.getElementById('{elementId}')?.click(); true;";
            var result = await BrowserAutomationUtils.ExecuteScriptAsync(_browser, script);
            return result != null;
        }

        /// <summary>
        /// 要素をCSSセレクタで検索してクリック
        /// </summary>
        /// <param name="selector">CSSセレクタ</param>
        /// <returns>クリック成功/失敗</returns>
        public async Task<bool> ClickElementBySelectorAsync(string selector)
        {
            var script = $"document.querySelector('{selector}')?.click(); true;";
            var result = await BrowserAutomationUtils.ExecuteScriptAsync(_browser, script);
            return result != null;
        }

        /// <summary>
        /// テキストボックスに値を入力
        /// </summary>
        /// <param name="elementId">要素のID</param>
        /// <param name="text">入力テキスト</param>
        /// <returns>入力成功/失敗</returns>
        public async Task<bool> SetTextByIdAsync(string elementId, string text)
        {
            var script = $@"
                var element = document.getElementById('{elementId}');
                if (element) {{
                    element.value = '{text.Replace("'", "\\'")}';
                    element.dispatchEvent(new Event('input', {{ bubbles: true }}));
                    element.dispatchEvent(new Event('change', {{ bubbles: true }}));
                    return true;
                }}
                return false;";

            var result = await BrowserAutomationUtils.ExecuteScriptAsync(_browser, script);
            return result?.ToString() == "True";
        }

        /// <summary>
        /// フォームを自動入力
        /// </summary>
        /// <param name="formData">フォームデータ（ID: 値）</param>
        /// <returns>入力成功数</returns>
        public async Task<int> FillFormAsync(Dictionary<string, string> formData)
        {
            int successCount = 0;

            foreach (var kvp in formData)
            {
                if (await SetTextByIdAsync(kvp.Key, kvp.Value))
                {
                    successCount++;
                }

                // 少し待機
                await Task.Delay(100);
            }

            return successCount;
        }

        /// <summary>
        /// ページが完全に読み込まれるまで待機
        /// </summary>
        /// <param name="timeoutSeconds">タイムアウト時間（秒）</param>
        /// <returns>読み込み完了/タイムアウト</returns>
        public async Task<bool> WaitForPageLoadAsync(int timeoutSeconds = 30)
        {
            var endTime = DateTime.Now.AddSeconds(timeoutSeconds);

            while (DateTime.Now < endTime)
            {
                var readyState = await BrowserAutomationUtils.ExecuteScriptAsync(_browser,
                    "document.readyState");

                if (readyState?.ToString() == "complete")
                {
                    return true;
                }

                await Task.Delay(500);
            }

            return false;
        }

        /// <summary>
        /// 要素が存在するまで待機
        /// </summary>
        /// <param name="selector">CSSセレクタ</param>
        /// <param name="timeoutSeconds">タイムアウト時間（秒）</param>
        /// <returns>要素発見/タイムアウト</returns>
        public async Task<bool> WaitForElementAsync(string selector, int timeoutSeconds = 10)
        {
            var endTime = DateTime.Now.AddSeconds(timeoutSeconds);

            while (DateTime.Now < endTime)
            {
                var element = await BrowserAutomationUtils.ExecuteScriptAsync(_browser,
                    $"document.querySelector('{selector}') !== null");

                if (element?.ToString() == "True")
                {
                    return true;
                }

                await Task.Delay(500);
            }

            return false;
        }

        #endregion

        #region 座標操作

        /// <summary>
        /// 座標をクリック
        /// </summary>
        /// <param name="x">X座標</param>
        /// <param name="y">Y座標</param>
        /// <param name="isRightClick">右クリックかどうか</param>
        /// <returns>クリック成功/失敗</returns>
        /// <summary>
        /// 座標をクリック
        /// </summary>
        /// <param name="x">X座標</param>
        /// <param name="y">Y座標</param>
        /// <param name="isRightClick">右クリックかどうか</param>
        /// <returns>クリック成功/失敗</returns>
        public bool ClickCoordinate(int x, int y, bool isRightClick = false)
        {
            return BrowserAutomationUtils.SendMouseClick(_browser, x, y, isRightClick);
        }

        /// <summary>
        /// テキストを直接入力
        /// </summary>
        /// <param name="text">入力テキスト</param>
        /// <returns>入力成功/失敗</returns>
        public bool SendText(string text)
        {
            return BrowserAutomationUtils.SendKeyboardInput(_browser, text);
        }

        /// <summary>
        /// キーを送信
        /// </summary>
        /// <param name="key">キー</param>
        /// <param name="isKeyDown">キーダウンかどうか</param>
        /// <returns>送信成功/失敗</returns>
        public bool SendKey(Key key, bool isKeyDown = true)
        {
            return BrowserAutomationUtils.SendKeyCode(_browser, (int)key, isKeyDown);
        }

        /// <summary>
        /// Enter キーを送信
        /// </summary>
        /// <returns>送信成功/失敗</returns>
        public bool SendEnter()
        {
            return SendKey(Key.Enter);
        }

        /// <summary>
        /// Tab キーを送信
        /// </summary>
        /// <returns>送信成功/失敗</returns>
        public bool SendTab()
        {
            return SendKey(Key.Tab);
        }

        #endregion

        #region HTTPS通信

        /// <summary>
        /// APIにGETリクエストを送信
        /// </summary>
        /// <param name="url">リクエストURL</param>
        /// <param name="headers">追加ヘッダー</param>
        /// <returns>レスポンス</returns>
        public async Task<HttpsResponse> GetAsync(string url, Dictionary<string, string> headers = null)
        {
            return await _httpsClient.GetAsync(url, headers);
        }

        /// <summary>
        /// APIにPOSTリクエストを送信
        /// </summary>
        /// <param name="url">リクエストURL</param>
        /// <param name="data">送信データ</param>
        /// <param name="contentType">コンテンツタイプ</param>
        /// <param name="headers">追加ヘッダー</param>
        /// <returns>レスポンス</returns>
        public async Task<HttpsResponse> PostAsync(string url, string data = null,
            string contentType = "application/x-www-form-urlencoded",
            Dictionary<string, string> headers = null)
        {
            return await _httpsClient.PostAsync(url, data, contentType, headers);
        }

        /// <summary>
        /// JSON APIにリクエストを送信
        /// </summary>
        /// <param name="url">リクエストURL</param>
        /// <param name="jsonObject">JSONオブジェクト</param>
        /// <param name="headers">追加ヘッダー</param>
        /// <returns>レスポンス</returns>
        public async Task<HttpsResponse> PostJsonAsync(string url, object jsonObject,
            Dictionary<string, string> headers = null)
        {
            return await _httpsClient.PostJsonAsync(url, jsonObject, headers);
        }

        #endregion

        #region スクリーンショット・ページ情報

        /// <summary>
        /// 現在のページタイトルを取得
        /// </summary>
        /// <returns>ページタイトル</returns>
        public async Task<string> GetPageTitleAsync()
        {
            var result = await BrowserAutomationUtils.ExecuteScriptAsync(_browser, "document.title");
            return result?.ToString() ?? "";
        }

        /// <summary>
        /// 現在のページURLを取得
        /// </summary>
        /// <returns>ページURL</returns>
        public string GetCurrentUrl()
        {
            return _browser.Address;
        }

        /// <summary>
        /// ページのHTMLソースを取得
        /// </summary>
        /// <returns>HTMLソース</returns>
        public async Task<string> GetPageSourceAsync()
        {
            var result = await BrowserAutomationUtils.ExecuteScriptAsync(_browser,
                "document.documentElement.outerHTML");
            return result?.ToString() ?? "";
        }

        /// <summary>
        /// 要素のテキストを取得
        /// </summary>
        /// <param name="selector">CSSセレクタ</param>
        /// <returns>要素のテキスト</returns>
        public async Task<string> GetElementTextAsync(string selector)
        {
            var script = $"document.querySelector('{selector}')?.textContent || ''";
            var result = await BrowserAutomationUtils.ExecuteScriptAsync(_browser, script);
            return result?.ToString() ?? "";
        }

        /// <summary>
        /// 要素の値を取得
        /// </summary>
        /// <param name="selector">CSSセレクタ</param>
        /// <returns>要素の値</returns>
        public async Task<string> GetElementValueAsync(string selector)
        {
            var script = $"document.querySelector('{selector}')?.value || ''";
            var result = await BrowserAutomationUtils.ExecuteScriptAsync(_browser, script);
            return result?.ToString() ?? "";
        }

        #endregion

        #region 高度な自動化

        /// <summary>
        /// ログインフォームを自動入力してログイン
        /// </summary>
        /// <param name="loginId">ログインID</param>
        /// <param name="password">パスワード</param>
        /// <param name="loginButtonSelector">ログインボタンのセレクタ</param>
        /// <param name="userIdSelector">ユーザーIDフィールドのセレクタ</param>
        /// <param name="passwordSelector">パスワードフィールドのセレクタ</param>
        /// <returns>ログイン実行成功/失敗</returns>
        public async Task<bool> AutoLoginAsync(string loginId, string password,
            string loginButtonSelector = "input[type='submit'], button[type='submit'], .login-btn",
            string userIdSelector = "input[name='loginId'], input[name='userId'], input[name='email']",
            string passwordSelector = "input[name='password'], input[type='password']")
        {
            try
            {
                // ユーザーID入力
                var userIdScript = $@"
                    var userField = document.querySelector('{userIdSelector}');
                    if (userField) {{
                        userField.value = '{loginId.Replace("'", "\\'")}';
                        userField.dispatchEvent(new Event('input', {{ bubbles: true }}));
                        userField.dispatchEvent(new Event('change', {{ bubbles: true }}));
                    }}";
                await BrowserAutomationUtils.ExecuteScriptAsync(_browser, userIdScript);
                await Task.Delay(300);

                // パスワード入力
                var passwordScript = $@"
                    var passField = document.querySelector('{passwordSelector}');
                    if (passField) {{
                        passField.value = '{password.Replace("'", "\\'")}';
                        passField.dispatchEvent(new Event('input', {{ bubbles: true }}));
                        passField.dispatchEvent(new Event('change', {{ bubbles: true }}));
                    }}";
                await BrowserAutomationUtils.ExecuteScriptAsync(_browser, passwordScript);
                await Task.Delay(300);

                // ログインボタンをクリック
                return await ClickElementBySelectorAsync(loginButtonSelector);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"自動ログインエラー: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// チケット選択と購入の自動化
        /// </summary>
        /// <param name="ticketSelectors">チケット選択のセレクタ一覧</param>
        /// <param name="purchaseButtonSelector">購入ボタンのセレクタ</param>
        /// <returns>購入処理実行成功/失敗</returns>
        public async Task<bool> AutoPurchaseTicketsAsync(List<string> ticketSelectors,
            string purchaseButtonSelector = ".purchase-btn, .buy-btn, input[value*='購入']")
        {
            try
            {
                // チケットを順番に選択
                foreach (var selector in ticketSelectors)
                {
                    if (await WaitForElementAsync(selector, 5))
                    {
                        await ClickElementBySelectorAsync(selector);
                        await Task.Delay(500);
                    }
                }

                // 購入ボタンをクリック
                if (await WaitForElementAsync(purchaseButtonSelector, 10))
                {
                    return await ClickElementBySelectorAsync(purchaseButtonSelector);
                }

                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"自動購入エラー: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (!_disposed)
            {
                _httpsClient?.Dispose();
                _disposed = true;
            }
        }

        #endregion
    }
}