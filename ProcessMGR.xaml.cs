using MakuTweakerNew.Properties;
using LibreHardwareMonitor.Hardware;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Windows.Interop;

namespace MakuTweakerNew
{
    public partial class ProcessMGR : Page
    {
        private Computer _storageComputer;
        private bool _cpuInitDone = false;
        private bool _gpuInitDone = false;
        private bool _netInitDone = false;
        private bool _diskInitDone = false;
        private dynamic pmgrLang;
        private dynamic monitoringLang;
        private GraphState _diskTempGraph;
        private bool _hasDiskTempData = false;
        private List<IHardware> _storageHardwares = new List<IHardware>();
        private static bool _hasAutoStarted = false;
        private ObservableCollection<ProcessItem> _items = new ObservableCollection<ProcessItem>();
        private readonly ObservableCollection<DeviceOption> _deviceOptionsList = new ObservableCollection<DeviceOption>();
        private bool _isUpdatingPaused = false;
        private int _cpuTdpZeroTicks = 0;
        private int _gpuTdpZeroTicks = 0;
        private DispatcherTimer _timer;
        private long _dynamicMemoryThreshold = 524288000;
        private DispatcherTimer _saveBoundsTimer;
        bool isLoaded = false;
        private bool helpVisible = false;
        MainWindow mw = (MainWindow)Application.Current.MainWindow;
        private static bool _isExclusiveMode = false;
        private bool _isNarrowView = false;
        private string _currentSearchQuery = "";
        private WindowState _previousWindowState;
        private DispatcherTimer _performanceTimer;
        private bool _performanceVisible = false;
        private bool _performanceMonitoringInitialized = false;
        private bool _isChangingPerformanceCombo = false;
        private string _currentPerformanceSection = string.IsNullOrEmpty(Properties.Settings.Default.LastPerformanceTab) ? "Cpu" : Properties.Settings.Default.LastPerformanceTab;
        private Computer _hardwareComputer;
        private IHardware _selectedGpuHardware;
        private string _selectedNetworkId = AllNetworkAdaptersTag;
        private string _selectedDiskInstance = TotalDiskInstance;
        private List<IHardware> _cpuHardwares = new List<IHardware>();
        private List<IHardware> _gpuHardwares = new List<IHardware>();
        private List<NetworkInterface> _networkInterfaces = new List<NetworkInterface>();
        private readonly Dictionary<string, NetworkSample> _networkSamples = new Dictionary<string, NetworkSample>();
        private readonly Dictionary<string, PerformanceCounter> _diskUsageCounters = new Dictionary<string, PerformanceCounter>();
        private PerformanceCounter _cpuUsageCounter;
        private List<PerformanceCounter> _cpuCoreUsageCounters;
        private List<GraphState> _cpuCoreGraphs;
        private enum CpuViewMode { Overall, Physical, Logical }
        private CpuViewMode _cpuViewMode = CpuViewMode.Overall;
        private int _cpuPhysicalCoreCount = 0;
        private string _realIpv4 = "-";
        private string _realExtIp = "-";
        private string _realMac = "-";
        private bool _isNetworkPrivacyDisabled = false;
        private GraphState _cpuUsageGraph;
        private GraphState _cpuClockGraph;
        private GraphState _cpuTdpGraph;
        private GraphState _cpuTempGraph;
        private GraphState _gpuCoreLoadGraph;
        private GraphState _gpuMemoryLoadGraph;
        private GraphState _gpuTdpGraph;
        private GraphState _gpuTempGraph;
        private GraphState _gpuEncoderLoadGraph;
        private GraphState _networkDownloadGraph;
        private GraphState _networkUploadGraph;
        private GraphState _diskUsageGraph;
        private GraphState _diskReadGraph;
        private GraphState _diskWriteGraph;
        private GraphState _ramUsageGraph;
        private GraphState _cpuNavGraph;
        private GraphState _gpuNavGraph;
        private GraphState _ramNavGraph;
        private GraphState _networkNavGraph;
        private GraphState _diskNavGraph;

        private const int WM_DEVICECHANGE = 0x0219;
        private HwndSource _hwndSource;

        private Dictionary<string, PerformanceCounter> _cpuUsageCounters = new Dictionary<string, PerformanceCounter>();
        private PerformanceCounter _cpuFreqCounter;
        private PerformanceCounter _cpuPerfCounter;
        private double _lastValidCpuClock = 0;

        private readonly Dictionary<string, PerformanceCounter> _diskReadCounters = new Dictionary<string, PerformanceCounter>();
        private readonly Dictionary<string, PerformanceCounter> _diskWriteCounters = new Dictionary<string, PerformanceCounter>();
        private readonly Dictionary<string, PerformanceCounter> _diskResponseCounters = new Dictionary<string, PerformanceCounter>();
        private DateTime _lastCpuStaticInfoUpdate = DateTime.MinValue;

        private double _fallbackGpuCoreLoad = 0;
        private double _fallbackGpuEncoderLoad = 0;
        private string _fallbackGpuVramString = "0 GB";
        private bool _isQueryingWmiGpu = false;
        private bool _isLoadingDiskInfo = false;

        private class DiskHistory
        {
            public Queue<double> Usage { get; } = new Queue<double>();
            public Queue<double> Read { get; } = new Queue<double>();
            public Queue<double> Write { get; } = new Queue<double>();
            public Queue<double> Temp { get; } = new Queue<double>();
        }

        private Dictionary<string, DiskHistory> _diskHistoryCache = new Dictionary<string, DiskHistory>();

        private void SafeEnqueue(Queue<double> q, double val)
        {
            if (double.IsNaN(val) || double.IsInfinity(val)) val = 0;
            val = Math.Max(0, val);
            q.Enqueue(val);
            while (q.Count > GraphHistoryLimit) q.Dequeue();
        }

        private const int GraphHistoryLimit = 60;
        private const string AllNetworkAdaptersTag = "__all_network__";
        private const string TotalDiskInstance = "_Total";

        private int _cpuVisibleCards = 2;
        private int _gpuVisibleCards = 2;

