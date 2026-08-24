using Microsoft.Win32;
using System;
using System.Globalization;
using System.Management;
using System.Windows.Controls;
using System.Diagnostics;

namespace MakuTweakerNew
{
    public partial class WinInfo : Page
    {
        private dynamic _winInfo;

        public WinInfo()
        {
            InitializeComponent();
            LoadLang();
            LoadOsInfo();
            LoadLocaleInfo();
            LoadIsolationInfo();
            LoadProtectionInfo();
        }

        private void LoadLang()
        {
            var languageCode = Properties.Settings.Default.lang ?? "en";
            _winInfo = MainWindow.Localization.LoadLocalization(languageCode, "wininfo");

            label.Text = _winInfo["main"]["label"];

            osNameCard.Header = _winInfo["main"]["name"];
            osVersionCard.Header = _winInfo["main"]["version"];
            osEditionCard.Header = _winInfo["main"]["edition"];
            osBuildCard.Header = _winInfo["main"]["build"];
            installDateCard.Header = _winInfo["main"]["install_date"];
            activationCard.Header = _winInfo["main"]["activation"];

            localeSection.Header = _winInfo["main"]["locale_header"];
            isolationSection.Header = _winInfo["main"]["isolation_header"];
            protectionSection.Header = _winInfo["main"]["protection_header"];

            sysLanguageCard.Header = _winInfo["main"]["language"];
            regionCard.Header = _winInfo["main"]["region"];
            timezoneCard.Header = _winInfo["main"]["timezone"];
            coreIsolationCard.Header = _winInfo["main"]["coreisol"];
            uacCard.Header = _winInfo["main"]["uac"];
        }

        private void LoadOsInfo()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Caption, InstallDate FROM Win32_OperatingSystem"))
                {
                    foreach (var item in searcher.Get())
                    {
                        osNameText.Text = item["Caption"]?.ToString() ?? "Unknown OS";

                        string installDateRaw = item["InstallDate"]?.ToString();
                        if (!string.IsNullOrEmpty(installDateRaw))
                        {
                            DateTime dt = ManagementDateTimeConverter.ToDateTime(installDateRaw);
                            installDateText.Text = dt.ToString("dd.MM.yyyy");
                        }
                    }
                }

                string displayVersion = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion", "DisplayVersion", "")?.ToString();
                string releaseId = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion", "ReleaseId", "")?.ToString();
                osVersionText.Text = !string.IsNullOrEmpty(displayVersion) ? displayVersion : releaseId;

                osEditionText.Text = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion", "EditionID", "Unknown")?.ToString();

                string currentBuild = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion", "CurrentBuild", "")?.ToString();
                string ubr = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion", "UBR", "")?.ToString();
                osBuildText.Text = $"{currentBuild}.{ubr}";

