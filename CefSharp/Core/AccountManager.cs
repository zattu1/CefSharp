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
    /// アカウント情報を管理するサービスクラス（シンプル版）
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

            LoadAccountsAsync().ConfigureAwait(false);
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
        /// 現在選択されているアカウント
        /// </summary>
        public AccountInfo CurrentAccount { get; private set; }

        /// <summary>
        /// 全てのアカウントを取得
        /// </summary>
        /// <returns>アカウントのリスト</returns>
        public List<AccountInfo> GetAllAccounts()
        {
            return _accounts.ToList();
        }

        /// <summary>
        /// 有効なアカウントのみを取得
        /// </summary>
        /// <returns>有効なアカウントのリスト</returns>
        public List<AccountInfo> GetActiveAccounts()
        {
            return _accounts.Where(a => a.IsActive).ToList();
        }

        /// <summary>
        /// アカウントを追加
        /// </summary>
        /// <param name="account">追加するアカウント</param>
        /// <returns>成功した場合true</returns>
        public async Task<bool> AddAccountAsync(AccountInfo account)
        {
            try
            {
                if (_accounts.Count >= MAX_ACCOUNTS)
                {
                    throw new InvalidOperationException($"アカウントは最大{MAX_ACCOUNTS}個まで登録できます");
                }

                if (string.IsNullOrWhiteSpace(account.AccountName))
                {
                    throw new ArgumentException("アカウント名が必要です");
                }

                if (_accounts.Any(a => a.AccountName.Equals(account.AccountName, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new ArgumentException("同じ名前のアカウントが既に存在します");
                }

                _accounts.Add(account);
                await SaveAccountsAsync();

                AccountChanged?.Invoke(this, account);
                Console.WriteLine($"Account added: {account.AccountName}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"AddAccountAsync error: {ex.Message}");
                return false;
            }
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
                var existingAccount = _accounts.FirstOrDefault(a => a.AccountName == account.AccountName);
                if (existingAccount == null)
                {
                    throw new ArgumentException("指定されたアカウントが見つかりません");
                }

                var index = _accounts.IndexOf(existingAccount);
                _accounts[index] = account;

                await SaveAccountsAsync();

                AccountChanged?.Invoke(this, account);
                Console.WriteLine($"Account updated: {account.AccountName}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"UpdateAccountAsync error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// アカウントを削除（IDによる削除）
        /// </summary>
        /// <param name="accountId">削除するアカウントID</param>
        /// <returns>成功した場合true</returns>
        public async Task<bool> DeleteAccountAsync(string accountId)
        {
            try
            {
                var account = _accounts.FirstOrDefault(a => a.Id == accountId);
                if (account == null)
                {
                    return false;
                }

                _accounts.Remove(account);
                await SaveAccountsAsync();

                // 削除されたアカウントが現在選択されている場合はクリア
                if (CurrentAccount == account)
                {
                    CurrentAccount = null;
                    CurrentAccountChanged?.Invoke(this, null);
                }

                Console.WriteLine($"Account deleted: {account.AccountName}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DeleteAccountAsync error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// アカウント名でアカウントを削除
        /// </summary>
        /// <param name="accountName">削除するアカウント名</param>
        /// <returns>成功した場合true</returns>
        public async Task<bool> DeleteAccountByNameAsync(string accountName)
        {
            try
            {
                var account = _accounts.FirstOrDefault(a => a.AccountName == accountName);
                if (account == null)
                {
                    return false;
                }

                return await DeleteAccountAsync(account.Id);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DeleteAccountByNameAsync error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// アカウントを取得
        /// </summary>
        /// <param name="accountName">アカウント名</param>
        /// <returns>アカウント情報（見つからない場合はnull）</returns>
        public AccountInfo GetAccount(string accountName)
        {
            return _accounts.FirstOrDefault(a => a.AccountName == accountName);
        }

        /// <summary>
        /// IDでアカウントを取得
        /// </summary>
        /// <param name="accountId">アカウントID</param>
        /// <returns>アカウント情報（見つからない場合はnull）</returns>
        public AccountInfo GetAccountById(string accountId)
        {
            return _accounts.FirstOrDefault(a => a.Id == accountId);
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
        /// 現在のアカウントを名前で設定
        /// </summary>
        /// <param name="accountName">アカウント名</param>
        public void SetCurrentAccountByName(string accountName)
        {
            var account = GetAccount(accountName);
            SetCurrentAccount(account);
        }

        /// <summary>
        /// 現在のアカウントをIDで設定
        /// </summary>
        /// <param name="accountId">アカウントID</param>
        public void SetCurrentAccountById(string accountId)
        {
            var account = GetAccountById(accountId);
            SetCurrentAccount(account);
        }

        /// <summary>
        /// 新しい空のアカウントを作成
        /// </summary>
        /// <param name="accountName">アカウント名</param>
        /// <returns>新しいアカウント情報</returns>
        public AccountInfo CreateNewAccount(string accountName = null)
        {
            if (string.IsNullOrEmpty(accountName))
            {
                accountName = GenerateDefaultAccountName();
            }

            return new AccountInfo
            {
                Id = Guid.NewGuid().ToString(),
                AccountName = accountName,
                ProxyHost = "127.0.0.1",
                ProxyPort = 8080,
                RotationIntervalSeconds = 30
            };
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
        /// アカウント情報をファイルから読み込み
        /// </summary>
        private async Task LoadAccountsAsync()
        {
            try
            {
                if (!File.Exists(_accountsFilePath))
                {
                    // デフォルトアカウントを作成
                    var defaultAccount = CreateNewAccount("デフォルト");
                    await AddAccountAsync(defaultAccount);
                    return;
                }

                var json = await File.ReadAllTextAsync(_accountsFilePath, Encoding.UTF8);
                var accounts = JsonSerializer.Deserialize<List<AccountInfo>>(json);

                _accounts.Clear();
                if (accounts != null)
                {
                    _accounts.AddRange(accounts);
                }

                Console.WriteLine($"Loaded {_accounts.Count} accounts");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"LoadAccountsAsync error: {ex.Message}");

                // エラーの場合はデフォルトアカウントを作成
                var defaultAccount = CreateNewAccount("デフォルト");
                await AddAccountAsync(defaultAccount);
            }
        }

        /// <summary>
        /// デフォルトアカウント名を生成
        /// </summary>
        /// <returns>デフォルトアカウント名</returns>
        private string GenerateDefaultAccountName()
        {
            for (int i = 1; i <= MAX_ACCOUNTS; i++)
            {
                var name = $"アカウント{i}";
                if (!_accounts.Any(a => a.AccountName == name))
                {
                    return name;
                }
            }
            return $"アカウント{DateTime.Now.Ticks}";
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