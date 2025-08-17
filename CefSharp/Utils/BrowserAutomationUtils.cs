using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Threading;
using CefSharp;
using CefSharp.Wpf;

namespace CefSharp.fastBOT.Utils
{
    /// <summary>
    /// ブラウザ自動化のためのユーティリティクラス（CefSharpネイティブAPI対応版）
    /// </summary>
    public class BrowserAutomationUtils
    {
        #region Win32 API定義

        [DllImport("user32.dll")]
        private static extern IntPtr ChildWindowFromPoint(IntPtr hWndParent, POINT Point);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

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
                    cookieManager.VisitAllCookies(visitor);
                }
                else
                {
                    cookieManager.VisitUrlCookies(url, true, visitor);
                }

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
        /// Cookie更新・設定
        /// </summary>
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

        #region CefSharpネイティブAPI操作（元のI/F維持）

        /// <summary>
        /// 指定座標をマウスクリック（C++のleftMouseClickを参考）
        /// </summary>
        /// <param name="browser">対象ブラウザ</param>
        /// <param name="x">X座標</param>
        /// <param name="y">Y座標</param>
        /// <param name="downUpFlag">クリック種別フラグ（3=通常クリック）</param>
        public static bool LeftMouseClick(ChromiumWebBrowser browser, int x, int y, int downUpFlag = 3)
        {
            try
            {
                // ブラウザがロード済みかチェック
                if (browser?.GetBrowser() == null)
                {
                    Console.WriteLine("LeftMouseClick: ブラウザが初期化されていません");
                    return false;
                }

                var host = browser.GetBrowser().GetHost();
                if (host == null)
                {
                    Console.WriteLine("LeftMouseClick: ブラウザホストが取得できません");
                    return false;
                }

                Console.WriteLine($"LeftMouseClick: 座標({x}, {y}) でクリック実行, フラグ={downUpFlag}");

                // フォーカスを確保
                Application.Current.Dispatcher.Invoke(() =>
                {
                    browser.Focus();
                });
                host.SetFocus(true);

                // マウス移動
                host.SendMouseMoveEvent(x, y, false, CefEventFlags.None);
                Thread.Sleep(50);

                // C++のdownUpFlagに従った処理
                if ((downUpFlag & 1) != 0) // ダウン
                {
                    host.SendMouseClickEvent(x, y, MouseButtonType.Left, false, 1, CefEventFlags.None);
                    Console.WriteLine("LeftMouseClick: ダウン実行");
                }

                if ((downUpFlag & 2) != 0) // アップ
                {
                    // 少し待機してからアップ（C++のmsleep(200)を参考）
                    Thread.Sleep(200);
                    host.SendMouseClickEvent(x, y, MouseButtonType.Left, true, 1, CefEventFlags.None);
                    Console.WriteLine("LeftMouseClick: アップ実行");
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"LeftMouseClick エラー: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 仮想キーボード入力（C++のvirtualKeyboardを参考）
        /// </summary>
        /// <param name="browser">対象ブラウザ</param>
        /// <param name="x">クリック座標X</param>
        /// <param name="y">クリック座標Y</param>
        /// <param name="text">入力テキスト</param>
        public static bool VirtualKeyboard(ChromiumWebBrowser browser, int x, int y, string text)
        {
            try
            {
                // ブラウザがロード済みかチェック
                if (browser?.GetBrowser() == null)
                {
                    Console.WriteLine("VirtualKeyboard: ブラウザが初期化されていません");
                    return false;
                }

                var host = browser.GetBrowser().GetHost();
                if (host == null)
                {
                    Console.WriteLine("VirtualKeyboard: ブラウザホストが取得できません");
                    return false;
                }

                Console.WriteLine($"VirtualKeyboard: クリック座標=({x}, {y}), テキスト='{text}'");

                // フォーカスを確保
                Application.Current.Dispatcher.Invoke(() =>
                {
                    browser.Focus();
                });
                host.SetFocus(true);

                // まずクリックしてフォーカスを当てる
                host.SendMouseMoveEvent(x, y, false, CefEventFlags.None);
                Thread.Sleep(50);
                host.SendMouseClickEvent(x, y, MouseButtonType.Left, false, 1, CefEventFlags.None);
                Thread.Sleep(100);
                host.SendMouseClickEvent(x, y, MouseButtonType.Left, true, 1, CefEventFlags.None);

                // 少し待機してからテキスト入力
                Thread.Sleep(100);

                // テキストを1文字ずつ送信（C++版と同様）
                foreach (char c in text)
                {
                    Console.WriteLine($"Sending character: {c}");

                    var keyEvent = new KeyEvent
                    {
                        WindowsKeyCode = c,
                        Type = KeyEventType.Char,
                        Modifiers = CefEventFlags.None
                    };

                    host.SendKeyEvent(keyEvent);

                    // C++のmsleep(50)を参考
                    Thread.Sleep(50);
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"VirtualKeyboard エラー: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 仮想キーコード送信（C++のvirtualKeyCodeを参考）
        /// </summary>
        /// <param name="browser">対象ブラウザ</param>
        /// <param name="x">クリック座標X</param>
        /// <param name="y">クリック座標Y</param>
        /// <param name="keyCode">仮想キーコード</param>
        public static bool VirtualKeyCode(ChromiumWebBrowser browser, int x, int y, int keyCode)
        {
            try
            {
                // ブラウザがロード済みかチェック
                if (browser?.GetBrowser() == null) return false;

                var host = browser.GetBrowser().GetHost();
                if (host == null) return false;

                Console.WriteLine($"VirtualKeyCode: クリック座標=({x}, {y}), キーコード={keyCode}");

                // フォーカスを確保
                Application.Current.Dispatcher.Invoke(() =>
                {
                    browser.Focus();
                });
                host.SetFocus(true);

                // まずクリックしてフォーカスを当てる
                host.SendMouseMoveEvent(x, y, false, CefEventFlags.None);
                Thread.Sleep(50);
                host.SendMouseClickEvent(x, y, MouseButtonType.Left, false, 1, CefEventFlags.None);
                host.SendMouseClickEvent(x, y, MouseButtonType.Left, true, 1, CefEventFlags.None);

                // キーコードを送信
                var keyEventDown = new KeyEvent
                {
                    WindowsKeyCode = keyCode,
                    Type = KeyEventType.KeyDown,
                    Modifiers = CefEventFlags.None
                };
                host.SendKeyEvent(keyEventDown);

                var keyEventUp = new KeyEvent
                {
                    WindowsKeyCode = keyCode,
                    Type = KeyEventType.KeyUp,
                    Modifiers = CefEventFlags.None
                };
                host.SendKeyEvent(keyEventUp);

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"VirtualKeyCode エラー: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// C++のChildWindowFromPointと同等の処理でターゲットウィンドウを探す（互換性維持用）
        /// </summary>
        /// <param name="parentHwnd">親ウィンドウハンドル</param>
        /// <param name="point">座標</param>
        /// <returns>ターゲットウィンドウハンドル</returns>
        private static IntPtr FindTargetWindow(IntPtr parentHwnd, POINT point)
        {
            IntPtr currentHwnd = parentHwnd;

            while (true)
            {
                IntPtr nextHwnd = ChildWindowFromPoint(currentHwnd, point);
                if (nextHwnd == IntPtr.Zero || nextHwnd == currentHwnd)
                {
                    return currentHwnd;
                }
                currentHwnd = nextHwnd;
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

        #region 既存の互換性メソッド

        /// <summary>
        /// 既存のマウスクリック（互換性のため残す）
        /// </summary>
        public static bool SendMouseClick(ChromiumWebBrowser browser, int x, int y, bool isRightClick = false)
        {
            return LeftMouseClick(browser, x, y, 3); // 通常のクリック
        }

        /// <summary>
        /// 既存のキーボード入力（互換性のため残す）
        /// </summary>
        public static bool SendKeyboardInput(ChromiumWebBrowser browser, string text)
        {
            // 画面中央を仮の座標として使用
            return VirtualKeyboard(browser, 500, 300, text);
        }

        /// <summary>
        /// 既存のキーコード送信（互換性のため残す）
        /// </summary>
        public static bool SendKeyCode(ChromiumWebBrowser browser, int keyCode, bool isKeyDown = true)
        {
            // 画面中央を仮の座標として使用
            return VirtualKeyCode(browser, 500, 300, keyCode);
        }

        #endregion

        #region Cookie収集クラス

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

        #endregion
    }
}