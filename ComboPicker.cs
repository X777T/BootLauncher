using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace BootLauncher
{
    public class ComboPicker : Control
    {
        static readonly Color Surface = Theme.Card;
        static readonly Color SurfaceHover = Theme.CardHover;
        static readonly Color Border = Theme.Border;

        int _selectedIndex = -1;
        bool _hover;

        public List<object> Items { get; private set; }
        public event EventHandler SelectedIndexChanged;

        public int SelectedIndex
        {
            get { return _selectedIndex; }
            set
            {
                if (_selectedIndex == value) return;
                _selectedIndex = value;
                Invalidate();
                if (SelectedIndexChanged != null) SelectedIndexChanged(this, EventArgs.Empty);
            }
        }

        public object SelectedItem
        {
            get { return _selectedIndex >= 0 && _selectedIndex < Items.Count ? Items[_selectedIndex] : null; }
            set { SelectedIndex = Items.IndexOf(value); }
        }

        public ComboPicker()
        {
            Items = new List<object>();
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Color.FromArgb(0x1A, 0x1A, 0x1A);
            ForeColor = Color.FromArgb(0xE8, 0xE8, 0xE8);
            Font = new Font("Segoe UI", 10F);
            Cursor = Cursors.Hand;
            Height = 30;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = Round(rect, 8))
            {
                using (var b = new SolidBrush(_hover ? SurfaceHover : Surface))
                    g.FillPath(b, path);
                using (var pen = new Pen(Border, 1f))
                    g.DrawPath(pen, path);
            }
            string text = SelectedItem != null ? SelectedItem.ToString() : "";
            var textRect = new Rectangle(12, 0, Width - 34, Height);
            TextRenderer.DrawText(g, text, Font, textRect, ForeColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            using (var pen = new Pen(Color.FromArgb(0x9A, 0x9A, 0x9A), 1.6f))
            {
                int cx = Width - 18, cy = Height / 2;
                g.DrawLine(pen, cx - 4, cy - 2, cx, cy + 2);
                g.DrawLine(pen, cx, cy + 2, cx + 4, cy - 2);
            }
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            if (Items.Count == 0) return;
            var menu = UiKit.NewMenu(Font);
            menu.MinimumSize = new Size(Width, 0);
            for (int i = 0; i < Items.Count; i++)
            {
                var item = new ToolStripMenuItem((i == _selectedIndex ? "✓  " : "      ") + Items[i]) { Tag = i };
                item.Click += (s, ev) => SelectedIndex = (int)((ToolStripMenuItem)s).Tag;
                menu.Items.Add(item);
            }
            menu.Show(this, new Point(0, Height + 2));
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