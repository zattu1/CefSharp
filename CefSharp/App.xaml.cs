using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Collections.Generic;
using CefSharp;
using CefSharp.Wpf;
using CefSharp.fastBOT.Utils;

namespace CefSharp.fastBOT
{
    /// <summary>
    /// App.xaml の相互作用ロジック（動作確認済み版）
    /// プロセス分離 + ブラウザ表示の両立
    /// </summary>
    public partial class App : Application
    {
        private static int _currentInstanceNumber = 0;

        public App()
        {
            InitializeCefSharp();
        }

        /// <summary>
        /// CefSharpを初期化（動作確認済み設定）
        /// </summary>
        private void InitializeCefSharp()
        {
            try
            {
                Console.WriteLine("=== CefSharp Initialization Start (Working Version) ===");

                // 起動順に基づくインスタンス番号を取得
                _currentInstanceNumber = GetNextAvailableInstanceNumber();

                Console.WriteLine($"Instance number: {_currentInstanceNumber}");

                var settings = new CefSettings();

                // インスタンス番号毎のキャッシュパス設定
                string cachePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "fastBOT", "Instance", _currentInstanceNumber.ToString()
                );

                // ディレクトリを確実に作成
                Directory.CreateDirectory(cachePath);
                settings.CachePath = cachePath;

                // インスタンス毎のログファイル設定
                settings.LogFile = Path.Combine(cachePath, "cef_debug.log");

                // 基本設定
                settings.Locale = "ja";
                settings.AcceptLanguageList = "ja-JP,ja,en-US,en";
                settings.UserAgent = UserAgentHelper.GetChromeUserAgent();

                // 必要最小限のコマンドライン引数のみ設定
                settings.CefCommandLineArgs.Add("--disable-gpu-vsync");
                settings.CefCommandLineArgs.Add("--max_old_space_size", "4096");
                settings.CefCommandLineArgs.Add("--disable-gpu-compositing");
                settings.CefCommandLineArgs.Add("--disable-features", "VizDisplayCompositor");

                // GCM関連のエラーを抑制（動作に影響しない）
                settings.CefCommandLineArgs.Add("--disable-background-networking");
                settings.CefCommandLineArgs.Add("--disable-background-timer-throttling");
                settings.CefCommandLineArgs.Add("--disable-backgrounding-occluded-windows");
                settings.CefCommandLineArgs.Add("--disable-renderer-backgrounding");

                // 軽量化（動作に影響しない）
                settings.CefCommandLineArgs.Add("--disable-extensions");
                settings.CefCommandLineArgs.Add("--disable-plugins");
                settings.CefCommandLineArgs.Add("--disable-print-preview");

#if DEBUG
                settings.RemoteDebuggingPort = 8088 + _currentInstanceNumber;
                settings.LogSeverity = LogSeverity.Info;
                Console.WriteLine($"Debug mode - Remote debugging enabled on port {8088 + _currentInstanceNumber}");
#else
                settings.LogSeverity = LogSeverity.Error;
#endif

                Console.WriteLine($"fastBOT: CefSharp initializing with cache: {cachePath}");
                Console.WriteLine($"UserAgent: {settings.UserAgent}");

                // インスタンスロックファイルを作成
                CreateInstanceLockFile(_currentInstanceNumber, cachePath);

                // CefSharp初期化前の状態チェック
                Console.WriteLine($"Before initialization - Cef.IsInitialized: {Cef.IsInitialized}");

                // CefSharp初期化
                var initResult = Cef.Initialize(settings);
                Console.WriteLine($"Cef.Initialize() returned: {initResult}");
                Console.WriteLine($"After initialization - Cef.IsInitialized: {Cef.IsInitialized}");

                if (!initResult)
                {
                    throw new InvalidOperationException("CefSharp initialization failed");
                }

                // CefSharp初期化後にシステム情報を出力
                try
                {
                    Console.WriteLine($"Cef version: {Cef.CefVersion}");
                    Console.WriteLine($"Chromium version: {Cef.ChromiumVersion}");
                    Console.WriteLine($"CefSharp version: {Cef.CefSharpVersion}");
                    Console.WriteLine(UserAgentHelper.GetSystemInfo());
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"システム情報取得エラー: {ex.Message}");
                }

