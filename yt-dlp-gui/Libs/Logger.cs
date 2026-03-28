using System;
using System.IO;
using yt_dlp_gui;

namespace Libs {
    /// <summary>
    /// アプリケーションログを管理するユーティリティクラス
    /// ログは logs/{yyyy-MM-dd-HHmmss}.log に出力される
    /// </summary>
    public static class Logger {
        private static readonly object _lock = new object();
        private static string? _currentLogPath;
        private static readonly string LogFolder = Path.Combine(App.AppPath, "logs");

        /// <summary>
        /// 現在のセッションのログファイルパスを取得
        /// </summary>
        public static string CurrentLogPath {
            get {
                if (_currentLogPath == null) {
                    InitializeLogFile();
                }
                return _currentLogPath!;
            }
        }

        /// <summary>
        /// ログファイルを初期化
        /// </summary>
        private static void InitializeLogFile() {
            lock (_lock) {
                if (_currentLogPath != null) return;

                try {
                    if (!Directory.Exists(LogFolder)) {
                        Directory.CreateDirectory(LogFolder);
                    }

                    var timestamp = DateTime.Now.ToString("yyyy-MM-dd-HHmmss");
                    _currentLogPath = Path.Combine(LogFolder, $"{timestamp}.log");

                    // ログファイルのヘッダーを書き込み
                    var header = $"=== yt-dlp-gui Log ==={Environment.NewLine}" +
                                 $"Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss}{Environment.NewLine}" +
                                 $"Version: {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version}{Environment.NewLine}" +
                                 $"OS: {Environment.OSVersion}{Environment.NewLine}" +
                                 $"==================={Environment.NewLine}{Environment.NewLine}";
                    File.WriteAllText(_currentLogPath, header);
                } catch {
                    // ログ初期化失敗時は一時フォルダに出力
                    _currentLogPath = Path.Combine(Path.GetTempPath(), $"yt-dlp-gui-{DateTime.Now:yyyyMMdd-HHmmss}.log");
                }
            }
        }

        /// <summary>
        /// 情報ログを出力
        /// </summary>
        public static void Info(string message) {
            Write("INFO", message);
        }

        /// <summary>
        /// 警告ログを出力
        /// </summary>
        public static void Warn(string message) {
            Write("WARN", message);
        }

        /// <summary>
        /// エラーログを出力
        /// </summary>
        public static void Error(string message, Exception? ex = null) {
            var fullMessage = ex != null ? $"{message}: {ex.Message}\n{ex.StackTrace}" : message;
            Write("ERROR", fullMessage);
        }

        /// <summary>
        /// デバッグログを出力
        /// </summary>
        public static void Debug(string message) {
            Write("DEBUG", message);
        }

        /// <summary>
        /// コマンド実行ログを出力
        /// </summary>
        public static void Command(string executable, string arguments) {
            Write("CMD", $"{executable} {arguments}");
        }

        /// <summary>
        /// コマンド出力ログを出力
        /// </summary>
        public static void CommandOutput(string output, bool isError = false) {
            Write(isError ? "STDERR" : "STDOUT", output);
        }

        /// <summary>
        /// コマンド終了ログを出力
        /// </summary>
        public static void CommandExit(int exitCode) {
            Write("EXIT", $"ExitCode: {exitCode}");
        }

        /// <summary>
        /// ダウンロード関連ログを出力
        /// </summary>
        public static void Download(string title, string message) {
            Write("DL", $"[{title}] {message}");
        }

        /// <summary>
        /// ログを書き込み
        /// </summary>
        private static void Write(string level, string message) {
            try {
                lock (_lock) {
                    var logLine = $"[{DateTime.Now:HH:mm:ss}] [{level}] {message}{Environment.NewLine}";
                    File.AppendAllText(CurrentLogPath, logLine);
                }
            } catch {
                // ログ書き込み失敗は無視
            }
        }

        /// <summary>
        /// 古いログファイルを削除（30日以上前）
        /// </summary>
        public static void CleanupOldLogs(int daysToKeep = 30) {
            try {
                if (!Directory.Exists(LogFolder)) return;

                var cutoff = DateTime.Now.AddDays(-daysToKeep);
                foreach (var file in Directory.GetFiles(LogFolder, "*.log")) {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.CreationTime < cutoff) {
                        try {
                            fileInfo.Delete();
                        } catch {
                            // 削除失敗は無視
                        }
                    }
                }
            } catch {
                // クリーンアップ失敗は無視
            }
        }
    }
}
