using MakuTweakerNew.Properties;
using MicaWPF.Controls;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.Mail;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using Windows.UI.Composition.Desktop;
using static System.Net.Mime.MediaTypeNames;

namespace MakuTweakerNew
{
    public partial class SysAndRec : Page
    {
        [Flags]
        public enum RecycleFlags : uint
        {
            SHERB_NOCONFIRMATION = 0x00000001,
            SHERB_NOPROGRESSUI = 0x00000002,
            SHERB_NOSOUND = 0x00000004
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern uint SHEmptyRecycleBin(IntPtr hWnd, string pszRootPath, RecycleFlags dwFlags);

        bool isLoaded = false;
        MainWindow mw = (MainWindow)System.Windows.Application.Current.MainWindow;

        public SysAndRec()
        {
            InitializeComponent();
            checkReg();
            LoadLang();
            isLoaded = true;

            if (!HasBattery())
            {
                batterylabel.Visibility = Visibility.Collapsed;
                report.Visibility = Visibility.Collapsed;
            }
        }

        private async void UpdateTempSizeLabel()
        {
            tempSizeText.Visibility = Visibility.Collapsed;
            tempSizeRing.Visibility = Visibility.Visible;
            tempSizeRing.IsActive = true;

            long sizeBytes = await Task.Run(() =>
            {
                string dir1 = Path.GetTempPath();
                string dir2 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp");
                return GetDirectorySize(dir1) + GetDirectorySize(dir2);
            });

            if (sizeBytes < 104857600)
            {
                tempFullPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                string size = GetFormattedSize(sizeBytes);
                tempSizeText.Text = $" ({size})";
                tempSizeRing.IsActive = false;
                tempSizeRing.Visibility = Visibility.Collapsed;
                tempSizeText.Visibility = Visibility.Visible;
            }
        }

        private async void UpdatePipSizeLabel()
        {
            pipFullPanel.Visibility = Visibility.Visible;
            pipSizeText.Visibility = Visibility.Collapsed;
            pipSizeRing.Visibility = Visibility.Visible;
            pipSizeRing.IsActive = true;

            long sizeBytes = await Task.Run(() =>
            {
                string pipDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "pip");
                return GetDirectorySize(pipDir);
            });

            if (sizeBytes < 104857600)
            {
                pipFullPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                string size = GetFormattedSize(sizeBytes);
                pipSizeText.Text = $" ({size})";
                pipSizeRing.IsActive = false;
                pipSizeRing.Visibility = Visibility.Collapsed;
                pipSizeText.Visibility = Visibility.Visible;
            }
        }

        private long GetDirectorySize(string folderPath)
        {
            long size = 0;
            try
            {
                if (Directory.Exists(folderPath))
                {
                    var options = new System.IO.EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = true };
                    string[] files = Directory.GetFiles(folderPath, "*.*", options);
                    foreach (string file in files)
                    {
                        try { size += new FileInfo(file).Length; } catch { }
                    }
                }
            }
            catch { }
            return size;
        }

        private string GetFormattedSize(long size)
        {
            if (size < 1024) return size + " B";
            if (size < 1048576) return (size / 1024.0).ToString("F2") + " KB";
            if (size < 1073741824) return (size / 1048576.0).ToString("F2") + " MB";
            return (size / 1073741824.0).ToString("F2") + " GB";
        }

