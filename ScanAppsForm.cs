using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace BootLauncher
{
    public class ScanAppsForm : DarkForm
    {
        readonly Config _config;
        readonly Panel _panel;
        readonly List<ScanAppRow> _rows = new List<ScanAppRow>();

        public ScanAppsForm(Config config)
        {
            _config = config;
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(760, 580);
            Font = new Font("Segoe UI", 11F);
            StartPosition = FormStartPosition.Manual;

            var title = MakeDragLabel(Strings.T("ScanTitle"), new Rectangle(40, 20, 680, 34));
            title.TextAlign = ContentAlignment.MiddleCenter;
            title.ForeColor = Color.White;
            title.Font = new Font("Segoe UI", 14F, FontStyle.Bold);
            Controls.Add(title);

            var info = MakeDragLabel("", new Rectangle(40, 60, 680, 28));
            info.TextAlign = ContentAlignment.MiddleCenter;
            Controls.Add(info);

            _panel = new SmoothPanel
            {
                Bounds = new Rectangle(40, 100, 680, 380),
                AutoScroll = true,
                BackColor = Theme.Bg,
                BorderStyle = BorderStyle.None
            };
            _panel.Resize += (s, e) => LayoutRows();
            Controls.Add(_panel);

            List<AppItem> found;
            try
            {
                UseWaitCursor = true;
                var existing = new HashSet<string>(
                    _config.Apps.Select(a => a.Path.ToLowerInvariant()));
                found = AppScanner.Scan()
                    .Where(a => !existing.Contains(a.Path.ToLowerInvariant()))
                    .ToList();
            }
            finally
            {
                UseWaitCursor = false;
            }

            float scale = 1f;
            try
            {
                using (var g = CreateGraphics())
                    scale = g.DpiX / 96f;
            }
            catch { }
            int iconSize = Math.Max(32, (int)Math.Round(48 * scale));

            foreach (var app in found)
            {
                Image img = null;
                try
                {
                    img = ShellIcon.GetLargeIcon(app.Path);
                    if (img == null)
                    {
                        using (var ico = Icon.ExtractAssociatedIcon(app.Path))
                        {
                            if (ico != null) img = ico.ToBitmap();
                        }
                    }
                    if (img == null) img = SystemIcons.Application.ToBitmap();
                    var row = new ScanAppRow(app, ShellIcon.Resize(img, iconSize));
                    _rows.Add(row);
                    _panel.Controls.Add(row);
                }
                catch { }
                finally
                {
                    if (img != null) img.Dispose();
                }
            }
            info.Text = string.Format(Strings.T("FoundApps"), found.Count);

            var selectAll = MakeButton(Strings.T("SelectAll"), new Rectangle(40, 502, 110, 40), false);
            selectAll.Click += (s, e) => SetAll(true);
            Controls.Add(selectAll);

            var clearAll = MakeButton(Strings.T("ClearAll"), new Rectangle(158, 502, 110, 40), false);
            clearAll.Click += (s, e) => SetAll(false);
            Controls.Add(clearAll);

            var add = MakeButton(Strings.T("Add"), new Rectangle(484, 502, 110, 40), true);
            add.Click += (s, e) => OnAdd();
            Controls.Add(add);

            var cancel = MakeButton(Strings.T("Cancel"), new Rectangle(602, 502, 118, 40), false);
            cancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(cancel);

            Shown += (s, e) => LayoutRows();
        }

        void LayoutRows()
        {
            int scrollbar = _panel.VerticalScroll.Visible ? SystemInformation.VerticalScrollBarWidth : 0;
            int w = Math.Max(100, _panel.ClientSize.Width - 2 - scrollbar);
            for (int i = 0; i < _rows.Count; i++)
            {
                _rows[i].Location = new Point(1, 1 + i * _rows[i].Height);
                _rows[i].Width = w;
            }
            _panel.AutoScrollMinSize = new Size(0, _rows.Count == 0 ? 0 : _rows.Count * _rows[0].Height + 2);
        }

        void SetAll(bool value)
        {
            foreach (var row in _rows)
            {
                row.Checked = value;
                row.Invalidate();
            }
        }

        void OnAdd()
        {
            var picked = _rows.Where(r => r.Checked).Select(r => r.App).ToList();
            if (picked.Count == 0)
            {
                MessageBox.Show(this, Strings.T("NoneSelected"), "BootLauncher",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            foreach (var app in picked) _config.Apps.Add(app);
            DialogResult = DialogResult.OK;
            Close();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (Owner == null) return;
            try
            {
                int headerPhys = (int)Math.Round(56f * Owner.DeviceDpi / 96f);
                int x = Owner.Left + (Owner.Width - Width) / 2;
                int y = Math.Max(
                    Owner.Top + (Owner.Height - Height) / 2,
                    Owner.Top + headerPhys + 8);
                Location = new Point(x, y);
            }
            catch { }
        }
    }

    class SmoothPanel : Panel
    {
        public SmoothPanel()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            BackColor = Theme.Bg;
            BorderStyle = BorderStyle.None;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            UiKit.DarkScroll(Handle);
        }
    }

    class ScanAppRow : Control
    {
        public AppItem App { get; private set; }
        public bool Checked { get; set; }
        // 行右侧的小标记（例如应用当前所属的分类名），为空则不显示
        public string Badge { get; set; }
        bool _hover;
        readonly Image _icon;
        readonly Font _badgeFont = new Font("Segoe UI", 9.5F);

        public ScanAppRow(AppItem app, Image icon)
        {
            App = app;
            _icon = icon;
            Height = 64;
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = new Font("Segoe UI", 11F);
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            Checked = !Checked;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float k = Height / 64f;

            // 圆角卡片行：悬停浅色、选中淡蓝底 + 蓝色描边
            var card = new RectangleF(4 * k, 2 * k, Width - 8 * k, Height - 4 * k);
            if (Checked || _hover)
            {
                using (var p = UiKit.RoundRect(card, 12 * k))
                {
                    using (var b = new SolidBrush(Checked ? Color.FromArgb(27, 48, 84) : Theme.Card))
                        g.FillPath(b, p);
                    if (Checked)
                        using (var pen = new Pen(Color.FromArgb(120, Theme.Accent), 1.2f))
                            g.DrawPath(pen, p);
                }
            }

            // 圆形勾选框
            float cs = 22 * k;
            var cr = new RectangleF(18 * k, (Height - cs) / 2f, cs, cs);
            if (Checked)
            {
                using (var b = new SolidBrush(Theme.Accent))
                    g.FillEllipse(b, cr);
                using (var p = new Pen(Color.White, Math.Max(1.6f, 2f * k)))
                {
                    p.StartCap = LineCap.Round;
                    p.EndCap = LineCap.Round;
                    p.LineJoin = LineJoin.Round;
                    g.DrawLines(p, new[]
                    {
                        new PointF(cr.X + cs * 0.28f, cr.Y + cs * 0.52f),
                        new PointF(cr.X + cs * 0.44f, cr.Y + cs * 0.68f),
                        new PointF(cr.X + cs * 0.74f, cr.Y + cs * 0.34f)
                    });
                }
            }
            else
            {
                using (var p = new Pen(_hover ? Color.FromArgb(120, 124, 136) : Color.FromArgb(78, 82, 94), Math.Max(1.2f, 1.6f * k)))
                    g.DrawEllipse(p, cr.X + 0.8f, cr.Y + 0.8f, cr.Width - 1.6f, cr.Height - 1.6f);
            }

            // 图标：圆角方块底 + 图标本身 1:1 贴图，不再用 SetClip（无抗锯齿，边缘会有锯齿）
            int tile = Height - (int)(18 * k);
            var tr = new RectangleF(56 * k, (Height - tile) / 2f, tile, tile);
            using (var p = UiKit.RoundRect(tr, 11 * k))
            using (var b = new SolidBrush(Theme.Card))
                g.FillPath(b, p);
            int inner = (int)(tile * 0.78f);
            var ir = new Rectangle((int)Math.Round(tr.X + (tile - inner) / 2f),
                (int)Math.Round(tr.Y + (tile - inner) / 2f), inner, inner);
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(_icon, ir);
            g.PixelOffsetMode = PixelOffsetMode.Default;

            int textX = (int)(tr.Right + 16 * k);
            int rightLimit = Width - (int)(24 * k);

            // 右侧的分类小标签（胶囊）
            if (!string.IsNullOrEmpty(Badge))
            {
                var flagsB = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
                int maxW = Math.Max(40, (int)((Width - textX) * 0.4f));
                var sz = TextRenderer.MeasureText(g, Badge, _badgeFont, new Size(maxW, Height), flagsB);
                int padX = (int)(12 * k);
                float bw = Math.Min(maxW, sz.Width) + padX * 2;
                float bh = sz.Height + 8 * k;
                var pill = new RectangleF(rightLimit - bw, (Height - bh) / 2f, bw, bh);
                using (var p = UiKit.RoundRect(pill, bh / 2f))
                {
                    using (var b = new SolidBrush(Color.FromArgb(Checked ? 60 : 255, Checked ? Theme.Accent : Theme.Border)))
                        g.FillPath(b, p);
                }
                TextRenderer.DrawText(g, Badge, _badgeFont,
                    new Rectangle((int)pill.X + padX, (int)pill.Y, (int)(bw - padX * 2), (int)bh),
                    Checked ? Theme.AccentHi : Theme.TextDim,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | flagsB);
                rightLimit = (int)pill.X - (int)(12 * k);
            }

            TextRenderer.DrawText(g, App.Name, Font,
                new Rectangle(textX, 0, Math.Max(10, rightLimit - textX), Height),
                Theme.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        }

        static GraphicsPath RoundedPath(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
