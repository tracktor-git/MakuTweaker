using Hardcodet.Wpf.TaskbarNotification;
using MakuTweakerNew.Properties;
using MicaWPF.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Windows.UI.Composition.Desktop;

namespace MakuTweakerNew
{
    public partial class Perf : Page
    {
        bool isLoaded = false;
        public Perf()
        {
            InitializeComponent();
            LoadLang();
            checkReg();
            this.Loaded += Perf_Loaded;
            var languageCode = Properties.Settings.Default.lang ?? "en";
            var perfor = MainWindow.Localization.LoadLocalization(languageCode, "perfor");
            isLoaded = true;

        }

        private string GetCmdOutput(string command, string arguments)
        {
            try
            {
                using (Process p = new Process())
                {
                    p.StartInfo.FileName = command;
                    p.StartInfo.Arguments = arguments;
                    p.StartInfo.UseShellExecute = false;
                    p.StartInfo.RedirectStandardOutput = true;
                    p.StartInfo.CreateNoWindow = true;
                    p.Start();
                    string output = p.StandardOutput.ReadToEnd();
                    p.WaitForExit();
                    return output.ToLower();
                }
            }
            catch
            {
                iNKORE.UI.WPF.Modern.Controls.MessageBox.Show("CMD Error", "MakuTweaker", MessageBoxButton.OK, MessageBoxImage.Error);
                return "";
            }
        }
        private bool IsPowerSettingZero(string output)
        {
            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            if (lines.Length >= 2)
            {
                return lines[lines.Length - 1].Contains("0x00000000") &&
                       lines[lines.Length - 2].Contains("0x00000000");
            }
            return false;
        }
        private void checkReg()
        {
            string powerVideo = GetCmdOutput("powercfg", "/q SCHEME_CURRENT SUB_VIDEO VIDEOIDLE");
            string powerSleep = GetCmdOutput("powercfg", "/q SCHEME_CURRENT SUB_SLEEP STANDBYIDLE");
            sleeptimeout.IsOn = IsPowerSettingZero(powerVideo) && IsPowerSettingZero(powerSleep);

            ultperf.IsOn = IsUltimatePerformanceActive();
        }