        private bool? _lastIsNarrow = null;
        private bool? _lastIsCompact = null;
        private bool? _lastIsHeightTooSmall = null;

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private class MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto, SetLastError = true)]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx([System.Runtime.InteropServices.In, System.Runtime.InteropServices.Out] MEMORYSTATUSEX lpBuffer);

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, int processId);
        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);
        private const uint PROCESS_TERMINATE = 0x0001;

        [System.Runtime.InteropServices.DllImport("ntdll.dll", SetLastError = true)]
        private static extern int NtQueryInformationProcess(IntPtr processHandle, int processInformationClass, ref int processInformation, int processInformationLength, out int returnLength);

        private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
        private bool IsCriticalSystemProcess(int processId)
        {
            if (processId <= 4) return true;
            IntPtr hProcess = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
            if (hProcess == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                int isCritical = 0;
                int status = NtQueryInformationProcess(hProcess, 29, ref isCritical, sizeof(int), out _);
                return (status >= 0 && isCritical == 1);
            }
            finally
            {
                CloseHandle(hProcess);
            }
        }

        private Dictionary<string, string> _friendlyNameCache = new Dictionary<string, string>();

        private string GetFriendlyProcessName(Process p)
        {
            string rawName = p.ProcessName;
            int displayMode = Properties.Settings.Default.ProcessNameDisplayMode;
            if (displayMode == 1) return rawName;

            if (_friendlyNameCache.TryGetValue(rawName, out string cachedName))
                return cachedName;

            string finalName = rawName;
            try
            {
                string description = p.MainModule?.FileVersionInfo?.FileDescription;

                if (!string.IsNullOrWhiteSpace(description) &&
                    !description.Equals(rawName, StringComparison.OrdinalIgnoreCase))
                {
                    if (displayMode == 0)
                        finalName = description;
                    else if (displayMode == 2)
                        finalName = $"{description} ({rawName})";
                }
            }
            catch { }

            _friendlyNameCache[rawName] = finalName;
            return finalName;
        }

        private bool CanTerminateProcess(int processId)
        {
            try
            {
                IntPtr handle = OpenProcess(PROCESS_TERMINATE, false, processId);
                if (handle != IntPtr.Zero)
                {
                    CloseHandle(handle);
                    return true;
                }
                return false;
            }
            catch { return false; }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_DEVICECHANGE)
            {
                _diskInfoCache = null;

                foreach (var counter in _diskUsageCounters.Values) { try { counter.Dispose(); } catch { } }
                _diskUsageCounters.Clear();

                foreach (var counter in _diskReadCounters.Values) { try { counter.Dispose(); } catch { } }
                _diskReadCounters.Clear();

                foreach (var counter in _diskWriteCounters.Values) { try { counter.Dispose(); } catch { } }
                _diskWriteCounters.Clear();

                foreach (var counter in _diskResponseCounters.Values) { try { counter.Dispose(); } catch { } }
                _diskResponseCounters.Clear();

                _selectedDiskInstance = TotalDiskInstance;

                if (_currentPerformanceSection == "Disk")
                {
                    Task.Run(async () =>
                    {
                        await Task.Delay(1000);
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            _isLoadingDiskInfo = false;
                            UpdatePerformanceSection("Disk");
                        });
                    });
                }
            }
            return IntPtr.Zero;
        }

        public bool IsExclusiveMode => _isExclusiveMode;
        public async Task ToggleExclusiveMode(bool animate = true)
        {
            if (mw == null || mw.NavigationView_Root == null) return;
            _isExclusiveMode = !_isExclusiveMode;
            var chrome = System.Windows.Shell.WindowChrome.GetWindowChrome(mw);

            if (!_isExclusiveMode)
            {
                if (AutoStartCheck != null) AutoStartCheck.IsChecked = false;
                Properties.Settings.Default.AutoStartExclusive = false;
                ResetExclusiveWindowBounds();

                if (mw.WindowState == WindowState.Maximized)
                {
                    mw.WindowState = WindowState.Normal;
                    await Task.Delay(50);
                }

                mw.MinWidth = 1150;
                mw.MaxWidth = 1150;
                mw.Width = 1150;
                mw.MinHeight = 710;
                mw.MaxHeight = 710;
                mw.Height = 710;
                mw.ResizeMode = ResizeMode.CanMinimize;
                if (chrome != null) chrome.ResizeBorderThickness = new Thickness(0);

                mw.WindowState = WindowState.Normal;
                mw.Left = SystemParameters.WorkArea.Left + (SystemParameters.WorkArea.Width - mw.Width) / 2;
                mw.Top = SystemParameters.WorkArea.Top + (SystemParameters.WorkArea.Height - mw.Height) / 2;
                await Task.Delay(20);
            }

            if (!animate)
            {
                MainContent.BeginAnimation(FrameworkElement.MarginProperty, null);
                label.BeginAnimation(FrameworkElement.MarginProperty, null);

                MainContent.Margin = _isExclusiveMode ? new Thickness(23, 0, 24, 12) : new Thickness(12, 0, 24, 12);
                label.Margin = _isExclusiveMode ? new Thickness(24, 11, 0, 10) : new Thickness(15, 11, 0, 10);
            }
            else
            {
                var ease = new CubicEase() { EasingMode = EasingMode.EaseInOut };
                var duration = TimeSpan.FromMilliseconds(300);

                var mainMarginAnim = new ThicknessAnimation { To = _isExclusiveMode ? new Thickness(23, 0, 24, 12) : new Thickness(12, 0, 24, 12), Duration = duration, EasingFunction = ease };
                var labelMarginAnim = new ThicknessAnimation { To = _isExclusiveMode ? new Thickness(24, 11, 0, 10) : new Thickness(15, 11, 0, 10), Duration = duration, EasingFunction = ease };
                var btnWidthAnim = new DoubleAnimation { To = _isExclusiveMode ? 0 : 26, Duration = duration, EasingFunction = ease };
                var btnOpacityAnim = new DoubleAnimation { To = _isExclusiveMode ? 0 : 1, Duration = duration, EasingFunction = ease };
                var btnMarginAnim = new ThicknessAnimation { To = _isExclusiveMode ? new Thickness(0, 10, 0, 0) : new Thickness(10, 10, 0, 0), Duration = duration, EasingFunction = ease };
                var restartBtnMarginAnim = new ThicknessAnimation { To = _isExclusiveMode ? new Thickness(10, 10, 0, 0) : new Thickness(5, 10, 0, 0), Duration = duration, EasingFunction = ease };

                MainContent.BeginAnimation(FrameworkElement.MarginProperty, mainMarginAnim);
                label.BeginAnimation(FrameworkElement.MarginProperty, labelMarginAnim);
            }

            if (AutoStartCheck != null)
            {
                AutoStartCheck.Visibility = (_isExclusiveMode && mw.Width >= 1050) ? Visibility.Visible : Visibility.Collapsed;
            }

            Visibility microGraphVis = _isExclusiveMode ? Visibility.Visible : Visibility.Collapsed;
            if (CpuNavGraphContainer != null) CpuNavGraphContainer.Visibility = microGraphVis;
            if (GpuNavGraphContainer != null) GpuNavGraphContainer.Visibility = microGraphVis;
            if (RamNavGraphContainer != null) RamNavGraphContainer.Visibility = microGraphVis;
            if (NetworkNavGraphContainer != null) NetworkNavGraphContainer.Visibility = microGraphVis;
            if (DiskNavGraphContainer != null) DiskNavGraphContainer.Visibility = microGraphVis;

            mw.AnimateExclusiveModeTransition(_isExclusiveMode, animate);
            mw.UpdateSettingsButtonForExclusive(true, _isExclusiveMode);

            if (animate)
            {
                var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };
                var duration = TimeSpan.FromMilliseconds(300);
                var navWidthAnim = new DoubleAnimation
                {
                    To = _isExclusiveMode ? 230 : 180,
                    Duration = duration,
                    EasingFunction = ease
                };
                NavColumn.BeginAnimation(ColumnDefinition.MinWidthProperty, navWidthAnim);
            }
            else
            {
                NavColumn.MinWidth = _isExclusiveMode ? 230 : 180;
                NavColumn.Width = new GridLength(_isExclusiveMode ? 230 : 180);
            }

            if (_isExclusiveMode)
            {
                mw.ResizeMode = ResizeMode.CanResize;
                mw.MaxWidth = double.PositiveInfinity;
                mw.MaxHeight = double.PositiveInfinity;
                mw.MinWidth = 580;
                mw.MinHeight = 380;
                if (chrome != null) chrome.ResizeBorderThickness = new Thickness(6);
                mw.WindowState = WindowState.Normal;

                if (Properties.Settings.Default.AutoStartExclusive && Properties.Settings.Default.ExclusiveWindowLeft != -10000)
                {
                    mw.Width = Math.Max(580, Properties.Settings.Default.ExclusiveWindowWidth);
                    mw.Height = Math.Max(380, Properties.Settings.Default.ExclusiveWindowHeight);
                    mw.Left = Properties.Settings.Default.ExclusiveWindowLeft;
                    mw.Top = Properties.Settings.Default.ExclusiveWindowTop;
                }
            }

            mw.UpdateLayout();
            UpdateResponsiveUI(mw.ActualWidth, mw.ActualHeight);

            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                mw.Activate();
                this.Focus();
                ProcessListView.Focus();
            }, DispatcherPriority.ApplicationIdle);
        }

        private string FormatMemory(long bytes)
        {
            double megabytes = bytes / 1024.0 / 1024.0;
            if (Properties.Settings.Default.onlyMB_processMGR)
            {
                return $"{Math.Round(megabytes, 2):N2} MB";
            }
            if (megabytes >= 1024)
            {
                return $"{Math.Round(megabytes / 1024.0, 2)} GB";
            }
            return $"{Math.Round(megabytes, 2)} MB";
        }

        [System.Runtime.InteropServices.DllImport("gdi32.dll", SetLastError = true)]
        private static extern bool DeleteObject(IntPtr hObject);
        private Dictionary<string, ImageSource> _iconCache = new Dictionary<string, ImageSource>();

        private ImageSource GetProcessIcon(Process p)
        {
            string name = p.ProcessName;
            if (_iconCache.TryGetValue(name, out ImageSource cachedIcon))
                return cachedIcon;

            ImageSource imgSource = null;
            try
            {
                string path = p.MainModule.FileName;
                using (System.Drawing.Icon icon = System.Drawing.Icon.ExtractAssociatedIcon(path))
                {
                    if (icon != null)
                    {
                        using (System.Drawing.Bitmap bmp = icon.ToBitmap())
                        {
                            IntPtr hBitmap = bmp.GetHbitmap();
                            try
                            {
                                imgSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                                    hBitmap, IntPtr.Zero, Int32Rect.Empty,
                                    System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
                                imgSource.Freeze();
                            }
                            finally { DeleteObject(hBitmap); }
                        }
                    }
                }
            }
            catch { }

            _iconCache[name] = imgSource;
            return imgSource;
        }

        public ProcessMGR()
        {
            InitializeComponent();
            LoadLang();
            InitializePerformanceGraphs();

            if (PerformanceNavList != null)
            {
                string lastTab = Properties.Settings.Default.LastPerformanceTab;
                if (string.IsNullOrEmpty(lastTab)) lastTab = "Cpu";

                bool found = false;
                foreach (ListBoxItem item in PerformanceNavList.Items)
                {
                    if (item.Tag != null && item.Tag.ToString() == lastTab)
                    {
                        PerformanceNavList.SelectedItem = item;
                        found = true;
                        break;
                    }
                }

                if (!found)
                    PerformanceNavList.SelectedIndex = 0;
            }

            string[] args = Environment.GetCommandLineArgs();
            bool launchedViaTaskmgr = args.Length > 1 && args.Any(arg => arg.IndexOf("taskmgr.exe", StringComparison.OrdinalIgnoreCase) >= 0);
            if ((Properties.Settings.Default.AutoStartExclusive || launchedViaTaskmgr) && !MainWindow.HasAutoStartedExclusive)
            {
                _isExclusiveMode = true;
                MainContent.Margin = new Thickness(23, 0, 24, 12);
                label.Margin = new Thickness(24, 11, 0, 10);

                if (NavColumn != null)
                {
                    NavColumn.MinWidth = 230;
                    NavColumn.Width = new GridLength(230);
                }

                Visibility microGraphVis = Visibility.Visible;
                if (CpuNavGraphContainer != null) CpuNavGraphContainer.Visibility = microGraphVis;
                if (GpuNavGraphContainer != null) GpuNavGraphContainer.Visibility = microGraphVis;
                if (RamNavGraphContainer != null) RamNavGraphContainer.Visibility = microGraphVis;
                if (NetworkNavGraphContainer != null) NetworkNavGraphContainer.Visibility = microGraphVis;
                if (DiskNavGraphContainer != null) DiskNavGraphContainer.Visibility = microGraphVis;
            }

            if (MemoryLimitCombo != null)
                MemoryLimitCombo.SelectedIndex = Properties.Settings.Default.LastProcessFilterIndex;

            if (AutoStartCheck != null)
                AutoStartCheck.IsChecked = Properties.Settings.Default.AutoStartExclusive;

            if (CompactViewCheck != null)
                CompactViewCheck.IsChecked = Properties.Settings.Default.compact;

            if (GroupProcessesCheck != null)
                GroupProcessesCheck.IsChecked = Properties.Settings.Default.group;

            if (mw != null)
            {
                double w = mw.Width > 0 ? mw.Width : SystemParameters.PrimaryScreenWidth;
                double h = mw.Height > 0 ? mw.Height : SystemParameters.PrimaryScreenHeight;
                UpdateResponsiveUI(w, h);
            }

            _saveBoundsTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            _saveBoundsTimer.Tick += SaveBoundsTimer_Tick;
            isLoaded = true;
        }

        private void SaveBoundsTimer_Tick(object sender, EventArgs e)
        {
            _saveBoundsTimer.Stop();
            if (_isExclusiveMode)
            {
                SaveExclusiveWindowBounds();
            }
        }

        public class ProcessItem : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;
            protected void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

            public string Identifier { get; set; } = "";
            public int Id { get; set; }
            public long RawMemory { get; set; }

            private string memoryUsage;
            public string MemoryUsage
            {
                get => memoryUsage;
                set { if (memoryUsage != value) { memoryUsage = value; OnPropertyChanged(nameof(MemoryUsage)); } }
            }
            public string RawName { get; set; }

            private double memoryPercentage;
            public double MemoryPercentage
            {
                get => memoryPercentage;
                set { if (memoryPercentage != value) { memoryPercentage = value; OnPropertyChanged(nameof(MemoryPercentage)); } }
            }

            private string name;
            public string Name
            {
                get => name;
                set { if (name != value) { name = value; OnPropertyChanged(nameof(Name)); } }
            }

            private ImageSource icon;
            public ImageSource Icon
            {
                get => icon;
                set { if (icon != value) { icon = value; OnPropertyChanged(nameof(Icon)); } }
            }

            public override string ToString() => $"{Name} ({MemoryUsage})";
        }

        private sealed class DeviceOption
        {
            public string DisplayName { get; set; } = "";
            public object Tag { get; set; }
            public override string ToString() => DisplayName;
        }

        private sealed class NetworkSample
        {
            public long BytesReceived { get; set; }
            public long BytesSent { get; set; }
            public DateTime Time { get; set; }
        }

        private sealed class GraphState
        {
            private readonly Canvas _canvas;
            private readonly Polyline _line;
            private readonly TextBlock _valueText;
            public double FixedMaximum { get; set; }
            private readonly Func<double, string> _formatter;
            private readonly Queue<double> _values = new Queue<double>();

            private readonly Line _tooltipLine;
            private readonly Border _tooltipBorder;
            private readonly TextBlock _tooltipText;
            private Point? _lastMousePos;

            public void SetHistory(Queue<double> history)
            {
                _values.Clear();
                if (history != null)
                {
                    foreach (var val in history) _values.Enqueue(val);
                }

                if (_values.Count > 0 && _valueText != null && _formatter != null)
                {
                    _valueText.Text = _formatter(_values.Last());
                }
                Render();
            }
            public GraphState(Canvas canvas, Polyline line, TextBlock valueText = null, double fixedMaximum = 0, Func<double, string> formatter = null)
            {
                _canvas = canvas;
                _line = line;
                _valueText = valueText;
                FixedMaximum = fixedMaximum;
                _formatter = formatter;

                _tooltipLine = new Line
                {
                    Stroke = new SolidColorBrush(Color.FromArgb(100, 255, 255, 255)),
                    StrokeThickness = 1,
                    Visibility = Visibility.Collapsed,
                    IsHitTestVisible = false
                };

                _tooltipText = new TextBlock
                {
                    Foreground = Brushes.White,
                    FontSize = 11,
                    FontFamily = new FontFamily("Segoe UI"),
                    Margin = new Thickness(6, 2, 6, 2)
                };

                _tooltipBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(200, 30, 30, 30)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Child = _tooltipText,
                    Visibility = Visibility.Collapsed,
                    IsHitTestVisible = false
                };

                Panel.SetZIndex(_tooltipLine, 10);
                Panel.SetZIndex(_tooltipBorder, 11);

                _canvas.Children.Add(_tooltipLine);
                _canvas.Children.Add(_tooltipBorder);

                _canvas.Background = Brushes.Transparent;
                _canvas.MouseEnter += Canvas_MouseEnter;
                _canvas.MouseLeave += Canvas_MouseLeave;
                _canvas.MouseMove += Canvas_MouseMove;
            }

            private void Canvas_MouseEnter(object sender, MouseEventArgs e)
            {
                if (_values.Count > 0 && _formatter != null)
                {
                    _tooltipLine.Visibility = Visibility.Visible;
                    _tooltipBorder.Visibility = Visibility.Visible;
                }
            }

            private void Canvas_MouseLeave(object sender, MouseEventArgs e)
            {
                _lastMousePos = null;
                _tooltipLine.Visibility = Visibility.Collapsed;
                _tooltipBorder.Visibility = Visibility.Collapsed;
            }

            private void Canvas_MouseMove(object sender, MouseEventArgs e)
            {
                _lastMousePos = e.GetPosition(_canvas);
                UpdateTooltip();
            }

            private void UpdateTooltip()
            {
                if (_lastMousePos == null || _values.Count == 0 || _formatter == null)
                {
                    _tooltipLine.Visibility = Visibility.Collapsed;
                    _tooltipBorder.Visibility = Visibility.Collapsed;
                    return;
                }

                double width = _canvas.ActualWidth;
                double height = _canvas.ActualHeight;
                if (width <= 2 || height <= 2) return;

                Point pos = _lastMousePos.Value;
                double step = width / Math.Max(1, ProcessMGR.GraphHistoryLimit - 1);

                int visualIndex = (int)Math.Round(pos.X / step);
                int offset = ProcessMGR.GraphHistoryLimit - _values.Count;
                int actualIndex = visualIndex - offset;

                if (actualIndex >= 0 && actualIndex < _values.Count)
                {
                    _tooltipLine.Visibility = Visibility.Visible;
                    _tooltipBorder.Visibility = Visibility.Visible;

                    double value = _values.ElementAt(actualIndex);
                    _tooltipText.Text = _formatter(value);

                    double xPos = visualIndex * step;

                    _tooltipLine.X1 = xPos;
                    _tooltipLine.X2 = xPos;
                    _tooltipLine.Y1 = 0;
                    _tooltipLine.Y2 = height;

                    _tooltipBorder.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    double borderX = xPos + 5;
                    if (borderX + _tooltipBorder.DesiredSize.Width > width)
                        borderX = xPos - _tooltipBorder.DesiredSize.Width - 5;

                    Canvas.SetLeft(_tooltipBorder, borderX);
                    Canvas.SetTop(_tooltipBorder, 5);
                }
                else
                {
                    _tooltipLine.Visibility = Visibility.Collapsed;
                    _tooltipBorder.Visibility = Visibility.Collapsed;
                }
            }

            public void Add(double value)
            {
                if (double.IsNaN(value) || double.IsInfinity(value))
                    value = 0;

                value = Math.Max(0, value);
                _values.Enqueue(value);

                while (_values.Count > ProcessMGR.GraphHistoryLimit)
                    _values.Dequeue();

                if (_valueText != null && _formatter != null)
                {
                    _valueText.Text = _formatter(value);
                }
                Render();
            }

            public void Reset()
            {
                _values.Clear();
                _line.Points = new PointCollection();
                if (_valueText != null && _formatter != null)
                {
                    _valueText.Text = _formatter(0);
                }
            }

            public void Render()
            {
                double width = _canvas.ActualWidth;
                double height = _canvas.ActualHeight;

                if (width <= 2 || height <= 2 || _values.Count == 0)
                    return;

                double maximum = FixedMaximum > 0
                    ? FixedMaximum
                    : Math.Max(1, _values.Max() * 1.18);

                double step = width / Math.Max(1, ProcessMGR.GraphHistoryLimit - 1);
                int offset = ProcessMGR.GraphHistoryLimit - _values.Count;
                int index = 0;
                PointCollection points = new PointCollection();

                foreach (double rawValue in _values)
                {
                    double value = Math.Min(rawValue, maximum);
                    double x = (offset + index) * step;
                    double y = height - ((value / maximum) * height);
                    points.Add(new Point(x, Math.Max(0, Math.Min(height, y))));
                    index++;
                }

                _line.Points = points;
                _line.InvalidateVisual();
                _canvas.InvalidateVisual();

                if (_lastMousePos != null)
                {
                    UpdateTooltip();
                }
            }
        }


        private void InitializePerformanceGraphs()
        {
            _cpuUsageGraph = CreateGraph(CpuUsageCanvas, CpuUsageLine, CpuUsageValue, 100, FormatPercentValue);
            _cpuClockGraph = CreateGraph(CpuClockCanvas, CpuClockLine, CpuClockValue, 0, FormatClockValue);
            _cpuTdpGraph = CreateGraph(CpuTdpCanvas, CpuTdpLine, CpuTdpValue, 0, FormatTdpValue);
            _cpuTempGraph = CreateGraph(CpuTempCanvas, CpuTempLine, CpuTempValue, 120, FormatTempValue);
            _gpuCoreLoadGraph = CreateGraph(GpuCoreLoadCanvas, GpuCoreLoadLine, GpuCoreLoadValue, 100, FormatPercentValue);
            _gpuMemoryLoadGraph = CreateGraph(GpuMemoryLoadCanvas, GpuMemoryLoadLine, GpuMemoryLoadValue, 100, FormatPercentValue);
            _gpuTdpGraph = CreateGraph(GpuTdpCanvas, GpuTdpLine, GpuTdpValue, 0, FormatTdpValue);
            _gpuTempGraph = CreateGraph(GpuTempCanvas, GpuTempLine, GpuTempValue, 120, FormatTempValue);
            _gpuEncoderLoadGraph = CreateGraph(GpuEncoderLoadCanvas, GpuEncoderLoadLine, GpuEncoderLoadValue, 100, FormatPercentValue);
            _networkDownloadGraph = CreateGraph(NetworkDownloadCanvas, NetworkDownloadLine, NetworkDownloadValue, 0, FormatNetworkValue);
            _networkUploadGraph = CreateGraph(NetworkUploadCanvas, NetworkUploadLine, NetworkUploadValue, 0, FormatNetworkValue);
            _diskUsageGraph = CreateGraph(DiskUsageCanvas, DiskUsageLine, DiskUsageValue, 100, FormatPercentValue);
            _diskReadGraph = CreateGraph(DiskReadCanvas, DiskReadLine, DiskReadValue, 0, FormatDiskSpeedValue);
            _diskWriteGraph = CreateGraph(DiskWriteCanvas, DiskWriteLine, DiskWriteValue, 0, FormatDiskSpeedValue);
            _diskTempGraph = CreateGraph(DiskTempCanvas, DiskTempLine, DiskTempValue, 120, FormatTempValue);
            _ramUsageGraph = CreateGraph(RamUsageCanvas, RamUsageLine, null, 0, FormatRamValue);

            _cpuNavGraph = CreateGraph(CpuNavGraphCanvas, CpuNavGraphLine, null, 100, null);
            _gpuNavGraph = CreateGraph(GpuNavGraphCanvas, GpuNavGraphLine, null, 100, null);
            _ramNavGraph = CreateGraph(RamNavGraphCanvas, RamNavGraphLine, null, 0, null);
            _networkNavGraph = CreateGraph(NetworkNavGraphCanvas, NetworkNavGraphLine, null, 0, null);
            _diskNavGraph = CreateGraph(DiskNavGraphCanvas, DiskNavGraphLine, null, 100, null);
        }

        private GraphState CreateGraph(Canvas canvas, Polyline line, TextBlock valueText, double fixedMaximum, Func<double, string> formatter)
        {
            GraphState graph = new GraphState(canvas, line, valueText, fixedMaximum, formatter);
            canvas.SizeChanged += (s, e) => graph.Render();
            return graph;
        }

        private string FormatPercentValue(double value) => $"{Math.Round(Math.Max(0, Math.Min(100, value)))}%";

        private string FormatTdpValue(double value) => $"{Math.Round(Math.Max(0, value))} W";
        private string FormatTempValue(double value) => $"{Math.Round(Math.Max(0, value))} °C";
        private string FormatRamValue(double value) => $"{Math.Round(Math.Max(0, value), 1)} GB";

        private string FormatDiskSpeedValue(double value)
        {
            if (value <= 0) return "0 KB/s";
            if (value >= 1048576) return $"{value / 1048576.0:0.0} MB/s";
            return $"{value / 1024.0:0.0} KB/s";
        }

        private string FormatClockValue(double value)
        {
            if (value <= 0)
                return "0 MHz";

            return value >= 1000
                ? $"{value / 1000.0:0.00} GHz"
                : $"{value:0} MHz";
        }

        private string FormatNetworkValue(double value)
        {
            if (value >= 1000)
                return $"{value / 1000.0:0.00} Gbit/s";

            return $"{value:0.0} Mbit/s";
        }

        private string GetPerformanceLabel()
        {
            try
            {
                var languageCode = Properties.Settings.Default.lang ?? "en";
                var perfor = MainWindow.Localization.LoadLocalization(languageCode, "perfor");
                string labelText = perfor["main"]["label"].ToString();

                if (!string.IsNullOrWhiteSpace(labelText))
                    return labelText;
            }
            catch { }
            return pmgrLang["main"]["monitoring"].ToString();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            ProcessListView.ItemsSource = _items;
            this.Focus();
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(2);
            _timer.Tick += Timer_Tick;

            if (mw != null)
            {
                mw.SearchBox.MinWidth = 260;
                mw.SizeChanged += MainWindow_SizeChanged;
                mw.LocationChanged += MainWindow_LocationChanged;
                mw.Closing += Mw_Closing;
            }

            RefreshProcessList();
            _timer.Start();

            string[] args = Environment.GetCommandLineArgs();
            bool launchedViaTaskmgr = args.Length > 1 && args.Any(arg => arg.IndexOf("taskmgr.exe", StringComparison.OrdinalIgnoreCase) >= 0);

            string lastMainTab = Properties.Settings.Default.LastMainTab;
            if (lastMainTab == "Performance")
            {
                SetPerformanceVisible(true);
            }
            else
            {
                SetPerformanceVisible(false);
            }

            if ((Properties.Settings.Default.AutoStartExclusive || launchedViaTaskmgr) && !MainWindow.HasAutoStartedExclusive)
            {
                MainWindow.HasAutoStartedExclusive = true;
                _isExclusiveMode = true;

                if (NavColumn != null)
                {
                    NavColumn.MinWidth = 230;
                    NavColumn.Width = new GridLength(230);
                }

                if (mw != null)
                {
                    mw.UpdateSettingsButtonForExclusive(true, true);

                    Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        mw.Activate();
                        this.Focus();
                        ProcessListView.Focus();
                    }, DispatcherPriority.ApplicationIdle);
                }
            }
            else
            {
                if (mw != null) UpdateResponsiveUI(mw.ActualWidth, mw.ActualHeight);
            }

            var window = Window.GetWindow(this);
            if (window != null)
            {
                _hwndSource = HwndSource.FromHwnd(new WindowInteropHelper(window).Handle);
                _hwndSource?.AddHook(WndProc);
            }
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_timer != null)
            {
                _timer.Stop();
                _timer.Tick -= Timer_Tick;
            }

            DisposePerformanceMonitoring();

            if (mw != null)
            {
                mw.SizeChanged -= MainWindow_SizeChanged;
                mw.LocationChanged -= MainWindow_LocationChanged;
                mw.Closing -= Mw_Closing;

                mw.SearchBox.ClearValue(FrameworkElement.MinWidthProperty);
                mw.SearchBox.BeginAnimation(UIElement.OpacityProperty, null);
                mw.SearchBox.Opacity = 1;
                mw.SearchBox.Visibility = Visibility.Visible;
            }

            if (mw != null && mw.Topmost)
            {
                mw.Topmost = false;
                if (TopmostIcon != null)
                {
                    TopmostIcon.Symbol = iNKORE.UI.WPF.Modern.Controls.Symbol.Pin;
                }
            }
            _hwndSource?.RemoveHook(WndProc);
        }

        private void AutoStart_Changed(object sender, RoutedEventArgs e)
        {
            if (isLoaded && AutoStartCheck != null)
            {
                Properties.Settings.Default.AutoStartExclusive = AutoStartCheck.IsChecked ?? false;
                Properties.Settings.Default.Save();
                if (Properties.Settings.Default.AutoStartExclusive && _isExclusiveMode)
                {
                    SaveExclusiveWindowBounds();
                }
                else
                {
                    ResetExclusiveWindowBounds();
                }
            }
        }

        private void SaveExclusiveWindowBounds()
        {
            if (mw != null && mw.WindowState == WindowState.Normal && Properties.Settings.Default.AutoStartExclusive)
            {
                Properties.Settings.Default.ExclusiveWindowWidth = Math.Max(580, mw.Width);
                Properties.Settings.Default.ExclusiveWindowHeight = Math.Max(380, mw.Height);
                Properties.Settings.Default.ExclusiveWindowLeft = mw.Left;
                Properties.Settings.Default.ExclusiveWindowTop = mw.Top;
                Properties.Settings.Default.Save();
            }
        }

        private void ResetExclusiveWindowBounds()
        {
            Properties.Settings.Default.ExclusiveWindowWidth = 1150;
            Properties.Settings.Default.ExclusiveWindowHeight = 710;
            Properties.Settings.Default.ExclusiveWindowLeft = -10000;
            Properties.Settings.Default.ExclusiveWindowTop = -10000;
            Properties.Settings.Default.Save();
        }

        private void Mw_Closing(object sender, CancelEventArgs e)
        {
            if (_isExclusiveMode)
            {
                SaveExclusiveWindowBounds();
            }
            DisposePerformanceMonitoring();
        }

        private void PerformanceBtn_Click(object sender, RoutedEventArgs e)
        {
            SetPerformanceVisible(!_performanceVisible);
        }

        private async void SetPerformanceVisible(bool visible)
        {
            _performanceVisible = visible;

            var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };
            var duration = TimeSpan.FromMilliseconds(300);

            Properties.Settings.Default.LastMainTab = visible ? "Performance" : "Processes";
            Properties.Settings.Default.Save();

            if (visible)
            {
                MainContent.Visibility = Visibility.Collapsed;
                PerformanceContent.Visibility = Visibility.Visible;
                PerformanceContent.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = TimeSpan.FromMilliseconds(200),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                });

                PerformanceBtnText.Text = pmgrLang["main"]["backtoprocess"].ToString();

                await EnsurePerformanceMonitoringAsync();

                if (!_isUpdatingPaused)
                {
                    RefreshPerformanceMetrics();
                    _performanceTimer?.Start();
                }
            }
            else
            {
                _performanceTimer?.Stop();
                PerformanceContent.BeginAnimation(UIElement.OpacityProperty, null);
                PerformanceContent.Opacity = 0;
                PerformanceContent.Visibility = Visibility.Collapsed;
                MainContent.Visibility = Visibility.Visible;
                MainContent.Opacity = 1;
                MainContent.IsHitTestVisible = true;

                string performanceLabel = GetPerformanceLabel();
                PerformanceBtnText.Text = pmgrLang["main"]["monitoring"].ToString();
            }
        }

        private async Task EnsurePerformanceMonitoringAsync()
        {
            if (_performanceMonitoringInitialized)
                return;

            if (CpuUsageValue != null) CpuUsageValue.Text = "";
            if (CpuClockValue != null) CpuClockValue.Text = "";
            if (CpuTdpValue != null) CpuTdpValue.Text = "";
            if (CpuTempValue != null) CpuTempValue.Text = "";
            if (CpuNavValue != null) CpuNavValue.Text = "-";

            if (GpuCoreLoadValue != null) GpuCoreLoadValue.Text = "";
            if (GpuMemoryLoadValue != null) GpuMemoryLoadValue.Text = "";
            if (GpuTdpValue != null) GpuTdpValue.Text = "";
            if (GpuTempValue != null) GpuTempValue.Text = "";
            if (GpuEncoderLoadValue != null) GpuEncoderLoadValue.Text = "";
            if (GpuNavValue != null) GpuNavValue.Text = "-";

            if (RamUsageValue != null) RamUsageValue.Text = "";
            if (RamPagefileValue != null) RamPagefileValue.Text = "";
            if (RamNavValue != null) RamNavValue.Text = "-";

            if (NetworkDownloadValue != null) NetworkDownloadValue.Text = "";
            if (NetworkUploadValue != null) NetworkUploadValue.Text = "";
            if (NetworkNavValue != null) NetworkNavValue.Text = "-";

            if (DiskNavValue != null) DiskNavValue.Text = "-";

            PerfLoadingRing.Visibility = Visibility.Visible;
            _performanceMonitoringInitialized = true;

            await Task.Run(() =>
            {
                try
                {
                    _hardwareComputer = new Computer
                    {
                        IsCpuEnabled = true,
                        IsGpuEnabled = true,
                        IsStorageEnabled = false 
                    };
                    _hardwareComputer.Open();
                    RefreshHardwareDevices();
                }
                catch (Exception ex) { Debug.WriteLine($"LHM init failed: {ex.Message}"); }

                try
                {
                    _cpuUsageCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total", true);
                    _cpuUsageCounter.NextValue();
                    _cpuFreqCounter = new PerformanceCounter("Processor Information", "Processor Frequency", "_Total", true);
                    _cpuPerfCounter = new PerformanceCounter("Processor Information", "% Processor Performance", "_Total", true);
                    _cpuFreqCounter.NextValue();
                    _cpuPerfCounter.NextValue();
                }
                catch
                {
                    _cpuUsageCounter = null;
                    _cpuFreqCounter = null;
                    _cpuPerfCounter = null;
                }
            });

            _cpuInitDone = true;

            int currentSpeedMs = Properties.Settings.Default.GraphUpdateSpeedMs;
            if (currentSpeedMs < 500) currentSpeedMs = 1000;

            _performanceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(currentSpeedMs) };
            _performanceTimer.Tick += PerformanceTimer_Tick;
            _performanceTimer.Start();
            UpdatePerformanceSection(_currentPerformanceSection);
            PerfLoadingRing.Visibility = Visibility.Collapsed;
            _ = LoadRemainingHardwareAsync();
        }

        private async Task LoadRemainingHardwareAsync()
        {
            await Task.Run(() => UpdateFallbackWmiGpuLoad());
            _gpuInitDone = true;
            await Task.Delay(150);
            await Task.Run(() => RefreshNetworkInterfaces());
            _netInitDone = true;
        }

        private void DisposePerformanceMonitoring()
        {
            if (_performanceTimer != null)
            {
                _performanceTimer.Stop();
                _performanceTimer.Tick -= PerformanceTimer_Tick;
                _performanceTimer = null;
            }

            try { _cpuUsageCounter?.Dispose(); } catch { }
            _cpuUsageCounter = null;
            try { _cpuFreqCounter?.Dispose(); } catch { }
            _cpuFreqCounter = null;
            try { _cpuPerfCounter?.Dispose(); } catch { }
            _cpuPerfCounter = null;
            try { _storageComputer?.Close(); } catch { }
            _storageComputer = null;

            foreach (PerformanceCounter counter in _diskUsageCounters.Values)
            {
                try { counter.Dispose(); } catch { }
            }
            _diskUsageCounters.Clear();

            try { _hardwareComputer?.Close(); } catch { }
            _hardwareComputer = null;
            _performanceMonitoringInitialized = false;
        }

        private void PerformanceTimer_Tick(object sender, EventArgs e) => RefreshPerformanceMetrics();

        private void PerformanceNavList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PerformanceNavList.SelectedItem is ListBoxItem selectedItem && selectedItem.Tag != null)
            {
                string tag = selectedItem.Tag.ToString();
                Properties.Settings.Default.LastPerformanceTab = tag;
                Properties.Settings.Default.Save();

                UpdatePerformanceSection(tag);
            }
        }

        private void UpdatePerformanceSection(string section)
        {
            _currentPerformanceSection = section;

            CpuPerfPanel.Visibility = section == "Cpu" ? Visibility.Visible : Visibility.Collapsed;
            RamPerfPanel.Visibility = section == "Ram" ? Visibility.Visible : Visibility.Collapsed;
            GpuPerfPanel.Visibility = section == "Gpu" ? Visibility.Visible : Visibility.Collapsed;
            DiskPerfPanel.Visibility = section == "Disk" ? Visibility.Visible : Visibility.Collapsed;
            NetworkPerfPanel.Visibility = section == "Network" ? Visibility.Visible : Visibility.Collapsed;

            switch (section)
            {
                case "Ram":
                    PerformanceTitle.Text = monitoringLang["main"]["RAM"].ToString();
                    break;

                case "Gpu":
                    PerformanceTitle.Text = monitoringLang["main"]["GPU"].ToString();
                    break;

                case "Disk":
                    PerformanceTitle.Text = monitoringLang["main"]["Disk"].ToString();
                    break;

                case "Network":
                    PerformanceTitle.Text = monitoringLang["main"]["Network"].ToString();
                    if (_realExtIp == monitoringLang["main"]["fetching"].ToString() || _realExtIp == "-")
                    {
                        Task.Run(async () =>
                        {
                            string ip = await IpResolver.GetExternalIpAsync();
                            Dispatcher.Invoke(() =>
                            {
                                _realExtIp = ip;
                                NetworkExternalIpValue.Text = _isNetworkPrivacyDisabled ? _realExtIp : "-";
                            });
                        });
                    }
                    break;

                case "Cpu":
                default:
                    PerformanceTitle.Text = monitoringLang["main"]["CPU"].ToString();
                    break;
            }

            RefreshPerformanceDeviceCombo();
        }

        private void GpuDeviceCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isChangingPerformanceCombo) return;

            if (GpuDeviceCombo.SelectedItem is DeviceOption option)
            {
                _selectedGpuHardware = option.Tag as IHardware;
                ResetCurrentPerformanceGraphs();
                RefreshPerformanceMetrics();
            }
        }

        private void NetworkDeviceCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isChangingPerformanceCombo) return;

            if (NetworkDeviceCombo.SelectedItem is DeviceOption option)
            {
                _selectedNetworkId = option.Tag as string ?? AllNetworkAdaptersTag;
                _networkSamples.Clear();
                ResetCurrentPerformanceGraphs();
                RefreshPerformanceMetrics();
            }
        }

        private void DiskDeviceCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isChangingPerformanceCombo) return;

            if (DiskDeviceCombo.SelectedItem is DeviceOption option)
            {
                _selectedDiskInstance = option.Tag as string ?? TotalDiskInstance;

                if (_diskHistoryCache.TryGetValue(_selectedDiskInstance, out var hist))
                {
                    _diskUsageGraph.SetHistory(hist.Usage);
                    _diskReadGraph.SetHistory(hist.Read);
                    _diskWriteGraph.SetHistory(hist.Write);
                    _diskTempGraph.SetHistory(hist.Temp);
                }
                else
                {
                    ResetCurrentPerformanceGraphs();
                }

                RefreshPerformanceMetrics();
            }
        }

        private async void RefreshPerformanceDeviceCombo()
        {
            GpuDeviceCombo.Visibility = Visibility.Collapsed;
            NetworkDeviceCombo.Visibility = Visibility.Collapsed;
            DiskDeviceCombo.Visibility = Visibility.Collapsed;
            SingleDeviceName.Visibility = Visibility.Collapsed;

            if (_currentPerformanceSection == "Cpu" || _currentPerformanceSection == "Ram")
            {
                SingleDeviceName.Visibility = _currentPerformanceSection == "Cpu" ? Visibility.Visible : Visibility.Collapsed;
                return;
            }

            _isChangingPerformanceCombo = true;

            try
            {
                List<DeviceOption> options = new List<DeviceOption>();
                int selectedIndex = 0;
                ComboBox targetCombo = null;

                if (_currentPerformanceSection == "Gpu")
                {
                    targetCombo = GpuDeviceCombo;
                    RefreshHardwareDevices();

                    if (_gpuHardwares.Count == 0)
                    {
                        options.Add(new DeviceOption { DisplayName = "", Tag = null });
                    }
                    else
                    {
                        for (int i = 0; i < _gpuHardwares.Count; i++)
                        {
                            IHardware gpu = _gpuHardwares[i];
                            options.Add(new DeviceOption { DisplayName = gpu.Name, Tag = gpu });
                            if (ReferenceEquals(gpu, _selectedGpuHardware))
                                selectedIndex = i;
                        }
                        if (_selectedGpuHardware == null)
                            _selectedGpuHardware = _gpuHardwares[0];
                    }
                }
                else if (_currentPerformanceSection == "Network")
                {
                    targetCombo = NetworkDeviceCombo;
                    RefreshNetworkInterfaces();

                    if (_networkInterfaces.Count > 1)
                        options.Add(new DeviceOption
                        {
                            DisplayName = monitoringLang["main"]["allnetwork"].ToString(),
                            Tag = AllNetworkAdaptersTag
                        });

                    for (int i = 0; i < _networkInterfaces.Count; i++)
                    {
                        NetworkInterface adapter = _networkInterfaces[i];
                        options.Add(new DeviceOption { DisplayName = adapter.Name, Tag = adapter.Id });
                        if (adapter.Id == _selectedNetworkId)
                            selectedIndex = options.Count - 1;
                    }
                }
                else if (_currentPerformanceSection == "Disk")
                {
                    targetCombo = DiskDeviceCombo;
                    if (_isLoadingDiskInfo) return;
                    _isLoadingDiskInfo = true;

                    PerfLoadingRing.Visibility = Visibility.Visible;
                    targetCombo.IsEnabled = false;
                    SingleDeviceName.Visibility = Visibility.Collapsed;
                    targetCombo.Visibility = Visibility.Visible;

                    DiskCapacityValue.Text = "-";
                    DiskSystemValue.Text = "-";
                    DiskUsageValue.Text = "";
                    DiskReadValue.Text = "";
                    DiskWriteValue.Text = "";
                    _diskTempGraph.Reset();
                    if (DiskFreeSpaceValue != null) DiskFreeSpaceValue.Text = "-";

                    List<DeviceOption> diskOptions = await Task.Run(() =>
                    {
                        if (!_diskInitDone)
                        {
                            try
                            {
                                _storageComputer = new Computer { IsStorageEnabled = true };
                                _storageComputer.Open();

                                var disks = _storageComputer.Hardware
                                    .Where(h => h.HardwareType.ToString() == "Storage" || h.HardwareType.ToString() == "HDD" || h.HardwareType.ToString() == "SSD")
                                    .ToList();

                                lock (_storageHardwares)
                                {
                                    _storageHardwares = disks;
                                }
                            }
                            catch (Exception ex) { Debug.WriteLine(ex); }
                            _diskInitDone = true;
                        }
                        return GetDiskOptions();
                    });

                    if (_currentPerformanceSection != "Disk")
                    {
                        _isLoadingDiskInfo = false;
                        PerfLoadingRing.Visibility = Visibility.Collapsed;
                        return;
                    }

                    options.AddRange(diskOptions);

                    if (string.IsNullOrWhiteSpace(_selectedDiskInstance))
                    {
                        _selectedDiskInstance = TotalDiskInstance;
                    }

                    for (int i = 0; i < options.Count; i++)
                    {
                        if ((options[i].Tag as string) == _selectedDiskInstance)
                            selectedIndex = i;
                    }

                    targetCombo.IsEnabled = true;
                    _isLoadingDiskInfo = false;
                    PerfLoadingRing.Visibility = Visibility.Collapsed;
                    RefreshPerformanceMetrics();
                }

                if (targetCombo != null)
                {
                    targetCombo.ItemsSource = options;
                    targetCombo.SelectedIndex = options.Count > 0 ? Math.Min(selectedIndex, options.Count - 1) : -1;

                    if (options.Count <= 1)
                    {
                        targetCombo.Visibility = Visibility.Collapsed;
                        SingleDeviceName.Visibility = Visibility.Visible;
                        SingleDeviceName.Text = options.Count > 0 ? options[selectedIndex].DisplayName : "-";
                    }
                    else
                    {
                        targetCombo.Visibility = Visibility.Visible;
                        SingleDeviceName.Visibility = Visibility.Collapsed;
                    }
                }
            }
            finally
            {
                _isChangingPerformanceCombo = false;
                if (mw != null)
                {
                    UpdateResponsiveUI(mw.ActualWidth, mw.ActualHeight);
                }
            }
        }

        private void ResetCurrentPerformanceGraphs()
        {
            if (_currentPerformanceSection == "Cpu")
            {
                _cpuUsageGraph.Reset();
                _cpuClockGraph.Reset();
                _cpuTdpGraph.Reset();
                _cpuTempGraph.Reset();
                if (_cpuCoreGraphs != null)
                {
                    foreach (var g in _cpuCoreGraphs) g.Reset();
                }
            }
            else if (_currentPerformanceSection == "Gpu")
            {
                _gpuCoreLoadGraph.Reset();
                _gpuMemoryLoadGraph.Reset();
                _gpuTdpGraph.Reset();
                _gpuTempGraph.Reset();
                _gpuEncoderLoadGraph.Reset();
            }
            else if (_currentPerformanceSection == "Network")
            {
                _networkDownloadGraph.Reset();
                _networkUploadGraph.Reset();
            }
            else if (_currentPerformanceSection == "Disk")
            {
                _diskUsageGraph.Reset();
                _diskReadGraph.Reset();
                _diskWriteGraph.Reset();
                _diskTempGraph.Reset();
            }
            else if (_currentPerformanceSection == "Ram")
            {
                _ramUsageGraph.Reset();
            }
        }

        private bool _isUpdatingPerformance = false;
        private async void RefreshPerformanceMetrics()
        {
            if (_isUpdatingPaused || _isUpdatingPerformance) return;
            _isUpdatingPerformance = true;

            try
            {
                await Task.Run(() => UpdateHardwareSensors());
                UpdateCpuStaticInfo();

                double cpuUsage = ReadCpuUsage();
                double cpuClock = ReadCpuClock();

                IHardware cpu = _cpuHardwares.FirstOrDefault();
                double cpuTdp = 0;
                double cpuTemp = 0;
                if (cpu != null)
                {
                    if (_currentPerformanceSection == "Cpu")
                        SingleDeviceName.Text = cpu.Name;

                    cpuTdp = FindSensorValue(cpu, SensorType.Power, new[] { "Package", "CPU Package", "CPU Cores", "Core", "Total" }) ?? 0;
                    cpuTemp = FindSensorValue(cpu, SensorType.Temperature, new[] { "Core (Tctl/Tdie)", "Package", "Core Max", "CPU Package", "Core Average", "CPU" }) ?? 0;
                }
                else
                {
                    if (_currentPerformanceSection == "Cpu")
                        SingleDeviceName.Text = "";
                }

                _cpuUsageGraph.Add(cpuUsage);
                _cpuClockGraph.Add(cpuClock);
                _cpuTdpGraph.Add(cpuTdp);
                _cpuTempGraph.Add(cpuTemp);

                int currentCpuVisible = 0;
                if (cpuTemp > 0) { CpuTempBorder.Visibility = Visibility.Visible; currentCpuVisible++; } else { CpuTempBorder.Visibility = Visibility.Collapsed; }
                if (cpuTdp > 0) { CpuTdpBorder.Visibility = Visibility.Visible; currentCpuVisible++; } else { CpuTdpBorder.Visibility = Visibility.Collapsed; }

                CpuTempTdpGrid.Visibility = currentCpuVisible > 0 ? Visibility.Visible : Visibility.Collapsed;

                bool layoutNeedsUpdate = false;
                if (_cpuVisibleCards != currentCpuVisible)
                {
                    _cpuVisibleCards = currentCpuVisible;
                    layoutNeedsUpdate = true;
                }

                CpuNavValue.Text = $"{FormatPercentValue(cpuUsage)} • {FormatClockValue(cpuClock)}";
                if (_cpuNavGraph != null) _cpuNavGraph.Add(cpuUsage);

                if (_cpuViewMode != CpuViewMode.Overall && _cpuCoreUsageCounters != null && _cpuCoreGraphs != null)
                {
                    if (_cpuViewMode == CpuViewMode.Logical)
                    {
                        for (int i = 0; i < _cpuCoreUsageCounters.Count && i < _cpuCoreGraphs.Count; i++)
                        {
                            try { _cpuCoreGraphs[i].Add(_cpuCoreUsageCounters[i].NextValue()); } catch { }
                        }
                    }
                    else if (_cpuViewMode == CpuViewMode.Physical)
                    {
                        int threadsPerCore = _cpuCoreUsageCounters.Count / _cpuPhysicalCoreCount;
                        if (threadsPerCore < 1) threadsPerCore = 1;

                        for (int i = 0; i < _cpuPhysicalCoreCount && i < _cpuCoreGraphs.Count; i++)
                        {
                            double sum = 0;
                            int valid = 0;
                            for (int j = 0; j < threadsPerCore; j++)
                            {
                                int idx = i * threadsPerCore + j;
                                if (idx < _cpuCoreUsageCounters.Count)
                                {
                                    try { sum += _cpuCoreUsageCounters[idx].NextValue(); valid++; } catch { }
                                }
                            }
                            _cpuCoreGraphs[i].Add(valid > 0 ? sum / valid : 0);
                        }
                    }
                }

                IHardware gpu = _selectedGpuHardware ?? _gpuHardwares.FirstOrDefault();
                double gpuCoreLoad = 0;
                double gpuMemoryLoad = 0;
                double gpuTdp = 0;
                double gpuTemp = 0;
                double gpuEncoderLoad = 0;
                double gpuMemoryGraphValue = 0;

                string vramString = "0 GB (0%)";
                string vramFullString = "0 GB / 0 GB (0%)";

                if (gpu != null)
                {
                    gpuCoreLoad = Clamp(FindSensorValue(gpu, SensorType.Load,
                        new[] { "GPU Core", "D3D 3D", "3D", "Core", "Graphics" },
                        new[] { "Memory", "Video", "Bus", "Engine" }) ?? 0, 0, 100);

                    gpuEncoderLoad = Clamp(FindSensorValue(gpu, SensorType.Load,
                        new[] { "Video Engine", "Video", "Encoder", "Encode", "Codec", "Media Engine" },
                        new[] { "Memory", "Bus", "Core", "3D" }) ?? 0, 0, 100);

                    if (gpuCoreLoad == 0 && gpuEncoderLoad == 0)
                    {
                        UpdateFallbackWmiGpuLoad();
                        gpuCoreLoad = _fallbackGpuCoreLoad;
                        gpuEncoderLoad = _fallbackGpuEncoderLoad;
                    }

                    gpuMemoryLoad = Clamp(FindSensorValue(gpu, SensorType.Load,
                        new[] { "GPU Memory", "Memory" }) ?? ReadGpuMemoryLoadFromSmallData(gpu) ?? 0, 0, 100);

                    gpuTdp = FindSensorValue(gpu, SensorType.Power, new[] { "GPU Power", "Total" }) ?? 0;
                    gpuTemp = FindSensorValue(gpu, SensorType.Temperature, new[] { "GPU Core", "Core" }) ?? 0;

                    double? usedVramMB = FindSensorValue(gpu, SensorType.SmallData,
                        new[] { "GPU Memory Used", "Memory Used", "D3D Dedicated Memory Used", "Dedicated Memory Used" });

                    double? totalVramMB = FindSensorValue(gpu, SensorType.SmallData,
                        new[] { "GPU Memory Total", "Memory Total", "D3D Dedicated Memory Total", "Dedicated Memory Total" });

                    if (usedVramMB.HasValue && totalVramMB.HasValue && totalVramMB.Value > 0)
                    {
                        double usedVramGB = usedVramMB.Value / 1024.0;
                        double totalVramGB = totalVramMB.Value / 1024.0;
                        double memPercent = (usedVramMB.Value / totalVramMB.Value) * 100.0;
                        vramString = $"{usedVramGB:0.##} GB";
                        vramFullString = $"{usedVramGB:0.##} GB / {totalVramGB:0.##} GB ({Math.Round(memPercent)}%)";

                        _gpuMemoryLoadGraph.FixedMaximum = totalVramGB;
                        gpuMemoryGraphValue = usedVramGB;
                    }
                    else
                    {
                        vramString = gpuMemoryLoad > 0 ? $"{FormatPercentValue(gpuMemoryLoad)}" : _fallbackGpuVramString;
                        vramFullString = vramString;

                        _gpuMemoryLoadGraph.FixedMaximum = 100;
                        gpuMemoryGraphValue = gpuMemoryLoad;
                    }

                    bool isIgpu = gpu != null && (gpu.Name.IndexOf("Intel", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                  gpu.Name.IndexOf("Radeon Graphics", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                  gpu.Name.IndexOf("UHD", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                  gpu.Name.IndexOf("Iris", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                  gpu.Name.IndexOf("Vega", StringComparison.OrdinalIgnoreCase) >= 0);

                    if (gpuTdp == 0 && cpu != null && isIgpu)
                    {
                        gpuTdp = FindSensorValue(cpu, SensorType.Power, new[] { "Graphics", "GT", "GPU" }) ?? 0;
                    }

                    if (gpuTemp == 0 && cpu != null && isIgpu)
                    {
                        gpuTemp = cpuTemp;
                    }
                }

                _gpuCoreLoadGraph.Add(gpuCoreLoad);
                _gpuMemoryLoadGraph.Add(gpuMemoryGraphValue);
                _gpuTdpGraph.Add(gpuTdp);
                _gpuTempGraph.Add(gpuTemp);
                _gpuEncoderLoadGraph.Add(gpuEncoderLoad);

                int currentGpuVisible = 0;
                if (gpuTemp > 0) { GpuTempBorder.Visibility = Visibility.Visible; currentGpuVisible++; } else { GpuTempBorder.Visibility = Visibility.Collapsed; }
                if (gpuTdp > 0) { GpuTdpBorder.Visibility = Visibility.Visible; currentGpuVisible++; } else { GpuTdpBorder.Visibility = Visibility.Collapsed; }

                GpuTempTdpGrid.Visibility = currentGpuVisible > 0 ? Visibility.Visible : Visibility.Collapsed;

                if (_gpuVisibleCards != currentGpuVisible)
                {
                    _gpuVisibleCards = currentGpuVisible;
                    layoutNeedsUpdate = true;
                }
                if (layoutNeedsUpdate && mw != null)
                {
                    UpdateResponsiveUI(mw.Width, mw.Height);
                }

                if (_gpuNavGraph != null) _gpuNavGraph.Add(gpuCoreLoad);
                GpuMemoryLoadValue.Text = _isNarrowView ? vramString : vramFullString;
                GpuNavValue.Text = gpu != null ? $"{FormatPercentValue(gpuCoreLoad)} • {vramString}" : "";

                var (networkDownload, networkUpload) = ReadNetworkUsageMbps();
                double totalNetwork = networkDownload + networkUpload;

                _networkDownloadGraph.Add(networkDownload);
                _networkUploadGraph.Add(networkUpload);

                NetworkDownloadValue.Text = FormatNetworkValue(networkDownload);
                NetworkUploadValue.Text = FormatNetworkValue(networkUpload);
                NetworkNavValue.Text = FormatNetworkValue(totalNetwork);
                if (_networkNavGraph != null) _networkNavGraph.Add(totalNetwork);

                NetworkInterface adapter = _selectedNetworkId == AllNetworkAdaptersTag
                ? _networkInterfaces.FirstOrDefault(a => a.GetIPProperties().UnicastAddresses.Any(ip => ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork))
                : _networkInterfaces.FirstOrDefault(a => a.Id == _selectedNetworkId);

                if (adapter != null)
                {
                    _realIpv4 = adapter.GetIPProperties().UnicastAddresses.FirstOrDefault(ip => ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)?.Address.ToString() ?? "-";
                    string mac = adapter.GetPhysicalAddress().ToString();
                    _realMac = string.IsNullOrEmpty(mac) ? "-" : string.Join(":", Enumerable.Range(0, mac.Length / 2).Select(i => mac.Substring(i * 2, 2)));

                    NetworkIpValue.Text = _isNetworkPrivacyDisabled ? _realIpv4 : "-";
                    NetworkMacValue.Text = _isNetworkPrivacyDisabled ? _realMac : "-";
                }
                else
                {
                    NetworkIpValue.Text = "-";
                    NetworkMacValue.Text = "-";
                }

                string selectedInstance = string.IsNullOrWhiteSpace(_selectedDiskInstance) ? TotalDiskInstance : _selectedDiskInstance;
                double diskUsage = 0, diskRead = 0, diskWrite = 0, diskTemp = 0;
                bool hasHealth = false, hasTotalReads = false, hasTotalWrites = false;
                string healthStr = "", totalReadsStr = "", totalWritesStr = "";

                if (_currentPerformanceSection == "Disk" && DiskDeviceCombo.Items.Count > 0)
                {
                    var diskOptionsToPoll = DiskDeviceCombo.Items.Cast<DeviceOption>().Select(o => o.Tag as string ?? TotalDiskInstance).ToList();

                    foreach (var inst in diskOptionsToPoll)
                    {
                        if (!_diskHistoryCache.ContainsKey(inst))
                            _diskHistoryCache[inst] = new DiskHistory();

                        var hist = _diskHistoryCache[inst];

                        double curUsage = ReadDiskUsage(inst);
                        double curRead = ReadDiskCounter("Disk Read Bytes/sec", _diskReadCounters, inst);
                        double curWrite = ReadDiskCounter("Disk Write Bytes/sec", _diskWriteCounters, inst);
                        double curTemp = 0;

                        if (inst != TotalDiskInstance)
                        {
                            string[] parts = inst.Split(' ');
                            if (parts.Length > 0 && int.TryParse(parts[0], out int diskNum))
                            {
                                if (_diskInfoCache != null && _diskInfoCache.TryGetValue(diskNum.ToString(), out DiskInfo info))
                                {
                                    var storageHw = _storageHardwares.FirstOrDefault(h =>
                                        h.Name.IndexOf(info.Model, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                        info.Model.IndexOf(h.Name, StringComparison.OrdinalIgnoreCase) >= 0);

                                    if (storageHw != null)
                                    {
                                        curTemp = FindSensorValue(storageHw, SensorType.Temperature, new[] { "Temperature" }) ?? 0;

                                        if (inst == selectedInstance)
                                        {
                                            //double? remainingLife = FindSensorValue(storageHw, SensorType.Level, new[] { "Remaining Life", "Health" });
                                            //double? wearLevel = FindSensorValue(storageHw, SensorType.Level, new[] { "Wear Level" });
                                            //double? health = remainingLife ?? (wearLevel.HasValue ? (100.0 - wearLevel.Value) : (double?)null);
                                            //if (health.HasValue) { healthStr = $"{Math.Round(health.Value)}%"; hasHealth = true; }

                                            double? totalRead = FindSensorValue(storageHw, SensorType.Data, new[] { "Data Read" });
                                            double? totalWrite = FindSensorValue(storageHw, SensorType.Data, new[] { "Data Written" });


                                            if (totalRead.HasValue)
                                            {
                                                totalReadsStr = totalRead.Value >= 1000 ? $"{totalRead.Value / 1024.0:0.0} TB" : $"{totalRead.Value:0.0} GB";
                                                hasTotalReads = true;
                                            }

                                            if (totalWrite.HasValue)
                                            {
                                                totalWritesStr = totalWrite.Value >= 1000 ? $"{totalWrite.Value / 1024.0:0.0} TB" : $"{totalWrite.Value:0.0} GB";
                                                hasTotalWrites = true;
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        SafeEnqueue(hist.Usage, curUsage);
                        SafeEnqueue(hist.Read, curRead);
                        SafeEnqueue(hist.Write, curWrite);
                        SafeEnqueue(hist.Temp, curTemp);

                        if (inst == selectedInstance)
                        {
                            diskUsage = curUsage;
                            diskRead = curRead;
                            diskWrite = curWrite;
                            diskTemp = curTemp;
                        }
                    }
                }

                _diskUsageGraph.Add(diskUsage);
                _diskReadGraph.Add(diskRead);
                _diskWriteGraph.Add(diskWrite);
                _diskTempGraph.Add(diskTemp);
                _hasDiskTempData = diskTemp > 0;

                if (Chart_DiskTemp != null)
                {
                    Chart_DiskTemp.Visibility = _hasDiskTempData && !_lastIsHeightTooSmall.GetValueOrDefault() ? Visibility.Visible : Visibility.Collapsed;
                }

                if (DiskHealthPanel != null)
                {
                    DiskHealthPanel.Visibility = hasHealth ? Visibility.Visible : Visibility.Collapsed;
                    if (hasHealth) DiskHealthValue.Text = healthStr;
                }
                if (DiskTotalReadsPanel != null)
                {
                    DiskTotalReadsPanel.Visibility = hasTotalReads ? Visibility.Visible : Visibility.Collapsed;
                    if (hasTotalReads) DiskTotalReadsValue.Text = totalReadsStr;
                }
                if (DiskTotalWritesPanel != null)
                {
                    DiskTotalWritesPanel.Visibility = hasTotalWrites ? Visibility.Visible : Visibility.Collapsed;
                    if (hasTotalWrites) DiskTotalWritesValue.Text = totalWritesStr;
                }

                if (DiskSMARTGrid != null)
                {
                    DiskSMARTGrid.Visibility = (hasHealth || hasTotalReads || hasTotalWrites) ? Visibility.Visible : Visibility.Collapsed;
                }

                if (_diskNavGraph != null) _diskNavGraph.Add(diskUsage);
                DiskNavValue.Text = FormatPercentValue(diskUsage);

                if (!_isLoadingDiskInfo)
                {
                    DiskUsageValue.Text = FormatPercentValue(diskUsage);
                    DiskReadValue.Text = FormatDiskSpeedValue(diskRead);
                    DiskWriteValue.Text = FormatDiskSpeedValue(diskWrite);

                    string sysDrive = Environment.SystemDirectory.Substring(0, 2);
                    bool isSystem = selectedInstance.Contains(sysDrive) && selectedInstance != TotalDiskInstance;

                    string diskTypeStr = "-";
                    string capacityStr = "-";
                    string freeSpaceStr = "-";

                    string FormatDiskSpace(double gb) => gb >= 1000 ? $"{gb / 1024.0:0.##} TB" : $"{Math.Round(gb)} GB";

                    if (selectedInstance != TotalDiskInstance)
                    {
                        string[] parts = selectedInstance.Split(' ');
                        if (parts.Length > 0 && int.TryParse(parts[0], out int diskNum))
                        {
                            if (_diskInfoCache != null && _diskInfoCache.TryGetValue(diskNum.ToString(), out DiskInfo info))
                            {
                                diskTypeStr = info.Type;
                                capacityStr = FormatDiskSpace(info.SizeGB);
                                freeSpaceStr = FormatDiskSpace(info.FreeSpaceGB);
                            }
                        }
                    }
                    else
                    {
                        diskTypeStr = monitoringLang["main"]["alldrives"].ToString();
                        if (_diskInfoCache != null)
                        {
                            double totalCap = _diskInfoCache.Values.Sum(i => i.SizeGB);
                            double totalFree = _diskInfoCache.Values.Sum(i => i.FreeSpaceGB);
                            capacityStr = FormatDiskSpace(totalCap);
                            freeSpaceStr = FormatDiskSpace(totalFree);
                        }
                    }

                    DiskCapacityValue.Text = capacityStr;
                    DiskSystemValue.Text = isSystem ? $"{diskTypeStr}" : diskTypeStr;
                    if (DiskFreeSpaceValue != null) DiskFreeSpaceValue.Text = freeSpaceStr;

                    if (DiskHealthValue != null) DiskHealthValue.Text = healthStr;
                    if (DiskTotalReadsValue != null) DiskTotalReadsValue.Text = totalReadsStr;
                    if (DiskTotalWritesValue != null) DiskTotalWritesValue.Text = totalWritesStr;
                }

                MEMORYSTATUSEX memStatus = new MEMORYSTATUSEX();
                memStatus.dwLength = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(MEMORYSTATUSEX));
                double ramUsageGB = 0;
                double totalRamGB = 0;
                if (GlobalMemoryStatusEx(memStatus))
                {
                    totalRamGB = memStatus.ullTotalPhys / 1024.0 / 1024.0 / 1024.0;
                    ramUsageGB = (memStatus.ullTotalPhys - memStatus.ullAvailPhys) / 1024.0 / 1024.0 / 1024.0;
                    double ramPercent = totalRamGB > 0 ? (ramUsageGB / totalRamGB * 100) : 0;

                    RamUsageValue.Text = _isNarrowView
                            ? $"{ramUsageGB:0.##} GB"
                            : $"{ramUsageGB:0.##} GB / {totalRamGB:0.##} GB ({Math.Round(ramPercent)}%)";

                    RamNavValue.Text = $"{Math.Round(ramPercent)}% • {ramUsageGB:0.##} GB";

                    if (_ramNavGraph != null)
                    {
                        _ramNavGraph.FixedMaximum = totalRamGB;
                        _ramNavGraph.Add(ramUsageGB);
                    }

                    double pageTotalGB = memStatus.ullTotalPageFile / 1024.0 / 1024.0 / 1024.0;
                    double pageAvailGB = memStatus.ullAvailPageFile / 1024.0 / 1024.0 / 1024.0;
                    double pageUsageGB = pageTotalGB - pageAvailGB;
                    double pagePercent = pageTotalGB > 0 ? (pageUsageGB / pageTotalGB * 100) : 0;
                    RamPagefileValue.Text = $"{pageUsageGB:0.##} GB / {pageTotalGB:0.##} GB ({Math.Round(pagePercent)}%)";
                }
                else
                {
                    RamUsageValue.Text = _isNarrowView
                            ? $"{ramUsageGB:0.##} GB"
                            : $"{ramUsageGB:0.##} GB / {totalRamGB:0.##} GB";

                    RamNavValue.Text = FormatRamValue(ramUsageGB);
                    RamPagefileValue.Text = "-";
                }

                if (totalRamGB > 0)
                {
                    _ramUsageGraph.FixedMaximum = totalRamGB;
                }
                _ramUsageGraph.Add(ramUsageGB);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Performance refresh failed: {ex.Message}");
            }
            finally
            {
                _isUpdatingPerformance = false;
            }
        }

        private void RefreshHardwareDevices()
        {
            if (_hardwareComputer == null) return;
            try
            {
                foreach (IHardware hardware in _hardwareComputer.Hardware)
                    UpdateHardwareRecursive(hardware);

                _cpuHardwares = _hardwareComputer.Hardware.Where(h => h.HardwareType == HardwareType.Cpu).ToList();
                _gpuHardwares = _hardwareComputer.Hardware.Where(IsGpuHardware).ToList();
            }
            catch (Exception ex) { Debug.WriteLine($"Hardware refresh failed: {ex.Message}"); }
        }

        private void UpdateHardwareSensors()
        {
            if (_hardwareComputer != null)
            {
                try
                {
                    foreach (IHardware hardware in _hardwareComputer.Hardware)
                        UpdateHardwareRecursive(hardware);
                }
                catch { }
            }

            if (_storageComputer != null && _diskInitDone)
            {
                try
                {
                    foreach (IHardware hardware in _storageComputer.Hardware)
                        UpdateHardwareRecursive(hardware);
                }
                catch { }
            }
        }

        private void UpdateHardwareRecursive(IHardware hardware)
        {
            hardware.Update();

            foreach (IHardware subHardware in hardware.SubHardware)
                UpdateHardwareRecursive(subHardware);
        }

        private bool IsGpuHardware(IHardware hardware)
        {
            return hardware.HardwareType.ToString().StartsWith("Gpu", StringComparison.OrdinalIgnoreCase);
        }

        private IEnumerable<ISensor> GetSensorsRecursive(IHardware hardware)
        {
            foreach (ISensor sensor in hardware.Sensors)
                yield return sensor;

            foreach (IHardware subHardware in hardware.SubHardware)
            {
                foreach (ISensor sensor in GetSensorsRecursive(subHardware))
                    yield return sensor;
            }
        }

        private double? FindSensorValue(IHardware hardware, SensorType sensorType, string[] preferredTokens, string[] excludedTokens = null)
        {
            if (hardware == null)
                return null;

            List<ISensor> sensors = GetSensorsRecursive(hardware)
                .Where(sensor => sensor.SensorType == sensorType && sensor.Value.HasValue)
                .Where(sensor => excludedTokens == null || !ContainsAny(sensor.Name, excludedTokens))
                .ToList();

            foreach (string token in preferredTokens)
            {
                ISensor sensor = sensors.FirstOrDefault(s => s.Name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0);
                if (sensor != null)
                    return sensor.Value.GetValueOrDefault();
            }

            return sensors.FirstOrDefault()?.Value;
        }

        private bool ContainsAny(string value, IEnumerable<string> tokens)
        {
            return tokens.Any(token => value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private double ReadCpuUsage()
        {
            try
            {
                if (_cpuUsageCounter != null)
                    return Clamp(_cpuUsageCounter.NextValue(), 0, 100);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CPU counter failed: {ex.Message}");
            }

            foreach (IHardware cpu in _cpuHardwares)
            {
                double? sensorValue = FindSensorValue(cpu, SensorType.Load, new[] { "CPU Total", "Total" });
                if (sensorValue.HasValue)
                    return Clamp(sensorValue.Value, 0, 100);
            }

            return ReadWmiCpuUsage();
        }

        private double ReadCpuClock()
        {
            List<double> clocks = _cpuHardwares
                .SelectMany(GetSensorsRecursive)
                .Where(sensor => sensor.SensorType == SensorType.Clock && sensor.Value.HasValue)
                .Where(sensor => !ContainsAny(sensor.Name, new[] { "Bus", "Memory", "Uncore", "Ring" }))
                .Select(sensor => (double)sensor.Value.GetValueOrDefault())
                .ToList();

            if (clocks.Count > 0)
                return clocks.Average();

            try
            {
                if (_cpuFreqCounter != null && _cpuPerfCounter != null)
                {
                    double baseFreq = _cpuFreqCounter.NextValue();
                    double perfPercent = _cpuPerfCounter.NextValue();
                    if (baseFreq > 0 && perfPercent > 0)
                    {
                        _lastValidCpuClock = baseFreq * (perfPercent / 100.0);
                        return _lastValidCpuClock;
                    }
                    else if (_lastValidCpuClock > 0)
                    {
                        return _lastValidCpuClock;
                    }
                }
            }
            catch { }

            return ReadWmiCpuClock();
        }

        private void UpdateCpuStaticInfo()
        {
            if ((DateTime.UtcNow - _lastCpuStaticInfoUpdate).TotalSeconds < 2)
                return;

            _lastCpuStaticInfoUpdate = DateTime.UtcNow;

            try
            {
                long tickCountMs = Environment.TickCount64;
                TimeSpan uptime = TimeSpan.FromMilliseconds(tickCountMs);
                CpuUptimeValue.Text = $"{(int)uptime.TotalDays}:{uptime.Hours:D2}:{uptime.Minutes:D2}:{uptime.Seconds:D2}";
            }
            catch { }

            try
            {
                CpuProcessCountValue.Text = Process.GetProcesses().Length.ToString();
            }
            catch { }
        }

        private double ReadWmiCpuUsage()
        {
            try
            {
                using ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT PercentProcessorTime FROM Win32_PerfFormattedData_PerfOS_Processor WHERE Name='_Total'");

                foreach (ManagementObject item in searcher.Get())
                {
                    if (item["PercentProcessorTime"] != null)
                        return Clamp(Convert.ToDouble(item["PercentProcessorTime"]), 0, 100);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CPU WMI load failed: {ex.Message}");
            }

            return 0;
        }

        private double ReadWmiCpuClock()
        {
            try
            {
                using ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT CurrentClockSpeed FROM Win32_Processor");
                List<double> clocks = new List<double>();

                foreach (ManagementObject item in searcher.Get())
                {
                    if (item["CurrentClockSpeed"] != null)
                        clocks.Add(Convert.ToDouble(item["CurrentClockSpeed"]));
                }

                if (clocks.Count > 0)
                    return clocks.Average();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CPU WMI clock failed: {ex.Message}");
            }

            return 0;
        }

        private void UpdateFallbackWmiGpuLoad()
        {
            if (_isQueryingWmiGpu) return;
            _isQueryingWmiGpu = true;

            Task.Run(() =>
            {
                try
                {
                    using ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT Name, UtilizationPercentage FROM Win32_PerfFormattedData_GPUPerformanceCounters_GPUEngine");
                    double core = 0;
                    double encoder = 0;
                    foreach (ManagementObject item in searcher.Get())
                    {
                        string name = item["Name"]?.ToString();
                        if (name != null)
                        {
                            if (name.IndexOf("engtype_3D", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                if (item["UtilizationPercentage"] != null)
                                    core += Convert.ToDouble(item["UtilizationPercentage"]);
                            }
                            else if (name.IndexOf("engtype_Video", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                if (item["UtilizationPercentage"] != null)
                                    encoder += Convert.ToDouble(item["UtilizationPercentage"]);
                            }
                        }
                    }
                    _fallbackGpuCoreLoad = Clamp(core, 0, 100);
                    _fallbackGpuEncoderLoad = Clamp(encoder, 0, 100);

                    using ManagementObjectSearcher searcherMem = new ManagementObjectSearcher("SELECT DedicatedUsage, SharedUsage FROM Win32_PerfFormattedData_GPUPerformanceCounters_GPUAdapterMemory");
                    double maxMemBytes = 0;
                    foreach (ManagementObject item in searcherMem.Get())
                    {
                        double dedicated = item["DedicatedUsage"] != null ? Convert.ToDouble(item["DedicatedUsage"]) : 0;
                        double shared = item["SharedUsage"] != null ? Convert.ToDouble(item["SharedUsage"]) : 0;
                        double total = dedicated + shared;
                        if (total > maxMemBytes) maxMemBytes = total;
                    }
                    if (maxMemBytes > 0)
                        _fallbackGpuVramString = $"{maxMemBytes / (1024.0 * 1024.0 * 1024.0):0.##} GB";
                }
                catch { }
                finally
                {
                    _isQueryingWmiGpu = false;
                }
            });
        }

        private double? ReadGpuMemoryLoadFromSmallData(IHardware gpu)
        {
            double? used = FindSensorValue(gpu, SensorType.SmallData,
                new[] { "GPU Memory Used", "Memory Used", "D3D Dedicated Memory Used", "Dedicated Memory Used" });

            double? total = FindSensorValue(gpu, SensorType.SmallData,
                new[] { "GPU Memory Total", "Memory Total", "D3D Dedicated Memory Total", "Dedicated Memory Total" });

            if (used.HasValue && total.HasValue && total.Value > 0)
                return used.Value / total.Value * 100.0;

            return null;
        }

        private void RefreshNetworkInterfaces()
        {
            try
            {
                List<NetworkInterface> adapters = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(adapter => adapter.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    .Where(adapter => adapter.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                    .Where(adapter => adapter.OperationalStatus == OperationalStatus.Up)
                    .OrderBy(adapter => adapter.Name)
                    .ToList();

                _networkInterfaces = adapters;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Network interface refresh failed: {ex.Message}");
                _networkInterfaces = new List<NetworkInterface>();
            }
        }

        private (double Download, double Upload) ReadNetworkUsageMbps()
        {
            if (!_netInitDone || _networkInterfaces.Count == 0)
                return (0, 0);

            IEnumerable<NetworkInterface> adapters = _selectedNetworkId == AllNetworkAdaptersTag
                    ? _networkInterfaces
                    : _networkInterfaces.Where(adapter => adapter.Id == _selectedNetworkId);

            var (bytesReceived, bytesSent) = GetTotalNetworkBytes(adapters);
            DateTime now = DateTime.UtcNow;
            string sampleKey = _selectedNetworkId ?? AllNetworkAdaptersTag;

            if (!_networkSamples.TryGetValue(sampleKey, out NetworkSample previous))
            {
                _networkSamples[sampleKey] = new NetworkSample { BytesReceived = bytesReceived, BytesSent = bytesSent, Time = now };
                return (0, 0);
            }

            double seconds = (now - previous.Time).TotalSeconds;
            long deltaReceived = Math.Max(0, bytesReceived - previous.BytesReceived);
            long deltaSent = Math.Max(0, bytesSent - previous.BytesSent);
            _networkSamples[sampleKey] = new NetworkSample { BytesReceived = bytesReceived, BytesSent = bytesSent, Time = now };

            if (seconds <= 0)
                return (0, 0);

            return (deltaReceived * 8.0 / seconds / 1_000_000.0, deltaSent * 8.0 / seconds / 1_000_000.0);
        }

        private (long Received, long Sent) GetTotalNetworkBytes(IEnumerable<NetworkInterface> adapters)
        {
            long totalReceived = 0;
            long totalSent = 0;

            foreach (NetworkInterface adapter in adapters)
            {
                try
                {
                    IPv4InterfaceStatistics stats = adapter.GetIPv4Statistics();
                    totalReceived += stats.BytesReceived;
                    totalSent += stats.BytesSent;
                }
                catch { }
            }

            return (totalReceived, totalSent);
        }

        private class DiskInfo
        {
            public string Model { get; set; }
            public string Type { get; set; }
            public double SizeGB { get; set; }
            public double FreeSpaceGB { get; set; }
        }

        private static Dictionary<string, DiskInfo> _diskInfoCache = null;

        private void InitializeDiskInfoCache()
        {
            if (_diskInfoCache != null) return;
            _diskInfoCache = new Dictionary<string, DiskInfo>();

            try
            {
                Dictionary<string, string> mediaTypes = new Dictionary<string, string>();
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(@"root\Microsoft\Windows\Storage", "SELECT DeviceId, MediaType, BusType FROM MSFT_PhysicalDisk"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        string id = obj["DeviceId"]?.ToString();
                        if (string.IsNullOrEmpty(id)) continue;

                        int mediaType = Convert.ToInt32(obj["MediaType"]);
                        int busType = Convert.ToInt32(obj["BusType"]);
                        string typeStr = busType switch
                        {
                            17 => "NVMe SSD",
                            14 or 16 => monitoringLang["main"]["sdc"].ToString(),
                            _ => mediaType switch
                            {
                                5 => "NVMe SSD",
                                4 => "SSD",
                                3 => "HDD",
                                _ => "Unknown"
                            }
                        };
                        mediaTypes[id] = typeStr;
                    }
                }

                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT Index, DeviceID, Model, Size, InterfaceType FROM Win32_DiskDrive"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        string index = obj["Index"]?.ToString();
                        string model = obj["Model"]?.ToString();
                        string deviceId = obj["DeviceID"]?.ToString();
                        string interfaceType = obj["InterfaceType"]?.ToString()?.ToUpperInvariant() ?? "";

                        if (index != null && ulong.TryParse(obj["Size"]?.ToString(), out ulong sizeBytes))
                        {
                            double freeSpaceBytes = 0;
                            try
                            {
                                string query = $"SELECT * FROM Win32_DiskDrive WHERE DeviceID='{deviceId.Replace("\\", "\\\\")}'";
                                using var relSearcher = new ManagementObjectSearcher(query);

                                foreach (ManagementObject drive in relSearcher.Get())
                                {
                                    foreach (ManagementObject partition in drive.GetRelated("Win32_DiskPartition"))
                                    {
                                        foreach (ManagementObject logical in partition.GetRelated("Win32_LogicalDisk"))
                                        {
                                            if (logical["FreeSpace"] != null)
                                            {
                                                freeSpaceBytes += Convert.ToDouble(logical["FreeSpace"]);
                                            }
                                            logical.Dispose();
                                        }
                                        partition.Dispose();
                                    }
                                    drive.Dispose();
                                }
                            }
                            catch { }

                            string type = mediaTypes.TryGetValue(index, out string t) && t != "Unknown" ? t : "";

                            if (string.IsNullOrEmpty(type) || type == monitoringLang["main"]["other"].ToString())
                            {
                                type = (interfaceType, model) switch
                                {
                                    var (it, m) when it.Contains("USB") => "USB",
                                    var (it, m) when it.Contains("SD") || it.Contains("MMC") || m.Contains("SD", StringComparison.OrdinalIgnoreCase) => "SD",
                                    var (it, m) when m.Contains("NVMe", StringComparison.OrdinalIgnoreCase) => "NVMe SSD",
                                    var (it, m) when m.Contains("SSD", StringComparison.OrdinalIgnoreCase) => "SSD",
                                    var (it, m) when m.Contains("HDD", StringComparison.OrdinalIgnoreCase) ||
                                                     m.Contains("WDC") || m.Contains("ST") => "HDD",
                                    _ => monitoringLang["main"]["other"].ToString()
                                };
                            }

                            _diskInfoCache[index] = new DiskInfo
                            {
                                Model = model ?? "Disk",
                                Type = type,
                                SizeGB = sizeBytes / 1024.0 / 1024.0 / 1024.0,
                                FreeSpaceGB = freeSpaceBytes / 1024.0 / 1024.0 / 1024.0
                            };
                        }
                    }
                }
            }
            catch { }
        }

        private List<DeviceOption> GetDiskOptions()
        {
            var options = new List<DeviceOption>();
            try
            {
                PerformanceCounterCategory category = new PerformanceCounterCategory("PhysicalDisk");
                var instances = category.GetInstanceNames()
                    .Where(name => !string.IsNullOrWhiteSpace(name) && name != TotalDiskInstance)
                    .OrderBy(name => name)
                    .ToList();

                InitializeDiskInfoCache();

                options.Add(new DeviceOption { DisplayName = monitoringLang["main"]["alldrives"].ToString(), Tag = TotalDiskInstance });

                foreach (var inst in instances)
                {
                    string displayName = inst;
                    string[] parts = inst.Split(' ');
                    if (parts.Length > 0 && int.TryParse(parts[0], out int diskNum))
                    {
                        if (_diskInfoCache != null && _diskInfoCache.TryGetValue(diskNum.ToString(), out DiskInfo info))
                        {
                            string letters = string.Join(" ", parts.Skip(1));
                            displayName = string.IsNullOrEmpty(letters) ? info.Model : $"{info.Model} ({letters})";
                        }
                    }
                    options.Add(new DeviceOption { DisplayName = displayName, Tag = inst });
                }
            }
            catch {}
            return options;
        }

        private double ReadDiskCounter(string counterName, Dictionary<string, PerformanceCounter> cache, string instance = null)
        {
            instance = instance ?? (string.IsNullOrWhiteSpace(_selectedDiskInstance) ? TotalDiskInstance : _selectedDiskInstance);
            string key = $"{counterName}_{instance}";

            if (!cache.TryGetValue(key, out PerformanceCounter counter))
            {
                try
                {
                    counter = new PerformanceCounter("PhysicalDisk", counterName, instance, true);
                    counter.NextValue();
                    cache[key] = counter;
                }
                catch
                {
                    return 0;
                }
            }

            try
            {
                return counter.NextValue();
            }
            catch
            {
                return 0;
            }
        }

        private double ReadDiskUsage(string instance = null)
        {
            instance = instance ?? (string.IsNullOrWhiteSpace(_selectedDiskInstance) ? TotalDiskInstance : _selectedDiskInstance);
            try
            {
                PerformanceCounter counter = GetDiskCounter(instance);
                return Clamp(counter.NextValue(), 0, 100);
            }
            catch
            {
                return ReadWmiDiskUsage(instance);
            }
        }

        private PerformanceCounter GetDiskCounter(string instance)
        {
            if (!_diskUsageCounters.TryGetValue(instance, out PerformanceCounter counter))
            {
                counter = new PerformanceCounter("PhysicalDisk", "% Disk Time", instance, true);
                counter.NextValue();
                _diskUsageCounters[instance] = counter;
            }

            return counter;
        }

        private double ReadWmiDiskUsage(string instance)
        {
            try
            {
                string safeInstance = instance.Replace("'", "''");
                using ManagementObjectSearcher searcher = new ManagementObjectSearcher($"SELECT PercentDiskTime FROM Win32_PerfFormattedData_PerfDisk_PhysicalDisk WHERE Name='{safeInstance}'");

                foreach (ManagementObject item in searcher.Get())
                {
                    if (item["PercentDiskTime"] != null)
                        return Clamp(Convert.ToDouble(item["PercentDiskTime"]), 0, 100);
                }
            }
            catch {}
            return 0;
        }

        private double Clamp(double value, double min, double max)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return min;

            return Math.Max(min, Math.Min(max, value));
        }

        private void Timer_Tick(object sender, EventArgs e) => RefreshProcessList();

        private void RefreshProcessList()
        {
            if (_isUpdatingPaused) return;
            try
            {
                string[] criticalExclusions = {
                    "registry", "smss", "csrss", "wininit", "services", "lsass", "winlogon", "shellhost", "useroobebroker", "shellexperiencehost", "runtimebroker", "makutweaker",
                    "dwm", "system", "idle", "memory compression", "secure system", "rundll32", "dllhost", "svchost", "sihost", "smartscreen", "fontdrvhost"
                };

                string[] standardExclusions = {
                    "msedgewebview2", "startmenuexperiencehost", "taskmgr", "explorer",
                    "ctfmon", "searchindexer", "crossdeviceservice", "bioenrollmenthost",
                    "searchapp", "wpfsurface", "searchhost", "phoneexperiencehost", "textinputhost", "nvidia overlay", "lockapp",
                    "systemsettings", "crossdeviceresume", "applicationframehost", "searchui", "gamebar",
                    "xboxgamebarwidgets", "xboxpcappft", "icloudservices", "widgets", "xboxgamebarspotify", "backgroundtaskhost",
                    "perfwatson2", "systemsettingsadminflows", "igcctray", "igcc", "microsoft.cmdpal.ui", "wwahost", "rtkuwp",
                    "nvcontainer", "snippingtool", "softlandingtask", "unsecapp", "gameinputredistservice",
                    "accuserps", "nvsphelper64", "openrgb", "widgetservice",
                    "applemobiledeviceprocess", "aqauserps", "windowspackagemanagerserver", "dataexchangehost",
                    "inputpersonalization", "bootcamp", "settingsynchost", "igfxtray", "igfxhk", "securityhealthsystray",
                    "storedesktopextension", "searchprotocolhost", "backgroundtransferhost", "xgamehelper",
                    "comppkgsrv", "gamebarftserver", "appactions", "systemsettingsbroker"
                };


                string savedExclusions = Properties.Settings.Default.ProcessExclusions;

                var userExclusions = !string.IsNullOrWhiteSpace(savedExclusions)
                    ? savedExclusions.Split(',').Select(x => x.Trim().ToLower()).ToList()
                    : Enumerable.Empty<string>();

                var finalStandardExclusions = standardExclusions.Concat(userExclusions).Distinct().ToList();
                long threshold = _dynamicMemoryThreshold;

                bool showOnlyHung = OnlyNotRespondingCheck?.IsChecked ?? false;
                bool groupProcesses = GroupProcessesCheck?.IsChecked ?? false;
                bool showHidden = Properties.Settings.Default.ShowHidden_processMGR;

                if (MemoryLimitCombo?.SelectedItem is ComboBoxItem comboItem)
                    threshold = long.Parse(comboItem.Tag.ToString());

                var allValidProcesses = Process.GetProcesses()
                    .Where(p =>
                    {
                        try
                        {
                            string processNameLow = p.ProcessName.ToLower();

                            if (criticalExclusions.Contains(processNameLow)) return false;
                            if (userExclusions.Contains(processNameLow)) return false;
                            if (IsCriticalSystemProcess(p.Id)) return false;
                            if (showOnlyHung && p.Responding) return false;

                            if (!showHidden)
                            {
                                if (p.SessionId == 0) return false;
                                if (finalStandardExclusions.Contains(processNameLow)) return false;
                                if (processNameLow.StartsWith("windowsinternal")) return false;

                                string exePath = p.MainModule?.FileName;
                                if (!string.IsNullOrEmpty(exePath))
                                {
                                    string[] hiddenPaths = {
                                        @"\Windows\System32\drivers\",
                                        @"\Windows\System32\DriverStore\",
                                        @"\Windows\SystemApps\",
                                        @"\Windows\WinSxS\",
                                        @"\Windows\Servicing\",
                                        @"\Windows\SoftwareDistribution\",
                                        @"\Program Files\Windows Defender\",
                                        @"\ProgramData\Microsoft\Windows Defender\",
                                        @"\Microsoft\OneDrive\",
                                        @"\Microsoft\EdgeUpdate\",
                                        @"\Microsoft\EdgeWebView\",
                                        @"\Windows\Microsoft.NET\Framework\",
                                        @"\Windows\Microsoft.NET\Framework64\",
                                        @"\Common Files\Microsoft Shared\ClickToRun\"
                                    };

                                    bool isHiddenPath = hiddenPaths.Any(path =>
                                        exePath.IndexOf(path, StringComparison.OrdinalIgnoreCase) >= 0);

                                    if (isHiddenPath) return false;
                                }

                                if (!CanTerminateProcess(p.Id)) return false;
                            }

                            return true;
                        }
                        catch
                        {
                            return showHidden && !criticalExclusions.Contains(p.ProcessName.ToLower()) && !userExclusions.Contains(p.ProcessName.ToLower());
                        }
                    });

                List<ProcessItem> targetList;

                if (groupProcesses)
                {
                    targetList = allValidProcesses
                        .GroupBy(p => p.ProcessName)
                        .Select(g => new
                        {
                            Name = g.Key,
                            TotalMemory = g.Sum(p => { try { return p.WorkingSet64; } catch { return 0L; } }),
                            FirstProc = g.First()
                        })
                        .Where(g => g.TotalMemory > threshold)
                        .OrderByDescending(g => g.TotalMemory)
                        .Select(g => new ProcessItem
                        {
                            Identifier = "GROUP_" + g.Name,
                            Id = g.FirstProc.Id,
                            RawName = g.Name,
                            Name = GetFriendlyProcessName(g.FirstProc),
                            RawMemory = g.TotalMemory,
                            MemoryUsage = FormatMemory(g.TotalMemory),
                            Icon = GetProcessIcon(g.FirstProc)
                        })
                        .ToList();
                }
                else
                {
                    targetList = allValidProcesses
                        .Where(p => { try { return p.WorkingSet64 > threshold; } catch { return false; } })
                        .OrderByDescending(p => { try { return p.WorkingSet64; } catch { return 0L; } })
                        .Select(p => new ProcessItem
                        {
                            Identifier = p.Id.ToString(),
                            Id = p.Id,
                            RawName = p.ProcessName,
                            Name = GetFriendlyProcessName(p),
                            RawMemory = p.WorkingSet64,
                            MemoryUsage = FormatMemory(p.WorkingSet64),
                            Icon = GetProcessIcon(p)
                        })
                        .ToList();
                }

                if (!string.IsNullOrWhiteSpace(_currentSearchQuery))
                {
                    targetList = targetList.Where(p =>
                        (p.Name != null && p.Name.ToLower().Contains(_currentSearchQuery)) ||
                        (p.RawName != null && p.RawName.ToLower().Contains(_currentSearchQuery))
                    ).ToList();
                }

                long totalVisibleMemory = targetList.Sum(x => x.RawMemory);
                foreach (var item in targetList)
                {
                    item.MemoryPercentage = totalVisibleMemory > 0 ? ((double)item.RawMemory / totalVisibleMemory * 100) : 0;
                }

                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    try
                    {
                        var existing = _items.Where(x => !string.IsNullOrEmpty(x.Identifier))
                                             .GroupBy(x => x.Identifier)
                                             .ToDictionary(g => g.Key, g => g.First());

                        foreach (var p in targetList)
                        {
                            if (existing.TryGetValue(p.Identifier, out var item))
                            {
                                item.Name = p.Name;
                                item.RawName = p.RawName;
                                item.MemoryUsage = p.MemoryUsage;
                                item.MemoryPercentage = p.MemoryPercentage;
                                item.Id = p.Id;
                                item.Icon = p.Icon;
                            }
                            else
                            {
                                _items.Add(p);
                            }
                        }

                        for (int i = _items.Count - 1; i >= 0; i--)
                        {
                            if (!targetList.Any(p => p.Identifier == _items[i].Identifier))
                                _items.RemoveAt(i);
                        }

                        for (int i = 0; i < targetList.Count; i++)
                        {
                            var item = _items.FirstOrDefault(x => x.Identifier == targetList[i].Identifier);
                            if (item != null)
                            {
                                int index = _items.IndexOf(item);
                                if (index != i) _items.Move(index, i);
                            }
                        }
                    }
                    catch
                    {
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        private async void KillProcess_Click(object sender, RoutedEventArgs e)
        {
            List<ProcessItem> targets = new List<ProcessItem>();
            if (sender is Button btn && btn.DataContext is ProcessItem item)
            {
                targets.Add(item);
            }
            else if (ProcessListView.SelectedItems.Count > 0)
            {
                foreach (var selected in ProcessListView.SelectedItems)
                {
                    if (selected is ProcessItem processItem)
                    {
                        targets.Add(processItem);
                    }
                }
            }

            if (targets.Count > 0)
            {
                try
                {
                    foreach (var target in targets)
                    {
                        var processesToKill = Process.GetProcessesByName(target.RawName);
                        foreach (var proc in processesToKill)
                        {
                            try { proc.Kill(); } catch { }
                        }
                    }

                    await Task.Delay(150);
                    _isUpdatingPaused = false;
                    if (_timer != null && !_timer.IsEnabled) _timer.Start();

                    PauseIcon.Symbol = iNKORE.UI.WPF.Modern.Controls.Symbol.Pause;
                    RefreshProcessList();
                }
                catch (Exception ex)
                {
                    iNKORE.UI.WPF.Modern.Controls.MessageBox.Show(ex.Message, "MakuTweaker", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void FilterChanged(object sender, RoutedEventArgs e)
        {
            if (isLoaded)
            {
                Properties.Settings.Default.LastProcessFilterIndex = MemoryLimitCombo.SelectedIndex;
                Properties.Settings.Default.compact = CompactViewCheck?.IsChecked ?? false;
                Properties.Settings.Default.group = GroupProcessesCheck?.IsChecked ?? false;
                Properties.Settings.Default.Save();
                RefreshProcessList();
            }
        }

        private void ProcessListView_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete && ProcessListView.SelectedItem != null)
            {
                KillProcess_Click(sender, e);
            }

            if (e.Key == Key.F5)
            {
                _items.Clear();
                RefreshProcessList();
            }
            else if (e.Key == Key.F11)
            {
                ToggleExclusiveMode();
            }
        }

        private void LoadLang()
        {
            var languageCode = Properties.Settings.Default.lang ?? "en";

            pmgrLang = MainWindow.Localization.LoadLocalization(languageCode, "pmgr");
            monitoringLang = MainWindow.Localization.LoadLocalization(Properties.Settings.Default.lang, "monitoring");

            var perfor = MainWindow.Localization.LoadLocalization(languageCode, "perfor");
            var myan = MainWindow.Localization.LoadLocalization(languageCode, "myan");
            var tooltips = MainWindow.Localization.LoadLocalization(languageCode, "tooltips");

            mgr_set_tooltip.Content = pmgrLang["main"]["settings"];
            mgr_pause_tooltip.Content = pmgrLang["main"]["pause"];
            mgr_pin_tooltip.Content = pmgrLang["main"]["topmost"];
            myan_tooltip.Content = myan["main"]["excltitle"];
            PerformanceBtnText.Text = perfor["main"]["label"];

            label.Text = pmgrLang["main"]["label"];
            groupproc.Text = pmgrLang["main"]["process"];
            groupmem.Text = pmgrLang["main"]["memuse"];

            if (CompactViewCheck != null) CompactViewCheck.Content = pmgrLang["main"]["compact"];
            if (GroupProcessesCheck != null) GroupProcessesCheck.Content = pmgrLang["main"]["group"];
            if (OnlyNotRespondingCheck != null) OnlyNotRespondingCheck.Content = pmgrLang["main"]["onlyfrozen"];
            if (AutoStartCheck != null) AutoStartCheck.Content = pmgrLang["main"]["modeset"];

            if (MemoryLimitCombo != null && MemoryLimitCombo.Items.Count >= 7)
            {
                string[] keys = { "showall", "from50mb", "from100mb", "from300mb", "from500mb", "from1000mb", "from2000mb" };

                for (int i = 0; i < keys.Length; i++)
                {
                    if (i < MemoryLimitCombo.Items.Count && MemoryLimitCombo.Items[i] is ComboBoxItem comboItem)
                        comboItem.Content = pmgrLang["main"][keys[i]];
                }
            }

            if (this.Resources["ItemContextMenu"] is System.Windows.Controls.ContextMenu contextMenu)
            {
                var items = (contextMenu as System.Windows.Controls.ItemsControl).Items;

                if (items.Count >= 3)
                {
                    if (items[0] is MenuItem itemKill) itemKill.Header = pmgrLang["main"]["endprocess"];
                    if (items[1] is MenuItem itemAddExcl) itemAddExcl.Header = pmgrLang["main"]["excl"];
                    if (items[2] is MenuItem itemLoc) itemLoc.Header = pmgrLang["main"]["location"];
                }
            }

            CpuNavLabel.Text = monitoringLang["main"]["CPU"];
            GpuNavLabel.Text = monitoringLang["main"]["GPU"];
            RamNavLabel.Text = monitoringLang["main"]["RAM"];
            NetworkNavLabel.Text = monitoringLang["main"]["Network"];
            DiskNavLabel.Text = monitoringLang["main"]["Disk"];

            CpuUsageLabel.Text = monitoringLang["main"]["cpuusage"];
            CpuClockLabel.Text = monitoringLang["main"]["cpufreq"];
            CpuTempLabel.Text = monitoringLang["main"]["temp"];
            CpuTdpLabel.Text = monitoringLang["main"]["tdp"];
            CpuProcessCountLabel.Text = monitoringLang["main"]["proccount"];
            CpuUptimeLabel.Text = monitoringLang["main"]["uptime"];

            GpuCoreLoadLabel.Text = monitoringLang["main"]["gpuusage"];
            GpuMemoryLoadLabel.Text = monitoringLang["main"]["gpumem"];
            GpuEncoderLoadLabel.Text = monitoringLang["main"]["gpucod"];
            GpuTempLabel.Text = monitoringLang["main"]["temp"];
            GpuTdpLabel.Text = monitoringLang["main"]["tdp"];

            RamUsageLabel.Text = monitoringLang["main"]["ram"];
            RamPagefileLabel.Text = monitoringLang["main"]["rampafi"];

            NetworkDownloadLabel.Text = monitoringLang["main"]["netload"];
            NetworkUploadLabel.Text = monitoringLang["main"]["netusage"];
            NetworkIPv4Label.Text = monitoringLang["main"]["ipv4"];
            NetworkExtIpLabel.Text = monitoringLang["main"]["ipad"];
            NetworkMacLabel.Text = monitoringLang["main"]["mac"];
            BtnToggleNetworkPrivacy.Content = monitoringLang["main"]["show"];

            DiskUsageLabel.Text = monitoringLang["main"]["diskusage"];
            DiskReadLabel.Text = monitoringLang["main"]["readspeed"];
            DiskWriteLabel.Text = monitoringLang["main"]["writespeed"];
            DiskCapacityLabel.Text = monitoringLang["main"]["capacity"];
            DiskFreeSpaceLabel.Text = monitoringLang["main"]["freespace"];
            DiskTypeLabel.Text = monitoringLang["main"]["typedisk"];
            DiskHealthLabel.Text = monitoringLang["main"]["state"];
            DiskTotalReadsLabel.Text = monitoringLang["main"]["totalreads"];
            DiskTotalWritesLabel.Text = monitoringLang["main"]["totalwrites"];
            DiskTempLabel.Text = monitoringLang["main"]["temp"];

            MenuShowOverallCpu.Header = monitoringLang["main"]["cpu_all"];
            MenuShowPhysicalCpu.Header = monitoringLang["main"]["cpu_cores"];
            MenuShowLogicalCpu.Header = monitoringLang["main"]["cpu_threads"];
        }

        private void OpenLocation_Click(object sender, RoutedEventArgs e)
        {
            if (ProcessListView.SelectedItem is ProcessItem selected)
            {
                try
                {
                    var proc = Process.GetProcessById(selected.Id);
                    string filePath = proc.MainModule.FileName;
                    Process.Start("explorer.exe", $"/select, \"{filePath}\"");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "MakuTweaker Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        private void ContextMenu_Opened(object sender, RoutedEventArgs e) => _isUpdatingPaused = true;
        private void ContextMenu_Closed(object sender, RoutedEventArgs e)
        {
            _isUpdatingPaused = false;
            if (PauseIcon.Symbol == iNKORE.UI.WPF.Modern.Controls.Symbol.Pause)
            {
                _timer.Start();
                RefreshProcessList();
            }
        }

        private void MemoryLimitCombo_DropDownOpened(object sender, EventArgs e) => _isUpdatingPaused = true;
        private void MemoryLimitCombo_DropDownClosed(object sender, EventArgs e)
        {
            _isUpdatingPaused = false;
            RefreshProcessList();
            PauseIcon.Symbol = iNKORE.UI.WPF.Modern.Controls.Symbol.Pause;
        }

        private void SettingsBtn_Click(object sender, RoutedEventArgs e)
        {
            if (mw != null && mw.Topmost)
            {
                mw.Topmost = false;
                if (TopmostIcon != null)
                {
                    TopmostIcon.Symbol = iNKORE.UI.WPF.Modern.Controls.Symbol.Pin;
                }
            }

            ExclusionWindow win = new ExclusionWindow();
            win.Owner = Application.Current.MainWindow;
            win.ShowDialog();

            if (_performanceTimer != null)
            {
                int newSpeedMs = Properties.Settings.Default.GraphUpdateSpeedMs;
                if (newSpeedMs < 500) newSpeedMs = 1000;

                _performanceTimer.Interval = TimeSpan.FromMilliseconds(newSpeedMs);
            }

            _friendlyNameCache.Clear();
            RefreshProcessList();
        }

        private void AddToExclusions_Click(object sender, RoutedEventArgs e)
        {
            if (ProcessListView.SelectedItems.Count > 0)
            {
                try
                {
                    string savedExclusions = Properties.Settings.Default.ProcessExclusions;
                    var currentExclusions = !string.IsNullOrWhiteSpace(savedExclusions)
                        ? savedExclusions.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim().ToLower()).ToList()
                        : new List<string>();

                    bool changed = false;

                    foreach (var selected in ProcessListView.SelectedItems)
                    {
                        if (selected is ProcessItem item)
                        {
                            string currentName = item.RawName.ToLower();
                            if (!currentExclusions.Contains(currentName))
                            {
                                currentExclusions.Add(currentName);
                                changed = true;
                            }
                        }
                    }

                    if (changed)
                    {
                        Properties.Settings.Default.ProcessExclusions = string.Join(", ", currentExclusions);
                        Properties.Settings.Default.Save();
                        RefreshProcessList();
                    }
                }
                catch (Exception ex)
                {
                    iNKORE.UI.WPF.Modern.Controls.MessageBox.Show(ex.Message, "MakuTweaker Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void PauseBtn_Click(object sender, RoutedEventArgs e)
        {
            _isUpdatingPaused = !_isUpdatingPaused;
            if (_isUpdatingPaused)
            {
                _timer.Stop();
                _performanceTimer?.Stop();
                PauseIcon.Symbol = iNKORE.UI.WPF.Modern.Controls.Symbol.Play;
            }
            else
            {
                _timer.Start();
                _performanceTimer?.Start();
                _items.Clear();
                PauseIcon.Symbol = iNKORE.UI.WPF.Modern.Controls.Symbol.Pause;

                RefreshProcessList();
                if (_performanceVisible)
                {
                    RefreshPerformanceMetrics();
                }
            }
        }

        private void restartBtn_Click(object sender, RoutedEventArgs e)
        {
            if (isLoaded)
            {
                _items.Clear();
                _iconCache.Clear();
                _friendlyNameCache.Clear();
                RefreshProcessList();
            }
        }

        private void MakuYan_Click(object sender, RoutedEventArgs e)
        {
            if (mw != null && mw.Topmost)
            {
                mw.Topmost = false;
                if (TopmostIcon != null)
                {
                    TopmostIcon.Symbol = iNKORE.UI.WPF.Modern.Controls.Symbol.Pin;
                }
            }

            MakuYan mkyan = new MakuYan();
            mkyan.Owner = Application.Current.MainWindow;
            mkyan.ShowDialog();
            RefreshProcessList();
        }

        private void Page_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F5)
            {
                _items.Clear();
                RefreshProcessList();
            }
            else if (e.Key == Key.F11)
            {
                ToggleExclusiveMode();
            }
        }

        private void ProcessListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ProcessListView.SelectedItem != null)
                OpenLocation_Click(sender, e);
        }

        private void TopmostBtn_Click(object sender, RoutedEventArgs e)
        {
            if (mw != null)
            {
                mw.Topmost = !mw.Topmost;

                if (mw.Topmost)
                {
                    TopmostIcon.Symbol = iNKORE.UI.WPF.Modern.Controls.Symbol.UnPin;
                }
                else
                {
                    TopmostIcon.Symbol = iNKORE.UI.WPF.Modern.Controls.Symbol.Pin;
                }
            }
        }
        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateResponsiveUI(e.NewSize.Width, e.NewSize.Height);
            if (_isExclusiveMode)
            {
                _saveBoundsTimer.Stop();
                _saveBoundsTimer.Start();
            }
        }

        private void MainWindow_LocationChanged(object sender, EventArgs e)
        {
            if (_isExclusiveMode)
            {
                _saveBoundsTimer.Stop();
                _saveBoundsTimer.Start();
            }
        }

        private void UpdateResponsiveUI(double windowWidth, double windowHeight)
        {
            bool isCompact = windowWidth < 740;
            bool isNarrow = windowWidth < 900;
            bool isWide = windowWidth >= 1050;
            _isNarrowView = isNarrow;
            bool isHeightTooSmall = windowHeight < 600;

            if (_lastIsHeightTooSmall != isHeightTooSmall)
            {
                _lastIsHeightTooSmall = isHeightTooSmall;
                Visibility chartVisibility = isHeightTooSmall ? Visibility.Collapsed : Visibility.Visible;

                if (PerformanceScrollViewer != null)
                {
                    PerformanceScrollViewer.VerticalScrollBarVisibility = isHeightTooSmall
                        ? ScrollBarVisibility.Hidden
                        : ScrollBarVisibility.Auto;
                }

                Border[] charts = {
            CpuUsageBorder, Chart_CpuClock, Chart_CpuTemp, Chart_CpuTdp,
            Chart_GpuCoreLoad, Chart_GpuMemLoad, Chart_GpuTemp, Chart_GpuTdp, Chart_GpuEncoder,
            Chart_NetDownload, Chart_NetUpload,
            Chart_DiskUsage, Chart_DiskRead, Chart_DiskWrite,
            Chart_RamUsage
        };

                foreach (var chart in charts)
                {
                    if (chart != null) chart.Visibility = chartVisibility;
                }

                if (Chart_DiskTemp != null)
                {
                    Chart_DiskTemp.Visibility = isHeightTooSmall ? Visibility.Collapsed : (_hasDiskTempData ? Visibility.Visible : Visibility.Collapsed);
                }
            }

            if (ChartContainer != null && _lastIsNarrow != isNarrow)
            {
                _lastIsNarrow = isNarrow;

                if (isNarrow)
                {
                    NavColumn.MinWidth = 0;
                    NavColumn.Width = new GridLength(0);
                    PerformanceNavBorder.Visibility = Visibility.Collapsed;
                    Grid.SetColumn(ChartContainer, 0);
                    Grid.SetColumnSpan(ChartContainer, 2);
                }
                else
                {
                    double targetWidth = _isExclusiveMode ? 230 : 180;
                    NavColumn.MinWidth = targetWidth;
                    NavColumn.Width = new GridLength(targetWidth);
                    PerformanceNavBorder.Visibility = Visibility.Visible;
                    Grid.SetColumn(ChartContainer, 1);
                    Grid.SetColumnSpan(ChartContainer, 1);
                }
            }

            if (_cpuViewMode != CpuViewMode.Overall && CpuUsageBorder != null)
            {
                CpuUsageBorder.MinHeight = GetDynamicCpuGraphHeight(windowHeight);
            }
            else if (CpuUsageBorder != null)
            {
                CpuUsageBorder.MinHeight = 0;
            }

            if (AutoStartCheck != null)
            {
                AutoStartCheck.Visibility = (_isExclusiveMode && isWide) ? Visibility.Visible : Visibility.Collapsed;
            }

            int columnsCount = isNarrow ? 1 : 2;
            if (CpuTempTdpGrid != null) CpuTempTdpGrid.Columns = Math.Min(columnsCount, Math.Max(1, _cpuVisibleCards));
            if (GpuTempTdpGrid != null) GpuTempTdpGrid.Columns = Math.Min(columnsCount, Math.Max(1, _gpuVisibleCards));
            if (DiskReadWriteGrid != null) DiskReadWriteGrid.Columns = isNarrow ? 1 : 2;

            if (_lastIsCompact != isCompact)
            {
                _lastIsCompact = isCompact;
                Visibility generalVisibility = isCompact ? Visibility.Collapsed : Visibility.Visible;

                if (OnlyNotRespondingCheck != null) OnlyNotRespondingCheck.Visibility = generalVisibility;
                if (MakuYan != null) MakuYan.Visibility = generalVisibility;
                if (PerformanceBtn != null) PerformanceBtn.Visibility = generalVisibility;
                if (SettingsBtn != null) SettingsBtn.Visibility = generalVisibility;

                if (PerformanceBtn != null)
                {
                    PerformanceBtn.Width = isCompact ? 32 : double.NaN;
                    PerformanceBtn.Padding = isCompact ? new Thickness(0) : new Thickness(10, 0, 10, 0);
                    PerformanceBtn.Margin = new Thickness(8, 10, 24, 0);

                    if (PerformanceBtnText != null) PerformanceBtnText.Visibility = generalVisibility;
                    if (PerformanceBtnIcon != null) PerformanceBtnIcon.Margin = isCompact ? new Thickness(0) : new Thickness(0, 1, 8, 0);
                }
            }

            Visibility copyBtnVis = (windowWidth < 900) ? Visibility.Collapsed : Visibility.Visible;
            if (BtnCopyIPv4 != null) BtnCopyIPv4.Visibility = copyBtnVis;
            if (BtnCopyExtIP != null) BtnCopyExtIP.Visibility = copyBtnVis;
            if (BtnCopyMAC != null) BtnCopyMAC.Visibility = copyBtnVis;
            if (BtnToggleNetworkPrivacy != null) BtnToggleNetworkPrivacy.Visibility = copyBtnVis;
        }

        public static class IpResolver
        {
            public static async Task<string> GetExternalIpAsync()
            {
                string[] services = {
                    "https://api.ipify.org",
                    "https://icanhazip.com",
                    "https://ifconfig.me/ip"
                };

                using HttpClient client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(3);

                foreach (var service in services)
                {
                    try
                    {
                        string ip = await client.GetStringAsync(service);
                        return ip.Trim();
                    }
                    catch
                    {
                    }
                }
                return "...";
            }
        }

        private void MenuShowOverallCpu_Click(object sender, RoutedEventArgs e)
        {
            _cpuViewMode = CpuViewMode.Overall;
            MenuShowOverallCpu.IsChecked = true;
            MenuShowPhysicalCpu.IsChecked = false;
            MenuShowLogicalCpu.IsChecked = false;
            CpuUsageCanvas.Visibility = Visibility.Visible;
            CpuCoresScrollViewer.Visibility = Visibility.Collapsed;
            CpuUsageBorder.Height = 132;
            CpuUsageBorder.MinHeight = 0;
        }

        private void MenuShowPhysicalCpu_Click(object sender, RoutedEventArgs e)
        {
            _cpuViewMode = CpuViewMode.Physical;
            MenuShowOverallCpu.IsChecked = false;
            MenuShowPhysicalCpu.IsChecked = true;
            MenuShowLogicalCpu.IsChecked = false;
            CpuUsageCanvas.Visibility = Visibility.Collapsed;
            CpuCoresScrollViewer.Visibility = Visibility.Visible;
            CpuUsageBorder.ClearValue(HeightProperty);
            CpuUsageBorder.MinHeight = mw != null && mw.ActualHeight > 1110 ? Math.Max(250, mw.ActualHeight - 450) : 250;
            InitializeDetailedCpuGraphs();
        }

        private void MenuShowLogicalCpu_Click(object sender, RoutedEventArgs e)
        {
            _cpuViewMode = CpuViewMode.Logical;
            MenuShowOverallCpu.IsChecked = false;
            MenuShowPhysicalCpu.IsChecked = false;
            MenuShowLogicalCpu.IsChecked = true;
            CpuUsageCanvas.Visibility = Visibility.Collapsed;
            CpuCoresScrollViewer.Visibility = Visibility.Visible;
            CpuUsageBorder.ClearValue(HeightProperty);
            CpuUsageBorder.MinHeight = mw != null && mw.ActualHeight > 1110 ? Math.Max(250, mw.ActualHeight - 450) : 250;
            InitializeDetailedCpuGraphs();
        }

        private void InitializeDetailedCpuGraphs()
        {
            if (_cpuCoreUsageCounters == null)
            {
                _cpuCoreUsageCounters = new List<PerformanceCounter>();
                int logicalCount = Environment.ProcessorCount;
                for (int i = 0; i < logicalCount; i++)
                {
                    try
                    {
                        var pc = new PerformanceCounter("Processor", "% Processor Time", i.ToString());
                        _cpuCoreUsageCounters.Add(pc);
                        pc.NextValue();
                    }
                    catch { }
                }

                try
                {
                    using (var searcher = new ManagementObjectSearcher("Select NumberOfCores from Win32_Processor"))
                    {
                        foreach (var item in searcher.Get())
                        {
                            _cpuPhysicalCoreCount += Convert.ToInt32(item["NumberOfCores"]);
                        }
                    }
                }
                catch { }
                if (_cpuPhysicalCoreCount <= 0) _cpuPhysicalCoreCount = logicalCount;
            }

            CpuCoresGrid.Children.Clear();
            _cpuCoreGraphs = new List<GraphState>();

            CpuUsageBorder.ClearValue(FrameworkElement.MinHeightProperty);
            if (mw != null && _cpuViewMode != CpuViewMode.Overall)
            {
                CpuUsageBorder.MinHeight = GetDynamicCpuGraphHeight(mw.ActualHeight);
            }

            int count = _cpuViewMode == CpuViewMode.Physical ? _cpuPhysicalCoreCount : _cpuCoreUsageCounters.Count;

            int columns = count <= 4 ? 2 : count <= 16 ? 4 : 8;
            CpuCoresGrid.Columns = columns;

            if (count > 32)
            {
                CpuCoresScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                int rows = (int)Math.Ceiling((double)count / columns);
                CpuCoresGrid.MinHeight = rows * 60;
            }
            else
            {
                CpuCoresScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
                CpuCoresGrid.MinHeight = 0;
            }

            for (int i = 0; i < count; i++)
            {
                var border = new Border
                {
                    BorderBrush = (Brush)FindResource("ControlElevationBorderBrush"),
                    BorderThickness = new Thickness(0, 0, 1, 1),
                    Margin = new Thickness(0)
                };

                var grid = new Grid();
                var canvas = new Canvas { ClipToBounds = true };

                var polyline = new Polyline
                {
                    Stroke = (Brush)FindResource("SystemAccentColorLight2Brush"),
                    StrokeThickness = 1
                };

                var valueText = new TextBlock
                {
                    Text = "0%",
                    Foreground = (Brush)FindResource("TextFillColorPrimaryBrush") ?? Brushes.White,
                    FontSize = 11,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(0, 2, 4, 0),
                    FontFamily = new FontFamily("Segoe UI"),
                    Opacity = 0.8
                };

                var coreNumberText = new TextBlock
                {
                    Text = (i + 1).ToString(),
                    Foreground = (Brush)FindResource("TextFillColorPrimaryBrush") ?? Brushes.White,
                    FontSize = 11,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(4, 2, 0, 0),
                    FontFamily = new FontFamily("Segoe UI"),
                    Opacity = 0.5
                };

                canvas.Children.Add(polyline);
                grid.Children.Add(canvas);
                grid.Children.Add(coreNumberText);
                grid.Children.Add(valueText);

                border.Child = grid;
                CpuCoresGrid.Children.Add(border);
                _cpuCoreGraphs.Add(CreateGraph(canvas, polyline, valueText, 100, FormatPercentValue));
            }
        }

        private void BtnToggleNetworkPrivacy_Click(object sender, RoutedEventArgs e)
        {
            _isNetworkPrivacyDisabled = !_isNetworkPrivacyDisabled;
            if (_isNetworkPrivacyDisabled)
            {
                BtnToggleNetworkPrivacy.Content = monitoringLang["main"]["hide"].ToString(); NetworkIpValue.Text = _realIpv4;
                NetworkExternalIpValue.Text = _realExtIp;
                NetworkMacValue.Text = _realMac;
            }
            else
            {
                BtnToggleNetworkPrivacy.Content = monitoringLang["main"]["show"].ToString(); NetworkIpValue.Text = "-";
                NetworkExternalIpValue.Text = "-";
                NetworkMacValue.Text = "-";
            }
        }

        private async void CopyNetworkInfo_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                var icon = btn.Content as iNKORE.UI.WPF.Modern.Controls.SymbolIcon;

                string textToCopy = null;
                if (btn.Tag is string key)
                {
                    switch (key)
                    {
                        case "IPv4": textToCopy = _realIpv4; break;
                        case "ExtIP": textToCopy = _realExtIp; break;
                        case "MAC": textToCopy = _realMac; break;
                    }
                }
                else if (btn.Tag is TextBlock tb)
                {
                    textToCopy = tb.Text;
                }

                if (!string.IsNullOrEmpty(textToCopy) && textToCopy != "-" && textToCopy != "Определение...")
                {
                    try
                    {
                        Clipboard.SetText(textToCopy);
                        if (icon != null)
                        {
                            await TriggerCheckmarkAsync(btn, icon);
                        }
                    }
                    catch
                    {
                    }
                }
            }
        }

        private double GetDynamicCpuGraphHeight(double windowHeight)
        {
            if (windowHeight > 1110)
            {
                return 250 + (windowHeight - 1110);
            }
            return 250;
        }

        private async Task TriggerCheckmarkAsync(System.Windows.Controls.Button button, iNKORE.UI.WPF.Modern.Controls.SymbolIcon icon)
        {
            var originalSymbol = icon.Symbol;
            icon.Symbol = iNKORE.UI.WPF.Modern.Controls.Symbol.Accept;
            button.IsEnabled = false;
            await Task.Delay(5000);
            icon.Symbol = originalSymbol;
            button.IsEnabled = true;
        }

        public void FilterProcesses(string query)
        {
            _currentSearchQuery = query?.ToLower() ?? "";
            RefreshProcessList();
        }
    }
}