        private void FadeIn(UIElement element, double durationSeconds)
        {
            var fadeInAnimation = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = 0,
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(durationSeconds),
                EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseIn }
            };
            element.BeginAnimation(OpacityProperty, fadeInAnimation);
        }

        private void FadeOut(UIElement element, double durationSeconds)
        {
            var fadeOutAnimation = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = 1.0,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(durationSeconds),
                EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
            };
            element.BeginAnimation(OpacityProperty, fadeOutAnimation);
        }

        private bool HasBattery()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Battery"))
                using (var results = searcher.Get())
                {
                    return results.Count > 0;
                }
            }
            catch
            {
                return false;
            }
        }

        private int checkWinVer()
        {
            string keyPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";
            string valueName = "CurrentBuild";

            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(keyPath))
            {
                if (key != null)
                {
                    object value = key.GetValue(valueName);

                    if (value != null && int.TryParse(value.ToString(), out int build))
                    {
                        return build;
                    }
                }
            }
            return 19045;
        }

        private void sfc_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("cmd.exe", "/k sfc /scannow");
            mw.RebootNotify(3);
            MarkApplied(sfc);
        }

        private void dism_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("cmd.exe", "/k DISM /Online /Cleanup-Image /RestoreHealth");
            mw.RebootNotify(3);
            MarkApplied(dism);
        }

        private async void temp_Click(object sender, RoutedEventArgs e)
        {
            var languageCode = Properties.Settings.Default.lang ?? "en";
            var sr = MainWindow.Localization.LoadLocalization(languageCode, "sr");

            t.Text = sr["main"]["status"];
            t.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            double targetWidth = t.DesiredSize.Width * 1.3;
            pbCache.Width = targetWidth > 150 ? targetWidth : 200;

            if (mw != null) mw.NavigationView_Root.IsEnabled = false;
            temp.IsEnabled = false;

            ring.Visibility = Visibility.Collapsed;
            pbCache.Visibility = Visibility.Visible;
            pbCache.Value = 0;
            loadingPanel.Visibility = Visibility.Visible;

            FadeIn(loadingPanel, 300);
            await Task.Delay(300);

            await Task.Run(() =>
            {
                string dir1 = Path.GetTempPath();
                string dir2 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp");

                List<string> filesToDelete = new List<string>();
                List<string> dirsToDelete = new List<string>();

                Action<string> scanDir = (d) => {
                    try
                    {
                        if (Directory.Exists(d))
                        {
                            filesToDelete.AddRange(Directory.GetFiles(d, "*.*", SearchOption.AllDirectories));
                            dirsToDelete.AddRange(Directory.GetDirectories(d, "*.*", SearchOption.AllDirectories));
                        }
                    }
                    catch { }
                };

                scanDir(dir1);
                scanDir(dir2);

                int total = filesToDelete.Count;
                if (total == 0) total = 1;
                int current = 0;

                foreach (var file in filesToDelete)
                {
                    try { File.Delete(file); } catch { }
                    current++;
                    System.Windows.Application.Current.Dispatcher.Invoke(() => pbCache.Value = (double)current / total * 100);
                }

                dirsToDelete.Sort((a, b) => b.Length.CompareTo(a.Length));
                foreach (var dir in dirsToDelete)
                {
                    try { Directory.Delete(dir, false); } catch { }
                }
            });

            FadeOut(loadingPanel, 300);
            await Task.Delay(300);

            pbCache.Visibility = Visibility.Collapsed;
            loadingPanel.Visibility = Visibility.Collapsed;
            if (mw != null) mw.NavigationView_Root.IsEnabled = true;
            MarkApplied(temp);
            UpdateTempSizeLabel();
        }

        private void MarkApplied(MicaWPF.Controls.Button btn)
        {
            btn.IsEnabled = false;
            var languageCode = Properties.Settings.Default.lang ?? "en";
            var basel = MainWindow.Localization.LoadLocalization(languageCode, "base");
            btn.Content = basel["def"]["applied"];
        }

        private void SetBtn(MicaWPF.Controls.Button btn, string normalText, string appliedText)
        {
            btn.Content = btn.IsEnabled ? normalText : appliedText;
        }

        private void LoadLang()
        {
            var languageCode = Properties.Settings.Default.lang ?? "en";
            var sr = MainWindow.Localization.LoadLocalization(languageCode, "sr");
            var main = MainWindow.Localization.LoadLocalization(languageCode, "base");
            var compon = MainWindow.Localization.LoadLocalization(languageCode, "compon");
            var tooltips = MainWindow.Localization.LoadLocalization(languageCode, "tooltips");
            string applied = main["def"]["applied"];

            label.Text = sr["main"]["label"];
            sfclabel.Text = sr["main"]["sfclabel"];
            dismlabel.Text = sr["main"]["dismlabel"];
            templabel.Text = sr["main"]["templabel"];
            piplabel.Text = sr["main"]["pipcache"];
            batterylabel.Text = sr["main"]["batterylabel"];

            recyclebinlabel.Text = sr["main"]["recyclebinlabel"];
            thumbcachelabel.Text = sr["main"]["thumbcachelabel"];

            UpdateTempSizeLabel();
            UpdatePipSizeLabel();
            UpdateRecycleBinSizeLabel();
            UpdateThumbCacheSizeLabel();

            SetBtn(sfc, sr["main"]["b2"], applied);
            SetBtn(dism, sr["main"]["b2"], applied);
            SetBtn(temp, sr["main"]["b4"], applied);
            SetBtn(pipcache, sr["main"]["b4"], applied);
            SetBtn(recyclebin, sr["main"]["b4"], applied);
            SetBtn(thumbcache, sr["main"]["b4"], applied);
            SetBtn(report, sr["main"]["reportbutton"], applied);

            bitlocker.Header = sr["main"]["bitlocker"];
            chkdsk.Header = sr["main"]["chkdsk"];
            coreisol.Header = sr["main"]["coreisol"];
            hybern.Header = sr["main"]["hybern"];
            smartscreen.Header = sr["main"]["smartscreen"];
            uac.Header = sr["main"]["uac"];
            sticky.Header = sr["main"]["sticky"];
            bing.Header = sr["main"]["bing"];
            telemetry.Header = sr["main"]["telemetry"];

            sys_tooltip_sfc.Content = tooltips["main"]["sfc"];
            sys_tooltip_dism.Content = tooltips["main"]["dism"];
            sys_tooltip_sticky.Content = tooltips["main"]["sticky"];
            sys_tooltip_coreisol.Content = tooltips["main"]["coreisol"];
            sys_tooltip_uac.Content = tooltips["main"]["duac"];
            sys_tooltip_smartscreen.Content = tooltips["main"]["smartscr"];
            sys_tooltip_hyber.Content = tooltips["main"]["hybern"];
            sys_tooltip_chkdsk.Content = tooltips["main"]["chkdsk"];
            sys_tooltip_bitlocker.Content = tooltips["main"]["bitlocker"];
            sys_tooltip_bing.Content = tooltips["main"]["bing"];

            foreach (var toggle in AllToggles)
            {
                toggle.OnContent = main["def"]["on"];
                toggle.OffContent = main["def"]["off"];
            }
        }

        private List<ModernWpf.Controls.ToggleSwitch> AllToggles => new()
        {
            bitlocker,
            chkdsk,
            coreisol,
            hybern,
            smartscreen,
            uac,
            sticky,
            bing,
            telemetry
        };

        private void checkReg()
        {
            try
            {
                bitlocker.IsOn = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\BitLocker")?.GetValue("PreventDeviceEncryption")?.Equals(1) ?? false;
                chkdsk.IsOn = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager")?.GetValue("AutoChkTimeout")?.Equals(60) ?? false;
                coreisol.IsOn = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios")?.GetValue("HypervisorEnforcedCodeIntegrity")?.Equals(0) ?? false;
                hybern.IsOn = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Power")?.GetValue("HibernateEnabled")?.Equals(0) ?? false;
                telemetry.IsOn = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Policies\DataCollection")?.GetValue("AllowTelemetry")?.Equals(0) ?? false;
                smartscreen.IsOn = (Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System")?.GetValue("EnableSmartScreen")?.Equals(0) ?? false) ||
              (Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer")?.GetValue("SmartScreenEnabled")?.Equals("Off") ?? false);
                uac.IsOn = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System")?.GetValue("EnableLUA")?.Equals(0) ?? false;

                sticky.IsOn = (Registry.CurrentUser.OpenSubKey(@"Control Panel\Accessibility\StickyKeys")?.GetValue("Flags")?.Equals("506") ?? false)
                          || (Registry.CurrentUser.OpenSubKey(@"Control Panel\Accessibility\ToggleKeys")?.GetValue("Flags")?.Equals("58") ?? false)
                          || (Registry.CurrentUser.OpenSubKey(@"Control Panel\Accessibility\Keyboard Response")?.GetValue("Flags")?.Equals("122") ?? false);

                bing.IsOn = Registry.CurrentUser.OpenSubKey(@"Software\Policies\Microsoft\Windows\Explorer")?.GetValue("DisableSearchBoxSuggestions")?.Equals(1) ?? false;
            }
            catch (Exception ex)
            {
            }
        }

        private void report_Click(object sender, RoutedEventArgs e)
        {
            var languageCode = Properties.Settings.Default.lang ?? "en";
            var sr = MainWindow.Localization.LoadLocalization(languageCode, "sr");
            Microsoft.Win32.SaveFileDialog saveFileDialog1 = new Microsoft.Win32.SaveFileDialog();
            saveFileDialog1.Filter = "HTML (*.html)|*.html";
            saveFileDialog1.Title = "Microsoft Battery Report";
            saveFileDialog1.FileName = "battery-report.html";
            if (saveFileDialog1.ShowDialog() == true)
            {
                string reportPath = saveFileDialog1.FileName;
                Process.Start("cmd.exe", $"/c powercfg /batteryreport /output \"{reportPath}\"");
                MarkApplied(report);
            }
        }

        private void sticky_Toggled(object sender, RoutedEventArgs e)
        {
            if (isLoaded)
            {
                Registry.CurrentUser.CreateSubKey(@"Control Panel\Accessibility\StickyKeys").SetValue("Flags", sticky.IsOn ? "506" : "510");
                Registry.CurrentUser.CreateSubKey(@"Control Panel\Accessibility\Keyboard Response").SetValue("Flags", sticky.IsOn ? "122" : "126");
                Registry.CurrentUser.CreateSubKey(@"Control Panel\Accessibility\ToggleKeys").SetValue("Flags", sticky.IsOn ? "58" : "62");
                mw.RebootNotify(1);
            }
        }

        private void coreisol_Toggled(object sender, RoutedEventArgs e)
        {
            if (isLoaded)
            {
                Registry.LocalMachine.CreateSubKey(@"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios")
                    .SetValue("HypervisorEnforcedCodeIntegrity", coreisol.IsOn ? 0 : 1);
                mw.RebootNotify(1);
            }
        }

        private void uac_Toggled(object sender, RoutedEventArgs e)
        {
            var languageCode = Properties.Settings.Default.lang ?? "en";
            var sr = MainWindow.Localization.LoadLocalization(languageCode, "sr");

            if (isLoaded)
            {
                if (checkWinVer() >= 22621 && uac.IsOn)
                {
                    var res = iNKORE.UI.WPF.Modern.Controls.MessageBox.Show(sr["status"]["uacwarn"], "MakuTweaker", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                    if (res == MessageBoxResult.No)
                    {
                        uac.IsOn = false;
                        return;
                    }
                }
                switch (uac.IsOn)
                {
                    case true:
                        Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System").SetValue("EnableLUA", 0);
                        Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Policies\Attachments")?.SetValue("SaveZoneInformation", 1, RegistryValueKind.DWord);
                        Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Policies\Associations")?.SetValue("LowRiskFileTypes", ".exe;.msi;.bat;", RegistryValueKind.String);
                        break;
                    case false:
                        Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System").SetValue("EnableLUA", 1);
                        break;
                }
            }
        }

        private void smartscreen_Toggled(object sender, RoutedEventArgs e)
        {
            if (isLoaded)
            {
                Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System").SetValue("EnableSmartScreen", smartscreen.IsOn ? 0 : 1);
                Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer").SetValue("SmartScreenEnabled", smartscreen.IsOn ? "Off" : "Warn", RegistryValueKind.String);
                Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Policies\Attachments").SetValue("SaveZoneInformation", smartscreen.IsOn ? 1 : 0, RegistryValueKind.DWord);
            }
        }

        private void hybern_Toggled(object sender, RoutedEventArgs e)
        {
            if (isLoaded)
            {
                Process.Start("cmd.exe", $"/C powercfg /h {(hybern.IsOn ? "off" : "on")}");
                mw.RebootNotify(1);
            }
        }

        private void bing_Toggled(object sender, RoutedEventArgs e)
        {
            if (isLoaded)
            {
                Registry.CurrentUser.CreateSubKey(@"Software\Policies\Microsoft\Windows\Explorer").SetValue("DisableSearchBoxSuggestions", bing.IsOn ? 1 : 0);
            }
        }

        private void chkdsk_Toggled(object sender, RoutedEventArgs e)
        {
            if (isLoaded)
            {
                Registry.LocalMachine.CreateSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager").SetValue("AutoChkTimeout", chkdsk.IsOn ? 60 : 8);
            }
        }

        private void bitlocker_Toggled(object sender, RoutedEventArgs e)
        {
            if (isLoaded)
            {
                Registry.LocalMachine.CreateSubKey(@"SYSTEM\CurrentControlSet\Control\BitLocker").SetValue("PreventDeviceEncryption", bitlocker.IsOn ? 1 : 0, RegistryValueKind.DWord);
            }
        }

        private void telemetry_Toggled(object sender, RoutedEventArgs e)
        {
            if (isLoaded)
            {
                switch (telemetry.IsOn)
                {
                    case true:
                        Registry.LocalMachine.CreateSubKey(@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Policies\DataCollection").SetValue("AllowTelemetry", 0);
                        Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection").SetValue("AllowTelemetry", 0);
                        Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection").SetValue("MaxTelemetryAllowed", 0);
                        Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows NT\CurrentVersion\Software Protection Platform").SetValue("NoGenTicket", 1);
                        Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection").SetValue("DoNotShowFeedbackNotifications", 1);

                        Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\AppCompat").SetValue("AITEnable", 0);
                        Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\AppCompat").SetValue("AllowTelemetry", 0);
                        Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\AppCompat").SetValue("DisableEngine", 1);
                        Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\AppCompat").SetValue("DisableInventory", 1);
                        Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\AppCompat").SetValue("DisablePCA", 1);
                        Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\AppCompat").SetValue("DisableUAR", 1);
                        break;
                    case false:
                        Registry.LocalMachine.CreateSubKey(@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Policies\DataCollection").SetValue("AllowTelemetry", 1);
                        Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection").SetValue("AllowTelemetry", 1);
                        Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection").SetValue("MaxTelemetryAllowed", 1);
                        Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows NT\CurrentVersion\Software Protection Platform").SetValue("NoGenTicket", 0);
                        Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection").SetValue("DoNotShowFeedbackNotifications", 0);

                        Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\AppCompat").SetValue("AITEnable", 1);
                        Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\AppCompat").SetValue("AllowTelemetry", 1);
                        Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\AppCompat").SetValue("DisableEngine", 0);
                        Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\AppCompat").SetValue("DisableInventory", 0);
                        Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\AppCompat").SetValue("DisablePCA", 0);
                        Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\AppCompat").SetValue("DisableUAR", 0);
                        break;
                }
            }
        }

        private async void pipcache_Click(object sender, RoutedEventArgs e)
        {
            var languageCode = Properties.Settings.Default.lang ?? "en";
            var sr = MainWindow.Localization.LoadLocalization(languageCode, "sr");

            t.Text = sr["main"]["status"];
            t.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            double targetWidth = t.DesiredSize.Width * 1.3;
            pbCache.Width = targetWidth > 150 ? targetWidth : 200;

            if (mw != null) mw.NavigationView_Root.IsEnabled = false;
            pipcache.IsEnabled = false;

            ring.Visibility = Visibility.Collapsed;
            pbCache.Visibility = Visibility.Visible;
            pbCache.Value = 0;
            loadingPanel.Visibility = Visibility.Visible;

            FadeIn(loadingPanel, 300);
            await Task.Delay(300);

            await Task.Run(() =>
            {
                string pipDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "pip");

                List<string> filesToDelete = new List<string>();
                List<string> dirsToDelete = new List<string>();

                try
                {
                    if (Directory.Exists(pipDir))
                    {
                        filesToDelete.AddRange(Directory.GetFiles(pipDir, "*.*", SearchOption.AllDirectories));
                        dirsToDelete.AddRange(Directory.GetDirectories(pipDir, "*.*", SearchOption.AllDirectories));
                    }
                }
                catch { }

                int total = filesToDelete.Count;
                if (total == 0) total = 1;
                int current = 0;

                foreach (var file in filesToDelete)
                {
                    try { File.Delete(file); } catch { }
                    current++;
                    System.Windows.Application.Current.Dispatcher.Invoke(() => pbCache.Value = (double)current / total * 100);
                }

                dirsToDelete.Sort((a, b) => b.Length.CompareTo(a.Length));
                foreach (var dir in dirsToDelete)
                {
                    try { Directory.Delete(dir, false); } catch { }
                }
            });

            FadeOut(loadingPanel, 300);
            await Task.Delay(300);

            pbCache.Visibility = Visibility.Collapsed;
            loadingPanel.Visibility = Visibility.Collapsed;
            if (mw != null) mw.NavigationView_Root.IsEnabled = true;
            MarkApplied(pipcache);
            UpdatePipSizeLabel();
        }

        private string WindowsRoot() => Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        private string SystemDriveRoot() => WindowsRoot().Substring(0, 3);
        private async void UpdateSizeLabel(ModernWpf.Controls.ProgressRing ring, TextBlock text, StackPanel panel, Func<long> computeSize)
        {
            text.Visibility = Visibility.Collapsed;
            ring.Visibility = Visibility.Visible;
            ring.IsActive = true;

            long sizeBytes = await Task.Run(computeSize);

            if (sizeBytes < 104857600)
            {
                panel.Visibility = Visibility.Collapsed;
            }
            else
            {
                string size = GetFormattedSize(sizeBytes);
                text.Text = $" ({size})";
                ring.IsActive = false;
                ring.Visibility = Visibility.Collapsed;
                text.Visibility = Visibility.Visible;
            }
        }

        private async Task CleanFilesAsync(string[] files, MicaWPF.Controls.Button btn, string statusKey, Action postAction = null)
        {
            var languageCode = Properties.Settings.Default.lang ?? "en";
            var sr = MainWindow.Localization.LoadLocalization(languageCode, "sr");

            t.Text = sr["main"][statusKey];
            t.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double targetWidth = t.DesiredSize.Width * 1.3;
            pbCache.Width = targetWidth > 150 ? targetWidth : 200;

            if (mw != null) mw.NavigationView_Root.IsEnabled = false;
            btn.IsEnabled = false;

            ring.Visibility = Visibility.Collapsed;
            pbCache.Visibility = Visibility.Visible;
            pbCache.Value = 0;
            loadingPanel.Visibility = Visibility.Visible;

            FadeIn(loadingPanel, 300);
            await Task.Delay(300);

            await Task.Run(() =>
            {
                int total = files.Length;
                if (total == 0) total = 1;
                int current = 0;

                foreach (var file in files)
                {
                    try { File.Delete(file); } catch { }
                    current++;
                    System.Windows.Application.Current.Dispatcher.Invoke(() => pbCache.Value = (double)current / total * 100);
                }

                try { postAction?.Invoke(); } catch { }
            });

            FadeOut(loadingPanel, 300);
            await Task.Delay(300);

            pbCache.Visibility = Visibility.Collapsed;
            loadingPanel.Visibility = Visibility.Collapsed;
            if (mw != null) mw.NavigationView_Root.IsEnabled = true;
            MarkApplied(btn);
        }
        private void RestartExplorer()
        {
            try
            {
                foreach (var proc in Process.GetProcessesByName("explorer"))
                {
                    try { proc.Kill(); } catch { }
                }
            }
            catch { }
        }

        private void UpdateRecycleBinSizeLabel()
            => UpdateSizeLabel(recyclebinSizeRing, recyclebinSizeText, recyclebinFullPanel, () =>
            {
                long total = 0;
                foreach (var drive in DriveInfo.GetDrives())
                {
                    try
                    {
                        if (drive.IsReady && drive.DriveType == DriveType.Fixed)
                            total += GetDirectorySize(Path.Combine(drive.Name, "$Recycle.Bin"));
                    }
                    catch { }
                }
                return total;
            });

        private void UpdateThumbCacheSizeLabel()
            => UpdateSizeLabel(thumbcacheSizeRing, thumbcacheSizeText, thumbcacheFullPanel, () =>
            {
                long total = 0;
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Microsoft", "Windows", "Explorer");
                try
                {
                    if (Directory.Exists(dir))
                        foreach (var f in Directory.GetFiles(dir, "thumbcache_*.db"))
                            try { total += new FileInfo(f).Length; } catch { }
                }
                catch { }
                return total;
            });

        private async void recyclebin_Click(object sender, RoutedEventArgs e)
        {
            var languageCode = Properties.Settings.Default.lang ?? "en";
            var sr = MainWindow.Localization.LoadLocalization(languageCode, "sr");

            t.Text = sr["main"]["status"];
            t.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double targetWidth = t.DesiredSize.Width * 1.3;
            pbCache.Width = targetWidth > 150 ? targetWidth : 200;

            if (mw != null) mw.NavigationView_Root.IsEnabled = false;
            recyclebin.IsEnabled = false;

            pbCache.Visibility = Visibility.Collapsed;
            ring.Visibility = Visibility.Visible;
            ring.IsActive = true;
            loadingPanel.Visibility = Visibility.Visible;

            FadeIn(loadingPanel, 300);
            await Task.Delay(300);

            await Task.Run(() =>
            {
                try
                {
                    SHEmptyRecycleBin(IntPtr.Zero, null,
                        RecycleFlags.SHERB_NOCONFIRMATION | RecycleFlags.SHERB_NOPROGRESSUI | RecycleFlags.SHERB_NOSOUND);
                }
                catch { }
            });

            FadeOut(loadingPanel, 300);
            await Task.Delay(300);

            ring.IsActive = false;
            ring.Visibility = Visibility.Collapsed;
            loadingPanel.Visibility = Visibility.Collapsed;

            if (mw != null) mw.NavigationView_Root.IsEnabled = true;
            MarkApplied(recyclebin);
            UpdateRecycleBinSizeLabel();
        }

        private async void thumbcache_Click(object sender, RoutedEventArgs e)
        {
            string explorerDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft", "Windows", "Explorer");

            string[] files = Array.Empty<string>();
            try
            {
                if (Directory.Exists(explorerDir))
                    files = Directory.GetFiles(explorerDir, "thumbcache_*.db");
            }
            catch { }

            await CleanFilesAsync(files, thumbcache, "status", postAction: RestartExplorer);
            UpdateThumbCacheSizeLabel();
        }
    }
}