        private (int ExitCode, string Output, string Error) RunPowerCfg(string args)
        {
            using var process = new Process();

            process.StartInfo = new ProcessStartInfo
            {
                FileName = "powercfg.exe",
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            process.Start();

            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();

            process.WaitForExit();

            return (process.ExitCode, output, error);
        }

        private const string UltimateTemplateGuid = "e9a42b02-d5df-448d-aa00-03f14749eb61";

        private string EnsureUltimatePerformanceExists()
        {
            string listOutput = RunPowerCfg("/list").Output;

            var guids = Regex.Matches(
                listOutput,
                @"([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})");

            foreach (Match guidMatch in guids)
            {
                string guid = guidMatch.Value;

                var aliasCheck = RunPowerCfg($"/query {guid}");

                if (aliasCheck.Output.Contains("Ultimate Performance",
                    StringComparison.OrdinalIgnoreCase))
                {
                    return guid;
                }
            }

            var duplicateResult =
                RunPowerCfg($"/duplicatescheme {UltimateTemplateGuid}");

            Match newGuid = Regex.Match(
                duplicateResult.Output,
                @"([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})");

            if (!newGuid.Success)
                throw new Exception(
                    $"Не удалось создать Ultimate Performance.\n{duplicateResult.Output}\n{duplicateResult.Error}");

            return newGuid.Value;
        }

        private void LoadLang()
        {
            var languageCode = Properties.Settings.Default.lang ?? "en";
            var perfor = MainWindow.Localization.LoadLocalization(languageCode, "perfor");
            var main = MainWindow.Localization.LoadLocalization(languageCode, "base");
            var tooltips = MainWindow.Localization.LoadLocalization(languageCode, "tooltips");

            label.Text = perfor["main"]["label"];
            apply.Content = perfor["main"]["applyb"];
            minpercent.Content = perfor["main"]["minb"];
            maxpercent.Content = perfor["main"]["maxb"];
            infolabel.Text = perfor["main"]["info"];
            sleeptimeout.Header = perfor["main"]["sleeptimeout"];
            ultperf.Header = perfor["main"]["ultperf"];
            sys_tooltip_ultperf.Content = tooltips["main"]["ultperf"];

            foreach (var toggle in AllToggles)
            {
                toggle.OnContent = main["def"]["on"];
                toggle.OffContent = main["def"]["off"];
            }
        }

        private List<ModernWpf.Controls.ToggleSwitch> AllToggles => new()
        {
            sleeptimeout, ultperf
        };

        private void ApplyThrottle(int percent)
        {
            if (percent < 1 || percent > 100)
                return;

            string scheme = GetActiveScheme();

            var r1 = RunPowerCfg($"/setdcvalueindex {scheme} SUB_PROCESSOR PROCTHROTTLEMAX {percent}");
            var r2 = RunPowerCfg($"/setacvalueindex {scheme} SUB_PROCESSOR PROCTHROTTLEMAX {percent}");
            var r3 = RunPowerCfg($"/setactive {scheme}");

            if (r1.ExitCode == 0 && r2.ExitCode == 0 && r3.ExitCode == 0)
            {
                percentslider.Value = percent / 10.0;
                ShowThrottleNotification(percent);
            }
            else
            {
                iNKORE.UI.WPF.Modern.Controls.MessageBox.Show($"{r1.Error}\n{r2.Error}\n{r3.Error}", "MakuTweaker Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RunCmdCommand(string fileName, string arguments)
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using (Process p = new Process())
            {
                p.StartInfo = psi;
                p.Start();
            }
        }

        private void sleeptimeout_Toggled(object sender, RoutedEventArgs e)
        {
            if (isLoaded)
            {
                RunCmdCommand("powercfg", $"-change -monitor-timeout-ac {(sleeptimeout.IsOn ? "0" : "10")}");
                RunCmdCommand("powercfg", $"-change -monitor-timeout-dc {(sleeptimeout.IsOn ? "0" : "5")}");
                RunCmdCommand("powercfg", $"-change -standby-timeout-ac {(sleeptimeout.IsOn ? "0" : "30")}");
                RunCmdCommand("powercfg", $"-change -standby-timeout-dc {(sleeptimeout.IsOn ? "0" : "15")}");
            }
        }

        private void ultperf_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded)
                return;

            try
            {
                if (ultperf.IsOn)
                {
                    string guid = EnsureUltimatePerformanceExists();

                    var result = RunPowerCfg($"/setactive {guid}");

                    if (result.ExitCode != 0)
                    {
                        MessageBox.Show(
                            result.Error,
                            "powercfg error");
                    }
                }
                else
                {
                    RunPowerCfg(
                        "/setactive 381b4222-f694-41f0-9685-ff5bb260df2e");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.ToString(),
                    "Ultimate Performance");
            }
        }

        private void apply_Click(object sender, RoutedEventArgs e)
        {
            int percent = (int)percentslider.Value * 10;
            ApplyThrottle(percent);
        }



        private void Perf_Loaded(object sender, RoutedEventArgs e)
        {
            RunPowerCfg("-attributes SUB_PROCESSOR PROCTHROTTLEMAX -ATTRIB_HIDE");

            int percent = GetCurrentThrottlePercent();

            if (percent >= 1 && percent <= 100)
                percentslider.Value = percent / 10.0;
            else
                percentslider.Value = 10;
        }

        private string GetActiveScheme()
        {
            var result = RunPowerCfg("/getactivescheme");

            if (result.ExitCode != 0)
                return "SCHEME_CURRENT";

            Match match = Regex.Match(result.Output, @"GUID:\s+([a-fA-F0-9\-]+)");

            return match.Success ? match.Groups[1].Value : "SCHEME_CURRENT";
        }

        private const string SUB_PROCESSOR_GUID = "54533251-82be-4824-96c1-47b60b740d00";
        private const string PROCTHROTTLEMAX_GUID = "bc5038f7-23e0-4960-96da-33abaf5935ec";
        private int GetCurrentThrottlePercent()
        {
            string scheme = GetActiveScheme();

            var result = RunPowerCfg(
                $"/query {scheme} {SUB_PROCESSOR_GUID} {PROCTHROTTLEMAX_GUID}"
            );

            if (result.ExitCode != 0)
                return -1;

            var matches = Regex.Matches(result.Output, @"0x([0-9A-Fa-f]+)");

            if (matches.Count == 0)
                return -1;

            string hex = matches[matches.Count - 1].Groups[1].Value;

            return Convert.ToInt32(hex, 16);
        }

        private void minpercent_Click(object sender, RoutedEventArgs e)
        {
            ApplyThrottle(10);
        }

        private void maxpercent_Click(object sender, RoutedEventArgs e)
        {
            ApplyThrottle(100);
        }

        private Stream GetResourceStream(string relativePath)
        {
            var uri = new Uri($"pack://application:,,,/{relativePath}", UriKind.Absolute);
            var resourceInfo = Application.GetResourceStream(uri);

            if (resourceInfo == null)
                throw new FileNotFoundException($"Ресурс {relativePath} не найден.");

            return resourceInfo.Stream;
        }

        private void ShowThrottleNotification(int percent)
        {
            var languageCode = Properties.Settings.Default.lang ?? "en";
            var perfor = MainWindow.Localization.LoadLocalization(languageCode, "perfor");

            string baseText = perfor["main"]["flyout"];
            string message = $"{baseText}{percent}%";

            Icon trayIcon = new Icon(GetResourceStream("assets/icons/MakuT.ico"));

            TaskbarIcon tray = new TaskbarIcon
            {
                ToolTipText = "MakuTweaker",
                Icon = trayIcon
            };

            tray.ShowBalloonTip("MakuTweaker", message, BalloonIcon.Info);

            Task.Delay(8000).ContinueWith(t =>
            {
                tray.Dispatcher.Invoke(() => tray.Dispose());
            });
        }

        private bool IsUltimatePerformanceActive()
        {
            string activeGuid = GetActiveScheme();

            string ultimateGuid = EnsureUltimatePerformanceExists();

            return activeGuid.Equals(
                ultimateGuid,
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
