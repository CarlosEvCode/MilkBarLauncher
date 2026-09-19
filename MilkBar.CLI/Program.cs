using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace MilkBar.CLI
{
    internal class Program
    {
        private static Process? _cemuProcess;
        private static readonly ManualResetEventSlim _shutdownEvent = new ManualResetEventSlim(false);

        private static int Main(string[] args)
        {
            Console.WriteLine("========================================================================");
            Console.WriteLine(" MilkBar CLI - Headless Multiplayer Client for Zelda: Breath of the Wild");
            Console.WriteLine("========================================================================");

            ClientOptions options = ConfigResolver.ParseArgs(args);

            if (options.ShowHelp)
            {
                PrintHelp();
                return 0;
            }

            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                Console.WriteLine("\n[INFO] Interrupt received. Shutting down multiplayer session...");
                NamedPipes.Disconnect();
                _shutdownEvent.Set();
            };

            try
            {
                // Validate prerequisites
                if (string.IsNullOrEmpty(options.CemuExe) || !File.Exists(options.CemuExe))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[ERROR] Cemu executable not found. Specify with --cemu <path>");
                    Console.ResetColor();
                    return 1;
                }

                if (string.IsNullOrEmpty(options.GameRpx) || !File.Exists(options.GameRpx))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[ERROR] Game executable (U-King.rpx) not found. Specify with --game <path>");
                    Console.ResetColor();
                    return 1;
                }

                if (string.IsNullOrEmpty(options.DllPath) || !File.Exists(options.DllPath))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[ERROR] InjectDLL.dll not found. Specify with --dll <path>");
                    Console.ResetColor();
                    return 1;
                }

                Console.WriteLine($"[1/5] Target Server:    {options.IP}:{options.Port}");
                Console.WriteLine($"[1/5] Player Name:      {options.PlayerName}");
                Console.WriteLine($"[1/5] Cemu Path:        {options.CemuExe}");
                Console.WriteLine($"[1/5] Game RPX:         {options.GameRpx}");
                Console.WriteLine($"[1/5] Mod DLL:          {options.DllPath}");
                Console.WriteLine();

                // 1. Get existing Cemu processes to filter out
                var existingProcesses = Injector.GetProcesses("Cemu");

                // 2. Launch Cemu with game RPX
                Console.WriteLine("[2/5] Starting Cemu process...");
                var psi = new ProcessStartInfo
                {
                    FileName = options.CemuExe,
                    Arguments = $"-g \"{options.GameRpx}\"",
                    UseShellExecute = false,
                    WorkingDirectory = Path.GetDirectoryName(options.CemuExe) ?? ""
                };

                _cemuProcess = Process.Start(psi);
                if (_cemuProcess == null)
                {
                    throw new InvalidOperationException("Failed to spawn Cemu process.");
                }

                Thread.Sleep(750);

                // 3. Inject mod DLL
                Console.WriteLine("[3/5] Injecting InjectDLL.dll into Cemu memory...");
                _cemuProcess = Injector.Inject("Cemu", Path.GetFullPath(options.DllPath), existingProcesses);
                Console.WriteLine($"      Injection successful (PID: {_cemuProcess.Id})");

                // 4. Start Named Pipe Server
                Console.WriteLine("[4/5] Establishing Named Pipe connection with Cemu...");
                NamedPipes.StartServer(options.TimeoutSeconds);
                Console.WriteLine("      Named Pipe connected successfully.");

                // 5. Send Connection & StartLoop instructions
                Console.WriteLine($"[5/5] Connecting to server {options.IP}:{options.Port}...");
                string connectCmd = $"!connect;{options.IP};{options.Port};{options.Password};{options.PlayerName};{options.ServerName};0;{options.Model};";
                
                if (!NamedPipes.SendInstruction(connectCmd))
                {
                    throw new InvalidOperationException("Server connection handshake failed via internal pipe.");
                }

                Thread.Sleep(100);

                if (!NamedPipes.SendInstruction("!startServerLoop"))
                {
                    throw new InvalidOperationException("Failed to activate multiplayer sync loop.");
                }

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine();
                Console.WriteLine("========================================================================");
                Console.WriteLine(" [SUCCESS] Multiplayer session active! Enjoy playing in Hyrule.");
                Console.WriteLine("========================================================================");
                Console.ResetColor();
                Console.WriteLine(" Press Ctrl+C or close Cemu to disconnect.");

                // Monitor Cemu process lifecycle
                _cemuProcess.EnableRaisingEvents = true;
                _cemuProcess.Exited += (s, e) =>
                {
                    Console.WriteLine("\n[INFO] Cemu has exited. Closing multiplayer connection.");
                    NamedPipes.Disconnect();
                    _shutdownEvent.Set();
                };

                _shutdownEvent.Wait();
                return 0;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n[ERROR] {ex.Message}");
                Console.ResetColor();

                if (_cemuProcess != null && !_cemuProcess.HasExited)
                {
                    try { _cemuProcess.Kill(); } catch { }
                }

                NamedPipes.Disconnect();
                return 1;
            }
        }

        private static void PrintHelp()
        {
            Console.WriteLine("Usage: MilkBar.CLI.exe [OPTIONS]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  -i, --ip <ip>             Target server IP address (default: 127.0.0.1)");
            Console.WriteLine("  -p, --port <port>         Target server port (default: 5050)");
            Console.WriteLine("  --pass, --password <pwd>  Server password (default: none)");
            Console.WriteLine("  -n, --name <player>       Player display name (default: Link)");
            Console.WriteLine("  -m, --model <model>       Player character model (default: Link:Link)");
            Console.WriteLine("  -c, --cemu <path>         Path to Cemu.exe (auto-detected if omitted)");
            Console.WriteLine("  -g, --game <path>         Path to U-King.rpx (auto-detected if omitted)");
            Console.WriteLine("  -d, --dll <path>          Path to InjectDLL.dll (auto-detected if omitted)");
            Console.WriteLine("  -t, --timeout <sec>       Connection pipe timeout in seconds (default: 20)");
            Console.WriteLine("  -h, --help                Show this help message and exit");
        }
    }
}
