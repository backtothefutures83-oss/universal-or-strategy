// Build 1105: Monolith Panel -- Frozen WPF Brush Palette
// Migrated from V12_001.cs lines 251-307
using System.Windows.Media;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class V12_002
    {
        #region Panel Brushes

        private static SolidColorBrush PanelBrush(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }

        // Base
        private static readonly SolidColorBrush BgDeep    = PanelBrush(5, 5, 8);
        private static readonly SolidColorBrush BgSlate   = PanelBrush(15, 23, 42);
        private static readonly SolidColorBrush BtnBg     = PanelBrush(23, 23, 28);
        private static readonly SolidColorBrush TextPri   = PanelBrush(220, 220, 220);
        private static readonly SolidColorBrush TextDim   = PanelBrush(120, 120, 140);

        // Accent
        private static readonly SolidColorBrush CyanFg    = PanelBrush(34, 211, 238);
        private static readonly SolidColorBrush CyanBg    = PanelBrush(22, 78, 99);
        private static readonly SolidColorBrush CyanBdr   = PanelBrush(8, 145, 178);

        // Green
        private static readonly SolidColorBrush GreenFg   = PanelBrush(74, 222, 128);
        private static readonly SolidColorBrush GreenBg   = PanelBrush(6, 78, 59);
        private static readonly SolidColorBrush GreenBdr  = PanelBrush(5, 150, 105);

        // Red
        private static readonly SolidColorBrush RedFg     = PanelBrush(252, 165, 165);
        private static readonly SolidColorBrush RedBg     = PanelBrush(127, 29, 29);
        private static readonly SolidColorBrush RedBdr    = PanelBrush(220, 38, 38);

        // Orange
        private static readonly SolidColorBrush OrangeFg  = PanelBrush(251, 146, 60);
        private static readonly SolidColorBrush OrangeBg  = PanelBrush(124, 45, 18);
        private static readonly SolidColorBrush OrangeBdr = PanelBrush(234, 88, 12);

        // Yellow
        private static readonly SolidColorBrush YellowFg  = PanelBrush(250, 204, 21);
        private static readonly SolidColorBrush YellowBg  = PanelBrush(113, 63, 18);
        private static readonly SolidColorBrush YellowBdr = PanelBrush(202, 138, 4);

        // Pink
        private static readonly SolidColorBrush PinkFg    = PanelBrush(244, 114, 182);
        private static readonly SolidColorBrush PinkBg    = PanelBrush(131, 24, 67);
        private static readonly SolidColorBrush PinkBdr   = PanelBrush(219, 39, 119);

        // Purple (RMA accent)
        private static readonly SolidColorBrush PurpleFg  = PanelBrush(168, 85, 247);

        #endregion
    }
}
