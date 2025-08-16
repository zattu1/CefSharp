using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CefSharp.fastBOT.Models
{
    /// <summary>
    /// アカウント情報を格納するクラス（改良版）
    /// </summary>
    public class AccountInfo : INotifyPropertyChanged
    {
        private string _accountName = string.Empty;
        private string _loginId = string.Empty;
        private string _password = string.Empty;
        private string _proxyHost = string.Empty;
        private int _proxyPort = 8080;
        private string _proxyUsername = string.Empty;
        private string _proxyPassword = string.Empty;
        private bool _useProxyRotation = false;
        private bool _rotationPerRequest = true;
        private int _rotationIntervalSeconds = 30;

        /// <summary>
        /// アカウントID（一意識別子）
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// アカウント番号（1-10の固定番号）
        /// </summary>
        public int AccountNumber { get; set; } = 1;

        /// <summary>
        /// アカウント名（識別用）
        /// </summary>
        public string Name
        {
            get => _accountName;
            set => SetProperty(ref _accountName, value);
        }

        /// <summary>
        /// アカウント名（識別用）- 互換性のため
        /// </summary>
        public string AccountName
        {
            get => _accountName;
            set => SetProperty(ref _accountName, value);
        }

        /// <summary>
        /// ログインID
        /// </summary>
        public string LoginId
        {
            get => _loginId;
            set => SetProperty(ref _loginId, value);
        }

        /// <summary>
        /// パスワード（暗号化して保存）
        /// </summary>
        public string Password
        {
            get => _password;
            set => SetProperty(ref _password, value);
        }

        /// <summary>
        /// Proxyホスト
        /// </summary>
        public string ProxyHost
        {
            get => _proxyHost;
            set => SetProperty(ref _proxyHost, value);
        }

        /// <summary>
        /// Proxyポート
        /// </summary>
        public int ProxyPort
        {
            get => _proxyPort;
            set => SetProperty(ref _proxyPort, value);
        }

        /// <summary>
        /// Proxyユーザー名
        /// </summary>
        public string ProxyUsername
        {
            get => _proxyUsername;
            set => SetProperty(ref _proxyUsername, value);
        }

        /// <summary>
        /// Proxyパスワード
        /// </summary>
        public string ProxyPassword
        {
            get => _proxyPassword;
            set => SetProperty(ref _proxyPassword, value);
        }

        /// <summary>
        /// Proxyローテーション使用フラグ
        /// </summary>
        public bool UseProxyRotation
        {
            get => _useProxyRotation;
            set => SetProperty(ref _useProxyRotation, value);
        }

        /// <summary>
        /// リクエスト毎にローテーションするかどうか
        /// </summary>
        public bool RotationPerRequest
        {
            get => _rotationPerRequest;
            set => SetProperty(ref _rotationPerRequest, value);
        }

        /// <summary>
        /// ローテーション間隔（秒）
        /// </summary>
        public int RotationIntervalSeconds
        {
            get => _rotationIntervalSeconds;
            set => SetProperty(ref _rotationIntervalSeconds, value);
        }

        // 購入者情報の個別プロパティ
        public string LastName { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastKana { get; set; } = string.Empty;
        public string FirstKana { get; set; } = string.Empty;
        public string Tel1 { get; set; } = string.Empty;
        public string Tel2 { get; set; } = string.Empty;
        public string Tel3 { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;

        // クレジットカード情報の個別プロパティ
        public string CardNumber { get; set; } = string.Empty;
        public string Cvv { get; set; } = string.Empty;
        public string ExpiryMonth { get; set; } = string.Empty;
        public string ExpiryYear { get; set; } = string.Empty;
        public string CardName { get; set; } = string.Empty;

        /// <summary>
        /// 購入者情報（複合オブジェクト）
        /// </summary>
        public PurchaserInfo Purchaser { get; set; } = new PurchaserInfo();

        /// <summary>
        /// クレジットカード情報（複合オブジェクト）
        /// </summary>
        public CreditCardInfo CreditCard { get; set; } = new CreditCardInfo();

        /// <summary>
        /// 作成日時
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// 最終更新日時
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// アカウントが有効かどうか
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// 備考
        /// </summary>
        public string Notes { get; set; } = string.Empty;

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            UpdatedAt = DateTime.Now;
            OnPropertyChanged(propertyName);
            return true;
        }

        /// <summary>
        /// 全データをクリア（アカウント番号とIDは保持）
        /// </summary>
        public void ClearAllData()
        {
            // ログイン情報をクリア
            LoginId = string.Empty;
            Password = string.Empty;

            // ネットワーク設定をクリア
            ProxyHost = "127.0.0.1";
            ProxyPort = 8080;
            ProxyUsername = string.Empty;
            ProxyPassword = string.Empty;
            UseProxyRotation = false;
            RotationPerRequest = true;
            RotationIntervalSeconds = 30;

            // 購入者情報をクリア
            LastName = string.Empty;
            FirstName = string.Empty;
            LastKana = string.Empty;
            FirstKana = string.Empty;
            Tel1 = string.Empty;
            Tel2 = string.Empty;
            Tel3 = string.Empty;
            Email = string.Empty;

            // クレジットカード情報をクリア
            CardNumber = string.Empty;
            Cvv = string.Empty;
            ExpiryMonth = string.Empty;
            ExpiryYear = string.Empty;
            CardName = string.Empty;

            // 複合オブジェクトもクリア
            Purchaser = new PurchaserInfo();
            CreditCard = new CreditCardInfo();

            // 備考をクリア
            Notes = string.Empty;

            // アカウント名をデフォルトに戻す
            AccountName = $"アカウント{AccountNumber}";

            UpdatedAt = DateTime.Now;
        }

        /// <summary>
        /// データが設定されているかチェック
        /// </summary>
        /// <returns>ログイン情報が設定されている場合true</returns>
        public bool HasData()
        {
            return !string.IsNullOrEmpty(LoginId) || !string.IsNullOrEmpty(Password) ||
                   !string.IsNullOrEmpty(LastName) || !string.IsNullOrEmpty(Email);
        }

        /// <summary>
        /// ログイン情報が完全かチェック
        /// </summary>
        /// <returns>ログインIDとパスワードが両方設定されている場合true</returns>
        public bool HasCompleteLoginInfo()
        {
            return !string.IsNullOrEmpty(LoginId) && !string.IsNullOrEmpty(Password);
        }

        /// <summary>
        /// 購入者情報が完全かチェック
        /// </summary>
        /// <returns>必要な購入者情報が設定されている場合true</returns>
        public bool HasCompletePurchaserInfo()
        {
            return !string.IsNullOrEmpty(LastName) && !string.IsNullOrEmpty(FirstName) &&
                   !string.IsNullOrEmpty(Email);
        }

        /// <summary>
        /// クレジットカード情報が完全かチェック
        /// </summary>
        /// <returns>必要なクレジットカード情報が設定されている場合true</returns>
        public bool HasCompleteCreditCardInfo()
        {
            return !string.IsNullOrEmpty(CardNumber) && !string.IsNullOrEmpty(Cvv) &&
                   !string.IsNullOrEmpty(ExpiryMonth) && !string.IsNullOrEmpty(ExpiryYear);
        }

        /// <summary>
        /// アカウント情報の複製を作成
        /// </summary>
        /// <returns>複製されたアカウント情報</returns>
        public AccountInfo Clone()
        {
            return new AccountInfo
            {
                Id = Guid.NewGuid().ToString(), // 新しいIDを生成
                AccountNumber = this.AccountNumber,
                AccountName = this.AccountName,
                LoginId = this.LoginId,
                Password = this.Password,
                ProxyHost = this.ProxyHost,
                ProxyPort = this.ProxyPort,
                ProxyUsername = this.ProxyUsername,
                ProxyPassword = this.ProxyPassword,
                UseProxyRotation = this.UseProxyRotation,
                RotationPerRequest = this.RotationPerRequest,
                RotationIntervalSeconds = this.RotationIntervalSeconds,
                LastName = this.LastName,
                FirstName = this.FirstName,
                LastKana = this.LastKana,
                FirstKana = this.FirstKana,
                Tel1 = this.Tel1,
                Tel2 = this.Tel2,
                Tel3 = this.Tel3,
                Email = this.Email,
                CardNumber = this.CardNumber,
                Cvv = this.Cvv,
                ExpiryMonth = this.ExpiryMonth,
                ExpiryYear = this.ExpiryYear,
                CardName = this.CardName,
                Purchaser = this.Purchaser.Clone(),
                CreditCard = this.CreditCard.Clone(),
                CreatedAt = this.CreatedAt,
                UpdatedAt = DateTime.Now,
                IsActive = this.IsActive,
                Notes = this.Notes
            };
        }

        /// <summary>
        /// 表示用文字列を取得
        /// </summary>
        /// <returns>表示用文字列</returns>
        public override string ToString()
        {
            var status = IsActive ? "" : " (無効)";
            var hasLogin = HasCompleteLoginInfo() ? " ●" : " ○";
            var hasData = HasData() ? "" : " (未設定)";

            return $"アカウント{AccountNumber}{hasLogin}{hasData}{status}";
        }

        /// <summary>
        /// コンボボックス表示用の文字列を取得
        /// </summary>
        /// <returns>コンボボックス表示用文字列</returns>
        public string GetDisplayText()
        {
            if (HasCompleteLoginInfo())
            {
                return $"アカウント{AccountNumber} - {LoginId}";
            }
            else if (HasData())
            {
                return $"アカウント{AccountNumber} - 設定中";
            }
            else
            {
                return $"アカウント{AccountNumber} - 未設定";
            }
        }
    }

    /// <summary>
    /// 購入者情報
    /// </summary>
    public class PurchaserInfo
    {
        public string LastName { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastKana { get; set; } = string.Empty;
        public string FirstKana { get; set; } = string.Empty;
        public string PhoneNumber1 { get; set; } = string.Empty;
        public string PhoneNumber2 { get; set; } = string.Empty;
        public string PhoneNumber3 { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// 購入者情報の複製を作成
        /// </summary>
        /// <returns>複製された購入者情報</returns>
        public PurchaserInfo Clone()
        {
            return new PurchaserInfo
            {
                LastName = this.LastName,
                FirstName = this.FirstName,
                LastKana = this.LastKana,
                FirstKana = this.FirstKana,
                PhoneNumber1 = this.PhoneNumber1,
                PhoneNumber2 = this.PhoneNumber2,
                PhoneNumber3 = this.PhoneNumber3,
                Email = this.Email
            };
        }
    }

    /// <summary>
    /// クレジットカード情報
    /// </summary>
    public class CreditCardInfo
    {
        public string CardNumber { get; set; } = string.Empty;
        public string CVV { get; set; } = string.Empty;
        public int ExpiryMonth { get; set; } = 1;
        public int ExpiryYear { get; set; } = DateTime.Now.Year;
        public string CardHolderName { get; set; } = string.Empty;

        /// <summary>
        /// クレジットカード情報の複製を作成
        /// </summary>
        /// <returns>複製されたクレジットカード情報</returns>
        public CreditCardInfo Clone()
        {
            return new CreditCardInfo
            {
                CardNumber = this.CardNumber,
                CVV = this.CVV,
                ExpiryMonth = this.ExpiryMonth,
                ExpiryYear = this.ExpiryYear,
                CardHolderName = this.CardHolderName
            };
        }
    }
}