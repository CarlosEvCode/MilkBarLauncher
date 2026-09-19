using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace MilkBar.CLI
{
    public static class Injector
    {
        private static readonly IntPtr INTPTR_ZERO = (IntPtr)0;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint dwDesiredAccess, int bInheritHandle, uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern int CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, IntPtr dwSize, uint flAllocationType, uint flProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern int WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] buffer, uint size, int lpNumberOfBytesWritten);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr lpThreadAttributes, uint dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, IntPtr lpThreadId);

        public static Process Inject(string processName, string dllPath, List<Process>? filter = null)
        {
            Process? processToInject = GetProcesses(processName)
                .Where(process => filter != null ? !filter.Any(p => p.Id == process.Id) : true)
                .FirstOrDefault();

            if (processToInject == null)
            {
                throw new InvalidOperationException($"Failed to find process '{processName}' for injection.");
            }

            if (!File.Exists(dllPath))
            {
                processToInject.Kill();
                throw new FileNotFoundException($"Mod DLL not found at: {dllPath}");
            }

            if (!ProcessInject((uint)processToInject.Id, dllPath))
            {
                processToInject.Kill();
                throw new InvalidOperationException("Failed to inject DLL into Cemu memory space.");
            }

            return processToInject;
        }

        private static bool ProcessInject(uint processId, string dllPath)
        {
            IntPtr hndProc = OpenProcess((0x2 | 0x8 | 0x10 | 0x20 | 0x400), 1, processId);
            if (hndProc == INTPTR_ZERO)
            {
                return false;
            }

            IntPtr lpAddress = VirtualAllocEx(hndProc, IntPtr.Zero, (IntPtr)dllPath.Length + 1, (0x1000 | 0x2000), 0x40);
            if (lpAddress == INTPTR_ZERO)
            {
                CloseHandle(hndProc);
                return false;
            }

            byte[] bytes = Encoding.ASCII.GetBytes(dllPath + "\0");
            if (WriteProcessMemory(hndProc, lpAddress, bytes, (uint)bytes.Length, 0) == 0)
            {
                CloseHandle(hndProc);
                return false;
            }

            IntPtr kernel32 = GetModuleHandle("kernel32.dll");
            IntPtr loadlibAddy = GetProcAddress(kernel32, "LoadLibraryA");
            if (loadlibAddy == IntPtr.Zero)
            {
                CloseHandle(hndProc);
                return false;
            }

            IntPtr hThread = CreateRemoteThread(hndProc, IntPtr.Zero, 0, loadlibAddy, lpAddress, 0, IntPtr.Zero);
            if (hThread == IntPtr.Zero)
            {
                CloseHandle(hndProc);
                return false;
            }

            CloseHandle(hThread);
            CloseHandle(hndProc);
            return true;
        }

        public static List<Process> GetProcesses(string processName)
        {
            return Process.GetProcessesByName(processName).ToList();
        }
    }
}
