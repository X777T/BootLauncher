using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace BootLauncher
{
    static class AppScanner
    {
        public static List<AppItem> Scan()
        {
            var apps = new Dictionary<string, AppItem>(StringComparer.OrdinalIgnoreCase);

            ScanShortcutFolder(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), apps);
            ScanShortcutFolder(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), apps);

            ScanUninstallKey(Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall"), apps);
            ScanUninstallKey(Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall"), apps);
            ScanUninstallKey(Registry.LocalMachine.OpenSubKey(@"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"), apps);

            return apps.Values.OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        static void ScanShortcutFolder(string folder, Dictionary<string, AppItem> apps)
        {
            if (!Directory.Exists(folder)) return;
            try
            {
                foreach (var lnk in Directory.GetFiles(folder, "*.lnk", SearchOption.AllDirectories))
                {
                    string target = ResolveShortcut(lnk);
                    if (string.IsNullOrEmpty(target) || !File.Exists(target)) continue;
                    if (!Path.GetExtension(target).ToLowerInvariant().EndsWith(".exe")) continue;
                    string name = Path.GetFileNameWithoutExtension(lnk);
                    if (IsUninstaller(name) || IsRuntimeComponent(name)) continue;
                    Add(apps, name, target);
                }
            }
            catch { }
        }

        static string ResolveShortcut(string lnkPath)
        {
            try
            {
                var shellType = Type.GetTypeFromProgID("WScript.Shell");
                dynamic shell = Activator.CreateInstance(shellType);
                dynamic shortcut = shell.CreateShortcut(lnkPath);
                string target = shortcut.TargetPath;
                return string.IsNullOrEmpty(target) ? null : target;
            }
            catch
            {
                return null;
            }
        }

        static void ScanUninstallKey(RegistryKey key, Dictionary<string, AppItem> apps)
        {
            if (key == null) return;
            try
            {
                foreach (var subName in key.GetSubKeyNames())
                {
                    using (var sub = key.OpenSubKey(subName))
                    {
                        if (sub == null) continue;
                        if (Convert.ToInt32(sub.GetValue("SystemComponent", 0)) == 1) continue;
                        string name = sub.GetValue("DisplayName") as string;
                        if (string.IsNullOrEmpty(name)) continue;
                        if (IsUninstaller(name)) continue;
                        if (IsRuntimeComponent(name)) continue;
                        string exe = FindExe(sub);
                        if (exe != null) Add(apps, name, exe);
                    }
                }
            }
            catch { }
            finally
            {
                key.Close();
            }
        }

        static string FindExe(RegistryKey sub)
        {
            string icon = sub.GetValue("DisplayIcon") as string;
            if (!string.IsNullOrEmpty(icon))
            {
                string path = icon.Split(',')[0].Trim().Trim('"');
                if (path.ToLowerInvariant().EndsWith(".exe") && File.Exists(path) &&
                    !IsUninstaller(Path.GetFileNameWithoutExtension(path)))
                    return path;
            }
            string dir = sub.GetValue("InstallLocation") as string;
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
            {
                try
                {
                    foreach (var file in Directory.GetFiles(dir, "*.exe", SearchOption.TopDirectoryOnly))
                    {
                        if (!IsUninstaller(file) && !IsUninstaller(Path.GetFileNameWithoutExtension(file)))
                            return file;
                    }
                }
                catch { }
            }
            return null;
        }

        static bool IsUninstaller(string name)
        {
            string lower = name.ToLowerInvariant();
            return lower.Contains("卸载") || lower.Contains("uninstall") ||
                   lower.Contains("unins") || lower.Contains("update");
        }

        static bool IsRuntimeComponent(string name)
        {
            string lower = name.ToLowerInvariant();
            return lower.Contains("visual c++") || lower.Contains("redistributable") ||
                   lower.Contains(".net") || lower.Contains("webview2") ||
                   lower.Contains("windows sdk") || lower.Contains("windows desktop");
        }

        static void Add(Dictionary<string, AppItem> apps, string name, string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
            string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            if (path.StartsWith(winDir, StringComparison.OrdinalIgnoreCase)) return;
            if (path.IndexOf("Package Cache", StringComparison.OrdinalIgnoreCase) >= 0) return;
            if (IsUninstaller(Path.GetFileNameWithoutExtension(path))) return;
            if (!apps.ContainsKey(path))
                apps[path] = new AppItem { Name = name.Trim(), Path = path };
        }
    }
}
