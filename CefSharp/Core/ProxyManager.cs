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
                return false;
            }

            try
            {
                var result = await Cef.UIThreadTaskFactory.StartNew(() =>
                {
                    try
                    {
                        var requestContext = browser.GetBrowser().GetHost().RequestContext;
                        
                        if (!requestContext.CanSetPreference("proxy"))
                        {
                            return new { Success = false, Error = "Proxy preference is read-only" };
                        }

                        var proxyDict = new Dictionary<string, object>
                        {
                            ["mode"] = "fixed_servers",
                            ["server"] = $"{proxyConfig.Scheme}://{proxyConfig.Host}:{proxyConfig.Port}"
                        };

                        string error;
                        bool success = requestContext.SetPreference("proxy", proxyDict, out error);
                        
                        return new { Success = success, Error = error };
                    }
                    catch (Exception ex)
                    {
                        return new { Success = false, Error = ex.Message };
                    }
                });

                if (result.Success)
                {
                    _browserProxyMap[browser] = proxyConfig;
                    
                    // 認証が必要な場合はRequestHandlerを設定
                    if (!string.IsNullOrEmpty(proxyConfig.Username))
                    {
                        SetProxyAuthentication(browser, proxyConfig);
                    }
                }

                return result.Success;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Proxy設定エラー: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> DisableProxyAsync(ChromiumWebBrowser browser)
        {
            if (browser == null || !browser.IsBrowserInitialized)
            {
                return false;
            }

            try
            {
                var result = await Cef.UIThreadTaskFactory.StartNew(() =>
                {
                    try
                    {
                        var requestContext = browser.GetBrowser().GetHost().RequestContext;
                        
                        if (!requestContext.CanSetPreference("proxy"))
                        {
                            return new { Success = false, Error = "Proxy preference is read-only" };
                        }

                        var proxyDict = new Dictionary<string, object>
                        {
                            ["mode"] = "direct"
                        };

                        string error;
                        bool success = requestContext.SetPreference("proxy", proxyDict, out error);
                        
                        return new { Success = success, Error = error };
                    }
                    catch (Exception ex)
                    {
                        return new { Success = false, Error = ex.Message };
                    }
                });

                if (result.Success)
                {
                    _browserProxyMap.Remove(browser);
                }

                return result.Success;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Proxy無効化エラー: {ex.Message}");
                return false;
            }
        }

        private void SetProxyAuthentication(ChromiumWebBrowser browser, ProxyConfig proxyConfig)
        {
            if (browser.RequestHandler is Handlers.ProxyAuthRequestHandler authHandler)
            {
                authHandler.UpdateCredentials(proxyConfig.Username, proxyConfig.Password);
            }
            else
            {
                browser.RequestHandler = new Handlers.ProxyAuthRequestHandler(
                    proxyConfig.Username, proxyConfig.Password);
            }
        }

        public ProxyConfig GetProxyConfig(ChromiumWebBrowser browser)
        {
            return _browserProxyMap.TryGetValue(browser, out var config) ? config : null;
        }
    }
}