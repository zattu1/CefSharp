using CefSharp;
using CefSharp.fastBOT.Models;
using CefSharp.fastBOT.Utils;
using CefSharp.fastBOT.UI;
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
    /// ブラウザタブ管理クラス（スレッドセーフ版）
    /// </summary>
    public class BrowserTabManager : IDisposable
    {
        private readonly TabControl _tabControl;
        private readonly List<BrowserTab> _tabs;
        private readonly HtmlDataManager _htmlDataManager;
        private readonly object _lockObject = new object();
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

            // タブ選択変更イベントを追加（UIスレッドでのみ）
            if (Application.Current.Dispatcher.CheckAccess())
            {
                _tabControl.SelectionChanged += TabControl_SelectionChanged;
            }
            else
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    _tabControl.SelectionChanged += TabControl_SelectionChanged;
                });
            }

            Console.WriteLine("BrowserTabManager initialized successfully");
        }

        /// <summary>
        /// タブ選択変更時の処理（スレッドセーフ版）
        /// </summary>
        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                // UIスレッドでの実行を保証
                if (!Application.Current.Dispatcher.CheckAccess())
                {
                    Application.Current.Dispatcher.BeginInvoke(new Action(() => TabControl_SelectionChanged(sender, e)));
                    return;
                }

                var currentTab = GetCurrentTabInternal();
                if (currentTab?.Browser != null)
                {
                    var currentUrl = currentTab.Browser.Address;
                    if (!string.IsNullOrEmpty(currentUrl))
                    {
                        // URL変更イベントを発火
                        OnCurrentUrlChanged?.Invoke(currentUrl);
                        Console.WriteLine($"Tab switched - OnCurrentUrlChanged fired: {currentUrl}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Tab selection changed error: {ex.Message}");
            }
        }

        /// <summary>
        /// 現在アクティブなタブを取得（スレッドセーフ版）
        /// </summary>
        /// <returns>アクティブなタブ</returns>
        public BrowserTab GetCurrentTab()
        {
            try
            {
                // UIスレッドでの実行を保証
                if (!Application.Current.Dispatcher.CheckAccess())
                {
                    return Application.Current.Dispatcher.Invoke(() => GetCurrentTabInternal());
                }
                else
                {
                    return GetCurrentTabInternal();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetCurrentTab error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 現在アクティブなタブを取得（内部実装・UIスレッド専用）
        /// </summary>
        /// <returns>アクティブなタブ</returns>
        private BrowserTab GetCurrentTabInternal()
        {
            try
            {
                lock (_lockObject)
                {
                    var selectedTabItem = _tabControl.SelectedItem as TabItem;
                    return _tabs.FirstOrDefault(t => t.TabItem == selectedTabItem);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetCurrentTabInternal error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 現在アクティブなブラウザを取得（スレッドセーフ版）
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
        /// 新しいタブを作成（スレッドセーフ版）
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

                // UIスレッドでの実行を保証
                if (!Application.Current.Dispatcher.CheckAccess())
                {
                    return Application.Current.Dispatcher.Invoke(() => CreateTabInternal(title, url, requestContext));
                }
                else
                {
                    return CreateTabInternal(title, url, requestContext);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Tab creation failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 新しいタブを作成（内部実装・UIスレッド専用）
        /// </summary>
        private BrowserTab CreateTabInternal(string title, string url, IRequestContext requestContext = null)
        {
            try
            {
                // ブラウザを作成
                var browser = new ChromiumWebBrowser(url);

                // RequestContextは安全に設定
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
                    }
                }

                // タブヘッダー用のStackPanel作成
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
                    HtmlExtractor = null
                };

                // ブラウザイベントの設定
                browser.TitleChanged += (sender, args) => OnBrowserTitleChanged(tab, args.NewValue?.ToString());
                browser.AddressChanged += (sender, args) => OnBrowserAddressChanged(tab, args.NewValue?.ToString());
                browser.LoadingStateChanged += (sender, args) => OnBrowserLoadingStateChanged(tab, args);
                browser.FrameLoadEnd += (sender, args) =>
                {
                    if (args.Frame.IsMain)
                    {
                        OnFrameLoadEnd(tab, args.Frame);
                    }
                };

                // 初期Faviconを設定
                SetDefaultFavicon(tab);

                // スレッドセーフにタブを追加
                lock (_lockObject)
                {
                    _tabs.Add(tab);
                    _tabControl.Items.Add(tabItem);
                    _tabControl.SelectedItem = tabItem;
                }

                // URL同期
                SyncUrlToMainWindow(tab);

                Console.WriteLine($"Tab created successfully: {title}");
                return tab;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"CreateTabInternal failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// タブを閉じる（スレッドセーフ版）
        /// </summary>
        /// <param name="tab">閉じるタブ</param>
        /// <returns>成功した場合true</returns>
        public bool CloseTab(BrowserTab tab)
        {
            try
            {
                if (tab?.TabItem == null) return false;

                // UIスレッドでの実行を保証
                if (!Application.Current.Dispatcher.CheckAccess())
                {
                    return Application.Current.Dispatcher.Invoke(() => CloseTabInternal(tab));
                }
                else






                {
                    return CloseTabInternal(tab);
                }


            }
            catch (Exception ex)
            {
                Console.WriteLine($"Tab close failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// タブを閉じる（内部実装・UIスレッド専用）
        /// </summary>
        private bool CloseTabInternal(BrowserTab tab)
        {
            try
            {
                lock (_lockObject)
                {
                    if (_tabs.Contains(tab))
                    {
                        _tabControl.Items.Remove(tab.TabItem);
                        _tabs.Remove(tab);

                        tab.Browser?.Dispose();
                        tab.HtmlExtractor = null;

                        Console.WriteLine($"Tab closed: {tab.OriginalTitle}");
                        return true;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"CloseTabInternal failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// すべてのタブを取得（スレッドセーフ版）
        /// </summary>
        /// <returns>タブの一覧</returns>
        public List<BrowserTab> GetAllTabs()
        {
            lock (_lockObject)
            {
                return new List<BrowserTab>(_tabs);
            }
        }

        /// <summary>
        /// タブの総数を取得（スレッドセーフ版）
        /// </summary>
        public int TabCount
        {
            get
            {
                lock (_lockObject)
                {
                    return _tabs.Count;
                }
            }
        }

        /// <summary>
        /// 指定したタブをアクティブにする（スレッドセーフ版）
        /// </summary>
        /// <param name="tab">アクティブにするタブ</param>
        public void ActivateTab(BrowserTab tab)
        {
            if (tab?.TabItem == null) return;

            try
            {
                if (!Application.Current.Dispatcher.CheckAccess())
                {
                    Application.Current.Dispatcher.BeginInvoke(new Action(() => ActivateTabInternal(tab)));
                }
                else
                {
                    ActivateTabInternal(tab);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ActivateTab error: {ex.Message}");
            }
        }

        /// <summary>
        /// 指定したタブをアクティブにする（内部実装）
        /// </summary>
        private void ActivateTabInternal(BrowserTab tab)
        {
            lock (_lockObject)
            {
                if (_tabs.Contains(tab))
                {
                    _tabControl.SelectedItem = tab.TabItem;
                }
            }
        }

        #region HTML取得機能（スレッドセーフ版）

        /// <summary>
        /// 現在のタブのHTMLを取得（スレッドセーフ版）
        /// </summary>
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

        #endregion

        #region ブラウザイベントハンドラー（スレッドセーフ版）

        /// <summary>
        /// ブラウザのタイトル変更イベント（スレッドセーフ版）
        /// </summary>
        private void OnBrowserTitleChanged(BrowserTab tab, string newTitle)
        {
            try
            {
                if (!Application.Current.Dispatcher.CheckAccess())
                {
                    Application.Current.Dispatcher.BeginInvoke(new Action(() => OnBrowserTitleChanged(tab, newTitle)));
                    return;
                }

                if (!string.IsNullOrWhiteSpace(newTitle))
                {
                    tab.OriginalTitle = newTitle;
                    tab.TitleTextBlock.Text = TruncateTitle(newTitle, CalculateMaxTitleLength());
                    Console.WriteLine($"Tab title updated: {TruncateTitle(newTitle, CalculateMaxTitleLength())}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"タイトル更新エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// ブラウザのアドレス変更イベント（スレッドセーフ版）
        /// </summary>
        private void OnBrowserAddressChanged(BrowserTab tab, string newAddress)
        {
            try
            {
                Console.WriteLine($"Browser address changed: {newAddress}");

                // アドレス変更時はデフォルトFaviconを設定
                SetDefaultFavicon(tab);

                // 現在のアクティブタブの場合、イベントを発火
                var currentTab = GetCurrentTab();
                if (currentTab == tab)
                {
                    SyncUrlToMainWindow(tab);
                    OnCurrentUrlChanged?.Invoke(newAddress);
                    Console.WriteLine($"OnCurrentUrlChanged event fired for: {newAddress}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"OnBrowserAddressChanged error: {ex.Message}");
            }
        }

        /// <summary>
        /// ブラウザの読み込み状態変更イベント（スレッドセーフ版）
        /// </summary>
        private void OnBrowserLoadingStateChanged(BrowserTab tab, LoadingStateChangedEventArgs args)
        {
            try
            {
                if (!Application.Current.Dispatcher.CheckAccess())
                {
                    Application.Current.Dispatcher.BeginInvoke(new Action(() => OnBrowserLoadingStateChanged(tab, args)));
                    return;
                }

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
                Console.WriteLine($"読み込み状態変更処理エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// フレーム読み込み完了時の処理（スレッドセーフ版）
        /// </summary>
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
                            var currentTab = GetCurrentTabInternal();
                            if (currentTab == tab)
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

        #region プライベートメソッド（スレッドセーフ版）

        /// <summary>
        /// MainWindowにURLを同期（スレッドセーフ版）
        /// </summary>
        private void SyncUrlToMainWindow(BrowserTab tab)
        {
            try
            {
                var currentTab = GetCurrentTab();
                if (tab?.Browser != null && currentTab == tab)
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
        /// デフォルトのFaviconを設定（スレッドセーフ版）
        /// </summary>
        private void SetDefaultFavicon(BrowserTab tab)
        {
            try
            {
                if (!Application.Current.Dispatcher.CheckAccess())
                {
                    Application.Current.Dispatcher.BeginInvoke(new Action(() => SetDefaultFavicon(tab)));
                    return;
                }

                tab.FaviconImage.Source = CreateDefaultFavicon();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"デフォルトFavicon設定エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// デフォルトFaviconを作成
        /// </summary>
        private ImageSource CreateDefaultFavicon()
        {
            try
            {
                // UIスレッドでのみ実行
                if (!Application.Current.Dispatcher.CheckAccess())
                {
                    return Application.Current.Dispatcher.Invoke(() => CreateDefaultFavicon());
                }

                // フォールバック用のアイコンを描画
                var drawingVisual = new DrawingVisual();
                using (var drawingContext = drawingVisual.RenderOpen())
                {
                    drawingContext.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, 16, 16));

                    var pageBrush = new SolidColorBrush(Color.FromRgb(240, 240, 240));
                    var pagePen = new Pen(new SolidColorBrush(Color.FromRgb(128, 128, 128)), 1);

                    var geometry = new PathGeometry();
                    var figure = new PathFigure { StartPoint = new Point(3, 2) };
                    figure.Segments.Add(new LineSegment(new Point(10, 2), true));
                    figure.Segments.Add(new LineSegment(new Point(12, 4), true));
                    figure.Segments.Add(new LineSegment(new Point(12, 14), true));
                    figure.Segments.Add(new LineSegment(new Point(3, 14), true));
                    figure.IsClosed = true;
                    geometry.Figures.Add(figure);

                    drawingContext.DrawGeometry(pageBrush, pagePen, geometry);
                    drawingContext.DrawLine(pagePen, new Point(10, 2), new Point(10, 4));
                    drawingContext.DrawLine(pagePen, new Point(10, 4), new Point(12, 4));

                    var textPen = new Pen(new SolidColorBrush(Color.FromRgb(100, 100, 100)), 0.5);
                    drawingContext.DrawLine(textPen, new Point(5, 6), new Point(10, 6));
                    drawingContext.DrawLine(textPen, new Point(5, 8), new Point(11, 8));
                    drawingContext.DrawLine(textPen, new Point(5, 10), new Point(9, 10));
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

        /// <summary>
        /// CefSharpブラウザからFaviconを取得（スレッドセーフ版）
        /// </summary>
        private void GetFaviconFromBrowser(BrowserTab tab)
        {
            try
            {
                if (tab?.Browser != null)
                {
                    var mainFrame = tab.Browser.GetMainFrame();
                    if (mainFrame != null)
                    {
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
                                        SetDefaultFavicon(tab);
                                    }
                                }
                                else
                                {
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
                        SetDefaultFavicon(tab);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetFaviconFromBrowser error: {ex.Message}");
                SetDefaultFavicon(tab);
            }
        }

        /// <summary>
        /// URLからFaviconを読み込み（スレッドセーフ版）
        /// </summary>
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
                                using var stream = new System.IO.MemoryStream(imageData);

                                var bitmapImage = new BitmapImage();
                                bitmapImage.BeginInit();
                                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                                bitmapImage.StreamSource = stream;
                                bitmapImage.DecodePixelWidth = 16;
                                bitmapImage.DecodePixelHeight = 16;
                                bitmapImage.EndInit();

                                if (bitmapImage.CanFreeze)
                                {
                                    bitmapImage.Freeze();
                                }

                                if (tab?.FaviconImage != null)
                                {
                                    tab.FaviconImage.Source = bitmapImage;
                                    Console.WriteLine($"Favicon loaded successfully: {faviconUrl}");
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
        /// MainWindowのFaviconを更新（スレッドセーフ版）
        /// </summary>
        private void UpdateMainWindowFavicon(string faviconUrl)
        {
            try
            {
                if (!Application.Current.Dispatcher.CheckAccess())
                {
                    Application.Current.Dispatcher.BeginInvoke(new Action(() => UpdateMainWindowFavicon(faviconUrl)));
                    return;
                }

                var mainWindow = Application.Current.MainWindow as MainWindow;
                mainWindow?.UpdateAddressFaviconFromTab(faviconUrl);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"MainWindow Favicon更新エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// タブのタイトルを手動で更新（スレッドセーフ版）
        /// </summary>
        public void UpdateTabTitle(BrowserTab tab, string newTitle)
        {
            if (tab?.TabItem == null) return;

            try
            {
                if (!Application.Current.Dispatcher.CheckAccess())
                {
                    Application.Current.Dispatcher.BeginInvoke(new Action(() => UpdateTabTitle(tab, newTitle)));
                    return;
                }

                lock (_lockObject)
                {
                    if (_tabs.Contains(tab))
                    {
                        tab.OriginalTitle = newTitle;
                        if (tab.TitleTextBlock != null)
                        {
                            tab.TitleTextBlock.Text = TruncateTitle(newTitle, CalculateMaxTitleLength());
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"UpdateTabTitle error: {ex.Message}");
            }
        }

        /// <summary>
        /// 固定タブ幅に基づいてタイトルの最大文字数を計算
        /// </summary>
        private int CalculateMaxTitleLength()
        {
            double availableWidth = FIXED_TAB_WIDTH - 30;
            return (int)(availableWidth / 9);
        }

        /// <summary>
        /// タイトルを指定した長さに切り詰める
        /// </summary>
        private string TruncateTitle(string title, int maxLength = 20)
        {
            if (string.IsNullOrEmpty(title))
                return "新しいタブ";

            if (title.Length <= maxLength)
                return title;

            return title.Substring(0, maxLength - 3) + "...";
        }

        #endregion

        #region JavaScript実行機能（スレッドセーフ版）

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
        public delegate void JavaScriptCallback(JavaScriptResult result);

        /// <summary>
        /// 現在のタブでJavaScriptを実行（非同期・スレッドセーフ版）
        /// </summary>
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
        /// 現在のタブでJavaScriptを実行（同期・スレッドセーフ版）
        /// </summary>
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
        /// 指定したブラウザでJavaScriptを実行（非同期・スレッドセーフ版）
        /// </summary>
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
                            if (Application.Current.Dispatcher.CheckAccess())
                            {
                                callback(result);
                            }
                            else
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
                            if (Application.Current.Dispatcher.CheckAccess())
                            {
                                callback(errorResult);
                            }
                            else
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
                    }
                });
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
        /// 指定したブラウザでJavaScriptを実行（同期・スレッドセーフ版）
        /// </summary>
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

        #endregion

        #region その他のメソッド

        /// <summary>
        /// HTMLの保存履歴を取得
        /// </summary>
        public List<HtmlFileInfo> GetSavedHtmlFiles()
        {
            return _htmlDataManager.GetSavedFiles();
        }

        /// <summary>
        /// HTMLデータを比較
        /// </summary>
        public HtmlComparisonResult CompareHtmlData(HtmlData htmlData1, HtmlData htmlData2)
        {
            return _htmlDataManager.CompareHtml(htmlData1.Content, htmlData2.Content);
        }

        #endregion

        /// <summary>
        /// リソースを解放（スレッドセーフ版）
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            try
            {
                // UIスレッドでイベントハンドラーを解除
                if (Application.Current.Dispatcher.CheckAccess())
                {
                    _tabControl.SelectionChanged -= TabControl_SelectionChanged;
                }
                else
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        _tabControl.SelectionChanged -= TabControl_SelectionChanged;
                    });
                }

                // すべてのタブを閉じる
                var tabsToClose = GetAllTabs();
                foreach (var tab in tabsToClose)
                {
                    CloseTab(tab);
                }

                lock (_lockObject)
                {
                    _tabs.Clear();
                }

                _disposed = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"BrowserTabManager.Dispose error: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// ブラウザタブ情報（スレッドセーフ版）
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