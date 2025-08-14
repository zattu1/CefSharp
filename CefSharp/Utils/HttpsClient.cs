using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using Newtonsoft.Json;
using CefSharp.fastBOT.Utils;

namespace CefSharp.fastBOT.Utils
{
    /// <summary>
    /// HTTPS通信を行うクライアントクラス
    /// </summary>
    public class HttpsClient : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly CookieContainer _cookieContainer;
        private bool _disposed = false;

        #region コンストラクタ

        /// <summary>
        /// HttpsClientのコンストラクタ
        /// </summary>
        /// <param name="timeout">タイムアウト時間（秒）</param>
        /// <param name="userAgent">ユーザーエージェント</param>
        /// <param name="proxy">プロキシ設定</param>
        public HttpsClient(int timeout = 30, string userAgent = null, WebProxy proxy = null)
        {
            _cookieContainer = new CookieContainer();

            var handler = new HttpClientHandler()
            {
                CookieContainer = _cookieContainer,
                UseCookies = true
            };

            // プロキシ設定
            if (proxy != null)
            {
                handler.Proxy = proxy;
                handler.UseProxy = true;
            }

            // SSL証明書の検証を無効化（開発用）
            handler.ServerCertificateCustomValidationCallback =
                (message, cert, chain, errors) => true;

            _httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(timeout)
            };

            // デフォルトヘッダー設定
            _httpClient.DefaultRequestHeaders.Add("User-Agent",
                userAgent ?? UserAgentHelper.GetChromeUserAgent());

            _httpClient.DefaultRequestHeaders.Add("Accept",
                "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");

            _httpClient.DefaultRequestHeaders.Add("Accept-Language",
                "ja,en-US;q=0.9,en;q=0.8");

            _httpClient.DefaultRequestHeaders.Add("Accept-Encoding",
                "gzip, deflate, br");

