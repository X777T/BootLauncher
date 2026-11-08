using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace BootLauncher
{
    // 从已添加的应用里勾选若干个（给分类加应用用），不涉及选文件。
    public class AppPickForm : DarkForm
    {
        public List<AppItem> Picked { get; private set; }

        readonly Panel _list;
        readonly List<ScanAppRow> _rows = new List<ScanAppRow>();
        readonly Label _hint;

        public AppPickForm(string title, IList<AppItem> candidates)
        {
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 11F);
            ClientSize = new Size(620, 700);

            var t = MakeDragLabel(title, new Rectangle(30, 16, 560, 36));
            t.Font = new Font("Segoe UI", 14F, FontStyle.Bold);
            t.ForeColor = Color.White;
            Controls.Add(t);

            _hint = MakeDragLabel("", new Rectangle(30, 54, 560, 24));
            _hint.Font = new Font("Segoe UI", 10F);
            _hint.ForeColor = Color.FromArgb(0x9A, 0x9A, 0x9A);
            Controls.Add(_hint);

            _list = new SmoothPanel
            {
                Bounds = new Rectangle(30, 88, 560, 490),
                AutoScroll = true,
                BackColor = Theme.Bg,
                BorderStyle = BorderStyle.None
            };
            _list.Resize += (s, e) => LayoutRows();
            Controls.Add(_list);

            float scale = 1f;
            try
            {
                using (var g = CreateGraphics()) scale = g.DpiX / 96f;
            }
            catch { }
            int iconSize = Math.Max(32, (int)Math.Round(48 * scale));

            foreach (var app in candidates)
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
                        // 已经归在别的分类里的应用，标出它现在属于哪个分类
                        Badge = string.IsNullOrEmpty(app.Category) ? null : string.Format(Strings.T("InCategory"), app.Category)
                    };
                    row.MouseUp += (s, e) => UpdateCount();
                    _rows.Add(row);
                    _list.Controls.Add(row);
                }
                catch { }
                finally
                {
                    if (img != null) img.Dispose();
                }
            }

            var selectAll = MakeButton(Strings.T("SelectAll"), new Rectangle(30, 594, 110, 40), false);
            selectAll.Click += (s, e) => SetAll(true);
            Controls.Add(selectAll);

            var clearAll = MakeButton(Strings.T("ClearAll"), new Rectangle(150, 594, 110, 40), false);
            clearAll.Click += (s, e) => SetAll(false);
            Controls.Add(clearAll);

            var ok = MakeButton(Strings.T("Add"), new Rectangle(360, 648, 110, 42), true);
            ok.Click += (s, e) => OnOk();
            Controls.Add(ok);

            var cancel = MakeButton(Strings.T("Cancel"), new Rectangle(480, 648, 110, 42), false);
            cancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(cancel);

            UpdateCount();
            Shown += (s, e) => LayoutRows();
        }

        void LayoutRows()
        {
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

        void UpdateCount()
        {
            _hint.Text = string.Format(Strings.T("PickedCount"), _rows.Count(r => r.Checked));
        }

        void OnOk()
        {
            var picked = _rows.Where(r => r.Checked).Select(r => r.App).ToList();
            if (picked.Count == 0)
            {
                MessageBox.Show(this, Strings.T("NoneSelected"), "BootLauncher",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            Picked = picked;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
