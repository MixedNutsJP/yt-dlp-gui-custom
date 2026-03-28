using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using yt_dlp_gui;
using yt_dlp_gui.Models;
using yt_dlp_gui.Views;
using yt_dlp_gui.Wrappers;

namespace Libs {
    /// <summary>
    /// ダウンロードキューを管理し、並列ダウンロードを制御するクラス
    /// </summary>
    public class DownloadManager {
        private readonly ConcurrentDictionary<Guid, (DLP Process, CancellationTokenSource CTS)> _activeDownloads = new();
        private readonly Main.ViewData _data;
        private readonly Func<string>? _getTempPath;
        private bool _isProcessing = false;
        private readonly object _lockObject = new object();

        public DownloadManager(Main.ViewData data, Func<string>? getTempPath = null) {
            _data = data;
            _getTempPath = getTempPath;
        }

        /// <summary>
        /// キューに追加して処理開始
        /// </summary>
        public void Enqueue(DownloadItem item) {
            Application.Current.Dispatcher.Invoke(() => {
                _data.DownloadQueue.Add(item);
            });
            QueueStorage.Save(_data.DownloadQueue);
            ProcessQueue();
        }

        /// <summary>
        /// 即時ダウンロード（キューの先頭に追加して即実行）
        /// </summary>
        public void DownloadNow(DownloadItem item) {
            Application.Current.Dispatcher.Invoke(() => {
                _data.DownloadQueue.Insert(0, item);
            });
            QueueStorage.Save(_data.DownloadQueue);
            ProcessQueue();
        }

        /// <summary>
        /// キュー内の待機中アイテムをすべてダウンロード開始
        /// </summary>
        public void StartAll() {
            ProcessQueue();
        }

        /// <summary>
        /// キュー処理ループ
        /// </summary>
        private void ProcessQueue() {
            lock (_lockObject) {
                if (_isProcessing) return;
                _isProcessing = true;
            }

            try {
                // 現在のアクティブダウンロード数と最大並列数を取得
                int activeCount = _activeDownloads.Count;
                int maxConcurrent = _data.MaxConcurrentDownloads;
                int slotsAvailable = maxConcurrent - activeCount;

                if (slotsAvailable <= 0) return;

                // 待機中のアイテムを取得（空きスロット分だけ）
                var queuedItems = new System.Collections.Generic.List<DownloadItem>();
                Application.Current.Dispatcher.Invoke(() => {
                    queuedItems = _data.DownloadQueue
                        .Where(x => x.Status == DownloadItemStatus.Queued)
                        .Take(slotsAvailable)
                        .ToList();
                });

                // 各アイテムに対してダウンロードタスクを開始
                foreach (var item in queuedItems) {
                    _ = Task.Run(async () => {
                        await ExecuteDownloadAsync(item);
                    });
                }
            } finally {
                lock (_lockObject) {
                    _isProcessing = false;
                }
            }
        }

        /// <summary>
        /// 個別ダウンロード実行
        /// </summary>
        private async Task ExecuteDownloadAsync(DownloadItem item) {
            var cts = new CancellationTokenSource();
            DLP? dlp = null;

            try {
                Application.Current.Dispatcher.Invoke(() => {
                    item.Status = DownloadItemStatus.Downloading;
                    item.Progress = 0;
                    item.Speed = 0;
                    item.ETA = string.Empty;
                });

                dlp = BuildDLP(item);
                _activeDownloads[item.Id] = (dlp, cts);

                var reporter = new ItemStatusReporter(item);

                // 同期的にExec（DLP.Execは内部でWaitForExitを呼ぶ）
                dlp.Exec(std => {
                    if (cts.IsCancellationRequested) return;
                    reporter.GetStatus(std);
                });

                if (!cts.IsCancellationRequested) {
                    Application.Current.Dispatcher.Invoke(() => {
                        item.Status = DownloadItemStatus.Completed;
                        item.Progress = 100;
                    });

                    Logger.Download(item.Title, "Download completed successfully");

                    // 完了通知
                    if (_data.UseNotifications) {
                        ShowNotification(item);
                    }
                }
            } catch (Exception ex) {
                Logger.Error($"[Download] Failed: {item.Title}", ex);
                Application.Current.Dispatcher.Invoke(() => {
                    item.Status = DownloadItemStatus.Failed;
                    item.ErrorMessage = ex.Message;
                });
            } finally {
                _activeDownloads.TryRemove(item.Id, out _);

                // 完了済みは保存から除外される
                QueueStorage.Save(_data.DownloadQueue);

                // キュー処理を継続（空いたスロットで次のアイテムを処理）
                ProcessQueue();
            }
        }

