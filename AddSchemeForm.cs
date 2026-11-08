using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace BootLauncher
{
    public class AddSchemeForm : DarkForm
    {
        const int MaxSchemes = 10;

        public string ChosenName { get; private set; }
        public List<string> ChosenApps { get; private set; }

        readonly Config _config;
        readonly Panel _pagePick = new Panel();
        readonly Panel _pageName = new Panel();
        readonly Panel _list;
        readonly List<ScanAppRow> _rows = new List<ScanAppRow>();
        readonly Label _pickHint;
        readonly Label _nameHint;
        readonly Label _error;
        TextBox _nameBox;

        // preselected：打开时默认勾上的应用（一般是主界面当前已选的），用户可以在第一步自由增减
        public AddSchemeForm(Config config, List<string> preselected)
        {
            _config = config;

            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 11F);
            ClientSize = new Size(620, 700);

            foreach (var p in new[] { _pagePick, _pageName })
            {
                p.Bounds = new Rectangle(0, 0, 620, 700);
                p.BackColor = BackColor;
                Controls.Add(p);
            }

            BuildPickPage(preselected ?? new List<string>(), out _pickHint, out _list);
            BuildNamePage(out _nameHint, out _error);
            _pageName.Visible = false;
            Shown += (s, e) => LayoutRows();
        }

        // ---------- 第一步：选应用 ----------

        void BuildPickPage(List<string> preselected, out Label hint, out Panel list)
        {
            hint = MakeDragLabel("", new Rectangle(30, 16, 560, 30));
            hint.Font = new Font("Segoe UI", 10.5F);
            hint.ForeColor = Color.FromArgb(0x9A, 0x9A, 0x9A);
            _pagePick.Controls.Add(hint);

            list = new SmoothPanel
            {
                Bounds = new Rectangle(30, 56, 560, 520),
                AutoScroll = true,
                BackColor = Theme.Bg,
                BorderStyle = BorderStyle.None
            };
            list.Resize += (s, e) => LayoutRows();
            _pagePick.Controls.Add(list);

            float scale = 1f;
            try
            {
                using (var g = CreateGraphics()) scale = g.DpiX / 96f;
            }
            catch { }
            int iconSize = Math.Max(32, (int)Math.Round(48 * scale));

            foreach (var app in _config.Apps)
            {
                Image img = null;
                try
                {
                    img = ShellIcon.GetLargeIcon(app.Path);
                    if (img == null)
                    {
                        using (var ico = Icon.ExtractAssociatedIcon(app.Path))
                            if (ico != null) img = ico.ToBitmap();
                    }
                    if (img == null) img = SystemIcons.Application.ToBitmap();
                    var row = new ScanAppRow(app, ShellIcon.Resize(img, iconSize))
                    {
                        Checked = preselected.Contains(app.Name)
                    };
                    row.MouseUp += (s, e) => UpdateCount();
                    _rows.Add(row);
                    list.Controls.Add(row);
                }
                catch { }
                finally
                {
                    if (img != null) img.Dispose();
                }
            }

            var selectAll = MakeButton(Strings.T("SelectAll"), new Rectangle(30, 592, 110, 40), false);
            selectAll.Click += (s, e) => SetAll(true);
            _pagePick.Controls.Add(selectAll);

            var clearAll = MakeButton(Strings.T("ClearAll"), new Rectangle(150, 592, 110, 40), false);
            clearAll.Click += (s, e) => SetAll(false);
            _pagePick.Controls.Add(clearAll);

            var next = MakeButton(Strings.T("Next"), new Rectangle(360, 648, 110, 42), true);
            next.Click += (s, e) => GoNext();
            _pagePick.Controls.Add(next);

            var cancel = MakeButton(Strings.T("Cancel"), new Rectangle(480, 648, 110, 42), false);
            cancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            _pagePick.Controls.Add(cancel);

            UpdateCount();
        }

        void LayoutRows()
        {
            if (_list == null) return;
            int scrollbar = _list.VerticalScroll.Visible ? SystemInformation.VerticalScrollBarWidth : 0;
            int w = Math.Max(100, _list.ClientSize.Width - 2 - scrollbar);
            for (int i = 0; i < _rows.Count; i++)
            {
                _rows[i].Location = new Point(1, 1 + i * _rows[i].Height);
                _rows[i].Width = w;
            }
            _list.AutoScrollMinSize = new Size(0, _rows.Count == 0 ? 0 : _rows.Count * _rows[0].Height + 2);
        }

        void SetAll(bool value)
        {
            foreach (var row in _rows)
            {
                row.Checked = value;
                row.Invalidate();
            }
            UpdateCount();
        }

        List<string> CheckedNames()
        {
            return _rows.Where(r => r.Checked).Select(r => r.App.Name).ToList();
        }

        void UpdateCount()
        {
            _pickHint.Text = string.Format(Strings.T("SchemePickHint"), _rows.Count(r => r.Checked));
        }

        void GoNext()
        {
            var names = CheckedNames();
            if (names.Count == 0)
            {
                MessageBox.Show(this, Strings.T("SchemeNeedApps"), "BootLauncher",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            _nameHint.Text = string.Format(Strings.T("SchemeNameHint"), names.Count);
            _pagePick.Visible = false;
            _pageName.Visible = true;
            _nameBox.Focus();
            _nameBox.SelectAll();
        }

        // ---------- 第二步：给方案命名 ----------

        string NextDefaultName()
        {
            for (int i = 1; i <= 99; i++)
            {
                string n = string.Format(Strings.T("SchemeN"), i);
                if (FindScheme(n) == null) return n;
            }
            return string.Format(Strings.T("SchemeN"), _config.Schemes.Count + 1);
        }

        void BuildNamePage(out Label hint, out Label error)
        {
            hint = MakeDragLabel("", new Rectangle(30, 16, 560, 30));
            hint.Font = new Font("Segoe UI", 10.5F);
            hint.ForeColor = Color.FromArgb(0x9A, 0x9A, 0x9A);
            _pageName.Controls.Add(hint);

            _pageName.Controls.Add(MakeLabel(Strings.T("SchemeName"), new Rectangle(30, 62, 560, 28)));

            _nameBox = MakeTextBox(new Rectangle(30, 94, 560, 36));
            _nameBox.Font = new Font("Segoe UI", 12F);
            _nameBox.MaxLength = 16;
            _nameBox.Text = NextDefaultName();
            _nameBox.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    Save();
                }
            };
            _nameBox.TextChanged += (s, e) => _error.Text = "";
            _pageName.Controls.Add(_nameBox);

            error = MakeDragLabel("", new Rectangle(30, 134, 560, 26));
            error.Font = new Font("Segoe UI", 10F);
            error.ForeColor = Color.FromArgb(255, 99, 99);
            _pageName.Controls.Add(error);

            if (_config.Schemes.Count > 0)
            {
                var exist = MakeDragLabel(Strings.T("SchemeExisting"), new Rectangle(30, 172, 560, 26));
                exist.Font = new Font("Segoe UI", 10F);
                exist.ForeColor = Color.FromArgb(0x9A, 0x9A, 0x9A);
                _pageName.Controls.Add(exist);

                int rowH = 40, gap = 6, y = 204;
                foreach (var sc in _config.Schemes.Take(MaxSchemes))
                {
                    var scheme = sc;
                    int count = scheme.Apps == null ? 0 : scheme.Apps.Count;
                    var row = MakeButton(scheme.Name + "    " + string.Format(Strings.T("SchemeAppsCount"), count),
                        new Rectangle(30, y, 560, rowH), false);
                    row.Click += (s, e) =>
                    {
                        _nameBox.Text = scheme.Name; // 点已有方案 = 用它的名字去覆盖
                        _nameBox.Focus();
                        _nameBox.SelectAll();
                    };
                    _pageName.Controls.Add(row);
                    y += rowH + gap;
                }
            }

            var back = MakeButton(Strings.T("Prev"), new Rectangle(30, 648, 110, 42), false);
            back.Click += (s, e) =>
            {
                _pageName.Visible = false;
                _pagePick.Visible = true;
            };
            _pageName.Controls.Add(back);

            var save = MakeButton(Strings.T("Save"), new Rectangle(360, 648, 110, 42), true);
            save.Click += (s, e) => Save();
            _pageName.Controls.Add(save);

            var cancel = MakeButton(Strings.T("Cancel"), new Rectangle(480, 648, 110, 42), false);
            cancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            _pageName.Controls.Add(cancel);
        }

        Scheme FindScheme(string name)
        {
            foreach (var s in _config.Schemes)
                if (s.Name == name) return s;
            return null;
        }

        void Save()
        {
            string name = _nameBox.Text.Trim();
            if (name.Length == 0)
            {
                _error.Text = Strings.T("SchemeNameEmpty");
                return;
            }
            var existing = FindScheme(name);
            if (existing == null && _config.Schemes.Count >= MaxSchemes)
            {
                _error.Text = Strings.T("SchemeMax");
                return;
            }
            if (existing != null && existing.Apps != null && existing.Apps.Count > 0)
            {
                if (MessageBox.Show(this, string.Format(Strings.T("ConfirmOverwriteScheme"), name),
                        "BootLauncher", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    return;
            }
            ChosenName = name;
            ChosenApps = CheckedNames();
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
