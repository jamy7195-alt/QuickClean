using System.Diagnostics;

namespace QuickClean;

/// <summary>
/// 进程“黑名单”：内存整理与“结束无响应进程”功能不得触碰这些关键进程。
/// </summary>
internal static class ProcessSafety
{
    private static readonly HashSet<string> Protected = new(StringComparer.OrdinalIgnoreCase)
    {
        // 内核/系统
        "system", "idle", "registry", "memory compression", "secure system",
        "csrss", "wininit", "winlogon", "lsass", "services", "smss",
        // 桌面与外壳
        "dwm", "explorer", "fontdrvhost", "sihost", "taskhostw", "conhost",
        // 服务与安全
        "svchost", "audiodg", "spoolsv", "msmpeng", "securityhealthservice",
        "smartscreen", "searchindexer", "winmgmt", "wudfhost",
        // 本程序自身
        "quickclean",
    };

    public static bool IsProtected(string processName) => Protected.Contains(processName);

    public static bool IsProtected(Process p) => IsProtected(p.ProcessName);
}
