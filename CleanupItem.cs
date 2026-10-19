namespace QuickClean;

/// <summary>一个可清理项目。Roots 即“白名单”——只允许在这些目录内枚举与删除。</summary>
internal sealed class CleanupItem
{
    public string Key { get; }
    public string Name { get; }
    public string Description { get; }
    public bool RequiresAdmin { get; }
    public bool SafeDefault { get; }
    public string[] Roots { get; }
    public Func<string, bool>? FileFilter { get; }
    public bool IsRecycleBin => Key == CleanupCatalog.RecycleBinKey;

    public long ScannedBytes { get; set; }
    public long ScannedFiles { get; set; }

    public CleanupItem(string key, string name, string description, bool requiresAdmin, bool safeDefault, string[] roots, Func<string, bool>? filter = null)
    {
        Key = key;
        Name = name;
        Description = description;
        RequiresAdmin = requiresAdmin;
        SafeDefault = safeDefault;
        Roots = roots;
        FileFilter = filter;
    }
}
