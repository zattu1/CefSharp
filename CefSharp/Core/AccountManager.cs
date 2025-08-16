using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CefSharp.fastBOT.Models;

namespace CefSharp.fastBOT.Core
{
    /// <summary>
    /// アカウント情報を管理するサービスクラス（改良版）
    /// 最大10個のアカウントを管理し、即座の切り替えをサポート
    /// </summary>
    public class AccountManager
    {
        private readonly string _accountsFilePath;
        private readonly List<AccountInfo> _accounts;
        private const int MAX_ACCOUNTS = 10;

        public AccountManager()
        {
            var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "fastBOT");
            Directory.CreateDirectory(appDataPath);
            _accountsFilePath = Path.Combine(appDataPath, "accounts.json");
            _accounts = new List<AccountInfo>();

            // 同期的にアカウントを初期化
            InitializeAccountsSync();
        }

        /// <summary>
        /// アカウント情報が変更された時のイベント
        /// </summary>
        public event EventHandler<AccountInfo> AccountChanged;

        /// <summary>
        /// 現在選択されているアカウントが変更された時のイベント
        /// </summary>
        public event EventHandler<AccountInfo> CurrentAccountChanged;

        /// <summary>
        /// アカウントリストが更新された時のイベント
        /// </summary>
        public event EventHandler AccountListUpdated;

        /// <summary>
        /// 現在選択されているアカウント
        /// </summary>
        public AccountInfo CurrentAccount { get; private set; }

        /// <summary>
        /// 全てのアカウントを取得（1-10の番号順）
        /// </summary>
        /// <returns>アカウントのリスト</returns>
        public List<AccountInfo> GetAllAccounts()
        {
            return _accounts.OrderBy(a => a.AccountNumber).ToList();
        }

        /// <summary>
        /// 有効なアカウントのみを取得
        /// </summary>
        /// <returns>有効なアカウントのリスト</returns>
        public List<AccountInfo> GetActiveAccounts()
        {
            return _accounts.Where(a => a.IsActive).OrderBy(a => a.AccountNumber).ToList();
        }

        /// <summary>
        /// 指定した番号のアカウントを取得
        /// </summary>
        /// <param name="accountNumber">アカウント番号（1-10）</param>
        /// <returns>アカウント情報（存在しない場合は新規作成）</returns>
        public AccountInfo GetAccountByNumber(int accountNumber)
        {
            if (accountNumber < 1 || accountNumber > MAX_ACCOUNTS)
                throw new ArgumentException($"アカウント番号は1から{MAX_ACCOUNTS}の間で指定してください");

            var account = _accounts.FirstOrDefault(a => a.AccountNumber == accountNumber);
            if (account == null)
            {
                // アカウントが存在しない場合は新規作成
                account = CreateNewAccount(accountNumber);
                _accounts.Add(account);
                SaveAccountsAsync().ConfigureAwait(false);
                AccountListUpdated?.Invoke(this, EventArgs.Empty);
            }

            return account;
        }

