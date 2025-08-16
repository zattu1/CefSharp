using CefSharp;
using CefSharp.fastBOT.Models;
using CefSharp.fastBOT.UI;
using CefSharp.fastBOT.Utils;
using CefSharp.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CefSharp.fastBOT.Core
{
    /// <summary>
    /// ブラウザタブ管理クラス（修正版）
    /// </summary>
    public class BrowserTabManager : IDisposable
    {
        private readonly TabControl _tabControl;
        private readonly List<BrowserTab> _tabs;
        private readonly HtmlDataManager _htmlDataManager;
        private bool _disposed = false;

        // タブ幅の設定
        private const double FIXED_TAB_WIDTH = 200.0;
        private const double MIN_TAB_WIDTH = 120.0;
        private const double MAX_TAB_WIDTH = 250.0;

        // MainWindowとの連携用
        public event Action<string> OnCurrentUrlChanged;
        public event Action<HtmlData> OnHtmlDataExtracted;

        public BrowserTabManager(TabControl tabControl)
        {
            _tabControl = tabControl ?? throw new ArgumentNullException(nameof(tabControl));
            _tabs = new List<BrowserTab>();
            _htmlDataManager = new HtmlDataManager();

            // タブ選択変更イベントを追加
            _tabControl.SelectionChanged += TabControl_SelectionChanged;

            Console.WriteLine("BrowserTabManager initialized successfully");
        }

        /// <summary>
        /// タブ選択変更時の処理
        /// </summary>
        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                var currentTab = GetCurrentTab();
                if (currentTab?.Browser != null)
                {
                    var currentUrl = currentTab.Browser.Address;
                    if (!string.IsNullOrEmpty(currentUrl))
                    {
                        OnCurrentUrlChanged?.Invoke(currentUrl);
                        Console.WriteLine($"Tab switched to URL: {currentUrl}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Tab selection changed error: {ex.Message}");
            }
        }

        /// <summary>
        /// 新しいタブを作成（古いバージョンの安定した方式を採用）
        /// </summary>
        /// <param name="title">初期タイトル</param>
        /// <param name="url">初期URL</param>
        /// <param name="requestContext">リクエストコンテキスト</param>
        /// <returns>作成されたタブ</returns>
        public BrowserTab CreateTab(string title, string url, IRequestContext requestContext = null)
        {
            try
            {
                Console.WriteLine($"Creating tab with title: {title}, URL: {url}");

                // 古いバージョンと同じ方式でブラウザを作成
                var browser = new ChromiumWebBrowser(url);

                // RequestContextは安全に設定（失敗した場合はnullのまま）
                if (requestContext != null)
                {
                    try
                    {
                        browser.RequestContext = requestContext;
                        Console.WriteLine("RequestContext set after browser creation");
                    }
                    catch (Exception rcEx)
                    {
                        Console.WriteLine($"Failed to set RequestContext: {rcEx.Message}");
                        Console.WriteLine("Browser will use global context");
                    }
                }

                // タブヘッダー用のStackPanel作成（古いバージョンと同じ構造）
                var headerPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    VerticalAlignment = VerticalAlignment.Center,
                    Width = FIXED_TAB_WIDTH,
                    HorizontalAlignment = HorizontalAlignment.Left
                };

                // Favicon用Image
                var faviconImage = new Image
                {
                    Width = 16,
                    Height = 16,
                    Margin = new Thickness(4, 0, 6, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Left
                };

                // タイトル用TextBlock
                var titleTextBlock = new TextBlock
                {
                    Text = TruncateTitle(title, CalculateMaxTitleLength()),
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Width = FIXED_TAB_WIDTH - 30,
                    Margin = new Thickness(0, 0, 4, 0)
                };

                headerPanel.Children.Add(faviconImage);
                headerPanel.Children.Add(titleTextBlock);

                var tabItem = new TabItem
                {
                    Header = headerPanel,
                    Content = browser,
                    Width = FIXED_TAB_WIDTH,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch
                };

                var tab = new BrowserTab
                {
                    TabItem = tabItem,
                    Browser = browser,
                    ContextName = requestContext?.GetHashCode().ToString() ?? "Default",
                    OriginalTitle = title,
                    FaviconImage = faviconImage,
                    TitleTextBlock = titleTextBlock,
                    HtmlExtractor = null // 後で初期化
                };

                // ブラウザイベントの設定（古いバージョンと同じ）
                browser.TitleChanged += (sender, args) => OnBrowserTitleChanged(tab, args.NewValue?.ToString());
                browser.AddressChanged += (sender, args) => OnBrowserAddressChanged(tab, args.NewValue?.ToString());
                browser.LoadingStateChanged += (sender, args) => OnBrowserLoadingStateChanged(tab, args);

                // FrameLoadEndイベントでFaviconを取得
                browser.FrameLoadEnd += (sender, args) =>
                {
                    if (args.Frame.IsMain)
                    {
                        OnFrameLoadEnd(tab, args.Frame);
                    }
                };

                // 初期Faviconを設定
                SetDefaultFavicon(tab);

                _tabs.Add(tab);
                _tabControl.Items.Add(tabItem);
                _tabControl.SelectedItem = tabItem;

                // タブ作成後にURL同期
                SyncUrlToMainWindow(tab);

                Console.WriteLine($"Tab created successfully: {title}");
                return tab;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Tab creation failed: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return null;
            }
        }

        #region HTML取得機能

        /// <summary>
        /// 現在のタブのHTMLを取得
        /// </summary>
        /// <param name="dataType">データタイプ</param>
        /// <param name="selector">セレクター（Element取得時）</param>
        /// <param name="autoSave">自動保存するかどうか</param>
        /// <returns>HTMLデータ</returns>
        public async Task<HtmlData> ExtractHtmlAsync(HtmlDataType dataType = HtmlDataType.FullPage, string selector = null, bool autoSave = false)
        {
            try
            {
                var currentTab = GetCurrentTab();
                if (currentTab?.Browser == null)
                {
                    throw new InvalidOperationException("アクティブなタブまたはブラウザが見つかりません");
                }

                // HtmlExtractionServiceを遅延初期化
                if (currentTab.HtmlExtractor == null)
                {
                    currentTab.HtmlExtractor = new HtmlExtractionService(currentTab.Browser);
                }

                string content = string.Empty;
                PageInfo pageInfo = null;

                // データタイプに応じてHTMLを取得
                switch (dataType)
                {
                    case HtmlDataType.FullPage:
                        content = await currentTab.HtmlExtractor.GetPageHtmlAsync();
                        pageInfo = await currentTab.HtmlExtractor.GetPageInfoAsync();
                        break;

                    case HtmlDataType.BodyOnly:
                        content = await currentTab.HtmlExtractor.GetPageBodyHtmlAsync();
                        pageInfo = await currentTab.HtmlExtractor.GetPageInfoAsync();
                        break;

                    case HtmlDataType.Element:
                        if (string.IsNullOrEmpty(selector))
                            throw new ArgumentException("Element取得時はセレクターが必要です", nameof(selector));
                        content = await currentTab.HtmlExtractor.GetElementHtmlAsync(selector);
                        pageInfo = await currentTab.HtmlExtractor.GetPageInfoAsync();
                        break;

                    case HtmlDataType.TextOnly:
                        content = await currentTab.HtmlExtractor.GetPageTextAsync();
                        pageInfo = await currentTab.HtmlExtractor.GetPageInfoAsync();
                        break;

                    default:
                        throw new ArgumentException($"サポートされていないデータタイプ: {dataType}");
                }

                var htmlData = new HtmlData
                {
                    Content = content,
                    PageInfo = pageInfo,
                    DataType = dataType,
                    Selector = selector ?? string.Empty,
                    CapturedAt = DateTime.Now,
                    Size = System.Text.Encoding.UTF8.GetByteCount(content)
                };

                // 自動保存が有効な場合
                if (autoSave)
                {
                    var filePath = await _htmlDataManager.SaveHtmlDataAsync(content, pageInfo, dataType);
                    htmlData.FilePath = filePath;
                }

                // イベントを発火
                OnHtmlDataExtracted?.Invoke(htmlData);

                Console.WriteLine($"HTML extracted: {dataType}, Size: {htmlData.Size} bytes");
                return htmlData;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ExtractHtmlAsync error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 複数のデータタイプでHTMLを一括取得
        /// </summary>
        /// <param name="dataTypes">取得するデータタイプのリスト</param>
        /// <param name="autoSave">自動保存するかどうか</param>
        /// <returns>HTMLデータのリスト</returns>
        public async Task<List<HtmlData>> ExtractMultipleHtmlAsync(List<HtmlDataType> dataTypes, bool autoSave = false)
        {
            var results = new List<HtmlData>();

            foreach (var dataType in dataTypes)
            {
                try
                {
                    var htmlData = await ExtractHtmlAsync(dataType, autoSave: autoSave);
                    results.Add(htmlData);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to extract {dataType}: {ex.Message}");
                }
            }

            return results;
        }

        /// <summary>
        /// 指定した要素のHTMLを取得
        /// </summary>
        /// <param name="selector">CSSセレクター</param>
        /// <param name="autoSave">自動保存するかどうか</param>
        /// <returns>HTMLデータ</returns>
        public async Task<HtmlData> ExtractElementHtmlAsync(string selector, bool autoSave = false)
        {
            return await ExtractHtmlAsync(HtmlDataType.Element, selector, autoSave);
        }

        /// <summary>
        /// HTMLの保存履歴を取得
        /// </summary>
        /// <returns>保存されたHTMLファイルの情報</returns>
        public List<HtmlFileInfo> GetSavedHtmlFiles()
        {
            return _htmlDataManager.GetSavedFiles();
        }

        /// <summary>
        /// HTMLデータを比較
        /// </summary>
        /// <param name="htmlData1">HTMLデータ1</param>
        /// <param name="htmlData2">HTMLデータ2</param>
        /// <returns>比較結果</returns>
        public HtmlComparisonResult CompareHtmlData(HtmlData htmlData1, HtmlData htmlData2)
        {
            return _htmlDataManager.CompareHtml(htmlData1.Content, htmlData2.Content);
        }

        #endregion

        #region JavaScript実行機能（古いバージョンから移植）

        /// <summary>
        /// JavaScript実行結果を表すクラス
        /// </summary>
        public class JavaScriptResult
        {
            public bool Success { get; set; }
            public object Result { get; set; }
            public string ErrorMessage { get; set; }
            public string Script { get; set; }
            public DateTime ExecutedAt { get; set; }
        }

        /// <summary>
        /// JavaScript実行完了時のコールバック
        /// </summary>
        /// <param name="result">実行結果</param>
        public delegate void JavaScriptCallback(JavaScriptResult result);

        /// <summary>
        /// 現在のタブでJavaScriptを実行（非同期）
        /// </summary>
        /// <param name="script">実行するJavaScriptコード</param>
        /// <param name="callback">実行完了時のコールバック</param>
        /// <param name="timeoutSeconds">タイムアウト秒数（デフォルト30秒）</param>
        public void ExecuteJavaScriptAsync(string script, JavaScriptCallback callback = null, int timeoutSeconds = 30)
        {
            try
            {
                var currentTab = GetCurrentTab();
                if (currentTab?.Browser == null)
                {
                    var errorResult = new JavaScriptResult
                    {
                        Success = false,
                        ErrorMessage = "アクティブなタブまたはブラウザが見つかりません",
                        Script = script,
                        ExecutedAt = DateTime.Now
                    };

                    callback?.Invoke(errorResult);
                    return;
                }

                ExecuteJavaScriptOnBrowser(currentTab.Browser, script, callback, timeoutSeconds);
            }
            catch (Exception ex)
            {
                var errorResult = new JavaScriptResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    Script = script,
                    ExecutedAt = DateTime.Now
                };

                callback?.Invoke(errorResult);
                Console.WriteLine($"ExecuteJavaScriptAsync error: {ex.Message}");
            }
        }

        /// <summary>
        /// 現在のタブでJavaScriptを実行（同期）
        /// </summary>
        /// <param name="script">実行するJavaScriptコード</param>
        /// <param name="timeoutSeconds">タイムアウト秒数（デフォルト30秒）</param>
        /// <returns>実行結果</returns>
        public async Task<JavaScriptResult> ExecuteJavaScriptSync(string script, int timeoutSeconds = 30)
        {
            try
            {
                var currentTab = GetCurrentTab();
                if (currentTab?.Browser == null)
                {
                    return new JavaScriptResult
                    {
                        Success = false,
                        ErrorMessage = "アクティブなタブまたはブラウザが見つかりません",
                        Script = script,
                        ExecutedAt = DateTime.Now
                    };
                }

                return await ExecuteJavaScriptOnBrowserSync(currentTab.Browser, script, timeoutSeconds);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ExecuteJavaScriptSync error: {ex.Message}");
                return new JavaScriptResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    Script = script,
                    ExecutedAt = DateTime.Now
                };
            }
        }

        /// <summary>
        /// 指定したブラウザでJavaScriptを実行（非同期）
        /// </summary>
        /// <param name="browser">対象ブラウザ</param>
        /// <param name="script">実行するJavaScriptコード</param>
        /// <param name="callback">実行完了時のコールバック</param>
        /// <param name="timeoutSeconds">タイムアウト秒数</param>
        public void ExecuteJavaScriptOnBrowser(ChromiumWebBrowser browser, string script, JavaScriptCallback callback = null, int timeoutSeconds = 30)
        {
            try
            {
                if (browser == null || !browser.IsBrowserInitialized)
                {
                    var errorResult = new JavaScriptResult
                    {
                        Success = false,
                        ErrorMessage = "ブラウザが初期化されていません",
                        Script = script,
                        ExecutedAt = DateTime.Now
                    };

                    callback?.Invoke(errorResult);
                    return;
                }

                var mainFrame = browser.GetMainFrame();
                if (mainFrame == null)
                {
                    var errorResult = new JavaScriptResult
                    {
                        Success = false,
                        ErrorMessage = "メインフレームが取得できません",
                        Script = script,
                        ExecutedAt = DateTime.Now
                    };

                    callback?.Invoke(errorResult);
                    return;
                }

                // タイムアウト設定
                var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));

                // JavaScript実行
                var task = mainFrame.EvaluateScriptAsync(script);

                task.ContinueWith(completedTask =>
                {
                    try
                    {
                        var result = new JavaScriptResult
                        {
                            Script = script,
                            ExecutedAt = DateTime.Now
                        };

                        if (completedTask.IsCanceled)
                        {
                            result.Success = false;
                            result.ErrorMessage = "JavaScriptの実行がタイムアウトしました";
                        }
                        else if (completedTask.IsFaulted)
                        {
                            result.Success = false;
                            result.ErrorMessage = completedTask.Exception?.GetBaseException()?.Message ?? "JavaScript実行中にエラーが発生しました";
                        }
                        else
                        {
                            var response = completedTask.Result;
                            result.Success = response.Success;
                            result.Result = response.Result;
                            result.ErrorMessage = response.Success ? null : response.Message;
                        }

                        // UIスレッドでコールバックを実行
                        if (callback != null)
                        {
                            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                try
                                {
                                    callback(result);
                                }
                                catch (Exception callbackEx)
                                {
                                    Console.WriteLine($"JavaScript callback error: {callbackEx.Message}");
                                }
                            }));
                        }

                        Console.WriteLine($"JavaScript executed: Success={result.Success}, Result={result.Result}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"JavaScript execution completion error: {ex.Message}");

                        var errorResult = new JavaScriptResult
                        {
                            Success = false,
                            ErrorMessage = ex.Message,
                            Script = script,
                            ExecutedAt = DateTime.Now
                        };

                        if (callback != null)
                        {
                            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                try
                                {
                                    callback(errorResult);
                                }
                                catch (Exception callbackEx)
                                {
                                    Console.WriteLine($"Error callback execution error: {callbackEx.Message}");
                                }
                            }));
                        }
                    }
                }, cancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                var errorResult = new JavaScriptResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    Script = script,
                    ExecutedAt = DateTime.Now
                };

                callback?.Invoke(errorResult);
                Console.WriteLine($"ExecuteJavaScriptOnBrowser error: {ex.Message}");
            }
        }

        /// <summary>
        /// 指定したブラウザでJavaScriptを実行（同期）
        /// </summary>
        /// <param name="browser">対象ブラウザ</param>
        /// <param name="script">実行するJavaScriptコード</param>
        /// <param name="timeoutSeconds">タイムアウト秒数</param>
        /// <returns>実行結果</returns>
        public async Task<JavaScriptResult> ExecuteJavaScriptOnBrowserSync(ChromiumWebBrowser browser, string script, int timeoutSeconds = 30)
        {
            try
            {
                if (browser == null || !browser.IsBrowserInitialized)
                {
                    return new JavaScriptResult
                    {
                        Success = false,
                        ErrorMessage = "ブラウザが初期化されていません",
                        Script = script,
                        ExecutedAt = DateTime.Now
                    };
                }

                var mainFrame = browser.GetMainFrame();
                if (mainFrame == null)
                {
                    return new JavaScriptResult
                    {
                        Success = false,
                        ErrorMessage = "メインフレームが取得できません",
                        Script = script,
                        ExecutedAt = DateTime.Now
                    };
                }

                // タイムアウト設定
                using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));

                try
                {
                    var response = await mainFrame.EvaluateScriptAsync(script);

                    return new JavaScriptResult
                    {
                        Success = response.Success,
                        Result = response.Result,
                        ErrorMessage = response.Success ? null : response.Message,
                        Script = script,
                        ExecutedAt = DateTime.Now
                    };
                }
                catch (OperationCanceledException)
                {
                    return new JavaScriptResult
                    {
                        Success = false,
                        ErrorMessage = "JavaScriptの実行がタイムアウトしました",
                        Script = script,
                        ExecutedAt = DateTime.Now
                    };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ExecuteJavaScriptOnBrowserSync error: {ex.Message}");
                return new JavaScriptResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    Script = script,
                    ExecutedAt = DateTime.Now
                };
            }
        }

        /// <summary>
        /// JavaScript実行のヘルパーメソッド：要素の存在確認
        /// </summary>
        /// <param name="selector">CSSセレクター</param>
        /// <param name="callback">結果コールバック</param>
        public void CheckElementExists(string selector, Action<bool> callback)
        {
            var script = $"document.querySelector('{selector}') !== null";

            ExecuteJavaScriptAsync(script, result =>
            {
                try
                {
                    if (result.Success && result.Result is bool exists)
                    {
                        callback?.Invoke(exists);
                    }
                    else
                    {
                        callback?.Invoke(false);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"CheckElementExists callback error: {ex.Message}");
                    callback?.Invoke(false);
                }
            });
        }

        /// <summary>
        /// JavaScript実行のヘルパーメソッド：要素のテキスト取得
        /// </summary>
        /// <param name="selector">CSSセレクター</param>
        /// <param name="callback">結果コールバック</param>
        public void GetElementText(string selector, Action<string> callback)
        {
            var script = $@"
                var element = document.querySelector('{selector}');
                element ? element.textContent : null;
            ";

            ExecuteJavaScriptAsync(script, result =>
            {
                try
                {
                    var text = result.Success ? result.Result?.ToString() : null;
                    callback?.Invoke(text);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"GetElementText callback error: {ex.Message}");
                    callback?.Invoke(null);
                }
            });
        }

        /// <summary>
        /// JavaScript実行のヘルパーメソッド：要素をクリック
        /// </summary>
        /// <param name="selector">CSSセレクター</param>
        /// <param name="callback">成功/失敗のコールバック</param>
        public void ClickElement(string selector, Action<bool> callback = null)
        {
            var script = $@"
                var element = document.querySelector('{selector}');
                if (element) {{
                    element.click();
                    true;
                }} else {{
                    false;
                }}
            ";

            ExecuteJavaScriptAsync(script, result =>
            {
                try
                {
                    var success = result.Success && result.Result is bool clicked && clicked;
                    callback?.Invoke(success);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ClickElement callback error: {ex.Message}");
                    callback?.Invoke(false);
                }
            });
        }

        /// <summary>
        /// JavaScript実行のヘルパーメソッド：フォーム入力
        /// </summary>
        /// <param name="selector">CSSセレクター</param>
        /// <param name="value">入力値</param>
        /// <param name="callback">成功/失敗のコールバック</param>
        public void SetElementValue(string selector, string value, Action<bool> callback = null)
        {
            var script = $@"
                var element = document.querySelector('{selector}');
                if (element) {{
                    element.value = '{value.Replace("'", "\\'")}';
                    element.dispatchEvent(new Event('input', {{ bubbles: true }}));
                    element.dispatchEvent(new Event('change', {{ bubbles: true }}));
                    true;
                }} else {{
                    false;
                }}
            ";

            ExecuteJavaScriptAsync(script, result =>
            {
                try
                {
                    var success = result.Success && result.Result is bool valueSet && valueSet;
                    callback?.Invoke(success);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"SetElementValue callback error: {ex.Message}");
                    callback?.Invoke(false);
                }
            });
        }

        #endregion

        #region ブラウザイベントハンドラー（古いバージョンをベースに改良）

        /// <summary>
        /// ブラウザのタイトル変更イベント
        /// </summary>
        /// <param name="tab">対象タブ</param>
        /// <param name="newTitle">新しいタイトル</param>
        private void OnBrowserTitleChanged(BrowserTab tab, string newTitle)
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (!string.IsNullOrWhiteSpace(newTitle))
                    {
                        tab.OriginalTitle = newTitle;
                        tab.TitleTextBlock.Text = TruncateTitle(newTitle, CalculateMaxTitleLength());

                        Console.WriteLine($"Tab title updated: {TruncateTitle(newTitle, CalculateMaxTitleLength())}");
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"タイトル更新エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// ブラウザのアドレス変更イベント
        /// </summary>
        /// <param name="tab">対象タブ</param>
        /// <param name="newAddress">新しいアドレス</param>
        private void OnBrowserAddressChanged(BrowserTab tab, string newAddress)
        {
            try
            {
                Console.WriteLine($"Tab address changed: {newAddress}");

                // アドレス変更時はデフォルトFaviconを設定（後でFrameLoadEndで更新される）
                SetDefaultFavicon(tab);

                // 現在のアクティブタブの場合、MainWindowのURLを更新
                if (GetCurrentTab() == tab)
                {
                    SyncUrlToMainWindow(tab);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"アドレス変更処理エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// ブラウザの読み込み状態変更イベント
        /// </summary>
        /// <param name="tab">対象タブ</param>
        /// <param name="args">読み込み状態</param>
        private void OnBrowserLoadingStateChanged(BrowserTab tab, LoadingStateChangedEventArgs args)
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    try
                    {
                        if (args.IsLoading)
                        {
                            // 読み込み中の表示を更新
                            var titleBlock = tab.TitleTextBlock;
                            if (titleBlock != null && !titleBlock.Text.StartsWith("🔄 "))
                            {
                                titleBlock.Text = "🔄 " + TruncateTitle(tab.OriginalTitle, CalculateMaxTitleLength() - 2);
                            }
                        }
                        else
                        {
                            // 読み込み完了時の表示を更新
                            var titleBlock = tab.TitleTextBlock;
                            if (titleBlock != null)
                            {
                                titleBlock.Text = TruncateTitle(tab.OriginalTitle, CalculateMaxTitleLength());
                            }

                            // 読み込み完了時にHTML抽出サービスを初期化
                            if (tab.Browser != null && tab.HtmlExtractor == null)
                            {
                                tab.HtmlExtractor = new HtmlExtractionService(tab.Browser);
                                Console.WriteLine("HtmlExtractionService initialized after loading completed");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Tab header update error: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"読み込み状態変更処理エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// フレーム読み込み完了時の処理
        /// </summary>
        /// <param name="tab">対象タブ</param>
        /// <param name="frame">フレーム</param>
        private void OnFrameLoadEnd(BrowserTab tab, IFrame frame)
        {
            try
            {
                if (frame.IsMain)
                {
                    Console.WriteLine($"Main frame load completed for: {frame.Url}");

                    // メインフレーム読み込み完了後にFaviconを取得
                    Task.Delay(500).ContinueWith(_ =>
                    {
                        try
                        {
                            GetFaviconFromBrowser(tab);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"GetFaviconFromBrowser error: {ex.Message}");
                        }
                    }, TaskScheduler.Current);

                    // UIスレッドで安全にMainWindowのURLを再同期
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            if (GetCurrentTab() == tab)
                            {
                                SyncUrlToMainWindow(tab);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"URL sync error in OnFrameLoadEnd: {ex.Message}");
                        }
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"OnFrameLoadEnd error: {ex.Message}");
            }
        }

        #endregion

        #region タブ操作メソッド

        /// <summary>
        /// タブを閉じる
        /// </summary>
        /// <param name="tab">閉じるタブ</param>
        /// <returns>成功した場合true</returns>
        public bool CloseTab(BrowserTab tab)
        {
            try
            {
                if (tab?.TabItem != null && _tabs.Contains(tab))
                {
                    _tabControl.Items.Remove(tab.TabItem);
                    _tabs.Remove(tab);

                    tab.Browser?.Dispose();
                    tab.HtmlExtractor = null;

                    Console.WriteLine($"Tab closed: {tab.OriginalTitle}");
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Tab close failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 現在アクティブなタブを取得
        /// </summary>
        /// <returns>アクティブなタブ</returns>
        public BrowserTab GetCurrentTab()
        {
            try
            {
                var selectedTabItem = _tabControl.SelectedItem as TabItem;
                return _tabs.FirstOrDefault(t => t.TabItem == selectedTabItem);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetCurrentTab error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 現在アクティブなブラウザを取得
        /// </summary>
        /// <returns>アクティブなブラウザ</returns>
        public ChromiumWebBrowser GetCurrentBrowser()
        {
            try
            {
                var currentTab = GetCurrentTab();
                return currentTab?.Browser;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetCurrentBrowser error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// すべてのタブを取得
        /// </summary>
        /// <returns>タブの一覧</returns>
        public List<BrowserTab> GetAllTabs()
        {
            return new List<BrowserTab>(_tabs);
        }

        /// <summary>
        /// タブの総数を取得
        /// </summary>
        public int TabCount => _tabs.Count;

        /// <summary>
        /// 指定したタブをアクティブにする
        /// </summary>
        /// <param name="tab">アクティブにするタブ</param>
        public void ActivateTab(BrowserTab tab)
        {
            if (tab?.TabItem != null && _tabs.Contains(tab))
            {
                _tabControl.SelectedItem = tab.TabItem;
            }
        }

        /// <summary>
        /// タブのタイトルを手動で更新
        /// </summary>
        /// <param name="tab">対象タブ</param>
        /// <param name="newTitle">新しいタイトル</param>
        public void UpdateTabTitle(BrowserTab tab, string newTitle)
        {
            if (tab?.TabItem != null && _tabs.Contains(tab))
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    tab.OriginalTitle = newTitle;
                    if (tab.TitleTextBlock != null)
                    {
                        tab.TitleTextBlock.Text = TruncateTitle(newTitle, CalculateMaxTitleLength());
                    }
                });
            }
        }

        #endregion

        #region プライベートメソッド（古いバージョンから移植・改良）

        /// <summary>
        /// MainWindowにURLを同期
        /// </summary>
        /// <param name="tab">対象タブ</param>
        private void SyncUrlToMainWindow(BrowserTab tab)
        {
            try
            {
                if (tab?.Browser != null && GetCurrentTab() == tab)
                {
                    var currentUrl = tab.Browser.Address;
                    if (!string.IsNullOrEmpty(currentUrl))
                    {
                        OnCurrentUrlChanged?.Invoke(currentUrl);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"URL sync error: {ex.Message}");
            }
        }

        /// <summary>
        /// デフォルトのFaviconを設定
        /// </summary>
        /// <param name="tab">対象タブ</param>
        private void SetDefaultFavicon(BrowserTab tab)
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    tab.FaviconImage.Source = CreateDefaultFavicon();
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"デフォルトFavicon設定エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// デフォルトFaviconを作成
        /// </summary>
        /// <returns>デフォルトFaviconのImageSource</returns>
        private ImageSource CreateDefaultFavicon()
        {
            try
            {
                // UIスレッドでのみ実行されることを確認
                if (!Application.Current.Dispatcher.CheckAccess())
                {
                    return Application.Current.Dispatcher.Invoke(() => CreateDefaultFavicon());
                }

                // まずfastBOT.icoリソースから読み込みを試行
                var resourceFavicon = LoadFaviconFromResource();
                if (resourceFavicon != null)
                {
                    return resourceFavicon;
                }

                // リソースが読み込めない場合はフォールバック用のアイコンを描画
                return CreateFallbackFavicon();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"CreateDefaultFavicon error: {ex.Message}");
                return CreateFallbackFavicon();
            }
        }

        /// <summary>
        /// 埋め込みリソースからfastBOT.icoを読み込み
        /// </summary>
        /// <returns>fastBOT.icoのImageSource</returns>
        private ImageSource LoadFaviconFromResource()
        {
            try
            {
                // pack://application:,,, URI スキームを使用してリソースにアクセス
                var uri = new Uri("pack://application:,,,/Resources/fastBOT.ico");

                var bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.UriSource = uri;
                bitmapImage.DecodePixelWidth = 16;
                bitmapImage.DecodePixelHeight = 16;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();

                if (bitmapImage.CanFreeze)
                {
                    bitmapImage.Freeze();
                }

                Console.WriteLine("fastBOT.ico loaded from resources");
                return bitmapImage;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load fastBOT.ico from resources: {ex.Message}");

                // 代替方法: 埋め込みリソースから直接読み込み
                try
                {
                    var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                    var resourceName = "CefSharp.fastBOT.Resources.fastBOT.ico"; // 名前空間に注意

                    using var stream = assembly.GetManifestResourceStream(resourceName);
                    if (stream != null)
                    {
                        var bitmapImage = new BitmapImage();
                        bitmapImage.BeginInit();
                        bitmapImage.StreamSource = stream;
                        bitmapImage.DecodePixelWidth = 16;
                        bitmapImage.DecodePixelHeight = 16;
                        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                        bitmapImage.EndInit();

                        if (bitmapImage.CanFreeze)
                        {
                            bitmapImage.Freeze();
                        }

                        Console.WriteLine("fastBOT.ico loaded from embedded resources");
                        return bitmapImage;
                    }
                }
                catch (Exception embeddedEx)
                {
                    Console.WriteLine($"Failed to load embedded fastBOT.ico: {embeddedEx.Message}");
                }

                return null;
            }
        }

        /// <summary>
        /// フォールバック用のアイコンを描画
        /// </summary>
        /// <returns>描画されたアイコンのImageSource</returns>
        private ImageSource CreateFallbackFavicon()
        {
            try
            {
                // 16x16のビットマップを作成
                var drawingVisual = new DrawingVisual();
                using (var drawingContext = drawingVisual.RenderOpen())
                {
                    // 背景を透明に
                    drawingContext.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, 16, 16));

                    // ページアイコンを描画（書類のようなアイコン）
                    var pageBrush = new SolidColorBrush(Color.FromRgb(240, 240, 240));
                    var pagePen = new Pen(new SolidColorBrush(Color.FromRgb(128, 128, 128)), 1);

                    // 書類の形を描画
                    var geometry = new PathGeometry();
                    var figure = new PathFigure { StartPoint = new Point(3, 2) };
                    figure.Segments.Add(new LineSegment(new Point(10, 2), true));
                    figure.Segments.Add(new LineSegment(new Point(12, 4), true));
                    figure.Segments.Add(new LineSegment(new Point(12, 14), true));
                    figure.Segments.Add(new LineSegment(new Point(3, 14), true));
                    figure.IsClosed = true;
                    geometry.Figures.Add(figure);

                    drawingContext.DrawGeometry(pageBrush, pagePen, geometry);

                    // 書類の折り目
                    drawingContext.DrawLine(pagePen, new Point(10, 2), new Point(10, 4));
                    drawingContext.DrawLine(pagePen, new Point(10, 4), new Point(12, 4));

                    // 書類の線（テキストを表現）
                    var textPen = new Pen(new SolidColorBrush(Color.FromRgb(100, 100, 100)), 0.5);
                    drawingContext.DrawLine(textPen, new Point(5, 6), new Point(10, 6));
                    drawingContext.DrawLine(textPen, new Point(5, 8), new Point(11, 8));
                    drawingContext.DrawLine(textPen, new Point(5, 10), new Point(9, 10));
                }

                var renderTargetBitmap = new RenderTargetBitmap(16, 16, 96, 96, PixelFormats.Pbgra32);
                renderTargetBitmap.Render(drawingVisual);

                // UIスレッドでFreeze
                if (renderTargetBitmap.CanFreeze)
                {
                    renderTargetBitmap.Freeze();
                }

                return renderTargetBitmap;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"CreateFallbackFavicon error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// CefSharpブラウザからFaviconを取得
        /// </summary>
        /// <param name="tab">対象タブ</param>
        private void GetFaviconFromBrowser(BrowserTab tab)
        {
            try
            {
                if (tab?.Browser != null)
                {
                    // CefSharp .NETCore版では直接ブラウザからMainFrameを取得
                    var mainFrame = tab.Browser.GetMainFrame();
                    if (mainFrame != null)
                    {
                        // JavaScriptでFaviconのURLを取得
                        var script = @"
                            (function() {
                                var links = document.getElementsByTagName('link');
                                var faviconUrl = '';
                                
                                for (var i = 0; i < links.length; i++) {
                                    var link = links[i];
                                    var rel = link.getAttribute('rel');
                                    if (rel) {
                                        rel = rel.toLowerCase();
                                        if (rel === 'icon' || rel === 'shortcut icon' || rel.indexOf('icon') !== -1) {
                                            if (link.href) {
                                                faviconUrl = link.href;
                                                break;
                                            }
                                        }
                                    }
                                }
                                
                                if (!faviconUrl) {
                                    faviconUrl = window.location.origin + '/favicon.ico';
                                }
                                
                                return faviconUrl;
                            })();
                        ";

                        mainFrame.EvaluateScriptAsync(script).ContinueWith(task =>
                        {
                            try
                            {
                                if (task.Result.Success && task.Result.Result != null)
                                {
                                    var faviconUrl = task.Result.Result.ToString();
                                    if (!string.IsNullOrEmpty(faviconUrl))
                                    {
                                        Console.WriteLine($"Found favicon URL: {faviconUrl}");
                                        LoadFaviconFromUrl(tab, faviconUrl);
                                    }
                                    else
                                    {
                                        Console.WriteLine("No favicon URL found");
                                        SetDefaultFavicon(tab);
                                    }
                                }
                                else
                                {
                                    Console.WriteLine($"JavaScript execution failed: {task.Result?.Message}");
                                    SetDefaultFavicon(tab);
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Favicon script result processing error: {ex.Message}");
                                SetDefaultFavicon(tab);
                            }
                        }, TaskScheduler.Current);
                    }
                    else
                    {
                        Console.WriteLine("MainFrame is null");
                        SetDefaultFavicon(tab);
                    }
                }
                else
                {
                    Console.WriteLine("Browser is null");
                    SetDefaultFavicon(tab);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetFaviconFromBrowser error: {ex.Message}");
                SetDefaultFavicon(tab);
            }
        }

        /// <summary>
        /// URLからFaviconを読み込み（改良版）
        /// </summary>
        /// <param name="tab">対象タブ</param>
        /// <param name="faviconUrl">FaviconのURL</param>
        private void LoadFaviconFromUrl(BrowserTab tab, string faviconUrl)
        {
            try
            {
                Task.Run(async () =>
                {
                    try
                    {
                        using var httpClient = new System.Net.Http.HttpClient();
                        httpClient.Timeout = TimeSpan.FromSeconds(5);
                        httpClient.DefaultRequestHeaders.Add("User-Agent",
                            UserAgentHelper.GetChromeUserAgent());

                        var imageData = await httpClient.GetByteArrayAsync(faviconUrl);

                        // UIスレッドで画像を作成
                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            try
                            {
                                // メモリストリームを使用してBitmapImageを作成
                                using var stream = new System.IO.MemoryStream(imageData);

                                var bitmapImage = new BitmapImage();
                                bitmapImage.BeginInit();
                                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                                bitmapImage.StreamSource = stream;
                                bitmapImage.DecodePixelWidth = 16;
                                bitmapImage.DecodePixelHeight = 16;
                                bitmapImage.EndInit();

                                // UIスレッドでFreeze
                                if (bitmapImage.CanFreeze)
                                {
                                    bitmapImage.Freeze();
                                }

                                // 安全にUIオブジェクトにアクセス
                                if (tab?.FaviconImage != null)
                                {
                                    tab.FaviconImage.Source = bitmapImage;
                                    Console.WriteLine($"Favicon loaded successfully: {faviconUrl}");

                                    // MainWindowのFaviconも更新
                                    UpdateMainWindowFavicon(faviconUrl);
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Favicon image creation error: {ex.Message}");
                                SetDefaultFavicon(tab);
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Favicon download error for {faviconUrl}: {ex.Message}");
                        // UIスレッドでデフォルトFaviconを設定
                        Application.Current.Dispatcher.Invoke(() => SetDefaultFavicon(tab));
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"LoadFaviconFromUrl error: {ex.Message}");
                SetDefaultFavicon(tab);
            }
        }

        /// <summary>
        /// MainWindowのFaviconを更新
        /// </summary>
        /// <param name="faviconUrl">FaviconのURL</param>
        private void UpdateMainWindowFavicon(string faviconUrl)
        {
            try
            {
                // MainWindowのインスタンスを取得
                var mainWindow = Application.Current.MainWindow as MainWindow;
                mainWindow?.UpdateAddressFaviconFromTab(faviconUrl);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"MainWindow Favicon更新エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// 固定タブ幅に基づいてタイトルの最大文字数を計算
        /// </summary>
        /// <returns>最大文字数</returns>
        private int CalculateMaxTitleLength()
        {
            // アイコンとマージンを考慮して、おおよその文字数を計算
            // 1文字あたり約8-10ピクセルと仮定
            double availableWidth = FIXED_TAB_WIDTH - 30; // Faviconとマージンを引く
            return (int)(availableWidth / 9); // 1文字9ピクセルと仮定
        }

        /// <summary>
        /// タイトルを指定した長さに切り詰める
        /// </summary>
        /// <param name="title">元のタイトル</param>
        /// <param name="maxLength">最大長</param>
        /// <returns>切り詰められたタイトル</returns>
        private string TruncateTitle(string title, int maxLength = 20)
        {
            if (string.IsNullOrEmpty(title))
                return "新しいタブ";

            if (title.Length <= maxLength)
                return title;

            return title.Substring(0, maxLength - 3) + "...";
        }

        #endregion

        public void Dispose()
        {
            if (!_disposed)
            {
                // イベントハンドラーを解除
                _tabControl.SelectionChanged -= TabControl_SelectionChanged;

                foreach (var tab in _tabs.ToList())
                {
                    CloseTab(tab);
                }
                _tabs.Clear();
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// ブラウザタブ情報（HTML取得機能統合版）
    /// </summary>
    public class BrowserTab
    {
        public TabItem TabItem { get; set; }
        public ChromiumWebBrowser Browser { get; set; }
        public string ContextName { get; set; }
        public string OriginalTitle { get; set; }
        public Image FaviconImage { get; set; }
        public TextBlock TitleTextBlock { get; set; }
        public HtmlExtractionService HtmlExtractor { get; set; }
    }
}