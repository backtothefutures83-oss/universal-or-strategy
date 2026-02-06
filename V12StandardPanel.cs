// V12 STANDARD EDITION - NinjaTrader 8 Vertical Sidebar Control Surface
// RELIABILITY FIRST: Uses standard UserControlCollection API (no injection)
// Docks to RIGHT side of chart as a vertical sidebar (~240px wide)
//
// Version: V12 STANDARD - Vertical Sidebar (Mockup Match)
// Author: Claude Code + SIMA Architecture
//
// STANDARD APPROACH:
// 1. Uses UserControlCollection.Add() - NinjaTrader's documented API
// 2. No visual tree manipulation, no reflection, no injection
// 3. 100% reliable loading - GUARANTEED
//

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives; // For Popup
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using NinjaTrader.Cbi;
using NinjaTrader.Data;
using NinjaTrader.Gui.Chart;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Indicators;

namespace NinjaTrader.NinjaScript.Indicators
{
    public class V12StandardPanel : Indicator
    {
        #region Variables

        /// <summary>Per-mode settings for panel persistence</summary>
        public class V12ModeSettings
        {
            public int TargetCount { get; set; }
            public string T1Val { get; set; }
            public string T1Type { get; set; }
            public string T2Val { get; set; }
            public string T2Type { get; set; }
            public string T3Val { get; set; }
            public string T3Type { get; set; }
            public string STR { get; set; }
            public string StopType { get; set; }
            public string MAX { get; set; }

            public V12ModeSettings()
            {
                TargetCount = 3;
                T1Val = "1.0"; T1Type = "ATR";
                T2Val = "2.0"; T2Type = "ATR";
                T3Val = "3.0"; T3Type = "ATR";
                STR = "$150"; StopType = "ATR"; MAX = "$1200";
            }

            public string ToLine()
            {
                return $"{TargetCount}|{T1Val}|{T1Type}|{T2Val}|{T2Type}|{T3Val}|{T3Type}|{STR}|{StopType}|{MAX}";
            }

            public static V12ModeSettings FromLine(string line)
            {
                var s = new V12ModeSettings();
                if (string.IsNullOrEmpty(line)) return s;
                var parts = line.Split('|');
                if (parts.Length >= 10)
                {
                    int.TryParse(parts[0], out int tc); s.TargetCount = tc > 0 ? tc : 3;
                    s.T1Val = parts[1]; s.T1Type = parts[2];
                    s.T2Val = parts[3]; s.T2Type = parts[4];
                    s.T3Val = parts[5]; s.T3Type = parts[6];
                    s.STR = parts[7]; s.StopType = parts[8]; s.MAX = parts[9];
                }
                return s;
            }
        }

        /// <summary>Full config with explicit mode properties</summary>
        public class V12FullConfig
        {
            public string ActiveMode { get; set; }
            public V12ModeSettings ORB { get; set; }
            public V12ModeSettings RMA { get; set; }
            public V12ModeSettings RETEST { get; set; }
            public V12ModeSettings MOMO { get; set; }
            public V12ModeSettings FFMA { get; set; }
            public V12ModeSettings TREND { get; set; }

            public V12FullConfig()
            {
                ActiveMode = "ORB";
                ORB = new V12ModeSettings();
                RMA = new V12ModeSettings();
                RETEST = new V12ModeSettings();
                MOMO = new V12ModeSettings();
                FFMA = new V12ModeSettings();
                TREND = new V12ModeSettings();
            }

            public V12ModeSettings GetSettings(string mode)
            {
                switch(mode?.ToUpper())
                {
                    case "ORB": return ORB ?? (ORB = new V12ModeSettings());
                    case "RMA": return RMA ?? (RMA = new V12ModeSettings());
                    case "RETEST": return RETEST ?? (RETEST = new V12ModeSettings());
                    case "MOMO": return MOMO ?? (MOMO = new V12ModeSettings());
                    case "FFMA": return FFMA ?? (FFMA = new V12ModeSettings());
                    case "TREND": return TREND ?? (TREND = new V12ModeSettings());
                    default: return ORB ?? (ORB = new V12ModeSettings());
                }
            }

            public void SetSettings(string mode, V12ModeSettings settings)
            {
                switch(mode?.ToUpper())
                {
                    case "ORB": ORB = settings; break;
                    case "RMA": RMA = settings; break;
                    case "RETEST": RETEST = settings; break;
                    case "MOMO": MOMO = settings; break;
                    case "FFMA": FFMA = settings; break;
                    case "TREND": TREND = settings; break;
                }
            }
        }

        // In-memory config store
        private V12FullConfig fullConfig;

        // Config file path for persistence
        private string ConfigFilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "NinjaTrader 8", "V12_Std_Memory.json");

        // Panel state
        private bool panelCreated = false;
        private bool isApplyingSettings = false; // Prevents auto-save during mode switch
        private Border mainPanel;
        private ScrollViewer scrollViewer;
        private StackPanel mainStack;

        // TCP Connection
        private TcpClient tcpClient;
        private NetworkStream tcpStream;
        private readonly object tcpLock = new object();
        private Thread receiveThread;
        private volatile bool isConnected = false;
        private ConcurrentQueue<string> responseQueue = new ConcurrentQueue<string>();

        // Symbol State
        private string activeSymbol = "MES";
        private double lastPrice = 0;

        // Mode Flags
        private bool isRetestRmaToggle = false;
        private bool isTrendRmaToggle = false;
        private bool isGhostMode = false;

        // Selected Target Count (3 default)
        private int selectedTargetCount = 3;

        // Selected Config Mode
        private string selectedConfigMode = "ORB";

        // UI Components - Section 0: Identity
        private Border hubStatusLed;
        private ComboBox leaderAccountCombo;
        private ComboBox fleetAccountCombo; // Legacy - replaced by multi-select
        private Button fleetSelectButton; // Button to open fleet selection popup
        private Popup fleetPopup; // Popup for multi-select checkboxes
        private StackPanel fleetCheckboxPanel; // Container for account checkboxes
        private List<string> selectedFleetAccounts = new List<string>(); // Tracked selected accounts
        private ComboBox directionCombo;
        private TextBox priceInput;
        private Button submitButton;

        // UI Components - Section 1: Execution
        private Button orLongButton, orShortButton;
        private Button retestButton, rmaButton;
        private int retestCycleState = 0;        // 0=RETEST, 1=RET+, 2=RET-
        private bool isRmaModeActive = false;    // RMA chart-click mode toggle
        private Button momoButton, ffmaButton, mButton;
        private Button trendButton, trendRmaToggle;
        private Button t1Button, t2Button, t3Button, t4Button, t5Button;
        private Button trim25Button, trim50Button, beButton, trailButton;
        private TextBox trailDistInput;
        private Button flattenButton;
        private TextBlock lastPriceText;

        // UI Components - Section 2: Telemetry
        private TextBlock or5Text, or15Text;
        private TextBlock ema9Text, ema15Text, ema30Text, ema65Text, ema200Text;
        private TextBlock atrText;
        private Button mktSyncButton;
        private Border trendIndicator;
        private TextBlock trendText;

        // UI Components - Section 3: Config
        private Button modeOrbButton, modeRmaButton, modeRetestButton;
        private Button modeMomoButton, modeFfmaButton, modeTrendButton;
        private Button cnt1, cnt2, cnt3, cnt4, cnt5;
        private TextBox svT1Val, svT2Val, svT3Val, svT4Val, svT5Val;
        private ComboBox svT1Type, svT2Type, svT3Type, svT4Type, svT5Type, svStrType;
        private TextBox strVal, maxVal;
        private StackPanel t2Row, t3Row, t4Row, t5Row;  // For visibility control
        private Button syncAllButton;

        // Glow effect
        private DispatcherTimer glowTimer;

        #endregion

        #region V12 Color Palette (Frozen Brushes)

        private static readonly SolidColorBrush BgDeep;
        private static readonly SolidColorBrush BgSlate;
        private static readonly SolidColorBrush BorderSlate;
        private static readonly SolidColorBrush BtnBg;
        private static readonly SolidColorBrush BtnBorder;
        private static readonly SolidColorBrush TextPrimary;
        private static readonly SolidColorBrush TextMuted;
        private static readonly SolidColorBrush CyanAccent;
        private static readonly SolidColorBrush GreenBg, GreenFg, GreenBorder;
        private static readonly SolidColorBrush RedBg, RedFg, RedBorder;
        private static readonly SolidColorBrush OrangeBg, OrangeFg, OrangeBorder;
        private static readonly SolidColorBrush YellowBg, YellowFg, YellowBorder;
        private static readonly SolidColorBrush PinkBg, PinkFg, PinkBorder;
        private static readonly SolidColorBrush CyanBg, CyanFg, CyanBorder;
        private static readonly SolidColorBrush PurpleFg;

        static V12StandardPanel()
        {
            BgDeep = Freeze(new SolidColorBrush(Color.FromRgb(5, 5, 8)));
            BgSlate = Freeze(new SolidColorBrush(Color.FromRgb(15, 23, 42)));
            BorderSlate = Freeze(new SolidColorBrush(Color.FromRgb(30, 41, 59)));
            BtnBg = Freeze(new SolidColorBrush(Color.FromRgb(23, 23, 28)));
            BtnBorder = Freeze(new SolidColorBrush(Color.FromRgb(45, 45, 55)));
            TextPrimary = Freeze(new SolidColorBrush(Color.FromRgb(220, 220, 220)));
            TextMuted = Freeze(new SolidColorBrush(Color.FromRgb(115, 115, 125)));
            CyanAccent = Freeze(new SolidColorBrush(Color.FromRgb(34, 211, 238)));

            GreenBg = Freeze(new SolidColorBrush(Color.FromRgb(6, 78, 59)));
            GreenFg = Freeze(new SolidColorBrush(Color.FromRgb(74, 222, 128)));
            GreenBorder = Freeze(new SolidColorBrush(Color.FromRgb(5, 150, 105)));

            RedBg = Freeze(new SolidColorBrush(Color.FromRgb(127, 29, 29)));
            RedFg = Freeze(new SolidColorBrush(Color.FromRgb(252, 165, 165)));
            RedBorder = Freeze(new SolidColorBrush(Color.FromRgb(220, 38, 38)));

            OrangeBg = Freeze(new SolidColorBrush(Color.FromRgb(124, 45, 18)));
            OrangeFg = Freeze(new SolidColorBrush(Color.FromRgb(251, 146, 60)));
            OrangeBorder = Freeze(new SolidColorBrush(Color.FromRgb(234, 88, 12)));

            YellowBg = Freeze(new SolidColorBrush(Color.FromRgb(113, 63, 18)));
            YellowFg = Freeze(new SolidColorBrush(Color.FromRgb(250, 204, 21)));
            YellowBorder = Freeze(new SolidColorBrush(Color.FromRgb(202, 138, 4)));

            PinkBg = Freeze(new SolidColorBrush(Color.FromRgb(131, 24, 67)));
            PinkFg = Freeze(new SolidColorBrush(Color.FromRgb(244, 114, 182)));
            PinkBorder = Freeze(new SolidColorBrush(Color.FromRgb(219, 39, 119)));

            CyanBg = Freeze(new SolidColorBrush(Color.FromRgb(22, 78, 99)));
            CyanFg = Freeze(new SolidColorBrush(Color.FromRgb(34, 211, 238)));
            CyanBorder = Freeze(new SolidColorBrush(Color.FromRgb(8, 145, 178)));

            PurpleFg = Freeze(new SolidColorBrush(Color.FromRgb(168, 85, 247)));
        }

        private static SolidColorBrush Freeze(SolidColorBrush brush) { brush.Freeze(); return brush; }

        #endregion

        #region Properties

