using System.Runtime.InteropServices;

namespace QuickClean;

/// <summary>系统内存占用读取。</summary>
internal static class MemoryInfo
{
    public readonly record struct Stats(ulong TotalBytes, ulong AvailBytes)
    {
        /// <summary>已用 = 总量 - 可用（可用含空闲 + 备用，与任务管理器口径一致）。</summary>
        public ulong UsedBytes => TotalBytes - AvailBytes;
    }

    public static Stats Get()
    {
        var status = new NativeMethods.MEMORYSTATUSEX
        {
            dwLength = (uint)Marshal.SizeOf<NativeMethods.MEMORYSTATUSEX>(),
        };
        if (!NativeMethods.GlobalMemoryStatusEx(ref status))
            throw new InvalidOperationException("读取内存信息失败。");

        return new Stats(status.ullTotalPhys, status.ullAvailPhys);
    }
}
