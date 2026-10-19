namespace QuickClean;

/// <summary>
/// 文件系统“黑名单”：这些目录下的任何文件一律禁止删除。
/// 它是安全机制的第二道防线——第一道是每个清理项只允许在自身声明的
/// 安全根目录（白名单）内枚举；本类用于兜底，防止任何路径逃逸。
/// </summary>
internal static class ProtectedPaths
{
    private static readonly string[] Roots = Build();

    private static string[] Build()
    {
        var list = new List<string>
        {
            // —— 用户个人数据（永不触碰）——
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
            Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), // Roaming 配置

            // —— 系统关键目录 ——
            @"C:\Windows\System32",
            @"C:\Windows\SysWOW64",
            @"C:\Windows\WinSxS",
            @"C:\Windows\Boot",
            @"C:\Windows\inf",
            @"C:\Windows\System",
            @"C:\Windows\assembly",
            @"C:\Windows\Microsoft.NET",
            @"C:\Windows\servicing",
            @"C:\Windows\Installer",
            @"C:\Windows\SystemResources",
            @"C:\Windows\L2Schemas",
            @"C:\Windows\LiveKernelReports",

            // —— 程序目录 / 系统盘关键区 ——
            @"C:\Program Files",
            @"C:\Program Files (x86)",
            @"C:\ProgramData",
            @"C:\",
            @"C:\$Recycle.Bin",
            @"C:\System Volume Information",
            @"C:\Recovery",
            @"C:\PerfLogs",
        };

        // 用户主目录下额外的个人数据目录
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(profile))
        {
            list.Add(Path.Combine(profile, "Downloads"));
            list.Add(Path.Combine(profile, "OneDrive"));
            list.Add(Path.Combine(profile, "Favorites"));
            list.Add(Path.Combine(profile, "Contacts"));
            list.Add(Path.Combine(profile, "Links"));
        }

        return list
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p =>
            {
                try { return Path.GetFullPath(p); }
                catch { return p; }
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>判断给定路径是否位于受保护目录之下（含该目录本身）。</summary>
    public static bool IsProtected(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return true;

        string full;
        try { full = Path.GetFullPath(path); }
        catch { return true; } // 无法解析的路径一律视为受保护

        foreach (var root in Roots)
        {
            if (IsUnder(full, root)) return true;
        }
        return false;
    }

    private static bool IsUnder(string fullPath, string root)
    {
        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return false;
        if (fullPath.Length == root.Length) return true;
        char c = fullPath[root.Length];
        return c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar;
    }
}
