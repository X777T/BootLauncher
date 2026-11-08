using System;
using System.Drawing;
using System.Windows.Forms;

namespace BootLauncher
{
    // 单行文本输入对话框（新建 / 重命名分类用）。validate 返回错误文案，返回 null 表示通过。
    public class PromptForm : DarkForm
    {
        public string Value { get; private set; }

        readonly TextBox _tb;
        readonly Label _error;
        readonly Func<string, string> _validate;

        public PromptForm(string title, string label, string initial, Func<string, string> validate)
        {
            _validate = validate;

            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 10F);
            ClientSize = new Size(380, 176);

            var t = MakeDragLabel(title, new Rectangle(22, 14, 336, 30));
            t.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            t.ForeColor = Color.White;
            Controls.Add(t);

            var l = MakeDragLabel(label, new Rectangle(22, 48, 336, 22));
            l.Font = new Font("Segoe UI", 9F);
            l.ForeColor = Color.FromArgb(0x9A, 0x9A, 0x9A);
            Controls.Add(l);

            _tb = MakeTextBox(new Rectangle(22, 74, 336, 30));
            _tb.Text = initial ?? "";
            _tb.MaxLength = 24;
            _tb.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    Submit();
                }
            };
            _tb.TextChanged += (s, e) => _error.Text = "";
            Controls.Add(_tb);

            _error = MakeDragLabel("", new Rectangle(22, 108, 336, 22));
            _error.Font = new Font("Segoe UI", 9F);
            _error.ForeColor = Color.FromArgb(255, 99, 99);
            Controls.Add(_error);

            var ok = MakeButton(Strings.T("OK"), new Rectangle(22 + 336 - 90 - 98, 134, 90, 34), true);
            ok.Click += (s, e) => Submit();
            Controls.Add(ok);

            var cancel = MakeButton(Strings.T("Cancel"), new Rectangle(22 + 336 - 90, 134, 90, 34), false);
            cancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(cancel);

            Shown += (s, e) => { _tb.Focus(); _tb.SelectAll(); };
        }

        void Submit()
        {
            string v = _tb.Text.Trim();
            string err = _validate != null ? _validate(v) : null;
            if (err != null)
            {
                _error.Text = err;
                return;
            }
            Value = v;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
