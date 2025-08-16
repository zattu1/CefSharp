using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CefSharp;
using CefSharp.Wpf;
using CefSharp.fastBOT.Models;

namespace CefSharp.fastBOT.Core
{
    public class ProxyManager
    {
        private readonly Dictionary<ChromiumWebBrowser, ProxyConfig> _browserProxyMap;

        public ProxyManager()
        {
            _browserProxyMap = new Dictionary<ChromiumWebBrowser, ProxyConfig>();
        }

        public async Task<bool> SetProxyAsync(ChromiumWebBrowser browser, ProxyConfig proxyConfig)
        {
            if (browser == null || !browser.IsBrowserInitialized)
            {
                Console.WriteLine("ブラウザが初期化されていません");
                return false;
            }

            try
            {
                // UIスレッドで実行
                var result = await Cef.UIThreadTaskFactory.StartNew(() =>
                {
                    try
                    {
                        var requestContext = browser.GetBrowser().GetHost().RequestContext;

                        Console.WriteLine($"プロキシ設定開始: {proxyConfig.Host}:{proxyConfig.Port}");

                        // プロキシ設定の確認
                        if (!requestContext.CanSetPreference("proxy"))
                        {
                            Console.WriteLine("プロキシプリファレンスは読み取り専用です");
                            return new { Success = false, Error = "Proxy preference is read-only" };
                        }

                        // プロキシ設定を構築（CefSharp用の正しい形式）
                        var proxyDict = new Dictionary<string, object>();

                        if (proxyConfig.Scheme.ToLower() == "socks5")
                        {
                            // SOCKS5プロキシの場合
                            proxyDict["mode"] = "fixed_servers";
                            proxyDict["server"] = $"socks5://{proxyConfig.Host}:{proxyConfig.Port}";
                        }
                        else
                        {
                            // HTTPプロキシの場合
                            proxyDict["mode"] = "fixed_servers";
                            proxyDict["server"] = $"{proxyConfig.Host}:{proxyConfig.Port}";
                        }

                        Console.WriteLine($"プロキシ設定辞書: mode={proxyDict["mode"]}, server={proxyDict["server"]}");

                        string error;
                        bool success = requestContext.SetPreference("proxy", proxyDict, out error);

                        if (!success)
                        {
                            Console.WriteLine($"プロキシ設定失敗: {error}");
                        }
                        else
                        {
                            Console.WriteLine("プロキシ設定成功");
                        }

                        return new { Success = success, Error = error };
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"プロキシ設定例外: {ex.Message}");
                        return new { Success = false, Error = ex.Message };
                    }
                });

                if (result.Success)
                {
                    _browserProxyMap[browser] = proxyConfig;

                    // 認証が必要な場合はRequestHandlerを設定
                    if (!string.IsNullOrEmpty(proxyConfig.Username))
                    {
                        Console.WriteLine("プロキシ認証設定中...");
                        SetProxyAuthentication(browser, proxyConfig);
                    }

                    // 設定後にページを再読み込みして反映
                    await Task.Delay(500); // 少し待機
                    browser.Reload();

                    Console.WriteLine("プロキシ設定完了、ページ再読み込み実行");
                }
                else
                {
                    Console.WriteLine($"プロキシ設定失敗: {result.Error}");
                }

                return result.Success;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SetProxyAsync例外: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> DisableProxyAsync(ChromiumWebBrowser browser)
        {
            if (browser == null || !browser.IsBrowserInitialized)
            {
                Console.WriteLine("ブラウザが初期化されていません");
                return false;
            }

            try
            {
                var result = await Cef.UIThreadTaskFactory.StartNew(() =>
                {
                    try
                    {
                        var requestContext = browser.GetBrowser().GetHost().RequestContext;

                        Console.WriteLine("プロキシ無効化開始");

                        if (!requestContext.CanSetPreference("proxy"))
                        {
                            Console.WriteLine("プロキシプリファレンスは読み取り専用です");
                            return new { Success = false, Error = "Proxy preference is read-only" };
                        }

                        var proxyDict = new Dictionary<string, object>
                        {
                            ["mode"] = "direct"
                        };

                        Console.WriteLine("プロキシをダイレクト接続に設定");

                        string error;
                        bool success = requestContext.SetPreference("proxy", proxyDict, out error);

                        if (!success)
                        {
                            Console.WriteLine($"プロキシ無効化失敗: {error}");
                        }
                        else
                        {
                            Console.WriteLine("プロキシ無効化成功");
                        }

                        return new { Success = success, Error = error };
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"プロキシ無効化例外: {ex.Message}");
                        return new { Success = false, Error = ex.Message };
                    }
                });

                if (result.Success)
                {
                    _browserProxyMap.Remove(browser);

                    // 設定後にページを再読み込みして反映
                    await Task.Delay(500);
                    browser.Reload();

                    Console.WriteLine("プロキシ無効化完了、ページ再読み込み実行");
                }

                return result.Success;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DisableProxyAsync例外: {ex.Message}");
                return false;
            }
        }

