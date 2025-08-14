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
    public partial class MainWindow : Window
    {
        private ProxyManager _proxyManager;
        private RequestContextManager _requestContextManager;
        private BrowserTabManager _tabManager;
        private UserSettings _userSettings;
        private AutomationService _automationService;
        private JavaScriptHelpers _jsHelpers;
        private System.Timers.Timer _proxyRotationTimer;

        public MainWindow()
        {
            InitializeComponent();
            InitializeManagers();
            InitializeUI();
            CreateInitialTab();
        }

        // InitializeManagersメソッド内で初期化
        private void InitializeManagers()
        {
            _proxyManager = new ProxyManager();
            _requestContextManager = new RequestContextManager();
            _tabManager = new BrowserTabManager(TabWidget);
            _userSettings = new UserSettings();
            _jsHelpers = new JavaScriptHelpers(this);

            // BrowserTabManagerからのURL変更イベントを購読
            _tabManager.OnCurrentUrlChanged += TabManager_OnCurrentUrlChanged;

            // Proxyローテーションタイマー
            _proxyRotationTimer = new System.Timers.Timer();
            _proxyRotationTimer.Elapsed += ProxyRotationTimer_Elapsed;
        }

        /// <summary>
        /// BrowserTabManagerからのURL変更通知を処理
        /// </summary>
        /// <param name="newUrl">新しいURL</param>
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
        /// URLラインエディットを更新
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

        private void InitializeUI()
        {
            // 月のコンボボックス初期化
            for (int i = 1; i <= 12; i++)
            {
                MonthComboBox.Items.Add(i.ToString());
            }
            MonthComboBox.SelectedIndex = 0;

            // 年のコンボボックス初期化
            for (int year = 2025; year <= 2035; year++)
            {
                YearComboBox.Items.Add(year.ToString());
            }
            YearComboBox.SelectedIndex = 0;

            // デフォルトURL設定
            UrlLineEdit.Text = "https://www.yahoo.co.jp/";

            // タブコントロールの右クリックメニュー設定
            SetupTabContextMenu();

            UpdateStatus("fastBOT initialized");
        }

        /// <summary>
        /// タブコントロールの右クリックメニューを設定
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

        private void CreateInitialTab()
        {
            var context = _requestContextManager.CreateIsolatedContext("MainSession");
            var tab = _tabManager.CreateTab("読み込み中...", UrlLineEdit.Text, context);

            if (tab != null)
            {
                // AutomationServiceを初期化
                _automationService = new AutomationService(tab.Browser);

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
                            // 読み込み完了後、少し遅延してFaviconを取得
                            Task.Delay(1000).ContinueWith(_ =>
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
                                                Console.WriteLine($"Attempting to load favicon for: {currentUrl}");

                                                // 現在のタブかどうかを安全にチェック
                                                var currentTab = _tabManager?.GetCurrentTab();
                                                if (currentTab?.Browser == tab.Browser)
                                                {
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
                        AddressFaviconImage.Source = CreateDefaultAddressFavicon();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Default favicon setting error: {ex.Message}");
                    }
                });

                Console.WriteLine("Initial tab created and events configured");
            }
        }

        #region ブラウザイベント

        private void Browser_IsBrowserInitializedChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue)
            {
                Dispatcher.Invoke(() =>
                {
                    StartButton.IsEnabled = true;
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
                        // URLライン編集フィールドを直接更新（重複回避のため条件チェック）
                        if (UrlLineEdit.Text != newAddress)
                        {
                            UrlLineEdit.Text = newAddress;
                        }

                        // アドレス変更時にFaviconをリセット（安全に実行）
                        try
                        {
                            if (AddressFaviconImage != null)
                            {
                                AddressFaviconImage.Source = CreateDefaultAddressFavicon();
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

        #region Proxy管理

        private void ProxyLineEdit_TextChanged(object sender, TextChangedEventArgs e)
        {
            // リアルタイムでProxy設定を更新（オプション）
        }

        private async void ApplyProxyButton_Click(object sender, RoutedEventArgs e)
        {
            await ApplyProxySettings();
        }

        private void ProxyRotationCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            SetProxyRotationEnabled(true);
        }

        private void ProxyRotationCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            SetProxyRotationEnabled(false);
        }

        private async Task ApplyProxySettings()
        {
            try
            {
                string proxyText = ProxyLineEdit.Text.Trim();

                if (string.IsNullOrEmpty(proxyText))
                {
                    // Proxyを無効化
                    await _proxyManager.DisableProxyAsync(_tabManager.GetCurrentBrowser());
                    UpdateProxyStatus("Proxy無効");
                    return;
                }

                var proxyConfig = ParseProxyText(proxyText);
                if (proxyConfig != null)
                {
                    bool success = await _proxyManager.SetProxyAsync(
                        _tabManager.GetCurrentBrowser(), proxyConfig);

                    if (success)
                    {
                        UpdateProxyStatus($"Proxy: {proxyConfig.Host}:{proxyConfig.Port}");
                        UpdateStatus("Proxy設定を適用しました");
                    }
                    else
                    {
                        UpdateStatus("Proxy設定に失敗しました");
                    }
                }
                else
                {
                    UpdateStatus("Proxy形式が不正です (例: 127.0.0.1:8080:user:pass)");
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Proxy設定エラー: {ex.Message}");
            }
        }

        private ProxyConfig ParseProxyText(string proxyText)
        {
            try
            {
                var parts = proxyText.Split(':');
                if (parts.Length < 2) return null;

                var config = new ProxyConfig
                {
                    Host = parts[0],
                    Port = int.Parse(parts[1])
                };

                if (parts.Length >= 4)
                {
                    config.Username = parts[2];
                    config.Password = parts[3];
                }

                return config;
            }
            catch
            {
                return null;
            }
        }

        private void SetProxyRotationEnabled(bool enabled)
        {
            PerRequestRadioButton.IsEnabled = enabled;
            EverySecondRadioButton.IsEnabled = enabled;
            ProxyEverySecondLineEdit.IsEnabled = enabled;

            if (enabled && EverySecondRadioButton.IsChecked == true)
            {
                StartProxyRotationTimer();
            }
            else
            {
                StopProxyRotationTimer();
            }
        }

        private void StartProxyRotationTimer()
        {
            if (int.TryParse(ProxyEverySecondLineEdit.Text, out int seconds))
            {
                _proxyRotationTimer.Interval = seconds * 1000;
                _proxyRotationTimer.Start();
                UpdateStatus($"Proxyローテーション開始: {seconds}秒間隔");
            }
        }

        private void StopProxyRotationTimer()
        {
            _proxyRotationTimer.Stop();
            UpdateStatus("Proxyローテーション停止");
        }

        private async void ProxyRotationTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            await Dispatcher.InvokeAsync(async () =>
            {
                // TODO: Proxyリストからランダムに選択して設定
                await ApplyProxySettings();
            });
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

                                if (bitmapImage != null && AddressFaviconImage != null)
                                {
                                    AddressFaviconImage.Source = bitmapImage;
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
                        if (AddressFaviconImage != null)
                        {
                            AddressFaviconImage.Source = CreateDefaultAddressFavicon();
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"アドレスバーFavicon更新エラー: {ex.Message}");
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (AddressFaviconImage != null)
                    {
                        AddressFaviconImage.Source = CreateDefaultAddressFavicon();
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

        #region 自動化機能の実装例

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            if (_automationService == null) return;

            try
            {
                UpdateStatus("自動ログインを開始します...");

                var loginId = LoginEdit.Text;
                var password = PasswordEdit.Password;

                if (string.IsNullOrEmpty(loginId) || string.IsNullOrEmpty(password))
                {
                    UpdateStatus("ログインIDとパスワードを入力してください");
                    return;
                }

                // 自動ログイン実行
                bool success = await _automationService.AutoLoginAsync(loginId, password);

                if (success)
                {
                    UpdateStatus("ログインを実行しました");
                    ProgressBar.Value = 3;
                }
                else
                {
                    UpdateStatus("ログインに失敗しました");
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"ログインエラー: {ex.Message}");
            }
        }

        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (_automationService == null) return;

            try
            {
                StartButton.IsEnabled = false;
                UpdateStatus("自動購入を開始します...");
                ProgressBar.Value = 1;

                // 1. ページ読み込み待機
                UpdateStatus("ページ読み込み待機中...");
                await _automationService.WaitForPageLoadAsync(30);
                ProgressBar.Value = 2;

                // 2. ログイン情報の自動入力
                if (!string.IsNullOrEmpty(LoginEdit.Text) && !string.IsNullOrEmpty(PasswordEdit.Password))
                {
                    UpdateStatus("ログイン中...");
                    await _automationService.AutoLoginAsync(LoginEdit.Text, PasswordEdit.Password);
                    ProgressBar.Value = 4;
                }

                // 3. チケット選択画面まで遷移
                UpdateStatus("チケット選択画面へ移動中...");
                await Task.Delay(2000);
                ProgressBar.Value = 6;

                // 4. チケット自動選択（イベントリストから選択されたもの）
                var selectedEvents = GetSelectedEvents();
                if (selectedEvents.Count > 0)
                {
                    UpdateStatus("チケットを選択中...");

                    var ticketSelectors = new List<string>
                    {
                        ".ticket-select-btn",
                        ".seat-select",
                        $".ticket-count option[value='{NumComboBox.Text}']"
                    };

                    await _automationService.AutoPurchaseTicketsAsync(ticketSelectors);
                    ProgressBar.Value = 8;
                }

                // 5. 購入者情報入力
                UpdateStatus("購入者情報を入力中...");
                await FillPurchaserInfo();
                ProgressBar.Value = 10;

                // 6. 最終確認
                UpdateStatus("購入処理完了待機中...");
                await _automationService.WaitForElementAsync(".purchase-complete, .order-complete", 60);
                ProgressBar.Value = 11;

                UpdateStatus("自動購入処理が完了しました");
            }
            catch (Exception ex)
            {
                UpdateStatus($"自動購入エラー: {ex.Message}");
            }
            finally
            {
                StartButton.IsEnabled = true;
            }
        }

        /// <summary>
        /// 購入者情報を自動入力
        /// </summary>
        private async Task FillPurchaserInfo()
        {
            var formData = new Dictionary<string, string>
            {
                ["lastName"] = LastNameLineEdit.Text,
                ["firstName"] = FirstNameLineEdit.Text,
                ["lastKana"] = LastKanaLineEdit.Text,
                ["firstKana"] = FirstKanaLineEdit.Text,
                ["email"] = EmailLineEdit.Text,
                ["tel1"] = Tel1LineEdit.Text,
                ["tel2"] = Tel2LineEdit.Text,
                ["tel3"] = Tel3LineEdit.Text,
                ["cardNumber"] = CardNumberLineEdit.Text,
                ["cvv"] = CvvLineEdit.Text,
                ["cardName"] = CardNameLineEdit.Text
            };

            int filledCount = await _automationService.FillFormAsync(formData);
            UpdateStatus($"購入者情報を入力しました ({filledCount}項目)");
        }

        /// <summary>
        /// 選択されたイベントを取得
        /// </summary>
        private List<string> GetSelectedEvents()
        {
            var selectedEvents = new List<string>();

            // EventListWidgetから選択されたアイテムを取得
            foreach (var item in EventListWidget.SelectedItems)
            {
                selectedEvents.Add(item.ToString());
            }

            return selectedEvents;
        }

        private void AllCheckBox_Click(object sender, RoutedEventArgs e)
        {
            // TODO: 全イベント選択/解除
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: 自動購入停止
            StartButton.IsEnabled = true;
            ProgressBar.Value = 0;
            UpdateStatus("自動購入を停止しました");
        }

        #endregion

        #region Cookie管理機能

        private async void ViewCookiesButton_Click(object sender, RoutedEventArgs e)
        {
            if (_automationService == null) return;

            try
            {
                var cookies = await _automationService.GetCurrentPageCookiesAsync();

                var cookieInfo = string.Join("\n",
                    cookies.ConvertAll(c => $"{c.Name}: {c.Value}"));

                MessageBox.Show($"Cookie一覧:\n{cookieInfo}", "Cookie情報");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Cookie取得エラー: {ex.Message}");
            }
        }

        #endregion

        #region HTTPS通信テスト

        private async void TestHttpsButton_Click(object sender, RoutedEventArgs e)
        {
            if (_automationService == null) return;

            try
            {
                UpdateStatus("HTTPS通信テスト中...");

                var response = await _automationService.GetAsync("https://httpbin.org/get");

                if (response.IsSuccess)
                {
                    UpdateStatus($"HTTPS通信成功: {response.StatusCode}");
                    Console.WriteLine($"レスポンス: {response.Content}");
                }
                else
                {
                    UpdateStatus($"HTTPS通信失敗: {response.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"HTTPS通信エラー: {ex.Message}");
            }
        }

        #endregion

        #region UI更新

        private void UpdateStatus(string message)
        {
            StatusText.Text = $"{DateTime.Now:HH:mm:ss} - {message}";
        }

        private void UpdateProxyStatus(string message)
        {
            ProxyStatusText.Text = message;
        }

        private void UpdateRequestContextInfo()
        {
            var currentTab = _tabManager.GetCurrentTab();
            if (currentTab != null)
            {
                RequestContextInfo.Text = $"Context: {currentTab.ContextName}";
            }
        }

        #endregion

        protected override void OnClosed(EventArgs e)
        {
            // イベントハンドラーを解除
            if (_tabManager != null)
            {
                _tabManager.OnCurrentUrlChanged -= TabManager_OnCurrentUrlChanged;
            }

            _proxyRotationTimer?.Stop();
            _proxyRotationTimer?.Dispose();
            _automationService?.Dispose();
            _tabManager?.Dispose();
            _requestContextManager?.Dispose();
            base.OnClosed(e);
        }

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

        /// <summary>
        /// JavaScript実行テスト用のボタンイベント（デバッグ用）
        /// </summary>
        private void TestJavaScriptButton_Click(object sender, RoutedEventArgs e)
        {
            // テスト1: 簡単な計算
            ExecuteJavaScript("2 + 3", result =>
            {
                UpdateStatus($"計算結果: {result.Result} (成功: {result.Success})");
            });

            // テスト2: ページタイトル取得
            ExecuteJavaScript("document.title", result =>
            {
                UpdateStatus($"ページタイトル: {result.Result}");
            });

            // テスト3: 要素の存在確認
            _tabManager?.CheckElementExists("body", exists =>
            {
                UpdateStatus($"body要素の存在: {exists}");
            });

            // テスト4: 同期実行のテスト
            _ = Task.Run(async () =>
            {
                var result = await ExecuteJavaScriptSync("window.location.href");

                Dispatcher.Invoke(() =>
                {
                    UpdateStatus($"現在のURL: {result.Result}");
                });
            });
        }

        /// <summary>
        /// JavaScript実行結果をログに出力
        /// </summary>
        /// <param name="result">実行結果</param>
        private void LogJavaScriptResult(BrowserTabManager.JavaScriptResult result)
        {
            var logMessage = $"[{result.ExecutedAt:HH:mm:ss}] JavaScript実行 - " +
                            $"成功: {result.Success}, " +
                            $"結果: {result.Result}, " +
                            $"エラー: {result.ErrorMessage}";

            Console.WriteLine(logMessage);
            UpdateStatus(result.Success ? $"JS実行成功: {result.Result}" : $"JS実行失敗: {result.ErrorMessage}");
        }

        /// <summary>
        /// よく使用されるJavaScript操作のショートカットメソッド
        /// </summary>
        public class JavaScriptHelpers
        {
            private readonly MainWindow _mainWindow;

            public JavaScriptHelpers(MainWindow mainWindow)
            {
                _mainWindow = mainWindow;
            }

            /// <summary>
            /// ページが完全に読み込まれるまで待機
            /// </summary>
            /// <param name="callback">完了コールバック</param>
            /// <param name="maxWaitSeconds">最大待機秒数</param>
            public void WaitForPageLoad(Action<bool> callback, int maxWaitSeconds = 30)
            {
                var script = "document.readyState === 'complete'";
                var startTime = DateTime.Now;

                void CheckPageLoad()
                {
                    _mainWindow.ExecuteJavaScript(script, result =>
                    {
                        if (result.Success && result.Result is bool isComplete && isComplete)
                        {
                            callback?.Invoke(true);
                        }
                        else if ((DateTime.Now - startTime).TotalSeconds >= maxWaitSeconds)
                        {
                            callback?.Invoke(false);
                        }
                        else
                        {
                            // 500ms後に再チェック
                            Task.Delay(500).ContinueWith(_ => CheckPageLoad());
                        }
                    });
                }

                CheckPageLoad();
            }

            /// <summary>
            /// 要素が表示されるまで待機
            /// </summary>
            /// <param name="selector">CSSセレクター</param>
            /// <param name="callback">完了コールバック</param>
            /// <param name="maxWaitSeconds">最大待機秒数</param>
            public void WaitForElement(string selector, Action<bool> callback, int maxWaitSeconds = 30)
            {
                var script = $@"
                    var element = document.querySelector('{selector}');
                    element && element.offsetParent !== null;
                ";

                var startTime = DateTime.Now;

                void CheckElement()
                {
                    _mainWindow.ExecuteJavaScript(script, result =>
                    {
                        if (result.Success && result.Result is bool isVisible && isVisible)
                        {
                            callback?.Invoke(true);
                        }
                        else if ((DateTime.Now - startTime).TotalSeconds >= maxWaitSeconds)
                        {
                            callback?.Invoke(false);
                        }
                        else
                        {
                            // 500ms後に再チェック
                            Task.Delay(500).ContinueWith(_ => CheckElement());
                        }
                    });
                }

                CheckElement();
            }

            /// <summary>
            /// スクロール操作
            /// </summary>
            /// <param name="x">X座標</param>
            /// <param name="y">Y座標</param>
            /// <param name="callback">完了コールバック</param>
            public void ScrollTo(int x, int y, Action<bool> callback = null)
            {
                var script = $"window.scrollTo({x}, {y}); true;";

                _mainWindow.ExecuteJavaScript(script, result =>
                {
                    callback?.Invoke(result.Success);
                });
            }

            /// <summary>
            /// ページの最下部にスクロール
            /// </summary>
            /// <param name="callback">完了コールバック</param>
            public void ScrollToBottom(Action<bool> callback = null)
            {
                var script = "window.scrollTo(0, document.body.scrollHeight); true;";

                _mainWindow.ExecuteJavaScript(script, result =>
                {
                    callback?.Invoke(result.Success);
                });
            }

            /// <summary>
            /// 複数の要素に対する一括操作
            /// </summary>
            /// <param name="selector">CSSセレクター</param>
            /// <param name="action">各要素に対する操作（click, hide, show など）</param>
            /// <param name="callback">完了コールバック</param>
            public void ExecuteOnAllElements(string selector, string action, Action<int> callback = null)
            {
                var script = $@"
                    var elements = document.querySelectorAll('{selector}');
                    var count = 0;
                    for (var i = 0; i < elements.length; i++) {{
                        try {{
                            switch('{action}') {{
                                case 'click':
                                    elements[i].click();
                                    break;
                                case 'hide':
                                    elements[i].style.display = 'none';
                                    break;
                                case 'show':
                                    elements[i].style.display = '';
                                    break;
                            }}
                            count++;
                        }} catch (e) {{
                            console.error('Error executing action on element:', e);
                        }}
                    }}
                    count;
                ";

                _mainWindow.ExecuteJavaScript(script, result =>
                {
                    if (result.Success && result.Result is long count)
                    {
                        callback?.Invoke((int)count);
                    }
                    else
                    {
                        callback?.Invoke(0);
                    }
                });
            }
        }

        #endregion
    }
}