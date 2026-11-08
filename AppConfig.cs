using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace BootLauncher
{
    public class Scheme
    {
        public string Name { get; set; }
        public List<string> Apps { get; set; }

        public Scheme()
        {
            Apps = new List<string>();
        }
    }

    public class Config
    {
        public List<AppItem> Apps { get; set; }
        public List<string> LastSelected { get; set; }
        public List<Scheme> Schemes { get; set; }
        public List<string> Categories { get; set; }
        public string CurrentScheme { get; set; }
        public int IconSize { get; set; }
        public bool AutoStart { get; set; }
        public string Language { get; set; }
        public int WindowX { get; set; }
        public int WindowY { get; set; }
        public int WindowW { get; set; }
        public int WindowH { get; set; }

        public Config()
        {
            Apps = new List<AppItem>();
            LastSelected = new List<string>();
            Schemes = new List<Scheme>();
            Categories = new List<string>();
            CurrentScheme = "";
            IconSize = 128;
            AutoStart = false;
            Language = "system";
        }
    }

    static class AppConfig
    {
        static readonly string Dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BootLauncher");
        static readonly string FilePath = Path.Combine(Dir, "config.json");
        static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

        public static Config Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var config = Serializer.Deserialize<Config>(File.ReadAllText(FilePath));
                    if (config != null)
                    {
                        if (config.Apps == null) config.Apps = new List<AppItem>();
                        if (config.LastSelected == null) config.LastSelected = new List<string>();
                        if (config.Schemes == null) config.Schemes = new List<Scheme>();
                        if (config.Categories == null) config.Categories = new List<string>();
                        if (config.CurrentScheme == null) config.CurrentScheme = "";
                        if (config.IconSize < 64 || config.IconSize > 256) config.IconSize = 128;
                        if (string.IsNullOrEmpty(config.Language)) config.Language = "system";
                        return config;
                    }
                }
            }
            catch { }
            return new Config();
        }

        public static void Save(Config config)
        {
            try
            {
                Directory.CreateDirectory(Dir);
                File.WriteAllText(FilePath, Serializer.Serialize(config));
            }
            catch { }
        }
    }
}
