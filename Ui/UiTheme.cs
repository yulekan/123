using System.Drawing;
using System.Windows.Forms;

namespace PPEAccounting.Ui
{
    /// <summary>Единая палитра и шрифты. Раньше цвета дублировались в каждой форме.</summary>
    internal static class UiTheme
    {
        public static readonly Color Burgundy = Color.FromArgb(128, 0, 32);
        public static readonly Color BurgundyHover = Color.FromArgb(98, 0, 24);
        public static readonly Color DarkGrey = Color.FromArgb(33, 37, 41);
        public static readonly Color DarkGreyHover = Color.FromArgb(60, 60, 60);
        public static readonly Color SidebarHover = Color.FromArgb(52, 56, 60);
        public static readonly Color LightBackground = Color.FromArgb(240, 242, 245);
        public static readonly Color PanelBackground = Color.FromArgb(248, 249, 250);
        public static readonly Color GridLine = Color.FromArgb(230, 230, 230);
        public static readonly Color Success = Color.FromArgb(32, 115, 70);
        public static readonly Color Danger = Color.FromArgb(190, 40, 50);

        public static readonly Font BaseFont = new Font("Segoe UI", 10f);
        public static readonly Font PageTitleFont = new Font("Segoe UI", 20f, FontStyle.Bold);
        public static readonly Font SectionTitleFont = new Font("Segoe UI", 18f, FontStyle.Bold);
        public static readonly Font LogoFont = new Font("Segoe UI", 16f, FontStyle.Bold);
        public static readonly Font WelcomeFont = new Font("Segoe UI", 14f);
        public static readonly Font MenuFont = new Font("Segoe UI", 12f);
        public static readonly Font ButtonFont = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        public static readonly Font GridFont = new Font("Segoe UI", 10f);
        public static readonly Font InfoFont = new Font("Segoe UI", 10f, FontStyle.Italic);

        public static RoundedButton PrimaryButton(string text, int width = 130, int height = 38)
        {
            return new RoundedButton
            {
                Text = text,
                Width = width,
                Height = height,
                BackColor = Burgundy,
                HoverColor = BurgundyHover,
                BorderRadius = 8,
                Margin = new Padding(0, 0, 10, 0)
            };
        }

        public static RoundedButton SecondaryButton(string text, int width = 130, int height = 38)
        {
            return new RoundedButton
            {
                Text = text,
                Width = width,
                Height = height,
                BackColor = DarkGrey,
                HoverColor = DarkGreyHover,
                BorderRadius = 8,
                Margin = new Padding(0, 0, 10, 0)
            };
        }
    }
}
