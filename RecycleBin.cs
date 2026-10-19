using System.Runtime.InteropServices;

namespace QuickClean;

/// <summary>回收站操作：查询容量、清空、把单个文件移入回收站。</summary>
internal static class RecycleBin
{
    private const uint SHERB_NOCONFIRMATION = 0x00000001;
    private const uint SHERB_NOPROGRESSUI = 0x00000002;
    private const uint SHERB_NOSOUND = 0x00000004;

    private const uint FO_DELETE = 0x0003;
    private const ushort FOF_SILENT = 0x0004;
    private const ushort FOF_NOCONFIRMATION = 0x0010;
    private const ushort FOF_ALLOWUNDO = 0x0040;
    private const ushort FOF_NOERRORUI = 0x0400;

    /// <summary>查询所有驱动器回收站的总大小与对象数。</summary>
    public static (long size, long count) Query()
    {
        var info = new NativeMethods.SHQUERYRBINFO
        {
            cbSize = Marshal.SizeOf<NativeMethods.SHQUERYRBINFO>(),
        };
        int hr = NativeMethods.SHQueryRecycleBin(null, ref info);
        return hr == 0 ? (info.i64Size, info.i64NumItems) : (0, 0);
    }

    /// <summary>清空所有驱动器的回收站。返回是否成功。</summary>
    public static bool Empty()
    {
        int hr = NativeMethods.SHEmptyRecycleBin(IntPtr.Zero, null, SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND);
        return hr == 0;
    }

    /// <summary>把单个文件移入回收站（可恢复）。返回是否成功。</summary>
    public static bool DeleteFile(string path)
    {
        var op = new NativeMethods.SHFILEOPSTRUCT
        {
            hwnd = IntPtr.Zero,
            wFunc = FO_DELETE,
            pFrom = path + "\0",           // 需要双 \0 结尾
            pTo = null,
            fFlags = FOF_SILENT | FOF_NOCONFIRMATION | FOF_ALLOWUNDO | FOF_NOERRORUI,
            fAnyOperationsAborted = false,
            hNameMappings = IntPtr.Zero,
            lpszProgressTitle = null,
        };
        int hr = NativeMethods.SHFileOperationW(ref op);
        return hr == 0 && !op.fAnyOperationsAborted;
    }
}
