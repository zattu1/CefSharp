using CefSharp.fastBOT.Core;
using CefSharp.fastBOT.Models;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace CefSharp.fastBOT
{
    /// <summary>
    /// AutoPurchaseControl.xaml の相互作用ロジック
    /// 自動購入機能のコントロールパネル（UserControl）改良版
    /// </summary>
    public partial class AutoPurchaseControl : UserControl
    {
        // 各種マネージャー
        private ProxyManager _proxyManager;
        private AutomationService _automationService;
        private HtmlExtractionService _htmlService;
        private AccountManager _accountManager;

        // タイマー
        private System.Timers.Timer _proxyRotationTimer;

        // HTML取得関連
        private string _lastHtmlContent;

        // アカウント切り替え管理
        private bool _isUpdatingAccount = false;

        // MainWindowのブラウザを参照するためのプロパティ
        public BrowserTabManager BrowserTabManager { get; set; }
        public RequestContextManager RequestContextManager { get; set; }

        public AutoPurchaseControl()
        {
            InitializeComponent();
            InitializeManagers();
            InitializeUI();
        }

        /// <summary>
        /// 各種マネージャーを初期化
        /// </summary>
        private void InitializeManagers()
        {
            try
            {
                _proxyManager = new ProxyManager();
                _accountManager = new AccountManager();

                // Proxyローテーションタイマー
                _proxyRotationTimer = new System.Timers.Timer();
                _proxyRotationTimer.Elapsed += ProxyRotationTimer_Elapsed;

                // アカウントマネージャーのイベントハンドラーを設定
                _accountManager.CurrentAccountChanged += AccountManager_CurrentAccountChanged;
                _accountManager.AccountListUpdated += AccountManager_AccountListUpdated;

                // イベントハンドラーを設定
                SetupEventHandlers();

                Console.WriteLine("AutoPurchaseControl managers initialized successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"InitializeManagers error: {ex.Message}");
                UpdateStatus($"マネージャー初期化エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// イベントハンドラーを設定
        /// </summary>
        private void SetupEventHandlers()
        {
            try
            {
                // アカウント管理のイベントハンドラー
                if (AccountComboBox != null)
                {
                    AccountComboBox.SelectionChanged += AccountComboBox_SelectionChanged;
                }
                if (SaveAccountButton != null)
                {
                    SaveAccountButton.Click += SaveAccountButton_Click;
                }
                if (NewAccountButton != null)
                {
                    NewAccountButton.Click += NewAccountButton_Click;
                }
                if (DeleteAccountButton != null)
                {
                    DeleteAccountButton.Click += DeleteAccountButton_Click;
                }

                // フォーム入力のイベントハンドラー
                LoginEdit.TextChanged += FormField_Changed;
                PasswordEdit.PasswordChanged += FormField_Changed;
                ProxyLineEdit.TextChanged += FormField_Changed;
                LastNameLineEdit.TextChanged += FormField_Changed;
                FirstNameLineEdit.TextChanged += FormField_Changed;
                LastKanaLineEdit.TextChanged += FormField_Changed;
                FirstKanaLineEdit.TextChanged += FormField_Changed;
                Tel1LineEdit.TextChanged += FormField_Changed;
                Tel2LineEdit.TextChanged += FormField_Changed;
                Tel3LineEdit.TextChanged += FormField_Changed;
                EmailLineEdit.TextChanged += FormField_Changed;
                CardNumberLineEdit.TextChanged += FormField_Changed;
                CvvLineEdit.TextChanged += FormField_Changed;
                CardNameLineEdit.TextChanged += FormField_Changed;
                MonthComboBox.SelectionChanged += FormField_Changed;
                YearComboBox.SelectionChanged += FormField_Changed;

                // その他のボタンイベント
                LoginButton.Click += LoginButton_Click;
                StartButton.Click += StartButton_Click;
                StopButton.Click += StopButton_Click;
                ApplyProxyButton.Click += ApplyProxyButton_Click;
                ProxyRotationCheckBox.Checked += ProxyRotationCheckBox_Checked;
                ProxyRotationCheckBox.Unchecked += ProxyRotationCheckBox_Unchecked;
                AllCheckBox.Click += AllCheckBox_Click;

                Console.WriteLine("AutoPurchaseControl event handlers setup completed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SetupEventHandlers error: {ex.Message}");
            }
        }

        /// <summary>
        /// UIを初期化
        /// </summary>
        private void InitializeUI()
        {
            try
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

                // 数量コンボボックス
                NumComboBox.SelectedIndex = 0;

                // アカウントコンボボックスを初期化
                InitializeAccountComboBox();

                UpdateStatus("fastBOT - チケット自動購入システム 初期化完了");
                UpdateHtmlStatus("未実行");
                UpdateAccountStatus(null);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"InitializeUI error: {ex.Message}");
            }
        }

        /// <summary>
        /// アカウントコンボボックスを初期化（1-10のスロット）
        /// </summary>
        private void InitializeAccountComboBox()
        {
            try
            {
                if (AccountComboBox != null)
                {
                    _isUpdatingAccount = true;

                    AccountComboBox.Items.Clear();

                    // 1-10のアカウントスロットを追加
                    for (int i = 1; i <= _accountManager.MaxAccounts; i++)
                    {
                        var account = _accountManager.GetAccountByNumber(i);
                        AccountComboBox.Items.Add(account.GetDisplayText());
                    }

                    // デフォルトで1番目のアカウントを選択
                    if (AccountComboBox.Items.Count > 0)
                    {
                        AccountComboBox.SelectedIndex = 0;
                        var firstAccount = _accountManager.GetAccountByNumber(1);
                        LoadAccountToUI(firstAccount);
                        _accountManager.SetCurrentAccount(firstAccount);
                    }

                    _isUpdatingAccount = false;
                    Console.WriteLine($"Account combo box initialized with {AccountComboBox.Items.Count} slots");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"InitializeAccountComboBox error: {ex.Message}");
                _isUpdatingAccount = false;
            }
        }

        /// <summary>
        /// MainWindowのブラウザサービスを設定
        /// </summary>
        public void SetBrowserServices(BrowserTabManager tabManager, RequestContextManager contextManager)
        {
            try
            {
                BrowserTabManager = tabManager;
                RequestContextManager = contextManager;

                // 現在のブラウザを取得してHtmlExtractionServiceを初期化
                var currentBrowser = BrowserTabManager?.GetCurrentBrowser();
                if (currentBrowser != null)
                {
                    _htmlService = new HtmlExtractionService(currentBrowser);
                    _automationService = new AutomationService(currentBrowser);
                    StartButton.IsEnabled = true;
                }

                Console.WriteLine("Browser services set successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SetBrowserServices error: {ex.Message}");
            }
        }

        #region アカウント管理イベント

        /// <summary>
        /// アカウントマネージャーのCurrentAccountChangedイベントハンドラー
        /// </summary>
        private void AccountManager_CurrentAccountChanged(object sender, AccountInfo account)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                UpdateAccountStatus(account);
            });
        }

        /// <summary>
        /// アカウントマネージャーのAccountListUpdatedイベントハンドラー
        /// </summary>
        private void AccountManager_AccountListUpdated(object sender, EventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                RefreshAccountComboBox();
            });
        }

        /// <summary>
        /// アカウントコンボボックスの選択変更イベント
        /// </summary>
        private void AccountComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingAccount) return;

            try
            {
                if (AccountComboBox.SelectedIndex >= 0)
                {
                    int selectedAccountNumber = AccountComboBox.SelectedIndex + 1;
                    var account = _accountManager.GetAccountByNumber(selectedAccountNumber);

                    LoadAccountToUI(account);
                    _accountManager.SetCurrentAccount(account);

                    UpdateStatus($"アカウント{selectedAccountNumber}に切り替えました");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"AccountComboBox_SelectionChanged error: {ex.Message}");
            }
        }

        /// <summary>
        /// アカウント保存ボタンクリックイベント
        /// </summary>
        private async void SaveAccountButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (AccountComboBox.SelectedIndex >= 0)
                {
                    int selectedAccountNumber = AccountComboBox.SelectedIndex + 1;
                    var account = _accountManager.GetAccountByNumber(selectedAccountNumber);

                    SaveUIToAccount(account);
                    account.IsActive = true;

                    await _accountManager.UpdateAccountAsync(account);
                    RefreshAccountComboBox();

                    ShowSuccess($"アカウント{selectedAccountNumber}の情報を保存しました");
                }
            }
            catch (Exception ex)
            {
                ShowError($"アカウント保存エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// 新規アカウントボタンクリックイベント（クリア機能として動作）
        /// </summary>
        private void NewAccountButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (AccountComboBox.SelectedIndex >= 0)
                {
                    int selectedAccountNumber = AccountComboBox.SelectedIndex + 1;
                    var account = _accountManager.GetAccountByNumber(selectedAccountNumber);

                    // フォームをクリア
                    ClearUI();

                    UpdateStatus($"アカウント{selectedAccountNumber}のフォームをクリアしました");
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"フォームクリアエラー: {ex.Message}");
            }
        }

        /// <summary>
        /// アカウント削除ボタンクリックイベント
        /// </summary>
        private async void DeleteAccountButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (AccountComboBox.SelectedIndex >= 0)
                {
                    int selectedAccountNumber = AccountComboBox.SelectedIndex + 1;

                    var result = MessageBox.Show($"アカウント{selectedAccountNumber}のデータを削除しますか？",
                        "確認", MessageBoxButton.YesNo, MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        await _accountManager.ClearAccountAsync(selectedAccountNumber);
                        ClearUI();
                        RefreshAccountComboBox();
                        UpdateStatus($"アカウント{selectedAccountNumber}のデータを削除しました");
                    }
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"アカウント削除エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// フォーム入力変更イベント
        /// </summary>
        private void FormField_Changed(object sender, EventArgs e)
        {
            // フィールド変更時の処理（自動保存などが必要な場合に実装）
        }

        /// <summary>
        /// 全選択チェックボックスクリックイベント
        /// </summary>
        private void AllCheckBox_Click(object sender, RoutedEventArgs e)
        {
            // TODO: 全イベント選択/解除
        }

        #endregion

        #region 自動化機能

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            if (_automationService == null) return;

            try
            {
                UpdateProgress(1, 4, "自動ログインを開始します...");

                var loginId = LoginEdit.Text;
                var password = PasswordEdit.Password;

                if (string.IsNullOrEmpty(loginId) || string.IsNullOrEmpty(password))
                {
                    ShowError("ログインIDとパスワードを入力してください");
                    ProgressBar.Value = 0;
                    return;
                }

                UpdateProgress(2, 4, "ログイン情報を送信中...");
                bool success = await _automationService.AutoLoginAsync(loginId, password);

                if (success)
                {
                    UpdateProgress(4, 4, "ログインが完了しました");
                    ShowSuccess("ログインを実行しました");

                    // 少し待ってからプログレスバーをリセット
                    await Task.Delay(2000);
                    ProgressBar.Value = 0;
                }
                else
                {
                    ProgressBar.Value = 0;
                    ShowError("ログインに失敗しました");
                }
            }
            catch (Exception ex)
            {
                ProgressBar.Value = 0;
                ShowError($"ログインエラー: {ex.Message}");
            }
        }

        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (_automationService == null) return;

            try
            {
                StartButton.IsEnabled = false;
                UpdateProgress(1, 11, "自動購入を開始します...");

                // 1. ページ読み込み待機
                UpdateProgress(2, 11, "ページ読み込み待機中...");
                await _automationService.WaitForPageLoadAsync(30);

                // 2. ログイン情報の自動入力
                if (!string.IsNullOrEmpty(LoginEdit.Text) && !string.IsNullOrEmpty(PasswordEdit.Password))
                {
                    UpdateProgress(4, 11, "ログイン中...");
                    await _automationService.AutoLoginAsync(LoginEdit.Text, PasswordEdit.Password);
                }

                // 3. チケット選択画面へ遷移
                UpdateProgress(6, 11, "チケット選択画面へ移動中...");
                await Task.Delay(2000);

                // 4. チケット自動選択
                UpdateProgress(8, 11, "チケットを選択中...");
                await Task.Delay(2000);

                // 5. 購入者情報入力
                UpdateProgress(10, 11, "購入者情報を入力中...");
                await FillPurchaserInfo();

                // 6. 最終確認
                UpdateProgress(11, 11, "購入処理完了待機中...");
                await _automationService.WaitForElementAsync(".purchase-complete, .order-complete", 60);

                ShowSuccess("自動購入処理が完了しました");

                // 完了後にプログレスバーをリセット
                await Task.Delay(3000);
                ProgressBar.Value = 0;
            }
            catch (Exception ex)
            {
                ProgressBar.Value = 0;
                ShowError($"自動購入エラー: {ex.Message}");
            }
            finally
            {
                StartButton.IsEnabled = true;
            }
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StartButton.IsEnabled = true;
                ProgressBar.Value = 0;
                ShowSuccess("自動購入を停止しました");
            }
            catch (Exception ex)
            {
                ShowError($"停止処理エラー: {ex.Message}");
            }
        }

        private async Task FillPurchaserInfo()
        {
            try
            {
                var formData = new System.Collections.Generic.Dictionary<string, string>
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
            catch (Exception ex)
            {
                UpdateStatus($"購入者情報入力エラー: {ex.Message}");
            }
        }

        #endregion

        #region Proxy管理

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
                Console.WriteLine($"プロキシ設定適用開始: '{proxyText}'");

                var currentBrowser = BrowserTabManager?.GetCurrentBrowser();
                if (currentBrowser == null)
                {
                    ShowError("ブラウザが見つかりません");
                    return;
                }

                if (string.IsNullOrEmpty(proxyText))
                {
                    // プロキシを無効化
                    Console.WriteLine("プロキシを無効化中...");
                    bool disableSuccess = await _proxyManager.DisableProxyAsync(currentBrowser);

                    if (disableSuccess)
                    {
                        UpdateProxyStatus("Proxy無効");
                        ShowSuccess("Proxyを無効にしました");
                    }
                    else
                    {
                        ShowError("Proxy無効化に失敗しました");
                    }
                    return;
                }

                var proxyConfig = ParseProxyText(proxyText);
                if (proxyConfig == null)
                {
                    ShowError("Proxy形式が不正です\n例: 127.0.0.1:8080 または 127.0.0.1:8080:user:pass");
                    return;
                }

                Console.WriteLine($"プロキシ設定適用中: {proxyConfig}");
                bool success = await _proxyManager.SetProxyAsync(currentBrowser, proxyConfig);

                if (success)
                {
                    UpdateProxyStatus($"Proxy: {proxyConfig.Host}:{proxyConfig.Port}");
                    ShowSuccess("Proxy設定を適用しました");

                    // プロキシテストを実行（オプション）
                    Console.WriteLine("プロキシテスト実行中...");
                    bool testResult = await _proxyManager.TestProxyAsync(currentBrowser);
                    if (testResult)
                    {
                        ShowSuccess("プロキシテスト成功");
                    }
                    else
                    {
                        ShowError("プロキシテスト失敗 - 設定を確認してください");
                    }
                }
                else
                {
                    ShowError("Proxy設定に失敗しました");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ApplyProxySettingsエラー: {ex.Message}");
                ShowError($"Proxy設定エラー: {ex.Message}");
            }
        }

        private ProxyConfig ParseProxyText(string proxyText)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(proxyText))
                {
                    Console.WriteLine("プロキシテキストが空です");
                    return null;
                }

                Console.WriteLine($"プロキシテキストをパース中: {proxyText}");

                // ProxyConfig.Parseメソッドを使用
                var config = ProxyConfig.Parse(proxyText);

                if (config == null)
                {
                    Console.WriteLine("プロキシテキストのパースに失敗");
                    return null;
                }

                if (!config.IsValid())
                {
                    Console.WriteLine("無効なプロキシ設定");
                    return null;
                }

                Console.WriteLine($"パース結果: {config}");
                return config;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ParseProxyTextエラー: {ex.Message}");
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
                await ApplyProxySettings();
            });
        }

        #endregion

        #region ヘルパーメソッド

        /// <summary>
        /// UIからアカウントに情報を保存
        /// </summary>
        private void SaveUIToAccount(AccountInfo account)
        {
            try
            {
                account.LoginId = LoginEdit.Text;
                account.Password = PasswordEdit.Password;

                // Proxy設定
                var proxyParts = ProxyLineEdit.Text.Split(':');
                if (proxyParts.Length >= 2)
                {
                    account.ProxyHost = proxyParts[0];
                    if (int.TryParse(proxyParts[1], out int port))
                        account.ProxyPort = port;
                }

                account.UseProxyRotation = ProxyRotationCheckBox.IsChecked == true;
                account.RotationPerRequest = PerRequestRadioButton.IsChecked == true;
                if (int.TryParse(ProxyEverySecondLineEdit.Text, out int interval))
                    account.RotationIntervalSeconds = interval;

                // 購入者情報
                account.LastName = LastNameLineEdit.Text;
                account.FirstName = FirstNameLineEdit.Text;
                account.LastKana = LastKanaLineEdit.Text;
                account.FirstKana = FirstKanaLineEdit.Text;
                account.Email = EmailLineEdit.Text;
                account.Tel1 = Tel1LineEdit.Text;
                account.Tel2 = Tel2LineEdit.Text;
                account.Tel3 = Tel3LineEdit.Text;

                // クレジットカード情報
                account.CardNumber = CardNumberLineEdit.Text;
                account.Cvv = CvvLineEdit.Text;
                account.CardName = CardNameLineEdit.Text;

                if (MonthComboBox.SelectedItem != null)
                    account.ExpiryMonth = MonthComboBox.SelectedItem.ToString();
                if (YearComboBox.SelectedItem != null)
                    account.ExpiryYear = YearComboBox.SelectedItem.ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SaveUIToAccount error: {ex.Message}");
            }
        }

        /// <summary>
        /// アカウント情報をUIに読み込み
        /// </summary>
        private void LoadAccountToUI(AccountInfo account)
        {
            try
            {
                _isUpdatingAccount = true;

                // ログイン情報
                LoginEdit.Text = account.LoginId ?? "";
                PasswordEdit.Password = account.Password ?? "";

                // Proxy設定
                if (!string.IsNullOrEmpty(account.ProxyHost))
                {
                    ProxyLineEdit.Text = $"{account.ProxyHost}:{account.ProxyPort}";
                }
                ProxyRotationCheckBox.IsChecked = account.UseProxyRotation;
                PerRequestRadioButton.IsChecked = account.RotationPerRequest;
                EverySecondRadioButton.IsChecked = !account.RotationPerRequest;
                ProxyEverySecondLineEdit.Text = account.RotationIntervalSeconds.ToString();

                // 購入者情報
                LastNameLineEdit.Text = account.LastName ?? "";
                FirstNameLineEdit.Text = account.FirstName ?? "";
                LastKanaLineEdit.Text = account.LastKana ?? "";
                FirstKanaLineEdit.Text = account.FirstKana ?? "";
                EmailLineEdit.Text = account.Email ?? "";
                Tel1LineEdit.Text = account.Tel1 ?? "";
                Tel2LineEdit.Text = account.Tel2 ?? "";
                Tel3LineEdit.Text = account.Tel3 ?? "";

                // クレジットカード情報
                CardNumberLineEdit.Text = account.CardNumber ?? "";
                CvvLineEdit.Text = account.Cvv ?? "";
                CardNameLineEdit.Text = account.CardName ?? "";

                // 有効期限
                if (!string.IsNullOrEmpty(account.ExpiryMonth))
                {
                    for (int i = 0; i < MonthComboBox.Items.Count; i++)
                    {
                        if (MonthComboBox.Items[i].ToString() == account.ExpiryMonth)
                        {
                            MonthComboBox.SelectedIndex = i;
                            break;
                        }
                    }
                }

                if (!string.IsNullOrEmpty(account.ExpiryYear))
                {
                    for (int i = 0; i < YearComboBox.Items.Count; i++)
                    {
                        if (YearComboBox.Items[i].ToString() == account.ExpiryYear)
                        {
                            YearComboBox.SelectedIndex = i;
                            break;
                        }
                    }
                }

                _isUpdatingAccount = false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"LoadAccountToUI error: {ex.Message}");
                _isUpdatingAccount = false;
            }
        }

        /// <summary>
        /// UIをクリア
        /// </summary>
        private void ClearUI()
        {
            try
            {
                _isUpdatingAccount = true;

                LoginEdit.Text = "";
                PasswordEdit.Password = "";
                ProxyLineEdit.Text = "127.0.0.1:8080";
                ProxyRotationCheckBox.IsChecked = false;
                PerRequestRadioButton.IsChecked = true;
                ProxyEverySecondLineEdit.Text = "30";
                LastNameLineEdit.Text = "";
                FirstNameLineEdit.Text = "";
                LastKanaLineEdit.Text = "";
                FirstKanaLineEdit.Text = "";
                EmailLineEdit.Text = "";
                Tel1LineEdit.Text = "";
                Tel2LineEdit.Text = "";
                Tel3LineEdit.Text = "";
                CardNumberLineEdit.Text = "";
                CvvLineEdit.Text = "";
                CardNameLineEdit.Text = "";
                MonthComboBox.SelectedIndex = 0;
                YearComboBox.SelectedIndex = 0;

                _isUpdatingAccount = false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ClearUI error: {ex.Message}");
                _isUpdatingAccount = false;
            }
        }

        /// <summary>
        /// アカウントコンボボックスを更新
        /// </summary>
        private void RefreshAccountComboBox()
        {
            try
            {
                _isUpdatingAccount = true;

                int currentSelectedIndex = AccountComboBox.SelectedIndex;
                AccountComboBox.Items.Clear();

                // 1-10のアカウントスロットを再追加
                for (int i = 1; i <= _accountManager.MaxAccounts; i++)
                {
                    var account = _accountManager.GetAccountByNumber(i);
                    AccountComboBox.Items.Add(account.GetDisplayText());
                }

                // 選択状態を復元
                if (currentSelectedIndex >= 0 && currentSelectedIndex < AccountComboBox.Items.Count)
                {
                    AccountComboBox.SelectedIndex = currentSelectedIndex;
                }

                _isUpdatingAccount = false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"RefreshAccountComboBox error: {ex.Message}");
                _isUpdatingAccount = false;
            }
        }

        /// <summary>
        /// HTML自動保存
        /// </summary>
        private async Task AutoSaveHtml(string filePrefix)
        {
            try
            {
                if (string.IsNullOrEmpty(_lastHtmlContent))
                    return;

                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var fileName = $"{filePrefix}_{timestamp}.html";
                var filePath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "fastBOT_HTML", fileName);

                // ディレクトリを作成
                var directory = System.IO.Path.GetDirectoryName(filePath);
                if (!System.IO.Directory.Exists(directory))
                {
                    System.IO.Directory.CreateDirectory(directory);
                }

                await System.IO.File.WriteAllTextAsync(filePath, _lastHtmlContent, System.Text.Encoding.UTF8);
                UpdateStatus($"解析ログ自動保存: {fileName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"AutoSaveHtml error: {ex.Message}");
            }
        }

        #endregion

        #region UIステータス更新メソッド（MainWindowステータスバー連携版）

        /// <summary>
        /// MainWindowの参照を保持するプロパティ
        /// </summary>
        public MainWindow ParentMainWindow { get; set; }

        /// <summary>
        /// メインステータスを更新（MainWindowのステータスバーと連携）
        /// </summary>
        /// <param name="message">ステータスメッセージ</param>
        public void UpdateStatus(string message)
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    // MainWindowのステータスバーを更新
                    if (ParentMainWindow != null)
                    {
                        ParentMainWindow.UpdateMainStatus(message);
                    }
                    else
                    {
                        // フォールバック：コンソールに出力
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"UpdateStatus error: {ex.Message}");
            }
        }

        /// <summary>
        /// Proxyステータスを更新
        /// </summary>
        /// <param name="message">Proxyステータスメッセージ</param>
        private void UpdateProxyStatus(string message)
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (ProxyStatusText != null)
                    {
                        ProxyStatusText.Text = message;
                    }

                    // MainWindowにも通知
                    if (ParentMainWindow != null)
                    {
                        ParentMainWindow.ShowLogMessage($"Proxy: {message}", 2000);
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"UpdateProxyStatus error: {ex.Message}");
            }
        }

        /// <summary>
        /// HTML解析ステータスを更新
        /// </summary>
        /// <param name="message">HTML解析ステータスメッセージ</param>
        private void UpdateHtmlStatus(string message)
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (HtmlStatusText != null)
                    {
                        HtmlStatusText.Text = $"解析: {message}";
                    }

                    // MainWindowにも通知
                    if (ParentMainWindow != null)
                    {
                        ParentMainWindow.ShowLogMessage($"HTML解析: {message}", 2000);
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"UpdateHtmlStatus error: {ex.Message}");
            }
        }

        /// <summary>
        /// アカウントステータスを更新
        /// </summary>
        /// <param name="account">現在のアカウント情報</param>
        private void UpdateAccountStatus(AccountInfo account)
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (AccountStatusText != null)
                    {
                        if (account != null)
                        {
                            AccountStatusText.Text = $"アカウント: {account.GetDisplayText()}";
                        }
                        else
                        {
                            AccountStatusText.Text = "アカウント: 未選択";
                        }
                    }

                    // MainWindowにも通知
                    if (ParentMainWindow != null)
                    {
                        var statusMessage = account != null
                            ? $"アカウント: {account.GetDisplayText()}"
                            : "アカウント: 未選択";
                        ParentMainWindow.ShowLogMessage(statusMessage, 2000);
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"UpdateAccountStatus error: {ex.Message}");
            }
        }

        /// <summary>
        /// エラーメッセージを表示
        /// </summary>
        /// <param name="errorMessage">エラーメッセージ</param>
        public void ShowError(string errorMessage)
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (ParentMainWindow != null)
                    {
                        ParentMainWindow.ShowErrorMessage(errorMessage);
                    }
                    else
                    {
                        Console.WriteLine($"[ERROR] {errorMessage}");
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ShowError error: {ex.Message}");
            }
        }

        /// <summary>
        /// 成功メッセージを表示
        /// </summary>
        /// <param name="successMessage">成功メッセージ</param>
        public void ShowSuccess(string successMessage)
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (ParentMainWindow != null)
                    {
                        ParentMainWindow.ShowSuccessMessage(successMessage);
                    }
                    else
                    {
                        Console.WriteLine($"[SUCCESS] {successMessage}");
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ShowSuccess error: {ex.Message}");
            }
        }

        /// <summary>
        /// 進捗状況を更新
        /// </summary>
        /// <param name="step">現在のステップ</param>
        /// <param name="totalSteps">総ステップ数</param>
        /// <param name="message">進捗メッセージ</param>
        public void UpdateProgress(int step, int totalSteps, string message)
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    // プログレスバーを更新
                    if (ProgressBar != null)
                    {
                        ProgressBar.Value = step;
                        ProgressBar.Maximum = totalSteps;
                    }

                    // ステータスメッセージを更新
                    var progressMessage = $"[{step}/{totalSteps}] {message}";
                    UpdateStatus(progressMessage);
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"UpdateProgress error: {ex.Message}");
            }
        }

        #endregion

        #region パブリックメソッド（他のクラスから使用）

        /// <summary>
        /// 最後に取得したHTMLを取得
        /// </summary>
        public string GetLastHtmlContent()
        {
            return _lastHtmlContent;
        }

        /// <summary>
        /// 現在のHTMLを取得（非同期）
        /// </summary>
        public async Task<string> GetCurrentHtmlAsync()
        {
            try
            {
                if (_htmlService != null)
                {
                    var html = await _htmlService.GetPageHtmlAsync();
                    _lastHtmlContent = html;
                    return html;
                }
                return string.Empty;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetCurrentHtmlAsync error: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// 指定したセレクターの要素HTMLを取得
        /// </summary>
        public async Task<string> GetElementHtmlAsync(string selector)
        {
            try
            {
                if (_htmlService != null)
                {
                    return await _htmlService.GetElementHtmlAsync(selector);
                }
                return string.Empty;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetElementHtmlAsync error: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// 現在選択されているアカウント番号を取得
        /// </summary>
        public int GetCurrentAccountNumber()
        {
            return AccountComboBox.SelectedIndex + 1;
        }

        /// <summary>
        /// 指定したアカウント番号を選択
        /// </summary>
        public void SelectAccount(int accountNumber)
        {
            if (accountNumber >= 1 && accountNumber <= _accountManager.MaxAccounts)
            {
                AccountComboBox.SelectedIndex = accountNumber - 1;
            }
        }

        /// <summary>
        /// 現在のアカウント情報を強制保存
        /// </summary>
        public async Task<bool> SaveCurrentAccountAsync()
        {
            try
            {
                if (AccountComboBox.SelectedIndex >= 0)
                {
                    int selectedAccountNumber = AccountComboBox.SelectedIndex + 1;
                    var account = _accountManager.GetAccountByNumber(selectedAccountNumber);

                    SaveUIToAccount(account);
                    account.IsActive = account.HasData(); // データがある場合のみ有効化

                    bool result = await _accountManager.UpdateAccountAsync(account);
                    if (result)
                    {
                        RefreshAccountComboBox();
                        UpdateStatus($"アカウント{selectedAccountNumber}を自動保存しました");
                    }
                    return result;
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SaveCurrentAccountAsync error: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region リソースクリーンアップ

        /// <summary>
        /// リソースを解放
        /// </summary>
        public void Dispose()
        {
            try
            {
                // タイマーを停止・解放
                _proxyRotationTimer?.Stop();
                _proxyRotationTimer?.Dispose();

                // サービスを解放
                _automationService?.Dispose();

                // アカウント情報を最終保存
                if (AccountComboBox.SelectedIndex >= 0)
                {
                    Task.Run(async () => await SaveCurrentAccountAsync());
                }

                // アカウントマネージャーを解放
                _accountManager?.Dispose();

                Console.WriteLine("AutoPurchaseControl disposed successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Dispose error: {ex.Message}");
            }
        }

        #endregion
    }
}