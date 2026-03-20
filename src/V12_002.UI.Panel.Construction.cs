// Build 1105: Monolith Panel -- Construction + Layout
// Creates the WPF container hierarchy and all panel controls.
// Lifecycle wire-up (CreatePanel/DestroyPanel calls) deferred to Phase 3C.
using System.Windows;
using System.Windows.Controls;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class V12_002
    {
        #region Panel Fields

        private Grid _panelRoot;
        private Border _statusLed;
        private TextBlock _priceText;
        private TextBlock _modeText;
        private TextBlock _statusText;
        private Button _btnOrLong;
        private Button _btnOrShort;
        private Button _btnRma;
        private Button _btnMomo;
        private Button _btnFlatten;
        private Button _btnCancel;
        private Button _btnBe;
        private Button _btnTrail;
        private Button _btnTrim;
        private Button[] _modeChips;
        private Button[] _countChips;

        #endregion

        #region Panel Construction

        /// <summary>
        /// Builds the Monolith panel WPF tree and attaches to chart.
        /// Must be called on the UI thread (inside Dispatcher.InvokeAsync).
        /// Phase 3C wires this into OnStateChange -> State.Realtime.
        /// </summary>
        private void CreatePanel()
        {
            if (_panelRoot != null) return;

            _panelRoot = new Grid
            {
                Width = 195,
                VerticalAlignment = VerticalAlignment.Stretch,
                ClipToBounds = true
            };

            var border = new Border
            {
                Background = BgDeep,
                BorderBrush = CyanBdr,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2)
            };

            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };

            var mainStack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(2)
            };

            mainStack.Children.Add(BuildHeaderRow());
            mainStack.Children.Add(BuildEntrySection());
            mainStack.Children.Add(BuildManageSection());
            mainStack.Children.Add(BuildModeSection());
            mainStack.Children.Add(BuildInfoSection());

            scroll.Content = mainStack;
            border.Child = scroll;
            _panelRoot.Children.Add(border);

            AttachPanelHandlers();

            UserControlCollection.Add(_panelRoot);
        }

        /// <summary>
        /// Tears down the Monolith panel and removes from chart.
        /// Must be called on the UI thread.
        /// Phase 3C wires this into OnStateChange -> State.Terminated.
        /// </summary>
        private void DestroyPanel()
        {
            if (_panelRoot == null) return;

            DetachPanelHandlers();

            UserControlCollection.Remove(_panelRoot);

            _panelRoot = null;
            _statusLed = null;
            _priceText = null;
            _modeText = null;
            _statusText = null;
            _btnOrLong = null;
            _btnOrShort = null;
            _btnRma = null;
            _btnMomo = null;
            _btnFlatten = null;
            _btnCancel = null;
            _btnBe = null;
            _btnTrail = null;
            _btnTrim = null;
            _modeChips = null;
            _countChips = null;
        }

        #region Section Builders

        private Grid BuildHeaderRow()
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            _statusLed = new Border
            {
                Width = 8,
                Height = 8,
                Background = GreenFg,
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(2, 4, 4, 4),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(_statusLed, 0);

            var label = new TextBlock
            {
                Text = "V12 [" + BUILD_TAG + "]",
                Foreground = CyanFg,
                FontFamily = ConsolasFont,
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(label, 1);

            grid.Children.Add(_statusLed);
            grid.Children.Add(label);
            return grid;
        }

        private Border BuildEntrySection()
        {
            var section = CreateSectionBorder();
            var stack = new StackPanel();

            stack.Children.Add(CreateSectionHeader("ENTRY"));

            // OR L / OR S row
            var entryRow = new Grid();
            entryRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            entryRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            _btnOrLong = CreateDashedPanelButton("OR L", GreenFg);
            Grid.SetColumn(_btnOrLong, 0);
            _btnOrShort = CreateDashedPanelButton("OR S", RedFg);
            Grid.SetColumn(_btnOrShort, 1);

            entryRow.Children.Add(_btnOrLong);
            entryRow.Children.Add(_btnOrShort);
            stack.Children.Add(entryRow);

            // RMA arm button
            _btnRma = CreateDashedPanelButton("RMA", PurpleFg);
            stack.Children.Add(_btnRma);

            // MOMO mode button
            _btnMomo = CreateDashedPanelButton("MOMO", OrangeFg);
            stack.Children.Add(_btnMomo);

            section.Child = stack;
            return section;
        }

        private Border BuildManageSection()
        {
            var section = CreateSectionBorder();
            var stack = new StackPanel();

            stack.Children.Add(CreateSectionHeader("MANAGE"));

            // FLATTEN / CANCEL row
            var row1 = new Grid();
            row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            _btnFlatten = CreatePanelButton("FLATTEN", 0, RedBg, RedFg, RedBdr);
            _btnFlatten.Width = double.NaN;
            Grid.SetColumn(_btnFlatten, 0);
            _btnCancel = CreatePanelButton("CANCEL", 0, OrangeBg, OrangeFg, OrangeBdr);
            _btnCancel.Width = double.NaN;
            Grid.SetColumn(_btnCancel, 1);

            row1.Children.Add(_btnFlatten);
            row1.Children.Add(_btnCancel);
            stack.Children.Add(row1);

            // BE / TRAIL row
            var row2 = new Grid();
            row2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            _btnBe = CreatePanelButton("BE", 0, CyanBg, CyanFg, CyanBdr);
            _btnBe.Width = double.NaN;
            Grid.SetColumn(_btnBe, 0);
            _btnTrail = CreatePanelButton("TRAIL", 0, CyanBg, CyanFg, CyanBdr);
            _btnTrail.Width = double.NaN;
            Grid.SetColumn(_btnTrail, 1);

            row2.Children.Add(_btnBe);
            row2.Children.Add(_btnTrail);
            stack.Children.Add(row2);

            // TRIM 50%
            _btnTrim = CreatePanelButton("TRIM 50%", 0, YellowBg, YellowFg, YellowBdr);
            _btnTrim.Width = double.NaN;
            stack.Children.Add(_btnTrim);

            section.Child = stack;
            return section;
        }

        private Border BuildModeSection()
        {
            var section = CreateSectionBorder();
            var stack = new StackPanel();

            stack.Children.Add(CreateSectionHeader("MODE"));

            // Mode chips
            var modeWrap = new WrapPanel { Margin = new Thickness(0, 1, 0, 1) };
            string[] modes = { "ORB", "RMA", "RETEST", "MOMO", "FFMA", "TREND" };
            _modeChips = new Button[modes.Length];
            for (int i = 0; i < modes.Length; i++)
            {
                _modeChips[i] = CreateModeChip(modes[i], TextDim);
                modeWrap.Children.Add(_modeChips[i]);
            }
            stack.Children.Add(modeWrap);

            // Count chips
            var countWrap = new WrapPanel { Margin = new Thickness(0, 1, 0, 1) };
            _countChips = new Button[5];
            for (int i = 0; i < 5; i++)
            {
                _countChips[i] = CreateModeChip((i + 1).ToString(), TextDim);
                countWrap.Children.Add(_countChips[i]);
            }
            stack.Children.Add(countWrap);

            section.Child = stack;
            return section;
        }

        private Border BuildInfoSection()
        {
            var section = CreateSectionBorder();
            var stack = new StackPanel();

            _priceText = new TextBlock
            {
                Text = "--",
                Foreground = GreenFg,
                FontFamily = ConsolasFont,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 2)
            };
            stack.Children.Add(_priceText);

            _modeText = new TextBlock
            {
                Text = "Mode: --",
                Foreground = CyanFg,
                FontFamily = ConsolasFont,
                FontSize = 9,
                Margin = new Thickness(2, 1, 0, 1)
            };
            stack.Children.Add(_modeText);

            _statusText = new TextBlock
            {
                Text = "Status: Idle",
                Foreground = TextDim,
                FontFamily = ConsolasFont,
                FontSize = 9,
                Margin = new Thickness(2, 1, 0, 1)
            };
            stack.Children.Add(_statusText);

            section.Child = stack;
            return section;
        }

        private Border CreateSectionBorder()
        {
            return new Border
            {
                Background = BgSlate,
                BorderBrush = CyanBdr,
                BorderThickness = new Thickness(0, 0, 0, 0.5),
                Margin = new Thickness(0, 2, 0, 0),
                Padding = new Thickness(2)
            };
        }

        private TextBlock CreateSectionHeader(string text)
        {
            return new TextBlock
            {
                Text = text,
                Foreground = CyanFg,
                FontFamily = ConsolasFont,
                FontSize = 8,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 2)
            };
        }

        #endregion

        #endregion
    }
}
