using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using CefSharp.fastBOT.Models;

namespace CefSharp.fastBOT.Utils
{
    /// <summary>
    /// HTMLデータの管理、保存、履歴管理を行うクラス
    /// </summary>
    public class HtmlDataManager
    {
        private readonly string _baseDirectory;
        private readonly List<HtmlData> _htmlHistory;
        private const int MAX_HISTORY_COUNT = 100;

        public HtmlDataManager(string baseDirectory = null)
        {
            _baseDirectory = baseDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "fastBOT_HTML");
            _htmlHistory = new List<HtmlData>();

            EnsureDirectoryExists();
        }

        /// <summary>
        /// HTMLデータを保存
        /// </summary>
        /// <param name="htmlContent">HTMLコンテンツ</param>
        /// <param name="pageInfo">ページ情報</param>
        /// <param name="dataType">データタイプ</param>
        /// <param name="customFileName">カスタムファイル名（オプション）</param>
        /// <returns>保存されたファイルパス</returns>
        public async Task<string> SaveHtmlDataAsync(string htmlContent, PageInfo pageInfo = null, HtmlDataType dataType = HtmlDataType.FullPage, string customFileName = null)
        {
            try
            {
                if (string.IsNullOrEmpty(htmlContent))
                    throw new ArgumentException("HTMLコンテンツが空です", nameof(htmlContent));

                var htmlData = new HtmlData
                {
                    Id = Guid.NewGuid().ToString(),
                    Content = htmlContent,
                    PageInfo = pageInfo ?? new PageInfo(),
                    DataType = dataType,
                    CapturedAt = DateTime.Now,
                    Size = System.Text.Encoding.UTF8.GetByteCount(htmlContent)
                };

                // ファイル名を生成
                var fileName = customFileName ?? GenerateFileName(htmlData);
                var filePath = Path.Combine(_baseDirectory, fileName);

                // HTMLファイルを保存
                await File.WriteAllTextAsync(filePath, htmlContent, System.Text.Encoding.UTF8);

                // メタデータを保存
                var metadataPath = Path.ChangeExtension(filePath, ".json");
                await SaveMetadataAsync(htmlData, metadataPath);

                // 履歴に追加
                AddToHistory(htmlData, filePath);

                Console.WriteLine($"HTML data saved: {fileName}");
                return filePath;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SaveHtmlDataAsync error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 複数のHTMLデータを一括保存
        /// </summary>
        /// <param name="htmlDataList">HTMLデータのリスト</param>
        /// <returns>保存されたファイルパスのリスト</returns>
        public async Task<List<string>> SaveMultipleHtmlDataAsync(List<(string content, PageInfo pageInfo, HtmlDataType dataType, string customName)> htmlDataList)
        {
            var savedPaths = new List<string>();

            foreach (var (content, pageInfo, dataType, customName) in htmlDataList)
            {
                try
                {
                    var path = await SaveHtmlDataAsync(content, pageInfo, dataType, customName);
                    savedPaths.Add(path);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to save HTML data: {ex.Message}");
                }
            }

            return savedPaths;
        }

        /// <summary>
        /// HTMLデータを読み込み
        /// </summary>
        /// <param name="filePath">ファイルパス</param>
        /// <returns>HTMLデータ</returns>
        public async Task<HtmlData> LoadHtmlDataAsync(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    throw new FileNotFoundException($"ファイルが見つかりません: {filePath}");

                var content = await File.ReadAllTextAsync(filePath, System.Text.Encoding.UTF8);
                var metadataPath = Path.ChangeExtension(filePath, ".json");

                HtmlData htmlData = null;
                if (File.Exists(metadataPath))
                {
                    var metadataJson = await File.ReadAllTextAsync(metadataPath, System.Text.Encoding.UTF8);
                    htmlData = JsonSerializer.Deserialize<HtmlData>(metadataJson);
                    htmlData.Content = content;
                }
                else
                {
                    // メタデータがない場合は基本情報のみ
                    htmlData = new HtmlData
                    {
                        Id = Path.GetFileNameWithoutExtension(filePath),
                        Content = content,
                        DataType = HtmlDataType.FullPage,
                        CapturedAt = File.GetCreationTime(filePath),
                        Size = System.Text.Encoding.UTF8.GetByteCount(content)
                    };
                }

                return htmlData;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"LoadHtmlDataAsync error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// HTMLデータの履歴を取得
        /// </summary>
        /// <returns>履歴リスト</returns>
        public List<HtmlData> GetHistory()
        {
            return new List<HtmlData>(_htmlHistory);
        }

        /// <summary>
        /// 保存されたHTMLファイルの一覧を取得
        /// </summary>
        /// <returns>ファイル情報のリスト</returns>
        public List<HtmlFileInfo> GetSavedFiles()
        {
            try
            {
                var files = new List<HtmlFileInfo>();

                if (!Directory.Exists(_baseDirectory))
                    return files;

                var htmlFiles = Directory.GetFiles(_baseDirectory, "*.html");

                foreach (var file in htmlFiles)
                {
                    var fileInfo = new FileInfo(file);
                    var metadataPath = Path.ChangeExtension(file, ".json");

                    var htmlFileInfo = new HtmlFileInfo
                    {
                        FileName = fileInfo.Name,
                        FilePath = file,
                        Size = fileInfo.Length,
                        CreatedAt = fileInfo.CreationTime,
                        ModifiedAt = fileInfo.LastWriteTime
                    };

                    // メタデータがあれば読み込み
                    if (File.Exists(metadataPath))
                    {
                        try
                        {
                            var metadataJson = File.ReadAllText(metadataPath);
                            var metadata = JsonSerializer.Deserialize<HtmlData>(metadataJson);
                            htmlFileInfo.Title = metadata.PageInfo?.Title;
                            htmlFileInfo.Url = metadata.PageInfo?.Url;
                            htmlFileInfo.DataType = metadata.DataType;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Failed to read metadata for {file}: {ex.Message}");
                        }
                    }

                    files.Add(htmlFileInfo);
                }

                files.Sort((a, b) => b.CreatedAt.CompareTo(a.CreatedAt));
                return files;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetSavedFiles error: {ex.Message}");
                return new List<HtmlFileInfo>();
            }
        }

        /// <summary>
        /// 古いファイルを削除
        /// </summary>
        /// <param name="daysOld">削除対象の日数</param>
        /// <returns>削除されたファイル数</returns>
        public int CleanupOldFiles(int daysOld = 30)
        {
            try
            {
                if (!Directory.Exists(_baseDirectory))
                    return 0;

                var cutoffDate = DateTime.Now.AddDays(-daysOld);
                var deletedCount = 0;

                var files = Directory.GetFiles(_baseDirectory);

                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.CreationTime < cutoffDate)
                    {
                        try
                        {
                            File.Delete(file);
                            deletedCount++;
                            Console.WriteLine($"Deleted old file: {fileInfo.Name}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Failed to delete {file}: {ex.Message}");
                        }
                    }
                }

                return deletedCount;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"CleanupOldFiles error: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// HTMLコンテンツを比較
        /// </summary>
        /// <param name="content1">コンテンツ1</param>
        /// <param name="content2">コンテンツ2</param>
        /// <returns>比較結果</returns>
        public HtmlComparisonResult CompareHtml(string content1, string content2)
        {
            try
            {
                var result = new HtmlComparisonResult
                {
                    AreIdentical = content1 == content2,
                    SizeDifference = Math.Abs(content1.Length - content2.Length),
                    SimilarityPercentage = CalculateSimilarity(content1, content2) * 100
                };

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"CompareHtml error: {ex.Message}");
                return new HtmlComparisonResult { AreIdentical = false };
            }
        }

        /// <summary>
        /// ディレクトリが存在することを確認
        /// </summary>
        private void EnsureDirectoryExists()
        {
            try
            {
                if (!Directory.Exists(_baseDirectory))
                {
                    Directory.CreateDirectory(_baseDirectory);
                    Console.WriteLine($"Created directory: {_baseDirectory}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"EnsureDirectoryExists error: {ex.Message}");
            }
        }

        /// <summary>
        /// ファイル名を生成
        /// </summary>
        /// <param name="htmlData">HTMLデータ</param>
        /// <returns>ファイル名</returns>
        private string GenerateFileName(HtmlData htmlData)
        {
            var timestamp = htmlData.CapturedAt.ToString("yyyyMMdd_HHmmss");
            var typePrefix = htmlData.DataType.ToString().ToLower();

            var domain = "";
            if (!string.IsNullOrEmpty(htmlData.PageInfo?.Url))
            {
                try
                {
                    var uri = new Uri(htmlData.PageInfo.Url);
                    domain = $"_{uri.Host.Replace(".", "_")}";
                }
                catch
                {
                    // URI解析に失敗した場合は無視
                }
            }

            return $"{typePrefix}_{timestamp}{domain}.html";
        }

        /// <summary>
        /// メタデータを保存
        /// </summary>
        /// <param name="htmlData">HTMLデータ</param>
        /// <param name="metadataPath">メタデータファイルパス</param>
        private async Task SaveMetadataAsync(HtmlData htmlData, string metadataPath)
        {
            try
            {
                var metadataToSave = new HtmlData
                {
                    Id = htmlData.Id,
                    PageInfo = htmlData.PageInfo,
                    DataType = htmlData.DataType,
                    CapturedAt = htmlData.CapturedAt,
                    Size = htmlData.Size,
                    Selector = htmlData.Selector
                    // Contentは除外（別ファイルに保存済み）
                };

                var json = JsonSerializer.Serialize(metadataToSave, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });

                await File.WriteAllTextAsync(metadataPath, json, System.Text.Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SaveMetadataAsync error: {ex.Message}");
            }
        }

        /// <summary>
        /// 履歴に追加
        /// </summary>
        /// <param name="htmlData">HTMLデータ</param>
        /// <param name="filePath">ファイルパス</param>
        private void AddToHistory(HtmlData htmlData, string filePath)
        {
            try
            {
                htmlData.FilePath = filePath;
                _htmlHistory.Insert(0, htmlData);

                // 履歴の上限を維持
                if (_htmlHistory.Count > MAX_HISTORY_COUNT)
                {
                    _htmlHistory.RemoveAt(_htmlHistory.Count - 1);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"AddToHistory error: {ex.Message}");
            }
        }

        /// <summary>
        /// 文字列の類似度を計算（簡易版）
        /// </summary>
        /// <param name="str1">文字列1</param>
        /// <param name="str2">文字列2</param>
        /// <returns>類似度（0.0-1.0）</returns>
        private double CalculateSimilarity(string str1, string str2)
        {
            try
            {
                if (str1 == str2) return 1.0;
                if (string.IsNullOrEmpty(str1) || string.IsNullOrEmpty(str2)) return 0.0;

                var maxLength = Math.Max(str1.Length, str2.Length);
                var minLength = Math.Min(str1.Length, str2.Length);

                var commonChars = 0;
                for (int i = 0; i < minLength; i++)
                {
                    if (str1[i] == str2[i])
                        commonChars++;
                }

                return (double)commonChars / maxLength;
            }
            catch
            {
                return 0.0;
            }
        }
    }
}