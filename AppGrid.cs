using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Windows.Forms;

namespace BootLauncher
{
    // 应用网格：整个网格是一个自绘控件，所有图标在同一块缓冲里绘制并自己做平滑滚动，
    // 没有子控件，也就没有“滚动时子控件被位图平移留下残影”的问题。
    // 图标不再用 SetClip 裁成圆（GDI+ 的裁剪区没有抗锯齿，边缘锯齿的来源），
    // 而是直接画在抗锯齿的圆角卡片上。
    public class AppGrid : Control
    {
        const float Gap = 14f, Pad = 22f, NameH = 28f, CellPad = 10f;

        class Tile
        {
            public AppItem App;
            public bool IsAdd;
            public string IconPath;
            public Image Src;
            public Bitmap Cache;
            public int CacheSize;
            public bool Selected, InView = true, Dying, Fresh = true;
            public bool HovT, PressT;
            public float X, Y, TX, TY;
            public float Hov, SelP, Press, Vis = 1f, Intro, Delay;
        }

        readonly List<Tile> _tiles = new List<Tile>();
        readonly Timer _timer = new Timer { Interval = 10 };
        readonly Stopwatch _sw = Stopwatch.StartNew();
        readonly ImageAttributes _ia = new ImageAttributes();
        readonly Font _nameFont = new Font("Segoe UI", 9.75F);
        readonly Font _hintFont = new Font("Segoe UI", 11F);

        long _lastTicks;
        float _scroll, _scrollT, _contentH;
        Tile _hot, _pressed, _drag;
        bool _dragging, _sbDrag, _sbHover;
        Point _downPt, _lastMouse;
        PointF _grab;
        int _dragIndex, _iconL = 128;
        float _sbDragY, _sbDragScroll, _sbAlpha, _sbIdle = 9f;
        string _query = "";
        string _cat; // null=全部，""=未分类，其余=分类名

        public string AddText { get; set; }
        public string HintText { get; set; }
        public string NoResultsText { get; set; }

        public event EventHandler SelectionChanged;
        public event EventHandler AddRequested;
        public event Action<List<AppItem>> Reordered;
        public event Action<AppItem, Point> AppContext;

