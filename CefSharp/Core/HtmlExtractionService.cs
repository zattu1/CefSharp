using CefSharp;
using CefSharp.fastBOT.Models;
using CefSharp.Wpf;
using HtmlAgilityPack;
using Fizzler.Systems.HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace CefSharp.fastBOT.Core
{
    /// <summary>
    /// ブラウザからHTMLコンテンツを取得・解析するサービス（スレッドセーフ対応版）
    /// CEFの機能とFizzler.Systems.HtmlAgilityPackを使用してHTMLの詳細な解析機能を提供
    /// </summary>
    public class HtmlExtractionService
    {
        private readonly ChromiumWebBrowser _browser;
        private HtmlDocument _lastParsedDocument;
        private string _lastHtmlContent;
        private readonly object _lockObject = new object();
        private bool _disposed = false;

        public HtmlExtractionService(ChromiumWebBrowser browser)
        {
            _browser = browser ?? throw new ArgumentNullException(nameof(browser));
            ConfigureHtmlAgilityPack();
        }

        #region 設定メソッド

        /// <summary>
        /// HtmlAgilityPackの設定を初期化
        /// </summary>
        private void ConfigureHtmlAgilityPack()
        {
            // HtmlAgilityPackのグローバル設定
            HtmlNode.ElementsFlags.Remove("form");
            HtmlNode.ElementsFlags.Remove("option");
        }

        #endregion

        #region CEF直接取得メソッド（スレッドセーフ対応）

        /// <summary>
        /// CEFから直接現在のページの完全なHTMLを取得（スレッドセーフ）
        /// </summary>
        /// <returns>HTMLコンテンツ</returns>
        public async Task<string> GetPageHtmlAsync()
        {
            try
            {
                // UIスレッドでブラウザの状態確認を実行
                var (isInitialized, mainFrame) = await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        var initialized = _browser.IsBrowserInitialized;
                        var frame = initialized ? _browser.GetMainFrame() : null;
                        return (initialized, frame);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"UI thread browser check error: {ex.Message}");
                        return (false, null);
                    }
                });

                if (!isInitialized)
                {
                    throw new InvalidOperationException("ブラウザが初期化されていません");
                }

                if (mainFrame == null)
                {
                    throw new InvalidOperationException("メインフレームが取得できません");
                }

                // CEFのGetSourceAsyncメソッドを使用してHTMLソースを取得
                var html = await mainFrame.GetSourceAsync();

                lock (_lockObject)
                {
                    _lastHtmlContent = html;
                }

                return html;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetPageHtmlAsync error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// CEFから直接取得したHTMLからbody部分を抽出（スレッドセーフ）
        /// </summary>
        /// <returns>body部分のHTMLコンテンツ</returns>
        public async Task<string> GetPageBodyHtmlAsync()
        {
            try
            {
                var fullHtml = await GetPageHtmlAsync();
                var doc = await ParseHtmlAsync(fullHtml);

                var bodyNode = doc.DocumentNode.SelectSingleNode("//body");
                return bodyNode?.OuterHtml ?? string.Empty;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetPageBodyHtmlAsync error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// CEFから取得したHTMLから指定したCSSセレクターの要素のHTMLを取得（スレッドセーフ）
        /// </summary>
        /// <param name="selector">CSSセレクター</param>
        /// <returns>要素のHTMLコンテンツ</returns>
        public async Task<string> GetElementHtmlAsync(string selector)
        {
            try
            {
                if (string.IsNullOrEmpty(selector))
                {
                    throw new ArgumentException("セレクターが指定されていません", nameof(selector));
                }

                var fullHtml = await GetPageHtmlAsync();
                var doc = await ParseHtmlAsync(fullHtml);

                // Fizzlerを使用してCSSセレクターで要素を取得
                var nodes = doc.DocumentNode.QuerySelectorAll(selector);
                if (nodes.Any())
                {
                    return nodes.First().OuterHtml;
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetElementHtmlAsync error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// CEFから取得したHTMLから指定したCSSセレクターの要素の内部HTMLを取得（スレッドセーフ）
        /// </summary>
        /// <param name="selector">CSSセレクター</param>
        /// <returns>要素の内部HTMLコンテンツ</returns>
        public async Task<string> GetElementInnerHtmlAsync(string selector)
        {
            try
            {
                if (string.IsNullOrEmpty(selector))
                {
                    throw new ArgumentException("セレクターが指定されていません", nameof(selector));
                }

                var fullHtml = await GetPageHtmlAsync();
                var doc = await ParseHtmlAsync(fullHtml);

                // Fizzlerを使用してCSSセレクターで要素を取得
                var nodes = doc.DocumentNode.QuerySelectorAll(selector);
                if (nodes.Any())
                {
                    return nodes.First().InnerHtml;
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetElementInnerHtmlAsync error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// CEFから取得したHTMLからテキストコンテンツを抽出（スレッドセーフ）
        /// </summary>
        /// <returns>テキストコンテンツ</returns>
        public async Task<string> GetPageTextAsync()
        {
            try
            {
                var fullHtml = await GetPageHtmlAsync();
                var doc = await ParseHtmlAsync(fullHtml);

                var bodyNode = doc.DocumentNode.SelectSingleNode("//body");
                return bodyNode?.InnerText ?? string.Empty;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetPageTextAsync error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// CEFから取得した情報とHTMLパースによりページの基本情報を取得（スレッドセーフ）
        /// </summary>
        /// <returns>ページ情報</returns>
        public async Task<PageInfo> GetPageInfoAsync()
        {
            try
            {
                // UIスレッドでブラウザ情報を取得
                var (url, isInitialized) = await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        return (_browser.Address ?? string.Empty, _browser.IsBrowserInitialized);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Browser info retrieval error: {ex.Message}");
                        return (string.Empty, false);
                    }
                });

                if (!isInitialized)
                {
                    throw new InvalidOperationException("ブラウザが初期化されていません");
                }

                var fullHtml = await GetPageHtmlAsync();
                var doc = await ParseHtmlAsync(fullHtml);

                var pageInfo = new PageInfo
                {
                    Url = url,
                    AnalyzedAt = DateTime.Now
                };

                // HTMLからタイトルを取得
                var titleNode = doc.DocumentNode.SelectSingleNode("//title");
                pageInfo.Title = titleNode?.InnerText?.Trim() ?? string.Empty;

                // HTMLからmeta情報を取得
                var charsetMeta = doc.DocumentNode.SelectSingleNode("//meta[@charset]");
                if (charsetMeta != null)
                {
                    pageInfo.Language = charsetMeta.GetAttributeValue("charset", "");
                }
                else
                {
                    var contentTypeMeta = doc.DocumentNode.SelectSingleNode("//meta[@http-equiv='Content-Type']");
                    if (contentTypeMeta != null)
                    {
                        var content = contentTypeMeta.GetAttributeValue("content", "");
                        if (content.Contains("charset="))
                        {
                            pageInfo.Language = content.Split(new[] { "charset=" }, StringSplitOptions.None).LastOrDefault()?.Trim() ?? "";
                        }
                    }
                }

                // ドメイン情報を設定
                if (Uri.TryCreate(pageInfo.Url, UriKind.Absolute, out var uri))
                {
                    pageInfo.Encoding = uri.Host;
                }

                return pageInfo;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetPageInfoAsync error: {ex.Message}");
                throw;
            }
        }

        #endregion

        #region HTMLパーサー機能（Fizzler使用・スレッドセーフ）

        /// <summary>
        /// HTMLコンテンツをパースしてHtmlDocumentを返す（スレッドセーフ）
        /// </summary>
        /// <param name="htmlContent">HTMLコンテンツ</param>
        /// <returns>パース済みのHtmlDocument</returns>
        public async Task<HtmlDocument> ParseHtmlAsync(string htmlContent)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var doc = new HtmlDocument();
                    doc.LoadHtml(htmlContent);

                    lock (_lockObject)
                    {
                        _lastParsedDocument = doc;
                    }

                    return doc;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ParseHtmlAsync error: {ex.Message}");
                    throw;
                }
            });
        }

        /// <summary>
        /// 現在のページのHTMLをパースして返す（スレッドセーフ）
        /// </summary>
        /// <returns>パース済みのHtmlDocument</returns>
        public async Task<HtmlDocument> ParseCurrentPageAsync()
        {
            var html = await GetPageHtmlAsync();
            return await ParseHtmlAsync(html);
        }

        /// <summary>
        /// タグ名で要素を検索（CHtmlParserのgetElementsByTagNameに相当・スレッドセーフ）
        /// </summary>
        /// <param name="tagName">タグ名</param>
        /// <param name="attributeName">属性名（オプション）</param>
        /// <param name="attributeValue">属性値（オプション）</param>
        /// <returns>見つかった要素のリスト</returns>
        public async Task<List<HtmlNode>> GetElementsByTagNameAsync(string tagName, string attributeName = null, string attributeValue = null)
        {
            try
            {
                HtmlDocument doc;
                lock (_lockObject)
                {
                    doc = _lastParsedDocument;
                }

                if (doc == null)
                {
                    doc = await ParseCurrentPageAsync();
                }

                string cssSelector = tagName;
                if (!string.IsNullOrEmpty(attributeName) && !string.IsNullOrEmpty(attributeValue))
                {
                    cssSelector += $"[{attributeName}='{attributeValue}']";
                }

                var nodes = doc.DocumentNode.QuerySelectorAll(cssSelector);
                return nodes?.ToList() ?? new List<HtmlNode>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetElementsByTagNameAsync error: {ex.Message}");
                return new List<HtmlNode>();
            }
        }

        /// <summary>
        /// 属性値が指定した文字列を含む要素を検索（CHtmlParserのgetElementsByTagName4Containに相当・スレッドセーフ）
        /// </summary>
        /// <param name="tagName">タグ名</param>
        /// <param name="attributeName">属性名</param>
        /// <param name="containsValue">含まれるべき文字列</param>
        /// <returns>見つかった要素のリスト</returns>
        public async Task<List<HtmlNode>> GetElementsByTagNameContainAsync(string tagName, string attributeName, string containsValue)
        {
            try
            {
                HtmlDocument doc;
                lock (_lockObject)
                {
                    doc = _lastParsedDocument;
                }

                if (doc == null)
                {
                    doc = await ParseCurrentPageAsync();
                }

                // CSSセレクターで部分一致（XPathを使用）
                var xpath = $"//{tagName}[contains(@{attributeName}, '{containsValue}')]";
                var nodes = doc.DocumentNode.SelectNodes(xpath);
                return nodes?.ToList() ?? new List<HtmlNode>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetElementsByTagNameContainAsync error: {ex.Message}");
                return new List<HtmlNode>();
            }
        }

        /// <summary>
        /// フォームのinput要素からパラメーターを抽出（CHtmlParserのgetReqParamに相当・スレッドセーフ）
        /// </summary>
        /// <param name="formSelector">フォームのCSSセレクター（オプション）</param>
        /// <returns>input要素のname-value辞書</returns>
        public async Task<Dictionary<string, string>> GetFormParametersAsync(string formSelector = null)
        {
            try
            {
                HtmlDocument doc;
                lock (_lockObject)
                {
                    doc = _lastParsedDocument;
                }

                if (doc == null)
                {
                    doc = await ParseCurrentPageAsync();
                }

                var parameters = new Dictionary<string, string>();

                string inputSelector = string.IsNullOrEmpty(formSelector) ? "input" : $"{formSelector} input";
                var inputNodes = doc.DocumentNode.QuerySelectorAll(inputSelector);

                if (inputNodes != null)
                {
                    foreach (var input in inputNodes)
                    {
                        var name = input.GetAttributeValue("name", "");
                        var value = input.GetAttributeValue("value", "");

                        if (!string.IsNullOrEmpty(name))
                        {
                            parameters[name] = value;
                        }
                    }
                }

                return parameters;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetFormParametersAsync error: {ex.Message}");
                return new Dictionary<string, string>();
            }
        }

        /// <summary>
        /// 指定した要素の属性値を取得（CHtmlParserのgetAttrValueに相当・スレッドセーフ）
        /// </summary>
        /// <param name="node">対象ノード</param>
        /// <param name="attributeName">属性名</param>
        /// <returns>属性値</returns>
        public string GetAttributeValue(HtmlNode node, string attributeName)
        {
            return node?.GetAttributeValue(attributeName, "") ?? string.Empty;
        }

        /// <summary>
        /// 指定した要素の内部テキストを取得（CHtmlParserのgetInnerTextに相当・スレッドセーフ）
        /// </summary>
        /// <param name="node">対象ノード</param>
        /// <returns>内部テキスト</returns>
        public string GetInnerText(HtmlNode node)
        {
            return node?.InnerText?.Trim() ?? string.Empty;
        }

        /// <summary>
        /// 指定したXPathで要素を検索（スレッドセーフ）
        /// </summary>
        /// <param name="xpath">XPath式</param>
        /// <returns>見つかった要素のリスト</returns>
        public async Task<List<HtmlNode>> SelectNodesByXPathAsync(string xpath)
        {
            try
            {
                HtmlDocument doc;
                lock (_lockObject)
                {
                    doc = _lastParsedDocument;
                }

                if (doc == null)
                {
                    doc = await ParseCurrentPageAsync();
                }

                var nodes = doc.DocumentNode.SelectNodes(xpath);
                return nodes?.ToList() ?? new List<HtmlNode>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SelectNodesByXPathAsync error: {ex.Message}");
                return new List<HtmlNode>();
            }
        }

        /// <summary>
        /// CSSセレクターで要素を検索（Fizzlerを使用・スレッドセーフ）
        /// </summary>
        /// <param name="cssSelector">CSSセレクター</param>
        /// <returns>見つかった要素のリスト</returns>
        public async Task<List<HtmlNode>> SelectNodesByCssSelectorAsync(string cssSelector)
        {
            try
            {
                HtmlDocument doc;
                lock (_lockObject)
                {
                    doc = _lastParsedDocument;
                }

                if (doc == null)
                {
                    doc = await ParseCurrentPageAsync();
                }

                var nodes = doc.DocumentNode.QuerySelectorAll(cssSelector);
                return nodes?.ToList() ?? new List<HtmlNode>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SelectNodesByCssSelectorAsync error: {ex.Message}");
                return new List<HtmlNode>();
            }
        }

        /// <summary>
        /// 単一要素をCSSセレクターで検索（Fizzlerを使用・スレッドセーフ）
        /// </summary>
        /// <param name="cssSelector">CSSセレクター</param>
        /// <returns>見つかった最初の要素</returns>
        public async Task<HtmlNode> SelectSingleNodeByCssSelectorAsync(string cssSelector)
        {
            try
            {
                HtmlDocument doc;
                lock (_lockObject)
                {
                    doc = _lastParsedDocument;
                }

                if (doc == null)
                {
                    doc = await ParseCurrentPageAsync();
                }

                return doc.DocumentNode.QuerySelector(cssSelector);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SelectSingleNodeByCssSelectorAsync error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// URLの妥当性をチェック（CHtmlParserのisUrlValidに相当・スレッドセーフ）
        /// </summary>
        /// <param name="url">チェックするURL</param>
        /// <returns>妥当な場合true</returns>
        public static bool IsUrlValid(string url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                   (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }

        #endregion

        #region 高度なHTMLパーシング機能（スレッドセーフ）

        /// <summary>
        /// テーブルデータを抽出（スレッドセーフ）
        /// </summary>
        /// <param name="tableSelector">テーブルのCSSセレクター</param>
        /// <returns>テーブルデータ（行と列の2次元配列）</returns>
        public async Task<List<List<string>>> ExtractTableDataAsync(string tableSelector = "table")
        {
            try
            {
                HtmlDocument doc;
                lock (_lockObject)
                {
                    doc = _lastParsedDocument;
                }

                if (doc == null)
                {
                    doc = await ParseCurrentPageAsync();
                }

                var table = doc.DocumentNode.QuerySelector(tableSelector);

                if (table == null)
                    return new List<List<string>>();

                var rows = table.QuerySelectorAll("tr");
                var tableData = new List<List<string>>();

                foreach (var row in rows)
                {
                    var cells = row.QuerySelectorAll("td, th");
                    var rowData = cells.Select(cell => cell.InnerText?.Trim() ?? "").ToList();

                    if (rowData.Any())
                        tableData.Add(rowData);
                }

                return tableData;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ExtractTableDataAsync error: {ex.Message}");
                return new List<List<string>>();
            }
        }

        /// <summary>
        /// リンク情報を抽出（スレッドセーフ）
        /// </summary>
        /// <param name="linkSelector">リンクのCSSセレクター</param>
        /// <returns>リンク情報のリスト</returns>
        public async Task<List<LinkInfo>> ExtractLinksAsync(string linkSelector = "a[href]")
        {
            try
            {
                HtmlDocument doc;
                lock (_lockObject)
                {
                    doc = _lastParsedDocument;
                }

                if (doc == null)
                {
                    doc = await ParseCurrentPageAsync();
                }

                var links = doc.DocumentNode.QuerySelectorAll(linkSelector);

                var linkInfos = new List<LinkInfo>();

                foreach (var link in links)
                {
                    var href = link.GetAttributeValue("href", "");
                    var text = link.InnerText?.Trim() ?? "";
                    var title = link.GetAttributeValue("title", "");

                    if (!string.IsNullOrEmpty(href))
                    {
                        linkInfos.Add(new LinkInfo
                        {
                            Url = href,
                            Text = text,
                            Title = title
                        });
                    }
                }

                return linkInfos;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ExtractLinksAsync error: {ex.Message}");
                return new List<LinkInfo>();
            }
        }

        /// <summary>
        /// 画像情報を抽出（スレッドセーフ）
        /// </summary>
        /// <param name="imageSelector">画像のCSSセレクター</param>
        /// <returns>画像情報のリスト</returns>
        public async Task<List<ImageInfo>> ExtractImagesAsync(string imageSelector = "img")
        {
            try
            {
                HtmlDocument doc;
                lock (_lockObject)
                {
                    doc = _lastParsedDocument;
                }

                if (doc == null)
                {
                    doc = await ParseCurrentPageAsync();
                }

                var images = doc.DocumentNode.QuerySelectorAll(imageSelector);

                var imageInfos = new List<ImageInfo>();

                foreach (var img in images)
                {
                    var src = img.GetAttributeValue("src", "");
                    var alt = img.GetAttributeValue("alt", "");
                    var title = img.GetAttributeValue("title", "");

                    if (!string.IsNullOrEmpty(src))
                    {
                        imageInfos.Add(new ImageInfo
                        {
                            Src = src,
                            Alt = alt,
                            Title = title
                        });
                    }
                }

                return imageInfos;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ExtractImagesAsync error: {ex.Message}");
                return new List<ImageInfo>();
            }
        }

        /// <summary>
        /// メタタグ情報を抽出（スレッドセーフ）
        /// </summary>
        /// <returns>メタタグ情報の辞書</returns>
        public async Task<Dictionary<string, string>> ExtractMetaTagsAsync()
        {
            try
            {
                HtmlDocument doc;
                lock (_lockObject)
                {
                    doc = _lastParsedDocument;
                }

                if (doc == null)
                {
                    doc = await ParseCurrentPageAsync();
                }

                var metaTags = doc.DocumentNode.QuerySelectorAll("meta");

                var metaInfo = new Dictionary<string, string>();

                foreach (var meta in metaTags)
                {
                    var name = meta.GetAttributeValue("name", "");
                    var property = meta.GetAttributeValue("property", "");
                    var content = meta.GetAttributeValue("content", "");

                    if (!string.IsNullOrEmpty(content))
                    {
                        if (!string.IsNullOrEmpty(name))
                        {
                            metaInfo[$"name:{name}"] = content;
                        }
                        else if (!string.IsNullOrEmpty(property))
                        {
                            metaInfo[$"property:{property}"] = content;
                        }
                    }
                }

                return metaInfo;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ExtractMetaTagsAsync error: {ex.Message}");
                return new Dictionary<string, string>();
            }
        }

        #endregion

        #region ファイル保存機能（スレッドセーフ）

        /// <summary>
        /// HTMLをファイルに保存（スレッドセーフ）
        /// </summary>
        /// <param name="filePath">保存先ファイルパス</param>
        /// <returns>成功した場合true</returns>
        public async Task<bool> SaveHtmlToFileAsync(string filePath)
        {
            try
            {
                var html = await GetPageHtmlAsync();
                await File.WriteAllTextAsync(filePath, html, Encoding.UTF8);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SaveHtmlToFileAsync error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// パース済みHTMLをファイルに保存（スレッドセーフ）
        /// </summary>
        /// <param name="filePath">保存先ファイルパス</param>
        /// <returns>成功した場合true</returns>
        public async Task<bool> SaveParsedHtmlToFileAsync(string filePath)
        {
            try
            {
                HtmlDocument doc;
                lock (_lockObject)
                {
                    doc = _lastParsedDocument;
                }

                if (doc == null)
                {
                    doc = await ParseCurrentPageAsync();
                }

                await File.WriteAllTextAsync(filePath, doc.DocumentNode.OuterHtml, Encoding.UTF8);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SaveParsedHtmlToFileAsync error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// ページのスクリーンショットと共にHTMLを保存（スレッドセーフ）
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
        /// ページ情報をJSONファイルに保存（スレッドセーフ）
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

                await File.WriteAllTextAsync(filePath, json, Encoding.UTF8);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SavePageInfoToFileAsync error: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region プロパティ（スレッドセーフ）

        /// <summary>
        /// 最後にパースされたHTMLドキュメント（スレッドセーフ）
        /// </summary>
        public HtmlDocument LastParsedDocument
        {
            get
            {
                lock (_lockObject)
                {
                    return _lastParsedDocument;
                }
            }
        }

        /// <summary>
        /// 最後に取得されたHTMLコンテンツ（スレッドセーフ）
        /// </summary>
        public string LastHtmlContent
        {
            get
            {
                lock (_lockObject)
                {
                    return _lastHtmlContent;
                }
            }
        }

        #endregion

        #region リソース管理（スレッドセーフ）

        /// <summary>
        /// リソースを解放（スレッドセーフ）
        /// </summary>
        public void Dispose()
        {
            lock (_lockObject)
            {
                if (_disposed) return;

                try
                {
                    _lastParsedDocument = null;
                    _lastHtmlContent = null;
                    _disposed = true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"HtmlExtractionService dispose error: {ex.Message}");
                }
            }
        }

        #endregion
    }

    /// <summary>
    /// リンク情報を表すクラス
    /// </summary>
    public class LinkInfo
    {
        public string Url { get; set; }
        public string Text { get; set; }
        public string Title { get; set; }
    }

    /// <summary>
    /// 画像情報を表すクラス
    /// </summary>
    public class ImageInfo
    {
        public string Src { get; set; }
        public string Alt { get; set; }
        public string Title { get; set; }
    }
}