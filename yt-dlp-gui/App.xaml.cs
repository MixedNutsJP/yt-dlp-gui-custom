using Libs.Yaml;
using System.IO;
using System.Linq;
using System.Windows;
using yt_dlp_gui.Models;

namespace yt_dlp_gui {
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application {
        public static string CurrentVersion = "2023.03.28";
        public static Lang Lang { get; set; } = new();
        private void Application_Startup(object sender, StartupEventArgs e) {
            var args = e.Args.ToList();
            LoadPath();

            // 設定ファイルから言語設定を読み込み
            var configPath = Path(Folders.root, AppName + ".yaml");
            var savedLanguage = string.Empty;
            if (File.Exists(configPath)) {
                var config = Yaml.Open<Views.Main.GUIConfig>(configPath);
                savedLanguage = config.Language;
            }
            LoadLanguage(savedLanguage);
            new Views.Main().Show();
        }

        public static void LoadLanguage(string langCode) {
            string langPath;
            if (string.IsNullOrEmpty(langCode)) {
                // システム言語（デフォルト）を使用
                langPath = Path(Folders.root, AppName + ".lang");
            } else {
                // 指定言語を使用
                langPath = Path(Folders.languages, langCode, AppName + ".lang");
                // 言語ファイルが存在しない場合はデフォルトにフォールバック
                if (!File.Exists(langPath)) {
                    langPath = Path(Folders.root, AppName + ".lang");
                }
            }
            Lang = Yaml.Open<Lang>(langPath);
        }
    }
}