            _httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
        }

        #endregion

        #region HTTP メソッド

        /// <summary>
        /// GETリクエストを送信
        /// </summary>
        /// <param name="url">リクエストURL</param>
        /// <param name="headers">追加ヘッダー</param>
        /// <returns>レスポンス</returns>
        public async Task<HttpsResponse> GetAsync(string url, Dictionary<string, string> headers = null)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                AddHeaders(request, headers);

                using var response = await _httpClient.SendAsync(request);
                return await CreateResponseAsync(response);
            }
            catch (Exception ex)
            {
                return new HttpsResponse
                {
                    IsSuccess = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// POSTリクエストを送信
        /// </summary>
        /// <param name="url">リクエストURL</param>
        /// <param name="data">送信データ</param>
        /// <param name="contentType">コンテンツタイプ</param>
        /// <param name="headers">追加ヘッダー</param>
        /// <returns>レスポンス</returns>
        public async Task<HttpsResponse> PostAsync(string url, string data = null,
            string contentType = "application/x-www-form-urlencoded",
            Dictionary<string, string> headers = null)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, url);

                if (!string.IsNullOrEmpty(data))
                {
                    request.Content = new StringContent(data, Encoding.UTF8, contentType);
                }

                AddHeaders(request, headers);

                using var response = await _httpClient.SendAsync(request);
                return await CreateResponseAsync(response);
            }
            catch (Exception ex)
            {
                return new HttpsResponse
                {
                    IsSuccess = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// JSONデータをPOST送信
        /// </summary>
        /// <param name="url">リクエストURL</param>
        /// <param name="jsonObject">JSONオブジェクト</param>
        /// <param name="headers">追加ヘッダー</param>
        /// <returns>レスポンス</returns>
        public async Task<HttpsResponse> PostJsonAsync(string url, object jsonObject,
            Dictionary<string, string> headers = null)
        {
            var json = JsonConvert.SerializeObject(jsonObject);
            return await PostAsync(url, json, "application/json", headers);
        }

        /// <summary>
        /// フォームデータをPOST送信
        /// </summary>
        /// <param name="url">リクエストURL</param>
        /// <param name="formData">フォームデータ</param>
        /// <param name="headers">追加ヘッダー</param>
        /// <returns>レスポンス</returns>
        public async Task<HttpsResponse> PostFormAsync(string url,
            Dictionary<string, string> formData, Dictionary<string, string> headers = null)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, url);

                var encodedContent = new List<KeyValuePair<string, string>>();
                foreach (var kvp in formData)
                {
                    encodedContent.Add(new KeyValuePair<string, string>(kvp.Key, kvp.Value));
                }

                request.Content = new FormUrlEncodedContent(encodedContent);
                AddHeaders(request, headers);

                using var response = await _httpClient.SendAsync(request);
                return await CreateResponseAsync(response);
            }
            catch (Exception ex)
            {
                return new HttpsResponse
                {
                    IsSuccess = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        #endregion

        #region Cookie管理

        /// <summary>
        /// Cookieを追加
        /// </summary>
        /// <param name="url">対象URL</param>
        /// <param name="name">Cookie名</param>
        /// <param name="value">Cookie値</param>
        public void AddCookie(string url, string name, string value)
        {
            try
            {
                var uri = new Uri(url);
                var cookie = new System.Net.Cookie(name, value, "/", uri.Host);
                _cookieContainer.Add(cookie);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Cookie追加エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// Cookieを取得
        /// </summary>
        /// <param name="url">対象URL</param>
        /// <returns>Cookieのコレクション</returns>
        public CookieCollection GetCookies(string url)
        {
            try
            {
                var uri = new Uri(url);
                return _cookieContainer.GetCookies(uri);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Cookie取得エラー: {ex.Message}");
                return new CookieCollection();
            }
        }

        #endregion

        #region ヘルパーメソッド

        /// <summary>
        /// リクエストにヘッダーを追加
        /// </summary>
        /// <param name="request">HTTPリクエスト</param>
        /// <param name="headers">追加ヘッダー</param>
        private void AddHeaders(HttpRequestMessage request, Dictionary<string, string> headers)
        {
            if (headers == null) return;

            foreach (var header in headers)
            {
                try
                {
                    if (header.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ヘッダー追加エラー [{header.Key}]: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// HTTPレスポンスからHttpsResponseを作成
        /// </summary>
        /// <param name="response">HTTPレスポンス</param>
        /// <returns>HttpsResponse</returns>
        private async Task<HttpsResponse> CreateResponseAsync(HttpResponseMessage response)
        {
            var result = new HttpsResponse
            {
                StatusCode = (int)response.StatusCode,
                IsSuccess = response.IsSuccessStatusCode,
                Headers = new Dictionary<string, string>()
            };

            // ヘッダー情報を取得
            foreach (var header in response.Headers)
            {
                result.Headers[header.Key] = string.Join(", ", header.Value);
            }

            // コンテンツヘッダーも追加
            if (response.Content?.Headers != null)
            {
                foreach (var header in response.Content.Headers)
                {
                    result.Headers[header.Key] = string.Join(", ", header.Value);
                }
            }

            // レスポンスボディを取得
            if (response.Content != null)
            {
                result.Content = await response.Content.ReadAsStringAsync();
                result.ContentBytes = await response.Content.ReadAsByteArrayAsync();
            }

            return result;
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (!_disposed)
            {
                _httpClient?.Dispose();
                _disposed = true;
            }
        }

        #endregion
    }

    /// <summary>
    /// HTTPSレスポンスデータ
    /// </summary>
    public class HttpsResponse
    {
        /// <summary>成功フラグ</summary>
        public bool IsSuccess { get; set; }

        /// <summary>ステータスコード</summary>
        public int StatusCode { get; set; }

        /// <summary>レスポンスヘッダー</summary>
        public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();

        /// <summary>レスポンス内容（文字列）</summary>
        public string Content { get; set; }

        /// <summary>レスポンス内容（バイト配列）</summary>
        public byte[] ContentBytes { get; set; }

        /// <summary>エラーメッセージ</summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// JSONレスポンスをデシリアライズ
        /// </summary>
        /// <typeparam name="T">デシリアライズ先の型</typeparam>
        /// <returns>デシリアライズされたオブジェクト</returns>
        public T DeserializeJson<T>()
        {
            try
            {
                return JsonConvert.DeserializeObject<T>(Content);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"JSON デシリアライズ エラー: {ex.Message}");
                return default(T);
            }
        }
    }
}