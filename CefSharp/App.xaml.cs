using System;
using System.IO;
using System.Windows;
using CefSharp;
using CefSharp.Wpf;
using CefSharp.fastBOT.Utils;

namespace CefSharp.fastBOT
{
    public partial class App : Application
    {
        public App()
        {
            InitializeCefSharp();
        }

        private void InitializeCefSharp()
        {
            var settings = new CefSettings();

            // CachePath設定（RootCachePathは削除され、CachePathを使用）
            string cachePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "fastBOT", "CefCache"
            );
            settings.CachePath = cachePath;

            // 基本設定
            settings.Locale = "ja";
            settings.AcceptLanguageList = "ja-JP,ja,en-US,en";
            settings.UserAgent = UserAgentHelper.GetChromeUserAgent();

            // Chrome Runtime（v138以降必須）
            // settings.ChromeRuntime = true; // デフォルトでtrue

            // パフォーマンス設定
            settings.CefCommandLineArgs.Add("--disable-gpu-vsync");
            settings.CefCommandLineArgs.Add("--max_old_space_size", "4096");

#if DEBUG
            settings.RemoteDebuggingPort = 8088;
            settings.LogSeverity = LogSeverity.Info;
#else
            settings.LogSeverity = LogSeverity.Error;
#endif
            Console.WriteLine($"fastBOT: CefSharp initialized with cache: {cachePath}");
            Console.WriteLine($"UserAgent: {settings.UserAgent}");

            // CefSharp初期化
            if (!Cef.Initialize(settings))
            {
                throw new InvalidOperationException("CefSharp initialization failed");
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
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Cef.Shutdown();
            base.OnExit(e);
        }
    }
}