using System;
using System.Collections.Generic;

namespace CefSharp.fastBOT.Models
{
    /// <summary>
    /// ページ情報を格納するモデル
    /// </summary>
    public class PageInfo
    {
        /// <summary>
        /// ページタイトル
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// ページURL
        /// </summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// ページの読み込み状態
        /// </summary>
        public bool IsLoading { get; set; } = false;

        /// <summary>
        /// ページの読み込み進捗（0-100）
        /// </summary>
        public int LoadProgress { get; set; } = 0;

        /// <summary>
        /// ページのHTMLサイズ（文字数）
        /// </summary>
        public int HtmlSize { get; set; } = 0;

        /// <summary>
        /// ページに含まれるリンク数
        /// </summary>
        public int LinkCount { get; set; } = 0;

        /// <summary>
        /// ページに含まれるフォーム数
        /// </summary>
        public int FormCount { get; set; } = 0;

        /// <summary>
        /// ページに含まれる画像数
        /// </summary>
        public int ImageCount { get; set; } = 0;

        /// <summary>
        /// ページの言語設定
        /// </summary>
        public string Language { get; set; } = string.Empty;

        /// <summary>
        /// ページの文字エンコーディング
        /// </summary>
        public string Encoding { get; set; } = string.Empty;

        /// <summary>
        /// ページの最終更新日時
        /// </summary>
        public DateTime? LastModified { get; set; }

        /// <summary>
        /// ページ解析日時
        /// </summary>
        public DateTime AnalyzedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// 検出されたチケット情報
        /// </summary>
        public List<TicketInfo> TicketInfos { get; set; } = new List<TicketInfo>();

        /// <summary>
        /// ページエラー情報
        /// </summary>
        public List<string> Errors { get; set; } = new List<string>();

        /// <summary>
        /// メタデータ情報
        /// </summary>
        public Dictionary<string, string> MetaData { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// フォーム要素情報
        /// </summary>
        public List<FormElementInfo> FormElements { get; set; } = new List<FormElementInfo>();

        /// <summary>
        /// ページのスクリーンショット（Base64エンコード）
        /// </summary>
        public string Screenshot { get; set; } = string.Empty;

        /// <summary>
        /// ページ情報を文字列として取得
        /// </summary>
        /// <returns>ページ情報の概要</returns>
        public override string ToString()
        {
            return $"PageInfo: {Title} - {Url} ({HtmlSize:N0} chars, {LinkCount} links, {FormCount} forms)";
        }
    }

    /// <summary>
    /// チケット情報
    /// </summary>
    public class TicketInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Price { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
        public string Venue { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public bool IsAvailable { get; set; } = false;
        public string Selector { get; set; } = string.Empty;
    }

    /// <summary>
    /// フォーム要素情報
    /// </summary>
    public class FormElementInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Placeholder { get; set; } = string.Empty;
        public bool IsRequired { get; set; } = false;
        public string Selector { get; set; } = string.Empty;
    }
}