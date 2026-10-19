using System.Diagnostics;

namespace QuickClean;

/// <summary>内存优化：检测无响应进程、清空工作集、结束进程。</summary>
internal static class MemoryOptimizer
{
    public readonly record struct ReleaseResult(long AvailBeforeBytes, long AvailAfterBytes, int TrimmedProcesses)
    {
        /// <summary>释放的内存 = 整理后可用 - 整理前可用（可能为负，因系统会立即回收）。</summary>
        public long ReleasedBytes => AvailAfterBytes - AvailBeforeBytes;
    }

    /// <summary>枚举“无响应”(Not Responding) 的进程，自动排除关键进程。返回 (PID, 名称, 窗口标题)。</summary>
    public static List<(int Id, string Name, string Title)> GetUnresponsiveProcesses()
    {
        var result = new List<(int, string, string)>();
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                if (ProcessSafety.IsProtected(p)) continue;

                bool responding;
                try { responding = p.Responding; }
                catch { responding = true; } // 已退出/无窗口的进程视为“响应中”

                if (!responding)
                {
                    string title = "";
                    try { title = p.MainWindowTitle; } catch { }
                    result.Add((p.Id, p.ProcessName, title));
                }
            }
            catch { }
            finally { p.Dispose(); }
        }
        return result;
    }

    /// <summary>
    /// 一键释放：对非关键进程调用 SetProcessWorkingSetSize(-1,-1) 清空其工作集。
    /// 说明：这只是把进程页面挤到页面文件，Windows 可能立即回收，数值仅供参考，不强制结束任何进程。
    /// </summary>
    public static ReleaseResult ReleaseWorkingSets(IProgress<string>? progress = null, CancellationToken ct = default)
    {
        long before = (long)MemoryInfo.Get().AvailBytes;
        int trimmed = 0;

        foreach (var p in Process.GetProcesses())
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                if (ProcessSafety.IsProtected(p) || p.Id == Environment.ProcessId) continue;

                progress?.Report($"正在整理工作集：{p.ProcessName}");
                IntPtr h = NativeMethods.OpenProcess(
                    NativeMethods.PROCESS_SET_QUOTA | NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION,
                    false, p.Id);
                if (h == IntPtr.Zero) continue;

                try
                {
                    if (NativeMethods.SetProcessWorkingSetSize(h, new IntPtr(-1), new IntPtr(-1)))
                        trimmed++;
                }
                finally { NativeMethods.CloseHandle(h); }
            }
            catch { }
            finally { p.Dispose(); }
        }

        long after = (long)MemoryInfo.Get().AvailBytes;
        return new ReleaseResult(before, after, trimmed);
    }

    /// <summary>结束指定 PID 的进程。返回是否成功。</summary>
    public static bool KillProcess(int id)
    {
        try
        {
            using var p = Process.GetProcessById(id);
            p.Kill();
            p.WaitForExit(3000);
            return true;
        }
        catch { return false; }
    }
}
