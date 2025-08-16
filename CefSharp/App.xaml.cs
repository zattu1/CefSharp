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
    /// App.xaml の相互作用ロジック（修正版）
    /// インスタンス番号ベースのプロセス分離キャッシュ管理
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            InitializeCefSharp();
        }

        /// <summary>
        /// CefSharpを初期化（プロセス毎のキャッシュ管理版）
        /// </summary>
        private void InitializeCefSharp()
        {
            try
            {
                Console.WriteLine("=== CefSharp Initialization Start (Process-Separated Cache) ===");

                // 起動順に基づくインスタンス番号を取得
                var instanceNumber = GetNextAvailableInstanceNumber();

                Console.WriteLine($"Instance number: {instanceNumber}");

                var settings = new CefSettings();

                // インスタンス番号毎のキャッシュパス設定
                string cachePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "fastBOT", "Instance", instanceNumber.ToString()
                );
                settings.CachePath = cachePath;

                // インスタンス毎のログファイル設定
                settings.LogFile = Path.Combine(cachePath, "cef_debug.log");

                // 基本設定
                settings.Locale = "ja";
                settings.AcceptLanguageList = "ja-JP,ja,en-US,en";
                settings.UserAgent = UserAgentHelper.GetChromeUserAgent();

                // パフォーマンス設定（最小限）
                settings.CefCommandLineArgs.Add("--disable-gpu-vsync");
                settings.CefCommandLineArgs.Add("--max_old_space_size", "4096");

                // 安定性を重視した設定
                settings.CefCommandLineArgs.Add("--disable-gpu-compositing");
                settings.CefCommandLineArgs.Add("--disable-features", "VizDisplayCompositor");

                // GCM関連のエラーを抑制
                settings.CefCommandLineArgs.Add("--disable-background-networking");
                settings.CefCommandLineArgs.Add("--disable-background-timer-throttling");
                settings.CefCommandLineArgs.Add("--disable-backgrounding-occluded-windows");
                settings.CefCommandLineArgs.Add("--disable-renderer-backgrounding");

                // その他の不要な機能を無効化
                settings.CefCommandLineArgs.Add("--disable-extensions");
                settings.CefCommandLineArgs.Add("--disable-plugins");
                settings.CefCommandLineArgs.Add("--disable-print-preview");

#if DEBUG
                settings.RemoteDebuggingPort = 8088 + instanceNumber; // インスタンス毎に異なるポート
                settings.LogSeverity = LogSeverity.Info;
                Console.WriteLine($"Debug mode - Remote debugging enabled on port {8088 + instanceNumber}");
#else
                settings.LogSeverity = LogSeverity.Error;
#endif

                Console.WriteLine($"fastBOT: CefSharp initializing with cache: {cachePath}");
                Console.WriteLine($"UserAgent: {settings.UserAgent}");

                // インスタンスロックファイルを作成
                CreateInstanceLockFile(instanceNumber);

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
        private void CreateInstanceLockFile(int instanceNumber)
        {
            try
            {
                var instanceDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "fastBOT", "Instance", instanceNumber.ToString()
                );

                Directory.CreateDirectory(instanceDirectory);

                var lockFile = Path.Combine(instanceDirectory, "instance.lock");
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

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
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