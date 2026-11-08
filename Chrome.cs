using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace BootLauncher
{
    public abstract class HoverCtl : Control
    {
        protected float Hov;
        bool _in;
        readonly Timer _timer;

        protected HoverCtl()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
            _timer = new Timer { Interval = 15 };
            _timer.Tick += (s, e) =>
            {
                if (!UiKit.Approach(ref Hov, _in ? 1f : 0f, 0.18f)) _timer.Stop();
                Invalidate();
            };
        }

        protected int S(double v) { return (int)Math.Round(v * DeviceDpi / 96.0); }
        protected float Sf(double v) { return (float)(v * DeviceDpi / 96.0); }

        protected override void OnMouseEnter(EventArgs e) { _in = true; _timer.Start(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _in = false; _timer.Start(); base.OnMouseLeave(e); }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _timer.Dispose();
            base.Dispose(disposing);
        }
    }

    public enum WindowButtonKind { Minimize, Close }

    public class WindowButton : HoverCtl
    {
        public WindowButtonKind Kind { get; set; }
        public event EventHandler Activated;

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left && ClientRectangle.Contains(e.Location) && Activated != null)
                Activated(this, EventArgs.Empty);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var hoverBg = Kind == WindowButtonKind.Close ? Color.FromArgb(232, 17, 35) : Theme.CardHover;
            var rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            using (var path = UiKit.RoundRect(rect, Sf(8)))
            using (var brush = new SolidBrush(UiKit.Lerp(BackColor, hoverBg, Hov)))
                g.FillPath(brush, path);

            var fg = UiKit.Lerp(Theme.TextDim, Color.White, Hov);
            float cx = Width / 2f, cy = Height / 2f, h = Sf(5.5);
            using (var pen = new Pen(fg, Math.Max(1.4f, Sf(1.4))))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                if (Kind == WindowButtonKind.Close)
                {
                    g.DrawLine(pen, cx - h, cy - h, cx + h, cy + h);
                    g.DrawLine(pen, cx - h, cy + h, cx + h, cy - h);
                }
                else
                {
                    g.DrawLine(pen, cx - h, cy, cx + h, cy);
                }
            }
        }
    }

    public class Chip : HoverCtl
    {
        string _text = "";
        bool _active;
        public bool IsAdd { get; set; }
        public event EventHandler Activated;

        public string ChipText
        {
            get { return _text; }
            set { _text = value ?? ""; Fit(); Invalidate(); }
        }

        public bool Active
        {
            get { return _active; }
            set { _active = value; Invalidate(); }
        }

        public Chip()
        {
            Font = new Font("Segoe UI", 9.5F);
        }

        public void Fit()
        {
            Height = S(32);
            if (IsAdd) Width = S(38);
            else Width = TextRenderer.MeasureText(_text, Font, new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.NoPadding).Width + S(30);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left && ClientRectangle.Contains(e.Location) && Activated != null)
                Activated(this, EventArgs.Empty);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            using (var path = UiKit.RoundRect(rect, rect.Height / 2f))
            {
                Color fill = _active ? UiKit.Lerp(Theme.Accent, Theme.AccentHi, Hov)
                                     : UiKit.Lerp(Theme.Card, Theme.CardHover, Hov);
                using (var brush = new SolidBrush(fill))
                    g.FillPath(brush, path);
                if (!_active)
                    using (var pen = new Pen(Theme.Border, 1f))
                        g.DrawPath(pen, path);
            }

            if (IsAdd)
            {
                float cx = Width / 2f, cy = Height / 2f, h = Sf(5);
                using (var pen = new Pen(UiKit.Lerp(Theme.TextDim, Color.White, Hov), Sf(1.6)))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    g.DrawLine(pen, cx - h, cy, cx + h, cy);
                    g.DrawLine(pen, cx, cy - h, cx, cy + h);
                }
                return;
            }

            var fg = _active ? Color.White : UiKit.Lerp(Color.FromArgb(200, 203, 210), Color.White, Hov);
            TextRenderer.DrawText(g, _text, Font, ClientRectangle, fg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        }
    }

    public class SearchBox : Control
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        readonly TextBox _tb;
        bool _focus;
        string _cue = "";

        public event EventHandler QueryChanged;

        public SearchBox()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Surface;
            Cursor = Cursors.IBeam;
            _tb = new TextBox
            {
                BorderStyle = BorderStyle.None,
                BackColor = Theme.Card,
                ForeColor = Theme.Text,
                Font = new Font("Segoe UI", 10F)
            };
            _tb.GotFocus += (s, e) => { _focus = true; Invalidate(); };
            _tb.LostFocus += (s, e) => { _focus = false; Invalidate(); };
            _tb.TextChanged += (s, e) =>
            {
                Invalidate();
                if (QueryChanged != null) QueryChanged(this, EventArgs.Empty);
            };
            _tb.HandleCreated += (s, e) => ApplyCue();
            Controls.Add(_tb);
        }

        int S(double v) { return (int)Math.Round(v * DeviceDpi / 96.0); }

        public string Query { get { return _tb.Text; } }
        public bool HasText { get { return _tb.Text.Length > 0; } }

        public void SetCue(string cue) { _cue = cue ?? ""; ApplyCue(); }
        public void ClearText() { _tb.Clear(); }
        public void FocusInput() { _tb.Focus(); _tb.SelectAll(); }

        void ApplyCue()
        {
            if (_tb.IsHandleCreated) SendMessage(_tb.Handle, 0x1501, (IntPtr)1, _cue);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            _tb.Left = S(38);
            _tb.Width = Math.Max(10, Width - S(38) - S(34));
            _tb.Top = Math.Max(0, (Height - _tb.Height) / 2);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left) return;
            if (HasText && e.X >= Width - S(32)) _tb.Clear();
            _tb.Focus();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            using (var path = UiKit.RoundRect(rect, rect.Height / 2f))
            {
                using (var brush = new SolidBrush(Theme.Card))
                    g.FillPath(brush, path);
                using (var pen = new Pen(_focus ? Theme.Accent : Theme.Border, _focus ? 1.5f : 1f))
                    g.DrawPath(pen, path);
            }

            var ic = _focus ? Theme.Text : Theme.TextDim;
            float r = S(5.5), cx = S(19), cy = Height / 2f - S(1);
            using (var pen = new Pen(ic, Math.Max(1.5f, S(1.5))))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                g.DrawEllipse(pen, cx - r, cy - r, r * 2, r * 2);
                float d = r * 0.7071f;
                g.DrawLine(pen, cx + d + 1, cy + d + 1, cx + d + S(4.5), cy + d + S(4.5));
            }

            if (HasText)
            {
                float x = Width - S(17), y = Height / 2f, h = S(3.5);
                using (var brush = new SolidBrush(Color.FromArgb(70, 255, 255, 255)))
                    g.FillEllipse(brush, x - S(8), y - S(8), S(16), S(16));
                using (var pen = new Pen(Theme.Text, Math.Max(1.4f, S(1.4))))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    g.DrawLine(pen, x - h, y - h, x + h, y + h);
                    g.DrawLine(pen, x - h, y + h, x + h, y - h);
                }
            }
        }
    }
}
