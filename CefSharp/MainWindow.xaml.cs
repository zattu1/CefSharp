using CefSharp.fastBOT.Core;
using CefSharp.fastBOT.Models;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace CefSharp.fastBOT
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// 統合版：ブラウザ + 自動購入コントロールパネル
    /// </summary>
    public partial class MainWindow : Window
    {
        // ブラウザ関連マネージャー
        private RequestContextManager _requestContextManager;
        private BrowserTabManager _tabManager;
        private UserSettings _userSettings;

        public MainWindow()
        {
            InitializeComponent();
            InitializeManagers();
            InitializeUI();
            CreateInitialTab();
            SetupAutoPurchaseControl();
        }

        /// <summary>
        /// 基本的なマネージャーを初期化
        /// </summary>
        private void InitializeManagers()
        {
            try
            {
                _requestContextManager = new RequestContextManager();
                _tabManager = new BrowserTabManager(TabWidget);
                _userSettings = new UserSettings();

                // タブマネージャーからのURL変更イベントを購読
                _tabManager.OnCurrentUrlChanged += TabManager_OnCurrentUrlChanged;

                // 基本的なイベントハンドラーを設定
                SetupBasicEventHandlers();

                Console.WriteLine("Integrated MainWindow managers initialized successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"InitializeManagers error: {ex.Message}");
            }
        }

        /// <summary>
        /// 基本的なイベントハンドラーを設定
        /// </summary>
        private void SetupBasicEventHandlers()
        {
            try
            {
                // ブラウザコントロールの基本イベント
                PrevButton.Click += PrevButton_Click;
                NextButton.Click += NextButton_Click;
                ReloadButton.Click += ReloadButton_Click;
                TopButton.Click += TopButton_Click;
                GoButton.Click += GoButton_Click;
                NewTabButton.Click += CreateNewTabButton_Click;
                UrlLineEdit.KeyDown += UrlLineEdit_KeyDown;

                // コンテキストメニューのイベント
                var contextMenu = TabWidget.ContextMenu;
                if (contextMenu != null)
                {
                    foreach (var item in contextMenu.Items)
                    {
                        if (item is MenuItem menuItem)
                        {
                            switch (menuItem.Header.ToString())
                            {
                                case "新しいタブ(_N)":
                                    menuItem.Click += NewTabMenuItem_Click;
                                    break;
                                case "タブを閉じる(_C)":
                                    menuItem.Click += CloseTabMenuItem_Click;
                                    break;
                                case "他のタブを閉じる(_O)":
                                    menuItem.Click += CloseOtherTabsMenuItem_Click;
                                    break;
                                case "タブを複製(_D)":
                                    menuItem.Click += DuplicateTabMenuItem_Click;
                                    break;
                                case "新しいウィンドウで開く(_W)":
                                    menuItem.Click += NewWindowMenuItem_Click;
                                    break;
                                case "タブ復元(_R)":
                                    menuItem.Click += RestoreLastClosedTabMenuItem_Click;
                                    break;
                            }
                        }
                    }
                }

                Console.WriteLine("Basic event handlers setup completed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SetupBasicEventHandlers error: {ex.Message}");
            }
        }

        /// <summary>
        /// UIを初期化
        /// </summary>
        private void InitializeUI()
        {
            try
            {
                // デフォルトURL設定
                UrlLineEdit.Text = "https://www.yahoo.co.jp/";

                Console.WriteLine("Integrated UI initialized");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"InitializeUI error: {ex.Message}");
            }
        }

        /// <summary>
        /// 初期タブを作成
        /// </summary>
        private void CreateInitialTab()
        {
            try
            {
                var context = _requestContextManager.CreateIsolatedContext("MainBrowserSession");
                var tab = _tabManager.CreateTab("読み込み中...", UrlLineEdit.Text, context);

                if (tab != null)
                {
                    // ブラウザイベントの設定
                    tab.Browser.IsBrowserInitializedChanged += Browser_IsBrowserInitializedChanged;
                    tab.Browser.AddressChanged += Browser_AddressChanged;
                    tab.Browser.LoadingStateChanged += Browser_LoadingStateChanged;

                    Console.WriteLine("Initial tab created successfully");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"CreateInitialTab error: {ex.Message}");
            }
        }

        /// <summary>
        /// 自動購入コントロールパネルを設定
        /// </summary>
        private void SetupAutoPurchaseControl()
        {
            try
            {
                // AutoPurchaseControlにブラウザサービスを設定
                AutoPurchaseControlPanel.SetBrowserServices(_tabManager, _requestContextManager);

                Console.WriteLine("AutoPurchaseControl setup completed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SetupAutoPurchaseControl error: {ex.Message}");
            }
        }

        #region ブラウザコントロールイベント

        private void PrevButton_Click(object sender, RoutedEventArgs e)
        {
            _tabManager.GetCurrentBrowser()?.Back();
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            _tabManager.GetCurrentBrowser()?.Forward();
        }

        private void ReloadButton_Click(object sender, RoutedEventArgs e)
        {
            _tabManager.GetCurrentBrowser()?.Reload();
        }

        private void TopButton_Click(object sender, RoutedEventArgs e)
        {
            var browser = _tabManager.GetCurrentBrowser();
            if (browser != null)
            {
                browser.LoadUrl("https://www.yahoo.co.jp/");
            }
        }

        private void GoButton_Click(object sender, RoutedEventArgs e)
        {
            NavigateToUrl();
        }

        private void CreateNewTabButton_Click(object sender, RoutedEventArgs e)
        {
            CreateNewTab();
        }

        private void UrlLineEdit_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                NavigateToUrl();
            }
        }

        #endregion

        #region タブコンテキストメニューイベント

        private void NewTabMenuItem_Click(object sender, RoutedEventArgs e)
        {
            CreateNewTab();
        }

        private void CloseTabMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var currentTab = _tabManager.GetCurrentTab();
            if (currentTab != null)
            {
                _tabManager.CloseTab(currentTab);
            }
        }

        private void CloseOtherTabsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var currentTab = _tabManager.GetCurrentTab();
            var allTabs = _tabManager.GetAllTabs().ToList();

            foreach (var tab in allTabs)
            {
                if (tab != currentTab)
                {
                    _tabManager.CloseTab(tab);
                }
            }
        }

        private void DuplicateTabMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var currentTab = _tabManager.GetCurrentTab();
            var currentUrl = currentTab?.Browser?.Address;
            if (!string.IsNullOrEmpty(currentUrl))
            {
                var context = _requestContextManager.CreateIsolatedContext($"DuplicatedTab_{DateTime.Now.Ticks}");
                _tabManager.CreateTab("読み込み中...", currentUrl, context);
            }
        }

        private void NewWindowMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var newWindow = new MainWindow();
            newWindow.Show();
        }

        private void RestoreLastClosedTabMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // TODO: 閉じたタブの復元機能
        }

        #endregion

        #region ブラウザイベント

        private void Browser_IsBrowserInitializedChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue)
            {
                Dispatcher.Invoke(() =>
                {
                    UpdateButtonStates();

                    // ブラウザが初期化されたら、AutoPurchaseControlに最新のブラウザサービスを設定
                    AutoPurchaseControlPanel.SetBrowserServices(_tabManager, _requestContextManager);
                });
            }
        }

        private void Browser_AddressChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    UrlLineEdit.Text = e.NewValue?.ToString() ?? "";
                    UpdateButtonStates();
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Browser_AddressChanged error: {ex.Message}");
            }
        }

        private void Browser_LoadingStateChanged(object sender, CefSharp.LoadingStateChangedEventArgs e)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    WebProgressBar.Visibility = e.IsLoading ? Visibility.Visible : Visibility.Collapsed;
                    UpdateButtonStates();

                    // ページ読み込み完了時にAutoPurchaseControlのブラウザサービスを更新
                    if (!e.IsLoading)
                    {
                        AutoPurchaseControlPanel.SetBrowserServices(_tabManager, _requestContextManager);
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Browser_LoadingStateChanged error: {ex.Message}");
            }
        }

        private void TabManager_OnCurrentUrlChanged(string newUrl)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    UrlLineEdit.Text = newUrl ?? "";

                    // タブが変更されたら、AutoPurchaseControlに新しいブラウザサービスを設定
                    AutoPurchaseControlPanel.SetBrowserServices(_tabManager, _requestContextManager);
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"TabManager_OnCurrentUrlChanged error: {ex.Message}");
            }
        }

        #endregion

        #region ヘルパーメソッド

        /// <summary>
        /// URLに移動
        /// </summary>
        private void NavigateToUrl()
        {
            try
            {
                var url = UrlLineEdit.Text.Trim();
                if (string.IsNullOrEmpty(url)) return;

                // URLの正規化
                if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                {
                    url = "https://" + url;
                }

                var browser = _tabManager.GetCurrentBrowser();
                browser?.LoadUrl(url);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"NavigateToUrl error: {ex.Message}");
            }
        }

        /// <summary>
        /// 新しいタブを作成
        /// </summary>
        private void CreateNewTab()
        {
            try
            {
                var context = _requestContextManager.CreateIsolatedContext($"NewTab_{DateTime.Now.Ticks}");
                var tab = _tabManager.CreateTab("新しいタブ", "https://www.yahoo.co.jp/", context);

                if (tab != null)
                {
                    tab.Browser.IsBrowserInitializedChanged += Browser_IsBrowserInitializedChanged;
                    tab.Browser.AddressChanged += Browser_AddressChanged;
                    tab.Browser.LoadingStateChanged += Browser_LoadingStateChanged;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"CreateNewTab error: {ex.Message}");
            }
        }

        /// <summary>
        /// ボタンの状態を更新
        /// </summary>
        private void UpdateButtonStates()
        {
            try
            {
                var browser = _tabManager.GetCurrentBrowser();
                if (browser != null)
                {
                    PrevButton.IsEnabled = browser.CanGoBack;
                    NextButton.IsEnabled = browser.CanGoForward;
                    ReloadButton.IsEnabled = true;
                    GoButton.IsEnabled = true;
                }
                else
                {
                    PrevButton.IsEnabled = false;
                    NextButton.IsEnabled = false;
                    ReloadButton.IsEnabled = false;
                    GoButton.IsEnabled = false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"UpdateButtonStates error: {ex.Message}");
            }
        }

        #endregion

        #region リソースクリーンアップ

        protected override void OnClosed(EventArgs e)
        {
            try
            {
                // イベントハンドラーを解除
                if (_tabManager != null)
                {
                    _tabManager.OnCurrentUrlChanged -= TabManager_OnCurrentUrlChanged;
                }

                // AutoPurchaseControlのリソースを解放
                AutoPurchaseControlPanel?.Dispose();

                // リソースを解放
                _tabManager?.Dispose();
                _requestContextManager?.Dispose();

                base.OnClosed(e);
                Console.WriteLine("Integrated MainWindow disposed successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"OnClosed error: {ex.Message}");
                base.OnClosed(e);
            }
        }

        #endregion
    }
}