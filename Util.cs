namespace QuickClean;

/// <summary>通用工具方法。</summary>
internal static class Util
{
    private static readonly string[] Units = { "B", "KB", "MB", "GB", "TB" };

    /// <summary>把字节数格式化为人类可读字符串（如 123.45 MB）。</summary>
    public static string FormatBytes(long bytes)
    {
        double value = bytes;
        int unit = 0;
        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        return unit == 0 ? $"{bytes} B" : $"{value:N2} {Units[unit]}";
    }

    /// <summary>ulong 重载（用于内存容量）。</summary>
    public static string FormatBytes(ulong bytes) =>
        FormatBytes(bytes >= (ulong)long.MaxValue ? long.MaxValue : (long)bytes);
}
