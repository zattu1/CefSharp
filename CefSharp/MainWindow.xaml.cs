using CefSharp.fastBOT.Core;
using CefSharp.fastBOT.Models;
using CefSharp.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
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
        private UserSettings _userSettings;

        public MainWindow()
        {
            InitializeComponent();
            InitializeManagers();
            InitializeUI();
            CreateInitialTab();
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

                // BrowserTabManagerからのURL変更イベントを購読
                _tabManager.OnCurrentUrlChanged += TabManager_OnCurrentUrlChanged;

                // タブコンテキストメニューの設定
                SetupTabContextMenu();

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
                    tab.Browser.IsBrowserInitializedChanged += Browser_IsBrowserInitializedChanged;
                    tab.Browser.AddressChanged += Browser_AddressChanged;

                    // タイトル変更イベントの追加設定
                    tab.Browser.TitleChanged += (sender, args) =>
                    {
                        Console.WriteLine($"Page title changed: {args.NewValue}");
                    };

                    // LoadingStateChangedイベントでFaviconの再取得を確実に行う
                    tab.Browser.LoadingStateChanged += (sender, args) =>
                    {
                        try
                        {
                            if (!args.IsLoading)
                            {
                                // 読み込み完了後、少し遅延してFaviconを取得（重複を避けるため条件付き）
                                Task.Delay(2000).ContinueWith(_ =>
                                {
                                    try
                                    {
                                        // UIスレッドで安全に実行
                                        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                                        {
                                            try
                                            {
                                                var currentUrl = tab.Browser?.Address;
                                                if (!string.IsNullOrEmpty(currentUrl) && tab.Browser != null)
                                                {
                                                    // 現在のタブかどうかを安全にチェック
                                                    var currentTab = _tabManager?.GetCurrentTab();
                                                    if (currentTab?.Browser == tab.Browser)
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
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"LoadingStateChanged error: {ex.Message}");
                        }
                    };

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

        #region ブラウザイベント

        private void Browser_IsBrowserInitializedChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue)
            {
                Dispatcher.Invoke(() =>
                {
                    UpdateButtonStates();
                    UpdateRequestContextInfo();
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
                }
                else
                {
                    Application.Current.Dispatcher.Invoke(() => UpdateUrlLineEdit(newUrl));
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

        #region ブラウザコントロール

        private void PrevButton_Click(object sender, RoutedEventArgs e)
        {
            var browser = _tabManager.GetCurrentBrowser();
            if (browser?.CanGoBack == true)
            {
                browser.Back();
            }
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            var browser = _tabManager.GetCurrentBrowser();
            if (browser?.CanGoForward == true)
            {
                browser.Forward();
            }
        }

        private void ReloadButton_Click(object sender, RoutedEventArgs e)
        {
            var browser = _tabManager.GetCurrentBrowser();
            browser?.Reload();
        }

        private void TopButton_Click(object sender, RoutedEventArgs e)
        {
            var browser = _tabManager.GetCurrentBrowser();
            if (browser != null)
            {
                browser.Address = "https://www.yahoo.co.jp/";
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
            var browser = _tabManager.GetCurrentBrowser();
            if (browser != null && !string.IsNullOrEmpty(UrlLineEdit.Text))
            {
                string url = UrlLineEdit.Text;
                if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                {
                    url = "https://" + url;
                }
                browser.Address = url;
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
                    tab.Browser.IsBrowserInitializedChanged += Browser_IsBrowserInitializedChanged;
                    tab.Browser.AddressChanged += Browser_AddressChanged;

                    UpdateStatus("新しいタブを作成しました");
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"新しいタブの作成に失敗しました: {ex.Message}");
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
                if (currentTab != null && _tabManager.TabCount > 1) // 最後のタブは閉じない
                {
                    _tabManager.CloseTab(currentTab);
                    UpdateStatus("タブを閉じました");
                }
                else if (_tabManager.TabCount == 1)
                {
                    UpdateStatus("最後のタブは閉じることができません");
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"タブを閉じる際にエラーが発生しました: {ex.Message}");
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
                        newTab.Browser.IsBrowserInitializedChanged += Browser_IsBrowserInitializedChanged;
                        newTab.Browser.AddressChanged += Browser_AddressChanged;

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
                            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/138.0.0.0 Safari/537.36");

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

        #region UI更新

        private void UpdateStatus(string message)
        {
            try
            {
                // StatusTextコントロールが存在する場合のみ更新
                var statusText = this.FindName("StatusText") as TextBlock;
                if (statusText != null)
                {
                    statusText.Text = $"{DateTime.Now:HH:mm:ss} - {message}";
                }
                else
                {
                    // StatusTextが存在しない場合はコンソールに出力
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"UpdateStatus error: {ex.Message}");
            }
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
                    // 各ボタンが存在するかチェックして更新
                    var prevButton = this.FindName("PrevButton") as Button;
                    var nextButton = this.FindName("NextButton") as Button;
                    var reloadButton = this.FindName("ReloadButton") as Button;
                    var goButton = this.FindName("GoButton") as Button;

                    if (prevButton != null) prevButton.IsEnabled = browser.CanGoBack;
                    if (nextButton != null) nextButton.IsEnabled = browser.CanGoForward;
                    if (reloadButton != null) reloadButton.IsEnabled = true;
                    if (goButton != null) goButton.IsEnabled = true;
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
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"UpdateButtonStates error: {ex.Message}");
            }
        }

        #endregion

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
    }
}