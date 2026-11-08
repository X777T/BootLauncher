using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace BootLauncher
{
    public class AppEditForm : DarkForm
    {
        readonly TextBox _nameBox;
        readonly TextBox _pathBox;
        public AppItem Result { get; private set; }

        public AppEditForm(AppItem app)
        {
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(580, 300);
            Font = new Font("Segoe UI", 11F);

            var hint = MakeDragLabel(Strings.T("EditHint"), new Rectangle(30, 18, 520, 48));
            hint.Font = new Font("Segoe UI", 9.75F);
            hint.ForeColor = Color.FromArgb(0x9A, 0x9A, 0x9A);
            hint.TextAlign = ContentAlignment.TopLeft;
            Controls.Add(hint);

            Controls.Add(MakeLabel(Strings.T("Name"), new Rectangle(30, 82, 70, 32)));
            _nameBox = MakeTextBox(new Rectangle(110, 80, 440, 32));
            Controls.Add(_nameBox);

            Controls.Add(MakeLabel(Strings.T("Path"), new Rectangle(30, 134, 70, 32)));
            _pathBox = MakeTextBox(new Rectangle(110, 132, 262, 32));
            Controls.Add(_pathBox);

            var browse = MakeButton(Strings.T("Browse"), new Rectangle(380, 130, 80, 36), false);
            browse.Click += (s, e) =>
            {
                using (var ofd = new OpenFileDialog { Filter = "程序|*.exe;*.lnk;*.bat;*.cmd|所有文件|*.*" })
                {
                    if (ofd.ShowDialog(this) == DialogResult.OK)
                    {
                        _pathBox.Text = ofd.FileName;
                        if (_nameBox.Text.Trim().Length == 0)
                            _nameBox.Text = Path.GetFileNameWithoutExtension(ofd.FileName);
                    }
                }
            };
            Controls.Add(browse);

            var browseFolder = MakeButton(Strings.T("BrowseFolder"), new Rectangle(468, 130, 82, 36), false);
            browseFolder.Click += (s, e) =>
            {
                using (var fbd = new FolderBrowserDialog())
                {
                    if (fbd.ShowDialog(this) == DialogResult.OK)
                    {
                        _pathBox.Text = fbd.SelectedPath;
                        if (_nameBox.Text.Trim().Length == 0)
                            _nameBox.Text = new DirectoryInfo(fbd.SelectedPath).Name;
                    }
                }
            };
            Controls.Add(browseFolder);

            var ok = MakeButton(Strings.T("OK"), new Rectangle(330, 224, 100, 42), true);
            ok.Click += (s, e) => OnOk();
            Controls.Add(ok);

            var cancel = MakeButton(Strings.T("Cancel"), new Rectangle(440, 224, 110, 42), false);
            cancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(cancel);

            if (app != null)
            {
                _nameBox.Text = app.Name;
                _pathBox.Text = app.Path;
            }
        }

        void OnOk()
        {
            var name = _nameBox.Text.Trim();
            var path = _pathBox.Text.Trim();
            if (name.Length == 0)
            {
                MessageBox.Show(this, Strings.T("AppNameEmpty"), "BootLauncher",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (path.Length == 0 || (!File.Exists(path) && !Directory.Exists(path)))
            {
                MessageBox.Show(this, Strings.T("PathNotExist"), "BootLauncher",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            Result = new AppItem { Name = name, Path = path };
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
