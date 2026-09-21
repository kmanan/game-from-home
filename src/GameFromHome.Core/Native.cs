using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace GameFromHome.Core;

internal static class Native
{
    [StructLayout(LayoutKind.Sequential)] internal struct FileTime { public uint Low, High; public readonly long Value => ((long)High << 32) | Low; public static FileTime From(long v) => new() { Low = (uint)v, High = (uint)(v >> 32) }; }
    [StructLayout(LayoutKind.Sequential)] internal struct MemoryStatus { public uint Length, Load; public ulong Total, Available, TotalPage, AvailablePage, TotalVirtual, AvailableVirtual, Extended; }
    [StructLayout(LayoutKind.Sequential)] internal struct MemoryCounters { public uint Size, Faults; public nuint PeakWorking, Working, PeakPaged, Paged, PeakNonPaged, NonPaged, Pagefile, PeakPagefile, PrivateUsage, PrivateWorking, SharedCommit; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] internal struct ProcessEntry { public uint Size, Usage, Pid; public nuint Heap; public uint Module, Threads, Parent; public int Priority; public uint Flags; [MarshalAs(UnmanagedType.ByValTStr, SizeConst=260)] public string Exe; }
    [DllImport("kernel32.dll", SetLastError=true)] internal static extern SafeProcessHandle OpenProcess(uint access, bool inherit, int pid);
    [DllImport("kernel32.dll", SetLastError=true)] internal static extern bool GetProcessTimes(SafeProcessHandle process, out FileTime created, out FileTime exited, out FileTime kernel, out FileTime user);
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)] internal static extern bool QueryFullProcessImageName(SafeProcessHandle process, uint flags, StringBuilder name, ref uint size);
    [DllImport("kernel32.dll")] internal static extern bool ProcessIdToSessionId(int pid, out uint session);
    [DllImport("kernel32.dll", SetLastError=true)] internal static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
    [DllImport("psapi.dll", SetLastError=true)] internal static extern bool GetProcessMemoryInfo(SafeProcessHandle process, ref MemoryCounters counters, uint size);
    [DllImport("kernel32.dll", SetLastError=true)] internal static extern nint CreateToolhelp32Snapshot(uint flags, uint pid);
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode)] internal static extern bool Process32FirstW(nint snap, ref ProcessEntry entry);
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode)] internal static extern bool Process32NextW(nint snap, ref ProcessEntry entry);
    [DllImport("kernel32.dll")] internal static extern bool CloseHandle(nint handle);
    [DllImport("kernel32.dll")] internal static extern uint WaitForSingleObject(SafeProcessHandle handle, uint milliseconds);
    [DllImport("advapi32.dll", SetLastError=true)] internal static extern bool OpenProcessToken(SafeProcessHandle handle, uint desiredAccess, out SafeAccessTokenHandle token);

    internal delegate bool EnumWindowProc(nint hwnd, nint lParam);
    [DllImport("user32.dll")] internal static extern bool EnumWindows(EnumWindowProc callback, nint param);
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(nint hwnd, out int pid);
    [DllImport("user32.dll")] internal static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(nint hwnd);
    [DllImport("user32.dll")] internal static extern bool ShowWindow(nint hwnd, int command);
    [DllImport("user32.dll")] internal static extern nint GetWindow(nint hwnd, uint command);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] internal static extern int GetClassName(nint hwnd, StringBuilder text, int count);
}
