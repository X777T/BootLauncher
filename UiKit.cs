using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace BootLauncher
{
    static class Theme
    {
        public static readonly Color Bg = Color.FromArgb(14, 15, 19);
        public static readonly Color Surface = Color.FromArgb(20, 21, 26);
        public static readonly Color Card = Color.FromArgb(30, 32, 39);
        public static readonly Color CardHover = Color.FromArgb(40, 43, 52);
        public static readonly Color Border = Color.FromArgb(44, 47, 56);
        public static readonly Color Text = Color.FromArgb(242, 243, 245);
        public static readonly Color TextDim = Color.FromArgb(150, 153, 162);
        public static readonly Color Accent = Color.FromArgb(10, 132, 255);
        public static readonly Color AccentHi = Color.FromArgb(64, 156, 255);
        public static readonly Color AccentLo = Color.FromArgb(0, 104, 224);
    }

    static class UiKit
    {
        [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        [System.Runtime.InteropServices.DllImport("uxtheme.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        static extern int SetWindowTheme(IntPtr hwnd, string subAppName, string subIdList);

        // Win11 系统级圆角（由 DWM 抗锯齿裁剪）+ 自定义细边框色。返回 false 表示系统不支持（Win10），调用方回退到 Region。
        public static bool RoundWindow(IntPtr hwnd, Color border)
        {
            int round = 2; // DWMWCP_ROUND
            bool ok = DwmSetWindowAttribute(hwnd, 33, ref round, sizeof(int)) == 0;
            if (ok)
            {
                int c = border.R | (border.G << 8) | (border.B << 16); // COLORREF=0x00BBGGRR
                DwmSetWindowAttribute(hwnd, 34, ref c, sizeof(int));
            }
            return ok;
        }

        // 让原生滚动条用系统暗色主题，避免深色界面里冒出亮白色滚动条
        public static void DarkScroll(IntPtr hwnd)
        {
            try { SetWindowTheme(hwnd, "DarkMode_Explorer", null); } catch { }
        }

        // 统一的深色圆角菜单
        public static System.Windows.Forms.ContextMenuStrip NewMenu(Font font)
        {
            var menu = new System.Windows.Forms.ContextMenuStrip
            {
                BackColor = Theme.Card,
                ForeColor = Theme.Text,
                ShowImageMargin = false,
                Font = font,
                Renderer = new DarkMenuRenderer(),
                Padding = new System.Windows.Forms.Padding(4)
            };
            menu.HandleCreated += (s, e) => RoundWindow(menu.Handle, Theme.Border);
            return menu;
        }

        public static Color Lerp(Color a, Color b, float t)
        {
            if (t < 0f) t = 0f;
            if (t > 1f) t = 1f;
            return Color.FromArgb(
                (int)Math.Round(a.A + (b.A - a.A) * t),
                (int)Math.Round(a.R + (b.R - a.R) * t),
                (int)Math.Round(a.G + (b.G - a.G) * t),
                (int)Math.Round(a.B + (b.B - a.B) * t));
        }

        public static float EaseOut(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            float u = 1f - t;
            return 1f - u * u * u;
        }

        // 带轻微回弹，用于勾选徽标弹出
        public static float EaseBack(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        public static bool Approach(ref float value, float target, float step)
        {
            if (value == target) return false;
            if (Math.Abs(target - value) <= step) value = target;
            else value += Math.Sign(target - value) * step;
            return true;
        }

        public static GraphicsPath RoundRect(RectangleF r, float radius)
        {
            var path = new GraphicsPath();
            radius = Math.Min(radius, Math.Min(r.Width, r.Height) / 2f);
            if (radius < 0.5f)
            {
                path.AddRectangle(r);
                return path;
            }
            float d = radius * 2f;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
