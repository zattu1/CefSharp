using System;
using CefSharp;

namespace CefSharp.fastBOT.Utils
{
    /// <summary>
    /// UserAgent生成ヘルパークラス
    /// </summary>
    public static class UserAgentHelper
    {
        /// <summary>
        /// 現在の環境に基づいてChrome互換のUserAgentを生成
        /// </summary>
        /// <returns>Chrome互換UserAgent文字列</returns>
        public static string GetChromeUserAgent()
        {
            // Windows バージョンを取得
            var windowsVersion = GetWindowsVersion();

            // CefSharp/Chromiumのバージョンを取得
            var chromeVersion = GetChromeVersion();

            return $"Mozilla/5.0 (Windows NT {windowsVersion}; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/{chromeVersion} Safari/537.36";
        }

        /// <summary>
        /// Windowsのバージョンを取得
        /// </summary>
        /// <returns>Windows NTバージョン（例: "10.0", "11.0"）</returns>
        private static string GetWindowsVersion()
        {
            try
            {
                var version = Environment.OSVersion.Version;

                // Windows 11の判定（Build 22000以上）
                if (version.Major == 10 && version.Build >= 22000)
                {
                    return "11.0";
                }
                // Windows 10
                else if (version.Major == 10)
                {
                    return "10.0";
                }
                // Windows 8.1
                else if (version.Major == 6 && version.Minor == 3)
                {
                    return "6.3";
                }
                // Windows 8
                else if (version.Major == 6 && version.Minor == 2)
                {
                    return "6.2";
                }
                // Windows 7
                else if (version.Major == 6 && version.Minor == 1)
                {
                    return "6.1";
                }
                // その他（Windows 10として扱う）
                else
                {
                    return "10.0";
                }
            }
            catch
            {
                // エラーの場合はWindows 10として扱う
                return "10.0";
            }
        }

        /// <summary>
        /// CefSharp/ChromiumのバージョンからChromeバージョンを取得
        /// </summary>
        /// <returns>Chromeバージョン（例: "138.0.0.0"）</returns>
        private static string GetChromeVersion()
        {
            try
            {
                // CefSharpが初期化されているかチェック
                if (Cef.IsInitialized != true)
                {
                    return "138.0.0.0"; // デフォルト値
                }

                // CefSharpのバージョンを取得
                var cefVersion = Cef.CefSharpVersion;

                // CefSharpのバージョンからChromiumバージョンを推定
                // CefSharp 138.x.x は Chrome 138.x.x に対応
                if (!string.IsNullOrEmpty(cefVersion))
                {
                    var parts = cefVersion.Split('.');
                    if (parts.Length >= 3)
                    {
                        return $"{parts[0]}.{parts[1]}.{parts[2]}.0";
                    }
                }

                // フォールバック: 固定バージョン
                return "138.0.0.0";
            }
            catch
            {
                // エラーの場合は固定バージョンを返す
                return "138.0.0.0";
            }
        }

        /// <summary>
        /// Chromiumのバージョン情報を取得
        /// </summary>
        /// <returns>Chromiumバージョン情報</returns>
        public static string GetChromiumVersion()
        {
            try
            {
                if (Cef.IsInitialized != true)
                {
                    return "未初期化";
                }
                return Cef.ChromiumVersion;
            }
            catch
            {
                return "不明";
            }
        }

        /// <summary>
        /// CefSharpのバージョン情報を取得
        /// </summary>
        /// <returns>CefSharpバージョン情報</returns>
        public static string GetCefSharpVersion()
        {
            try
            {
                if (Cef.IsInitialized != true)
                {
                    return "未初期化";
                }
                return Cef.CefSharpVersion;
            }
            catch
            {
                return "不明";
            }
        }

        /// <summary>
        /// システム情報を含むUserAgent情報を取得
        /// </summary>
        /// <returns>システム情報</returns>
        public static string GetSystemInfo()
        {
            try
            {
                var windowsVersion = GetWindowsVersion();
                var chromeVersion = GetChromeVersion();
                var chromiumVersion = GetChromiumVersion();
                var cefSharpVersion = GetCefSharpVersion();

                return $@"System Information:
Windows Version: NT {windowsVersion}
Chrome Version: {chromeVersion}
Chromium Version: {chromiumVersion}
CefSharp Version: {cefSharpVersion}
Generated UserAgent: {GetChromeUserAgent()}";
            }
            catch (Exception ex)
            {
                return $"システム情報取得エラー: {ex.Message}";
            }
        }
    }
}