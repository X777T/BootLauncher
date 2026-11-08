using System;
using System.IO;
using System.Windows.Forms;

namespace BootLauncher
{
    static class AutoStart
    {
        static string ShortcutPath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Startup),
                    "BootLauncher.lnk");
            }
        }

        public static bool IsEnabled()
        {
            return File.Exists(ShortcutPath);
        }

        public static void SetEnabled(bool enabled)
        {
            try
            {
                if (enabled)
                {
                    var shellType = Type.GetTypeFromProgID("WScript.Shell");
                    dynamic shell = Activator.CreateInstance(shellType);
                    dynamic shortcut = shell.CreateShortcut(ShortcutPath);
                    shortcut.TargetPath = Application.ExecutablePath;
                    shortcut.WorkingDirectory = Path.GetDirectoryName(Application.ExecutablePath);
                    shortcut.Description = "BootLauncher";
                    shortcut.Save();
                }
                else if (File.Exists(ShortcutPath))
                {
                    File.Delete(ShortcutPath);
                }
            }
            catch { }
        }
    }
}
