using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace PPEAccounting.Ui
{
    /// <summary>
    /// Кнопка с закруглёнными углами. Состояние «выбрана» хранится в свойстве,
    /// а не в Tag, поэтому наведение мыши не сбрасывает подсветку активного пункта меню
    /// и не может уронить приложение приведением типа.
    /// </summary>
    internal sealed class RoundedButton : Button
    {
        private bool _hovered;
        private bool _selected;

        public RoundedButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            UseVisualStyleBackColor = false;
            Cursor = Cursors.Hand;
            Font = UiTheme.ButtonFont;
            ForeColor = Color.White;
            BorderRadius = 8;
        }

        public int BorderRadius { get; set; }
        public Color HoverColor { get; set; } = Color.Empty;
        public Color SelectedColor { get; set; } = Color.Empty;

        public bool Selected
        {
            get { return _selected; }
            set
            {
                if (_selected == value)
                    return;

                _selected = value;
                Invalidate();
            }
        }

        private Color CurrentColor
        {
            get
            {
                if (_selected && SelectedColor != Color.Empty)
                    return SelectedColor;

                if (_hovered && HoverColor != Color.Empty)
                    return HoverColor;

                return BackColor;
            }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hovered = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hovered = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            var graphics = pevent.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Parent != null ? Parent.BackColor : SystemColors.Control);

            var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            if (bounds.Width <= 0 || bounds.Height <= 0)
                return;

            var color = Enabled ? CurrentColor : Color.FromArgb(175, 175, 175);

            using (var brush = new SolidBrush(color))
            {
                if (BorderRadius > 0)
                {
                    using (var path = CreateRoundedPath(bounds, BorderRadius))
                        graphics.FillPath(brush, path);
                }
                else
                {
                    graphics.FillRectangle(brush, bounds);
                }
            }

            var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis;

            switch (TextAlign)
            {
                case ContentAlignment.TopLeft:
                case ContentAlignment.MiddleLeft:
                case ContentAlignment.BottomLeft:
                    flags |= TextFormatFlags.Left;
                    break;
                case ContentAlignment.TopRight:
                case ContentAlignment.MiddleRight:
                case ContentAlignment.BottomRight:
                    flags |= TextFormatFlags.Right;
                    break;
                default:
                    flags |= TextFormatFlags.HorizontalCenter;
                    break;
            }

            var textBounds = new Rectangle(
                Padding.Left,
                0,
                Math.Max(0, Width - Padding.Left - Padding.Right),
                Height);

            TextRenderer.DrawText(graphics, Text, Font, textBounds,
                Enabled ? ForeColor : Color.Gainsboro, flags);
        }

        private static GraphicsPath CreateRoundedPath(Rectangle bounds, int radius)
        {
            var path = new GraphicsPath();
            var diameter = Math.Min(Math.Min(radius * 2, bounds.Width), bounds.Height);

            if (diameter <= 1)
            {
                path.AddRectangle(bounds);
                return path;
            }

            var arc = new Rectangle(bounds.Location, new Size(diameter, diameter));

            path.AddArc(arc, 180, 90);

            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);

            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);

            arc.X = bounds.Left;
            path.AddArc(arc, 90, 90);

            path.CloseFigure();
            return path;
        }
    }
}
