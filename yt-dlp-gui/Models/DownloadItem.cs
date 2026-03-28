using Newtonsoft.Json;
using System;
using System.ComponentModel;
using yt_dlp_gui.Models;

namespace yt_dlp_gui.Models {
    public class DownloadItem : INotifyPropertyChanged {
        public event PropertyChangedEventHandler? PropertyChanged;

        public Guid Id { get; set; } = Guid.NewGuid();
        public string Url { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Thumbnail { get; set; } = string.Empty;

        // フォーマット情報（シリアライズ用に簡略化）
        public string VideoFormatId { get; set; } = string.Empty;
        public string AudioFormatId { get; set; } = string.Empty;
        public string VideoExt { get; set; } = string.Empty;
        public string AudioExt { get; set; } = string.Empty;
        public bool IsPackage { get; set; } = false;  // パッケージ形式（映像+音声一体型）かどうか
        public string OutputExt { get; set; } = string.Empty;  // ユーザーが選択した出力拡張子

        public DownloadItemStatus Status { get; set; } = DownloadItemStatus.Queued;

        // 完了後の実際のファイルパス（フォルダを開く機能用）
        [JsonIgnore] public string ActualPath { get; set; } = string.Empty;

        // プログレス情報（JSONには保存しない）
        [JsonIgnore] public decimal Progress { get; set; } = 0;
        [JsonIgnore] public decimal Speed { get; set; } = 0;
        [JsonIgnore] public string ETA { get; set; } = string.Empty;
        [JsonIgnore] public string ErrorMessage { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // 設定の複製（キュー追加時点の設定を保持）
        public bool UseAria2 { get; set; } = false;
        public bool NeedCookie { get; set; } = false;
        public CookieType CookieType { get; set; } = CookieType.Chrome;
        public ImpersonateType ImpersonateType { get; set; } = ImpersonateType.None;
        public string ProxyUrl { get; set; } = string.Empty;
        public bool ProxyEnabled { get; set; } = false;
        public string ConfigFile { get; set; } = string.Empty;
        public string TimeRange { get; set; } = string.Empty;
        public string LimitRate { get; set; } = string.Empty;
        public bool EmbedThumbnail { get; set; } = false;
        public bool EmbedChapters { get; set; } = false;
        public bool EmbedSubtitles { get; set; } = false;
        public bool SaveThumbnail { get; set; } = false;
        public string SubtitleLang { get; set; } = string.Empty;
        public ModifiedType ModifiedType { get; set; } = ModifiedType.Modified;

        // 表示用プロパティ（JSON保存しない）
        [JsonIgnore]
        public string StatusText => Status switch {
            DownloadItemStatus.Queued => "待機中",
            DownloadItemStatus.Downloading => "ダウンロード中",
            DownloadItemStatus.Completed => "完了",
            DownloadItemStatus.Failed => "失敗",
            DownloadItemStatus.Cancelled => "キャンセル",
            _ => ""
        };

        [JsonIgnore]
        public bool IsQueued => Status == DownloadItemStatus.Queued;

        [JsonIgnore]
        public bool IsDownloading => Status == DownloadItemStatus.Downloading;

        [JsonIgnore]
        public bool IsCompleted => Status == DownloadItemStatus.Completed;

        [JsonIgnore]
        public bool IsFailed => Status == DownloadItemStatus.Failed;

        [JsonIgnore]
        public bool IsCancelled => Status == DownloadItemStatus.Cancelled;

        [JsonIgnore]
        public bool CanCancel => Status == DownloadItemStatus.Downloading;

        [JsonIgnore]
        public bool CanRemove => Status != DownloadItemStatus.Downloading;

        [JsonIgnore]
        public bool CanMoveUp => Status == DownloadItemStatus.Queued;

        [JsonIgnore]
        public bool CanMoveDown => Status == DownloadItemStatus.Queued;
    }

    public enum DownloadItemStatus {
        Queued,       // 待機中
        Downloading,  // ダウンロード中
        Completed,    // 完了
        Failed,       // 失敗
        Cancelled     // キャンセル
    }
}
