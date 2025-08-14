using Newtonsoft.Json;
using System;
using System.IO;

namespace CefSharp.fastBOT.Models
{
    public class UserSettings
    {
        public string LoginId { get; set; }
        public string Password { get; set; }
        public string LastName { get; set; }
        public string FirstName { get; set; }
        public string LastKana { get; set; }
        public string FirstKana { get; set; }
        public string Tel1 { get; set; }
        public string Tel2 { get; set; }
        public string Tel3 { get; set; }
        public string Email { get; set; }
        public string CardNumber { get; set; }
        public string CVV { get; set; }
        public string Month { get; set; }
        public string Year { get; set; }
        public string CardName { get; set; }
        public string DefaultProxy { get; set; }
        public int CheckInterval { get; set; } = 2000;

        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "fastBOT", "settings.json");

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
                var json = JsonConvert.SerializeObject(this, Formatting.Indented);
                File.WriteAllText(SettingsPath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"設定保存エラー: {ex.Message}");
            }
        }

        public static UserSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    return JsonConvert.DeserializeObject<UserSettings>(json);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"設定読み込みエラー: {ex.Message}");
            }

            return new UserSettings();
        }
    }
}