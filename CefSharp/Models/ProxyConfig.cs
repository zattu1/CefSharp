using System;

namespace CefSharp.fastBOT.Models
{
    /// <summary>
    /// プロキシ設定情報を格納するクラス
    /// </summary>
    public class ProxyConfig
    {
        /// <summary>
        /// プロキシホスト
        /// </summary>
        public string Host { get; set; }

        /// <summary>
        /// プロキシポート
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// プロキシスキーム（http, https, socks5など）
        /// </summary>
        public string Scheme { get; set; } = "http";

        /// <summary>
        /// 認証用ユーザー名
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// 認証用パスワード
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// プロキシが有効かどうか
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public ProxyConfig()
        {
        }

        /// <summary>
        /// コンストラクタ（基本情報）
        /// </summary>
        /// <param name="host">ホスト</param>
        /// <param name="port">ポート</param>
        /// <param name="scheme">スキーム</param>
        public ProxyConfig(string host, int port, string scheme = "http")
        {
            Host = host;
            Port = port;
            Scheme = scheme;
        }

        /// <summary>
        /// コンストラクタ（認証付き）
        /// </summary>
        /// <param name="host">ホスト</param>
        /// <param name="port">ポート</param>
        /// <param name="username">ユーザー名</param>
        /// <param name="password">パスワード</param>
        /// <param name="scheme">スキーム</param>
        public ProxyConfig(string host, int port, string username, string password, string scheme = "http")
        {
            Host = host;
            Port = port;
            Username = username;
            Password = password;
            Scheme = scheme;
        }

        /// <summary>
        /// 認証が必要かどうか
        /// </summary>
        public bool RequiresAuthentication => !string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(Password);

        /// <summary>
        /// プロキシURLを取得
        /// </summary>
        /// <returns>プロキシURL</returns>
        public string GetProxyUrl()
        {
            if (RequiresAuthentication)
            {
                return $"{Scheme}://{Username}:{Password}@{Host}:{Port}";
            }
            else
            {
                return $"{Scheme}://{Host}:{Port}";
            }
        }

        /// <summary>
        /// 文字列表現を取得
        /// </summary>
        /// <returns>プロキシ情報の文字列</returns>
        public override string ToString()
        {
            var auth = RequiresAuthentication ? " (認証あり)" : "";
            return $"{Scheme}://{Host}:{Port}{auth}";
        }

        /// <summary>
        /// プロキシ文字列をパース（host:port:user:pass形式）
        /// </summary>
        /// <param name="proxyText">プロキシ文字列</param>
        /// <returns>ProxyConfigオブジェクト</returns>
        public static ProxyConfig Parse(string proxyText)
        {
            if (string.IsNullOrWhiteSpace(proxyText))
                return null;

            try
            {
                var parts = proxyText.Trim().Split(':');
                if (parts.Length < 2)
                    return null;

                var config = new ProxyConfig
                {
                    Host = parts[0].Trim(),
                    Port = int.Parse(parts[1].Trim())
                };

                // ユーザー名とパスワードがある場合
                if (parts.Length >= 4)
                {
                    config.Username = parts[2].Trim();
                    config.Password = parts[3].Trim();
                }

                // スキームの判定（ポート番号から推測）
                if (config.Port == 1080 || config.Port == 1081)
                {
                    config.Scheme = "socks5";
                }
                else
                {
                    config.Scheme = "http";
                }

                return config;
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"プロキシ文字列のパースエラー: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 設定が有効かどうかを検証
        /// </summary>
        /// <returns>有効な場合true</returns>
        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(Host) &&
                   Port > 0 && Port <= 65535 &&
                   !string.IsNullOrWhiteSpace(Scheme);
        }
    }
}