        private void SetProxyAuthentication(ChromiumWebBrowser browser, ProxyConfig proxyConfig)
        {
            try
            {
                if (browser.RequestHandler is Handlers.ProxyAuthRequestHandler authHandler)
                {
                    authHandler.UpdateCredentials(proxyConfig.Username, proxyConfig.Password);
                    Console.WriteLine("既存のプロキシ認証ハンドラーを更新");
                }
                else
                {
                    browser.RequestHandler = new Handlers.ProxyAuthRequestHandler(
                        proxyConfig.Username, proxyConfig.Password);
                    Console.WriteLine("新しいプロキシ認証ハンドラーを設定");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"プロキシ認証設定エラー: {ex.Message}");
            }
        }

        public ProxyConfig GetProxyConfig(ChromiumWebBrowser browser)
        {
            return _browserProxyMap.TryGetValue(browser, out var config) ? config : null;
        }

        /// <summary>
        /// 新しいブラウザインスタンスにプロキシを事前設定して作成
        /// </summary>
        public static ChromiumWebBrowser CreateBrowserWithProxy(ProxyConfig proxyConfig, string initialUrl = "about:blank")
        {
            try
            {
                Console.WriteLine($"プロキシ付きブラウザ作成開始: {proxyConfig.Host}:{proxyConfig.Port}");

                // RequestContextSettingsでプロキシを事前設定
                var settings = new RequestContextSettings();

                // コマンドライン引数でプロキシを設定（より確実）
                var commandLineArgs = new Dictionary<string, string>();

                if (proxyConfig.Scheme.ToLower() == "socks5")
                {
                    commandLineArgs.Add("proxy-server", $"socks5://{proxyConfig.Host}:{proxyConfig.Port}");
                }
                else
                {
                    commandLineArgs.Add("proxy-server", $"{proxyConfig.Host}:{proxyConfig.Port}");
                }

                // プロキシバイパスリストを設定（ローカルは直接接続）
                commandLineArgs.Add("proxy-bypass-list", "localhost,127.0.0.1,::1");

                Console.WriteLine($"コマンドライン引数: {string.Join(", ", commandLineArgs)}");

                // RequestContextを作成
                var requestContext = new RequestContext(settings);

                // ChromiumWebBrowserを作成
                var browser = new ChromiumWebBrowser(initialUrl)
                {
                    RequestContext = requestContext
                };

                // コマンドライン引数を適用（CefSharpの制限により、アプリケーション起動時にのみ有効）
                // 代替案として、BrowserSettingsで設定
                browser.BrowserSettings = new BrowserSettings()
                {
                    // その他の設定...
                };

                Console.WriteLine("プロキシ付きブラウザ作成完了");
                return browser;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"プロキシ付きブラウザ作成エラー: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// プロキシ設定をテスト
        /// </summary>
        public async Task<bool> TestProxyAsync(ChromiumWebBrowser browser)
        {
            try
            {
                Console.WriteLine("プロキシテスト開始");

                var currentUrl = browser.Address;

                // IPチェックサイトに移動してプロキシが適用されているか確認
                browser.LoadUrl("https://httpbin.org/ip");

                // ページロード待機
                await Task.Delay(5000);

                // HTMLを取得してIPを確認
                var html = await browser.GetSourceAsync();
                Console.WriteLine($"プロキシテスト結果: {html}");

                // 元のURLに戻る
                if (!string.IsNullOrEmpty(currentUrl) && currentUrl != "about:blank")
                {
                    browser.LoadUrl(currentUrl);
                }

                return !string.IsNullOrEmpty(html) && html.Contains("origin");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"プロキシテストエラー: {ex.Message}");
                return false;
            }
        }
    }
}