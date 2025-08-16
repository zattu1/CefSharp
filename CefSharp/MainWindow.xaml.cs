using CefSharp.fastBOT.Core;
using CefSharp.fastBOT.Models;
using CefSharp.fastBOT.Utils;
using CefSharp.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CefSharp.fastBOT
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック（修正版）
    /// 古いバージョンの安定した方式と新しいバージョンの機能を統合
    /// </summary>
    public partial class MainWindow : Window
    {
        // ブラウザ関連マネージャー
        private RequestContextManager _requestContextManager;
        private BrowserTabManager _tabManager;

        public MainWindow()
        {
            InitializeComponent();
            InitializeManagers();
            InitializeUI();
            CreateInitialTab();
        }

        #region 初期化

        /// <summary>
        /// AutoPurchaseControlとの連携を設定
        /// </summary>
        private void SetupAutoPurchaseControlIntegration()
        {
            try
            {
                if (AutoPurchaseControlPanel != null)
                {
                    // AutoPurchaseControlに親ウィンドウの参照を設定
                    AutoPurchaseControlPanel.ParentMainWindow = this;

                    // ブラウザサービスを設定
                    AutoPurchaseControlPanel.SetBrowserServices(_tabManager, _requestContextManager);

                    Console.WriteLine("AutoPurchaseControl integration setup completed");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SetupAutoPurchaseControlIntegration error: {ex.Message}");
            }
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

                // BrowserTabManagerからのURL変更イベントを購読
                _tabManager.OnCurrentUrlChanged += TabManager_OnCurrentUrlChanged;

                // タブコンテキストメニューの設定
                SetupTabContextMenu();

                // AutoPurchaseControlとの連携を設定
                SetupAutoPurchaseControlIntegration();

                Console.WriteLine("MainWindow managers initialized successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"InitializeManagers error: {ex.Message}");
            }
        }

        /// <summary>
        /// タブコンテキストメニューを設定
        /// </summary>
        private void SetupTabContextMenu()
        {
            var contextMenu = new ContextMenu();

            // 新しいタブメニュー
            var newTabItem = new MenuItem
            {
                Header = "新しいタブ(_N)",
                InputGestureText = "Ctrl+T"
            };
            newTabItem.Click += NewTabMenuItem_Click;
            contextMenu.Items.Add(newTabItem);

            // 区切り線
            contextMenu.Items.Add(new Separator());

            // タブを閉じるメニュー
            var closeTabItem = new MenuItem
            {
                Header = "タブを閉じる(_C)",
                InputGestureText = "Ctrl+W"
            };
            closeTabItem.Click += CloseTabMenuItem_Click;
            contextMenu.Items.Add(closeTabItem);

            // 他のタブを閉じるメニュー
            var closeOtherTabsItem = new MenuItem
            {
                Header = "他のタブを閉じる(_O)"
            };
            closeOtherTabsItem.Click += CloseOtherTabsMenuItem_Click;
            contextMenu.Items.Add(closeOtherTabsItem);

            // 区切り線
            contextMenu.Items.Add(new Separator());

            // タブを複製メニュー
            var duplicateTabItem = new MenuItem
            {
                Header = "タブを複製(_D)"
            };
            duplicateTabItem.Click += DuplicateTabMenuItem_Click;
            contextMenu.Items.Add(duplicateTabItem);

            // 新しいウィンドウで開くメニュー
            var newWindowItem = new MenuItem
            {
                Header = "新しいウィンドウで開く(_W)"
            };
            newWindowItem.Click += NewWindowMenuItem_Click;
            contextMenu.Items.Add(newWindowItem);

            // タブコントロールにコンテキストメニューを設定
            TabWidget.ContextMenu = contextMenu;

            // キーボードショートカットの設定
            SetupKeyboardShortcuts();
        }

        /// <summary>
        /// キーボードショートカットを設定
        /// </summary>
        private void SetupKeyboardShortcuts()
        {
            // Ctrl+T: 新しいタブ
            var newTabCommand = new RoutedCommand();
            newTabCommand.InputGestures.Add(new KeyGesture(Key.T, ModifierKeys.Control));
            this.CommandBindings.Add(new CommandBinding(newTabCommand, (s, e) => CreateNewTab()));

            // Ctrl+W: タブを閉じる
            var closeTabCommand = new RoutedCommand();
            closeTabCommand.InputGestures.Add(new KeyGesture(Key.W, ModifierKeys.Control));
            this.CommandBindings.Add(new CommandBinding(closeTabCommand, (s, e) => CloseCurrentTab()));

            // Ctrl+Shift+T: 最近閉じたタブを復元（将来の拡張用）
            var restoreTabCommand = new RoutedCommand();
            restoreTabCommand.InputGestures.Add(new KeyGesture(Key.T, ModifierKeys.Control | ModifierKeys.Shift));
            this.CommandBindings.Add(new CommandBinding(restoreTabCommand, (s, e) => RestoreLastClosedTab()));
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

                // ボタンの初期状態を設定
                UpdateButtonStates();

                // ステータスバー関連の初期化
                UpdateMainStatus("fastBOT initialized");
                UpdateAllStatusInfo();
                StartStatusBarTimer();
                SetupStatusBarContextMenu();

                UpdateStatus("fastBOT initialized");
                Console.WriteLine("UI initialized successfully");
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
                var context = _requestContextManager.CreateIsolatedContext("MainSession");
                var tab = _tabManager.CreateTab("読み込み中...", UrlLineEdit.Text, context);

                if (tab != null)
                {
                    // ブラウザイベントの設定
                    SetupBrowserEvents(tab.Browser);

                    // AutoPurchaseControlにブラウザサービスを設定
                    if (AutoPurchaseControlPanel != null)
                    {
                        AutoPurchaseControlPanel.SetBrowserServices(_tabManager, _requestContextManager);
                    }

                    // デフォルトのFaviconを設定（UIスレッドで実行）
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            var addressFaviconImage = this.FindName("AddressFaviconImage") as Image;
                            if (addressFaviconImage != null)
                            {
                                addressFaviconImage.Source = CreateDefaultAddressFavicon();
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Default favicon setting error: {ex.Message}");
                        }
                    });

                    Console.WriteLine("Initial tab created and events configured");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"CreateInitialTab error: {ex.Message}");
            }
        }

        /// <summary>
        /// ブラウザイベントを設定
        /// </summary>
        /// <param name="browser">対象ブラウザ</param>
        private void SetupBrowserEvents(ChromiumWebBrowser browser)
        {
            if (browser == null) return;

            // 既存のイベント設定...
            browser.IsBrowserInitializedChanged += Browser_IsBrowserInitializedChanged;
            browser.AddressChanged += Browser_AddressChanged;
            browser.TitleChanged += (sender, args) =>
            {
                Console.WriteLine($"Page title changed: {args.NewValue}");
            };

            // 読み込み状態変更イベント
            browser.LoadingStateChanged += (sender, args) =>
            {
                try
                {
                    Dispatcher.Invoke(() =>
                    {
                        UpdateNavigationButtonStates(args.CanGoBack, args.CanGoForward, args.CanReload);

                        if (!args.IsLoading)
                        {
                            ShowLogMessage("ページ読み込み完了", 2000);

                            // 少し遅延してFaviconを取得
                            Task.Delay(500).ContinueWith(_ =>
                            {
                                try
                                {
                                    Dispatcher.BeginInvoke(new Action(() =>
                                    {
                                        try
                                        {
                                            var currentUrl = browser?.Address;
                                            if (!string.IsNullOrEmpty(currentUrl))
                                            {
                                                var currentTab = _tabManager?.GetCurrentTab();
                                                if (currentTab?.Browser == browser)
                                                {
                                                    Console.WriteLine($"Loading address favicon for: {currentUrl}");
                                                    UpdateAddressFaviconFromTab(currentUrl);
                                                }
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            Console.WriteLine($"Favicon update error: {ex.Message}");
                                        }
                                    }), System.Windows.Threading.DispatcherPriority.Background);
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"Favicon task error: {ex.Message}");
                                }
                            }, TaskScheduler.Current);
                        }
                        else
                        {
                            ShowLogMessage("ページ読み込み中...", 1000);
                        }
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"LoadingStateChanged error: {ex.Message}");
                }
            };
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
                    UpdateRequestContextInfo();
                    Console.WriteLine("Browser initialized and button states updated");
                });
            }
        }

        private void Browser_AddressChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            try
            {
                // UIスレッドで安全に実行
                if (Application.Current.Dispatcher.CheckAccess())
                {
                    HandleAddressChanged(sender, e);
                }
                else
                {
                    Application.Current.Dispatcher.Invoke(() => HandleAddressChanged(sender, e));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Browser_AddressChanged error: {ex.Message}");
            }
        }

        private void HandleAddressChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            try
            {
                if (_tabManager?.GetCurrentBrowser() == sender)
                {
                    var newAddress = e.NewValue?.ToString();
                    if (!string.IsNullOrEmpty(newAddress))
                    {
                        // URLライン編集フィールドを直接更新（重複回避の為条件チェック）
                        if (UrlLineEdit.Text != newAddress)
                        {
                            UrlLineEdit.Text = newAddress;
                        }

                        // アドレス変更時にFaviconをリセット（安全に実行）
                        try
                        {
                            var addressFaviconImage = this.FindName("AddressFaviconImage") as Image;
                            if (addressFaviconImage != null)
                            {
                                addressFaviconImage.Source = CreateDefaultAddressFavicon();
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Favicon reset error: {ex.Message}");
                        }

                        // アドレス変更時にもボタン状態を更新
                        UpdateButtonStates();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"HandleAddressChanged error: {ex.Message}");
            }
        }

        private void TabManager_OnCurrentUrlChanged(string newUrl)
        {
            try
            {
                // UIスレッドで実行されることを確認
                if (Application.Current.Dispatcher.CheckAccess())
                {
                    UpdateUrlLineEdit(newUrl);
                    UpdateButtonStates(); // タブ切り替え時にもボタン状態を更新
                }
                else
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        UpdateUrlLineEdit(newUrl);
                        UpdateButtonStates();
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"URL change handling error: {ex.Message}");
            }
        }

        /// <summary>
        /// URLライン編集を更新
        /// </summary>
        /// <param name="newUrl">新しいURL</param>
        private void UpdateUrlLineEdit(string newUrl)
        {
            try
            {
                if (!string.IsNullOrEmpty(newUrl) && UrlLineEdit != null)
                {
                    // 現在のURL入力欄の値と異なる場合のみ更新
                    if (UrlLineEdit.Text != newUrl)
                    {
                        UrlLineEdit.Text = newUrl;
                        Console.WriteLine($"URL field updated to: {newUrl}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"URL field update error: {ex.Message}");
            }
        }

        #endregion

        #region ブラウザコントロール

        private void PrevButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var browser = _tabManager?.GetCurrentBrowser();
                if (browser?.CanGoBack == true)
                {
                    Console.WriteLine("Going back...");
                    browser.Back();
                    // LoadingStateChangedイベントで自動的にボタン状態が更新される
                }
                else
                {
                    Console.WriteLine($"Cannot go back. Browser: {browser != null}, CanGoBack: {browser?.CanGoBack}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"PrevButton_Click error: {ex.Message}");
            }
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var browser = _tabManager?.GetCurrentBrowser();
                if (browser?.CanGoForward == true)
                {
                    Console.WriteLine("Going forward...");
                    browser.Forward();
                    // LoadingStateChangedイベントで自動的にボタン状態が更新される
                }
                else
                {
                    Console.WriteLine($"Cannot go forward. Browser: {browser != null}, CanGoForward: {browser?.CanGoForward}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"NextButton_Click error: {ex.Message}");
            }
        }

        private void ReloadButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var browser = _tabManager?.GetCurrentBrowser();
                if (browser != null)
                {
                    Console.WriteLine("Reloading page...");
                    browser.Reload();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ReloadButton_Click error: {ex.Message}");
            }
        }

        private void TopButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var browser = _tabManager?.GetCurrentBrowser();
                if (browser != null)
                {
                    Console.WriteLine("Navigating to top page...");
                    browser.Address = "https://www.yahoo.co.jp/";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"TopButton_Click error: {ex.Message}");
            }
        }

        private void GoButton_Click(object sender, RoutedEventArgs e)
        {
            NavigateToUrl();
        }

        private void UrlLineEdit_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                NavigateToUrl();
            }
        }

        private void NavigateToUrl()
        {
            try
            {
                var browser = _tabManager?.GetCurrentBrowser();
                if (browser != null && !string.IsNullOrEmpty(UrlLineEdit.Text))
                {
                    string url = UrlLineEdit.Text;
                    if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                    {
                        url = "https://" + url;
                    }
                    Console.WriteLine($"Navigating to: {url}");
                    browser.Address = url;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"NavigateToUrl error: {ex.Message}");
            }
        }

        #endregion

        #region タブ管理メニューイベント

        /// <summary>
        /// 新しいタブメニュークリック
        /// </summary>
        private void NewTabMenuItem_Click(object sender, RoutedEventArgs e)
        {
            CreateNewTab();
        }

        /// <summary>
        /// タブを閉じるメニュークリック
        /// </summary>
        private void CloseTabMenuItem_Click(object sender, RoutedEventArgs e)
        {
            CloseCurrentTab();
        }

        /// <summary>
        /// 他のタブを閉じるメニュークリック
        /// </summary>
        private void CloseOtherTabsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            CloseOtherTabs();
        }

        /// <summary>
        /// タブを複製メニュークリック
        /// </summary>
        private void DuplicateTabMenuItem_Click(object sender, RoutedEventArgs e)
        {
            DuplicateCurrentTab();
        }

        /// <summary>
        /// 新しいウィンドウで開くメニュークリック
        /// </summary>
        private void NewWindowMenuItem_Click(object sender, RoutedEventArgs e)
        {
            OpenInNewWindow();
        }

        /// <summary>
        /// 新しいタブボタンクリック
        /// </summary>
        private void CreateNewTabButton_Click(object sender, RoutedEventArgs e)
        {
            CreateNewTab();
        }

        /// <summary>
        /// タブ復元メニュークリック
        /// </summary>
        private void RestoreLastClosedTabMenuItem_Click(object sender, RoutedEventArgs e)
        {
            RestoreLastClosedTab();
        }

        #endregion

        #region タブ操作メソッド

        /// <summary>
        /// 新しいタブを作成
        /// </summary>
        private void CreateNewTab()
        {
            try
            {
                var context = _requestContextManager.CreateIsolatedContext($"Tab_{DateTime.Now.Ticks}");
                var tab = _tabManager.CreateTab("新しいタブ", "about:blank", context);

                if (tab != null)
                {
                    // ブラウザイベントの設定
                    SetupBrowserEvents(tab.Browser);

                    // AutoPurchaseControlにブラウザサービスを更新
                    if (AutoPurchaseControlPanel != null)
                    {
                        AutoPurchaseControlPanel.SetBrowserServices(_tabManager, _requestContextManager);
                    }

                    // タブ数を更新
                    UpdateTabCount();
                    UpdateMainStatus("新しいタブを作成しました");
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"新しいタブの作成に失敗しました: {ex.Message}");
            }
        }

        /// <summary>
        /// 現在のタブを閉じる
        /// </summary>
        private void CloseCurrentTab()
        {
            try
            {
                var currentTab = _tabManager.GetCurrentTab();
                if (currentTab != null && _tabManager.TabCount > 1)
                {
                    _tabManager.CloseTab(currentTab);

                    // AutoPurchaseControlにブラウザサービスを更新
                    if (AutoPurchaseControlPanel != null)
                    {
                        AutoPurchaseControlPanel.SetBrowserServices(_tabManager, _requestContextManager);
                    }

                    // タブ数を更新
                    UpdateTabCount();
                    UpdateMainStatus("タブを閉じました");
                    UpdateButtonStates();
                }
                else if (_tabManager.TabCount == 1)
                {
                    ShowLogMessage("最後のタブは閉じることができません", 3000);
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"タブを閉じる際にエラーが発生しました: {ex.Message}");
            }
        }

        /// <summary>
        /// 他のタブを閉じる
        /// </summary>
        private void CloseOtherTabs()
        {
            try
            {
                var currentTab = _tabManager.GetCurrentTab();
                if (currentTab != null)
                {
                    var allTabs = _tabManager.GetAllTabs().ToList();
                    int closedCount = 0;

                    foreach (var tab in allTabs)
                    {
                        if (tab != currentTab)
                        {
                            _tabManager.CloseTab(tab);
                            closedCount++;
                        }
                    }

                    UpdateStatus($"{closedCount}個のタブを閉じました");
                    UpdateButtonStates();
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"タブを閉じる際にエラーが発生しました: {ex.Message}");
            }
        }

        /// <summary>
        /// 現在のタブを複製
        /// </summary>
        private void DuplicateCurrentTab()
        {
            try
            {
                var currentTab = _tabManager.GetCurrentTab();
                if (currentTab != null)
                {
                    var currentUrl = currentTab.Browser.Address;
                    var context = _requestContextManager.CreateIsolatedContext($"Duplicate_{DateTime.Now.Ticks}");
                    var newTab = _tabManager.CreateTab("複製されたタブ", currentUrl, context);

                    if (newTab != null)
                    {
                        // ブラウザイベントの設定
                        SetupBrowserEvents(newTab.Browser);

                        UpdateStatus("タブを複製しました");
                    }
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"タブの複製に失敗しました: {ex.Message}");
            }
        }

        /// <summary>
        /// 新しいウィンドウで開く
        /// </summary>
        private void OpenInNewWindow()
        {
            try
            {
                var currentTab = _tabManager.GetCurrentTab();
                if (currentTab != null)
                {
                    var currentUrl = currentTab.Browser.Address;

                    // 新しいMainWindowインスタンスを作成
                    var newWindow = new MainWindow();
                    newWindow.Show();

                    // 新しいウィンドウで指定URLを開く
                    newWindow.Loaded += (s, e) =>
                    {
                        newWindow.UrlLineEdit.Text = currentUrl;
                        newWindow.NavigateToUrl();
                    };

                    UpdateStatus("新しいウィンドウで開きました");
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"新しいウィンドウで開く際にエラーが発生しました: {ex.Message}");
            }
        }

        /// <summary>
        /// 最近閉じたタブを復元（将来の拡張用）
        /// </summary>
        private void RestoreLastClosedTab()
        {
            // TODO: 最近閉じたタブの履歴機能を実装
            UpdateStatus("タブ復元機能は未実装です");
        }

        #endregion

        #region UI更新

        /// <summary>
        /// ナビゲーションボタンの状態を更新（LoadingStateChangedイベント用）
        /// </summary>
        /// <param name="canGoBack">戻ることができるかどうか</param>
        /// <param name="canGoForward">進むことができるかどうか</param>
        /// <param name="canReload">再読込できるかどうか</param>
        private void UpdateNavigationButtonStates(bool canGoBack, bool canGoForward, bool canReload)
        {
            try
            {
                // デバッグ情報を出力
                Console.WriteLine($"UpdateNavigationButtonStates called - CanGoBack: {canGoBack}, CanGoForward: {canGoForward}, CanReload: {canReload}");

                // 各ボタンが存在するかチェックして更新
                var prevButton = this.FindName("PrevButton") as Button;
                var nextButton = this.FindName("NextButton") as Button;
                var reloadButton = this.FindName("ReloadButton") as Button;
                var goButton = this.FindName("GoButton") as Button;

                if (prevButton != null)
                {
                    prevButton.IsEnabled = canGoBack;
                    Console.WriteLine($"PrevButton enabled: {prevButton.IsEnabled}");
                }
                if (nextButton != null)
                {
                    nextButton.IsEnabled = canGoForward;
                    Console.WriteLine($"NextButton enabled: {nextButton.IsEnabled}");
                }
                if (reloadButton != null) reloadButton.IsEnabled = canReload;
                if (goButton != null) goButton.IsEnabled = true; // 移動ボタンは常に有効
            }
            catch (Exception ex)
            {
                Console.WriteLine($"UpdateNavigationButtonStates error: {ex.Message}");
            }
        }

        /// <summary>
        /// ボタンの状態を更新（従来の方法、フォールバック用）
        /// </summary>
        private void UpdateButtonStates()
        {
            try
            {
                var browser = _tabManager?.GetCurrentBrowser();

                // デバッグ情報を出力
                Console.WriteLine($"UpdateButtonStates called. Browser: {browser != null}");
                if (browser != null)
                {
                    Console.WriteLine($"  IsBrowserInitialized: {browser.IsBrowserInitialized}");
                    Console.WriteLine($"  CanGoBack: {browser.CanGoBack}");
                    Console.WriteLine($"  CanGoForward: {browser.CanGoForward}");
                    Console.WriteLine($"  Current Address: {browser.Address}");
                }

                if (browser != null && browser.IsBrowserInitialized)
                {
                    // LoadingStateChangedイベントが利用できない場合のフォールバック
                    UpdateNavigationButtonStates(browser.CanGoBack, browser.CanGoForward, true);
                }
                else
                {
                    // ブラウザがない場合は全てのボタンを無効化
                    var prevButton = this.FindName("PrevButton") as Button;
                    var nextButton = this.FindName("NextButton") as Button;
                    var reloadButton = this.FindName("ReloadButton") as Button;
                    var goButton = this.FindName("GoButton") as Button;

                    if (prevButton != null) prevButton.IsEnabled = false;
                    if (nextButton != null) nextButton.IsEnabled = false;
                    if (reloadButton != null) reloadButton.IsEnabled = false;
                    if (goButton != null) goButton.IsEnabled = false;

                    Console.WriteLine("All buttons disabled (browser not initialized)");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"UpdateButtonStates error: {ex.Message}");
            }
        }

        private void UpdateStatus(string message)
        {
            UpdateMainStatus(message);
        }

        private void UpdateRequestContextInfo()
        {
            try
            {
                var currentTab = _tabManager.GetCurrentTab();
                if (currentTab != null)
                {
                    // RequestContextInfoコントロールが存在する場合のみ更新
                    var requestContextInfo = this.FindName("RequestContextInfo") as TextBlock;
                    if (requestContextInfo != null)
                    {
                        requestContextInfo.Text = $"Context: {currentTab.ContextName}";
                    }
                    else
                    {
                        // RequestContextInfoが存在しない場合はコンソールに出力
                        Console.WriteLine($"RequestContext: {currentTab.ContextName}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"UpdateRequestContextInfo error: {ex.Message}");
            }
        }

        #endregion

        #region Favicon管理

        /// <summary>
        /// タブからアドレスバーのFaviconを更新（公開メソッド）
        /// </summary>
        /// <param name="faviconUrl">FaviconのURL</param>
        public void UpdateAddressFaviconFromTab(string faviconUrl)
        {
            // UIスレッドで実行されているかチェック
            if (Application.Current.Dispatcher.CheckAccess())
            {
                _ = Task.Run(() => UpdateAddressFaviconAsync(faviconUrl));
            }
            else
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    _ = Task.Run(() => UpdateAddressFaviconAsync(faviconUrl));
                });
            }
        }

        /// <summary>
        /// アドレスバーのFaviconを更新
        /// </summary>
        /// <param name="url">FaviconのURLまたはページURL</param>
        private async Task UpdateAddressFaviconAsync(string url)
        {
            try
            {
                // 直接FaviconのURLの場合とページURLの場合を両方試す
                var urlsToTry = new List<string>();

                if (url.Contains("favicon") || url.Contains("icon"))
                {
                    // 直接FaviconのURL
                    urlsToTry.Add(url);
                }
                else if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                {
                    // ページURLからFavicon URLを推測
                    urlsToTry.AddRange(new[]
                    {
                        $"{uri.Scheme}://{uri.Host}/favicon.ico",
                        $"{uri.Scheme}://{uri.Host}/apple-touch-icon.png",
                        $"{uri.Scheme}://{uri.Host}/favicon.png",
                        $"{uri.Scheme}://{uri.Host}/apple-touch-icon-precomposed.png"
                    });
                }

                bool faviconFound = false;
                foreach (var faviconUrl in urlsToTry)
                {
                    try
                    {
                        using var httpClient = new System.Net.Http.HttpClient();
                        httpClient.Timeout = TimeSpan.FromSeconds(3);
                        httpClient.DefaultRequestHeaders.Add("User-Agent",
                            UserAgentHelper.GetChromeUserAgent());

                        var imageData = await httpClient.GetByteArrayAsync(faviconUrl);

                        // UIスレッドで画像を作成・設定
                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            try
                            {
                                // メモリストリームからBitmapImageを作成
                                var bitmapImage = CreateBitmapImageFromBytes(imageData);

                                var addressFaviconImage = this.FindName("AddressFaviconImage") as Image;
                                if (bitmapImage != null && addressFaviconImage != null)
                                {
                                    addressFaviconImage.Source = bitmapImage;
                                    faviconFound = true;
                                    Console.WriteLine($"Address Favicon loaded: {faviconUrl}");
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Address Favicon image creation error: {ex.Message}");
                            }
                        });

                        if (faviconFound) break;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Address Favicon request error for {faviconUrl}: {ex.Message}");
                        continue;
                    }
                }

                if (!faviconFound)
                {
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        var addressFaviconImage = this.FindName("AddressFaviconImage") as Image;
                        if (addressFaviconImage != null)
                        {
                            addressFaviconImage.Source = CreateDefaultAddressFavicon();
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"アドレスバーFavicon更新エラー: {ex.Message}");
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    var addressFaviconImage = this.FindName("AddressFaviconImage") as Image;
                    if (addressFaviconImage != null)
                    {
                        addressFaviconImage.Source = CreateDefaultAddressFavicon();
                    }
                });
            }
        }

        /// <summary>
        /// バイト配列からBitmapImageを作成（スレッドセーフ）
        /// </summary>
        /// <param name="imageData">画像データ</param>
        /// <returns>BitmapImage</returns>
        private BitmapImage CreateBitmapImageFromBytes(byte[] imageData)
        {
            try
            {
                // UIスレッドでのみ実行
                if (!Application.Current.Dispatcher.CheckAccess())
                {
                    return Application.Current.Dispatcher.Invoke(() => CreateBitmapImageFromBytes(imageData));
                }

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

                return bitmapImage;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"CreateBitmapImageFromBytes error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// アドレスバー用のデフォルトFaviconを作成
        /// </summary>
        /// <returns>デフォルトFaviconのImageSource</returns>
        private ImageSource CreateDefaultAddressFavicon()
        {
            try
            {
                // UIスレッドでのみ実行
                if (!Application.Current.Dispatcher.CheckAccess())
                {
                    return Application.Current.Dispatcher.Invoke(() => CreateDefaultAddressFavicon());
                }

                // まずfastBOT.icoリソースから読み込みを試行
                var resourceFavicon = LoadAddressFaviconFromResource();
                if (resourceFavicon != null)
                {
                    return resourceFavicon;
                }

                // リソースが読み込めない場合はフォールバック用のアイコンを描画
                return CreateFallbackAddressFavicon();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"CreateDefaultAddressFavicon error: {ex.Message}");
                return CreateFallbackAddressFavicon();
            }
        }

        /// <summary>
        /// 埋め込みリソースからfastBOT.icoを読み込み（アドレスバー用）
        /// </summary>
        /// <returns>fastBOT.icoのImageSource</returns>
        private ImageSource LoadAddressFaviconFromResource()
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

                Console.WriteLine("Address bar fastBOT.ico loaded from resources");
                return bitmapImage;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load address bar fastBOT.ico from resources: {ex.Message}");

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

                        Console.WriteLine("Address bar fastBOT.ico loaded from embedded resources");
                        return bitmapImage;
                    }
                }
                catch (Exception embeddedEx)
                {
                    Console.WriteLine($"Failed to load embedded address bar fastBOT.ico: {embeddedEx.Message}");
                }

                return null;
            }
        }

        /// <summary>
        /// フォールバック用のアドレスバーアイコンを描画
        /// </summary>
        /// <returns>描画されたアイコンのImageSource</returns>
        private ImageSource CreateFallbackAddressFavicon()
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
                Console.WriteLine($"CreateFallbackAddressFavicon error: {ex.Message}");
                return null;
            }
        }

        #endregion

        #region JavaScript実行機能（MainWindow）

        /// <summary>
        /// 現在のタブでJavaScriptを実行（非同期）
        /// </summary>
        /// <param name="script">実行するJavaScriptコード</param>
        /// <param name="callback">実行完了時のコールバック</param>
        /// <param name="timeoutSeconds">タイムアウト秒数（デフォルト30秒）</param>
        public void ExecuteJavaScript(string script, BrowserTabManager.JavaScriptCallback callback = null, int timeoutSeconds = 30)
        {
            try
            {
                if (_tabManager == null)
                {
                    var errorResult = new BrowserTabManager.JavaScriptResult
                    {
                        Success = false,
                        ErrorMessage = "タブマネージャーが初期化されていません",
                        Script = script,
                        ExecutedAt = DateTime.Now
                    };

                    callback?.Invoke(errorResult);
                    return;
                }

                _tabManager.ExecuteJavaScriptAsync(script, callback, timeoutSeconds);
            }
            catch (Exception ex)
            {
                var errorResult = new BrowserTabManager.JavaScriptResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    Script = script,
                    ExecutedAt = DateTime.Now
                };

                callback?.Invoke(errorResult);
                Console.WriteLine($"MainWindow ExecuteJavaScript error: {ex.Message}");
            }
        }

        /// <summary>
        /// 現在のタブでJavaScriptを実行（同期）
        /// </summary>
        /// <param name="script">実行するJavaScriptコード</param>
        /// <param name="timeoutSeconds">タイムアウト秒数（デフォルト30秒）</param>
        /// <returns>実行結果</returns>
        public async Task<BrowserTabManager.JavaScriptResult> ExecuteJavaScriptSync(string script, int timeoutSeconds = 30)
        {
            try
            {
                if (_tabManager == null)
                {
                    return new BrowserTabManager.JavaScriptResult
                    {
                        Success = false,
                        ErrorMessage = "タブマネージャーが初期化されていません",
                        Script = script,
                        ExecutedAt = DateTime.Now
                    };
                }

                return await _tabManager.ExecuteJavaScriptSync(script, timeoutSeconds);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"MainWindow ExecuteJavaScriptSync error: {ex.Message}");
                return new BrowserTabManager.JavaScriptResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    Script = script,
                    ExecutedAt = DateTime.Now
                };
            }
        }

        #endregion

        #region インスタンス管理機能

        /// <summary>
        /// 全インスタンスの情報を取得
        /// </summary>
        /// <returns>インスタンス情報のリスト</returns>
        public List<InstanceInfo> GetAllInstancesInfo()
        {
            return RequestContextManager.GetAllInstancesInfo();
        }

        /// <summary>
        /// 現在のインスタンス番号を取得
        /// </summary>
        /// <returns>インスタンス番号</returns>
        public int GetCurrentInstanceNumber()
        {
            return _requestContextManager?.GetInstanceNumber() ?? 0;
        }

        /// <summary>
        /// 現在のインスタンスのキャッシュサイズを取得
        /// </summary>
        /// <returns>キャッシュサイズ（バイト）</returns>
        public long GetCurrentInstanceCacheSize()
        {
            return _requestContextManager?.GetCacheSize() ?? 0;
        }

        /// <summary>
        /// 現在のインスタンスのキャッシュをクリア
        /// </summary>
        /// <param name="contextName">特定のコンテキスト名（nullの場合は全体）</param>
        /// <returns>成功した場合true</returns>
        public bool ClearCurrentInstanceCache(string contextName = null)
        {
            try
            {
                var result = _requestContextManager?.ClearCache(contextName) ?? false;
                if (result)
                {
                    var message = string.IsNullOrEmpty(contextName)
                        ? "全キャッシュをクリアしました"
                        : $"コンテキスト '{contextName}' のキャッシュをクリアしました";
                    UpdateStatus(message);
                }
                return result;
            }
            catch (Exception ex)
            {
                UpdateStatus($"キャッシュクリアに失敗しました: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// インスタンス管理情報をコンソールに出力
        /// </summary>
        public void ShowInstanceManagementInfo()
        {
            try
            {
                Console.WriteLine("=== Instance Management Info ===");
                Console.WriteLine($"Current Instance: {GetCurrentInstanceNumber()}");
                Console.WriteLine($"Current Cache Size: {FormatBytes(GetCurrentInstanceCacheSize())}");
                Console.WriteLine($"Current Cache Path: {_requestContextManager?.GetBaseCachePath()}");

                Console.WriteLine("\nAll Instances:");
                var instances = GetAllInstancesInfo();
                foreach (var instance in instances)
                {
                    Console.WriteLine($"  {instance}");
                }
                Console.WriteLine("================================");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ShowInstanceManagementInfo error: {ex.Message}");
            }
        }

        /// <summary>
        /// バイト数を人間が読みやすい形式にフォーマット
        /// </summary>
        /// <param name="bytes">バイト数</param>
        /// <returns>フォーマットされた文字列</returns>
        private string FormatBytes(long bytes)
        {
            if (bytes < 1024)
                return $"{bytes} B";
            else if (bytes < 1024 * 1024)
                return $"{bytes / 1024.0:F1} KB";
            else if (bytes < 1024 * 1024 * 1024)
                return $"{bytes / (1024.0 * 1024.0):F1} MB";
            else
                return $"{bytes / (1024.0 * 1024.0 * 1024.0):F1} GB";
        }

        /// <summary>
        /// インスタンス管理ウィンドウを表示（将来の拡張用）
        /// </summary>
        public void ShowInstanceManagementWindow()
        {
            try
            {
                // TODO: インスタンス管理専用ウィンドウを実装
                ShowInstanceManagementInfo();
                UpdateStatus("インスタンス管理情報をコンソールに出力しました");
            }
            catch (Exception ex)
            {
                UpdateStatus($"インスタンス管理ウィンドウの表示に失敗しました: {ex.Message}");
            }
        }

        #endregion

        #region ステータスバー管理

        /// <summary>
        /// メインステータスメッセージを更新
        /// </summary>
        /// <param name="message">表示するメッセージ</param>
        /// <param name="showTimestamp">タイムスタンプを表示するかどうか</param>
        public void UpdateMainStatus(string message, bool showTimestamp = true)
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var mainStatusText = this.FindName("MainStatusText") as TextBlock;
                    var timeStampText = this.FindName("TimeStampText") as TextBlock;

                    if (mainStatusText != null)
                    {
                        mainStatusText.Text = message;
                    }

                    if (timeStampText != null && showTimestamp)
                    {
                        timeStampText.Text = DateTime.Now.ToString("HH:mm:ss");
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"UpdateMainStatus error: {ex.Message}");
            }
        }

        /// <summary>
        /// インスタンス番号を更新
        /// </summary>
        private void UpdateInstanceNumber()
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var instanceNumberText = this.FindName("InstanceNumberText") as TextBlock;

                    if (instanceNumberText != null)
                    {
                        var instanceNumber = _requestContextManager?.GetInstanceNumber() ?? 0;
                        instanceNumberText.Text = instanceNumber.ToString();
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"UpdateInstanceNumber error: {ex.Message}");
            }
        }

        /// <summary>
        /// キャッシュサイズを更新
        /// </summary>
        private void UpdateCacheSize()
        {
            try
            {
                Task.Run(async () =>
                {
                    try
                    {
                        var cacheSize = _requestContextManager?.GetCacheSize() ?? 0;
                        var formattedSize = FormatBytes(cacheSize);

                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            var cacheSizeText = this.FindName("CacheSizeText") as TextBlock;
                            if (cacheSizeText != null)
                            {
                                cacheSizeText.Text = formattedSize;
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"UpdateCacheSize background task error: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"UpdateCacheSize error: {ex.Message}");
            }
        }

        /// <summary>
        /// タブ数を更新
        /// </summary>
        private void UpdateTabCount()
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var tabCountText = this.FindName("TabCountText") as TextBlock;

                    if (tabCountText != null)
                    {
                        var tabCount = _tabManager?.TabCount ?? 0;
                        tabCountText.Text = tabCount.ToString();
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"UpdateTabCount error: {ex.Message}");
            }
        }

        /// <summary>
        /// 全ステータス情報を更新
        /// </summary>
        public void UpdateAllStatusInfo()
        {
            try
            {
                UpdateRequestContextInfo();
                UpdateInstanceNumber();
                UpdateCacheSize();
                UpdateTabCount();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"UpdateAllStatusInfo error: {ex.Message}");
            }
        }

        /// <summary>
        /// ステータスバーの定期更新を開始
        /// </summary>
        private void StartStatusBarTimer()
        {
            try
            {
                var statusTimer = new System.Windows.Threading.DispatcherTimer();
                statusTimer.Interval = TimeSpan.FromSeconds(5); // 5秒間隔で更新
                statusTimer.Tick += (s, e) =>
                {
                    try
                    {
                        UpdateCacheSize();
                        UpdateTabCount();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Status timer tick error: {ex.Message}");
                    }
                };
                statusTimer.Start();

                Console.WriteLine("Status bar timer started");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"StartStatusBarTimer error: {ex.Message}");
            }
        }

        /// <summary>
        /// ログメッセージをステータスバーに表示（一時的表示）
        /// </summary>
        /// <param name="message">ログメッセージ</param>
        /// <param name="duration">表示時間（ミリ秒）</param>
        public void ShowLogMessage(string message, int duration = 3000)
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var originalMessage = (this.FindName("MainStatusText") as TextBlock)?.Text ?? "Ready";

                    // 一時的にログメッセージを表示
                    UpdateMainStatus(message, true);

                    // 指定時間後に元のメッセージに戻す
                    var timer = new System.Windows.Threading.DispatcherTimer();
                    timer.Interval = TimeSpan.FromMilliseconds(duration);
                    timer.Tick += (s, e) =>
                    {
                        timer.Stop();
                        UpdateMainStatus(originalMessage, false);
                    };
                    timer.Start();
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ShowLogMessage error: {ex.Message}");
            }
        }

        /// <summary>
        /// エラーメッセージをステータスバーに表示
        /// </summary>
        /// <param name="errorMessage">エラーメッセージ</param>
        public void ShowErrorMessage(string errorMessage)
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var mainStatusText = this.FindName("MainStatusText") as TextBlock;

                    if (mainStatusText != null)
                    {
                        // エラーメッセージは赤色で表示
                        mainStatusText.Foreground = Brushes.Red;
                        mainStatusText.Text = $"エラー: {errorMessage}";

                        var timeStampText = this.FindName("TimeStampText") as TextBlock;
                        if (timeStampText != null)
                        {
                            timeStampText.Text = DateTime.Now.ToString("HH:mm:ss");
                        }

                        // 5秒後に色を元に戻す
                        var timer = new System.Windows.Threading.DispatcherTimer();
                        timer.Interval = TimeSpan.FromSeconds(5);
                        timer.Tick += (s, e) =>
                        {
                            timer.Stop();
                            if (mainStatusText != null)
                            {
                                mainStatusText.Foreground = Brushes.Black;
                            }
                        };
                        timer.Start();
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ShowErrorMessage error: {ex.Message}");
            }
        }

        /// <summary>
        /// 成功メッセージをステータスバーに表示
        /// </summary>
        /// <param name="successMessage">成功メッセージ</param>
        public void ShowSuccessMessage(string successMessage)
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var mainStatusText = this.FindName("MainStatusText") as TextBlock;

                    if (mainStatusText != null)
                    {
                        // 成功メッセージは緑色で表示
                        mainStatusText.Foreground = Brushes.Green;
                        mainStatusText.Text = successMessage;

                        var timeStampText = this.FindName("TimeStampText") as TextBlock;
                        if (timeStampText != null)
                        {
                            timeStampText.Text = DateTime.Now.ToString("HH:mm:ss");
                        }

                        // 3秒後に色を元に戻す
                        var timer = new System.Windows.Threading.DispatcherTimer();
                        timer.Interval = TimeSpan.FromSeconds(3);
                        timer.Tick += (s, e) =>
                        {
                            timer.Stop();
                            if (mainStatusText != null)
                            {
                                mainStatusText.Foreground = Brushes.Black;
                            }
                        };
                        timer.Start();
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ShowSuccessMessage error: {ex.Message}");
            }
        }

        /// <summary>
        /// ステータスバーのコンテキストメニューを設定
        /// </summary>
        private void SetupStatusBarContextMenu()
        {
            try
            {
                var statusBar = this.FindName("StatusBar") as StatusBar;
                if (statusBar == null) return;

                var contextMenu = new ContextMenu();

                // キャッシュクリアメニュー
                var clearCacheItem = new MenuItem
                {
                    Header = "キャッシュをクリア(_C)",
                    Icon = new TextBlock { Text = "🗑", FontSize = 12 }
                };
                clearCacheItem.Click += (s, e) =>
                {
                    try
                    {
                        var result = ClearCurrentInstanceCache();
                        if (result)
                        {
                            ShowSuccessMessage("キャッシュをクリアしました");
                            UpdateCacheSize();
                        }
                        else
                        {
                            ShowErrorMessage("キャッシュクリアに失敗しました");
                        }
                    }
                    catch (Exception ex)
                    {
                        ShowErrorMessage($"キャッシュクリアエラー: {ex.Message}");
                    }
                };
                contextMenu.Items.Add(clearCacheItem);

                // インスタンス管理情報表示メニュー
                var showInstanceInfoItem = new MenuItem
                {
                    Header = "インスタンス情報を表示(_I)",
                    Icon = new TextBlock { Text = "ℹ", FontSize = 12 }
                };
                showInstanceInfoItem.Click += (s, e) => ShowInstanceManagementInfo();
                contextMenu.Items.Add(showInstanceInfoItem);

                contextMenu.Items.Add(new Separator());

                // ステータス更新メニュー
                var refreshStatusItem = new MenuItem
                {
                    Header = "ステータス更新(_R)",
                    Icon = new TextBlock { Text = "🔄", FontSize = 12 }
                };
                refreshStatusItem.Click += (s, e) => UpdateAllStatusInfo();
                contextMenu.Items.Add(refreshStatusItem);

                statusBar.ContextMenu = contextMenu;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SetupStatusBarContextMenu error: {ex.Message}");
            }
        }

#endregion

        #region Window終了処理

        protected override void OnClosed(EventArgs e)
        {
            try
            {
                // イベントハンドラーを解除
                if (_tabManager != null)
                {
                    _tabManager.OnCurrentUrlChanged -= TabManager_OnCurrentUrlChanged;
                }

                // リソースを解放
                _tabManager?.Dispose();
                _requestContextManager?.Dispose();

                base.OnClosed(e);
                Console.WriteLine("MainWindow disposed successfully");
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