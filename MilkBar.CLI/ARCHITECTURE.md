# MilkBar CLI: Headless Architecture & Design Specification

## 1. Overview
`MilkBar.CLI` is a headless, console-based client for *The Legend of Zelda: Breath of the Wild Multiplayer* mod on Linux/Wine and Windows. It completely bypasses the legacy WPF GUI and its associated Win32 window hooking timers, preventing flickering and rendering instability under modern Wayland compositors (such as Hyprland, Sway, and GNOME Wayland).

---

## 2. Problem Statement
The original `MilkBarLauncher` used a Windows Presentation Foundation (WPF) interface designed to overlay on top of `Cemu.exe`. To achieve this, `CemuFollower.cs` invoked Win32 APIs (`SetParent`, `MoveWindow`, `DwmGetWindowAttribute`) on a 50ms polling timer. Under XWayland and Wine staging, this continuous reparenting loop causes rapid focus oscillation, window resizing anomalies, and high CPU usage.

---

## 3. Architecture & Execution Pipeline

```
+-------------------------------------------------------------+
|                      MilkBar.CLI.exe                        |
+-------------------------------------------------------------+
   |
   | 1. Config & Path Resolution
   v
   Reads %LOCALAPPDATA%\bcml\settings.json, C:\cemu_1.26.2, C:\Games
   Resolves Cemu.exe, U-King.rpx, and Resources\InjectDLL.dll
   |
   | 2. Spawn Cemu Process
   v
   Process.Start("Cemu.exe", "-g \"C:\Games\...\U-King.rpx\"")
   |
   | 3. DLL Injection (kernel32.dll Win32 P/Invoke)
   v
   - OpenProcess(PROCESS_ALL_ACCESS, pid)
   - VirtualAllocEx(hndProc, dllPath.Length)
   - WriteProcessMemory(hndProc, lpAddress, dllPathBytes)
   - CreateRemoteThread(hndProc, LoadLibraryA, lpAddress)
   |
   | 4. IPC Pipe Server
   v
   NamedPipeServerStream("languageConnectionPipe", PipeDirection.InOut)
   Waits for InjectDLL.dll connection handshake
   |
   | 5. Network Handshake Instructions
   v
   Send Instruction: "!connect;{IP};{PORT};{PASSWORD};{NAME};{SERVER_NAME};0;{MODEL};[END]"
   Send Instruction: "!startServerLoop;[END]"
   |
   | 6. Lifecycle Monitoring
   v
   Wait for Cemu process exit or SIGINT / Ctrl+C -> Clean pipe disconnect
```

---

## 4. CLI Arguments Reference

| Argument | Short | Default | Description |
| :--- | :--- | :--- | :--- |
| `--ip` | `-i` | `127.0.0.1` | Target multiplayer server IP address. |
| `--port` | `-p` | `5050` | Target server port. |
| `--password` | `--pass` | `""` | Server connection password (if configured). |
| `--name` | `-n` | `Link` | Player display name in the session. |
| `--model` | `-m` | `Link:Link` | Character model identifier. |
| `--cemu` | `-c` | Auto-detect | Explicit path to `Cemu.exe`. |
| `--game` | `-g` | Auto-detect | Explicit path to base game `U-King.rpx`. |
| `--dll` | `-d` | Auto-detect | Explicit path to `InjectDLL.dll`. |
| `--timeout` | `-t` | `20` | Named pipe connection timeout in seconds. |
| `--help` | `-h` | - | Display usage information. |

---

## 5. Build Instructions

### Requirements
- .NET 8.0 SDK (`dotnet-sdk-8.0` on Arch/CachyOS/Ubuntu)

### Compiling
```bash
cd MilkBar.CLI
dotnet publish -c Release -r win-x64 --no-self-contained -o ./publish
```

### Running with Wine
```bash
WINEPREFIX=~/.local/share/wineprefixes/botw-multiplayer wine ./publish/MilkBar.CLI.exe --ip 127.0.0.1 --port 5050
```
