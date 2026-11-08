using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace BootLauncher
{
    public class CheckLabel : Control
    {
        const int Box = 20, Gap = 10;
        static readonly Color Accent = Theme.Accent;
        static readonly Color AccentDark = Theme.AccentLo;

        string _text;
        bool _checked, _hover;

        public string TextContent
        {
            get { return _text; }
            set { _text = value; UpdateSize(); Invalidate(); }
        }

        public bool Checked
        {
            get { return _checked; }
            set { _checked = value; Invalidate(); }
        }

        public CheckLabel(string text)
        {
            _text = text;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Color.FromArgb(0x1A, 0x1A, 0x1A);
            ForeColor = Color.FromArgb(0xE8, 0xE8, 0xE8);
            Font = new Font("Segoe UI", 11F);
            Cursor = Cursors.Hand;
            UpdateSize();
        }

        public void RefreshText()
        {
            UpdateSize();
            Invalidate();
        }

        void UpdateSize()
        {
            var sz = TextRenderer.MeasureText(_text ?? "", Font);
            Height = Math.Max(24, sz.Height + 4);
            Width = Box + Gap + sz.Width + 2;
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            UpdateSize();
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnClick(EventArgs e)
        {
            Checked = !Checked;
            base.OnClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var box = new Rectangle(0, (Height - Box) / 2, Box, Box);
            using (var path = Round(box, 6))
            {
                if (_checked)
                {
                    using (var b = new LinearGradientBrush(box, Accent, AccentDark, 90f))
                        g.FillPath(b, path);
                    using (var pen = new Pen(Color.White, 2.2f))
                    {
                        g.DrawLine(pen, box.Left + 5f, box.Top + 10f, box.Left + 8.5f, box.Top + 13.5f);
                        g.DrawLine(pen, box.Left + 8.5f, box.Top + 13.5f, box.Left + 15f, box.Top + 6.5f);
                    }
                }
                else
                {
                    using (var b = new SolidBrush(_hover ? Color.FromArgb(0x2E, 0x2E, 0x2E) : Color.FromArgb(0x26, 0x26, 0x26)))
                        g.FillPath(b, path);
                    using (var pen = new Pen(_hover ? Color.FromArgb(0x6A, 0x6A, 0x6A) : Color.FromArgb(0x4A, 0x4A, 0x4A), 1.5f))
                        g.DrawPath(pen, path);
                }
            }
            var textRect = new Rectangle(Box + Gap, 0, Width - Box - Gap, Height);
            TextRenderer.DrawText(g, _text, Font, textRect, ForeColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        static GraphicsPath Round(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}