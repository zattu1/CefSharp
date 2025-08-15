using System;
using System.Collections.Generic;
using System.IO;
using System.Management;
using System.Security.Cryptography;
using System.Text;
using CefSharp;

namespace CefSharp.fastBOT.Core
{
    /// <summary>
    /// リクエストコンテキスト管理クラス
    /// PC固有のフォルダにキャッシュとクッキーを格納
    /// </summary>
    public class RequestContextManager : IDisposable
    {
        private readonly Dictionary<string, IRequestContext> _contexts;
        private readonly string _baseCacheDirectory;
        private readonly string _pcIdentifier;
        private bool _disposed = false;

        public RequestContextManager()
        {
            _contexts = new Dictionary<string, IRequestContext>();
            _pcIdentifier = GeneratePCIdentifier();

            // インスタンス番号を取得（App.xaml.csで既に決定済みのものを再利用）
            var instanceNumber = GetCurrentInstanceNumber(_pcIdentifier);

            _baseCacheDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "fastBOT",
                "BrowserData",
                $"PC_{_pcIdentifier}",
                $"Instance_{instanceNumber:D2}"
            );

            // ベースディレクトリを作成
            Directory.CreateDirectory(_baseCacheDirectory);

            Console.WriteLine($"RequestContextManager initialized with PC identifier: {_pcIdentifier}");
            Console.WriteLine($"RequestContextManager instance number: {instanceNumber}");
            Console.WriteLine($"Base cache directory: {_baseCacheDirectory}");
        }

        /// <summary>
        /// PC固有の識別子を生成
        /// </summary>
        /// <returns>PC識別子</returns>
        private string GeneratePCIdentifier()
        {
            try
            {
                var identifierParts = new List<string>();

                // マシン名を追加
                identifierParts.Add(Environment.MachineName);

                // プロセッサIDを取得（可能な場合）
                try
                {
                    using (var searcher = new ManagementObjectSearcher("SELECT ProcessorId FROM Win32_Processor"))
                    {
                        foreach (ManagementObject obj in searcher.Get())
                        {
                            var processorId = obj["ProcessorId"]?.ToString();
                            if (!string.IsNullOrEmpty(processorId))
                            {
                                identifierParts.Add(processorId);
                                break;
                            }
                        }
                    }
                }
                catch
                {
                    // プロセッサIDが取得できない場合はスキップ
                }

                // マザーボードシリアル番号を取得（可能な場合）
                try
                {
                    using (var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BaseBoard"))
                    {
                        foreach (ManagementObject obj in searcher.Get())
                        {
                            var serialNumber = obj["SerialNumber"]?.ToString();
                            if (!string.IsNullOrEmpty(serialNumber) && serialNumber != "To be filled by O.E.M.")
                            {
                                identifierParts.Add(serialNumber);
                                break;
                            }
                        }
                    }
                }
                catch
                {
                    // シリアル番号が取得できない場合はスキップ
                }

                // ユーザー名を追加
                identifierParts.Add(Environment.UserName);

                // すべての情報を結合してハッシュ化
                var combinedString = string.Join("-", identifierParts);
                using (var sha256 = SHA256.Create())
                {
                    var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(combinedString));
                    var hashString = Convert.ToBase64String(hashBytes)
                        .Replace("/", "_")
                        .Replace("+", "-")
                        .Replace("=", "")
                        .Substring(0, 16); // 16文字に短縮

                    Console.WriteLine($"PC identifier generated from: {combinedString}");
                    return hashString;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to generate PC identifier: {ex.Message}");
                // フォールバック：マシン名とユーザー名のみ使用
                var fallbackString = $"{Environment.MachineName}-{Environment.UserName}";
                using (var sha256 = SHA256.Create())
                {
                    var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(fallbackString));
                    return Convert.ToBase64String(hashBytes)
                        .Replace("/", "_")
                        .Replace("+", "-")
                        .Replace("=", "")
                        .Substring(0, 16);
                }
            }
        }

