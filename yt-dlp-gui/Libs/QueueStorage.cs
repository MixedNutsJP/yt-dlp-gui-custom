using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using yt_dlp_gui;
using yt_dlp_gui.Models;

namespace Libs {
    public static class QueueStorage {
        private static string QueueFile => Path.Combine(App.AppPath, "download-queue.json");

        /// <summary>
        /// 未完了のキューのみ保存（Completed は保存しない）
        /// </summary>
        public static void Save(IEnumerable<DownloadItem> items) {
            var toSave = items.Where(x => x.Status != DownloadItemStatus.Completed
                                       && x.Status != DownloadItemStatus.Cancelled);
            var json = JsonConvert.SerializeObject(toSave, Formatting.Indented, new JsonSerializerSettings {
                NullValueHandling = NullValueHandling.Ignore
            });
            File.WriteAllText(QueueFile, json);
        }

        /// <summary>
        /// 保存されたキューを読み込み
        /// </summary>
        public static List<DownloadItem> Load() {
            if (!File.Exists(QueueFile)) return new List<DownloadItem>();
            try {
                var json = File.ReadAllText(QueueFile);
                var items = JsonConvert.DeserializeObject<List<DownloadItem>>(json);
                if (items != null) {
                    // ダウンロード中だったものは待機中に戻す（アプリ再起動時）
                    foreach (var item in items) {
                        if (item.Status == DownloadItemStatus.Downloading) {
                            item.Status = DownloadItemStatus.Queued;
                        }
                        // プログレス情報をリセット
                        item.Progress = 0;
                        item.Speed = 0;
                        item.ETA = string.Empty;
                    }
                    return items;
                }
            } catch {
                // JSONパースエラーの場合は空リストを返す
            }
            return new List<DownloadItem>();
        }

        /// <summary>
        /// キューファイルを削除
        /// </summary>
        public static void Delete() {
            if (File.Exists(QueueFile)) {
                File.Delete(QueueFile);
            }
        }

        /// <summary>
        /// キューファイルが存在するか
        /// </summary>
        public static bool Exists() {
            return File.Exists(QueueFile);
        }
    }
}
