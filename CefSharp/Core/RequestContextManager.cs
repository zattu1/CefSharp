using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CefSharp;

namespace CefSharp.fastBOT.Core
{
    /// <summary>
    /// リクエストコンテキスト管理クラス（シンプル版・確実動作）
    /// </summary>
    public class RequestContextManager : IDisposable
    {
        private readonly Dictionary<string, IRequestContext> _contexts;
        private readonly int _instanceNumber;
        private readonly string _baseCachePath;
        private bool _disposed = false;

        public RequestContextManager()
        {
            _contexts = new Dictionary<string, IRequestContext>();

            // 現在のインスタンス番号を取得
            _instanceNumber = CommonSettings.GetCurrentInstanceNumber();

            // キャッシュパス
            _baseCachePath = CommonSettings.GetCachePath(_instanceNumber);

            // ベースディレクトリを確実に作成
            EnsureDirectoryExists(_baseCachePath);
        }

        /// <summary>
        /// ディレクトリの存在を確保
        /// </summary>
        /// <param name="directoryPath">作成するディレクトリパス</param>
        private void EnsureDirectoryExists(string directoryPath)
        {
            try
            {
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);

                    if (!Directory.Exists(directoryPath))
                    {
                        throw new DirectoryNotFoundException($"Failed to create directory: {directoryPath}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"EnsureDirectoryExists error for {directoryPath}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 分離されたコンテキストを作成（シンプル版）
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

                // CefSharp初期化チェック
                if (!(bool)Cef.IsInitialized)
                {
                    Console.WriteLine("WARNING: Cef is not initialized, using global context");
                    return Cef.GetGlobalRequestContext();
                }

                // コンテキストパス
                var contextName = GetContextName(name);
                var contextCachePath = Path.Combine(_baseCachePath, contextName);

                Console.WriteLine($"Creating context cache path: {contextCachePath}");
                EnsureDirectoryExists(contextCachePath);

                // 最小限のRequestContextSettings
                var settings = new RequestContextSettings()
                {
                    CachePath = contextCachePath,
                    AcceptLanguageList = "ja,en-US;q=0.9,en;q=0.8"
                };

                Console.WriteLine($"Creating RequestContext: {name} -> {contextName}");
                Console.WriteLine($"Cache path: {contextCachePath}");

                // RequestContextを作成
                IRequestContext context = null;
                try
                {
                    context = new RequestContext(settings);
                    Console.WriteLine($"RequestContext created successfully: {contextName}");
                }
                catch (Exception createEx)
                {
                    Console.WriteLine($"Failed to create RequestContext: {createEx.Message}");

                    // フォールバック: グローバルコンテキスト
                    Console.WriteLine("Using global RequestContext as fallback");
                    context = Cef.GetGlobalRequestContext();
                }

                if (context != null)
                {
                    _contexts[name] = context;
                    System.Threading.Thread.Sleep(50); // 短い待機
                }

                return context;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"RequestContext creation failed for '{name}': {ex.Message}");
                Console.WriteLine("Using global RequestContext as final fallback");
                return Cef.GetGlobalRequestContext();
            }
        }