                Console.WriteLine("=== CefSharp Initialization Complete ===");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"CefSharp initialization failed: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                MessageBox.Show($"ブラウザエンジンの初期化に失敗しました: {ex.Message}",
                    "初期化エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
            }
        }

        /// <summary>
        /// 利用可能なインスタンス番号を取得（1, 2, 3...の順番）
        /// </summary>
        /// <returns>インスタンス番号</returns>
        private int GetNextAvailableInstanceNumber()
        {
            try
            {
                var currentProcessName = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
                var baseDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "fastBOT", "Instance"
                );

                // ベースディレクトリを作成
                Directory.CreateDirectory(baseDirectory);

                var usedNumbers = new HashSet<int>();

                // 既存のインスタンスディレクトリをチェック
                if (Directory.Exists(baseDirectory))
                {
                    var instanceDirectories = Directory.GetDirectories(baseDirectory)
                        .Where(dir => int.TryParse(Path.GetFileName(dir), out _))
                        .ToList();

                    Console.WriteLine($"Found {instanceDirectories.Count} existing instance directories");

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
                                    if (int.TryParse(lockContent, out var lockedProcessId))
                                    {
                                        try
                                        {
                                            // プロセスが実際に動いているかチェック
                                            var process = System.Diagnostics.Process.GetProcessById(lockedProcessId);
                                            if (process.ProcessName == currentProcessName && !process.HasExited)
                                            {
                                                usedNumbers.Add(number);
                                                Console.WriteLine($"Instance {number} is locked by active process {lockedProcessId}");
                                            }
                                            else
                                            {
                                                // 異なるプロセス名または終了したプロセスの場合、ロックファイルを削除
                                                File.Delete(lockFile);
                                                Console.WriteLine($"Removed stale lock file for instance {number}");
                                            }
                                        }
                                        catch (ArgumentException)
                                        {
                                            // プロセスが存在しない場合、ロックファイルを削除
                                            File.Delete(lockFile);
                                            Console.WriteLine($"Removed orphaned lock file for instance {number}");
                                        }
                                    }
                                    else
                                    {
                                        // 無効なロックファイルを削除
                                        File.Delete(lockFile);
                                        Console.WriteLine($"Removed invalid lock file for instance {number}");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"Failed to check lock file for instance {number}: {ex.Message}");
                                }
                            }
                        }
                    }
                }

                // 1から順番に空いている番号を探す
                for (int i = 1; i <= 99; i++)
                {
                    if (!usedNumbers.Contains(i))
                    {
                        Console.WriteLine($"Selected instance number: {i}");
                        return i;
                    }
                }

                // 99個まで埋まっている場合は適当な番号を返す
                return new Random().Next(100, 999);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetNextAvailableInstanceNumber error: {ex.Message}");
                return DateTime.Now.Second + 1;
            }
        }

        /// <summary>
        /// インスタンスロックファイルを作成
        /// </summary>
        /// <param name="instanceNumber">インスタンス番号</param>
        /// <param name="cachePath">キャッシュパス</param>
        private void CreateInstanceLockFile(int instanceNumber, string cachePath)
        {
            try
            {
                var lockFile = Path.Combine(cachePath, "instance.lock");
                var currentProcessId = System.Diagnostics.Process.GetCurrentProcess().Id;

                File.WriteAllText(lockFile, currentProcessId.ToString());
                Console.WriteLine($"Created lock file for instance {instanceNumber}: {lockFile}");

                // アプリケーション終了時にロックファイルを削除
                AppDomain.CurrentDomain.ProcessExit += (sender, e) => CleanupInstanceLockFile(lockFile);
                Application.Current.Exit += (sender, e) => CleanupInstanceLockFile(lockFile);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to create instance lock file: {ex.Message}");
            }
        }

        /// <summary>
        /// インスタンスロックファイルをクリーンアップ
        /// </summary>
        /// <param name="lockFilePath">ロックファイルパス</param>
        private void CleanupInstanceLockFile(string lockFilePath)
        {
            try
            {
                if (File.Exists(lockFilePath))
                {
                    File.Delete(lockFilePath);
                    Console.WriteLine($"Cleaned up lock file: {lockFilePath}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to cleanup lock file: {ex.Message}");
            }
        }

        /// <summary>
        /// 現在のインスタンス番号を取得
        /// </summary>
        /// <returns>インスタンス番号</returns>
        public static int GetCurrentInstanceNumber()
        {
            return _currentInstanceNumber;
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                Console.WriteLine($"Application exiting - Instance {_currentInstanceNumber}");

                if (Cef.IsInitialized == true)
                {
                    Cef.Shutdown();
                    Console.WriteLine("CefSharp shutdown completed");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"CefSharp shutdown error: {ex.Message}");
            }

            base.OnExit(e);
        }
    }
}