        public AppGrid()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.Opaque, true);
            BackColor = Theme.Bg;
            AddText = "Add app";
            HintText = "";
            NoResultsText = "";
            _timer.Tick += (s, e) => Tick();
        }

        float Sc { get { return DeviceDpi / 96f; } }
        float Side { get { return _iconL * Sc; } }
        float CellW { get { return Side + CellPad * 2 * Sc; } }
        float CellH { get { return Side + 8 * Sc + NameH * Sc; } }
        float MaxScroll { get { return Math.Max(0f, _contentH - Height); } }
        bool SearchActive { get { return _query.Length > 0; } }
        // 搜索或分类过滤生效时，显示的不是完整列表，此时禁用拖拽排序
        bool Filtering { get { return _query.Length > 0 || _cat != null; } }

        public int SelectedCount
        {
            get { return _tiles.Count(t => !t.IsAdd && !t.Dying && t.Selected); }
        }

        public int IconSize
        {
            get { return _iconL; }
            set
            {
                if (value == _iconL) return;
                _iconL = value;
                ResetCaches();
                Layout(false);
            }
        }

        // ---------- 数据 ----------

        public void SetApps(IList<AppItem> apps, ICollection<string> selection)
        {
            var old = _tiles.Where(t => !t.IsAdd && !t.Dying).ToList();
            var ordered = new List<Tile>();
            int fresh = 0;
            foreach (var app in apps)
            {
                var t = old.Find(x => ReferenceEquals(x.App, app));
                if (t != null)
                {
                    old.Remove(t);
                    if (t.IconPath != app.Path) LoadIcon(t);
                }
                else
                {
                    t = new Tile { App = app, Delay = Math.Min(fresh, 24) * 0.025f };
                    fresh++;
                    LoadIcon(t);
                }
                if (selection != null) t.Selected = selection.Contains(app.Name);
                if (t.Fresh) t.SelP = t.Selected ? 1f : 0f;
                ordered.Add(t);
            }
            foreach (var t in old) t.Dying = true;

            var add = _tiles.Find(t => t.IsAdd) ?? new Tile { IsAdd = true, Delay = Math.Min(fresh, 24) * 0.025f };
            var dying = _tiles.Where(t => !t.IsAdd && t.Dying).ToList();
            _tiles.Clear();
            _tiles.AddRange(ordered);
            _tiles.AddRange(dying);
            _tiles.Add(add);
            ApplyFilterFlags();
            Layout(false);
            RaiseSelection();
        }

        public List<AppItem> SelectedApps()
        {
            return _tiles.Where(t => !t.IsAdd && !t.Dying && t.Selected).Select(t => t.App).ToList();
        }

        public void SetSelection(ICollection<string> names)
        {
            foreach (var t in _tiles)
                if (!t.IsAdd) t.Selected = names.Contains(t.App.Name);
            EnsureTimer();
            RaiseSelection();
        }

        public void SelectAll(bool value)
        {
            foreach (var t in _tiles)
                if (!t.IsAdd && !t.Dying && t.InView) t.Selected = value;
            EnsureTimer();
            RaiseSelection();
        }

        public void SelectApp(AppItem app, bool value)
        {
            var t = _tiles.Find(x => ReferenceEquals(x.App, app));
            if (t == null) return;
            t.Selected = value;
            EnsureTimer();
            RaiseSelection();
        }

        public void ScrollTo(AppItem app)
        {
            var t = _tiles.Find(x => ReferenceEquals(x.App, app));
            if (t == null) return;
            float pad = 14 * Sc;
            if (t.TY - pad < _scrollT) _scrollT = t.TY - pad;
            else if (t.TY + CellH + pad > _scrollT + Height) _scrollT = t.TY + CellH + pad - Height;
            _scrollT = Math.Max(0, Math.Min(_scrollT, MaxScroll));
            EnsureTimer();
        }

        public void SetFilter(string query)
        {
            _query = (query ?? "").Trim();
            ApplyFilterFlags();
            _scrollT = 0;
            Layout(false);
        }

        public void SetCategory(string cat)
        {
            if (cat == _cat) return;
            _cat = cat;
            Refilter();
            _scrollT = 0;
        }

        // 应用的分类被改动后调用，按当前搜索 + 分类重新决定谁可见
        public void Refilter()
        {
            ApplyFilterFlags();
            Layout(false);
        }

        void ApplyFilterFlags()
        {
            foreach (var t in _tiles)
            {
                if (t.IsAdd) { t.InView = !SearchActive; continue; }
                bool matchSearch = !SearchActive ||
                    (t.App.Name ?? "").IndexOf(_query, StringComparison.CurrentCultureIgnoreCase) >= 0;
                bool matchCat = _cat == null ||
                    (_cat.Length == 0 ? string.IsNullOrEmpty(t.App.Category) : t.App.Category == _cat);
                t.InView = matchSearch && matchCat;
            }
        }

        void RaiseSelection()
        {
            if (SelectionChanged != null) SelectionChanged(this, EventArgs.Empty);
        }

        void LoadIcon(Tile t)
        {
            if (t.Src != null) t.Src.Dispose();
            if (t.Cache != null) { t.Cache.Dispose(); t.Cache = null; }
            t.IconPath = t.App.Path;
            Image source = null;
            try
            {
                source = ShellIcon.GetLargeIcon(t.App.Path);
                if (source == null)
                {
                    using (var icon = Icon.ExtractAssociatedIcon(t.App.Path))
                        if (icon != null) source = icon.ToBitmap();
                }
            }
            catch { }
            if (source == null) source = SystemIcons.Application.ToBitmap();
            t.Src = source;
        }

        void ResetCaches()
        {
            foreach (var t in _tiles)
                if (t.Cache != null) { t.Cache.Dispose(); t.Cache = null; }
        }

        // ---------- 布局 ----------

        void Metrics(out int cols, out float left, out float top)
        {
            float cw = CellW, gap = Gap * Sc, pad = Pad * Sc;
            cols = Math.Max(1, (int)Math.Floor((Width - 2 * pad + gap) / (cw + gap)));
            float used = cols * cw + (cols - 1) * gap;
            left = Math.Max(0f, (Width - used) / 2f);
            top = 14 * Sc;
        }

        List<Tile> OrderedApps()
        {
            var apps = _tiles.Where(t => !t.IsAdd && !t.Dying && t.InView).ToList();
            if (_drag != null && apps.Remove(_drag))
                apps.Insert(Math.Max(0, Math.Min(_dragIndex, apps.Count)), _drag);
            return apps;
        }

        new void Layout(bool snap)
        {
            int cols; float left, top;
            Metrics(out cols, out left, out top);
            float cw = CellW, ch = CellH, gap = Gap * Sc;

            var lay = OrderedApps();
            int appCount = lay.Count;
            var add = _tiles.Find(t => t.IsAdd);
            if (add != null && add.InView) lay.Add(add);

            for (int i = 0; i < lay.Count; i++)
            {
                var t = lay[i];
                t.TX = left + (i % cols) * (cw + gap);
                t.TY = top + (i / cols) * (ch + gap);
            }
            bool empty = appCount == 0 && add != null && add.InView;
            if (empty)
            {
                add.TX = (Width - cw) / 2f;
                add.TY = Math.Max(top, (Height - ch) / 2f - 50 * Sc);
            }
            int rows = (lay.Count + cols - 1) / cols;
            _contentH = top + rows * (ch + gap) + 10 * Sc;

            foreach (var t in _tiles)
            {
                if (snap || t.Fresh)
                {
                    t.X = t.TX; t.Y = t.TY;
                    t.Fresh = false;
                }
            }
            _scrollT = Math.Max(0, Math.Min(_scrollT, MaxScroll));
            if (snap) _scroll = Math.Max(0, Math.Min(_scroll, MaxScroll));
            EnsureTimer();
            Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Layout(true);
        }

        protected override void OnDpiChangedAfterParent(EventArgs e)
        {
            base.OnDpiChangedAfterParent(e);
            ResetCaches();
            Layout(true);
        }

        // ---------- 动画 ----------

        void EnsureTimer()
        {
            if (_timer.Enabled) return;
            _lastTicks = _sw.ElapsedTicks;
            _timer.Start();
        }

        void Tick()
        {
            long now = _sw.ElapsedTicks;
            float dt = (float)(now - _lastTicks) / Stopwatch.Frequency;
            _lastTicks = now;
            dt = Math.Max(0.001f, Math.Min(dt, 0.05f));
            bool busy = false;

            if (_dragging)
            {
                float edge = 70 * Sc;
                float py = _lastMouse.Y;
                float speed = 0;
                if (py < edge) speed = -(1f - Math.Max(0, py) / edge);
                else if (py > Height - edge) speed = 1f - Math.Max(0, Height - py) / edge;
                if (Math.Abs(speed) > 0.01f)
                {
                    float next = Math.Max(0, Math.Min(_scroll + speed * 900 * Sc * dt, MaxScroll));
                    _scroll = _scrollT = next;
                    UpdateDrag(_lastMouse);
                    _sbIdle = 0;
                    busy = true;
                }
            }

            if (Math.Abs(_scrollT - _scroll) > 0.3f)
            {
                _scroll += (_scrollT - _scroll) * (1f - (float)Math.Exp(-dt * 16f));
                _sbIdle = 0;
                busy = true;
            }
            else if (_scroll != _scrollT) { _scroll = _scrollT; busy = true; }

            float k = 1f - (float)Math.Exp(-dt * 18f);
            for (int i = _tiles.Count - 1; i >= 0; i--)
            {
                var t = _tiles[i];
                if (t.Delay > 0f) { t.Delay -= dt; busy = true; continue; }
                busy |= UiKit.Approach(ref t.Hov, t.HovT ? 1f : 0f, dt / 0.15f);
                busy |= UiKit.Approach(ref t.SelP, t.Selected ? 1f : 0f, dt / 0.28f);
                busy |= UiKit.Approach(ref t.Press, t.PressT ? 1f : 0f, dt / 0.08f);
                busy |= UiKit.Approach(ref t.Intro, 1f, dt / 0.38f);
                busy |= UiKit.Approach(ref t.Vis, (t.InView && !t.Dying) ? 1f : 0f, dt / 0.2f);
                if (t != _drag)
                {
                    float dx = t.TX - t.X, dy = t.TY - t.Y;
                    if (Math.Abs(dx) > 0.4f || Math.Abs(dy) > 0.4f)
                    {
                        t.X += dx * k; t.Y += dy * k;
                        busy = true;
                    }
                    else { t.X = t.TX; t.Y = t.TY; }
                }
                if (t.Dying && t.Vis <= 0.001f) _tiles.RemoveAt(i);
            }

            _sbIdle += dt;
            bool showBar = _sbIdle < 0.9f || _sbHover || _sbDrag;
            busy |= UiKit.Approach(ref _sbAlpha, showBar ? 1f : 0f, dt / 0.2f);
            if (showBar) busy = true;

            Invalidate();
            if (!busy) _timer.Stop();
        }

        // ---------- 鼠标 ----------

        Tile HitTest(Point p)
        {
            for (int i = _tiles.Count - 1; i >= 0; i--)
            {
                var t = _tiles[i];
                if (t.Dying || t.Vis < 0.5f || t.Intro < 0.3f || t.Delay > 0f) continue;
                var r = new RectangleF(t.X, t.Y - _scroll, CellW, CellH);
                if (r.Contains(p.X, p.Y)) return t;
            }
            return null;
        }

        void SetHot(Tile t)
        {
            if (_hot == t) return;
            if (_hot != null) _hot.HovT = false;
            _hot = t;
            if (_hot != null) _hot.HovT = true;
            Cursor = t != null ? Cursors.Hand : Cursors.Default;
            EnsureTimer();
        }

        RectangleF ThumbRect()
        {
            float trackTop = 6 * Sc, trackH = Height - 12 * Sc;
            float thumbH = Math.Max(36 * Sc, trackH * Height / Math.Max(1f, _contentH));
            thumbH = Math.Min(thumbH, trackH);
            float y = trackTop;
            if (MaxScroll > 0) y += (trackH - thumbH) * (_scroll / MaxScroll);
            return new RectangleF(Width - 10 * Sc, y, 6 * Sc, thumbH);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            if (MaxScroll > 0 && e.X >= Width - 16 * Sc)
            {
                var th = ThumbRect();
                if (e.Y < th.Top || e.Y > th.Bottom)
                {
                    float trackH = Height - 12 * Sc;
                    float frac = (e.Y - 6 * Sc - th.Height / 2f) / Math.Max(1f, trackH - th.Height);
                    _scrollT = Math.Max(0, Math.Min(frac * MaxScroll, MaxScroll));
                }
                _sbDrag = true;
                _sbDragY = e.Y;
                _sbDragScroll = _scrollT;
                _sbIdle = 0;
                EnsureTimer();
                return;
            }
            _pressed = HitTest(e.Location);
            if (_pressed != null) { _pressed.PressT = true; _downPt = e.Location; }
            EnsureTimer();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            _lastMouse = e.Location;
            if (_sbDrag)
            {
                float trackH = Height - 12 * Sc;
                float range = Math.Max(1f, trackH - ThumbRect().Height);
                _scroll = _scrollT = Math.Max(0, Math.Min(_sbDragScroll + (e.Y - _sbDragY) * MaxScroll / range, MaxScroll));
                _sbIdle = 0;
                EnsureTimer();
                Invalidate();
                return;
            }
            if (_pressed != null && !_dragging && !_pressed.IsAdd && !Filtering &&
                (Math.Abs(e.X - _downPt.X) > 6 * Sc || Math.Abs(e.Y - _downPt.Y) > 6 * Sc))
                BeginDrag(e.Location);
            if (_dragging) { UpdateDrag(e.Location); return; }

            bool overBar = MaxScroll > 0 && e.X >= Width - 16 * Sc;
            if (overBar != _sbHover) { _sbHover = overBar; EnsureTimer(); }
            SetHot(overBar ? null : HitTest(e.Location));
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_dragging || _sbDrag) return;
            _sbHover = false;
            SetHot(null);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Right)
            {
                var r = HitTest(e.Location);
                if (r != null && !r.IsAdd && AppContext != null) AppContext(r.App, e.Location);
                return;
            }
            if (e.Button != MouseButtons.Left) return;
            if (_sbDrag) { _sbDrag = false; EnsureTimer(); return; }

            var pressed = _pressed;
            if (_dragging) EndDrag();
            else if (pressed != null && HitTest(e.Location) == pressed) Activate(pressed);
            if (pressed != null) pressed.PressT = false;
            _pressed = null;
            SetHot(HitTest(e.Location));
            EnsureTimer();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (MaxScroll <= 0) return;
            float step = (CellH + Gap * Sc) * 0.8f;
            _scrollT = Math.Max(0, Math.Min(_scrollT - e.Delta / 120f * step, MaxScroll));
            _sbIdle = 0;
            EnsureTimer();
        }

        void Activate(Tile t)
        {
            if (t.IsAdd)
            {
                if (AddRequested != null) AddRequested(this, EventArgs.Empty);
                return;
            }
            t.Selected = !t.Selected;
            EnsureTimer();
            RaiseSelection();
        }

        // ---------- 拖拽排序 ----------

        void BeginDrag(Point p)
        {
            _dragging = true;
            _drag = _pressed;
            _drag.PressT = false;
            _grab = new PointF(p.X - _drag.X, p.Y + _scroll - _drag.Y);
            _dragIndex = OrderedApps().IndexOf(_drag);
            Capture = true;
            EnsureTimer();
        }

        void UpdateDrag(Point p)
        {
            if (_drag == null) return;
            _drag.X = p.X - _grab.X;
            _drag.Y = p.Y + _scroll - _grab.Y;

            int cols; float left, top;
            Metrics(out cols, out left, out top);
            int n = _tiles.Count(t => !t.IsAdd && !t.Dying && t.InView);
            float cw = CellW, ch = CellH, gap = Gap * Sc;
            float cx = _drag.X + cw / 2f, cy = _drag.Y + ch / 2f;
            int best = 0;
            float bd = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                float sx = left + (i % cols) * (cw + gap) + cw / 2f;
                float sy = top + (i / cols) * (ch + gap) + ch / 2f;
                float d = (sx - cx) * (sx - cx) + (sy - cy) * (sy - cy);
                if (d < bd) { bd = d; best = i; }
            }
            if (best != _dragIndex)
            {
                _dragIndex = best;
                Layout(false);
            }
            EnsureTimer();
            Invalidate();
        }

        void EndDrag()
        {
            _dragging = false;
            Capture = false;
            var apps = OrderedApps();
            var rest = _tiles.Where(t => !t.IsAdd && !apps.Contains(t)).ToList();
            var add = _tiles.Find(t => t.IsAdd);
            _tiles.Clear();
            _tiles.AddRange(apps);
            _tiles.AddRange(rest);
            if (add != null) _tiles.Add(add);
            _drag = null;
            Layout(false);
            if (Reordered != null) Reordered(apps.Select(t => t.App).ToList());
        }

        // ---------- 绘制 ----------

        static Color A(Color c, float a)
        {
            return Color.FromArgb((int)Math.Max(0, Math.Min(255, c.A * a)), c);
        }

        protected override void OnPaintBackground(PaintEventArgs e) { }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.Bg);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Tile drawnLast = null;
            bool anyVisible = false;
            foreach (var t in _tiles)
            {
                if (t == _drag) { drawnLast = t; continue; }
                float sy = t.Y - _scroll;
                if (sy + CellH + 20 * Sc < 0 || sy - 20 * Sc > Height) continue;
                if (!t.IsAdd && t.InView && !t.Dying) anyVisible = true;
                DrawTile(g, t);
            }
            if (drawnLast != null) DrawTile(g, drawnLast);

            var add = _tiles.Find(t => t.IsAdd);
            bool empty = add != null && add.InView && !_tiles.Any(t => !t.IsAdd && !t.Dying && t.InView);
            if (empty && !string.IsNullOrEmpty(HintText))
            {
                float a = UiKit.EaseOut(add.Intro) * UiKit.EaseOut(add.Vis);
                var r = new Rectangle(0, (int)(add.Y - _scroll + CellH + 6 * Sc), Width, (int)(30 * Sc));
                TextRenderer.DrawText(g, HintText, _hintFont, r, UiKit.Lerp(Theme.Bg, Theme.TextDim, a),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.Top);
            }
            if (SearchActive && !anyVisible && !string.IsNullOrEmpty(NoResultsText))
            {
                TextRenderer.DrawText(g, NoResultsText, _hintFont,
                    new Rectangle(0, (int)(Height * 0.34f), Width, (int)(36 * Sc)), Theme.TextDim,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.Top);
            }

            DrawFades(g);
            DrawScrollBar(g);
        }

        void DrawFades(Graphics g)
        {
            int fh = (int)(18 * Sc);
            if (fh < 2 || Height < fh * 2) return;
            if (_scroll > 2f)
            {
                var r = new Rectangle(0, 0, Width, fh);
                using (var b = new LinearGradientBrush(new Rectangle(0, -1, Width, fh + 1), Theme.Bg,
                    Color.FromArgb(0, Theme.Bg), LinearGradientMode.Vertical))
                    g.FillRectangle(b, r);
            }
            if (_scroll < MaxScroll - 2f)
            {
                var r = new Rectangle(0, Height - fh, Width, fh);
                using (var b = new LinearGradientBrush(new Rectangle(0, Height - fh, Width, fh + 1),
                    Color.FromArgb(0, Theme.Bg), Theme.Bg, LinearGradientMode.Vertical))
                    g.FillRectangle(b, r);
            }
        }

        void DrawScrollBar(Graphics g)
        {
            if (MaxScroll <= 0 || _sbAlpha <= 0.01f) return;
            var th = ThumbRect();
            int alpha = (int)((_sbDrag || _sbHover ? 120 : 80) * _sbAlpha);
            using (var path = UiKit.RoundRect(th, th.Width / 2f))
            using (var b = new SolidBrush(Color.FromArgb(alpha, 255, 255, 255)))
                g.FillPath(b, path);
        }

        void DrawTile(Graphics g, Tile t)
        {
            float intro = UiKit.EaseOut(t.Intro);
            float a = intro * UiKit.EaseOut(t.Vis);
            if (a < 0.01f || t.Delay > 0f) return;

            bool dragged = t == _drag;
            float sc = Sc, side = Side;
            float hov = UiKit.EaseOut(t.Hov);
            float sel = t.IsAdd ? 0f : UiKit.EaseOut(t.SelP);
            float press = UiKit.EaseOut(t.Press);

            float cellX = t.X;
            float cellY = t.Y - _scroll + (1f - intro) * 14 * sc;
            float scale = 1f + 0.045f * hov - 0.04f * press + (dragged ? 0.06f : 0f);
            float s2 = side * scale;
            float cx = cellX + CellW / 2f;
            float cy = cellY + side / 2f - hov * 3 * sc;
            var card = new RectangleF(cx - s2 / 2f, cy - s2 / 2f, s2, s2);
            float radius = s2 * 0.2f;

            // 阴影（悬停 / 拖拽时）
            float shadow = Math.Max(hov * 0.8f, dragged ? 1f : 0f);
            if (shadow > 0.02f)
            {
                for (int i = 1; i <= 6; i++)
                {
                    float grow = i * 2.2f * sc;
                    var rr = new RectangleF(card.X - grow, card.Y - grow + 7 * sc, card.Width + 2 * grow, card.Height + 2 * grow);
                    using (var p = UiKit.RoundRect(rr, radius + grow))
                    using (var b = new SolidBrush(Color.FromArgb((int)(11f * (7 - i) / 6f * shadow * a), 0, 0, 0)))
                        g.FillPath(b, p);
                }
            }

            if (t.IsAdd) DrawAddCard(g, t, card, radius, hov, a);
            else DrawAppCard(g, t, card, radius, hov, sel, a);

            // 名称
            string name = t.IsAdd ? AddText : (t.App.Name ?? "");
            float strength = t.IsAdd ? hov : Math.Max(sel, hov);
            Color tc = UiKit.Lerp(Theme.Bg, UiKit.Lerp(Theme.TextDim, Theme.Text, strength), a);
            var nameRect = new Rectangle((int)Math.Round(cellX), (int)Math.Round(cellY + side + 8 * sc),
                (int)Math.Round(CellW), (int)Math.Round(NameH * sc));
            TextRenderer.DrawText(g, name, _nameFont, nameRect, tc,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.EndEllipsis |
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        }

        void DrawAppCard(Graphics g, Tile t, RectangleF card, float radius, float hov, float sel, float a)
        {
            float sc = Sc;
            Color fill = UiKit.Lerp(Theme.Card, Theme.CardHover, hov);
            fill = UiKit.Lerp(fill, Color.FromArgb(27, 48, 84), sel);

            using (var path = UiKit.RoundRect(card, radius))
            {
                using (var b = new SolidBrush(A(fill, a)))
                    g.FillPath(b, path);
            }

            // 选中光晕
            if (sel > 0.01f)
            {
                var glow = RectangleF.Inflate(card, 3 * sc, 3 * sc);
                using (var p = UiKit.RoundRect(glow, radius + 3 * sc))
                using (var pen = new Pen(Color.FromArgb((int)(55 * sel * a), Theme.Accent), 5 * sc))
                    g.DrawPath(pen, p);
            }

            // 边框（内缩半个线宽，保证抗锯齿后不被裁切）
            float bw = (1f + 1.5f * sel) * sc;
            var br = RectangleF.Inflate(card, -bw / 2f, -bw / 2f);
            using (var p = UiKit.RoundRect(br, radius - bw / 2f))
            using (var pen = new Pen(A(UiKit.Lerp(Theme.Border, Theme.Accent, Math.Max(sel, hov * 0.35f)), a), bw))
                g.DrawPath(pen, p);

            DrawIcon(g, t, card, hov, sel, a);

            if (sel > 0.01f)
            {
                float pop = UiKit.EaseBack(t.SelP);
                if (pop > 0f)
                {
                    float r = 11 * sc * pop;
                    var c = new PointF(card.Right - 15 * sc, card.Top + 15 * sc);
                    using (var ring = new SolidBrush(A(Theme.Bg, a)))
                        g.FillEllipse(ring, c.X - r - 2 * sc * pop, c.Y - r - 2 * sc * pop, (r + 2 * sc * pop) * 2, (r + 2 * sc * pop) * 2);
                    using (var b = new SolidBrush(A(Theme.Accent, a)))
                        g.FillEllipse(b, c.X - r, c.Y - r, r * 2, r * 2);
                    float k = sc * pop;
                    using (var pen = new Pen(Color.FromArgb((int)(255 * Math.Min(1f, pop) * a), 255, 255, 255), 2.2f * k))
                    {
                        pen.StartCap = LineCap.Round;
                        pen.EndCap = LineCap.Round;
                        pen.LineJoin = LineJoin.Round;
                        g.DrawLines(pen, new[]
                        {
                            new PointF(c.X - 4.5f * k, c.Y + 0.5f * k),
                            new PointF(c.X - 1.5f * k, c.Y + 3.5f * k),
                            new PointF(c.X + 4.5f * k, c.Y - 3.5f * k)
                        });
                    }
                }
            }
        }

        void DrawIcon(Graphics g, Tile t, RectangleF card, float hov, float sel, float a)
        {
            int px = Math.Max(16, (int)Math.Round(Side * 0.62f));
            if (t.Cache == null || t.CacheSize != px)
            {
                if (t.Cache != null) t.Cache.Dispose();
                t.Cache = (Bitmap)ShellIcon.Resize(t.Src, px);
                t.CacheSize = px;
            }

            float scale = card.Width / Side;
            float ds = px * scale;
            float x = card.X + (card.Width - ds) / 2f;
            float y = card.Y + (card.Height - ds) / 2f;
            if (Math.Abs(scale - 1f) < 0.002f)
            {
                // 静止时贴整数像素 1:1 绘制，不产生重采样模糊
                x = (float)Math.Round(x);
                y = (float)Math.Round(y);
                ds = px;
            }

            float alpha = a * (0.9f + 0.1f * Math.Max(sel, hov));
            var cm = new ColorMatrix(new[]
            {
                new float[] { 1, 0, 0, 0, 0 },
                new float[] { 0, 1, 0, 0, 0 },
                new float[] { 0, 0, 1, 0, 0 },
                new float[] { 0, 0, 0, alpha, 0 },
                new float[] { 0, 0, 0, 0, 1 }
            });
            _ia.SetColorMatrix(cm);
            _ia.SetWrapMode(WrapMode.TileFlipXY);

            var oldI = g.InterpolationMode;
            var oldP = g.PixelOffsetMode;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.DrawImage(t.Cache, new Rectangle((int)Math.Round(x), (int)Math.Round(y), (int)Math.Round(ds), (int)Math.Round(ds)),
                0, 0, px, px, GraphicsUnit.Pixel, _ia);
            g.InterpolationMode = oldI;
            g.PixelOffsetMode = oldP;
        }

        void DrawAddCard(Graphics g, Tile t, RectangleF card, float radius, float hov, float a)
        {
            float sc = Sc;
            using (var path = UiKit.RoundRect(card, radius))
            using (var b = new SolidBrush(A(UiKit.Lerp(Theme.Bg, Theme.Card, hov * 0.7f), a)))
                g.FillPath(b, path);

            var br = RectangleF.Inflate(card, -sc, -sc);
            Color line = UiKit.Lerp(Color.FromArgb(70, 74, 86), Theme.Accent, hov);
            using (var p = UiKit.RoundRect(br, radius - sc))
            using (var pen = new Pen(A(line, a), 1.6f * sc))
            {
                pen.DashStyle = DashStyle.Dash;
                pen.DashPattern = new[] { 4f, 3.5f };
                g.DrawPath(pen, p);
            }

            float len = card.Width * 0.13f;
            float cx = card.X + card.Width / 2f, cy = card.Y + card.Height / 2f;
            using (var pen = new Pen(A(UiKit.Lerp(Theme.TextDim, Theme.Accent, hov), a), 3f * sc))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                g.DrawLine(pen, cx - len, cy, cx + len, cy);
                g.DrawLine(pen, cx, cy - len, cx, cy + len);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _timer.Dispose();
                _ia.Dispose();
                _nameFont.Dispose();
                _hintFont.Dispose();
                foreach (var t in _tiles)
                {
                    if (t.Src != null) t.Src.Dispose();
                    if (t.Cache != null) t.Cache.Dispose();
                }
            }
            base.Dispose(disposing);
        }
    }
}