        /// <summary>
        /// アカウントを更新
        /// </summary>
        /// <param name="account">更新するアカウント</param>
        /// <returns>成功した場合true</returns>
        public async Task<bool> UpdateAccountAsync(AccountInfo account)
        {
            try
            {
                var existingAccount = _accounts.FirstOrDefault(a => a.AccountNumber == account.AccountNumber);
                if (existingAccount != null)
                {
                    var index = _accounts.IndexOf(existingAccount);
                    _accounts[index] = account;
                }
                else
                {
                    _accounts.Add(account);
                }

                await SaveAccountsAsync();
                AccountChanged?.Invoke(this, account);
                AccountListUpdated?.Invoke(this, EventArgs.Empty);
                Console.WriteLine($"Account updated: アカウント{account.AccountNumber}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"UpdateAccountAsync error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// アカウントを削除（データをクリア）
        /// </summary>
        /// <param name="accountNumber">削除するアカウント番号</param>
        /// <returns>成功した場合true</returns>
        public async Task<bool> ClearAccountAsync(int accountNumber)
        {
            try
            {
                var account = _accounts.FirstOrDefault(a => a.AccountNumber == accountNumber);
                if (account != null)
                {
                    // データをクリアして無効化
                    account.ClearAllData();
                    account.IsActive = false;

                    await SaveAccountsAsync();

                    // 削除されたアカウントが現在選択されている場合はクリア
                    if (CurrentAccount?.AccountNumber == accountNumber)
                    {
                        CurrentAccount = null;
                        CurrentAccountChanged?.Invoke(this, null);
                    }

                    AccountChanged?.Invoke(this, account);
                    AccountListUpdated?.Invoke(this, EventArgs.Empty);
                    Console.WriteLine($"Account cleared: アカウント{accountNumber}");
                }
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ClearAccountAsync error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 現在のアカウントを設定（番号で指定）
        /// </summary>
        /// <param name="accountNumber">アカウント番号</param>
        public void SetCurrentAccountByNumber(int accountNumber)
        {
            var account = GetAccountByNumber(accountNumber);
            SetCurrentAccount(account);
        }

        /// <summary>
        /// 現在のアカウントを設定（AccountInfoオブジェクトで）
        /// </summary>
        /// <param name="account">設定するアカウント</param>
        public void SetCurrentAccount(AccountInfo account)
        {
            CurrentAccount = account;
            CurrentAccountChanged?.Invoke(this, account);
        }

        /// <summary>
        /// 新しい空のアカウントを作成
        /// </summary>
        /// <param name="accountNumber">アカウント番号</param>
        /// <returns>新しいアカウント情報</returns>
        public AccountInfo CreateNewAccount(int accountNumber)
        {
            if (accountNumber < 1 || accountNumber > MAX_ACCOUNTS)
                throw new ArgumentException($"アカウント番号は1から{MAX_ACCOUNTS}の間で指定してください");

            return new AccountInfo
            {
                Id = Guid.NewGuid().ToString(),
                AccountNumber = accountNumber,
                AccountName = $"アカウント{accountNumber}",
                ProxyHost = "127.0.0.1",
                ProxyPort = 8080,
                RotationIntervalSeconds = 30,
                IsActive = true,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };
        }

        /// <summary>
        /// 全てのアカウントスロットを初期化（1-10）
        /// </summary>
        public async Task InitializeAllAccountSlots()
        {
            for (int i = 1; i <= MAX_ACCOUNTS; i++)
            {
                if (!_accounts.Any(a => a.AccountNumber == i))
                {
                    var newAccount = CreateNewAccount(i);
                    newAccount.IsActive = false; // 空のスロットは無効化
                    _accounts.Add(newAccount);
                }
            }
            await SaveAccountsAsync();
            AccountListUpdated?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// アカウント情報をファイルに保存
        /// </summary>
        private async Task SaveAccountsAsync()
        {
            try
            {
                var json = JsonSerializer.Serialize(_accounts, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });

                await File.WriteAllTextAsync(_accountsFilePath, json, Encoding.UTF8);
                Console.WriteLine($"Accounts saved to: {_accountsFilePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SaveAccountsAsync error: {ex.Message}");
            }
        }

        /// <summary>
        /// アカウント情報をファイルから読み込み（同期版）
        /// </summary>
        private void InitializeAccountsSync()
        {
            try
            {
                if (File.Exists(_accountsFilePath))
                {
                    var json = File.ReadAllText(_accountsFilePath, Encoding.UTF8);
                    var accounts = JsonSerializer.Deserialize<List<AccountInfo>>(json);

                    _accounts.Clear();
                    if (accounts != null)
                    {
                        _accounts.AddRange(accounts);
                    }
                }

                // 不足しているアカウントスロットを補完
                for (int i = 1; i <= MAX_ACCOUNTS; i++)
                {
                    if (!_accounts.Any(a => a.AccountNumber == i))
                    {
                        var newAccount = CreateNewAccount(i);
                        newAccount.IsActive = false; // 空のスロットは無効化
                        _accounts.Add(newAccount);
                    }
                }

                Console.WriteLine($"Loaded {_accounts.Count} account slots (1-{MAX_ACCOUNTS})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"InitializeAccountsSync error: {ex.Message}");

                // エラーの場合は全スロットを新規作成
                _accounts.Clear();
                for (int i = 1; i <= MAX_ACCOUNTS; i++)
                {
                    var newAccount = CreateNewAccount(i);
                    newAccount.IsActive = false;
                    _accounts.Add(newAccount);
                }
            }

            // 初期化完了を非同期で保存
            Task.Run(async () => await SaveAccountsAsync());
        }

        /// <summary>
        /// アカウント数を取得
        /// </summary>
        public int AccountCount => _accounts.Count;

        /// <summary>
        /// 最大アカウント数を取得
        /// </summary>
        public int MaxAccounts => MAX_ACCOUNTS;

        /// <summary>
        /// 使用可能なアカウント番号のリストを取得
        /// </summary>
        /// <returns>1から10までの番号リスト</returns>
        public List<int> GetAvailableAccountNumbers()
        {
            return Enumerable.Range(1, MAX_ACCOUNTS).ToList();
        }

        /// <summary>
        /// アカウントの表示名リストを取得（コンボボックス用）
        /// </summary>
        /// <returns>表示名のリスト</returns>
        public List<string> GetAccountDisplayNames()
        {
            var displayNames = new List<string>();
            for (int i = 1; i <= MAX_ACCOUNTS; i++)
            {
                var account = _accounts.FirstOrDefault(a => a.AccountNumber == i);
                if (account != null && account.IsActive && !string.IsNullOrEmpty(account.LoginId))
                {
                    displayNames.Add($"アカウント{i} - {account.LoginId}");
                }
                else
                {
                    displayNames.Add($"アカウント{i} - 未設定");
                }
            }
            return displayNames;
        }

        /// <summary>
        /// リソースをクリーンアップ
        /// </summary>
        public void Dispose()
        {
            try
            {
                SaveAccountsAsync().Wait();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"AccountManager dispose error: {ex.Message}");
            }
        }
    }
}