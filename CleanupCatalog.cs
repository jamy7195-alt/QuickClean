namespace QuickClean;

/// <summary>定义所有可清理项目及其安全根目录（白名单）。</summary>
internal static class CleanupCatalog
{
    public const string RecycleBinKey = "RecycleBin";

    private static string LocalAppData =>
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    public static List<CleanupItem> Create()
    {
        var la = LocalAppData;
        var list = new List<CleanupItem>();

        // 1. 用户临时文件（安全，默认勾选）
        var userTemp = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        list.Add(new CleanupItem("UserTemp", "用户临时文件", "%TEMP% 中的临时文件", false, true, new[] { userTemp }));

        // 2. 系统临时文件（需管理员）
        list.Add(new CleanupItem("SystemTemp", "系统临时文件", "C:\\Windows\\Temp（需管理员）", true, false, new[] { @"C:\Windows\Temp" }));

        // 3. Chrome 缓存（安全，默认勾选）
        var chrome = Path.Combine(la, "Google", "Chrome", "User Data");
        list.Add(new CleanupItem("ChromeCache", "Chrome 浏览器缓存", "缓存 / 代码缓存 / GPU 缓存", false, true, new[]
        {
            Path.Combine(chrome, "Default", "Cache"),
            Path.Combine(chrome, "Default", "Cache_Data"),
            Path.Combine(chrome, "Default", "Code Cache"),
            Path.Combine(chrome, "Default", "GPUCache"),
            Path.Combine(chrome, "ShaderCache"),
            Path.Combine(chrome, "Default", "Service Worker", "CacheStorage"),
        }));

        // 4. Edge 缓存（安全，默认勾选）
        var edge = Path.Combine(la, "Microsoft", "Edge", "User Data");
        list.Add(new CleanupItem("EdgeCache", "Edge 浏览器缓存", "缓存 / 代码缓存 / GPU 缓存", false, true, new[]
        {
            Path.Combine(edge, "Default", "Cache"),
            Path.Combine(edge, "Default", "Cache_Data"),
            Path.Combine(edge, "Default", "Code Cache"),
            Path.Combine(edge, "Default", "GPUCache"),
            Path.Combine(edge, "ShaderCache"),
            Path.Combine(edge, "Default", "Service Worker", "CacheStorage"),
        }));

        // 5. 回收站（不可恢复，默认不勾选）
        list.Add(new CleanupItem(RecycleBinKey, "回收站", "清空所有驱动器的回收站（不可恢复）", false, false, Array.Empty<string>()));

        // 6. Windows 更新缓存（需管理员）
        list.Add(new CleanupItem("WinUpdateCache", "Windows 更新缓存", "已下载的更新安装包（需管理员）", true, false, new[]
        {
            @"C:\Windows\SoftwareDistribution\Download",
        }));

        // 7. 缩略图缓存（安全，默认勾选；仅匹配 thumbcache_* / iconcache_*）
        var explorerDir = Path.Combine(la, "Microsoft", "Windows", "Explorer");
        list.Add(new CleanupItem("ThumbCache", "缩略图缓存", "thumbcache_* / iconcache_*", false, true, new[] { explorerDir },
            filter: f =>
            {
                var n = Path.GetFileName(f);
                return n.StartsWith("thumbcache_", StringComparison.OrdinalIgnoreCase)
                    || n.StartsWith("iconcache_", StringComparison.OrdinalIgnoreCase);
            }));

        // 8. 崩溃转储与错误报告（可选，默认不勾选）
        list.Add(new CleanupItem("CrashDumps", "崩溃转储与错误报告", "CrashDumps / WER 报告", false, false, new[]
        {
            Path.Combine(la, "CrashDumps"),
            Path.Combine(la, "Microsoft", "Windows", "WER", "ReportQueue"),
            Path.Combine(la, "Microsoft", "Windows", "WER", "ReportArchive"),
            Path.Combine(la, "Microsoft", "Windows", "WER", "Temp"),
        }));

        return list;
    }
}
