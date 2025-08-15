using CefSharp;
using CefSharp.fastBOT.Models;
using CefSharp.Wpf;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace CefSharp.fastBOT.Core
{
    /// <summary>
    /// ブラウザからHTMLコンテンツを取得するサービス
    /// </summary>
    public class HtmlExtractionService
    {
        private readonly ChromiumWebBrowser _browser;

        public HtmlExtractionService(ChromiumWebBrowser browser)
        {
            _browser = browser ?? throw new ArgumentNullException(nameof(browser));
        }

        /// <summary>
        /// 現在のページの完全なHTMLを取得
        /// </summary>
        /// <returns>HTMLコンテンツ</returns>
        public async Task<string> GetPageHtmlAsync()
        {
            try
            {
                if (!_browser.IsBrowserInitialized)
                {
                    throw new InvalidOperationException("ブラウザが初期化されていません");
                }

                var script = "document.documentElement.outerHTML";
                var response = await _browser.GetMainFrame().EvaluateScriptAsync(script);

                if (response.Success && response.Result != null)
                {
                    return response.Result.ToString();
                }
                else
                {
                    throw new Exception($"HTML取得に失敗しました: {response.Message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetPageHtmlAsync error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 現在のページのbody部分のHTMLを取得
        /// </summary>
        /// <returns>body部分のHTMLコンテンツ</returns>
        public async Task<string> GetPageBodyHtmlAsync()
        {
            try
            {
                if (!_browser.IsBrowserInitialized)
                {
                    throw new InvalidOperationException("ブラウザが初期化されていません");
                }

                var script = "document.body ? document.body.outerHTML : ''";
                var response = await _browser.GetMainFrame().EvaluateScriptAsync(script);

                if (response.Success && response.Result != null)
                {
                    return response.Result.ToString();
                }
                else
                {
                    throw new Exception($"body HTML取得に失敗しました: {response.Message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetPageBodyHtmlAsync error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 指定したセレクターの要素のHTMLを取得
        /// </summary>
        /// <param name="selector">CSSセレクター</param>
        /// <returns>要素のHTMLコンテンツ</returns>
        public async Task<string> GetElementHtmlAsync(string selector)
        {
            try
            {
                if (!_browser.IsBrowserInitialized)
                {
                    throw new InvalidOperationException("ブラウザが初期化されていません");
                }

                if (string.IsNullOrEmpty(selector))
                {
                    throw new ArgumentException("セレクターが指定されていません", nameof(selector));
                }

                var script = $@"
                    var element = document.querySelector('{selector.Replace("'", "\\'")}');
                    element ? element.outerHTML : null;
                ";

                var response = await _browser.GetMainFrame().EvaluateScriptAsync(script);

                if (response.Success)
                {
                    return response.Result?.ToString() ?? string.Empty;
                }
                else
                {
                    throw new Exception($"要素HTML取得に失敗しました: {response.Message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetElementHtmlAsync error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 指定したセレクターの要素の内部HTMLを取得
        /// </summary>
        /// <param name="selector">CSSセレクター</param>
        /// <returns>要素の内部HTMLコンテンツ</returns>
        public async Task<string> GetElementInnerHtmlAsync(string selector)
        {
            try
            {
                if (!_browser.IsBrowserInitialized)
                {
                    throw new InvalidOperationException("ブラウザが初期化されていません");
                }

                if (string.IsNullOrEmpty(selector))
                {
                    throw new ArgumentException("セレクターが指定されていません", nameof(selector));
                }

                var script = $@"
                    var element = document.querySelector('{selector.Replace("'", "\\'")}');
                    element ? element.innerHTML : null;
                ";

                var response = await _browser.GetMainFrame().EvaluateScriptAsync(script);

                if (response.Success)
                {
                    return response.Result?.ToString() ?? string.Empty;
                }
                else
                {
                    throw new Exception($"要素内部HTML取得に失敗しました: {response.Message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetElementInnerHtmlAsync error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 現在のページのテキストコンテンツを取得（HTMLタグを除去）
        /// </summary>
        /// <returns>テキストコンテンツ</returns>
        public async Task<string> GetPageTextAsync()
        {
            try
            {
                if (!_browser.IsBrowserInitialized)
                {
                    throw new InvalidOperationException("ブラウザが初期化されていません");
                }

                var script = "document.body ? document.body.textContent : ''";
                var response = await _browser.GetMainFrame().EvaluateScriptAsync(script);

                if (response.Success && response.Result != null)
                {
                    return response.Result.ToString();
                }
                else
                {
                    throw new Exception($"テキスト取得に失敗しました: {response.Message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetPageTextAsync error: {ex.Message}");
                throw;
            }
        }   

        /// <summary>
        /// ページの基本情報を取得（PageInfoを返す）
        /// </summary>
        /// <returns>ページ情報</returns>
        public async Task<PageInfo> GetPageInfoAsync()
        {
            try
            {
                if (!_browser.IsBrowserInitialized)
                {
                    throw new InvalidOperationException("ブラウザが初期化されていません");
                }

                var script = @"
                    ({
                        title: document.title,
                        url: window.location.href,
                        domain: window.location.hostname,
                        readyState: document.readyState,
                        lastModified: document.lastModified,
                        characterSet: document.characterSet,
                        contentType: document.contentType
                    })
                ";

                var response = await _browser.GetMainFrame().EvaluateScriptAsync(script);

                if (response.Success && response.Result != null)
                {
                    var resultDict = response.Result as System.Collections.Generic.IDictionary<string, object>;
                    if (resultDict != null)
                    {
                        var pageInfo = new PageInfo
                        {
                            Title = resultDict.TryGetValue("title", out var title) ? title?.ToString() ?? string.Empty : string.Empty,
                            Url = resultDict.TryGetValue("url", out var url) ? url?.ToString() ?? string.Empty : string.Empty,
                            Language = resultDict.TryGetValue("characterSet", out var characterSet) ? characterSet?.ToString() ?? string.Empty : string.Empty,
                            Encoding = resultDict.TryGetValue("contentType", out var contentType) ? contentType?.ToString() ?? string.Empty : string.Empty,
                            AnalyzedAt = DateTime.Now
                        };

                        // LastModifiedの解析を試行
                        if (resultDict.TryGetValue("lastModified", out var lastModified) && lastModified != null)
                        {
                            if (DateTime.TryParse(lastModified.ToString(), out var parsedDate))
                            {
                                pageInfo.LastModified = parsedDate;
                            }
                        }

                        return pageInfo;
                    }
                }

                throw new Exception($"ページ情報取得に失敗しました: {response.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetPageInfoAsync error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// HTMLをファイルに保存
        /// </summary>
        /// <param name="filePath">保存先ファイルパス</param>
        /// <returns>成功した場合true</returns>
        public async Task<bool> SaveHtmlToFileAsync(string filePath)
        {
            try
            {
                var html = await GetPageHtmlAsync();
                await System.IO.File.WriteAllTextAsync(filePath, html, System.Text.Encoding.UTF8);
                Console.WriteLine($"HTML saved to: {filePath}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SaveHtmlToFileAsync error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// ページのスクリーンショットと共にHTMLを保存
        /// </summary>
        /// <param name="baseFileName">ベースファイル名（拡張子なし）</param>
        /// <returns>成功した場合true</returns>
        public async Task<bool> SavePageSnapshotAsync(string baseFileName)
        {
            try
            {
                var tasks = new[]
                {
                    SaveHtmlToFileAsync($"{baseFileName}.html"),
                    SavePageInfoToFileAsync($"{baseFileName}_info.json")
                };

                var results = await Task.WhenAll(tasks);
                return results[0] && results[1];
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SavePageSnapshotAsync error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// ページ情報をJSONファイルに保存
        /// </summary>
        /// <param name="filePath">保存先ファイルパス</param>
        /// <returns>成功した場合true</returns>
        private async Task<bool> SavePageInfoToFileAsync(string filePath)
        {
            try
            {
                var pageInfo = await GetPageInfoAsync();
                var json = System.Text.Json.JsonSerializer.Serialize(pageInfo, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });

                await System.IO.File.WriteAllTextAsync(filePath, json, System.Text.Encoding.UTF8);
                Console.WriteLine($"Page info saved to: {filePath}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SavePageInfoToFileAsync error: {ex.Message}");
                return false;
            }
        }
    }
}