                bool isActivated = false;
                using (var searcher = new ManagementObjectSearcher(@"root\CIMV2", "SELECT LicenseStatus FROM SoftwareLicensingProduct WHERE PartialProductKey IS NOT NULL"))
                {
                    foreach (var queryObj in searcher.Get())
                    {
                        if (Convert.ToInt32(queryObj["LicenseStatus"]) == 1)
                        {
                            isActivated = true;
                            break;
                        }
                    }
                }
                activationText.Text = isActivated ? _winInfo["main"]["activated"] : _winInfo["main"]["not_activated"];
            }
            catch (Exception)
            {
                osNameText.Text = "Error reading OS info";
            }
        }

        private void LoadLocaleInfo()
        {
            try
            {
                sysLanguageText.Text = CultureInfo.CurrentUICulture.DisplayName;
                regionText.Text = RegionInfo.CurrentRegion.NativeName;
                timezoneText.Text = TimeZoneInfo.Local.DisplayName;
            }
            catch
            {
                sysLanguageText.Text = "N/A";
            }
        }

        private void LoadIsolationInfo()
        {
            try
            {
                int wdacStatus = 0;

                using (var searcher = new ManagementObjectSearcher(@"root\Microsoft\Windows\DeviceGuard", "SELECT CodeIntegrityPolicyEnforcementStatus FROM Win32_DeviceGuard"))
                {
                    foreach (var item in searcher.Get())
                    {
                        if (item["CodeIntegrityPolicyEnforcementStatus"] != null)
                        {
                            wdacStatus = Convert.ToInt32(item["CodeIntegrityPolicyEnforcementStatus"]);
                        }
                        break;
                    }
                }

                if (wdacStatus == 2)
                {
                    appControlText.Text = _winInfo["main"]["on_enforced"];
                }
                else if (wdacStatus == 1)
                {
                    appControlText.Text = _winInfo["main"]["on_audit"];
                }
                else
                {
                    int sacState = (int)(Registry.GetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\CI\Policy", "VerifiedAndReputablePolicyState", 0) ?? 0);

                    if (sacState == 1)
                        appControlText.Text = _winInfo["main"]["on_enforced"];
                    else if (sacState == 2)
                        appControlText.Text = _winInfo["main"]["on_audit"];
                    else
                        appControlText.Text = _winInfo["main"]["status_off"];
                }
            }
            catch { appControlText.Text = _winInfo["main"]["unknown"]; }

            try
            {
                using (var searcher = new ManagementObjectSearcher(@"root\Microsoft\Windows\DeviceGuard", "SELECT VirtualizationBasedSecurityStatus FROM Win32_DeviceGuard"))
                {
                    bool found = false;
                    foreach (var item in searcher.Get())
                    {
                        int vbsStatus = Convert.ToInt32(item["VirtualizationBasedSecurityStatus"]);
                        vbsText.Text = vbsStatus == 2 ? _winInfo["main"]["working"] : vbsStatus == 1 ? _winInfo["main"]["on_not_working"] : _winInfo["main"]["status_off"];
                        found = true;
                        break;
                    }
                    if (!found) vbsText.Text = _winInfo["main"]["not_supported"];
                }
            }
            catch { vbsText.Text = _winInfo["main"]["unknown"]; }

            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT HypervisorPresent FROM Win32_ComputerSystem"))
                {
                    foreach (var item in searcher.Get())
                    {
                        bool isHypervisorPresent = Convert.ToBoolean(item["HypervisorPresent"]);
                        hypervText.Text = isHypervisorPresent ? _winInfo["main"]["present"] : _winInfo["main"]["status_off_or_not_found"];
                        break;
                    }
                }
            }
            catch { hypervText.Text = _winInfo["main"]["unknown"]; }
        }

        private void LoadProtectionInfo()
        {
            try
            {
                int hvciEnabled = (int)(Registry.GetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity", "Enabled", 0) ?? 0);
                coreIsolationText.Text = hvciEnabled == 1 ? _winInfo["main"]["status_on"] : _winInfo["main"]["status_off"];
            }
            catch { coreIsolationText.Text = _winInfo["main"]["unknown"]; }

            try
            {
                bool isDefenderRunning = Process.GetProcessesByName("MsMpEng").Length > 0;

                if (isDefenderRunning)
                {
                    int disableRealtime = (int)(Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows Defender\Real-Time Protection", "DisableRealtimeMonitoring", 0) ?? 0);
                    defenderText.Text = disableRealtime == 0 ? _winInfo["main"]["on_realtime"] : _winInfo["main"]["status_off"];
                }
                else
                {
                    defenderText.Text = _winInfo["main"]["status_off"];
                }
            }
            catch { defenderText.Text = _winInfo["main"]["unknown"]; }

            try
            {
                int uacConsent = (int)(Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "ConsentPromptBehaviorAdmin", 0) ?? 0);
                int enableLua = (int)(Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "EnableLUA", 0) ?? 0);

                if (enableLua == 0) uacText.Text = _winInfo["main"]["status_off"];
                else if (uacConsent == 0) uacText.Text = _winInfo["main"]["uac_off_no_prompt"];
                else if (uacConsent == 5) uacText.Text = _winInfo["main"]["status_on"];
                else uacText.Text = string.Format(_winInfo["main"]["uac_medium"].ToString(), uacConsent);
            }
            catch { uacText.Text = _winInfo["main"]["unknown"]; }

            try
            {
                string smartScreenVal = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer", "SmartScreenEnabled", "Off")?.ToString();
                if (string.IsNullOrEmpty(smartScreenVal)) smartScreenVal = "Off";

                smartScreenText.Text = smartScreenVal.Equals("Off", StringComparison.OrdinalIgnoreCase) ? _winInfo["main"]["status_off"] : _winInfo["main"]["status_on"];
            }
            catch { smartScreenText.Text = _winInfo["main"]["unknown"]; }
        }
    }
}