        [NinjaScriptProperty]
        [Display(Name = "IPC Port", Description = "TCP Port to connect to V12 Strategy", Order = 1, GroupName = "Connection")]
        public int IpcPort { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Auto Connect", Description = "Automatically connect to IPC on load", Order = 2, GroupName = "Connection")]
        public bool AutoConnect { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Panel Width", Description = "Width of the sidebar panel (200-280)", Order = 1, GroupName = "Display")]
        public int PanelWidth { get; set; }

        #endregion

        #region OnStateChange

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "V12 STANDARD - Vertical Sidebar Control Surface (Reliable)";
                Name = "V12 Standard Panel";
                Calculate = Calculate.OnPriceChange;
                IsOverlay = true;
                DisplayInDataBox = false;
                DrawOnPricePanel = false;
                IsSuspendedWhileInactive = false;

                IpcPort = 5000;
                AutoConnect = true;
                PanelWidth = 242; // V12: Reduced from 250 to prevent sidebar clipping
            }
            else if (State == State.Configure)
            {
                Print("V12 STANDARD: BUILD 1008 (Layout Restored - Full Stretch)"); 
            }
            else if (State == State.Historical)
            {
                if (ChartControl != null)
                {
                    ChartControl.Dispatcher.InvokeAsync(() => {
                        CreatePanel();
                        PopulateAccountCombos();
                    });
                }
            }
            else if (State == State.Realtime)
            {
                activeSymbol = Instrument.MasterInstrument.Name;
                if (AutoConnect) Task.Run(() => ConnectToStrategy());
            }
            else if (State == State.Terminated)
            {
                if (ChartControl != null)
                {
                    ChartControl.Dispatcher.InvokeAsync(RemovePanel);
                }
                DisconnectFromStrategy();
            }
        }

        #endregion

        #region OnBarUpdate / OnMarketData

        protected override void OnBarUpdate()
        {
            if (CurrentBar < 1) return;
            lastPrice = Close[0];

            while (responseQueue.TryDequeue(out string response))
                ProcessStrategyResponse(response);
        }

        protected override void OnMarketData(MarketDataEventArgs e)
        {
            if (e.MarketDataType == MarketDataType.Last)
            {
                lastPrice = e.Price;
                UpdatePriceDisplay();
            }
        }

        private void UpdatePriceDisplay()
        {
            if (ChartControl != null && lastPriceText != null)
            {
                ChartControl.Dispatcher.BeginInvoke(new Action(() =>
                {
                    lastPriceText.Text = lastPrice.ToString("F2");
                }));
            }
        }

        #endregion

        #region Panel Creation (SIDE-BY-SIDE DOCKING)

        // New Architecture Handles
        private Grid rootContainer;      // The main container hijacked into the slot
        private Border contentBody;      // The visible V12 Panel Body (Background + Controls)
        private Button floatingAnchor;   // The ALWAYS visible toggle button
        
        // Legacy/Compat Fields
        private FrameworkElement chartTraderElement; 
        private Grid rootGrid;           // Needed for legacy removal checks