        /// <summary>
        /// コンテキスト名を生成
        /// </summary>
        /// <param name="originalName">元のコンテキスト名</param>
        /// <returns>コンテキスト名</returns>
        private string GetContextName(string originalName)
        {
            try
            {
                // ASCII文字のみ、最大8文字
                var name = new string(originalName
                    .Where(c => char.IsLetterOrDigit(c))
                    .Take(8)
                    .ToArray());

                if (string.IsNullOrEmpty(name))
                {
                    name = "ctx";
                }

                return name;
            }
            catch
            {
                return "default";
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
        /// 現在のインスタンス番号を取得
        /// </summary>
        /// <returns>インスタンス番号</returns>
        public int GetInstanceNumber()
        {
            return _instanceNumber;
        }

        /// <summary>
        /// ベースキャッシュパスを取得
        /// </summary>
        /// <returns>ベースキャッシュパス</returns>
        public string GetBaseCachePath()
        {
            return _baseCachePath;
        }

        /// <summary>
        /// キャッシュサイズを計算
        /// </summary>
        /// <returns>キャッシュサイズ（バイト）</returns>
        public long GetCacheSize()
        {
            try
            {
                if (!Directory.Exists(_baseCachePath))
                    return 0;

                var directoryInfo = new DirectoryInfo(_baseCachePath);
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
                foreach (var fileInfo in directoryInfo.GetFiles())
                {
                    size += fileInfo.Length;
                }

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
                    var name = GetContextName(contextName);
                    var contextPath = Path.Combine(_baseCachePath, name);
                    if (Directory.Exists(contextPath))
                    {
                        Directory.Delete(contextPath, true);
                        EnsureDirectoryExists(contextPath);
                        return true;
                    }
                }
                else
                {
                    if (Directory.Exists(_baseCachePath))
                    {
                        var lockFile = Path.Combine(_baseCachePath, "instance.lock");
                        var lockFileExists = File.Exists(lockFile);
                        string lockContent = null;

                        if (lockFileExists)
                        {
                            lockContent = File.ReadAllText(lockFile);
                        }

                        Directory.Delete(_baseCachePath, true);
                        EnsureDirectoryExists(_baseCachePath);

                        if (lockFileExists && !string.IsNullOrEmpty(lockContent))
                        {
                            File.WriteAllText(lockFile, lockContent);
                        }

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
        /// 非アクティブなインスタンスのキャッシュをクリーンアップ
        /// </summary>
        /// <returns>クリーンアップされたインスタンス数</returns>
        public static int CleanupInactiveInstances()
        {
            int cleanedCount = 0;

            try
            {
                var instances = CommonSettings.GetAllInstancesInfo();
                var inactiveInstances = instances.Where(i => !i.IsActive).ToList();

                foreach (var instance in inactiveInstances)
                {
                    try
                    {
                        if (Directory.Exists(instance.CachePath))
                        {
                            Directory.Delete(instance.CachePath, true);
                            cleanedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to cleanup instance {instance.InstanceNumber}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"CleanupInactiveInstances error: {ex.Message}");
            }

            return cleanedCount;
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
            }
        }
    }

    /// <summary>
    /// インスタンス情報を表すクラス
    /// </summary>
    public class InstanceInfo
    {
        /// <summary>
        /// インスタンス番号
        /// </summary>
        public int InstanceNumber { get; set; }

        /// <summary>
        /// キャッシュパス
        /// </summary>
        public string CachePath { get; set; }

        /// <summary>
        /// アクティブかどうか
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// プロセスID
        /// </summary>
        public int? ProcessId { get; set; }

        /// <summary>
        /// キャッシュサイズ（バイト）
        /// </summary>
        public long CacheSize { get; set; }

        /// <summary>
        /// キャッシュサイズを人間が読みやすい形式で取得
        /// </summary>
        /// <returns>フォーマットされたキャッシュサイズ</returns>
        public string GetFormattedCacheSize()
        {
            if (CacheSize < 1024)
                return $"{CacheSize} B";
            else if (CacheSize < 1024 * 1024)
                return $"{CacheSize / 1024.0:F1} KB";
            else if (CacheSize < 1024 * 1024 * 1024)
                return $"{CacheSize / (1024.0 * 1024.0):F1} MB";
            else
                return $"{CacheSize / (1024.0 * 1024.0 * 1024.0):F1} GB";
        }

        /// <summary>
        /// インスタンス情報の文字列表現
        /// </summary>
        /// <returns>インスタンス情報の文字列</returns>
        public override string ToString()
        {
            var status = IsActive ? $"Active (PID: {ProcessId})" : "Inactive";
            return $"Instance {InstanceNumber}: {status}, Cache: {GetFormattedCacheSize()}";
        }
    }
}