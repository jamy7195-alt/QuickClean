namespace QuickClean;

/// <summary>扫描与清理引擎。所有删除均限定在各清理项声明的白名单根目录内，并叠加黑名单兜底。</summary>
internal static class CleanupEngine
{
    public sealed record ScanResult(CleanupItem Item, long Bytes, long Files);

    public sealed record CleanItemResult(CleanupItem Item, long DeletedFiles, long DeletedBytes, long SkippedFiles, long SkippedBytes, List<string> Warnings);

    // ==================== 扫描 ====================

    public static Task<ScanResult> ScanAsync(CleanupItem item, CancellationToken ct)
        => Task.Run(() => Scan(item, ct), ct);

    public static ScanResult Scan(CleanupItem item, CancellationToken ct)
    {
        if (item.IsRecycleBin)
        {
            var (size, count) = RecycleBin.Query();
            item.ScannedBytes = size;
            item.ScannedFiles = count;
            return new ScanResult(item, size, count);
        }

        long bytes = 0, files = 0;
        foreach (var root in item.Roots)
        {
            if (!Directory.Exists(root)) continue;
            foreach (var f in EnumerateFiles(root, item.FileFilter))
            {
                ct.ThrowIfCancellationRequested();
                try { bytes += new FileInfo(f).Length; files++; }
                catch { /* 文件被并发删除，忽略 */ }
            }
        }
        item.ScannedBytes = bytes;
        item.ScannedFiles = files;
        return new ScanResult(item, bytes, files);
    }

    // ==================== 清理 ====================

    public static Task<CleanItemResult> CleanAsync(CleanupItem item, bool toRecycleBin, IProgress<string>? progress, CancellationToken ct)
        => Task.Run(() => Clean(item, toRecycleBin, progress, ct), ct);

    public static CleanItemResult Clean(CleanupItem item, bool toRecycleBin, IProgress<string>? progress, CancellationToken ct)
    {
        if (item.IsRecycleBin) return CleanRecycleBin(item, progress);

        long deletedFiles = 0, deletedBytes = 0, skippedFiles = 0, skippedBytes = 0;
        var warnings = new List<string>();

        foreach (var root in item.Roots)
        {
            if (!Directory.Exists(root)) continue;
            foreach (var f in EnumerateFiles(root, item.FileFilter))
            {
                ct.ThrowIfCancellationRequested();
                progress?.Report($"{item.Name}：{Path.GetFileName(f)}");

                // 黑名单兜底：正常情况下不会触发，因根目录已受限
                if (ProtectedPaths.IsProtected(f))
                {
                    skippedFiles++;
                    try { skippedBytes += new FileInfo(f).Length; } catch { }
                    if (warnings.Count < 50) warnings.Add($"受保护路径，跳过：{f}");
                    continue;
                }

                long len = 0;
                try { len = new FileInfo(f).Length; } catch { }

                try
                {
                    if (toRecycleBin) RecycleBin.DeleteFile(f);
                    else File.Delete(f);
                    deletedFiles++;
                    deletedBytes += len;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // 文件被占用 / 无权删除：跳过并计数，不中断
                    skippedFiles++;
                    skippedBytes += len;
                }
            }

            TryDeleteEmptyDirectories(root);
        }

        return new CleanItemResult(item, deletedFiles, deletedBytes, skippedFiles, skippedBytes, warnings);
    }

    private static CleanItemResult CleanRecycleBin(CleanupItem item, IProgress<string>? progress)
    {
        progress?.Report("正在清空回收站...");
        var (size, count) = RecycleBin.Query();
        bool ok = RecycleBin.Empty();
        if (!ok)
            return new CleanItemResult(item, 0, 0, 0, size, new List<string> { "清空回收站失败。" });
        return new CleanItemResult(item, count, size, 0, 0, new List<string>());
    }

    // ==================== 文件枚举（逐目录 try/catch，跳过 junction/符号链接） ====================

    private static IEnumerable<string> EnumerateFiles(string root, Func<string, bool>? filter)
    {
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            var dir = pending.Pop();

            string[] files;
            try { files = Directory.GetFiles(dir); }
            catch { files = Array.Empty<string>(); }

            foreach (var f in files)
            {
                if (filter is null || filter(f)) yield return f;
            }

            string[] subdirs;
            try { subdirs = Directory.GetDirectories(dir); }
            catch { subdirs = Array.Empty<string>(); }

            foreach (var d in subdirs)
            {
                try
                {
                    if ((File.GetAttributes(d) & FileAttributes.ReparsePoint) != 0) continue; // 跳过 junction，防逃逸/循环
                    pending.Push(d);
                }
                catch { }
            }
        }
    }

    private static void TryDeleteEmptyDirectories(string root)
    {
        try
        {
            var opts = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.ReparsePoint,
            };
            foreach (var dir in Directory.EnumerateDirectories(root, "*", opts).Reverse())
            {
                try
                {
                    if (!Directory.EnumerateFileSystemEntries(dir).Any())
                        Directory.Delete(dir);
                }
                catch { }
            }
        }
        catch { }
    }
}