        /// <summary>
        /// DownloadItemからDLPオブジェクトを構築
        /// </summary>
        private DLP BuildDLP(DownloadItem item) {
            // ダウンロード先パスを動的に生成（単一DLの保存先設定を使用）
            var targetFolder = _data.TargetPath;
            var originExt = GetOriginExt(item);
            // ユーザーが選択した出力拡張子を使用（指定がなければoriginExtを使用）
            var outputExt = !string.IsNullOrWhiteSpace(item.OutputExt) ? item.OutputExt : originExt;
            var fileName = GetValidFileName(item.Title) + "." + outputExt;
            var targetPath = Path.Combine(targetFolder, fileName);

            // 実際のパスをアイテムに保存（完了後のフォルダを開く機能用）
            item.ActualPath = targetPath;

            // フォーマットID生成（単一DLと同じロジック: 常に "videoId+audioId" 形式）
            // AudioFormatIdが空の場合のみvideoIdのみを使用
            var formatId = string.IsNullOrWhiteSpace(item.AudioFormatId)
                ? item.VideoFormatId
                : $"{item.VideoFormatId}+{item.AudioFormatId}";

            // ダウンロード開始ログ
            Logger.Download(item.Title, $"Starting download: formatId={formatId}, ext={outputExt}, path={targetPath}");

            // DLPオブジェクト構築（単一ダウンロードと同じ順序）
            var dlp = new DLP(item.Url);
            dlp.IsLive = false;

            // 一時ディレクトリ（単一DLと同じ設定を使用）
            var tempPath = _getTempPath != null ? _getTempPath() : Path.GetTempPath();

            // 単一ダウンロードと同じ順序でオプション設定
            dlp.Temp(tempPath)
               .LoadConfig(item.ConfigFile ?? "")
               .MTime(item.ModifiedType)
               .Cookie(item.CookieType, item.NeedCookie)
               .Impersonate(item.ImpersonateType)
               .Proxy(item.ProxyUrl, item.ProxyEnabled)
               .UseAria2(item.UseAria2)
               .LimitRate(item.LimitRate ?? "")
               .DownloadSections(item.TimeRange ?? "");

            // 埋め込み設定とダウンロード（単一ダウンロードと同じ順序）
            dlp.EmbedChapters(item.EmbedChapters)
               .Thumbnail(item.SaveThumbnail, targetPath, item.EmbedThumbnail)
               .Subtitle(item.SubtitleLang ?? "", targetPath, item.EmbedSubtitles)
               .DownloadFormat(formatId, targetPath, originExt);

            return dlp;
        }

        /// <summary>
        /// ファイル名に使用できない文字を除去
        /// </summary>
        private string GetValidFileName(string name) {
            var invalidChars = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
            var regex = new Regex($"[{Regex.Escape(invalidChars)}]");
            return regex.Replace(name, "_").Trim();
        }

        /// <summary>
        /// 出力拡張子を決定（オリジナルのOriginExtプロパティと同じロジック）
        /// </summary>
        private string GetOriginExt(DownloadItem item) {
            // パッケージ形式の場合（映像+音声一体型）
            if (item.IsPackage) {
                return item.VideoExt.ToLower().Trim('.');
            }
            // webm + webm → webm
            if (item.VideoExt == "webm" && item.AudioExt == "webm") {
                return "webm";
            }
            // mp4 + m4a → mp4
            if (item.VideoExt == "mp4" && item.AudioExt == "m4a") {
                return "mp4";
            }
            // それ以外 → mkv
            return "mkv";
        }

        /// <summary>
        /// 完了通知を表示
        /// </summary>
        private void ShowNotification(DownloadItem item) {
            try {
                Util.NotifySound(_data.PathNotify);

                var toast = new ToastContentBuilder()
                    .AddText(item.Title)
                    .AddText(App.Lang.Dialog.DownloadCompleted)
                    .AddAudio(new ToastAudio() {
                        Silent = true,
                        Loop = false,
                        Src = new Uri("ms-winsoundevent:Notification.Default")
                    });

                if (File.Exists(item.ActualPath)) {
                    toast.AddButton(
                        new ToastButton()
                        .SetContent(App.Lang.Dialog.OpenFolder)
                        .AddArgument("action", "browse")
                        .AddArgument("file", item.ActualPath)
                        .SetBackgroundActivation()
                    );
                }

                toast.AddButton(
                    new ToastButton()
                    .SetContent(App.Lang.Dialog.Close)
                    .AddArgument("action", "none")
                    .SetBackgroundActivation()
                );

                toast.Show();
            } catch {
                // 通知失敗は無視
            }
        }

