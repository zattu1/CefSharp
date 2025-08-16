using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CefSharp;

namespace CefSharp.fastBOT.Core
{
    /// <summary>
    /// リクエストコンテキスト管理クラス
    /// インスタンス番号ベースのキャッシュ管理（1, 2, 3...の順番でプロセス毎に分離）
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
            _instanceNumber = GetCurrentInstanceNumber();

            // ベースキャッシュパスを設定
            _baseCachePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "fastBOT", "Instance", _instanceNumber.ToString()
            );

            Console.WriteLine($"RequestContextManager initialized for instance {_instanceNumber}");
            Console.WriteLine($"Base cache path: {_baseCachePath}");
        }

        /// <summary>
        /// 現在のインスタンス番号を取得
        /// </summary>
        /// <returns>インスタンス番号</returns>
        private int GetCurrentInstanceNumber()
        {
            try
            {
                var currentProcessId = System.Diagnostics.Process.GetCurrentProcess().Id;
                var baseDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "fastBOT", "Instance"
                );

                if (Directory.Exists(baseDirectory))
                {
                    var instanceDirectories = Directory.GetDirectories(baseDirectory)
                        .Where(dir => int.TryParse(Path.GetFileName(dir), out _))
                        .ToList();

                    foreach (var dir in instanceDirectories)
                    {
                        var dirName = Path.GetFileName(dir);
                        if (int.TryParse(dirName, out var number))
                        {
                            var lockFile = Path.Combine(dir, "instance.lock");
                            if (File.Exists(lockFile))
                            {
                                try
                                {
                                    var lockContent = File.ReadAllText(lockFile);
                                    if (int.TryParse(lockContent, out var lockedProcessId) && lockedProcessId == currentProcessId)
                                    {
                                        return number;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"Failed to read lock file {lockFile}: {ex.Message}");
                                }
                            }
                        }
                    }
                }

                // ロックファイルが見つからない場合は1を返す
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

                // インスタンス番号配下のコンテキスト固有ディレクトリを作成
                var contextCachePath = Path.Combine(_baseCachePath, "Contexts", name);
                Directory.CreateDirectory(contextCachePath);

                var settings = new RequestContextSettings()
                {
                    CachePath = contextCachePath,
                    AcceptLanguageList = "ja-JP,ja,en-US,en"
                };

                var context = new RequestContext(settings);
                _contexts[name] = context;

                Console.WriteLine($"RequestContext created: {name}");
                Console.WriteLine($"Context cache path: {contextCachePath}");
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
                    var contextPath = Path.Combine(_baseCachePath, "Contexts", contextName);
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
                    // 全体のキャッシュをクリア（ロックファイルは保持）
                    if (Directory.Exists(_baseCachePath))
                    {
                        var lockFile = Path.Combine(_baseCachePath, "instance.lock");
                        var lockFileExists = File.Exists(lockFile);
                        string lockContent = null;

                        // ロックファイルのバックアップ
                        if (lockFileExists)
                        {
                            lockContent = File.ReadAllText(lockFile);
                        }

                        // ディレクトリを削除して再作成
                        Directory.Delete(_baseCachePath, true);
                        Directory.CreateDirectory(_baseCachePath);

                        // ロックファイルを復元
                        if (lockFileExists && !string.IsNullOrEmpty(lockContent))
                        {
                            File.WriteAllText(lockFile, lockContent);
                        }

                        Console.WriteLine($"All cache cleared for instance {_instanceNumber}");
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
        /// 全インスタンスの情報を取得
        /// </summary>
        /// <returns>インスタンス情報のリスト</returns>
        public static List<InstanceInfo> GetAllInstancesInfo()
        {
            var instances = new List<InstanceInfo>();

            try
            {
                var baseDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "fastBOT", "Instance"
                );

                if (Directory.Exists(baseDirectory))
                {
                    var instanceDirectories = Directory.GetDirectories(baseDirectory)
                        .Where(dir => int.TryParse(Path.GetFileName(dir), out _))
                        .OrderBy(dir => int.Parse(Path.GetFileName(dir)))
                        .ToList();

                    foreach (var dir in instanceDirectories)
                    {
                        var dirName = Path.GetFileName(dir);
                        if (int.TryParse(dirName, out var number))
                        {
                            var instance = new InstanceInfo
                            {
                                InstanceNumber = number,
                                CachePath = dir,
                                IsActive = false,
                                ProcessId = null,
                                CacheSize = 0
                            };

                            // ロックファイルをチェック
                            var lockFile = Path.Combine(dir, "instance.lock");
                            if (File.Exists(lockFile))
                            {
                                try
                                {
                                    var lockContent = File.ReadAllText(lockFile);
                                    if (int.TryParse(lockContent, out var processId))
                                    {
                                        try
                                        {
                                            var process = System.Diagnostics.Process.GetProcessById(processId);
                                            if (!process.HasExited)
                                            {
                                                instance.IsActive = true;
                                                instance.ProcessId = processId;
                                            }
                                        }
                                        catch
                                        {
                                            // プロセスが存在しない場合
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"Failed to read lock file for instance {number}: {ex.Message}");
                                }
                            }

                            // キャッシュサイズを計算
                            try
                            {
                                var directoryInfo = new DirectoryInfo(dir);
                                if (directoryInfo.Exists)
                                {
                                    instance.CacheSize = GetDirectorySizeStatic(directoryInfo);
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Failed to calculate cache size for instance {number}: {ex.Message}");
                            }

                            instances.Add(instance);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetAllInstancesInfo error: {ex.Message}");
            }

            return instances;
        }

        /// <summary>
        /// ディレクトリサイズを計算（静的メソッド）
        /// </summary>
        /// <param name="directoryInfo">ディレクトリ情報</param>
        /// <returns>サイズ（バイト）</returns>
        private static long GetDirectorySizeStatic(DirectoryInfo directoryInfo)
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
                    size += GetDirectorySizeStatic(subDirectory);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error calculating directory size for {directoryInfo.FullName}: {ex.Message}");
            }

            return size;
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
                var instances = GetAllInstancesInfo();
                var inactiveInstances = instances.Where(i => !i.IsActive).ToList();

                foreach (var instance in inactiveInstances)
                {
                    try
                    {
                        if (Directory.Exists(instance.CachePath))
                        {
                            Directory.Delete(instance.CachePath, true);
                            Console.WriteLine($"Cleaned up inactive instance {instance.InstanceNumber}");
                            cleanedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to cleanup instance {instance.InstanceNumber}: {ex.Message}");
                    }
                }

                Console.WriteLine($"Cleanup completed: {cleanedCount} inactive instances removed");
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
                Console.WriteLine($"RequestContextManager disposed for instance {_instanceNumber}");
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