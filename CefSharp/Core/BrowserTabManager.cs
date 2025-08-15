using CefSharp;
using CefSharp.fastBOT.Models;
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
    /// ブラウザタブ管理クラス（デバッグ強化版）
    /// </summary>
    public class BrowserTabManager : IDisposable
    {
        private readonly TabControl _tabControl;
        private readonly List<BrowserTab> _tabs;
        private readonly HtmlDataManager _htmlDataManager;
        private bool _disposed = false;

        // タブ幅の設定
        private const double FIXED_TAB_WIDTH = 200.0;

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
        /// 新しいタブを作成（デバッグ強化版）
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

                // CefSharpが初期化されているか確認
                if (Cef.IsInitialized != true)
                {
                    Console.WriteLine("ERROR: CefSharp is not initialized!");
                    throw new InvalidOperationException("CefSharp is not initialized");
                }

                Console.WriteLine("CefSharp is initialized, creating ChromiumWebBrowser...");

                // 基本的な設定でブラウザを作成（利用可能なプロパティのみ使用）
                BrowserSettings initialSettings = null;
                try
                {
                    initialSettings = new BrowserSettings()
                    {
                        Javascript = CefState.Enabled,
                        JavascriptCloseWindows = CefState.Disabled,
                        JavascriptAccessClipboard = CefState.Disabled,
                        JavascriptDomPaste = CefState.Disabled
                    };
                    Console.WriteLine("BrowserSettings created with basic configuration");
                }
                catch (Exception settingsEx)
                {
                    Console.WriteLine($"BrowserSettings creation failed, using default: {settingsEx.Message}");
                    initialSettings = new BrowserSettings();
                }

                var browser = new ChromiumWebBrowser()
                {
                    BrowserSettings = initialSettings
                };

                // RequestContextを設定
                if (requestContext != null)
                {
                    Console.WriteLine("Setting RequestContext...");
                    browser.RequestContext = requestContext;
                }
                else
                {
                    Console.WriteLine("Using default RequestContext");
                }

                // タブヘッダーを作成
                Console.WriteLine("Creating tab header...");
                var headerPanel = CreateTabHeader(title);

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
                    FaviconImage = GetFaviconImageFromHeader(headerPanel),
                    TitleTextBlock = GetTitleTextBlockFromHeader(headerPanel),
                    HtmlExtractor = null // 後で初期化
                };

                // ブラウザイベントの設定
                Console.WriteLine("Setting up browser events...");
                SetupBrowserEvents(tab);

                // 初期Faviconを設定
                SetDefaultFavicon(tab);

                _tabs.Add(tab);
                _tabControl.Items.Add(tabItem);
                _tabControl.SelectedItem = tabItem;

                Console.WriteLine($"Tab added to collection. Total tabs: {_tabs.Count}");

                // ブラウザが初期化された後にURLを読み込む
                browser.IsBrowserInitializedChanged += (sender, e) =>
                {
                    if (e.NewValue is bool isInitialized && isInitialized)
                    {
                        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            try
                            {
                                Console.WriteLine($"Browser initialized, loading URL: {url}");
                                browser.LoadUrl(url);

                                // HtmlExtractionServiceを初期化
                                tab.HtmlExtractor = new HtmlExtractionService(browser);
                                Console.WriteLine("HtmlExtractionService initialized");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error loading URL after browser initialization: {ex.Message}");
                            }
                        }));
                    }
                };

                // 即座にURLを読み込む（フォールバック）
                try
                {
                    Console.WriteLine($"Attempting immediate URL load: {url}");
                    browser.LoadUrl(url);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Immediate URL load failed: {ex.Message}");
                }

                // ブラウザ初期化状態を定期的にチェック（デバッグ用）
                var initCheckTimer = new System.Windows.Threading.DispatcherTimer();
                initCheckTimer.Interval = TimeSpan.FromSeconds(1);
                initCheckTimer.Tick += (sender, e) =>
                {
                    try
                    {
                        Console.WriteLine($"Browser initialization status: {browser.IsBrowserInitialized}");
                        if (browser.IsBrowserInitialized)
                        {
                            Console.WriteLine($"Browser is now initialized! Current URL: {browser.Address}");
                            initCheckTimer.Stop();

                            // HtmlExtractionServiceを確実に初期化
                            if (tab.HtmlExtractor == null)
                            {
                                tab.HtmlExtractor = new HtmlExtractionService(browser);
                                Console.WriteLine("HtmlExtractionService initialized via timer check");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Timer check error: {ex.Message}");
                    }
                };
                initCheckTimer.Start();

                // 10秒後にタイマーを停止
                Task.Delay(10000).ContinueWith(_ =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            initCheckTimer.Stop();
                            Console.WriteLine("Browser initialization check timer stopped");
                        }
                        catch { }
                    });
                });

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

        /// <summary>
        /// タブヘッダーを作成
        /// </summary>
        /// <param name="title">タイトル</param>
        /// <returns>ヘッダーパネル</returns>
        private StackPanel CreateTabHeader(string title)
        {
            var headerPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Width = FIXED_TAB_WIDTH,
                HorizontalAlignment = HorizontalAlignment.Left
            };

            var faviconImage = new Image
            {
                Width = 16,
                Height = 16,
                Margin = new Thickness(4, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left
            };

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

            return headerPanel;
        }

        /// <summary>
        /// ブラウザイベントを設定（デバッグ強化版）
        /// </summary>
        /// <param name="tab">対象タブ</param>
        private void SetupBrowserEvents(BrowserTab tab)
        {
            try
            {
                tab.Browser.IsBrowserInitializedChanged += (sender, args) =>
                {
                    Console.WriteLine($"Browser initialization changed: {args.NewValue}");
                };

                tab.Browser.TitleChanged += (sender, args) =>
                {
                    Console.WriteLine($"Title changed: {args.NewValue}");
                    OnBrowserTitleChanged(tab, args.NewValue?.ToString());
                };

                tab.Browser.AddressChanged += (sender, args) =>
                {
                    Console.WriteLine($"Address changed: {args.NewValue}");
                    OnBrowserAddressChanged(tab, args.NewValue?.ToString());
                };

                tab.Browser.LoadingStateChanged += (sender, args) =>
                {
                    Console.WriteLine($"Loading state changed - IsLoading: {args.IsLoading}, CanGoBack: {args.CanGoBack}, CanGoForward: {args.CanGoForward}");
                    OnBrowserLoadingStateChanged(tab, args);
                };

                tab.Browser.FrameLoadEnd += (sender, args) =>
                {
                    Console.WriteLine($"Frame load end - IsMain: {args.Frame.IsMain}, URL: {args.Frame.Url}");
                    if (args.Frame.IsMain)
                    {
                        OnFrameLoadEnd(tab, args.Frame);
                    }
                };

                tab.Browser.LoadError += (sender, args) =>
                {
                    Console.WriteLine($"Load error - ErrorCode: {args.ErrorCode}, ErrorText: {args.ErrorText}, FailedUrl: {args.FailedUrl}");
                };

                Console.WriteLine("Browser events setup completed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SetupBrowserEvents error: {ex.Message}");
            }
        }

        /// <summary>
        /// ヘッダーからFaviconImageを取得
        /// </summary>
        private Image GetFaviconImageFromHeader(StackPanel headerPanel)
        {
            return headerPanel.Children.OfType<Image>().FirstOrDefault();
        }

        /// <summary>
        /// ヘッダーからTitleTextBlockを取得
        /// </summary>
        private TextBlock GetTitleTextBlockFromHeader(StackPanel headerPanel)
        {
            return headerPanel.Children.OfType<TextBlock>().FirstOrDefault();
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
                if (currentTab?.HtmlExtractor == null)
                {
                    throw new InvalidOperationException("アクティブなタブまたはHTML抽出サービスが見つかりません");
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

        #region ブラウザイベントハンドラー

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
                Console.WriteLine($"OnBrowserTitleChanged error: {ex.Message}");
            }
        }

        private void OnBrowserAddressChanged(BrowserTab tab, string newAddress)
        {
            try
            {
                Console.WriteLine($"Tab address changed: {newAddress}");
                SetDefaultFavicon(tab);

                if (GetCurrentTab() == tab)
                {
                    SyncUrlToMainWindow(tab);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"OnBrowserAddressChanged error: {ex.Message}");
            }
        }

        private void OnBrowserLoadingStateChanged(BrowserTab tab, LoadingStateChangedEventArgs args)
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (args.IsLoading)
                    {
                        var titleBlock = tab.TitleTextBlock;
                        if (titleBlock != null && !titleBlock.Text.StartsWith("🔄 "))
                        {
                            titleBlock.Text = "🔄 " + TruncateTitle(tab.OriginalTitle, CalculateMaxTitleLength() - 2);
                        }
                    }
                    else
                    {
                        var titleBlock = tab.TitleTextBlock;
                        if (titleBlock != null)
                        {
                            titleBlock.Text = TruncateTitle(tab.OriginalTitle, CalculateMaxTitleLength());
                        }

                        // 読み込み完了時にHTML抽出サービスを更新
                        if (tab.Browser != null && tab.HtmlExtractor == null)
                        {
                            tab.HtmlExtractor = new HtmlExtractionService(tab.Browser);
                            Console.WriteLine("HtmlExtractionService initialized after loading completed");
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"OnBrowserLoadingStateChanged error: {ex.Message}");
            }
        }

        private void OnFrameLoadEnd(BrowserTab tab, IFrame frame)
        {
            try
            {
                if (frame.IsMain)
                {
                    Console.WriteLine($"Main frame load completed for: {frame.Url}");

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

                    if (GetCurrentTab() == tab)
                    {
                        SyncUrlToMainWindow(tab);
                    }
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

        #endregion

        #region プライベートメソッド

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
                Console.WriteLine($"SyncUrlToMainWindow error: {ex.Message}");
            }
        }

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
                Console.WriteLine($"SetDefaultFavicon error: {ex.Message}");
            }
        }

        private ImageSource CreateDefaultFavicon()
        {
            // 簡略化されたデフォルトアイコン作成
            try
            {
                var drawingVisual = new DrawingVisual();
                using (var drawingContext = drawingVisual.RenderOpen())
                {
                    drawingContext.DrawRectangle(Brushes.LightGray,
                        new Pen(Brushes.Gray, 1), new Rect(0, 0, 16, 16));
                    drawingContext.DrawLine(new Pen(Brushes.DarkGray, 1),
                        new Point(4, 8), new Point(12, 8));
                }

                var renderTargetBitmap = new RenderTargetBitmap(16, 16, 96, 96, PixelFormats.Pbgra32);
                renderTargetBitmap.Render(drawingVisual);

                if (renderTargetBitmap.CanFreeze)
                {
                    renderTargetBitmap.Freeze();
                }

                return renderTargetBitmap;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"CreateDefaultFavicon error: {ex.Message}");
                return null;
            }
        }

        private void GetFaviconFromBrowser(BrowserTab tab)
        {
            // ブラウザからFaviconを取得（簡略化）
            try
            {
                var mainFrame = tab.Browser.GetMainFrame();
                if (mainFrame != null)
                {
                    var script = @"
                        (function() {
                            var links = document.getElementsByTagName('link');
                            for (var i = 0; i < links.length; i++) {
                                var link = links[i];
                                var rel = link.getAttribute('rel');
                                if (rel && rel.toLowerCase().indexOf('icon') !== -1) {
                                    return link.href;
                                }
                            }
                            return window.location.origin + '/favicon.ico';
                        })();
                    ";

                    mainFrame.EvaluateScriptAsync(script).ContinueWith(task =>
                    {
                        // Favicon読み込み処理（簡略化）
                        Console.WriteLine($"Favicon script executed for tab: {tab.OriginalTitle}");
                    }, TaskScheduler.Current);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetFaviconFromBrowser error: {ex.Message}");
            }
        }

        private int CalculateMaxTitleLength()
        {
            return (int)((FIXED_TAB_WIDTH - 30) / 9);
        }

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