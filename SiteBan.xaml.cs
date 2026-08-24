using MicaWPF.Controls;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using static System.Net.WebRequestMethods;

namespace MakuTweakerNew
{
    public partial class SiteBan : MicaWindow
    {
        private const string StartTag = "# --- MakuTweaker Site Ban Start ---";
        private const string EndTag = "# --- MakuTweaker Site Ban End ---";
        private readonly string _hostsPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"drivers\etc\hosts");

        private readonly string[] _yandexDomains = new string[]
        {
        "yandex.com","passport.yandex.ru","avatars.yandex.net","yandex.ru",
        "ya.ru","yandex.net","yastatic.net","yandex.org","yandex.com.ru",
        "yandex.net.ru","yandex.by","yandex.kz","yandex.uz","yandex.md","yandex.fr",
        "yandex.az","yandex.com.tr","yandex.ro","yandex.asia","yandex.mobi",
        "direct.yandex.ru","metrika.yandex.ru","market.yandex.ru","money.yandex.ru",
        "afisha.yandex.ru","news.yandex.ru","pogoda.yandex.ru","tv.yandex.ru",
        "translate.yandex.ru","browser.yandex.ru","dzen.ru","zen.yandex.ru","kinopoisk.ru","auto.ru",
        "rabota.ru","cloud.yandex.ru","storage.yandexcloud.net", "balance.yandex.ru",
        "api.browser.yandex.ru","update.browser.yandex.net","station.yandex","metrica.yandex","docs.yandex",
        "rentaxi.yandex","translate-image.yandex","music.yandex.ru","disk.yandex.ru","taxi.yandex.ru","eda.yandex.ru","maps.yandex.ru","mail.yandex.ru"
        };

        MainWindow mw = (MainWindow)Application.Current.MainWindow;
        public SiteBan()
        {
            InitializeComponent();
            LoadLang();
            ProcBlockTxt.Text = GetBlockedSitesFromHosts();
        }

        private string GetBlockedSitesFromHosts()
        {
            if (!System.IO.File.Exists(_hostsPath)) return string.Empty;

            var lines = System.IO.File.ReadAllLines(_hostsPath);
            var result = new List<string>();
            bool isInsideBlock = false;

            foreach (var line in lines)
            {
                if (line.Contains(StartTag)) { isInsideBlock = true; continue; }
                if (line.Contains(EndTag)) break;
                if (isInsideBlock && line.StartsWith("127.0.0.1 "))
                    result.Add(line.Replace("127.0.0.1 ", "").Trim());
            }
            return string.Join(Environment.NewLine, result);
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var languageCode = Properties.Settings.Default.lang ?? "en";
            var myan = MainWindow.Localization.LoadLocalization(languageCode, "myan");
            var main = myan["main"];

            var lines = ProcBlockTxt.Text.Split(new[] { '\r', '\n', ',' }, StringSplitOptions.RemoveEmptyEntries);
            var domains = lines.Select(line => {
                var d = line.Trim().Replace("https://", "").Replace("http://", "").Split('/')[0];
                return d.ToLower();
            }).Where(d => !string.IsNullOrWhiteSpace(d)).Distinct().ToList();

            var forbidden = new[] {"adderly.top", "youtube.com/@makuadarii", "boosty.to/adderly" };

            if (domains.Any(d => forbidden.Any(f => d.Contains(f))))
            {
                iNKORE.UI.WPF.Modern.Controls.MessageBox.Show(main["makutnah"], "MakuTweaker", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            bool success = UpdateHostsFile(domains);
            if (success)
            {
                iNKORE.UI.WPF.Modern.Controls.MessageBox.Show(main["sitebandone"], "MakuTweaker", MessageBoxButton.OK, MessageBoxImage.Information);
                this.Close();
            }

            UpdateHostsFile(domains);
            this.Close();
        }

        private bool UpdateHostsFile(List<string> domains)
        {
            try
            {
                var lines = System.IO.File.Exists(_hostsPath) ? System.IO.File.ReadAllLines(_hostsPath).ToList() : new List<string>();

                int startIdx = lines.FindIndex(l => l.Contains(StartTag));
                int endIdx = lines.FindIndex(l => l.Contains(EndTag));

                if (startIdx != -1 && endIdx != -1)
                    lines.RemoveRange(startIdx, endIdx - startIdx + 1);

                if (domains.Any())
                {
                    lines.Add(StartTag);
                    foreach (var d in domains) lines.Add($"127.0.0.1 {d}");
                    lines.Add(EndTag);
                }

                var attrs = System.IO.File.GetAttributes(_hostsPath);
                if ((attrs & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                    System.IO.File.SetAttributes(_hostsPath, attrs & ~FileAttributes.ReadOnly);

                System.IO.File.WriteAllLines(_hostsPath, lines);
                return true;
            }
            catch (Exception ex)
            {
                iNKORE.UI.WPF.Modern.Controls.MessageBox.Show($"{ex.Message}");
                return false;
            }
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            ProcBlockTxt.Text = string.Empty;
        }

        public void LoadLang()
        {
            try
            {
                var languageCode = Properties.Settings.Default.lang ?? "en";
                var myan = MainWindow.Localization.LoadLocalization(languageCode, "myan");
                var main = myan["main"];
                this.Title = main["excltitle"];
                InfoText.Text = main["siteban"];
                ClearBtn.Content = main["clear"];
                SaveBtn.Content = main["applyban"];
                banyandex.Content = main["banyandex"];

                var allowedLangs = new[] { "ru", "uk", "kk", "lv", "et", "be", "az" };

                banyandex.Visibility = allowedLangs.Contains(languageCode)
                    ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                iNKORE.UI.WPF.Modern.Controls.MessageBox.Show(ex.Message, "MakuTweaker Error", MessageBoxButton.OK, MessageBoxImage.Stop);
            }
        }

        private void BlockYandex_Click(object sender, RoutedEventArgs e)
        {
            ProcBlockTxt.Text = string.Join(Environment.NewLine, _yandexDomains);
        }
    }
}
