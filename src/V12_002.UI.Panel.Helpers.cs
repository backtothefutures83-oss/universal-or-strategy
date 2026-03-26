// Build 1105: V12_001 panel port -- factories + Chart Trader discovery
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class V12_002
    {
        #region Panel Button Factories

        private static readonly FontFamily ConsolasFont = new FontFamily("Consolas");

        private Button CreateButton(string text, double width, SolidColorBrush bg, SolidColorBrush fg, SolidColorBrush border)
        {
            var btn = new Button
            {
                Content = text,
                Background = bg,
                Foreground = fg,
                BorderBrush = border,
                BorderThickness = new Thickness(1),
                FontFamily = ConsolasFont,
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Height = 22,
                Padding = new Thickness(2, 0, 2, 0),
                Cursor = System.Windows.Input.Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            if (width > 0)
                btn.Width = width;
            return btn;
        }

        private Button CreateDashedButton(string text, SolidColorBrush fg)
        {
            return new Button
            {
                Content = text,
                Background = BgSlate,
                Foreground = fg,
                BorderBrush = fg,
                BorderThickness = new Thickness(1),
                FontFamily = ConsolasFont,
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Height = 22,
                Cursor = System.Windows.Input.Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
        }

        private TextBox CreateTextBox(string defaultText, double width)
        {
            var tb = new TextBox
            {
                Text = defaultText,
                Background = BgSlate,
                Foreground = TextPrimary,
                BorderBrush = BtnBorder,
                BorderThickness = new Thickness(1),
                FontFamily = ConsolasFont,
                FontSize = 9,
                Height = 20,
                TextAlignment = TextAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            if (width > 0)
                tb.Width = width;
            return tb;
        }

        private ComboBox CreateCombo(double width, params string[] items)
        {
            var cb = new ComboBox
            {
                Background = BtnBg,
                Foreground = TextPrimary,
                BorderBrush = BtnBorder,
                BorderThickness = new Thickness(1),
                FontFamily = ConsolasFont,
                FontSize = 10,
                IsTextSearchEnabled = false
            };
            if (width > 0)
                cb.Width = width;
            for (int i = 0; i < items.Length; i++)
                cb.Items.Add(new ComboBoxItem { Content = items[i], Foreground = TextPrimary });
            if (cb.Items.Count > 0)
                cb.SelectedIndex = 0;
            return cb;
        }

        private Button CreateModeChip(string text, bool isActive)
        {
            return new Button
            {
                Content = text,
                Background = isActive ? CyanBg : BtnBg,
                Foreground = isActive ? CyanFg : TextMuted,
                BorderBrush = isActive ? CyanBorder : BtnBorder,
                BorderThickness = new Thickness(1),
                FontFamily = ConsolasFont,
                FontSize = 9,
                Height = 22,
                Padding = new Thickness(2, 0, 2, 0),
                Margin = new Thickness(0, 0, 0, 2),
                Cursor = System.Windows.Input.Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
        }

        private Button CreateCountChip(string text)
        {
            var btn = CreateButton(text, 0, BtnBg, TextPrimary, BtnBorder);
            btn.Height = 22;
            btn.FontSize = 9;
            btn.Margin = new Thickness(0, 0, 0, 2);
            btn.HorizontalAlignment = HorizontalAlignment.Stretch;
            return btn;
        }

        // Build 1107: Factory for live target control row.
        // Layout: [T{N} label 22px] [Price TextBox Star] [Cts label Auto] [Close button 22px]
        private Grid CreateLiveTargetRow(int targetNum, out TextBox priceBox, out TextBlock ctsBlock)
        {
            SolidColorBrush color;
            switch (targetNum)
            {
                case 1: color = GreenFg; break;
                case 2: color = YellowFg; break;
                case 3: color = OrangeFg; break;
                case 4: color = RedFg; break;
                case 5: color = PinkFg; break;
                default: color = TextPrimary; break;
            }

            Grid row = new Grid { Visibility = Visibility.Collapsed, Height = 22 };
            if (targetNum > 1) row.Margin = new Thickness(0, 2, 0, 0);
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) });

            TextBlock label = new TextBlock
            {
                Text = "T" + targetNum,
                Foreground = color,
                FontFamily = ConsolasFont,
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(label, 0);
            row.Children.Add(label);

            priceBox = CreateTextBox("--", 0);
            priceBox.FontSize = 10;
            priceBox.Margin = new Thickness(2, 0, 2, 0);
            priceBox.TextAlignment = TextAlignment.Right;
            Grid.SetColumn(priceBox, 1);
            row.Children.Add(priceBox);

            ctsBlock = new TextBlock
            {
                Text = "0",
                Foreground = TextMuted,
                FontFamily = ConsolasFont,
                FontSize = 8,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(2, 0, 2, 0)
            };
            Grid.SetColumn(ctsBlock, 2);
            row.Children.Add(ctsBlock);

            // Close button: liquidate this target at market
            Button closeBtn = CreateButton("X", 20, RedBg, RedFg, RedBorder);
            closeBtn.FontSize = 8;
            closeBtn.Padding = new Thickness(0);
            closeBtn.Click += (s, e) =>
            {
                PanelCommand("CLOSE_T" + targetNum);
                TriggerGlow(color);
            };
            Grid.SetColumn(closeBtn, 3);
            row.Children.Add(closeBtn);

            return row;
        }

        private TextBlock CreateEmaLabel(string label, string value, SolidColorBrush valueColor)
        {
            var tb = new TextBlock
            {
                FontSize = 11,
                FontFamily = ConsolasFont,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 10, 0)
            };
            tb.Inlines.Add(new System.Windows.Documents.Run(label) { Foreground = TextMuted });
            tb.Inlines.Add(new System.Windows.Documents.Run(value) { Foreground = valueColor });
            return tb;
        }

        #endregion

        #region Chart Trader Discovery

        private void DumpVisualTree()
        {
            try
            {
                Print("=== V12 VISUAL TREE DUMP (ChartControl -> Window) ===");
                DependencyObject current = ChartControl;
                int depth = 0;

                while (current != null)
                {
                    string shortName = current.GetType().Name;
                    string info = "  [" + depth + "] " + shortName;

                    if (current is FrameworkElement fe)
                        info += " Name=" + fe.Name + " W=" + fe.ActualWidth.ToString("F0")
                              + " H=" + fe.ActualHeight.ToString("F0") + " Vis=" + fe.Visibility;

                    if (current is Grid grid)
                    {
                        info += " Cols=" + grid.ColumnDefinitions.Count
                              + " Rows=" + grid.RowDefinitions.Count
                              + " Children=" + grid.Children.Count;

                        for (int i = 0; i < grid.ColumnDefinitions.Count; i++)
                        {
                            var cd = grid.ColumnDefinitions[i];
                            info += "\n      Col[" + i + "]: Width=" + cd.Width
                                  + " Actual=" + cd.ActualWidth.ToString("F0");
                        }

                        for (int i = 0; i < grid.Children.Count; i++)
                        {
                            var ch = grid.Children[i];
                            string childType = ch.GetType().Name;
                            if (childType.Contains("ChartTrader") || childType.Contains("Trader"))
                                info += "\n      ** Trader child at index " + i
                                      + ": " + ch.GetType().FullName;
                        }
                    }

                    Print(info);

                    if (current is Window) break;
                    current = VisualTreeHelper.GetParent(current);
                    depth++;
                }
                Print("=== END VISUAL TREE DUMP ===");
            }
            catch (Exception ex)
            {
                Print("V12 PANEL: DumpVisualTree error -- " + ex.Message);
            }
        }

        private FrameworkElement FindChartTraderViaOwnerChart()
        {
            try
            {
                if (ChartControl == null) return null;
                var ownerChart = ChartControl.OwnerChart;
                if (ownerChart == null)
                {
                    Print("V12 PANEL: Strategy 0 -- OwnerChart is null");
                    return null;
                }

                if (ownerChart is DependencyObject chartDO)
                {
                    var found = FindChildElementByTypeName(chartDO, "ChartTrader");
                    if (found != null)
                    {
                        Print("V12 PANEL: Strategy 0 found " + found.GetType().FullName
                            + " Vis=" + found.Visibility);
                        if (found.Visibility == Visibility.Visible)
                            return found;
                        Print("V12 PANEL: Strategy 0 -- ChartTrader not Visible, skipping");
                        return null;
                    }
                    Print("V12 PANEL: Strategy 0 -- no ChartTrader descendant from OwnerChart");
                }
                else
                {
                    Print("V12 PANEL: Strategy 0 -- OwnerChart is not a DependencyObject");
                }
            }
            catch (Exception ex)
            {
                Print("V12 PANEL: OwnerChart search error -- " + ex.Message);
            }
            return null;
        }

        private Grid FindDescendantGrid(DependencyObject parent, int minColumns)
        {
            if (parent == null) return null;
            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is Grid g && g.ColumnDefinitions.Count >= minColumns)
                    return g;
                var result = FindDescendantGrid(child, minColumns);
                if (result != null) return result;
            }
            return null;
        }

        private FrameworkElement FindChartTrader()
        {
            try
            {
                // Strategy 0: OwnerChart top-down search (most direct NT8 API path)
                FrameworkElement result = FindChartTraderViaOwnerChart();
                if (result != null)
                {
                    Print("V12 PANEL: FindChartTrader Strategy 0 (OwnerChart) -> " + result.GetType().Name);
                    return result;
                }

                // Strategy 1: ChartTab reflection
                result = FindChartTraderViaChartTab();
                if (result != null)
                {
                    Print("V12 PANEL: FindChartTrader Strategy 1 (ChartTab reflection) -> " + result.GetType().Name);
                    return result;
                }

                result = FindChartTraderBySiblingSearch();
                if (result != null)
                {
                    Print("V12 PANEL: FindChartTrader Strategy 2 (sibling search) -> " + result.GetType().Name);
                    return result;
                }

                result = FindChartTraderByTypeName();
                if (result != null)
                {
                    Print("V12 PANEL: FindChartTrader Strategy 3 (type name search) -> " + result.GetType().Name);
                    return result;
                }

                result = FindChartTraderByButton();
                if (result != null)
                {
                    Print("V12 PANEL: FindChartTrader Strategy 4 (button search) -> " + result.GetType().Name);
                    return result;
                }

                Print("V12 PANEL: FindChartTrader -- all strategies failed.");
                return null;
            }
            catch (Exception ex)
            {
                Print("V12 PANEL: FindChartTrader error -- " + ex.Message);
                return null;
            }
        }

        private FrameworkElement FindChartTraderViaChartTab()
        {
            try
            {
                DependencyObject current = ChartControl;
                object chartTab = null;

                while (current != null)
                {
                    string typeName = current.GetType().Name;
                    if (typeName == "ChartTab" || typeName.Contains("ChartTab"))
                    {
                        chartTab = current;
                        break;
                    }
                    current = VisualTreeHelper.GetParent(current);
                }

                if (chartTab == null)
                {
                    current = ChartControl;
                    while (current != null)
                    {
                        string typeName = current.GetType().Name;
                        if (typeName == "ChartTab" || typeName.Contains("ChartTab"))
                        {
                            chartTab = current;
                            break;
                        }
                        current = LogicalTreeHelper.GetParent(current);
                    }
                }

                if (chartTab == null)
                {
                    Print("V12 PANEL: Strategy 1 -- ChartTab not found in visual/logical tree");
                    return null;
                }

                Type tabType = chartTab.GetType();

                PropertyInfo ctProp = tabType.GetProperty("ChartTrader",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (ctProp != null)
                {
                    object ct = ctProp.GetValue(chartTab);
                    if (ct is FrameworkElement fe && fe.Visibility == Visibility.Visible)
                        return fe;
                }

                string[] fieldNames = new string[] { "chartTrader", "ChartTrader", "chartTraderControl", "_chartTrader" };
                for (int f = 0; f < fieldNames.Length; f++)
                {
                    FieldInfo fi = tabType.GetField(fieldNames[f], BindingFlags.NonPublic | BindingFlags.Instance);
                    if (fi != null)
                    {
                        object ct = fi.GetValue(chartTab);
                        if (ct is FrameworkElement fe && fe.Visibility == Visibility.Visible)
                            return fe;
                    }
                }

                if (chartTab is DependencyObject depObj)
                {
                    var found = FindChildElementByTypeName(depObj, "ChartTrader");
                    if (found != null && found.Visibility == Visibility.Visible)
                        return found;
                }

                Print("V12 PANEL: Strategy 1 -- ChartTab found (" + chartTab.GetType().Name
                    + ") but no ChartTrader property/field/child");
            }
            catch (Exception ex)
            {
                Print("V12 PANEL: ChartTab reflection failed -- " + ex.Message);
            }
            return null;
        }

        private FrameworkElement FindChartTraderBySiblingSearch()
        {
            try
            {
                DependencyObject current = ChartControl;
                while (current != null && !(current is Window))
                {
                    current = VisualTreeHelper.GetParent(current);
                    if (current is Grid grid)
                    {
                        foreach (UIElement child in grid.Children)
                        {
                            if (child is FrameworkElement fe && child.GetType().Name.Contains("ChartTrader")
                                && fe.Visibility == Visibility.Visible)
                                return fe;
                        }
                    }
                }
            }
            catch
            {
            }
            return null;
        }

        private FrameworkElement FindChartTraderByTypeName()
        {
            try
            {
                Window chartWindow = Window.GetWindow(ChartControl);
                if (chartWindow == null)
                    return null;
                var found = FindChildElementByTypeName(chartWindow, "ChartTrader");
                if (found != null && found.Visibility == Visibility.Visible)
                    return found;
            }
            catch
            {
            }
            return null;
        }

        private FrameworkElement FindChartTraderByButton()
        {
            try
            {
                Window chartWindow = Window.GetWindow(ChartControl);
                if (chartWindow == null)
                    return null;

                List<Button> allBuyButtons = FindAllButtonsByText(chartWindow, "Buy Mkt");
                for (int b = 0; b < allBuyButtons.Count; b++)
                {
                    DependencyObject parent = VisualTreeHelper.GetParent(allBuyButtons[b]);
                    while (parent != null)
                    {
                        if (parent is FrameworkElement fe && fe.GetType().Name.Contains("ChartTrader"))
                            return fe;
                        if (parent is Window)
                            break;
                        parent = VisualTreeHelper.GetParent(parent);
                    }
                }
            }
            catch (Exception ex)
            {
                Print("V12 PANEL: Button-based search error -- " + ex.Message);
            }
            return null;
        }

        private Grid FindChartTabGrid(DependencyObject child)
        {
            DependencyObject current = VisualTreeHelper.GetParent(child);
            Grid bestCandidate = null;

            while (current != null)
            {
                string typeName = current.GetType().Name;

                if (typeName == "ChartTab" || typeName.Contains("ChartTab"))
                {
                    if (current is Grid chartTabGrid && chartTabGrid.ColumnDefinitions.Count >= 2)
                        return chartTabGrid;

                    // Search ALL descendants (not just direct children) for target Grid
                    var descendantGrid = FindDescendantGrid(current, 2);
                    if (descendantGrid != null)
                    {
                        Print("V12 PANEL: FindChartTabGrid -- found descendant Grid inside "
                            + current.GetType().Name + " Cols=" + descendantGrid.ColumnDefinitions.Count);
                        return descendantGrid;
                    }
                }

                if (current is Grid grid && grid.ColumnDefinitions.Count >= 2)
                    bestCandidate = grid;

                if (current is Window)
                    break;
                current = VisualTreeHelper.GetParent(current);
            }

            return bestCandidate;
        }

        private FrameworkElement FindChildElementByTypeName(DependencyObject parent, string typeNameFragment)
        {
            if (parent == null)
                return null;
            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is FrameworkElement fe && fe.GetType().Name.Contains(typeNameFragment))
                    return fe;
                var result = FindChildElementByTypeName(child, typeNameFragment);
                if (result != null)
                    return result;
            }
            return null;
        }

        private List<Button> FindAllButtonsByText(DependencyObject parent, string text)
        {
            var list = new List<Button>();
            if (parent == null)
                return list;

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

        #endregion
    }
}
