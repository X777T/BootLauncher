using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace BootLauncher
{
    public class MainForm : Form
    {
        const int HeaderH = 64;
        const int SubH = 56;
        const int CatH = 48;
        const int FooterH = 124;

        readonly Config _config;
        AppGrid _grid;
        SearchBox _search;
        Chip _btnUpdate, _btnBack;
        Label _catLabel;
        Chip _catAdd, _catMore;
        readonly List<Chip> _catChips = new List<Chip>();
        ContextMenuStrip _catMenu, _catMoreMenu;
        string _curCat; // null=全部，""=未分类，其余=分类名

        class CatKey
        {
            public readonly string Value;
            public CatKey(string v) { Value = v; }
        }
        WindowButton _btnMin, _btnClose;
        Label _schemeLabel;
        readonly List<Chip> _schemeChips = new List<Chip>();
        Chip _schemeAdd, _schemeManage;
        ContextMenuStrip _menu, _schemeMenu;
        CheckLabel _autoStart;
        Label _langLabel, _iconSizeLabel;
        ComboPicker _langCombo, _iconSizeCombo;
        FlatButton _btnStart;
        bool _updatingTexts;

        int S(double v) { return (int)Math.Round(v * DeviceDpi / 96.0); }

        public MainForm()
        {
            _config = AppConfig.Load();
            Strings.Init(_config.Language);

            AutoScaleMode = AutoScaleMode.None;
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = new Font("Segoe UI", 9F);
            Text = "BootLauncher";
            // 任务栏 / Alt+Tab 用的图标，取自 exe 自身嵌入的 app.ico
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            MinimumSize = new Size(720, 520);
            ClientSize = new Size(1000, 650);
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
            KeyPreview = true;
            KeyDown += OnFormKeyDown;
            BuildUi();
        }

        void OnFormKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                if (_search.HasText) _search.ClearText();
                else Close();
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Enter)
            {
                if (_btnStart.Enabled) Launch();
                e.SuppressKeyPress = true;
            }
            else if (e.Control && e.KeyCode == Keys.F)
            {
                _search.FocusInput();
                e.SuppressKeyPress = true;
            }
            else if (e.Control && e.KeyCode == Keys.A && !_search.ContainsFocus)
            {
                _grid.SelectAll(_grid.SelectedCount == 0 || e.Shift == false && _grid.SelectedCount < _config.Apps.Count);
                e.SuppressKeyPress = true;
            }
        }

        Label MakeDimLabel(Color back)
        {
            return new Label
            {
                AutoSize = true,
                ForeColor = Theme.TextDim,
                BackColor = back,
                Font = new Font("Segoe UI", 10F)
            };
        }

        CheckLabel MakeCheck(bool isChecked)
        {
            var c = new CheckLabel("")
            {
                BackColor = Theme.Surface,
                ForeColor = Theme.Text,
                Font = new Font("Segoe UI", 10.5F),
                Checked = isChecked
            };
            return c;
        }

        ComboPicker MakeCombo()
        {
            return new ComboPicker { BackColor = Theme.Surface, Font = new Font("Segoe UI", 10F) };
        }

        void BuildUi()
        {
            _btnClose = new WindowButton { Kind = WindowButtonKind.Close, BackColor = Theme.Surface };
            _btnClose.Activated += (s, e) => Close();
            Controls.Add(_btnClose);

            _btnMin = new WindowButton { Kind = WindowButtonKind.Minimize, BackColor = Theme.Surface };
            _btnMin.Activated += (s, e) => WindowState = FormWindowState.Minimized;
            Controls.Add(_btnMin);

            _btnUpdate = new Chip { BackColor = Theme.Surface, Font = new Font("Segoe UI", 10F) };
            _btnUpdate.Activated += (s, e) => OpenScan();
            Controls.Add(_btnUpdate);

            _search = new SearchBox { BackColor = Theme.Surface };
            _search.QueryChanged += (s, e) =>
            {
                _grid.SetFilter(_search.Query);
                UpdateBackButton();
            };
            Controls.Add(_search);

            _btnBack = new Chip { BackColor = Theme.Surface, Font = new Font("Segoe UI", 10F), Visible = false };
            _btnBack.Activated += (s, e) => _search.ClearText();
            Controls.Add(_btnBack);

            _schemeLabel = MakeDimLabel(Theme.Bg);
            Controls.Add(_schemeLabel);

            _schemeAdd = new Chip { IsAdd = true, BackColor = Theme.Bg };
            _schemeAdd.Activated += (s, e) => AddScheme();
            Controls.Add(_schemeAdd);

            _schemeManage = new Chip { BackColor = Theme.Bg, Font = new Font("Segoe UI", 9.5F) };
            _schemeManage.Activated += (s, e) => OpenSchemeManager();
            Controls.Add(_schemeManage);

            _catLabel = MakeDimLabel(Theme.Bg);
            Controls.Add(_catLabel);

            _catAdd = new Chip { BackColor = Theme.Bg };
            _catAdd.Activated += (s, e) => NewCategory(null);
            Controls.Add(_catAdd);

            _catMore = new Chip { BackColor = Theme.Bg, ChipText = "···", Visible = false };
            _catMore.Activated += (s, e) => ShowCategoryOverflow();
            Controls.Add(_catMore);

            _iconSizeLabel = MakeDimLabel(Theme.Bg);
            Controls.Add(_iconSizeLabel);
            _iconSizeCombo = MakeCombo();
            _iconSizeCombo.BackColor = Theme.Bg;
            _iconSizeCombo.SelectedIndexChanged += OnIconSizeChanged;
            Controls.Add(_iconSizeCombo);

            _grid = new AppGrid { IconSize = _config.IconSize };
            _grid.SelectionChanged += (s, e) => UpdateStartButton();
            _grid.AddRequested += (s, e) => AddApp();
            _grid.AppContext += ShowAppMenu;
            _grid.Reordered += apps =>
            {
                _config.Apps.Clear();
                _config.Apps.AddRange(apps);
                AppConfig.Save(_config);
            };
            Controls.Add(_grid);

            _autoStart = MakeCheck(AutoStart.IsEnabled());
            _autoStart.Click += (s, e) =>
            {
                AutoStart.SetEnabled(_autoStart.Checked);
                _config.AutoStart = _autoStart.Checked;
                AppConfig.Save(_config);
            };
            Controls.Add(_autoStart);

            _langLabel = MakeDimLabel(Theme.Surface);
            Controls.Add(_langLabel);
            _langCombo = MakeCombo();
            _langCombo.SelectedIndexChanged += OnLanguageChanged;
            Controls.Add(_langCombo);

            _btnStart = new FlatButton
            {
                Font = new Font("Segoe UI", 13F, FontStyle.Bold),
                NormalColor = Theme.Accent,
                GradientColor = Theme.AccentLo,
                HoverColor = Theme.AccentHi,
                DownColor = Theme.AccentLo
            };
            _btnStart.Click += (s, e) => Launch();
            Controls.Add(_btnStart);

            _menu = UiKit.NewMenu(Font);
            _catMenu = UiKit.NewMenu(Font);
            _catMenu.Items.Add(Strings.T("Rename"));
            _catMenu.Items.Add(Strings.T("DeleteCategory"));
            _catMenu.Opening += (s, e) => _catMenu.Tag = _catMenu.SourceControl;
            _catMenu.ItemClicked += (s, e) =>
            {
                var chip = _catMenu.Tag as Chip;
                var key = chip == null ? null : chip.Tag as CatKey;
                if (key == null || key.Value == null || key.Value.Length == 0) return;
                _catMenu.Close();
                if (e.ClickedItem == _catMenu.Items[0]) RenameCategory(key.Value);
                else DeleteCategory(key.Value);
            };

            _catMoreMenu = UiKit.NewMenu(Font);

            _schemeMenu = UiKit.NewMenu(Font);
            _schemeMenu.Items.Add(Strings.T("DeleteScheme"));
            _schemeMenu.Opening += (s, e) => _schemeMenu.Tag = _schemeMenu.SourceControl;
            _schemeMenu.ItemClicked += (s, e) =>
            {
                var chip = _schemeMenu.Tag as Chip;
                if (chip == null) return;
                var scheme = chip.Tag as Scheme;
                if (scheme != null) DeleteScheme(scheme);
            };

            RefreshTexts();
        }

        // ---------- 应用增删改 ----------

        void LoadApps()
        {
            _grid.SetApps(_config.Apps, GetInitialSelection());
            BuildCategoryChips();
            UpdateStartButton();
        }

        // 在某个分类里点“添加”：从已添加的应用里挑，不再重新选文件。
        // 返回 false 表示没有可挑的应用，调用方回退到普通的“新增应用”流程。
        bool AddExistingToCategory(string cat)
        {
            var candidates = _config.Apps.Where(a => a.Category != cat).ToList();
            if (candidates.Count == 0) return false;
            using (var dlg = new AppPickForm(string.Format(Strings.T("PickForCategory"), cat), candidates))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return true;
                foreach (var a in dlg.Picked) a.Category = cat;
                AppConfig.Save(_config);
                BuildCategoryChips();
                _grid.Refilter();
                _grid.ScrollTo(dlg.Picked[0]);
            }
            return true;
        }

        void UpdateAddText()
        {
            if (_grid == null) return;
            _grid.AddText = string.IsNullOrEmpty(_curCat) ? Strings.T("AddApp") : Strings.T("AddToCategory");
            _grid.Invalidate();
        }

        void AddApp()
        {
            if (!string.IsNullOrEmpty(_curCat) && AddExistingToCategory(_curCat)) return;
            using (var dlg = new AppEditForm(null))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                // 正在看某个分类时添加的应用，直接归入该分类，否则会被过滤掉看不见
                if (!string.IsNullOrEmpty(_curCat)) dlg.Result.Category = _curCat;
                _config.Apps.Add(dlg.Result);
                AppConfig.Save(_config);
                _search.ClearText();
                BuildCategoryChips();
                _grid.SetApps(_config.Apps, null);
                _grid.SelectApp(dlg.Result, true); // 刚添加的应用直接勾选，少一次点击
                _grid.ScrollTo(dlg.Result);
            }
        }

        void EditApp(AppItem app)
        {
            using (var dlg = new AppEditForm(app))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                app.Name = dlg.Result.Name;
                app.Path = dlg.Result.Path;
                AppConfig.Save(_config);
                _grid.SetApps(_config.Apps, null);
            }
        }

        void DeleteApp(AppItem app)
        {
            if (MessageBox.Show(this, Strings.T("ConfirmDelete"), "BootLauncher",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            _config.Apps.Remove(app);
            AppConfig.Save(_config);
            _grid.SetApps(_config.Apps, null);
            BuildCategoryChips();
            UpdateStartButton();
        }

        void OpenScan()
        {
            using (var dlg = new ScanAppsForm(_config))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    AppConfig.Save(_config);
                    _grid.SetApps(_config.Apps, null);
                    BuildCategoryChips();
                }
            }
        }

        // ---------- 文案 / 选项 ----------

        void RefreshTexts()
        {
            _updatingTexts = true;
            _autoStart.TextContent = Strings.T("AutoStart");
            _btnUpdate.ChipText = Strings.T("Update");
            _search.SetCue(Strings.T("Search"));
            _btnBack.ChipText = Strings.T("Back");
            _schemeManage.ChipText = Strings.T("Manage");
            _grid.HintText = Strings.T("HintAdd");
            _grid.NoResultsText = Strings.T("NoResults");
            _schemeLabel.Text = Strings.T("Scheme");
            _iconSizeLabel.Text = Strings.T("IconSize");
            _langLabel.Text = Strings.T("Language");
            _schemeMenu.Items[0].Text = Strings.T("DeleteScheme");
            _catMenu.Items[0].Text = Strings.T("Rename");
            _catMenu.Items[1].Text = Strings.T("DeleteCategory");
            _catLabel.Text = Strings.T("Category");

            _langCombo.Items.Clear();
            _langCombo.Items.AddRange(new object[]
            {
                new LangItem("system", Strings.T("LangSystem")),
                new LangItem("zh-CN", Strings.T("LangZh")),
                new LangItem("en-US", Strings.T("LangEn"))
            });
            _langCombo.SelectedIndex = -1;
            for (int i = 0; i < _langCombo.Items.Count; i++)
                if (((LangItem)_langCombo.Items[i]).Value == _config.Language)
                    _langCombo.SelectedIndex = i;
            if (_langCombo.SelectedIndex < 0) _langCombo.SelectedIndex = 0;

            BuildSchemeChips();
            BuildCategoryChips();
            BuildIconSizeCombo();
            UpdateStartButton();
            _updatingTexts = false;
            LayoutUi();
            Invalidate();
        }

        void OnLanguageChanged(object sender, EventArgs e)
        {
            if (_updatingTexts) return;
            var item = _langCombo.SelectedItem as LangItem;
            if (item == null || item.Value == _config.Language) return;
            _config.Language = item.Value;
            Strings.Init(item.Value);
            AppConfig.Save(_config);
            RefreshTexts();
        }

        void BuildIconSizeCombo()
        {
            _iconSizeCombo.Items.Clear();
            _iconSizeCombo.Items.Add(new SizeItem(96, Strings.T("SizeSmall")));
            _iconSizeCombo.Items.Add(new SizeItem(128, Strings.T("SizeMedium")));
            _iconSizeCombo.Items.Add(new SizeItem(160, Strings.T("SizeLarge")));
            _iconSizeCombo.Items.Add(new SizeItem(192, Strings.T("SizeXLarge")));
            int idx = 1;
            for (int i = 0; i < _iconSizeCombo.Items.Count; i++)
                if (((SizeItem)_iconSizeCombo.Items[i]).Value == _config.IconSize) { idx = i; break; }
            _iconSizeCombo.SelectedIndex = -1;
            _iconSizeCombo.SelectedIndex = idx;
        }

        void OnIconSizeChanged(object sender, EventArgs e)
        {
            if (_updatingTexts) return;
            var item = _iconSizeCombo.SelectedItem as SizeItem;
            if (item == null || item.Value == _config.IconSize) return;
            _config.IconSize = item.Value;
            AppConfig.Save(_config);
            _grid.IconSize = item.Value;
        }

        // ---------- 分类 ----------

        void ShowAppMenu(AppItem app, Point pt)
        {
            _menu.Items.Clear();
            _menu.Items.Add(Strings.T("Edit"), null, (s, e) => EditApp(app));

            var cat = new ToolStripMenuItem(Strings.T("Category"));
            var none = new ToolStripMenuItem(Strings.T("NoCategory"))
            {
                Checked = string.IsNullOrEmpty(app.Category),
                CheckOnClick = false
            };
            none.Click += (s, e) => SetAppCategory(app, "");
            cat.DropDownItems.Add(none);
            foreach (var name in _config.Categories)
            {
                string n = name;
                var it = new ToolStripMenuItem(n) { Checked = app.Category == n };
                it.Click += (s, e) => SetAppCategory(app, n);
                cat.DropDownItems.Add(it);
            }
            cat.DropDownItems.Add(new ToolStripSeparator());
            var create = new ToolStripMenuItem(Strings.T("NewCategory"));
            create.Click += (s, e) => NewCategory(app);
            cat.DropDownItems.Add(create);
            _menu.Items.Add(cat);

            _menu.Items.Add(Strings.T("Delete"), null, (s, e) => DeleteApp(app));
            _menu.Show(_grid, pt);
        }

        void SetAppCategory(AppItem app, string cat)
        {
            app.Category = cat;
            AppConfig.Save(_config);
            BuildCategoryChips();
            _grid.Refilter();
        }

        string ValidateCategoryName(string name, string except)
        {
            if (string.IsNullOrEmpty(name)) return Strings.T("CategoryEmpty");
            foreach (var c in _config.Categories)
                if (string.Equals(c, name, StringComparison.CurrentCultureIgnoreCase) &&
                    !string.Equals(c, except, StringComparison.Ordinal))
                    return Strings.T("CategoryExists");
            return null;
        }

        // forApp 不为空时，新建后顺便把该应用归到新分类
        void NewCategory(AppItem forApp)
        {
            using (var dlg = new PromptForm(Strings.T("AddCategory"), Strings.T("CategoryName"), "",
                n => ValidateCategoryName(n, null)))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                _config.Categories.Add(dlg.Value);
                if (forApp != null) forApp.Category = dlg.Value;
                AppConfig.Save(_config);
                if (forApp == null) _curCat = dlg.Value; // 从分类条新建时直接切过去
                BuildCategoryChips();
                _grid.SetCategory(_curCat);
                _grid.Refilter();
            }
        }

        void RenameCategory(string old)
        {
            using (var dlg = new PromptForm(Strings.T("Rename"), Strings.T("CategoryName"), old,
                n => ValidateCategoryName(n, old)))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                string nn = dlg.Value;
                int i = _config.Categories.IndexOf(old);
                if (i >= 0) _config.Categories[i] = nn;
                foreach (var a in _config.Apps)
                    if (a.Category == old) a.Category = nn;
                if (_curCat == old) _curCat = nn;
                AppConfig.Save(_config);
                BuildCategoryChips();
                _grid.SetCategory(_curCat);
                _grid.Refilter();
            }
        }

        void DeleteCategory(string name)
        {
            if (MessageBox.Show(this, string.Format(Strings.T("ConfirmDeleteCategory"), name), "BootLauncher",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            _config.Categories.Remove(name);
            foreach (var a in _config.Apps)
                if (a.Category == name) a.Category = "";
            if (_curCat == name) _curCat = null;
            AppConfig.Save(_config);
            BuildCategoryChips();
            _grid.SetCategory(_curCat);
            _grid.Refilter();
        }

        void SelectCategory(string cat)
        {
            _curCat = cat;
            MarkActiveCategory();
            _grid.SetCategory(cat);
            UpdateAddText();
        }

        void BuildCategoryChips()
        {
            foreach (var c in _catChips)
            {
                Controls.Remove(c);
                c.Dispose();
            }
            _catChips.Clear();

            bool has = _config.Categories.Count > 0;
            if (has)
            {
                AddCatChip(Strings.T("All"), null, false);
                foreach (var name in _config.Categories) AddCatChip(name, name, true);
                bool anyUncat = _config.Apps.Any(a => string.IsNullOrEmpty(a.Category));
                if (anyUncat || _curCat == "") AddCatChip(Strings.T("Uncategorized"), "", false);
            }
            else if (_curCat != null)
            {
                _curCat = null;
                if (_grid != null) _grid.SetCategory(null);
            }
            foreach (var c in _catChips) Controls.Add(c);

            _catAdd.IsAdd = has;
            _catAdd.ChipText = has ? "" : "+  " + Strings.T("AddCategory");
            _catAdd.Fit();
            MarkActiveCategory();
            UpdateAddText();
            LayoutUi();
        }

        void AddCatChip(string text, string value, bool menu)
        {
            var chip = new Chip { BackColor = Theme.Bg, ChipText = text, Tag = new CatKey(value) };
            if (menu) chip.ContextMenuStrip = _catMenu;
            chip.Activated += (s, e) => SelectCategory(value);
            _catChips.Add(chip);
        }

        void MarkActiveCategory()
        {
            foreach (var c in _catChips)
            {
                var k = (CatKey)c.Tag;
                c.Active = k.Value == _curCat;
            }
        }

        void ShowCategoryOverflow()
        {
            _catMoreMenu.Items.Clear();
            foreach (var c in _catChips)
            {
                if (c.Visible) continue;
                var k = (CatKey)c.Tag;
                var it = new ToolStripMenuItem(c.ChipText) { Checked = k.Value == _curCat };
                it.Click += (s, e) => SelectCategory(k.Value);
                _catMoreMenu.Items.Add(it);
            }
            if (_catMoreMenu.Items.Count > 0)
                _catMoreMenu.Show(_catMore, new Point(0, _catMore.Height + 2));
        }

        void UpdateBackButton()
        {
            // 搜索框里有内容时才出现“返回”，点它清空搜索、回到完整列表
            _btnBack.Visible = _search.HasText;
            LayoutUi();
            Invalidate(new Rectangle(0, 0, ClientSize.Width, S(HeaderH)));
        }

        void UpdateStartButton()
        {
            if (_btnStart == null) return;
            int n = _grid.SelectedCount;
            _btnStart.ButtonText = n > 0 ? Strings.T("Start") + "  ·  " + n : Strings.T("Start");
            _btnStart.Enabled = n > 0;
            _btnStart.Invalidate();
        }

        // ---------- 方案 ----------

        Scheme FindScheme(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            foreach (var s in _config.Schemes)
                if (s.Name == name) return s;
            return null;
        }

        List<string> GetInitialSelection()
        {
            var scheme = FindScheme(_config.CurrentScheme);
            if (scheme != null) return scheme.Apps;
            return new List<string>(); // 默认方案：什么都不选
        }

        void BuildSchemeChips()
        {
            foreach (var c in _schemeChips)
            {
                Controls.Remove(c);
                c.Dispose();
            }
            _schemeChips.Clear();

            var def = new Chip { BackColor = Theme.Bg, ChipText = Strings.T("DefaultScheme") };
            def.Activated += (s, e) => SelectScheme(null);
            _schemeChips.Add(def);
            foreach (var sc in _config.Schemes)
            {
                var scheme = sc;
                var chip = new Chip { BackColor = Theme.Bg, ChipText = scheme.Name, Tag = scheme, ContextMenuStrip = _schemeMenu };
                chip.Activated += (s, e) => SelectScheme(scheme);
                _schemeChips.Add(chip);
            }
            foreach (var c in _schemeChips) Controls.Add(c);
            _schemeAdd.Fit();
            MarkActiveScheme();
            LayoutUi();
        }

        void MarkActiveScheme()
        {
            var current = FindScheme(_config.CurrentScheme);
            foreach (var c in _schemeChips)
                c.Active = c.Tag == null ? current == null : ReferenceEquals(c.Tag, current);
        }

        void SelectScheme(Scheme scheme)
        {
            _config.CurrentScheme = scheme == null ? "" : scheme.Name;
            // 默认方案 = 什么都不选；其它方案 = 勾上它保存的应用
            _grid.SetSelection(scheme != null ? scheme.Apps : new List<string>());
            AppConfig.Save(_config);
            MarkActiveScheme();
        }

        void AddScheme()
        {
            // 当前已选的只作为对话框里的默认勾选，具体包含哪些应用由用户在对话框里自己选
            var preselected = _grid.SelectedApps().Select(a => a.Name).ToList();
            using (var dlg = new AddSchemeForm(_config, preselected))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                var name = dlg.ChosenName;
                var existing = FindScheme(name);
                if (existing != null) existing.Apps = dlg.ChosenApps;
                else _config.Schemes.Add(new Scheme { Name = name, Apps = dlg.ChosenApps });
                _config.CurrentScheme = name;
                AppConfig.Save(_config);
                _grid.SetSelection(dlg.ChosenApps);
                BuildSchemeChips();
            }
        }

        void OpenSchemeManager()
        {
            bool add;
            using (var dlg = new SchemeManageForm(_config))
            {
                dlg.ShowDialog(this);
                add = dlg.AddRequested;
            }
            AppConfig.Save(_config);
            BuildSchemeChips();
            // 当前方案被删除后回到默认；被重命名时 CurrentScheme 已同步
            if (FindScheme(_config.CurrentScheme) == null) _config.CurrentScheme = "";
            MarkActiveScheme();
            if (add) AddScheme();
        }

        void DeleteScheme(Scheme scheme)
        {
            _config.Schemes.Remove(scheme);
            if (_config.CurrentScheme == scheme.Name) _config.CurrentScheme = "";
            AppConfig.Save(_config);
            BuildSchemeChips();
        }

        class LangItem
        {
            public readonly string Value;
            readonly string _text;
            public LangItem(string value, string text) { Value = value; _text = text; }
            public override string ToString() { return _text; }
        }

        class SizeItem
        {
            public readonly int Value;
            readonly string _text;
            public SizeItem(int value, string text) { Value = value; _text = text; }
            public override string ToString() { return _text; }
        }

        // ---------- 启动 ----------

        void Launch()
        {
            var selected = _grid.SelectedApps().Where(a => !string.IsNullOrEmpty(a.Path)).ToList();
            if (selected.Count == 0) return;

            foreach (var app in selected)
            {
                try
                {
                    Process.Start(new ProcessStartInfo(app.Path) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, Strings.T("LaunchFailed") + ": " + app.Name + "\r\n" + ex.Message,
                        "BootLauncher", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                System.Threading.Thread.Sleep(300);
            }

            AppConfig.Save(_config);
            Close();
        }

        // ---------- 窗口外壳 ----------

        [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
        bool _dwmRounded;

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                // 保留正常窗口框架样式（含 WS_CAPTION），DWM 才会播放最小化/还原动画并给原生阴影/圆角；
                // 非客户区在 WM_NCCALCSIZE 里裁掉，视觉上仍是无边框。
                cp.Style |= 0x00C00000  // WS_CAPTION
                          | 0x00040000  // WS_THICKFRAME
                          | 0x00080000  // WS_SYSMENU
                          | 0x00020000  // WS_MINIMIZEBOX
                          | 0x00010000; // WS_MAXIMIZEBOX
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            int round = 2; // DWMWCP_ROUND：Win11 系统级圆角，拖动缩放时由 DWM 实时裁剪
            _dwmRounded = DwmSetWindowAttribute(Handle, 33, ref round, sizeof(int)) == 0;
            if (_dwmRounded) Region = null;
            int transitions = 0; // DWMWA_TRANSITIONS_FORCEDISABLED=FALSE：确保最小化/还原动画不被禁用
            DwmSetWindowAttribute(Handle, 3, ref transitions, sizeof(int));
            int borderColor = Theme.Bg.R | (Theme.Bg.G << 8) | (Theme.Bg.B << 16); // COLORREF=0x00BBGGRR
            DwmSetWindowAttribute(Handle, 34, ref borderColor, sizeof(int));
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            bool restored = false;
            try
            {
                if (_config.WindowW >= 400 && _config.WindowH >= 300)
                {
                    var saved = new Rectangle(_config.WindowX, _config.WindowY,
                        _config.WindowW, _config.WindowH);
                    foreach (var screen in Screen.AllScreens)
                    {
                        var wa = screen.WorkingArea;
                        if (wa.IntersectsWith(saved))
                        {
                            Bounds = Rectangle.Intersect(saved, wa);
                            restored = true;
                            break;
                        }
                    }
                }
            }
            catch { }
            if (!restored)
            {
                try
                {
                    var wa = Screen.FromControl(this).WorkingArea;
                    int mx = (int)Math.Round(wa.Width * 0.10);
                    int my = (int)Math.Round(wa.Height * 0.12);
                    Bounds = Rectangle.FromLTRB(
                        wa.Left + mx, wa.Top + my,
                        wa.Right - mx, wa.Bottom - my);
                }
                catch { }
            }
            UpdateRegion();
            _normalSize = Size;
            LoadApps();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            _config.WindowX = Left;
            _config.WindowY = Top;
            _config.WindowW = Width;
            _config.WindowH = Height;
            AppConfig.Save(_config);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutUi();
            UpdateRegion();
        }

        protected override void OnDpiChanged(DpiChangedEventArgs e)
        {
            base.OnDpiChanged(e);
            foreach (var c in _schemeChips) c.Fit();
            _schemeAdd.Fit();
            _btnUpdate.Fit();
            LayoutUi();
        }

        void LayoutUi()
        {
            if (_grid == null || ClientSize.Width <= 0) return;
            int W = ClientSize.Width, H = ClientSize.Height;
            int pad = S(24);

            // 顶栏：右侧窗口按钮 → 更新 → 搜索
            int bs = S(36);
            int by = (S(HeaderH) - bs) / 2;
            _btnClose.SetBounds(W - S(14) - bs, by, bs, bs);
            _btnMin.SetBounds(_btnClose.Left - S(4) - bs, by, bs, bs);
            _btnUpdate.Fit();
            _btnUpdate.Location = new Point(_btnMin.Left - S(10) - _btnUpdate.Width, (S(HeaderH) - _btnUpdate.Height) / 2);
            int sw = Math.Max(S(140), Math.Min(S(320), _btnUpdate.Left - S(300) - S(14)));
            int sh = S(36);
            _search.SetBounds(_btnUpdate.Left - S(14) - sw, (S(HeaderH) - sh) / 2, sw, sh);
            _btnBack.Fit();
            _btnBack.Location = new Point(_search.Left - S(10) - _btnBack.Width, (S(HeaderH) - _btnBack.Height) / 2);

            // 方案条
            int subTop = S(HeaderH);
            int subH = S(SubH);
            _schemeLabel.Location = new Point(pad, subTop + (subH - _schemeLabel.Height) / 2);
            int x = _schemeLabel.Right + S(12);
            int chipY = subTop + (subH - S(32)) / 2;
            int limit = W - pad;

            _iconSizeCombo.Size = new Size(S(96), S(30));
            _iconSizeCombo.Location = new Point(W - pad - _iconSizeCombo.Width, subTop + (subH - _iconSizeCombo.Height) / 2);
            _iconSizeLabel.Location = new Point(_iconSizeCombo.Left - S(8) - _iconSizeLabel.Width,
                subTop + (subH - _iconSizeLabel.Height) / 2);
            limit = _iconSizeLabel.Left - S(16);

            foreach (var c in _schemeChips)
            {
                c.Fit();
                c.Visible = x + c.Width <= limit - _schemeAdd.Width - _schemeManage.Width - S(16);
                c.Location = new Point(x, chipY);
                x += c.Width + S(8);
            }
            _schemeManage.Fit();
            int tail = _schemeAdd.Width + S(8) + _schemeManage.Width;
            _schemeAdd.Location = new Point(Math.Min(x, limit - tail), chipY);
            _schemeManage.Location = new Point(_schemeAdd.Right + S(8), chipY);

            // 分类条
            int catTop = subTop + subH;
            int catH = S(CatH);
            int catY = catTop + (catH - S(32)) / 2;
            bool hasCats = _config.Categories.Count > 0;
            _catLabel.Visible = hasCats;
            _catLabel.Location = new Point(pad, catTop + (catH - _catLabel.Height) / 2);
            int cx = hasCats ? _catLabel.Right + S(12) : pad;
            int climit = W - pad;
            _catAdd.Fit();
            int reserve = _catAdd.Width + S(8);
            // 先判断是否会溢出：溢出时留出“···”的位置，放不下的分类收进它的菜单
            int total = cx;
            foreach (var c in _catChips) { c.Fit(); total += c.Width + S(8); }
            bool overflow = total + reserve > climit;
            _catMore.Fit();
            int avail = climit - reserve - (overflow ? _catMore.Width + S(8) : 0);
            foreach (var c in _catChips)
            {
                bool fits = cx + c.Width <= avail;
                c.Visible = fits;
                c.Location = new Point(cx, catY);
                if (fits) cx += c.Width + S(8);
            }
            _catMore.Visible = overflow;
            if (overflow)
            {
                _catMore.Location = new Point(cx, catY);
                cx += _catMore.Width + S(8);
            }
            _catAdd.Location = new Point(cx, catY);

            // 网格
            int gridTop = catTop + catH;
            _grid.SetBounds(0, gridTop, W, Math.Max(10, H - gridTop - S(FooterH)));

            // 底栏
            int ft = H - S(FooterH);
            int rowA = ft + S(14);
            _autoStart.Location = new Point(pad, rowA + (S(30) - _autoStart.Height) / 2);
            _langCombo.Size = new Size(S(140), S(30));
            _langCombo.Location = new Point(W - pad - _langCombo.Width, rowA);
            _langLabel.Location = new Point(_langCombo.Left - S(8) - _langLabel.Width, rowA + (S(30) - _langLabel.Height) / 2);
            _btnStart.SetBounds(pad, ft + S(58), W - pad * 2, S(52));
            _btnStart.Radius = S(26);
        }

        void UpdateRegion()
        {
            if (_dwmRounded || _resizing) return;
            try
            {
                int r = (int)Math.Round(16f * DeviceDpi / 96f);
                Region = UiRounded.Region(new Rectangle(0, 0, Width, Height), r);
            }
            catch { }
        }

        bool _resizing;

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.Bg);
            int W = ClientSize.Width, H = ClientSize.Height;
            int hh = S(HeaderH), fh = S(FooterH);

            using (var brush = new SolidBrush(Theme.Surface))
            {
                g.FillRectangle(brush, 0, 0, W, hh);
                g.FillRectangle(brush, 0, H - fh, W, fh);
            }
            using (var pen = new Pen(Theme.Border))
            {
                g.DrawLine(pen, 0, hh, W, hh);
                g.DrawLine(pen, 0, H - fh, W, H - fh);
            }

            // 品牌标识 + 标题
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int ls = S(30);
            var logo = new RectangleF(S(24), (hh - ls) / 2f, ls, ls);
            using (var path = UiKit.RoundRect(logo, ls * 0.3f))
            using (var b = new LinearGradientBrush(logo, Theme.AccentHi, Theme.AccentLo, 45f))
                g.FillPath(b, path);
            using (var pen = new Pen(Color.White, Math.Max(2f, S(2.4))))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;
                float cx = logo.X + logo.Width / 2f, cy = logo.Y + logo.Height / 2f, u = ls * 0.17f;
                g.DrawLines(pen, new[]
                {
                    new PointF(cx - u * 0.8f, cy - u * 1.2f),
                    new PointF(cx + u * 1.1f, cy),
                    new PointF(cx - u * 0.8f, cy + u * 1.2f)
                });
            }
            using (var font = new Font("Segoe UI Semibold", 12.5F))
                TextRenderer.DrawText(g, Strings.T("Title"), font,
                    new Rectangle(logo.Right > 0 ? (int)logo.Right + S(12) : 0, 0, Math.Max(10, (_btnBack.Visible ? _btnBack.Left : _search.Left) - (int)logo.Right - S(24)), hh),
                    Theme.Text,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        }

        Size _normalSize;

        protected override void SetBoundsCore(int x, int y, int width, int height, BoundsSpecified specified)
        {
            // 无边框窗口保留了 WS_CAPTION 框架以换取还原动画，但 WinForms 还原时会按“客户区+框架”
            // 算出大一圈的尺寸传进来，导致越还原越大。在交给 base 前把充气尺寸钳回正常值，
            // 这样 WinForms 内部状态也保持一致。用户手动缩放（_resizing）期间不钳制。
            if (!_resizing && _normalSize.Width > 0 && (specified & BoundsSpecified.Size) != 0)
            {
                if (width > _normalSize.Width) width = _normalSize.Width;
                if (height > _normalSize.Height) height = _normalSize.Height;
            }
            base.SetBoundsCore(x, y, width, height, specified);
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_NCCALCSIZE = 0x0083;
            if (m.Msg == WM_NCCALCSIZE && m.WParam != IntPtr.Zero)
            {
                m.Result = IntPtr.Zero; // 客户区铺满整个窗口，去掉系统标题栏/边框（无边框外观）
                return;
            }
            const int WM_SYSCOMMAND = 0x0112;
            const int SC_MINIMIZE = 0xF020;
            if (m.Msg == WM_SYSCOMMAND && (m.WParam.ToInt32() & 0xFFF0) == SC_MINIMIZE)
                _normalSize = Size; // 最小化前记住正常尺寸，供还原时钳制
            const int WM_ENTERSIZEMOVE = 0x0231;
            const int WM_EXITSIZEMOVE = 0x0232;
            if (m.Msg == WM_ENTERSIZEMOVE)
            {
                _resizing = true; // 用户正在手动缩放/拖动，期间不钳制尺寸
                if (!_dwmRounded) Region = null;
            }
            else if (m.Msg == WM_EXITSIZEMOVE)
            {
                _resizing = false;
                if (!_dwmRounded) UpdateRegion();
                _normalSize = Size; // 手动缩放结束，记住新的正常尺寸
            }

            base.WndProc(ref m);
            if (m.Msg != 0x84) return;
            int lp = m.LParam.ToInt32();
            var p = PointToClient(new Point((short)lp, (short)(lp >> 16)));
            int margin = Math.Max(4, (int)(5f * DeviceDpi / 96f));
            bool left = p.X <= margin, right = p.X >= ClientSize.Width - margin;
            bool top = p.Y <= margin, bottom = p.Y >= ClientSize.Height - margin;

            if (top && left) m.Result = (IntPtr)13;                          // HTTOPLEFT
            else if (top && right) m.Result = (IntPtr)14;                    // HTTOPRIGHT
            else if (bottom && left) m.Result = (IntPtr)16;                  // HTBOTTOMLEFT
            else if (bottom && right) m.Result = (IntPtr)17;                 // HTBOTTOMRIGHT
            else if (top) m.Result = (IntPtr)12;                             // HTTOP（要先于标题拖动判断）
            else if (bottom) m.Result = (IntPtr)15;                          // HTBOTTOM
            else if (left) m.Result = (IntPtr)10;                            // HTLEFT
            else if (right) m.Result = (IntPtr)11;                           // HTRIGHT
            else if (p.Y < S(HeaderH)) m.Result = (IntPtr)2;                 // 标题栏拖动
        }
    }
}
