using CefSharp.fastBOT.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Shapes;

namespace CefSharp.fastBOT
{
    public class CommonSettings
    {
        /// <summary>
        /// キャッシュパスを取得
        /// </summary>
        /// <param name="instanceNumber">インスタンス番号</param>
        /// <returns>キャッシュパス</returns>
        public static string GetCachePath(int instanceNumber)
        {
            var localDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var cachePath = System.IO.Path.Combine(localDataPath,
                "fastBOT", "Instance", $"instance_{instanceNumber}");
            return cachePath;
        }

        /// <summary>
        /// Baseキャッシュパスを取得
        /// </summary>
        /// <returns>キャッシュパス</returns>
        public static string GetCachePath()
        {
            return System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "fastBOT", "Instance");
        }

        /// <summary>
        /// 現在のインスタンス番号を取得
        /// </summary>
        /// <returns>インスタンス番号</returns>
        public static int GetCurrentInstanceNumber()
        {
            try
            {
                var currentProcessId = System.Diagnostics.Process.GetCurrentProcess().Id;
                var baseDirectory = GetCachePath();

                if (!Directory.Exists(baseDirectory))
                {
                    Directory.CreateDirectory(baseDirectory);
                }

                // 既存のインスタンスディレクトリをチェック
                var instanceDirectories = Directory.GetDirectories(baseDirectory)
                    .Where(dir => System.IO.Path.GetFileName(dir).StartsWith("instance_"))
                    .ToList();

                foreach (var dir in instanceDirectories)
                {
                    var dirName = System.IO.Path.GetFileName(dir);
                    if (dirName.StartsWith("instance_") && int.TryParse(dirName.Substring(1), out var number))
                    {
                        var lockFile = System.IO.Path.Combine(dir, "instance.lock");
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

                return 1; // デフォルト
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetCurrentInstanceNumber error: {ex.Message}");
                return 1;
            }
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
                var baseDirectory = System.IO.Path.Combine(CommonSettings.GetCachePath(), "fastBOT");

                if (Directory.Exists(baseDirectory))
                {
                    var instanceDirectories = Directory.GetDirectories(baseDirectory)
                        .Where(dir => System.IO.Path.GetFileName(dir).StartsWith("instance_"))
                        .OrderBy(dir =>
                        {
                            var name = System.IO.Path.GetFileName(dir);
                            return int.TryParse(name.Substring(1), out var num) ? num : 999;
                        })
                        .ToList();

                    foreach (var dir in instanceDirectories)
                    {
                        var dirName = System.IO.Path.GetFileName(dir);
                        if (dirName.StartsWith("instance_") && int.TryParse(dirName.Substring(1), out var number))
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
                            var lockFile = System.IO.Path.Combine(dir, "instance.lock");
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
    }
}