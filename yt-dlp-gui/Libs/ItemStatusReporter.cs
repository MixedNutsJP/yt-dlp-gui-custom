using System.Collections.Generic;
using System.Text.RegularExpressions;
using yt_dlp_gui.Models;

namespace Libs {
    /// <summary>
    /// 個別のダウンロードアイテム用のステータスレポーター
    /// 既存のStatusRepoterと同様のパターンを使用するが、
    /// 共有のDNStatus_Infosではなくアイテムのプロパティを直接更新
    /// </summary>
    public class ItemStatusReporter {
        private readonly DownloadItem _item;

        // 既存のStatusRepoterと同じRegexパターン
        private static readonly Regex regPart = new Regex(@"\[download\] Destination:.*\.f(?<fid>\d+(?:-\w+)?)\.\w+");
        private static readonly Regex regDLP = new Regex(@"^\[yt-dlp]");
        private static readonly Regex regAria = new Regex(@"(?<=\[#\w{6}).*?(?<downloaded>[\w]+).*?\/(?<total>[\w]+).*?(?<persent>[\w.]+)%.*?CN:(?<cn>\d+).*DL:(?<speed>\w+)(.*?ETA:(?<eta>\w+))?");
        private static readonly Regex regFF = new Regex(@"frame=.*?(?<frame>\d+).*?fps=.*?(?<fps>[\d.]+).*?size=.*?(?<size>\w+).*?time=(?<time>\S+).*?bitrate=(?<bitrate>\S+)");
        private static readonly Regex regYTDL = new Regex(@"^\[download\].*?(?<persent>[\d\.]+).*?(?<=of).*?(?<total>\S+).*?(?<=at).*?(?<speed>\S+).*?(?<=ETA).*?(?<eta>\S+)");
        private static readonly Regex regSpeedUnit = new Regex(@"(?<value>[\d.]+)\s*(?<unit>\w+)/s", RegexOptions.IgnoreCase);

        private int _currentFormat = 0; // 0=両方, 1=ビデオ, 2=オーディオ

        public ItemStatusReporter(DownloadItem item) {
            _item = item;
        }

        private static string GetValue(Dictionary<string, string> dict, string key, string defaultValue = "") {
            return dict.TryGetValue(key, out var value) ? value : defaultValue;
        }

        /// <summary>
        /// yt-dlp/aria2/ffmpegの出力を解析してダウンロードアイテムのステータスを更新
        /// </summary>
        public void GetStatus(string std) {
            if (string.IsNullOrWhiteSpace(std)) return;

            // フォーマットID検出（ビデオ/オーディオの切り替え）
            if (regPart.IsMatch(std)) {
                var r = Util.GetGroup(regPart, std);
                var fid = GetValue(r, "fid", "0");
                if (fid == _item.VideoFormatId) {
                    _currentFormat = 1;
                } else if (fid == _item.AudioFormatId) {
                    _currentFormat = 2;
                }
            }

            // yt-dlp ネイティブ出力
            if (regDLP.IsMatch(std)) {
                ParseYtDlpOutput(std);
            }
            // aria2 出力
            else if (regAria.IsMatch(std)) {
                ParseAria2Output(std);
            }
            // ffmpeg 出力
            else if (regFF.IsMatch(std)) {
                ParseFfmpegOutput(std);
            }
            // youtube-dl 出力
            else if (regYTDL.IsMatch(std)) {
                ParseYoutubeDlOutput(std);
            }
        }

        private void ParseYtDlpOutput(string std) {
            var d = std.Split(',');
            if (d.Length >= 7) {
                if (decimal.TryParse(d[4], out decimal total) && total > 0) {
                    if (decimal.TryParse(d[3], out decimal downloaded)) {
                        _item.Progress = downloaded / total * 100;
                    }
                } else {
                    if (decimal.TryParse(d[1].TrimEnd('%'), out decimal persent)) {
                        _item.Progress = persent;
                    }
                }

                if (decimal.TryParse(d[5], out decimal speed)) {
                    _item.Speed = speed / 1024 / 1024; // bytes/s → MB/s
                }

                if (decimal.TryParse(d[6], out decimal elapsed)) {
                    _item.ETA = Util.SecToStr(elapsed);
                }
            }
        }

        private void ParseAria2Output(string std) {
            var d = Util.GetGroup(regAria, std);
            if (decimal.TryParse(GetValue(d, "persent", "0"), out decimal persent)) {
                _item.Progress = persent;
            }

            var speedStr = GetValue(d, "speed", "0");
            _item.Speed = ParseSpeed(speedStr);
            _item.ETA = GetValue(d, "eta", "");
        }

        private void ParseFfmpegOutput(string std) {
            // ffmpegはプログレスが出ないので、時間ベースで更新
            var d = Util.GetGroup(regFF, std);
            _item.ETA = GetValue(d, "time", "");
        }

        private void ParseYoutubeDlOutput(string std) {
            var d = Util.GetGroup(regYTDL, std);
            if (decimal.TryParse(GetValue(d, "persent", "0"), out decimal persent)) {
                _item.Progress = persent;
            }

            var speedStr = GetValue(d, "speed", "0");
            _item.Speed = ParseSpeed(speedStr);
            _item.ETA = GetValue(d, "eta", "");
        }

        /// <summary>
        /// 速度文字列を MB/s に変換
        /// </summary>
        private decimal ParseSpeed(string speedStr) {
            var match = regSpeedUnit.Match(speedStr);
            if (match.Success) {
                if (decimal.TryParse(match.Groups["value"].Value, out decimal value)) {
                    var unit = match.Groups["unit"].Value.ToUpper();
                    return unit switch {
                        "B" => value / 1024 / 1024,
                        "KB" or "KIB" => value / 1024,
                        "MB" or "MIB" => value,
                        "GB" or "GIB" => value * 1024,
                        _ => value / 1024 / 1024 // デフォルトはbytes
                    };
                }
            }
            return 0;
        }
    }
}
