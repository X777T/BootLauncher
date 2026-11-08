using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace BootLauncher
{
    public class FlatButton : Control
    {
        public string ButtonText { get; set; }
        public Color NormalColor { get; set; }
        public Color HoverColor { get; set; }
        public Color DownColor { get; set; }
        public int Radius { get; set; }
        public Color GradientColor { get; set; }
        public Color HoverForeColor { get; set; }

        bool _hover;
        bool _down;
        float _t; // 悬停动画进度 0..1
        readonly Timer _timer;

        public FlatButton()
        {
            NormalColor = Color.FromArgb(22, 119, 255);
            HoverColor = Color.FromArgb(64, 148, 255);
            DownColor = Color.FromArgb(16, 96, 220);
            Radius = 10;
            ForeColor = Color.White;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
            _timer = new Timer { Interval = 15 };
            _timer.Tick += (s, e) =>
            {
                if (!UiKit.Approach(ref _t, _hover ? 1f : 0f, 0.2f)) _timer.Stop();
                Invalidate();
            };
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _timer.Dispose();
            base.Dispose(disposing);
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; _timer.Start(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; _timer.Start(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            Cursor = Enabled ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Width < 2 || Height < 2) return;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new RectangleF(0, 0, Width - 1, Height - 1);
            Color fg = ForeColor;
            using (var path = UiKit.RoundRect(rect, Radius))
            {
                if (!Enabled)
                {
                    using (var brush = new SolidBrush(Color.FromArgb(0x2A, 0x2A, 0x30)))
                        g.FillPath(brush, path);
                    fg = Color.FromArgb(0x70, 0x70, 0x78);
                }
                else if (_down)
                {
                    using (var brush = new SolidBrush(DownColor))
                        g.FillPath(brush, path);
                }
                else if (GradientColor != Color.Empty)
                {
                    var top = UiKit.Lerp(NormalColor, HoverColor, _t);
                    var bottom = UiKit.Lerp(GradientColor, HoverColor, _t * 0.6f);
                    using (var b = new LinearGradientBrush(new RectangleF(0, -1, Width, Height + 2), top, bottom, 90f))
                        g.FillPath(b, path);
                }
                else
                {
                    using (var brush = new SolidBrush(UiKit.Lerp(NormalColor, HoverColor, _t)))
                        g.FillPath(brush, path);
                }
            }
            if (Enabled && HoverForeColor != Color.Empty)
                fg = UiKit.Lerp(ForeColor, HoverForeColor, _t);
            TextRenderer.DrawText(g, ButtonText, Font, ClientRectangle, fg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }
}
