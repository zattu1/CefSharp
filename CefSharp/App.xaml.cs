using CefSharp;
using CefSharp.fastBOT.Utils;
using CefSharp.Wpf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Management;
using System.Security.Cryptography;
using System.Text;
using System.Windows;

namespace CefSharp.fastBOT
{
    /// <summary>
    /// App.xaml の相互作用ロジック
    /// PC固有のキャッシュディレクトリでCEFを初期化（診断強化版）
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            InitializeCefSharp();
        }

        /// <summary>
        /// CefSharpを初期化（診断強化版）
        /// </summary>
        private void InitializeCefSharp()
        {
            try
            {
                Console.WriteLine("=== CefSharp Initialization Start ===");

                // PC固有の識別子を生成
                var pcIdentifier = GeneratePCIdentifier();
                var instanceNumber = GetNextAvailableInstanceNumber(pcIdentifier);

                // PC + インスタンス番号固有のベースディレクトリを設定
                var baseDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "fastBOT",
                    "BrowserData",
                    $"PC_{pcIdentifier}",
                    $"Instance_{instanceNumber:D2}"
                );

                // ディレクトリを作成
                Directory.CreateDirectory(baseDirectory);
                Console.WriteLine($"Cache directory created: {baseDirectory}");

                var settings = new CefSettings()
                {
                    // ベースディレクトリをルートキャッシュパスとして設定
                    CachePath = baseDirectory,

                    // 基本設定
                    Locale = "ja",
                    AcceptLanguageList = "ja-JP,ja,en-US,en",
                    UserAgent = UserAgentHelper.GetChromeUserAgent(),

                    // パフォーマンス設定
                    MultiThreadedMessageLoop = false,

                    // 診断用設定
                    LogSeverity = LogSeverity.Info,
                    LogFile = Path.Combine(baseDirectory, "cef_debug.log")
                };

                // 基本的なコマンドライン引数
                settings.CefCommandLineArgs.Add("--disable-gpu-vsync");
                settings.CefCommandLineArgs.Add("--max_old_space_size", "4096");
                settings.CefCommandLineArgs.Add("--enable-media-stream");
                settings.CefCommandLineArgs.Add("--enable-usermedia-screen-capturing");

                // 診断・デバッグ用のコマンドライン引数
                settings.CefCommandLineArgs.Add("--enable-logging");
                settings.CefCommandLineArgs.Add("--log-level", "0");
                settings.CefCommandLineArgs.Add("--v", "1");

                // セキュリティを緩和（開発時のみ）
                settings.CefCommandLineArgs.Add("--disable-web-security");
                settings.CefCommandLineArgs.Add("--disable-features", "VizDisplayCompositor");
                settings.CefCommandLineArgs.Add("--allow-running-insecure-content");

                // GPU関連の問題を回避
                settings.CefCommandLineArgs.Add("--disable-gpu");
                settings.CefCommandLineArgs.Add("--disable-gpu-compositing");
                settings.CefCommandLineArgs.Add("--disable-software-rasterizer");

#if DEBUG
                settings.RemoteDebuggingPort = 8088;
                Console.WriteLine("Remote debugging enabled on port 8088");
#endif

                Console.WriteLine($"fastBOT: CefSharp initializing with:");
                Console.WriteLine($"  PC identifier: {pcIdentifier}");
                Console.WriteLine($"  Instance number: {instanceNumber}");
                Console.WriteLine($"  Cache directory: {baseDirectory}");
                Console.WriteLine($"  UserAgent: {settings.UserAgent}");
                Console.WriteLine($"  Log file: {settings.LogFile}");

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

                // 初期化後の診断情報
                try
                {
                    Console.WriteLine($"Cef version: {Cef.CefVersion}");
                    Console.WriteLine($"Chromium version: {Cef.ChromiumVersion}");
                    Console.WriteLine($"CefSharp version: {Cef.CefSharpVersion}");
                }
                catch (Exception diagEx)
                {
                    Console.WriteLine($"Diagnostic info error: {diagEx.Message}");
                }

                // CefSharp初期化後にシステム情報を出力
                try
                {
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
        /// 利用可能なインスタンス番号を取得
        /// </summary>
        private int GetNextAvailableInstanceNumber(string pcIdentifier)
        {
            try
            {
                var currentProcessName = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
                var runningProcesses = System.Diagnostics.Process.GetProcessesByName(currentProcessName);

                Console.WriteLine($"Running {currentProcessName} processes: {runningProcesses.Length}");

                var pcBaseDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "fastBOT",
                    "BrowserData",
                    $"PC_{pcIdentifier}"
                );

                var usedNumbers = new HashSet<int>();

                if (Directory.Exists(pcBaseDirectory))
                {
                    var instanceDirectories = Directory.GetDirectories(pcBaseDirectory, "Instance_*");

                    foreach (var dir in instanceDirectories)
                    {
                        var dirName = Path.GetFileName(dir);
                        if (dirName.StartsWith("Instance_") && dirName.Length > 9)
                        {
                            var numberPart = dirName.Substring(9);
                            if (int.TryParse(numberPart, out var number))
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
                                                var process = System.Diagnostics.Process.GetProcessById(lockedProcessId);
                                                if (process.ProcessName == currentProcessName)
                                                {
                                                    usedNumbers.Add(number);
                                                    Console.WriteLine($"Instance {number} is locked by process {lockedProcessId}");
                                                }
                                                else
                                                {
                                                    File.Delete(lockFile);
                                                    Console.WriteLine($"Removed stale lock file for instance {number}");
                                                }
                                            }
                                            catch (ArgumentException)
                                            {
                                                File.Delete(lockFile);
                                                Console.WriteLine($"Removed orphaned lock file for instance {number}");
                                            }
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
                }

                for (int i = 1; i <= 99; i++)
                {
                    if (!usedNumbers.Contains(i))
                    {
                        CreateInstanceLockFile(pcIdentifier, i);
                        return i;
                    }
                }

                return runningProcesses.Length;
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
        private void CreateInstanceLockFile(string pcIdentifier, int instanceNumber)
        {
            try
            {
                var instanceDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "fastBOT",
                    "BrowserData",
                    $"PC_{pcIdentifier}",
                    $"Instance_{instanceNumber:D2}"
                );

                Directory.CreateDirectory(instanceDirectory);

                var lockFile = Path.Combine(instanceDirectory, "instance.lock");
                var currentProcessId = System.Diagnostics.Process.GetCurrentProcess().Id;

                File.WriteAllText(lockFile, currentProcessId.ToString());
                Console.WriteLine($"Created lock file for instance {instanceNumber}: {lockFile}");

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
        /// アプリケーション終了時の処理
        /// </summary>
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

        /// <summary>
        /// PC固有の識別子を生成
        /// </summary>
        private string GeneratePCIdentifier()
        {
            try
            {
                var identifierParts = new System.Collections.Generic.List<string>();

                identifierParts.Add(Environment.MachineName);

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
                catch (Exception ex)
                {
                    Console.WriteLine($"プロセッサID取得エラー: {ex.Message}");
                }

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
                catch (Exception ex)
                {
                    Console.WriteLine($"マザーボードシリアル番号取得エラー: {ex.Message}");
                }

                identifierParts.Add(Environment.UserName);

                var combinedString = string.Join("-", identifierParts);
                Console.WriteLine($"PC識別情報: {combinedString}");

                using (var sha256 = SHA256.Create())
                {
                    var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(combinedString));
                    var hashString = Convert.ToBase64String(hashBytes)
                        .Replace("/", "_")
                        .Replace("+", "-")
                        .Replace("=", "")
                        .Substring(0, 16);

                    return hashString;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"PC識別子生成エラー: {ex.Message}");
                var fallbackString = $"{Environment.MachineName}-{Environment.UserName}";
                Console.WriteLine($"フォールバック識別情報: {fallbackString}");

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
    }
}