        /// <summary>
        /// 現在のインスタンス番号を取得（既存のロックファイルから判定）
        /// </summary>
        /// <param name="pcIdentifier">PC識別子</param>
        /// <returns>インスタンス番号</returns>
        private int GetCurrentInstanceNumber(string pcIdentifier)
        {
            try
            {
                var currentProcessId = System.Diagnostics.Process.GetCurrentProcess().Id;
                var pcBaseDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "fastBOT",
                    "BrowserData",
                    $"PC_{pcIdentifier}"
                );

                if (Directory.Exists(pcBaseDirectory))
                {
                    var instanceDirectories = Directory.GetDirectories(pcBaseDirectory, "Instance_*");

                    foreach (var dir in instanceDirectories)
                    {
                        var lockFile = Path.Combine(dir, "instance.lock");
                        if (File.Exists(lockFile))
                        {
                            try
                            {
                                var lockContent = File.ReadAllText(lockFile);
                                if (int.TryParse(lockContent, out var lockedProcessId) && lockedProcessId == currentProcessId)
                                {
                                    var dirName = Path.GetFileName(dir);
                                    if (dirName.StartsWith("Instance_") && dirName.Length > 9)
                                    {
                                        var numberPart = dirName.Substring(9);
                                        if (int.TryParse(numberPart, out var number))
                                        {
                                            return number;
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Failed to read lock file {lockFile}: {ex.Message}");
                            }
                        }
                    }
                }

                // ロックファイルが見つからない場合は1を返す（新規起動）
                return 1;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetCurrentInstanceNumber error: {ex.Message}");
                return 1;
            }
        }

        /// <summary>
        /// 分離されたコンテキストを作成
        /// </summary>
        /// <param name="name">コンテキスト名</param>
        /// <returns>リクエストコンテキスト</returns>
        public IRequestContext CreateIsolatedContext(string name)
        {
            try
            {
                if (_contexts.ContainsKey(name))
                {
                    Console.WriteLine($"Existing RequestContext returned: {name}");
                    return _contexts[name];
                }

                // グローバルキャッシュディレクトリ内にコンテキスト固有のサブディレクトリを作成
                var contextCachePath = Path.Combine(_baseCacheDirectory, "Contexts", name);
                Directory.CreateDirectory(contextCachePath);

                // 最小限の設定でRequestContextを作成
                RequestContextSettings settings = null;

                try
                {
                    settings = new RequestContextSettings()
                    {
                        AcceptLanguageList = "ja-JP,ja,en-US,en",
                        PersistSessionCookies = true
                    };
                }
                catch (Exception settingsEx)
                {
                    Console.WriteLine($"RequestContextSettings creation failed, using minimal settings: {settingsEx.Message}");
                    settings = new RequestContextSettings();
                }

                var context = new RequestContext(settings);
                _contexts[name] = context;

                Console.WriteLine($"RequestContext created: {name}");
                Console.WriteLine($"Context directory prepared: {contextCachePath}");
                return context;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"RequestContext creation failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 既存のコンテキストを取得
        /// </summary>
        /// <param name="name">コンテキスト名</param>
        /// <returns>リクエストコンテキスト</returns>
        public IRequestContext GetContext(string name)
        {
            _contexts.TryGetValue(name, out var context);
            return context;
        }

        /// <summary>
        /// コンテキストを削除
        /// </summary>
        /// <param name="name">コンテキスト名</param>
        /// <returns>削除に成功した場合true</returns>
        public bool RemoveContext(string name)
        {
            if (_contexts.TryGetValue(name, out var context))
            {
                try
                {
                    context?.Dispose();
                    _contexts.Remove(name);
                    Console.WriteLine($"RequestContext removed: {name}");
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"RequestContext removal failed: {ex.Message}");
                    return false;
                }
            }
            return false;
        }

        /// <summary>
        /// デフォルトコンテキストを取得
        /// </summary>
        /// <returns>グローバルリクエストコンテキスト</returns>
        public IRequestContext GetDefaultContext()
        {
            return Cef.GetGlobalRequestContext();
        }

        /// <summary>
        /// すべてのコンテキスト名を取得
        /// </summary>
        /// <returns>コンテキスト名のリスト</returns>
        public IEnumerable<string> GetContextNames()
        {
            return _contexts.Keys;
        }

        /// <summary>
        /// PC識別子を取得
        /// </summary>
        /// <returns>PC識別子</returns>
        public string GetPCIdentifier()
        {
            return _pcIdentifier;
        }

        /// <summary>
        /// ベースキャッシュディレクトリを取得
        /// </summary>
        /// <returns>ベースキャッシュディレクトリパス</returns>
        public string GetBaseCacheDirectory()
        {
            return _baseCacheDirectory;
        }

        /// <summary>
        /// キャッシュディレクトリのサイズを計算
        /// </summary>
        /// <returns>キャッシュサイズ（バイト）</returns>
        public long GetCacheSize()
        {
            try
            {
                if (!Directory.Exists(_baseCacheDirectory))
                    return 0;

                var directoryInfo = new DirectoryInfo(_baseCacheDirectory);
                return GetDirectorySize(directoryInfo);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to calculate cache size: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// ディレクトリサイズを再帰的に計算
        /// </summary>
        /// <param name="directoryInfo">ディレクトリ情報</param>
        /// <returns>サイズ（バイト）</returns>
        private long GetDirectorySize(DirectoryInfo directoryInfo)
        {
            long size = 0;

            try
            {
                // ファイルサイズを合計
                foreach (var fileInfo in directoryInfo.GetFiles())
                {
                    size += fileInfo.Length;
                }

                // サブディレクトリも再帰的に計算
                foreach (var subDirectory in directoryInfo.GetDirectories())
                {
                    size += GetDirectorySize(subDirectory);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error calculating directory size for {directoryInfo.FullName}: {ex.Message}");
            }

            return size;
        }

        /// <summary>
        /// キャッシュをクリア
        /// </summary>
        /// <param name="contextName">特定のコンテキスト名（nullの場合は全体）</param>
        /// <returns>クリアに成功した場合true</returns>
        public bool ClearCache(string contextName = null)
        {
            try
            {
                if (!string.IsNullOrEmpty(contextName))
                {
                    // 特定のコンテキストのキャッシュをクリア
                    var contextPath = Path.Combine(_baseCacheDirectory, "Contexts", contextName);
                    if (Directory.Exists(contextPath))
                    {
                        Directory.Delete(contextPath, true);
                        Directory.CreateDirectory(contextPath);
                        Console.WriteLine($"Cache cleared for context: {contextName}");
                        return true;
                    }
                }
                else
                {
                    // 全体のキャッシュをクリア
                    if (Directory.Exists(_baseCacheDirectory))
                    {
                        Directory.Delete(_baseCacheDirectory, true);
                        Directory.CreateDirectory(_baseCacheDirectory);
                        Console.WriteLine("All cache cleared");
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to clear cache: {ex.Message}");
                return false;
            }

            return false;
        }

        /// <summary>
        /// リソースを解放
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                foreach (var kvp in _contexts)
                {
                    try
                    {
                        kvp.Value?.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error disposing context {kvp.Key}: {ex.Message}");
                    }
                }
                _contexts.Clear();
                _disposed = true;
                Console.WriteLine("RequestContextManager disposed");
            }
        }
    }
}