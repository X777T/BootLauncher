using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace BootLauncher
{
    // 方案管理：重命名、删除、添加。重命名/删除直接改 config，调用方关闭后负责保存和刷新；
    // 点“添加方案”会关闭本窗口并通过 AddRequested 通知调用方走添加流程。
    public class SchemeManageForm : DarkForm
    {
        const int MaxSchemes = 10;

        public bool AddRequested { get; private set; }

        readonly Config _config;
        readonly SchemeList _list;
        readonly Label _title;

        public SchemeManageForm(Config config)
        {
            _config = config;

            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 11F);
            ClientSize = new Size(640, 620);

            _title = MakeDragLabel("", new Rectangle(30, 18, 580, 36));
            _title.Font = new Font("Segoe UI", 14F, FontStyle.Bold);
            _title.ForeColor = Color.White;
            Controls.Add(_title);

            _list = new SchemeList(_config.Schemes) { Bounds = new Rectangle(30, 66, 580, 454) };
            _list.RenameRequested += OnRename;
            _list.DeleteRequested += OnDelete;
            Controls.Add(_list);

            var add = MakeButton(Strings.T("AddScheme"), new Rectangle(30, 540, 150, 46), true);
            add.Click += (s, e) => OnAdd();
            Controls.Add(add);

            var close = MakeButton(Strings.T("Close"), new Rectangle(480, 540, 130, 46), false);
            close.Click += (s, e) => { DialogResult = DialogResult.OK; Close(); };
            Controls.Add(close);

            UpdateTitle();
        }

        void UpdateTitle()
        {
            _title.Text = Strings.T("ManageSchemes") + "  (" + _config.Schemes.Count + "/" + MaxSchemes + ")";
        }

        void OnRename(Scheme scheme)
        {
            string old = scheme.Name;
            using (var dlg = new PromptForm(Strings.T("Rename"), Strings.T("SchemeName"), old, n =>
            {
                if (string.IsNullOrEmpty(n)) return Strings.T("SchemeNameEmpty");
                foreach (var s in _config.Schemes)
                    if (!ReferenceEquals(s, scheme) &&
                        string.Equals(s.Name, n, StringComparison.CurrentCultureIgnoreCase))
                        return Strings.T("SchemeExists");
                return null;
            }))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                scheme.Name = dlg.Value;
                if (_config.CurrentScheme == old) _config.CurrentScheme = dlg.Value;
                _list.Reload();
            }
        }

        void OnDelete(Scheme scheme)
        {
            if (MessageBox.Show(this, string.Format(Strings.T("ConfirmDeleteScheme"), scheme.Name), "BootLauncher",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            _config.Schemes.Remove(scheme);
            if (_config.CurrentScheme == scheme.Name) _config.CurrentScheme = "";
            _list.Reload();
            UpdateTitle();
        }

        void OnAdd()
        {
            if (_config.Schemes.Count >= MaxSchemes)
            {
                MessageBox.Show(this, Strings.T("SchemeMax"), "BootLauncher",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            AddRequested = true;
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    // 自绘的方案列表：所有尺寸按 DPI 现算，名称过长自动省略，不会被固定像素裁掉。
    class SchemeList : Control
    {
        readonly List<Scheme> _items;
        readonly Font _nameFont = new Font("Segoe UI", 12.5F);
        readonly Font _subFont = new Font("Segoe UI", 10F);
        readonly Font _btnFont = new Font("Segoe UI", 10.5F);
        int _hotRow = -1, _hotBtn = -1; // _hotBtn: 0=重命名 1=删除
        float _scroll;

        public event Action<Scheme> RenameRequested;
        public event Action<Scheme> DeleteRequested;

        public SchemeList(List<Scheme> items)
        {
            _items = items;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Surface;
        }

        float Sc { get { return DeviceDpi / 96f; } }
        int RowH { get { return (int)Math.Round(78 * Sc); } }
        int Content { get { return _items.Count * RowH + (int)(8 * Sc); } }
        float MaxScroll { get { return Math.Max(0, Content - Height); } }

        public void Reload()
        {
            _scroll = Math.Max(0, Math.Min(_scroll, MaxScroll));
            _hotRow = _hotBtn = -1;
            Invalidate();
        }

        Rectangle CardRect(int i)
        {
            int m = (int)Math.Round(4 * Sc);
            int sbw = MaxScroll > 0 ? (int)Math.Round(10 * Sc) : 0;
            return new Rectangle(m, (int)Math.Round(i * RowH - _scroll) + m,
                Width - m * 2 - sbw, RowH - m * 2);
        }

        Rectangle BtnRect(int i, int which)
        {
            var c = CardRect(i);
            int bw = (int)Math.Round(88 * Sc), bh = (int)Math.Round(38 * Sc);
            int gap = (int)Math.Round(8 * Sc), pad = (int)Math.Round(16 * Sc);
            int y = c.Top + (c.Height - bh) / 2;
            int xDel = c.Right - pad - bw;
            return which == 1 ? new Rectangle(xDel, y, bw, bh)
                              : new Rectangle(xDel - gap - bw, y, bw, bh);
        }

        void HitTest(Point p, out int row, out int btn)
        {
            row = -1; btn = -1;
            for (int i = 0; i < _items.Count; i++)
            {
                if (!CardRect(i).Contains(p)) continue;
                row = i;
                if (BtnRect(i, 0).Contains(p)) btn = 0;
                else if (BtnRect(i, 1).Contains(p)) btn = 1;
                return;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int r, b;
            HitTest(e.Location, out r, out b);
            if (r != _hotRow || b != _hotBtn)
            {
                _hotRow = r; _hotBtn = b;
                Cursor = b >= 0 ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hotRow = _hotBtn = -1;
            Invalidate();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (MaxScroll <= 0) return;
            _scroll = Math.Max(0, Math.Min(_scroll - e.Delta / 120f * RowH * 0.6f, MaxScroll));
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left) return;
            int r, b;
            HitTest(e.Location, out r, out b);
            if (r < 0 || b < 0) return;
            var scheme = _items[r];
            if (b == 0 && RenameRequested != null) RenameRequested(scheme);
            else if (b == 1 && DeleteRequested != null) DeleteRequested(scheme);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            _scroll = Math.Max(0, Math.Min(_scroll, MaxScroll));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.Surface);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using (var path = UiKit.RoundRect(new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), 10 * Sc))
            using (var pen = new Pen(Theme.Border, 1f))
            {
                g.SetClip(path);
                DrawRows(g);
                g.ResetClip();
                g.DrawPath(pen, path);
            }
        }

        void DrawRows(Graphics g)
        {
            if (_items.Count == 0)
            {
                TextRenderer.DrawText(g, Strings.T("NoSchemes"), _nameFont, ClientRectangle, Theme.TextDim,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
                return;
            }

            float sc = Sc;
            for (int i = 0; i < _items.Count; i++)
            {
                var card = CardRect(i);
                if (card.Bottom < 0 || card.Top > Height) continue;
                var scheme = _items[i];
                bool hot = i == _hotRow;

                using (var p = UiKit.RoundRect(card, 10 * sc))
                using (var b = new SolidBrush(hot ? Theme.CardHover : Theme.Card))
                    g.FillPath(b, p);

                var rename = BtnRect(i, 0);
                var del = BtnRect(i, 1);

                int pad = (int)Math.Round(18 * sc);
                int textW = Math.Max(20, rename.Left - card.Left - pad - (int)Math.Round(12 * sc));
                int nameH = TextRenderer.MeasureText("Ag", _nameFont).Height;
                int subH = TextRenderer.MeasureText("Ag", _subFont).Height;
                int gap = (int)Math.Round(3 * sc);
                int blockH = nameH + gap + subH;
                int ty = card.Top + (card.Height - blockH) / 2;

                var flags = TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.EndEllipsis |
                            TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
                TextRenderer.DrawText(g, scheme.Name, _nameFont,
                    new Rectangle(card.Left + pad, ty, textW, nameH), Theme.Text, flags);
                int count = scheme.Apps == null ? 0 : scheme.Apps.Count;
                TextRenderer.DrawText(g, string.Format(Strings.T("SchemeAppsCount"), count), _subFont,
                    new Rectangle(card.Left + pad, ty + nameH + gap, textW, subH), Theme.TextDim, flags);

                DrawBtn(g, rename, Strings.T("Rename"), hot && _hotBtn == 0, false);
                DrawBtn(g, del, Strings.T("Delete"), hot && _hotBtn == 1, true);
            }

            if (MaxScroll > 0)
            {
                float trackH = Height - 12 * sc;
                float thumbH = Math.Max(36 * sc, trackH * Height / Content);
                float y = 6 * sc + (trackH - thumbH) * (_scroll / MaxScroll);
                var th = new RectangleF(Width - 10 * sc, y, 5 * sc, thumbH);
                using (var p = UiKit.RoundRect(th, th.Width / 2f))
                using (var b = new SolidBrush(Color.FromArgb(90, 255, 255, 255)))
                    g.FillPath(b, p);
            }
        }

        void DrawBtn(Graphics g, Rectangle r, string text, bool hot, bool danger)
        {
            Color fill = Color.FromArgb(0x3A, 0x3D, 0x47);
            Color fg = Theme.Text;
            if (hot) fill = danger ? Color.FromArgb(0xC4, 0x30, 0x30) : Color.FromArgb(0x4A, 0x4E, 0x5A);
            using (var p = UiKit.RoundRect(new RectangleF(r.X, r.Y, r.Width, r.Height), 9 * Sc))
            using (var b = new SolidBrush(fill))
                g.FillPath(b, p);
            TextRenderer.DrawText(g, text, _btnFont, r, fg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _nameFont.Dispose();
                _subFont.Dispose();
                _btnFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
