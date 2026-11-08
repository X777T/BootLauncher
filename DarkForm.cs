using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace BootLauncher
{
    public class DarkForm : Form
    {
        bool _dwmRounded;
        readonly Timer _fade = new Timer { Interval = 12 };

        public DarkForm()
        {
            BackColor = Theme.Surface;
            ForeColor = Theme.Text;
            Font = new Font("Segoe UI", 9F);
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            KeyPreview = true;
            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };

            // 打开时淡入，比直接“啪”地出现柔和
            Opacity = 0;
            _fade.Tick += (s, e) =>
            {
                double next = Opacity + 0.14;
                if (next >= 1) { Opacity = 1; _fade.Stop(); }
                else Opacity = next;
            };
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            _dwmRounded = UiKit.RoundWindow(Handle, Theme.Border);
            if (_dwmRounded) Region = null;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            UpdateRegion();
            _fade.Start();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateRegion();
        }

        protected void UpdateRegion()
        {
            if (_dwmRounded) return;
            try
            {
                int r = (int)Math.Round(14f * DeviceDpi / 96f);
                Region = UiRounded.Region(new Rectangle(0, 0, Width, Height), r);
            }
            catch { }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (_dwmRounded) return; // 边框由 DWM 画
            using (var pen = new Pen(Theme.Border, 1f))
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == 0x84)
                m.Result = (IntPtr)2;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _fade.Dispose();
            base.Dispose(disposing);
        }

        protected TextBox MakeTextBox(Rectangle bounds)
        {
            var tb = new FieldTextBox
            {
                BorderStyle = BorderStyle.None,
                BackColor = Theme.Card,
                ForeColor = Theme.Text,
                Font = Font
            };
            // bounds 是输入框整体（含圆角底板）的位置；文本框本体在里面留出内边距
            tb.Bounds = new Rectangle(bounds.X + 12, 0, Math.Max(20, bounds.Width - 24), tb.PreferredHeight);
            tb.Top = bounds.Y + (bounds.Height - tb.Height) / 2;
            return tb;
        }

        protected FlatButton MakeButton(string text, Rectangle bounds, bool primary)
        {
            return new FlatButton
            {
                ButtonText = text,
                Bounds = bounds,
                Font = Font,
                Radius = 10,
                ForeColor = Theme.Text,
                NormalColor = primary ? Theme.Accent : Theme.Card,
                HoverColor = primary ? Theme.AccentHi : Theme.CardHover,
                DownColor = primary ? Theme.AccentLo : Color.FromArgb(24, 25, 31)
            };
        }

        protected Label MakeLabel(string text, Rectangle bounds)
        {
            return new Label
            {
                Text = text,
                Bounds = bounds,
                ForeColor = Color.FromArgb(200, 203, 210),
                TextAlign = ContentAlignment.MiddleLeft,
                Font = Font
            };
        }

        protected Label MakeDragLabel(string text, Rectangle bounds)
        {
            return new DragThroughLabel
            {
                Text = text,
                Bounds = bounds,
                ForeColor = Color.FromArgb(200, 203, 210),
                TextAlign = ContentAlignment.MiddleLeft,
                Font = Font
            };
        }
    }

    static class UiRounded
    {
        public static Region Region(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return new Region(path);
        }
    }

    // 无边框文本框 + 身后自动挂一块圆角底板（聚焦时描边变强调色），看起来就是现代的圆角输入框。
    // 调用方照旧把它当 TextBox 用，不需要关心底板。
    class FieldTextBox : TextBox
    {
        FieldBack _back;

        float Dp { get { return DeviceDpi / 96f; } }

        protected override void OnParentChanged(EventArgs e)
        {
            base.OnParentChanged(e);
            if (Parent == null) return;
            if (_back == null)
            {
                _back = new FieldBack(this);
                LocationChanged += (s, a) => Sync();
                SizeChanged += (s, a) => Sync();
                VisibleChanged += (s, a) => Sync();
                GotFocus += (s, a) => _back.Invalidate();
                LostFocus += (s, a) => _back.Invalidate();
            }
            if (_back.Parent != Parent)
            {
                _back.BackColor = Parent.BackColor;
                Parent.Controls.Add(_back);
            }
            Parent.Controls.SetChildIndex(_back, Parent.Controls.GetChildIndex(this) + 1);
            Sync();
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            Sync();
        }

        void Sync()
        {
            if (_back == null) return;
            int px = (int)Math.Round(12 * Dp), py = (int)Math.Round(7 * Dp);
            _back.Bounds = new Rectangle(Left - px, Top - py, Width + px * 2, Height + py * 2);
            _back.Visible = Visible;
            _back.Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _back != null) _back.Dispose();
            base.Dispose(disposing);
        }

        class FieldBack : Control
        {
            readonly TextBox _tb;

            public FieldBack(TextBox tb)
            {
                _tb = tb;
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
                Cursor = Cursors.IBeam;
                TabStop = false;
            }

            // 底板尺寸完全由文本框推算，不参与窗体的自动缩放，避免高 DPI 下两者错位
            protected override void ScaleControl(SizeF factor, BoundsSpecified specified) { }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                base.OnMouseDown(e);
                _tb.Focus();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.Clear(BackColor);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                bool focus = _tb.Focused;
                var rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
                using (var path = UiKit.RoundRect(rect, 10 * DeviceDpi / 96f))
                {
                    using (var b = new SolidBrush(Theme.Card))
                        g.FillPath(b, path);
                    using (var pen = new Pen(focus ? Theme.Accent : Theme.Border, focus ? 1.6f : 1f))
                        g.DrawPath(pen, path);
                }
            }
        }
    }

    class DragThroughLabel : Label
    {
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == 0x84)
                m.Result = (IntPtr)(-1);
        }
    }

    class DarkColorTable : ProfessionalColorTable
    {
        static readonly Color Bg = Color.FromArgb(30, 32, 39);
        static readonly Color Sel = Color.FromArgb(48, 52, 64);
        static readonly Color Border = Color.FromArgb(44, 47, 56);
        public override Color ToolStripDropDownBackground { get { return Bg; } }
        public override Color ImageMarginGradientBegin { get { return Bg; } }
        public override Color ImageMarginGradientMiddle { get { return Bg; } }
        public override Color ImageMarginGradientEnd { get { return Bg; } }
        public override Color MenuItemSelected { get { return Sel; } }
        public override Color MenuItemSelectedGradientBegin { get { return Sel; } }
        public override Color MenuItemSelectedGradientEnd { get { return Sel; } }
        public override Color MenuItemBorder { get { return Sel; } }
        public override Color MenuBorder { get { return Border; } }
        public override Color MenuItemPressedGradientBegin { get { return Sel; } }
        public override Color MenuItemPressedGradientMiddle { get { return Sel; } }
        public override Color MenuItemPressedGradientEnd { get { return Sel; } }
        public override Color SeparatorDark { get { return Border; } }
        public override Color SeparatorLight { get { return Border; } }
        public override Color CheckBackground { get { return Sel; } }
        public override Color CheckSelectedBackground { get { return Sel; } }
        public override Color CheckPressedBackground { get { return Sel; } }
    }

    class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        public DarkMenuRenderer() : base(new DarkColorTable()) { RoundedEdges = false; }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = Color.FromArgb(0xC8, 0xC8, 0xC8);
            base.OnRenderArrow(e);
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = Theme.Text;
            base.OnRenderItemText(e);
        }

        // 选中项画成带圆角的柔和高亮，而不是整行方块
        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Selected && !e.Item.Pressed) return;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new RectangleF(2, 1, e.Item.Width - 4, e.Item.Height - 2);
            using (var p = UiKit.RoundRect(r, 6))
            using (var b = new SolidBrush(Color.FromArgb(48, 52, 64)))
                g.FillPath(b, p);
        }
    }
}
