namespace yt_dlp_gui.Models {
    /// <summary>
    /// キューに追加する際に適用する画質の指定方法。
    /// </summary>
    public enum QualityPreference {
        /// <summary>メイン画面の「映像」「音声」で選択中のフォーマットをそのまま使う</summary>
        Current,
        /// <summary>最大: その動画で取得可能な最高画質を自動取得</summary>
        Max,
        /// <summary>高（1080p 目安）</summary>
        High,
        /// <summary>中（720p 目安）</summary>
        Medium,
        /// <summary>低（480p 目安）</summary>
        Low,
        /// <summary>最小: その動画で取得可能な最低画質を自動取得</summary>
        Min
    }

    /// <summary>
    /// <see cref="QualityPreference"/> を yt-dlp のフォーマット指定へ変換する。
    ///
    /// キューは動画ごとに取得できるフォーマットが異なるため、特定の format_id を
    /// そのまま使い回すことができない。そこで解像度ベースのセレクタ式を組み立て、
    /// 該当する画質が無い動画では yt-dlp 側で自動的にフォールバックさせる。
    ///
    /// 最大 / 最小 は解像度を固定せず、その動画で実際に取得できる上限・下限を
    /// yt-dlp に選ばせる（自動取得）。
    /// </summary>
    public static class QualitySelector {
        /// <summary>
        /// プリセットの目標解像度（高さ）。Current / Max / Min は解像度を固定しないので null。
        /// </summary>
        public static int? TargetHeight(QualityPreference quality) => quality switch {
            QualityPreference.High => 1080,
            QualityPreference.Medium => 720,
            QualityPreference.Low => 480,
            _ => null
        };

        /// <summary>
        /// プリセット指定かどうか（false ならメイン画面の選択をそのまま使う）。
        /// </summary>
        public static bool IsPreset(QualityPreference quality) => quality != QualityPreference.Current;

        /// <summary>
        /// --format に渡すセレクタ式を組み立てる。
        /// '/' 区切りは yt-dlp のフォールバック順で、左から順に評価され
        /// 最初に成立したものが採用される。
        ///
        /// 解像度指定（高 / 中 / 低）で該当する画質が無かった場合は、
        /// 最大画質（= その動画で取得可能な最高画質）へフォールバックする。
        /// </summary>
        public static string BuildFormat(QualityPreference quality) {
            // 最大: 映像+音声の最良 → 単一ファイルの最良
            const string best = "bv*+ba/b";

            if (quality == QualityPreference.Min) {
                // 最小: 映像+音声の最低 → 単一ファイルの最低
                return "wv*+wa/w";
            }

            var height = TargetHeight(quality);
            if (height == null) {
                // Max（および想定外の値）は最高画質
                return best;
            }

            return string.Join("/", new[] {
                $"bv*[height<={height}]+ba", // 目標解像度以下で最良の映像 + 最良の音声
                $"b[height<={height}]",      // 目標解像度以下の映像音声一体型
                best                         // フォールバック: 最大画質
            });
        }

        /// <summary>
        /// 表示用ラベル（キュー一覧などで使う）。
        /// </summary>
        public static string Label(QualityPreference quality) => quality switch {
            QualityPreference.Max => App.Lang.Main.QualityMax,
            QualityPreference.High => App.Lang.Main.QualityHigh,
            QualityPreference.Medium => App.Lang.Main.QualityMedium,
            QualityPreference.Low => App.Lang.Main.QualityLow,
            QualityPreference.Min => App.Lang.Main.QualityMin,
            _ => App.Lang.Main.QualityCurrent
        };
    }
}
