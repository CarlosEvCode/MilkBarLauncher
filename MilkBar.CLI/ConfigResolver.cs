using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace MilkBar.CLI
{
    public class ClientOptions
    {
        public string IP { get; set; } = "127.0.0.1";
        public string Port { get; set; } = "5050";
        public string Password { get; set; } = "";
        public string PlayerName { get; set; } = "Link";
        public string ServerName { get; set; } = "Zelda Server";
        public string Model { get; set; } = "Link:Link";
        public string? CemuExe { get; set; }
        public string? GameRpx { get; set; }
        public string? DllPath { get; set; }
        public int TimeoutSeconds { get; set; } = 20;
        public bool ShowHelp { get; set; } = false;
    }

    public static class ConfigResolver
    {
        public static ClientOptions ParseArgs(string[] args)
        {
            var options = new ClientOptions();

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                string next = (i + 1 < args.Length) ? args[i + 1] : "";

                switch (arg.ToLowerInvariant())
                {
                    case "--ip":
                    case "-i":
                        options.IP = next;
                        i++;
                        break;
                    case "--port":
                    case "-p":
                        options.Port = next;
                        i++;
                        break;
                    case "--password":
                    case "--pass":
                        options.Password = next;
                        i++;
                        break;
                    case "--name":
                    case "-n":
                        options.PlayerName = next;
                        i++;
                        break;
                    case "--server-name":
                        options.ServerName = next;
                        i++;
                        break;
                    case "--model":
                    case "-m":
                        options.Model = next;
                        i++;
                        break;
                    case "--cemu":
                    case "-c":
                        options.CemuExe = next;
                        i++;
                        break;
                    case "--game":
                    case "-g":
                        options.GameRpx = next;
                        i++;
                        break;
                    case "--dll":
                    case "-d":
                        options.DllPath = next;
                        i++;
                        break;
                    case "--timeout":
                    case "-t":
                        if (int.TryParse(next, out int to))
                        {
                            options.TimeoutSeconds = to;
                        }
                        i++;
                        break;
                    case "--help":
                    case "-h":
                    case "/?":
                        options.ShowHelp = true;
                        break;
                }
            }

            ResolveDefaults(options);
            return options;
        }

        private static void ResolveDefaults(ClientOptions options)
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            string[] bcmlCandidates = new[]
            {
                Path.Combine(localAppData, "bcml", "settings.json"),
                Path.Combine(appData, "bcml", "settings.json"),
                Path.Combine(userProfile, ".config", "bcml", "settings.json"),
                @"C:\cemu_1.26.2\bcml\settings.json"
            };

            Dictionary<string, string>? bcml = null;
            foreach (var candidate in bcmlCandidates)
            {
                if (File.Exists(candidate))
                {
                    try
                    {
                        string json = File.ReadAllText(candidate);
                        bcml = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                        if (bcml != null) break;
                    }
                    catch
                    {
                        // Continue search
                    }
                }
            }

            // Resolve Cemu Exe
            if (string.IsNullOrEmpty(options.CemuExe))
            {
                if (bcml != null && bcml.TryGetValue("cemu_dir", out string? cemuDir) && !string.IsNullOrEmpty(cemuDir))
                {
                    string candidate = Path.Combine(cemuDir, "Cemu.exe");
                    if (File.Exists(candidate)) options.CemuExe = candidate;
                }

                if (string.IsNullOrEmpty(options.CemuExe))
                {
                    string[] cemuDefaults = new[]
                    {
                        @"C:\cemu_1.26.2\Cemu.exe",
                        @"C:\cemu\Cemu.exe",
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "cemu_1.26.2", "Cemu.exe")
                    };

                    foreach (var c in cemuDefaults)
                    {
                        if (File.Exists(c))
                        {
                            options.CemuExe = c;
                            break;
                        }
                    }
                }
            }

            // Resolve Game RPX
            if (string.IsNullOrEmpty(options.GameRpx))
            {
                if (bcml != null && bcml.TryGetValue("game_dir", out string? gameDir) && !string.IsNullOrEmpty(gameDir))
                {
                    string rpx = Path.Combine(gameDir.Replace("content", "code"), "U-King.rpx");
                    if (File.Exists(rpx)) options.GameRpx = rpx;
                }

                if (string.IsNullOrEmpty(options.GameRpx) && Directory.Exists(@"C:\Games"))
                {
                    var rpxFiles = Directory.GetFiles(@"C:\Games", "U-King.rpx", SearchOption.AllDirectories);
                    if (rpxFiles.Length > 0)
                    {
                        options.GameRpx = rpxFiles[0];
                    }
                }
            }

            // Resolve InjectDLL
            if (string.IsNullOrEmpty(options.DllPath))
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string[] dllCandidates = new[]
                {
                    Path.Combine(baseDir, "Resources", "InjectDLL.dll"),
                    Path.Combine(baseDir, "InjectDLL.dll"),
                    @"C:\MilkBarLauncher\Resources\InjectDLL.dll"
                };

                foreach (var dll in dllCandidates)
                {
                    if (File.Exists(dll))
                    {
                        options.DllPath = dll;
                        break;
                    }
                }
            }
        }
    }
}