        /// <summary>
        /// 特定のダウンロードをキャンセル
        /// </summary>
        public void Cancel(Guid id) {
            if (_activeDownloads.TryGetValue(id, out var active)) {
                active.CTS.Cancel();
                active.Process.Close();
            }

            Application.Current.Dispatcher.Invoke(() => {
                var item = _data.DownloadQueue.FirstOrDefault(x => x.Id == id);
                if (item != null) {
                    item.Status = DownloadItemStatus.Cancelled;
                }
            });

            QueueStorage.Save(_data.DownloadQueue);
        }

        /// <summary>
        /// 全てのダウンロードをキャンセル
        /// </summary>
        public void CancelAll() {
            foreach (var kvp in _activeDownloads) {
                kvp.Value.CTS.Cancel();
                kvp.Value.Process.Close();
            }

            Application.Current.Dispatcher.Invoke(() => {
                foreach (var item in _data.DownloadQueue.Where(x => x.Status == DownloadItemStatus.Downloading)) {
                    item.Status = DownloadItemStatus.Cancelled;
                }
            });

            QueueStorage.Save(_data.DownloadQueue);
        }

        /// <summary>
        /// キューからアイテムを削除（ダウンロード中でない場合のみ）
        /// </summary>
        public void Remove(Guid id) {
            Application.Current.Dispatcher.Invoke(() => {
                var item = _data.DownloadQueue.FirstOrDefault(x => x.Id == id);
                if (item != null && item.Status != DownloadItemStatus.Downloading) {
                    _data.DownloadQueue.Remove(item);
                }
            });
            QueueStorage.Save(_data.DownloadQueue);
        }

        /// <summary>
        /// キュー内でアイテムを上に移動
        /// </summary>
        public void MoveUp(Guid id) {
            Application.Current.Dispatcher.Invoke(() => {
                var index = -1;
                for (int i = 0; i < _data.DownloadQueue.Count; i++) {
                    if (_data.DownloadQueue[i].Id == id) {
                        index = i;
                        break;
                    }
                }

                if (index > 0 && _data.DownloadQueue[index].Status == DownloadItemStatus.Queued) {
                    var item = _data.DownloadQueue[index];
                    _data.DownloadQueue.RemoveAt(index);
                    _data.DownloadQueue.Insert(index - 1, item);
                }
            });
            QueueStorage.Save(_data.DownloadQueue);
        }

        /// <summary>
        /// キュー内でアイテムを下に移動
        /// </summary>
        public void MoveDown(Guid id) {
            Application.Current.Dispatcher.Invoke(() => {
                var index = -1;
                for (int i = 0; i < _data.DownloadQueue.Count; i++) {
                    if (_data.DownloadQueue[i].Id == id) {
                        index = i;
                        break;
                    }
                }

                if (index >= 0 && index < _data.DownloadQueue.Count - 1
                    && _data.DownloadQueue[index].Status == DownloadItemStatus.Queued) {
                    var item = _data.DownloadQueue[index];
                    _data.DownloadQueue.RemoveAt(index);
                    _data.DownloadQueue.Insert(index + 1, item);
                }
            });
            QueueStorage.Save(_data.DownloadQueue);
        }

        /// <summary>
        /// 完了済みアイテムをすべてクリア
        /// </summary>
        public void ClearCompleted() {
            Application.Current.Dispatcher.Invoke(() => {
                var completed = _data.DownloadQueue
                    .Where(x => x.Status == DownloadItemStatus.Completed || x.Status == DownloadItemStatus.Cancelled)
                    .ToList();

                foreach (var item in completed) {
                    _data.DownloadQueue.Remove(item);
                }
            });
            QueueStorage.Save(_data.DownloadQueue);
        }

        /// <summary>
        /// 失敗したアイテムを再試行
        /// </summary>
        public void Retry(Guid id) {
            Application.Current.Dispatcher.Invoke(() => {
                var item = _data.DownloadQueue.FirstOrDefault(x => x.Id == id);
                if (item != null && (item.Status == DownloadItemStatus.Failed || item.Status == DownloadItemStatus.Cancelled)) {
                    item.Status = DownloadItemStatus.Queued;
                    item.Progress = 0;
                    item.Speed = 0;
                    item.ETA = string.Empty;
                    item.ErrorMessage = string.Empty;
                }
            });
            QueueStorage.Save(_data.DownloadQueue);
            ProcessQueue();
        }
    }
}
