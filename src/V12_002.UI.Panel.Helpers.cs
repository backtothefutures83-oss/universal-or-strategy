// Build 1105: Monolith Panel -- Button Factory Methods
// Migrated from V12_001.cs lines 2549-2678
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class V12_002
    {
        #region Panel Button Factories

        private static readonly FontFamily ConsolasFont = new FontFamily("Consolas");

        private Button CreatePanelButton(string text, double width,
            SolidColorBrush bg, SolidColorBrush fg, SolidColorBrush border)
        {
            return new Button
            {
                Content         = text,
                Width           = width,
                Height          = 22,
                FontFamily      = ConsolasFont,
                FontSize        = 10,
                FontWeight      = FontWeights.SemiBold,
                Background      = bg,
                Foreground      = fg,
                BorderBrush     = border,
                BorderThickness = new Thickness(1),
                Margin          = new Thickness(1),
                Padding         = new Thickness(2, 0, 2, 0),
                Cursor          = System.Windows.Input.Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
        }

        private Button CreateDashedPanelButton(string text, SolidColorBrush fg)
        {
            return new Button
            {
                Content         = text,
                Height          = 22,
                FontFamily      = ConsolasFont,
                FontSize        = 10,
                FontWeight      = FontWeights.SemiBold,
                Background      = BgSlate,
                Foreground      = fg,
                BorderBrush     = fg,
                BorderThickness = new Thickness(1),
                Margin          = new Thickness(1),
                Padding         = new Thickness(2, 0, 2, 0),
                Cursor          = System.Windows.Input.Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
        }

        private Button CreateModeChip(string text, SolidColorBrush fg)
        {
            return new Button
            {
                Content         = text,
                Height          = 20,
                FontFamily      = ConsolasFont,
                FontSize        = 9,
                FontWeight      = FontWeights.SemiBold,
                Background      = BgSlate,
                Foreground      = fg,
                BorderBrush     = fg,
                BorderThickness = new Thickness(1),
                Margin          = new Thickness(1),
                Padding         = new Thickness(4, 0, 4, 0),
                Cursor          = System.Windows.Input.Cursors.Hand
            };
        }

        #endregion
    }
}