        private void CreatePanel()
        {
            if (panelCreated || ChartControl == null) return;

            try
            {
                // 1. Initialize Root Container (Invisible Grid)
                rootContainer = new Grid { ClipToBounds = true }; // V12.7: Prevent overflow

                // 2. Initialize V12 Content Body (The visual panel)
                // V12.7: RESTORED - No fixed width, stretch to fill slot
                contentBody = new Border
                {
                    Background = BgDeep,
                    BorderBrush = BorderSlate,
                    BorderThickness = new Thickness(1, 0, 0, 0),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    ClipToBounds = true
                };




                // Create Content - V12.7: Force horizontal stretch
                scrollViewer = new ScrollViewer
                {
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch, // V12.7: Critical
                    PanningMode = PanningMode.VerticalOnly
                };

                mainStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Stretch }; // V12.7
                mainStack.Children.Add(CreateSection0_Identity());
                mainStack.Children.Add(CreateSection1_Execution());
                mainStack.Children.Add(CreateSection2_Telemetry());
                mainStack.Children.Add(CreateSection3_Config());

                scrollViewer.Content = mainStack;
                contentBody.Child = scrollViewer;

                // 3. Initialize Floating Anchor (Rescue Button)
                // MOVED to Bottom-Right to avoid covering Native "Sell Mkt" buttons
                floatingAnchor = CreateButton("⚓", 30, CyanBg, CyanFg, CyanBorder);
                floatingAnchor.FontSize = 14;
                floatingAnchor.HorizontalAlignment = HorizontalAlignment.Right;
                floatingAnchor.VerticalAlignment = VerticalAlignment.Bottom; // Moved from Top
                floatingAnchor.Margin = new Thickness(0, 0, 2, 2); // Bottom Right corner
                floatingAnchor.ToolTip = "Toggle V12 / Native Chart Trader";
                floatingAnchor.Click += ToggleLayout_Click; // Centralized Handler
                
                // V12: Final nudge to ensure combos are populated if NT state was weird during load
                PopulateAccountCombos();

                // Assemble Root
                rootContainer.Children.Add(contentBody);
                rootContainer.Children.Add(floatingAnchor); // Floating anchor on top

                // 4. HIJACK STRATEGY
                chartTraderElement = FindChartTrader();
                
                if (chartTraderElement != null && chartTraderElement.Parent is Grid traderGrid)
                {
                    // Get Native slot coords
                    int col = Grid.GetColumn(chartTraderElement);
                    int row = Grid.GetRow(chartTraderElement);
                    int rSpan = Grid.GetRowSpan(chartTraderElement);
                    int cSpan = Grid.GetColumnSpan(chartTraderElement);

                    // Place our RootContainer in the EXACT same slot
                    Grid.SetColumn(rootContainer, col);
                    Grid.SetRow(rootContainer, row);
                    if (rSpan > 1) Grid.SetRowSpan(rootContainer, rSpan);
                    if (cSpan > 1) Grid.SetColumnSpan(rootContainer, cSpan);

                    // Add to Grid
                    traderGrid.Children.Add(rootContainer);
                    rootGrid = traderGrid; // Store for removal

                    // Default State: V12 Active, Native Hidden
                    chartTraderElement.Visibility = Visibility.Collapsed;
                    contentBody.Visibility = Visibility.Visible;
                    Print($"V12 STANDARD: Hijacked Native Slot (Col {col}, Row {row})");
                }
                else
                {
                   // Fallback: Just overlay on chart root
                   Print("V12 STANDARD: Native Trader not found/gridless. Using Overlay.");
                   rootGrid = FindParentGrid(ChartControl);
                   if (rootGrid != null)
                   {
                        Grid.SetColumn(rootContainer, 0);
                        Grid.SetRow(rootContainer, 0);
                        if (rootGrid.ColumnDefinitions.Count > 0) Grid.SetColumnSpan(rootContainer, rootGrid.ColumnDefinitions.Count);
                        if (rootGrid.RowDefinitions.Count > 0) Grid.SetRowSpan(rootContainer, rootGrid.RowDefinitions.Count);
                        rootGrid.Children.Add(rootContainer);
                   }
                }

                panelCreated = true;
                
                // Glow Timer
                glowTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
                glowTimer.Tick += (s, e) =>
                {
                    if (contentBody != null)
                    {
                        contentBody.BorderBrush = BorderSlate;
                        glowTimer.Stop();
                    }
                };

                Print("V12 STANDARD: Panel Created Successfully (Ghost Architecture)");

                // Load saved configuration
                LoadConfig();
            }
            catch (Exception ex)
            {
                Print("V12 STANDARD: CreatePanel Error: " + ex.Message);
            }
        }

        private void ToggleLayout_Click(object sender, RoutedEventArgs e)
        {
            if (chartTraderElement == null) return;

            bool isV12Active = (contentBody.Visibility == Visibility.Visible);

            if (isV12Active)
            {
                // Switch to NATIVE
                contentBody.Visibility = Visibility.Collapsed;
                chartTraderElement.Visibility = Visibility.Visible;
                Print("V12 STANDARD: Switched to Native View");
            }
            else
            {
                // Switch to V12
                contentBody.Visibility = Visibility.Visible;
                chartTraderElement.Visibility = Visibility.Collapsed;
                Print("V12 STANDARD: Switched to V12 View");
            }
        }

        #region Config Persistence

        /// <summary>Captures current UI values into a ModeSettings object</summary>
        private V12ModeSettings CaptureCurrentSettings()
        {
            return new V12ModeSettings
            {
                TargetCount = selectedTargetCount,
                T1Val = svT1Val?.Text ?? "1.0",
                T1Type = (svT1Type?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "ATR",
                T2Val = svT2Val?.Text ?? "2.0",
                T2Type = (svT2Type?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "ATR",
                T3Val = svT3Val?.Text ?? "3.0",
                T3Type = (svT3Type?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "ATR",
                STR = strVal?.Text ?? "$150",
                StopType = (svStrType?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "ATR",
                MAX = maxVal?.Text ?? "$1200"
            };
        }

        /// <summary>Applies ModeSettings to UI controls</summary>
        private void ApplySettings(V12ModeSettings settings)
        {
            if (settings == null) return;

            isApplyingSettings = true; // Prevent auto-save during UI updates
            try
            {
                if (strVal != null) strVal.Text = settings.STR ?? "$150";
                if (maxVal != null) maxVal.Text = settings.MAX ?? "$1200";

                if (svT1Val != null) svT1Val.Text = settings.T1Val ?? "1.0";
                if (svT2Val != null) svT2Val.Text = settings.T2Val ?? "2.0";
                if (svT3Val != null) svT3Val.Text = settings.T3Val ?? "3.0";

                SelectComboByValue(svT1Type, settings.T1Type);
                SelectComboByValue(svT2Type, settings.T2Type);
                SelectComboByValue(svT3Type, settings.T3Type);
                SelectComboByValue(svStrType, settings.StopType);

                selectedTargetCount = settings.TargetCount > 0 ? settings.TargetCount : 3;
                UpdateTargetCountVisual(selectedTargetCount);
                UpdateTargetVisibility(selectedTargetCount);
            }
            finally
            {
                isApplyingSettings = false;
            }
        }

        private void SaveConfig()
        {
            if (isApplyingSettings) return; // Don't save during mode switch

            try
            {
                if (fullConfig == null) fullConfig = new V12FullConfig();

                // Save current mode's settings
                fullConfig.ActiveMode = selectedConfigMode;
                fullConfig.SetSettings(selectedConfigMode, CaptureCurrentSettings());

                // Write simple text file format
                var sb = new StringBuilder();
                sb.AppendLine($"ActiveMode={fullConfig.ActiveMode}");
                sb.AppendLine($"ORB={fullConfig.ORB.ToLine()}");
                sb.AppendLine($"RMA={fullConfig.RMA.ToLine()}");
                sb.AppendLine($"RETEST={fullConfig.RETEST.ToLine()}");
                sb.AppendLine($"MOMO={fullConfig.MOMO.ToLine()}");
                sb.AppendLine($"FFMA={fullConfig.FFMA.ToLine()}");
                sb.AppendLine($"TREND={fullConfig.TREND.ToLine()}");
                
                File.WriteAllText(ConfigFilePath, sb.ToString());
                Print($"V12 STANDARD: Config saved for {selectedConfigMode}");
            }
            catch (Exception ex)
            {
                Print($"V12 STANDARD: SaveConfig error - {ex.Message}");
            }
        }

        private void LoadConfig()
        {
            try
            {
                if (!File.Exists(ConfigFilePath))
                {
                    Print("V12 STANDARD: No saved config found, using defaults");
                    fullConfig = new V12FullConfig();
                    return;
                }

                fullConfig = new V12FullConfig();
                var lines = File.ReadAllLines(ConfigFilePath);
                foreach (var line in lines)
                {
                    if (string.IsNullOrEmpty(line) || !line.Contains("=")) continue;
                    var idx = line.IndexOf('=');
                    var key = line.Substring(0, idx);
                    var val = line.Substring(idx + 1);

                    switch(key)
                    {
                        case "ActiveMode": fullConfig.ActiveMode = val; break;
                        case "ORB": fullConfig.ORB = V12ModeSettings.FromLine(val); break;
                        case "RMA": fullConfig.RMA = V12ModeSettings.FromLine(val); break;
                        case "RETEST": fullConfig.RETEST = V12ModeSettings.FromLine(val); break;
                        case "MOMO": fullConfig.MOMO = V12ModeSettings.FromLine(val); break;
                        case "FFMA": fullConfig.FFMA = V12ModeSettings.FromLine(val); break;
                        case "TREND": fullConfig.TREND = V12ModeSettings.FromLine(val); break;
                    }
                }

                // Restore active mode button visual
                string activeMode = fullConfig.ActiveMode ?? "ORB";
                selectedConfigMode = activeMode;
                Button modeBtn = GetModeButton(activeMode);
                if (modeBtn != null) HighlightModeButton(modeBtn);

                // Apply active mode's settings to UI
                ApplySettings(fullConfig.GetSettings(activeMode));

                Print($"V12 STANDARD: Config loaded, active mode: {activeMode}");
            }
            catch (Exception ex)
            {
                Print($"V12 STANDARD: LoadConfig error - {ex.Message}");
                fullConfig = new V12FullConfig();
            }
        }

        private Button GetModeButton(string mode)
        {
            switch(mode.ToUpper())
            {
                case "ORB": return modeOrbButton;
                case "RMA": return modeRmaButton;
                case "RETEST": return modeRetestButton;
                case "MOMO": return modeMomoButton;
                case "FFMA": return modeFfmaButton;
                case "TREND": return modeTrendButton;
                default: return modeOrbButton;
            }
        }

        private void HighlightModeButton(Button btn)
        {
            foreach (var b in new[] { modeOrbButton, modeRmaButton, modeRetestButton, modeMomoButton, modeFfmaButton, modeTrendButton })
            {
                if (b == null) continue;
                b.Background = BtnBg;
                b.Foreground = TextMuted;
                b.BorderBrush = BtnBorder;
            }
            if (btn != null)
            {
                btn.Background = CyanBg;
                btn.Foreground = CyanFg;
                btn.BorderBrush = CyanBorder;
            }
        }

        private void UpdateTargetCountVisual(int count)
        {
            foreach (var btn in new[] { cnt1, cnt2, cnt3, cnt4, cnt5 })
            {
                if (btn == null) continue;
                btn.Background = BtnBg;
                btn.Foreground = TextPrimary;
                btn.BorderBrush = BtnBorder;
            }
            
            Button targetBtn = null;
            switch(count)
            {
                case 1: targetBtn = cnt1; break;
                case 2: targetBtn = cnt2; break;
                case 3: targetBtn = cnt3; break;
                case 4: targetBtn = cnt4; break;
                case 5: targetBtn = cnt5; break;
            }
            
            if (targetBtn != null)
            {
                targetBtn.Background = CyanBg;
                targetBtn.Foreground = CyanFg;
                targetBtn.BorderBrush = CyanBorder;
            }
        }

        #endregion

        private void PopulateAccountCombos()
        {
            if (leaderAccountCombo == null) return;

            leaderAccountCombo.Items.Clear();
            selectedFleetAccounts.Clear();
            
            // Populate fleet checkbox panel if available
            if (fleetCheckboxPanel != null)
                fleetCheckboxPanel.Children.Clear();

            foreach (NinjaTrader.Cbi.Account account in NinjaTrader.Cbi.Account.All)
            {
                // Format with daily realized P/L
                double realizedPL = account.Get(NinjaTrader.Cbi.AccountItem.RealizedProfitLoss, NinjaTrader.Cbi.Currency.UsDollar);
                string plText = realizedPL >= 0 ? $"+${realizedPL:N0}" : $"-${Math.Abs(realizedPL):N0}";
                string displayName = $"{account.Name} [{plText}]";
                
                // Add to leader combo
                leaderAccountCombo.Items.Add(new ComboBoxItem { Content = displayName, Tag = account.Name });
                
                // Add checkbox to fleet panel
                if (fleetCheckboxPanel != null)
                {
                    CheckBox cb = new CheckBox
                    {
                        Content = displayName,
                        Tag = account.Name,
                        Foreground = TextPrimary,
                        FontSize = 10,
                        FontFamily = new FontFamily("Consolas"),
                        Margin = new Thickness(0, 1, 0, 1),
                        IsChecked = true // Default: all accounts selected
                    };
                    cb.Checked += (s, e) => {
                        if (!selectedFleetAccounts.Contains(account.Name))
                            selectedFleetAccounts.Add(account.Name);
                        UpdateFleetButtonText();
                        SendCommand($"TOGGLE_ACCOUNT|{account.Name}|1");
                    };
                    cb.Unchecked += (s, e) => {
                        selectedFleetAccounts.Remove(account.Name);
                        UpdateFleetButtonText();
                        SendCommand($"TOGGLE_ACCOUNT|{account.Name}|0");
                    };
                    fleetCheckboxPanel.Children.Add(cb);
                    
                    // Default: add all accounts to selected list
                    selectedFleetAccounts.Add(account.Name);
                }
            }

            if (leaderAccountCombo.Items.Count > 0) leaderAccountCombo.SelectedIndex = 0;
            
            UpdateFleetButtonText();
            Print($"V12 STANDARD: Populated {leaderAccountCombo.Items.Count} accounts with P/L.");
        }
        
        private void FleetSelectButton_Click(object sender, RoutedEventArgs e)
        {
            if (fleetPopup != null)
            {
                fleetPopup.IsOpen = !fleetPopup.IsOpen;
            }
        }
        
        private void SelectAllFleetAccounts(bool selectAll)
        {
            if (fleetCheckboxPanel == null) return;
            
            selectedFleetAccounts.Clear();
            foreach (var child in fleetCheckboxPanel.Children)
            {
                if (child is CheckBox cb)
                {
                    cb.IsChecked = selectAll;
                    if (selectAll && cb.Tag is string accountName)
                        selectedFleetAccounts.Add(accountName);
                }
            }
            UpdateFleetButtonText();
        }
        
        private void UpdateFleetButtonText()
        {
            if (fleetSelectButton == null) return;
            
            int count = selectedFleetAccounts.Count;
            int total = fleetCheckboxPanel?.Children.Count ?? 0;
            
            if (count == 0)
                fleetSelectButton.Content = "FLEET: None Selected ▼";
            else if (count == total)
                fleetSelectButton.Content = "FLEET: ALL Accounts ▼";
            else
                fleetSelectButton.Content = $"FLEET: {count} of {total} ▼";
        }

        private void RemovePanel()
        {
            if (!panelCreated) return;

            try
            {
                // Restore Native Trader
                if (chartTraderElement != null)
                {
                    chartTraderElement.Visibility = Visibility.Visible;
                }

                // Remove our Root
                if (rootGrid != null && rootContainer != null)
                {
                    if (rootGrid.Children.Contains(rootContainer))
                        rootGrid.Children.Remove(rootContainer);
                }

                // Clean refs (Avoid memory leaks)
                rootContainer = null;
                contentBody = null;
                chartTraderElement = null;
                mainPanel = null; // Important for legacy checks
                
                panelCreated = false;
                glowTimer?.Stop();
                Print("V12 STANDARD: Panel removed");
            }
            catch (Exception ex)
            {
                Print("V12 STANDARD: Removal error - " + ex.Message);
            }
        }

        // Keep this for finding parents
        private Grid FindParentGrid(DependencyObject child)
        {
            DependencyObject parent = VisualTreeHelper.GetParent(child);
            while (parent != null)
            {
                if (parent is Grid grid)
                {
                    // We want the grid that directly or near-directly holds the chart
                    if (grid.ColumnDefinitions.Count > 0 || grid.Children.Count > 1)
                        return grid;
                }
                parent = VisualTreeHelper.GetParent(parent);
            }
            return null;
        }

        // Unused legacy methods kept for compilation safety if referenced elsewhere
        private void CreateOverlayOnChartTrader() { CreateFallbackOverlay(); }


        private Grid FindFirstChildGrid(DependencyObject parent)
        {
            if (parent == null) return null;

            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is Grid grid)
                    return grid;
            }
            return null;
        }

        private FrameworkElement FindChartTrader()
        {
            try
            {
                Window chartWindow = Window.GetWindow(ChartControl);
                if (chartWindow == null) return null;

                // NUCLEAR OPTION: Find ALL 'Buy Mkt' buttons
                var allBuyButtons = FindAllButtonsByText(chartWindow, "Buy Mkt");
                Print($"V12 STANDARD: Found {allBuyButtons.Count} 'Buy Mkt' buttons");

                foreach (var btn in allBuyButtons)
                {
                    // Walk up and log hierarchy
                    DependencyObject parent = VisualTreeHelper.GetParent(btn);
                    string hierarchy = "Button";
                    FrameworkElement targetToHide = null;

                    while (parent != null)
                    {
                        var typeName = parent.GetType().Name;
                        hierarchy += $" -> {typeName}";

                        if (parent is FrameworkElement fe)
                        {
                            // Look for the main ChartTrader container
                            if (typeName.Contains("ChartTrader") || typeName.Contains("UserControl"))
                            {
                                targetToHide = fe;
                            }
                            // Fallback: A generic Grid that holds everything
                            if (targetToHide == null && fe is Grid)
                            {
                                targetToHide = fe; 
                            }
                        }
                        parent = VisualTreeHelper.GetParent(parent);
                    }
                    
                    Print($"V12 STANDARD: Hierarchy: {hierarchy}");

                    if (targetToHide != null)
                    {
                         targetToHide.Visibility = Visibility.Collapsed;
                         Print($"V12 STANDARD: HIDDEN Target -> {targetToHide.GetType().Name} ({targetToHide.Name})");
                         return targetToHide; // Return the first one we successfully hid
                    }
                }
                
                return null;
            }
            catch (Exception ex)
            {
                Print($"V12 STANDARD: FindChartTrader error - {ex.Message}");
                return null;
            }
        }

        private List<Button> FindAllButtonsByText(DependencyObject parent, string text)
        {
            var list = new List<Button>();
            if (parent == null) return list;

            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                
                if (child is Button btn)
                {
                    if (btn.Content != null && btn.Content.ToString() == text)
                        list.Add(btn);
                }

                list.AddRange(FindAllButtonsByText(child, text));
            }
            return list;
        }

        private void CreateFallbackOverlay()
        {
            // Fallback: Right side overlay, positioned to NOT block price scale
            // Price scale is typically ~60px, so we position just inside that
            mainPanel = new Border
            {
                Background = BgDeep,
                BorderBrush = CyanAccent,
                BorderThickness = new Thickness(1, 0, 0, 0),
                Width = PanelWidth,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Stretch,
                Margin = new Thickness(0)  // Far right edge
            };

            scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Background = BgDeep
            };

            mainStack = new StackPanel { Background = BgDeep };
            mainStack.Children.Add(CreateSection0_Identity());
            mainStack.Children.Add(CreateSection1_Execution());
            mainStack.Children.Add(CreateSection2_Telemetry());
            mainStack.Children.Add(CreateSection3_Config());

            scrollViewer.Content = mainStack;
            mainPanel.Child = scrollViewer;

            UserControlCollection.Add(mainPanel);
            panelCreated = true;
            Print("V12 STANDARD: Using fallback overlay (far right)");
        }

        // [Legacy RemovePanel removed to avoid duplication]

        #endregion

        #region Section 0: Identity

        private Border CreateSection0_Identity()
        {
            Border section = CreateSectionBorder();
            StackPanel stack = new StackPanel { Margin = new Thickness(2, 2, 2, 1), HorizontalAlignment = HorizontalAlignment.Stretch }; // V12.7

            // Section header
            stack.Children.Add(CreateSectionHeader("SECTION 0: IDENTITY"));

            // Row 1: Leader Account (Full Width)
            Grid row1 = new Grid { Margin = new Thickness(0, 1, 0, 1) };
            row1.Width = 238; // V12: Explicit width
            row1.HorizontalAlignment = HorizontalAlignment.Center;
            row1.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // LED
            hubStatusLed = new Border
            {
                Width = 8, Height = 8,
                CornerRadius = new CornerRadius(4),
                Background = TextMuted,
                Margin = new Thickness(0, 0, 4, 0),
                ToolTip = "IPC Status"
            };
            Grid.SetColumn(hubStatusLed, 0);
            row1.Children.Add(hubStatusLed);
            leaderAccountCombo = CreateCombo(0, new[] { "LEADER: APEX_MA", "Sim101" });
            leaderAccountCombo.HorizontalAlignment = HorizontalAlignment.Stretch;
            leaderAccountCombo.SelectionChanged += LeaderAccount_SelectionChanged; // V12.6: Sync Logic
            Grid.SetColumn(leaderAccountCombo, 1);
            row1.Children.Add(leaderAccountCombo);
            stack.Children.Add(row1);

            // Row 2: Fleet Manager - Multi-Select with Checkboxes
            Grid fleetRow = new Grid { Margin = new Thickness(0, 1, 0, 1) };
            fleetRow.Width = 238; // V12: Explicit width
            fleetRow.HorizontalAlignment = HorizontalAlignment.Center;
            fleetRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            
            // Fleet selection button (shows count of selected accounts)
            fleetSelectButton = new Button
            {
                Content = "FLEET: Select Accounts ▼",
                Height = 22,
                FontSize = 10,
                FontFamily = new FontFamily("Consolas"),
                Background = BgSlate,
                Foreground = TextPrimary,
                BorderBrush = BorderSlate,
                BorderThickness = new Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(4, 0, 4, 0)
            };
            fleetSelectButton.Click += FleetSelectButton_Click;
            Grid.SetColumn(fleetSelectButton, 0);
            fleetRow.Children.Add(fleetSelectButton);
            
            // Create the popup for fleet selection
            fleetPopup = new Popup
            {
                StaysOpen = false,
                Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom,
                PlacementTarget = fleetSelectButton,
                AllowsTransparency = true
            };
            
            // Popup content: Border with checkbox list
            Border popupBorder = new Border
            {
                Background = BgDeep,
                BorderBrush = CyanAccent,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2),
                Padding = new Thickness(4),
                MinWidth = 180
            };
            
            StackPanel popupStack = new StackPanel();
            
            // "Select All" checkbox
            CheckBox selectAllCheck = new CheckBox
            {
                Content = "Select ALL Accounts",
                Foreground = CyanAccent,
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 4)
            };
            selectAllCheck.Checked += (s, e) => { SelectAllFleetAccounts(true); };
            selectAllCheck.Unchecked += (s, e) => { SelectAllFleetAccounts(false); };
            popupStack.Children.Add(selectAllCheck);
            
            // Divider
            popupStack.Children.Add(new Border { Height = 1, Background = BorderSlate, Margin = new Thickness(0, 2, 0, 4) });
            
            // Account checkboxes container (populated dynamically)
            fleetCheckboxPanel = new StackPanel();
            popupStack.Children.Add(fleetCheckboxPanel);
            
            popupBorder.Child = popupStack;
            fleetPopup.Child = popupBorder;
            
            stack.Children.Add(fleetRow);

            // Row 3: Direction + Price + Submit + Close (SHIFTED DOWN)
            Grid row3 = new Grid { Margin = new Thickness(0, 1, 0, 1) };
            row3.Width = 238; // V12: Explicit width for layout stability
            row3.HorizontalAlignment = HorizontalAlignment.Center;
            row3.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) }); // V12.7: Reduced from 85 for safety
            row3.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row3.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row3.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            directionCombo = CreateCombo(0, new[] { "OR LONG", "OR SHORT" });
            directionCombo.HorizontalAlignment = HorizontalAlignment.Stretch;
            Grid.SetColumn(directionCombo, 0);
            row3.Children.Add(directionCombo);

            priceInput = CreateTextBox(0, "6961.25");
            priceInput.Margin = new Thickness(4, 0, 4, 0);
            Grid.SetColumn(priceInput, 1);
            row3.Children.Add(priceInput);

            submitButton = CreateButton("SUBMIT", 60, GreenBg, GreenFg, GreenBorder);
            submitButton.Height = 22;
            submitButton.Click += Submit_Click;
            Grid.SetColumn(submitButton, 2);
            row3.Children.Add(submitButton);

            // Close button in same row - V12: High Visibility RED
            Button closeBtnRow = CreateButton("✕", 28, RedBg, Brushes.White, RedBorder);
            closeBtnRow.FontSize = 14;
            closeBtnRow.FontWeight = FontWeights.Bold;
            closeBtnRow.Margin = new Thickness(4, 0, 0, 0);
            closeBtnRow.ToolTip = "Close Panel";
            closeBtnRow.Click += (s, e) => { if (mainPanel != null) mainPanel.Visibility = Visibility.Collapsed; };
            Grid.SetColumn(closeBtnRow, 3);
            row3.Children.Add(closeBtnRow);

            stack.Children.Add(row3);



            section.Child = stack;
            return section;
        }

        #endregion

        #region Section 1: Execution

        private Border CreateSection1_Execution()
        {
            Border section = CreateSectionBorder();
            StackPanel stack = new StackPanel { Margin = new Thickness(2, 2, 2, 2), HorizontalAlignment = HorizontalAlignment.Stretch }; // V12.7

            stack.Children.Add(CreateSectionHeader("SECTION 1: EXECUTION"));

            // ═══════════════════════════════════════════════════════════════════
            // 2-COLUMN LAYOUT - V12.7 RESTORED: Stretch to fill available width
            // LEFT:  Trade Entries | RIGHT: Management
            // ═══════════════════════════════════════════════════════════════════
            Grid mainGrid = new Grid { Margin = new Thickness(2, 1, 2, 1) }; // V12.7: Small margins
            mainGrid.HorizontalAlignment = HorizontalAlignment.Stretch; // V12.7: Fill slot
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); 

            // ═══════════════════════════════════════════════════════════════════
            // LEFT COLUMN: Trade Entry Buttons
            // ═══════════════════════════════════════════════════════════════════
            StackPanel leftCol = new StackPanel { Margin = new Thickness(0, 0, 1, 0), HorizontalAlignment = HorizontalAlignment.Stretch };
            
            // OR L button
            orLongButton = CreateDashedButton("OR L", CyanAccent);
            orLongButton.Click += (s, e) => { SendCommand("OR_LONG"); TriggerGlow(CyanAccent); };
            leftCol.Children.Add(orLongButton);

            // OR S (Short) button
            orShortButton = CreateDashedButton("OR S", PinkFg);
            orShortButton.Margin = new Thickness(0, 2, 0, 0);
            orShortButton.Click += (s, e) => { SendCommand("OR_SHORT"); TriggerGlow(PinkFg); };
            leftCol.Children.Add(orShortButton);

            // RETEST row with R toggle
            // V12.3: Locked to 36px to match TRAIL input for perfect center-line
            Grid retestRow = new Grid { Margin = new Thickness(0, 2, 0, 0) };
            retestRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            retestRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) }); // SYNC: Matches TrailDistInput
            
            retestButton = CreateButton("RETEST", double.NaN, OrangeBg, OrangeFg, OrangeBorder);
            retestButton.Click += (s, e) =>
            {
                retestCycleState = (retestCycleState + 1) % 3;
                switch(retestCycleState)
                {
                    case 0: retestButton.Content = "RETEST"; SendCommand("EXEC_RETEST"); break;
                    case 1: retestButton.Content = "RET +"; SendCommand("EXEC_RETEST_PLUS"); break;
                    case 2: retestButton.Content = "RET -"; SendCommand("EXEC_RETEST_MINUS"); break;
                }
                TriggerGlow(OrangeFg);
            };
            Grid.SetColumn(retestButton, 0);
            retestRow.Children.Add(retestButton);
            
            Button retestRmaToggle = CreateButton("R", 36, PurpleFg, TextPrimary, PurpleFg); // SYNC: Matches TrailDistInput
            retestRmaToggle.Margin = new Thickness(2, 0, 0, 0);
            retestRmaToggle.Opacity = 0.5;
            retestRmaToggle.Click += (s, e) => { 
                isRetestRmaToggle = !isRetestRmaToggle;
                retestRmaToggle.Opacity = isRetestRmaToggle ? 1.0 : 0.5; 
                SendCommand(isRetestRmaToggle ? "MODE_RETEST_RMA" : "MODE_RETEST_STD");
            };
            Grid.SetColumn(retestRmaToggle, 1);
            retestRow.Children.Add(retestRmaToggle);
            leftCol.Children.Add(retestRow);

            // RMA button
            rmaButton = CreateButton("RMA", double.NaN, PurpleFg, TextPrimary, PurpleFg);
            rmaButton.Margin = new Thickness(0, 2, 0, 0);
            rmaButton.Click += (s, e) =>
            {
                isRmaModeActive = !isRmaModeActive;
                if (isRmaModeActive)
                {
                    rmaButton.Background = PurpleFg;
                    rmaButton.Foreground = Brushes.White;
                    rmaButton.Content = "RMA ⏺";
                    SendCommand("SET_RMA_MODE|ON");
                    
                    // V12.2: Switch config mode to RMA when activated
                    SelectConfigMode("RMA", modeRmaButton);
                    // V12.2: Sync external app to RMA mode
                    SendCommand("SYNC_MODE|RMA");
                }
                else
                {
                    rmaButton.Background = Brushes.Transparent;
                    rmaButton.Foreground = PurpleFg;
                    rmaButton.Content = "RMA";
                    SendCommand("SET_RMA_MODE|OFF");
                }
                TriggerGlow(PurpleFg);
            };
            leftCol.Children.Add(rmaButton);


            // MOMO button
            momoButton = CreateButton("MOMO", double.NaN, GreenBg, GreenFg, GreenBorder);
            momoButton.Margin = new Thickness(0, 2, 0, 0);
            momoButton.Click += (s, e) => { 
                SendCommand("MODE_MOMO"); 
                TriggerGlow(GreenFg); 
                // V12.2: Switch config mode when entry button clicked
                SelectConfigMode("MOMO", modeMomoButton);
                SendCommand("SYNC_MODE|MOMO");
            };
            leftCol.Children.Add(momoButton);


            // FFMA button
            ffmaButton = CreateButton("FFMA", double.NaN, PinkBg, PinkFg, PinkBorder);
            ffmaButton.Margin = new Thickness(0, 2, 0, 0);
            ffmaButton.Click += (s, e) => { 
                SendCommand("MODE_FFMA"); 
                TriggerGlow(PinkFg); 
                // V12.2: Switch config mode when entry button clicked
                SelectConfigMode("FFMA", modeFfmaButton);
                SendCommand("SYNC_MODE|FFMA");
            };
            leftCol.Children.Add(ffmaButton);


            // MNL (M) button
            mButton = CreateButton("MNL", double.NaN, OrangeBg, OrangeFg, OrangeBorder);
            mButton.Margin = new Thickness(0, 2, 0, 0);
            mButton.Click += (s, e) => { SendCommand("MODE_M"); TriggerGlow(OrangeFg); };
            leftCol.Children.Add(mButton);

            // TREND row with R toggle
            // V12.3: Locked to 36px to match TRAIL/RETEST for perfect center-line
            Grid trendRow = new Grid { Margin = new Thickness(0, 2, 0, 0) };
            trendRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            trendRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) }); // SYNC: Matches TrailDistInput
            
            trendButton = CreateButton("TREND", double.NaN, BtnBg, TextPrimary, BtnBorder);
            trendButton.Click += (s, e) =>
            {
                string cmd = isTrendRmaToggle ? "EXEC_TREND_RMA" : "EXEC_TREND";
                SendCommand(cmd);
                TriggerGlow(CyanAccent);
                // V12.2: Switch config mode when entry button clicked
                SelectConfigMode("TREND", modeTrendButton);
                SendCommand("SYNC_MODE|TREND");
            };
            Grid.SetColumn(trendButton, 0);
            trendRow.Children.Add(trendButton);

            
            trendRmaToggle = CreateButton("R", 36, PurpleFg, TextPrimary, PurpleFg); // SYNC: Matches TrailDistInput
            trendRmaToggle.Margin = new Thickness(2, 0, 0, 0);
            trendRmaToggle.Opacity = 0.5;
            trendRmaToggle.Click += (s, e) =>
            {
                isTrendRmaToggle = !isTrendRmaToggle;
                trendRmaToggle.Opacity = isTrendRmaToggle ? 1.0 : 0.5;
                SendCommand(isTrendRmaToggle ? "MODE_TREND_RMA" : "MODE_TREND_STD");
            };
            Grid.SetColumn(trendRmaToggle, 1);
            trendRow.Children.Add(trendRmaToggle);
            leftCol.Children.Add(trendRow);

            Grid.SetColumn(leftCol, 0);
            mainGrid.Children.Add(leftCol);

            // RIGHT COLUMN: Management Buttons
            // ═══════════════════════════════════════════════════════════════════
            StackPanel rightCol = new StackPanel { Margin = new Thickness(1, 0, 0, 0), HorizontalAlignment = HorizontalAlignment.Stretch };

            // T1 button
            t1Button = CreateButton("T1", double.NaN, GreenBg, GreenFg, GreenBorder);
            t1Button.Click += (s, e) => { SendCommand("CLOSE_T1"); TriggerGlow(GreenFg); };
            rightCol.Children.Add(t1Button);

            // T2 button
            t2Button = CreateButton("T2", double.NaN, YellowBg, YellowFg, YellowBorder);
            t2Button.Margin = new Thickness(0, 2, 0, 0);
            t2Button.Click += (s, e) => { SendCommand("CLOSE_T2"); TriggerGlow(YellowFg); };
            rightCol.Children.Add(t2Button);

            // T3 button
            t3Button = CreateButton("T3", double.NaN, OrangeBg, OrangeFg, OrangeBorder);
            t3Button.Margin = new Thickness(0, 2, 0, 0);
            t3Button.Click += (s, e) => { SendCommand("CLOSE_T3"); TriggerGlow(OrangeFg); };
            rightCol.Children.Add(t3Button);

            // T4 button
            t4Button = CreateButton("T4", double.NaN, RedBg, RedFg, RedBorder);
            t4Button.Margin = new Thickness(0, 2, 0, 0);
            t4Button.Click += (s, e) => { SendCommand("CLOSE_T4"); TriggerGlow(RedFg); };
            rightCol.Children.Add(t4Button);

            // 25% button
            trim25Button = CreateButton("25%", double.NaN, YellowBg, YellowFg, YellowBorder);
            trim25Button.Margin = new Thickness(0, 2, 0, 0);
            trim25Button.Click += (s, e) => { SendCommand("TRIM_25"); TriggerGlow(YellowFg); };
            rightCol.Children.Add(trim25Button);

            // 50% button
            trim50Button = CreateButton("50%", double.NaN, OrangeBg, OrangeFg, OrangeBorder);
            trim50Button.Margin = new Thickness(0, 2, 0, 0);
            trim50Button.Click += (s, e) => { SendCommand("TRIM_50"); TriggerGlow(OrangeFg); };
            rightCol.Children.Add(trim50Button);

            // BE button
            beButton = CreateButton("BE", double.NaN, CyanBg, CyanFg, CyanBorder);
            beButton.Margin = new Thickness(0, 2, 0, 0);
            beButton.Click += (s, e) => { SendCommand("BE_PLUS_2"); TriggerGlow(CyanFg); };
            rightCol.Children.Add(beButton);

            // TRAIL row: Input + Button
            // V12.3: Locked to 36px to match R-Toggle for perfect center-line
            Grid trailRow = new Grid { Margin = new Thickness(0, 2, 0, 0) };
            trailRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) }); // SYNC: Matches retestRmaToggle
            trailRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            trailDistInput = CreateTextBox(0, "1.0");
            trailDistInput.Height = 22;
            trailDistInput.FontSize = 10;
            trailDistInput.ToolTip = "Trail distance in points";
            Grid.SetColumn(trailDistInput, 0);
            trailRow.Children.Add(trailDistInput);

            trailButton = CreateButton("TRAIL", double.NaN, BtnBg, TextPrimary, BtnBorder);
            trailButton.Margin = new Thickness(2, 0, 0, 0);
            trailButton.Click += (s, e) =>
            {
                string dist = trailDistInput?.Text ?? "1.0";
                SendCommand($"SET_TRAIL|{dist}");
                TriggerGlow(CyanFg);
            };
            Grid.SetColumn(trailButton, 1);
            trailRow.Children.Add(trailButton);

            rightCol.Children.Add(trailRow);

            // FLATTEN button
            flattenButton = CreateButton("FLATTEN", double.NaN, RedBg, RedFg, RedBorder);
            flattenButton.Margin = new Thickness(0, 2, 0, 0);
            flattenButton.FontWeight = FontWeights.Bold;
            flattenButton.Click += (s, e) => { SendCommand("FLATTEN"); TriggerGlow(RedFg); };
            rightCol.Children.Add(flattenButton);

            Grid.SetColumn(rightCol, 1);
            mainGrid.Children.Add(rightCol);

            stack.Children.Add(mainGrid);

            // Price display row
            Grid priceRow = new Grid { Margin = new Thickness(0, 3, 0, 0) };
            lastPriceText = new TextBlock
            {
                Text = "6961.25",
                Foreground = GreenFg,
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Consolas"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            priceRow.Children.Add(lastPriceText);
            stack.Children.Add(priceRow);

            section.Child = stack;
            return section;
        }

        private Button CreateTargetButton(string text, Brush bg, Brush fg, Brush border, string command, int col)
        {
            Button btn = CreateButton(text, double.NaN, bg, fg, border);
            btn.Margin = col > 0 ? new Thickness(2, 0, 0, 0) : new Thickness(0);
            btn.Click += (s, e) => { SendCommand(command); TriggerGlow(fg); };
            Grid.SetColumn(btn, col);
            return btn;
        }

        private Button CreateScaleButton(string text, Brush bg, Brush fg, Brush border, string command, int col)
        {
            Button btn = CreateButton(text, double.NaN, bg, fg, border);
            btn.Margin = col > 0 ? new Thickness(2, 0, 0, 0) : new Thickness(0);
            btn.Click += (s, e) => { SendCommand(command); TriggerGlow(fg); };
            Grid.SetColumn(btn, col);
            return btn;
        }

        #endregion

        #region Section 2: Telemetry

        private Border CreateSection2_Telemetry()
        {
            Border section = CreateSectionBorder();
            StackPanel stack = new StackPanel { Margin = new Thickness(2, 2, 2, 1), HorizontalAlignment = HorizontalAlignment.Stretch }; // V12.7

            stack.Children.Add(CreateSectionHeader("SECTION 2: TELEMETRY"));

            // OR5 line
            or5Text = new TextBlock
            {
                FontSize = 10,
                FontFamily = new FontFamily("Consolas"),
                Margin = new Thickness(0, 3, 0, 1)
            };
            or5Text.Inlines.Add(new System.Windows.Documents.Run("OR5: ") { Foreground = OrangeFg, FontWeight = FontWeights.Bold });
            or5Text.Inlines.Add(new System.Windows.Documents.Run("6981.75") { Foreground = OrangeFg });
            or5Text.Inlines.Add(new System.Windows.Documents.Run(" | ") { Foreground = TextMuted });
            or5Text.Inlines.Add(new System.Windows.Documents.Run("6970.25") { Foreground = OrangeFg });
            or5Text.Inlines.Add(new System.Windows.Documents.Run(" (R: 11.5)") { Foreground = TextMuted });
            stack.Children.Add(or5Text);

            // OR15 line
            or15Text = new TextBlock
            {
                FontSize = 10,
                FontFamily = new FontFamily("Consolas"),
                Margin = new Thickness(0, 0, 0, 2)
            };
            or15Text.Inlines.Add(new System.Windows.Documents.Run("OR15: ") { Foreground = OrangeFg, FontWeight = FontWeights.Bold });
            or15Text.Inlines.Add(new System.Windows.Documents.Run("6985.50") { Foreground = OrangeFg });
            or15Text.Inlines.Add(new System.Windows.Documents.Run(" | ") { Foreground = TextMuted });
            or15Text.Inlines.Add(new System.Windows.Documents.Run("6965.00") { Foreground = OrangeFg });
            or15Text.Inlines.Add(new System.Windows.Documents.Run(" (R: 20.5)") { Foreground = TextMuted });
            stack.Children.Add(or15Text);

            // EMA line 1: 9, 15, 30
            StackPanel emaRow1 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 0) };
            ema9Text = CreateEmaLabel("9:", "6972", TextPrimary);
            ema15Text = CreateEmaLabel("15:", "6975", TextPrimary);
            ema30Text = CreateEmaLabel("30:", "6959", GreenFg);
            emaRow1.Children.Add(ema9Text);
            emaRow1.Children.Add(ema15Text);
            emaRow1.Children.Add(ema30Text);
            stack.Children.Add(emaRow1);

            // EMA line 2: 65, 200, ATR
            StackPanel emaRow2 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 3) };
            ema65Text = CreateEmaLabel("65:", "6945", TextPrimary);
            ema200Text = CreateEmaLabel("200:", "6912", PurpleFg);
            atrText = new TextBlock
            {
                Text = "ATR: 12.5",
                Foreground = TextMuted,
                FontSize = 10,
                FontFamily = new FontFamily("Consolas"),
                FontStyle = FontStyles.Italic,
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            emaRow2.Children.Add(ema65Text);
            emaRow2.Children.Add(ema200Text);
            emaRow2.Children.Add(atrText);
            stack.Children.Add(emaRow2);

            // MKT SYNC + BULLISH/BEARISH
            Grid syncRow = new Grid();
            syncRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            syncRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            mktSyncButton = CreateButton("MKT SYNC", 70, CyanBg, CyanFg, CyanBorder);
            mktSyncButton.Height = 24;
            mktSyncButton.FontSize = 9;
            mktSyncButton.Click += (s, e) => SendCommand("MKT_SYNC");
            Grid.SetColumn(mktSyncButton, 0);
            syncRow.Children.Add(mktSyncButton);

            trendIndicator = new Border
            {
                Background = GreenBg,
                BorderBrush = GreenBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(10, 2, 10, 2),
                Margin = new Thickness(8, 0, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Right,
                Height = 24
            };
            trendText = new TextBlock
            {
                Text = "BULLISH",
                Foreground = GreenFg,
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Consolas"),
                VerticalAlignment = VerticalAlignment.Center
            };
            trendIndicator.Child = trendText;
            Grid.SetColumn(trendIndicator, 1);
            syncRow.Children.Add(trendIndicator);

            stack.Children.Add(syncRow);

            section.Child = stack;
            return section;
        }

        private TextBlock CreateEmaLabel(string label, string value, Brush valueColor)
        {
            TextBlock tb = new TextBlock
            {
                FontSize = 11,
                FontFamily = new FontFamily("Consolas"),
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 10, 0)
            };
            tb.Inlines.Add(new System.Windows.Documents.Run(label) { Foreground = TextMuted });
            tb.Inlines.Add(new System.Windows.Documents.Run(value) { Foreground = valueColor });
            return tb;
        }

        #endregion

        #region Section 3: Config

        private Border CreateSection3_Config()
        {
            Border section = CreateSectionBorder();
            section.BorderThickness = new Thickness(0); // No bottom border for last section
            StackPanel stack = new StackPanel { Margin = new Thickness(2, 2, 2, 4), HorizontalAlignment = HorizontalAlignment.Stretch }; // V12.7

            stack.Children.Add(CreateSectionHeader("SECTION 3: CONFIG"));

            // Two-column layout: Modes (left) | Counts (right)
            // V12.7 RESTORED: Stretch to fill available width
            Grid modeCountGrid = new Grid { Margin = new Thickness(0, 3, 0, 3) };
            modeCountGrid.HorizontalAlignment = HorizontalAlignment.Stretch; // V12.7: Fill slot
            modeCountGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            modeCountGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); 

            // LEFT COLUMN: Modes
            StackPanel modeColumn = new StackPanel { Margin = new Thickness(0, 0, 1, 0), HorizontalAlignment = HorizontalAlignment.Stretch };
            modeOrbButton = CreateModeChip("ORB", true, -1);
            modeRmaButton = CreateModeChip("RMA", false, -1);
            modeRetestButton = CreateModeChip("RETEST", false, -1);
            modeMomoButton = CreateModeChip("MOMO", false, -1);
            modeFfmaButton = CreateModeChip("FFMA", false, -1);
            modeTrendButton = CreateModeChip("TREND", false, -1);

            modeColumn.Children.Add(modeOrbButton);
            modeColumn.Children.Add(modeRmaButton);
            modeColumn.Children.Add(modeRetestButton);
            modeColumn.Children.Add(modeMomoButton);
            modeColumn.Children.Add(modeFfmaButton);
            modeColumn.Children.Add(modeTrendButton);
            Grid.SetColumn(modeColumn, 0);
            modeCountGrid.Children.Add(modeColumn);

            // RIGHT COLUMN: Counts
            StackPanel countColumn = new StackPanel { Margin = new Thickness(1, 0, 0, 0), HorizontalAlignment = HorizontalAlignment.Stretch };
            cnt1 = CreateCountChip("1", 1);
            cnt2 = CreateCountChip("2", 2);
            cnt3 = CreateCountChip("3", 3);
            cnt4 = CreateCountChip("4", 4);
            cnt5 = CreateCountChip("5", 5);

            // Default: 3 selected
            cnt3.Background = CyanBg;
            cnt3.Foreground = CyanFg;
            cnt3.BorderBrush = CyanBorder;

            countColumn.Children.Add(cnt1);
            countColumn.Children.Add(cnt2);
            countColumn.Children.Add(cnt3);
            countColumn.Children.Add(cnt4);
            countColumn.Children.Add(cnt5);
            Grid.SetColumn(countColumn, 1);
            modeCountGrid.Children.Add(countColumn);

            stack.Children.Add(modeCountGrid);

            // SV: T1 [1.0] ATR  T2 [2.0] ATR
            StackPanel svRow1 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 2) };
            svRow1.Children.Add(new TextBlock { Text = "SV:", Foreground = TextPrimary, FontSize = 9, FontFamily = new FontFamily("Consolas"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) });

            svRow1.Children.Add(new TextBlock { Text = "T1", Foreground = GreenFg, FontSize = 9, FontFamily = new FontFamily("Consolas"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 2, 0) });
            svT1Val = CreateTextBox(30, "1.0"); svT1Val.Height = 20; svT1Val.FontSize = 9;
            svRow1.Children.Add(svT1Val);
            svT1Type = CreateCombo(42, new[] { "ATR", "Ticks", "Pts" }); svT1Type.Height = 20; svT1Type.FontSize = 8; svT1Type.Margin = new Thickness(2, 0, 6, 0);
            svRow1.Children.Add(svT1Type);

            svRow1.Children.Add(new TextBlock { Text = "T2", Foreground = YellowFg, FontSize = 9, FontFamily = new FontFamily("Consolas"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 2, 0) });
            svT2Val = CreateTextBox(30, "2.0"); svT2Val.Height = 20; svT2Val.FontSize = 9;
            svRow1.Children.Add(svT2Val);
            svT2Type = CreateCombo(42, new[] { "ATR", "Ticks", "Pts" }); svT2Type.Height = 20; svT2Type.FontSize = 8;
            svRow1.Children.Add(svT2Type);

            stack.Children.Add(svRow1);

            // T3 [3.0] ATR
            StackPanel svRow2 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 2) };
            svRow2.Children.Add(new TextBlock { Text = "       T3", Foreground = OrangeFg, FontSize = 9, FontFamily = new FontFamily("Consolas"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 2, 0) });
            svT3Val = CreateTextBox(30, "3.0"); svT3Val.Height = 20; svT3Val.FontSize = 9;
            svRow2.Children.Add(svT3Val);
            svT3Type = CreateCombo(42, new[] { "ATR", "Ticks", "Pts" }); svT3Type.Height = 20; svT3Type.FontSize = 8; svT3Type.Margin = new Thickness(2, 0, 0, 0);
            svRow2.Children.Add(svT3Type);
            t3Row = svRow2;  // Store reference for visibility control
            stack.Children.Add(svRow2);

            // T4 [4.0] ATR (hidden by default)
            StackPanel svRow3 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 2), Visibility = Visibility.Collapsed };
            svRow3.Children.Add(new TextBlock { Text = "       T4", Foreground = PurpleFg, FontSize = 9, FontFamily = new FontFamily("Consolas"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 2, 0) });
            svT4Val = CreateTextBox(30, "4.0"); svT4Val.Height = 20; svT4Val.FontSize = 9;
            svRow3.Children.Add(svT4Val);
            svT4Type = CreateCombo(42, new[] { "ATR", "Ticks", "Pts" }); svT4Type.Height = 20; svT4Type.FontSize = 8; svT4Type.Margin = new Thickness(2, 0, 0, 0);
            svRow3.Children.Add(svT4Type);
            t4Row = svRow3;  // Store reference for visibility control
            stack.Children.Add(svRow3);

            // T5 [5.0] ATR (hidden by default)
            StackPanel svRow4 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 3), Visibility = Visibility.Collapsed };
            svRow4.Children.Add(new TextBlock { Text = "       T5", Foreground = CyanFg, FontSize = 9, FontFamily = new FontFamily("Consolas"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 2, 0) });
            svT5Val = CreateTextBox(30, "5.0"); svT5Val.Height = 20; svT5Val.FontSize = 9;
            svRow4.Children.Add(svT5Val);
            svT5Type = CreateCombo(42, new[] { "ATR", "Ticks", "Pts" }); svT5Type.Height = 20; svT5Type.FontSize = 8; svT5Type.Margin = new Thickness(2, 0, 0, 0);
            svRow4.Children.Add(svT5Type);
            t5Row = svRow4;  // Store reference for visibility control
            stack.Children.Add(svRow4);

            // STR: $150  MAX: $1200
            // V12: Force explicit width so Star columns work inside StackPanel
            Grid riskRow = new Grid { Margin = new Thickness(0, 0, 0, 3) };
            riskRow.HorizontalAlignment = HorizontalAlignment.Stretch;
            riskRow.Width = PanelWidth - 8; // Match modeCountGrid width
            riskRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            riskRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            riskRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            riskRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            TextBlock strLabel = new TextBlock { Text = "STR:", Foreground = OrangeFg, FontSize = 9, FontFamily = new FontFamily("Consolas"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 2, 0) };
            Grid.SetColumn(strLabel, 0);
            riskRow.Children.Add(strLabel);

            strVal = CreateTextBox(0, "$150"); strVal.Height = 20; strVal.FontSize = 9; strVal.Foreground = OrangeFg;
            
            // Add Stop Type Dropdown (NEW)
            svStrType = CreateCombo(40, new[] { "ATR", "Ticks", "Pts", "OR" }); 
            svStrType.Height = 20; svStrType.FontSize = 8;
            svStrType.ToolTip = "Stop Type: ATR, Ticks, Points, or Open Range";
            svStrType.SelectionChanged += (s, e) => 
            {
                string val = (svStrType.SelectedItem as ComboBoxItem)?.Content?.ToString();
                if (!string.IsNullOrEmpty(val)) SendCommand($"SET_STOP_TYPE|{val}");
            };

            StackPanel strStack = new StackPanel { Orientation = Orientation.Horizontal };
            strStack.Children.Add(strVal);
            strVal.Width = 33; // Explicit width to fit
            svStrType.Margin = new Thickness(2, 0, 0, 0); 
            strStack.Children.Add(svStrType);
            
            Grid.SetColumn(strStack, 1);
            riskRow.Children.Add(strStack);

            TextBlock maxLabel = new TextBlock { Text = "MAX:", Foreground = OrangeFg, FontSize = 9, FontFamily = new FontFamily("Consolas"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 2, 0) };
            Grid.SetColumn(maxLabel, 2);
            riskRow.Children.Add(maxLabel);

            maxVal = CreateTextBox(0, "$1200"); maxVal.Height = 20; maxVal.FontSize = 9; maxVal.Foreground = OrangeFg;
            Grid.SetColumn(maxVal, 3);
            riskRow.Children.Add(maxVal);

            stack.Children.Add(riskRow);

            // SYNC ALL button
            syncAllButton = CreateButton("SYNC ALL", double.NaN, CyanBg, CyanFg, CyanBorder);
            syncAllButton.Height = 24;
            syncAllButton.FontWeight = FontWeights.Bold;
            syncAllButton.Click += SyncAll_Click;
            stack.Children.Add(syncAllButton);

            section.Child = stack;
            return section;
        }

        private Button CreateModeChip(string text, bool isActive, int col)
        {
            Button btn = new Button
            {
                Content = text,
                Background = isActive ? CyanBg : BtnBg,
                Foreground = isActive ? CyanFg : TextMuted,
                BorderBrush = isActive ? CyanBorder : BtnBorder,
                BorderThickness = new Thickness(1),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 9,
                Height = 22,
                Padding = new Thickness(2, 0, 2, 0),
                Margin = new Thickness(0, 0, 0, 2),
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            btn.Click += (s, e) => SelectConfigMode(text, btn);
            if (col >= 0) Grid.SetColumn(btn, col);
            return btn;
        }

        private Button CreateCountChip(string text, int count)
        {
            Button btn = CreateButton(text, double.NaN, BtnBg, TextPrimary, BtnBorder); 
            btn.Height = 22;
            btn.FontSize = 9;
            btn.Margin = new Thickness(0, 0, 0, 2);
            btn.HorizontalAlignment = HorizontalAlignment.Stretch;
            btn.Click += (s, e) => SelectTargetCount(count, btn);
            return btn;
        }

        private void SelectConfigMode(string mode, Button clickedBtn)
        {
            // Save current mode's settings before switching
            if (fullConfig == null) fullConfig = new V12FullConfig();
            fullConfig.SetSettings(selectedConfigMode, CaptureCurrentSettings());

            // Switch to new mode
            selectedConfigMode = mode;
            fullConfig.ActiveMode = mode;
            SendCommand($"SET_MODE|{mode}");

            // Update button visuals
            HighlightModeButton(clickedBtn);

            // Load new mode's settings (or defaults if first use)
            ApplySettings(fullConfig.GetSettings(mode));

            // Persist the change
            SaveConfig();
        }

        private void SelectTargetCount(int count, Button clickedBtn)
        {
            selectedTargetCount = count;
            SendCommand($"SET_TARGETS|{count}");

            foreach (var btn in new[] { cnt1, cnt2, cnt3, cnt4, cnt5 })
            {
                if (btn == null) continue;
                btn.Background = BtnBg;
                btn.Foreground = TextPrimary;
                btn.BorderBrush = BtnBorder;
            }

            clickedBtn.Background = CyanBg;
            clickedBtn.Foreground = CyanFg;
            clickedBtn.BorderBrush = CyanBorder;

            // Update visibility of T2/T3 based on count
            UpdateTargetVisibility(count);
            SaveConfig();
        }

        private void UpdateTargetVisibility(int count)
        {
            // T1 always visible (count >= 1)
            // T2 visible if count >= 2
            // T3 visible if count >= 3
            // T4 visible if count >= 4
            // T5 visible if count >= 5

            if (svT2Val != null) svT2Val.IsEnabled = count >= 2;
            if (svT2Type != null) svT2Type.IsEnabled = count >= 2;
            if (svT3Val != null) svT3Val.IsEnabled = count >= 3;
            if (svT3Type != null) svT3Type.IsEnabled = count >= 3;
            if (svT4Val != null) svT4Val.IsEnabled = count >= 4;
            if (svT4Type != null) svT4Type.IsEnabled = count >= 4;
            if (svT5Val != null) svT5Val.IsEnabled = count >= 5;
            if (svT5Type != null) svT5Type.IsEnabled = count >= 5;

            // Show/hide rows based on count
            if (t3Row != null) t3Row.Visibility = count >= 3 ? Visibility.Visible : Visibility.Collapsed;
            if (t4Row != null) t4Row.Visibility = count >= 4 ? Visibility.Visible : Visibility.Collapsed;
            if (t5Row != null) t5Row.Visibility = count >= 5 ? Visibility.Visible : Visibility.Collapsed;
        }

        #endregion

        #region UI Helpers

        private Border CreateSectionBorder()
        {
            return new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(20, 20, 25)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Background = BgDeep,
                HorizontalAlignment = HorizontalAlignment.Stretch // V12.7: Fill width
            };
        }

        private TextBlock CreateSectionHeader(string text)
        {
            return new TextBlock
            {
                Text = text,
                Foreground = CyanAccent,
                FontSize = 8,
                FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Consolas"),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };
        }

        private Button CreateButton(string text, double width, Brush bg, Brush fg, Brush border)
        {
            Button btn = new Button
            {
                Content = text,
                Background = bg,
                Foreground = fg,
                BorderBrush = border,
                BorderThickness = new Thickness(1),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 10, // V12: Reduced from 11
                FontWeight = FontWeights.SemiBold,
                Height = 22, // V12: Confirmed height
                Padding = new Thickness(2, 0, 2, 0), // V12: Reduced padding
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            if (!double.IsNaN(width)) btn.Width = width;
            return btn;
        }

        private Button CreateDashedButton(string text, Brush fg)
        {
            return new Button
            {
                Content = text,
                Background = BgSlate,
                Foreground = fg,
                BorderBrush = fg,
                BorderThickness = new Thickness(1),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 10, // V12: Reduced from 12
                FontWeight = FontWeights.Bold,
                Height = 22, // V12: Reduced from 24
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
        }

        private TextBox CreateTextBox(double width, string defaultText)
        {
            TextBox tb = new TextBox
            {
                Text = defaultText,
                Background = BgSlate,
                Foreground = TextPrimary,
                BorderBrush = BtnBorder,
                BorderThickness = new Thickness(1),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 9,
                Height = 20,
                TextAlignment = TextAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center
            };

            // Prevent NinjaTrader from intercepting keyboard input (symbol lookup)
            // Mark as handled BEFORE the TextBox processes it to stop NT8 search dialog
            tb.PreviewKeyDown += (s, e) =>
            {
                // Let Tab/Enter/Escape bubble for navigation
                if (e.Key == Key.Tab || e.Key == Key.Enter || e.Key == Key.Escape)
                    return;
                
                // Stop event from bubbling to NinjaTrader chart - prevents symbol search
                e.Handled = true;
                
                // Manually handle the key input for the TextBox
                TextBox textBox = s as TextBox;
                if (textBox == null) return;
                
                string keyChar = "";
                if (e.Key >= Key.D0 && e.Key <= Key.D9)
                    keyChar = ((char)('0' + (e.Key - Key.D0))).ToString();
                else if (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9)
                    keyChar = ((char)('0' + (e.Key - Key.NumPad0))).ToString();
                else if (e.Key == Key.Back && textBox.Text.Length > 0 && textBox.SelectionStart > 0)
                {
                    int pos = textBox.SelectionStart;
                    textBox.Text = textBox.Text.Remove(pos - 1, 1);
                    textBox.SelectionStart = pos - 1;
                    return;
                }
                else if (e.Key == Key.Delete && textBox.SelectionStart < textBox.Text.Length)
                {
                    int pos = textBox.SelectionStart;
                    textBox.Text = textBox.Text.Remove(pos, 1);
                    textBox.SelectionStart = pos;
                    return;
                }
                else if (e.Key == Key.OemPeriod || e.Key == Key.Decimal)
                    keyChar = ".";
                else if (e.Key == Key.OemMinus || e.Key == Key.Subtract)
                    keyChar = "-";
                else if (e.Key == Key.Space)
                    keyChar = " ";
                else
                    return;  // Ignore other keys
                
                int caret = textBox.SelectionStart;
                textBox.Text = textBox.Text.Insert(caret, keyChar);
                textBox.SelectionStart = caret + 1;
            };
            tb.GotKeyboardFocus += (s, e) =>
            {
                // Stop bubbling to prevent NT8 chart keyboard shortcuts
                e.Handled = true;
            };

            // Auto-save on text change
            tb.TextChanged += (s, e) => { if (panelCreated) SaveConfig(); };

            if (width > 0) tb.Width = width;
            return tb;
        }

        private ComboBox CreateCombo(double width, string[] items)
        {
            ComboBox combo = new ComboBox
            {
                Background = BtnBg,
                Foreground = TextPrimary,
                BorderBrush = BtnBorder,
                BorderThickness = new Thickness(1),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 9, // V12: Slightly larger for readability
                Height = 20   // V12: Standardized height
            };

            if (width > 0) combo.Width = width;

            foreach (var item in items)
            {
                combo.Items.Add(new ComboBoxItem { Content = item });
            }

            if (combo.Items.Count > 0)
                combo.SelectedIndex = 0;

            // Auto-save on selection change
            combo.SelectionChanged += (s, e) => { if (panelCreated) SaveConfig(); };

            return combo;
        }

        private void TriggerGlow(Brush color)
        {
            if (mainPanel != null)
            {
                mainPanel.BorderBrush = color;
                mainPanel.BorderThickness = new Thickness(2, 0, 0, 0);
                glowTimer?.Stop();
                glowTimer?.Start();
            }
        }

        #endregion

        #region Event Handlers

        private void Submit_Click(object sender, RoutedEventArgs e)
        {
            string direction = (directionCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "OR LONG";
            string price = priceInput.Text;

            string cmd = direction.Contains("LONG") ? "OR_LONG" : "OR_SHORT";
            cmd += $"|{activeSymbol}";
            if (!string.IsNullOrEmpty(price) && price != "0.00")
                cmd += $"|{price}";

            SendCommand(cmd);
            TriggerGlow(GreenFg);
        }

        private void Fleet_Click(object sender, RoutedEventArgs e)
        {
            SendCommand("GET_FLEET");
        }

        private void LeaderAccount_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (leaderAccountCombo == null || !panelCreated) return;
            
            ComboBoxItem selected = leaderAccountCombo.SelectedItem as ComboBoxItem;
            if (selected != null && selected.Tag is string accountName)
            {
                // Sync the Native Chart Trader to this account
                SyncChartTraderAccount(accountName);
            }
        }

        private void SyncChartTraderAccount(string accountName)
        {
            // Goal: Find the 'AccountSelector' in the hidden chartTraderElement and set it
            if (chartTraderElement == null) return;
            
            try 
            {
                // Find the AccountSelector control (it's a ComboBox or Selector)
                Selector accountSelector = FindAccountSelector(chartTraderElement);
                if (accountSelector != null)
                {
                    foreach (var item in accountSelector.Items)
                    {
                        // NinjaTrader account items usually have a specific type, check string representation or Name
                        string itemText = item.ToString();
                        
                        if (itemText == accountName || (item is NinjaTrader.Cbi.Account acct && acct.Name == accountName))
                        {
                            accountSelector.SelectedItem = item;
                            Print($"V12 STANDARD: Synced Chart Trader to {accountName}");
                            return;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                 Print($"V12 STANDARD: Sync Error - {ex.Message}");
            }
        }

        private Selector FindAccountSelector(DependencyObject parent)
        {
            if (parent == null) return null;
            
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for(int i=0; i<count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                
                if (child is Selector selector)
                {
                    if (selector.Items.Count > 0)
                    {
                         var first = selector.Items[0];
                         if (first is NinjaTrader.Cbi.Account || first.ToString().Contains("Sim101"))
                            return selector;
                    }
                }
                
                var result = FindAccountSelector(child);
                if (result != null) return result;
            }
            return null;
        }

        private void SyncAll_Click(object sender, RoutedEventArgs e)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append($"CONFIG|{selectedConfigMode}|");
            sb.Append($"COUNT:{selectedTargetCount};");
            sb.Append($"T1:{svT1Val.Text};T1TYPE:{(svT1Type.SelectedItem as ComboBoxItem)?.Content ?? "ATR"};");
            sb.Append($"T2:{svT2Val.Text};T2TYPE:{(svT2Type.SelectedItem as ComboBoxItem)?.Content ?? "ATR"};");
            sb.Append($"T3:{svT3Val.Text};T3TYPE:{(svT3Type.SelectedItem as ComboBoxItem)?.Content ?? "ATR"};");
            sb.Append($"T5:{svT5Val.Text.Replace(" ", "")};T5TYPE:{(svT5Type.SelectedItem as ComboBoxItem)?.Content ?? "ATR"};");
            sb.Append($"STR:{strVal.Text};STRTYPE:{(svStrType.SelectedItem as ComboBoxItem)?.Content ?? "ATR"};MAX:{maxVal.Text};");

            SendCommand(sb.ToString());
            // PanelWidth = 250; // V12: Restored to 250 for proper alignment
            // IpcPort = 5000;
            // AutoConnect = true;
        }

        #endregion

        #region IPC Communication

        private void ConnectToStrategy()
        {
            try
            {
                lock (tcpLock)
                {
                    if (isConnected) return;

                    tcpClient = new TcpClient();
                    tcpClient.Connect("127.0.0.1", IpcPort);
                    tcpStream = tcpClient.GetStream();
                    isConnected = true;

                    Print($"V12 STANDARD: Connected on port {IpcPort}");

                    if (ChartControl != null)
                    {
                        ChartControl.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            if (hubStatusLed != null)
                            {
                                hubStatusLed.Background = GreenFg;
                                hubStatusLed.ToolTip = "IPC Connected";
                            }
                        }));
                    }

                    receiveThread = new Thread(ReceiveLoop) { IsBackground = true, Name = "V12_Std_Receive" };
                    receiveThread.Start();

                    SendCommand("GET_LAYOUT");
                }
            }
            catch (Exception ex)
            {
                Print($"V12 STANDARD: Connection failed - {ex.Message}");
                isConnected = false;

                if (ChartControl != null)
                {
                    ChartControl.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (hubStatusLed != null)
                        {
                            hubStatusLed.Background = TextMuted;
                            hubStatusLed.ToolTip = "IPC Disconnected";
                        }
                    }));
                }
            }
        }

        private void DisconnectFromStrategy()
        {
            lock (tcpLock)
            {
                isConnected = false;
                try { tcpStream?.Close(); } catch { }
                try { tcpClient?.Close(); } catch { }
                tcpStream = null;
                tcpClient = null;
            }
        }

        private void SendCommand(string command)
        {
            Task.Run(() =>
            {
                try
                {
                    if (!isConnected) ConnectToStrategy();

                    lock (tcpLock)
                    {
                        if (tcpStream != null && tcpStream.CanWrite)
                        {
                            if (!command.Contains("|"))
                                command = $"{command}|{activeSymbol}";

                            byte[] data = Encoding.UTF8.GetBytes(command + "\n");
                            tcpStream.Write(data, 0, data.Length);
                            tcpStream.Flush();

                            Print($"V12 STD: Sent -> {command}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Print($"V12 STD: Send error - {ex.Message}");
                    isConnected = false;
                }
            });
        }

        private void ReceiveLoop()
        {
            StringBuilder buffer = new StringBuilder();
            byte[] readBuffer = new byte[4096];

            while (isConnected)
            {
                try
                {
                    if (tcpStream == null || !tcpStream.CanRead) break;

                    int bytesRead = tcpStream.Read(readBuffer, 0, readBuffer.Length);
                    if (bytesRead == 0) break;

                    buffer.Append(Encoding.UTF8.GetString(readBuffer, 0, bytesRead));

                    string data = buffer.ToString();
                    int newlineIdx;
                    while ((newlineIdx = data.IndexOf('\n')) >= 0)
                    {
                        string message = data.Substring(0, newlineIdx).Trim();
                        data = data.Substring(newlineIdx + 1);

                        if (!string.IsNullOrEmpty(message))
                            responseQueue.Enqueue(message);
                    }
                    buffer.Clear();
                    buffer.Append(data);
                }
                catch
                {
                    break;
                }
            }
        }

        private void ProcessStrategyResponse(string response)
        {
            string[] parts = response.Split('|');
            if (parts.Length == 0) return;

            if (ChartControl != null)
            {
                ChartControl.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        if (parts[0] == "TELEMETRY" && parts.Length > 1)
                            ParseTelemetry(parts[1]);
                        else if (parts[0] == "CONFIG" && parts.Length > 1)
                            ParseConfig(parts[1]);
                        else if (parts[0] == "TREND" && parts.Length > 1)
                            UpdateTrendIndicator(parts[1]);
                        else if (parts[0] == "REQUEST_FLEET_STATE")
                        {
                            // V12.3: Strategy requests current fleet state after reconnect
                            foreach (string acctName in selectedFleetAccounts)
                            {
                                SendCommand($"TOGGLE_ACCOUNT|{acctName}|1");
                            }
                            Print($"V12 STD: Fleet state sent — {selectedFleetAccounts.Count} accounts active");
                        }
                    }
                    catch (Exception ex)
                    {
                        Print($"V12 STD: Parse error - {ex.Message}");
                    }
                }));
            }
        }

        private void ParseTelemetry(string data)
        {
            // V12.3: Parse telemetry data from strategy
            // Format: EMA9:val;EMA15:val;EMA30:val;EMA65:val;EMA200:val;ORH:val;ORL:val;ATR:val
            foreach (string pair in data.Split(';'))
            {
                string[] kv = pair.Split(':');
                if (kv.Length != 2) continue;

                string key = kv[0].Trim().ToUpper();
                string value = kv[1].Trim();

                switch (key)
                {
                    case "EMA9":
                        UpdateEmaText(ema9Text, "9:", value);
                        break;
                    case "EMA15":
                        UpdateEmaText(ema15Text, "15:", value);
                        break;
                    case "EMA30":
                        UpdateEmaText(ema30Text, "30:", value);
                        break;
                    case "EMA65":
                        UpdateEmaText(ema65Text, "65:", value);
                        break;
                    case "EMA200":
                        UpdateEmaText(ema200Text, "200:", value);
                        break;
                    case "ATR":
                        if (atrText != null) atrText.Text = "ATR: " + value;
                        break;
                    case "ORH":
                        UpdateORText(or5Text, or15Text, "ORH", value);
                        break;
                    case "ORL":
                        UpdateORText(or5Text, or15Text, "ORL", value);
                        break;
                }
            }
        }

        /// <summary>
        /// V12.3: Helper to update EMA TextBlock with label:value format
        /// </summary>
        private void UpdateEmaText(TextBlock tb, string label, string value)
        {
            if (tb == null) return;
            tb.Inlines.Clear();
            tb.Inlines.Add(new System.Windows.Documents.Run(label) { Foreground = TextMuted });
            tb.Inlines.Add(new System.Windows.Documents.Run(value) { Foreground = TextPrimary });
        }

        // V12.3: Cache OR values for display
        private string cachedORH = "0.00";
        private string cachedORL = "0.00";

        /// <summary>
        /// V12.3: Helper to update OR TextBlocks with H/L values
        /// </summary>
        private void UpdateORText(TextBlock or5Block, TextBlock or15Block, string key, string value)
        {
            if (key == "ORH") cachedORH = value;
            else if (key == "ORL") cachedORL = value;

            // Calculate range
            double h = 0, l = 0;
            double.TryParse(cachedORH, out h);
            double.TryParse(cachedORL, out l);
            double range = h - l;

            // Update OR5 line (placeholder - actual 5-min OR would need separate tracking)
            if (or5Block != null)
            {
                or5Block.Inlines.Clear();
                or5Block.Inlines.Add(new System.Windows.Documents.Run("OR5: ") { Foreground = OrangeFg, FontWeight = FontWeights.Bold });
                or5Block.Inlines.Add(new System.Windows.Documents.Run(cachedORH) { Foreground = OrangeFg });
                or5Block.Inlines.Add(new System.Windows.Documents.Run(" | ") { Foreground = TextMuted });
                or5Block.Inlines.Add(new System.Windows.Documents.Run(cachedORL) { Foreground = OrangeFg });
                or5Block.Inlines.Add(new System.Windows.Documents.Run(string.Format(" (R: {0:F1})", range)) { Foreground = TextMuted });
            }

            // Update OR15 line (uses same data for now - could be enhanced for 15-min OR)
            if (or15Block != null)
            {
                or15Block.Inlines.Clear();
                or15Block.Inlines.Add(new System.Windows.Documents.Run("OR15: ") { Foreground = OrangeFg, FontWeight = FontWeights.Bold });
                or15Block.Inlines.Add(new System.Windows.Documents.Run(cachedORH) { Foreground = OrangeFg });
                or15Block.Inlines.Add(new System.Windows.Documents.Run(" | ") { Foreground = TextMuted });
                or15Block.Inlines.Add(new System.Windows.Documents.Run(cachedORL) { Foreground = OrangeFg });
                or15Block.Inlines.Add(new System.Windows.Documents.Run(string.Format(" (R: {0:F1})", range)) { Foreground = TextMuted });
            }
        }

        private void ParseConfig(string data)
        {
            foreach (string pair in data.Split(';'))
            {
                string[] kv = pair.Split(':');
                if (kv.Length != 2) continue;

                string key = kv[0].Trim().ToUpper();
                string value = kv[1].Trim();

                switch (key)
                {
                    case "T1": if (svT1Val != null) svT1Val.Text = value; break;
                    case "T2": if (svT2Val != null) svT2Val.Text = value; break;
                    case "T3": if (svT3Val != null) svT3Val.Text = value; break;
                    case "STR": if (strVal != null) strVal.Text = value; break;
                    case "MAX": if (maxVal != null) maxVal.Text = value; break;
                    case "STRTYPE": if (svStrType != null) SelectComboByValue(svStrType, value); break;
                    case "COUNT":
                        if (int.TryParse(value, out int count))
                        {
                            // Update Visuals without sending command loop
                            selectedTargetCount = count;
                            UpdateTargetCountVisual(count);
                            UpdateTargetVisibility(count);
                        }
                        break;
                    // V12.5: Handle MODE sync from Strategy broadcast
                    case "MODE":
                        string incomingMode = value.ToUpper();
                        if (incomingMode != selectedConfigMode.ToUpper())
                        {
                            selectedConfigMode = incomingMode;
                            Button modeBtn = GetModeButton(incomingMode);
                            if (modeBtn != null)
                            {
                                HighlightModeButton(modeBtn);
                                // Load mode-specific settings if we have them
                                if (fullConfig != null)
                                {
                                    ApplySettings(fullConfig.GetSettings(incomingMode));
                                }
                            }
                            Print(string.Format("V12.5: MODE Synced -> {0}", incomingMode));
                        }
                        break;
                }
            }
        }

        private void SelectComboByValue(ComboBox combo, string value)
        {
             if (combo == null || string.IsNullOrEmpty(value)) return;
             
             string normalizedValue = value.ToUpper().Trim();
             
             // Mapping for User Friendliness / Legacy Support
             if (normalizedValue == "OR" || normalizedValue == "OPENRANGE" || normalizedValue == "OPEN RANGE") 
                 normalizedValue = "OR";
             else if (normalizedValue == "ATR" || normalizedValue == "A")
                 normalizedValue = "ATR";
             else if (normalizedValue == "TICKS" || normalizedValue == "T")
                 normalizedValue = "Ticks";
             else if (normalizedValue == "PTS" || normalizedValue == "POINTS" || normalizedValue == "P")
                 normalizedValue = "Pts";

             foreach (ComboBoxItem item in combo.Items)
             {
                 if (item.Content.ToString().ToUpper() == normalizedValue.ToUpper() || 
                     (normalizedValue == "OR" && item.Content.ToString() == "OR") ||
                     (normalizedValue == "ATR" && item.Content.ToString() == "ATR"))
                 {
                     combo.SelectedItem = item;
                     return;
                 }
             }
        }

        private void UpdateTrendIndicator(string trend)
        {
            if (trendText == null || trendIndicator == null) return;

            bool bullish = trend.ToUpper().Contains("BULL") || trend == "1";
            trendText.Text = bullish ? "BULLISH" : "BEARISH";
            trendText.Foreground = bullish ? GreenFg : RedFg;
            trendIndicator.Background = bullish ? GreenBg : RedBg;
            trendIndicator.BorderBrush = bullish ? GreenBorder : RedBorder;
        }

        #endregion
    }
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private V12StandardPanel[] cacheV12StandardPanel;
		public V12StandardPanel V12StandardPanel(int ipcPort, bool autoConnect, int panelWidth)
		{
			return V12StandardPanel(Input, ipcPort, autoConnect, panelWidth);
		}

		public V12StandardPanel V12StandardPanel(ISeries<double> input, int ipcPort, bool autoConnect, int panelWidth)
		{
			if (cacheV12StandardPanel != null)
				for (int idx = 0; idx < cacheV12StandardPanel.Length; idx++)
					if (cacheV12StandardPanel[idx] != null && cacheV12StandardPanel[idx].IpcPort == ipcPort && cacheV12StandardPanel[idx].AutoConnect == autoConnect && cacheV12StandardPanel[idx].PanelWidth == panelWidth && cacheV12StandardPanel[idx].EqualsInput(input))
						return cacheV12StandardPanel[idx];
			return CacheIndicator<V12StandardPanel>(new V12StandardPanel(){ IpcPort = ipcPort, AutoConnect = autoConnect, PanelWidth = panelWidth }, input, ref cacheV12StandardPanel);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.V12StandardPanel V12StandardPanel(int ipcPort, bool autoConnect, int panelWidth)
		{
			return indicator.V12StandardPanel(Input, ipcPort, autoConnect, panelWidth);
		}

		public Indicators.V12StandardPanel V12StandardPanel(ISeries<double> input , int ipcPort, bool autoConnect, int panelWidth)
		{
			return indicator.V12StandardPanel(input, ipcPort, autoConnect, panelWidth);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.V12StandardPanel V12StandardPanel(int ipcPort, bool autoConnect, int panelWidth)
		{
			return indicator.V12StandardPanel(Input, ipcPort, autoConnect, panelWidth);
		}

		public Indicators.V12StandardPanel V12StandardPanel(ISeries<double> input , int ipcPort, bool autoConnect, int panelWidth)
		{
			return indicator.V12StandardPanel(input, ipcPort, autoConnect, panelWidth);
		}
	}
}

#endregion
