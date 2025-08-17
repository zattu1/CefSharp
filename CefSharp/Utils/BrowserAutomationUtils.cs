using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using CefSharp;
using CefSharp.Wpf;

namespace CefSharp.fastBOT.Utils
{
    /// <summary>
    /// ブラウザ自動化のためのユーティリティクラス
    /// </summary>
    public class BrowserAutomationUtils
    {
        #region Win32 API定義

        [DllImport("user32.dll")]
        private static extern bool SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);

        // Windows Message定数
        private const uint WM_LBUTTONDOWN = 0x0201;
        private const uint WM_LBUTTONUP = 0x0202;
        private const uint WM_RBUTTONDOWN = 0x0204;
        private const uint WM_RBUTTONUP = 0x0205;
        private const uint WM_KEYDOWN = 0x0100;
        private const uint WM_KEYUP = 0x0101;
        private const uint WM_CHAR = 0x0102;

        #endregion

        #region Cookie操作

        /// <summary>
        /// 指定URLのCookieを取得（適切な非同期処理版）
        /// </summary>
        /// <param name="url">対象URL</param>
        /// <param name="cookieName">Cookie名（nullの場合は全て取得）</param>
        /// <returns>Cookieのリスト</returns>
        public static async Task<List<Cookie>> GetCookiesAsync(string url, string cookieName = null)
        {
            try
            {
                var cookieManager = Cef.GetGlobalCookieManager();
                var visitor = new CookieCollector();

                if (string.IsNullOrEmpty(cookieName))
                {
                    // 全Cookieを訪問
                    cookieManager.VisitAllCookies(visitor);
                }
                else
                {
                    // 特定URLのCookieを訪問
                    cookieManager.VisitUrlCookies(url, true, visitor);
                }

                // 適切に完了を待機
                await visitor.WaitForCompletion();

                return visitor.Cookies.FindAll(c =>
                    string.IsNullOrEmpty(cookieName) || c.Name.Equals(cookieName, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Cookie取得エラー: {ex.Message}");
                return new List<Cookie>();
            }
        }

        /// <summary>
        /// Cookie収集用のビジター（適切な完了通知付き）
        /// </summary>
        public class CookieCollector : ICookieVisitor
        {
            private readonly TaskCompletionSource<bool> _completionSource = new TaskCompletionSource<bool>();
            private int _expectedCount = -1;
            private int _visitedCount = 0;

            public List<Cookie> Cookies { get; } = new List<Cookie>();

            public bool Visit(Cookie cookie, int count, int total, ref bool deleteCookie)
            {
                Cookies.Add(cookie);
                deleteCookie = false;

                _visitedCount = count;

                // 初回でtotal数を記録
                if (_expectedCount == -1)
                {
                    _expectedCount = total;
                }

                // すべてのCookieを訪問完了した場合
                if (count == total)
                {
                    _completionSource.TrySetResult(true);
                }

                return true;
            }

            /// <summary>
            /// Cookie収集の完了を待機
            /// </summary>
            public Task WaitForCompletion()
            {
                return _completionSource.Task;
            }

            public void Dispose()
            {
                // 念のため完了をセット
                _completionSource.TrySetResult(true);
            }
        }
        /// <summary>
        /// Cookieを更新・設定
        /// </summary>
        /// <param name="url">対象URL</param>
        /// <param name="name">Cookie名</param>
        /// <param name="value">Cookie値</param>
        /// <param name="domain">ドメイン</param>
        /// <param name="path">パス</param>
        /// <param name="httpOnly">HttpOnlyフラグ</param>
        /// <param name="secure">Secureフラグ</param>
        /// <returns>設定成功/失敗</returns>
        public static bool UpdateCookieAsync(string url, string name, string value,
            string domain = null, string path = "/", bool httpOnly = false, bool secure = false)
        {
            try
            {
                var cookieManager = Cef.GetGlobalCookieManager();
                var uri = new Uri(url);

                var cookie = new Cookie
                {
                    Name = name,
                    Value = value,
                    Domain = domain ?? uri.Host,
                    Path = path,
                    HttpOnly = httpOnly,
                    Secure = secure,
                    Expires = DateTime.Now.AddYears(1)
                };

                return cookieManager.SetCookie(url, cookie);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Cookie更新エラー: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region HTML操作

        /// <summary>
        /// HTMLデータをブラウザに反映
        /// </summary>
        /// <param name="browser">対象ブラウザ</param>
        /// <param name="html">HTMLデータ</param>
        /// <param name="url">ベースURL</param>
        public static async Task LoadHtmlAsync(ChromiumWebBrowser browser, string html, string url = "about:blank")
        {
            try
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    browser.LoadHtml(html, url);
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"HTML読み込みエラー: {ex.Message}");
            }
        }

        /// <summary>
        /// JavaScriptを実行してHTML要素を操作
        /// </summary>
        /// <param name="browser">対象ブラウザ</param>
        /// <param name="script">JavaScriptコード</param>
        /// <returns>実行結果</returns>
        public static async Task<object> ExecuteScriptAsync(ChromiumWebBrowser browser, string script)
        {
            try
            {
                var response = await browser.EvaluateScriptAsync(script);
                return response.Success ? response.Result : null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"JavaScript実行エラー: {ex.Message}");
                return null;
            }
        }

        #endregion

        #region ウィンドウメッセージ操作

        /// <summary>
        /// ブラウザ画面をマウスクリック
        /// </summary>
        /// <param name="browser">対象ブラウザ</param>
        /// <param name="x">X座標</param>
        /// <param name="y">Y座標</param>
        /// <param name="isRightClick">右クリックかどうか</param>
        public static bool SendMouseClick(ChromiumWebBrowser browser, int x, int y, bool isRightClick = false)
        {
            try
            {
                var hwnd = GetBrowserHandle(browser);
                if (hwnd == IntPtr.Zero) return false;

                var lParam = (IntPtr)((y << 16) | x);

                if (isRightClick)
                {
                    PostMessage(hwnd, WM_RBUTTONDOWN, IntPtr.Zero, lParam);
                    PostMessage(hwnd, WM_RBUTTONUP, IntPtr.Zero, lParam);
                }
                else
                {
                    PostMessage(hwnd, WM_LBUTTONDOWN, IntPtr.Zero, lParam);
                    PostMessage(hwnd, WM_LBUTTONUP, IntPtr.Zero, lParam);
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"マウスクリックエラー: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// キーボード入力のウィンドウメッセージを送信
        /// </summary>
        /// <param name="browser">対象ブラウザ</param>
        /// <param name="text">入力テキスト</param>
        public static bool SendKeyboardInput(ChromiumWebBrowser browser, string text)
        {
            try
            {
                var hwnd = GetBrowserHandle(browser);
                if (hwnd == IntPtr.Zero) return false;

                foreach (char c in text)
                {
                    PostMessage(hwnd, WM_CHAR, (IntPtr)c, IntPtr.Zero);
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"キーボード入力エラー: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// キーコードのウィンドウメッセージを送信
        /// </summary>
        /// <param name="browser">対象ブラウザ</param>
        /// <param name="keyCode">仮想キーコード</param>
        /// <param name="isKeyDown">キーダウンかどうか</param>
        public static bool SendKeyCode(ChromiumWebBrowser browser, int keyCode, bool isKeyDown = true)
        {
            try
            {
                var hwnd = GetBrowserHandle(browser);
                if (hwnd == IntPtr.Zero) return false;

                uint message = isKeyDown ? WM_KEYDOWN : WM_KEYUP;
                PostMessage(hwnd, message, (IntPtr)keyCode, IntPtr.Zero);

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"キーコード送信エラー: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// ブラウザのウィンドウハンドルを取得
        /// </summary>
        /// <param name="browser">対象ブラウザ</param>
        /// <returns>ウィンドウハンドル</returns>
        private static IntPtr GetBrowserHandle(ChromiumWebBrowser browser)
        {
            try
            {
                return Application.Current.Dispatcher.Invoke(() =>
                {
                    var hwndSource = PresentationSource.FromVisual(browser) as HwndSource;
                    return hwndSource?.Handle ?? IntPtr.Zero;
                });
            }
            catch
            {
                return IntPtr.Zero;
            }
        }

        #endregion
    }

    /// <summary>
    /// Cookie収集用のビジター
    /// </summary>
    public class CookieCollector : ICookieVisitor
    {
        public List<Cookie> Cookies { get; } = new List<Cookie>();

        public bool Visit(Cookie cookie, int count, int total, ref bool deleteCookie)
        {
            Cookies.Add(cookie);
            deleteCookie = false;
            return true;
        }

        public void Dispose()
        {
            // リソースのクリーンアップが必要な場合はここで実装
        }
    }
}