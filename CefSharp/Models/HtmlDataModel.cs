using System;
using System.Collections.Generic;

namespace CefSharp.fastBOT.Models
{
    /// <summary>
    /// HTMLデータの種類を表す列挙型
    /// </summary>
    public enum HtmlDataType
    {
        /// <summary>
        /// ページ全体のHTML
        /// </summary>
        FullPage,

        /// <summary>
        /// Body部分のHTMLのみ
        /// </summary>
        BodyOnly,

        /// <summary>
        /// 指定した要素のHTML
        /// </summary>
        Element,

        /// <summary>
        /// テキストのみ（HTMLタグ除去）
        /// </summary>
        TextOnly,

        /// <summary>
        /// カスタムタイプ
        /// </summary>
        Custom
    }

    /// <summary>
    /// 抽出されたHTMLデータを格納するクラス
    /// </summary>
    public class HtmlData
    {
        /// <summary>
        /// 一意識別子
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// 抽出されたHTMLコンテンツ
        /// </summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// ページ情報
        /// </summary>
        public PageInfo PageInfo { get; set; } = new PageInfo();

        /// <summary>
        /// データの種類
        /// </summary>
        public HtmlDataType DataType { get; set; } = HtmlDataType.FullPage;

        /// <summary>
        /// セレクター（Element取得時のみ）
        /// </summary>
        public string Selector { get; set; } = string.Empty;

        /// <summary>
        /// データが取得された日時
        /// </summary>
        public DateTime CapturedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// データサイズ（バイト）
        /// </summary>
        public long Size { get; set; }

        /// <summary>
        /// 保存されたファイルパス（保存時のみ）
        /// </summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>
        /// カスタムプロパティ
        /// </summary>
        public Dictionary<string, object> CustomProperties { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// 追加のメタデータ（既存コードとの互換性のため）
        /// </summary>
        public Dictionary<string, object> Metadata => CustomProperties;
    }

    /// <summary>
    /// 保存されたHTMLファイルの情報
    /// </summary>
    public class HtmlFileInfo
    {
        /// <summary>
        /// ファイルパス
        /// </summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>
        /// ファイル名
        /// </summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// ページタイトル
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// ページタイトル（既存コードとの互換性のため）
        /// </summary>
        public string PageTitle 
        { 
            get => Title; 
            set => Title = value; 
        }

        /// <summary>
        /// 元のURL
        /// </summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// データの種類
        /// </summary>
        public HtmlDataType DataType { get; set; }

        /// <summary>
        /// ファイルサイズ（バイト）
        /// </summary>
        public long Size { get; set; }

        /// <summary>
        /// ファイルサイズ（バイト）
        /// </summary>
        public long FileSize 
        { 
            get => Size; 
            set => Size = value; 
        }

        /// <summary>
        /// 作成日時
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// 最終更新日時
        /// </summary>
        public DateTime ModifiedAt { get; set; }

        /// <summary>
        /// セレクター（Element取得時のみ）
        /// </summary>
        public string Selector { get; set; } = string.Empty;
    }

    /// <summary>
    /// HTML比較結果
    /// </summary>
    public class HtmlComparisonResult
    {
        /// <summary>
        /// 比較結果が同一かどうか
        /// </summary>
        public bool AreIdentical { get; set; }

        /// <summary>
        /// 比較結果が同一かどうか（既存コードとの互換性のため）
        /// </summary>
        public bool AreEqual 
        { 
            get => AreIdentical; 
            set => AreIdentical = value; 
        }

        /// <summary>
        /// 類似度（0-100%）
        /// </summary>
        public double SimilarityPercentage { get; set; }

        /// <summary>
        /// サイズの差分
        /// </summary>
        public long SizeDifference { get; set; }

        /// <summary>
        /// 差分の数
        /// </summary>
        public int DifferenceCount { get; set; }

        /// <summary>
        /// 差分の詳細
        /// </summary>
        public List<HtmlDifference> Differences { get; set; } = new List<HtmlDifference>();

        /// <summary>
        /// 差分の詳細（文字列リスト版）
        /// </summary>
        public List<string> DifferenceStrings { get; set; } = new List<string>();

        /// <summary>
        /// 比較実行日時
        /// </summary>
        public DateTime ComparedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// 比較に使用したアルゴリズム
        /// </summary>
        public string ComparisonAlgorithm { get; set; } = "Basic";
    }

    /// <summary>
    /// HTML差分情報
    /// </summary>
    public class HtmlDifference
    {
        /// <summary>
        /// 差分の種類
        /// </summary>
        public HtmlDifferenceType Type { get; set; }

        /// <summary>
        /// 差分の位置（行番号など）
        /// </summary>
        public int Position { get; set; }

        /// <summary>
        /// 元のコンテンツ
        /// </summary>
        public string OriginalContent { get; set; } = string.Empty;

        /// <summary>
        /// 新しいコンテンツ
        /// </summary>
        public string NewContent { get; set; } = string.Empty;

        /// <summary>
        /// 差分の説明
        /// </summary>
        public string Description { get; set; } = string.Empty;
    }

    /// <summary>
    /// HTML差分の種類
    /// </summary>
    public enum HtmlDifferenceType
    {
        /// <summary>
        /// 追加
        /// </summary>
        Added,

        /// <summary>
        /// 削除
        /// </summary>
        Deleted,

        /// <summary>
        /// 変更
        /// </summary>
        Modified,

        /// <summary>
        /// 移動
        /// </summary>
        Moved
    }
}