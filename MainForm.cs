using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace QuickClean;

internal sealed class MainForm : Form
{
    private readonly List<CleanupItem> _items;
    private readonly System.Windows.Forms.Timer _memTimer;
    private readonly StringBuilder _reportLog = new();
    private CancellationTokenSource? _cts;
    private bool _busy;

    private Label _lblTitle = null!, _lblAdmin = null!, _lblMemTotal = null!, _lblMemUsed = null!;
    private Label _lblMemAvail = null!, _lblMemDelta = null!, _lblStatus = null!;
    private Button _btnElevate = null!, _btnRefreshMem = null!, _btnReleaseMem = null!, _btnKill = null!;
    private Button _btnSelectAll = null!, _btnSelectNone = null!, _btnScan = null!, _btnClean = null!;
    private Button _btnSaveReport = null!, _btnCopyReport = null!;
    private RadioButton _rbQuick = null!, _rbCustom = null!;
    private ListView _list = null!;
    private CheckBox _chkRecycle = null!;
    private ProgressBar _progress = null!;
    private TextBox _report = null!;

    public MainForm()
    {
        _items = CleanupCatalog.Create();
        _memTimer = new System.Windows.Forms.Timer { Interval = 2000 };
        _memTimer.Tick += (_, _) => { if (!_busy) RefreshMemory(); };

        InitializeComponent();
        ConfigureList();
        UpdateAdminBadge();
        RefreshMemory();
        AppendReport($"[{Now()}] QuickClean 已启动。{(IsAdmin() ? "以管理员权限运行。" : "普通权限，部分项目需管理员。")}");
        _memTimer.Start();
        _ = AutoScanAsync();
    }

    // ==================== 界面构建 ====================

    private void InitializeComponent()
    {
        Text = "QuickClean · Windows 快捷安全清理工具";
        ClientSize = new Size(920, 750);
        MinimumSize = new Size(780, 640);
        StartPosition = FormStartPosition.CenterScreen;

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 7, Padding = new Padding(12) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 180));
        Controls.Add(root);

        // Row 0 —— 标题
        var titleFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        _lblTitle = new Label { Text = "QuickClean 快捷安全清理工具", AutoSize = true, Margin = new Padding(0, 10, 0, 0), Font = new Font("Microsoft YaHei UI", 14f, FontStyle.Bold) };
        _lblAdmin = new Label { AutoSize = true, Margin = new Padding(18, 14, 0, 0) };
        _btnElevate = new Button { Text = "以管理员身份运行", AutoSize = true, Margin = new Padding(8, 9, 0, 0) };
        _btnElevate.Click += (_, _) => Elevate();
        titleFlow.Controls.AddRange(new Control[] { _lblTitle, _lblAdmin, _btnElevate });
        root.Controls.Add(titleFlow, 0, 0);

        // Row 1 —— 内存
        var memGroup = new GroupBox { Text = "内存占用", Dock = DockStyle.Fill, Padding = new Padding(10, 4, 10, 6) };
        var memFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true };
        _lblMemTotal = MakeLabel("");
        _lblMemUsed = MakeLabel("");
        _lblMemAvail = MakeLabel("");
        _btnRefreshMem = MakeButton("刷新");
        _btnReleaseMem = MakeButton("一键释放内存");
        _btnKill = MakeButton("结束无响应进程");
        _lblMemDelta = new Label { AutoSize = true, ForeColor = Color.SeaGreen, Margin = new Padding(6, 5, 6, 5) };
        _btnRefreshMem.Click += (_, _) => RefreshMemory();
        _btnReleaseMem.Click += async (_, _) => await ReleaseMemoryAsync();
        _btnKill.Click += async (_, _) => await ShowUnresponsiveAsync();
        memFlow.Controls.AddRange(new Control[] { _lblMemTotal, _lblMemUsed, _lblMemAvail, _btnRefreshMem, _btnReleaseMem, _btnKill, _lblMemDelta });
        memGroup.Controls.Add(memFlow);
        root.Controls.Add(memGroup, 0, 1);

        // Row 2 —— 模式
        var modeGroup = new GroupBox { Text = "清理模式", Dock = DockStyle.Fill, Padding = new Padding(10, 2, 10, 2) };
        var modeFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        _rbQuick = new RadioButton { Text = "快速清理（一键清理安全项）", AutoSize = true, Margin = new Padding(4, 6, 18, 4), Checked = true };
        _rbCustom = new RadioButton { Text = "自定义清理（手动勾选）", AutoSize = true, Margin = new Padding(4, 6, 18, 4) };
        _rbQuick.CheckedChanged += (_, _) => ApplyMode();
        _rbCustom.CheckedChanged += (_, _) => ApplyMode();
        modeFlow.Controls.AddRange(new Control[] { _rbQuick, _rbCustom });
        modeGroup.Controls.Add(modeFlow);
        root.Controls.Add(modeGroup, 0, 2);

        // Row 3 —— 清理项列表
        _list = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            CheckBoxes = true,
            FullRowSelect = true,
            GridLines = true,
            MultiSelect = false,
            HideSelection = false,
        };
        _list.Columns.Add("清理项目", 180);
        _list.Columns.Add("可释放大小", 110);
        _list.Columns.Add("说明", 360);
        _list.Columns.Add("权限", 100);
        root.Controls.Add(_list, 0, 3);

        // Row 4 —— 操作
        var actionFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        _btnSelectAll = MakeButton("全选");
        _btnSelectNone = MakeButton("全不选");
        _btnScan = new Button { Text = "开始扫描", AutoSize = true, Margin = new Padding(6, 6, 6, 6), BackColor = Color.FromArgb(225, 240, 255) };
        _btnClean = new Button { Text = "开始清理", AutoSize = true, Margin = new Padding(6, 6, 6, 6), BackColor = Color.FromArgb(255, 235, 235) };
        _chkRecycle = new CheckBox { Text = "删除到回收站（可恢复）", AutoSize = true, Margin = new Padding(18, 8, 0, 4), Checked = true };
        _btnSelectAll.Click += (_, _) => SetAllChecks(true);
        _btnSelectNone.Click += (_, _) => SetAllChecks(false);
        _btnScan.Click += async (_, _) => await ScanAsync();
        _btnClean.Click += async (_, _) => await CleanAsync();
        actionFlow.Controls.AddRange(new Control[] { _btnSelectAll, _btnSelectNone, _btnScan, _btnClean, _chkRecycle });
        root.Controls.Add(actionFlow, 0, 4);

        // Row 5 —— 进度
        var progTable = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        progTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        progTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));
        _progress = new ProgressBar { Dock = DockStyle.Fill, Style = ProgressBarStyle.Continuous };
        _lblStatus = new Label { Text = "就绪", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
        progTable.Controls.Add(_progress, 0, 0);
        progTable.Controls.Add(_lblStatus, 1, 0);
        root.Controls.Add(progTable, 0, 5);

        // Row 6 —— 报告
        var reportGroup = new GroupBox { Text = "结果报告", Dock = DockStyle.Fill, Padding = new Padding(8, 4, 8, 8) };
        var reportTable = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        reportTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        reportTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        _report = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            BackColor = Color.White,
            Font = new Font("Consolas", 9f),
        };
        var reportBtnFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        _btnSaveReport = MakeButton("保存报告");
        _btnCopyReport = MakeButton("复制报告");
        _btnSaveReport.Click += (_, _) => SaveReport();
        _btnCopyReport.Click += (_, _) => CopyReport();
        reportBtnFlow.Controls.AddRange(new Control[] { _btnSaveReport, _btnCopyReport });
        reportTable.Controls.Add(_report, 0, 0);
        reportTable.Controls.Add(reportBtnFlow, 0, 1);
        reportGroup.Controls.Add(reportTable);
        root.Controls.Add(reportGroup, 0, 6);
    }

    private static Label MakeLabel(string text) => new() { Text = text, AutoSize = true, Margin = new Padding(6, 5, 6, 5) };

    private static Button MakeButton(string text) => new() { Text = text, AutoSize = true, Margin = new Padding(6, 6, 6, 6) };

    // ==================== 清理项列表 ====================

    private void ConfigureList()
    {
        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var item in _items)
        {
            var lvi = new ListViewItem(item.Name) { Tag = item, Checked = item.SafeDefault };
            lvi.SubItems.Add("未扫描");
            lvi.SubItems.Add(item.Description);
            lvi.SubItems.Add(item.RequiresAdmin ? "需管理员" : item.IsRecycleBin ? "不可恢复" : "安全");
            _list.Items.Add(lvi);
        }
        _list.EndUpdate();
        ApplyMode();
    }

    private void ApplyMode()
    {
        bool quick = _rbQuick.Checked;
        foreach (ListViewItem lvi in _list.Items)
        {
            var item = (CleanupItem)lvi.Tag!;
            if (quick) lvi.Checked = item.SafeDefault;
        }
        _btnSelectAll.Enabled = !quick;
        _btnSelectNone.Enabled = !quick;
    }

    private void SetAllChecks(bool check)
    {
        if (_rbQuick.Checked) return;
        foreach (ListViewItem lvi in _list.Items) lvi.Checked = check;
    }

    private List<CleanupItem> GetSelectedItems()
    {
        var sel = new List<CleanupItem>();
        foreach (ListViewItem lvi in _list.Items)
            if (lvi.Checked) sel.Add((CleanupItem)lvi.Tag!);
        return sel;
    }

    private void UpdateItemSize(CleanupItem item, long bytes)
    {
        foreach (ListViewItem lvi in _list.Items)
        {
            if (ReferenceEquals(lvi.Tag, item))
            {
                lvi.SubItems[1].Text = Util.FormatBytes(bytes);
                break;
            }
        }
    }

    // ==================== 扫描 / 清理 ====================

    private async Task AutoScanAsync()
    {
        try
        {
            foreach (var item in _items.Where(i => !i.RequiresAdmin))
            {
                if (IsDisposed) return;
                var res = await CleanupEngine.ScanAsync(item, CancellationToken.None);
                if (IsDisposed) return;
                UpdateItemSize(res.Item, res.Bytes);
            }
            if (!IsDisposed) AppendReport($"[{Now()}] 初始扫描完成。");
        }
        catch { /* 启动扫描失败不致命 */ }
    }

    private async Task ScanAsync()
    {
        if (_busy) return;
        var items = GetSelectedItems();
        if (items.Count == 0) { MessageBox.Show(this, "请先勾选要扫描的项目。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }

        SetBusy(true, "正在扫描...");
        _progress.Style = ProgressBarStyle.Marquee;
        _progress.MarqueeAnimationSpeed = 30;
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        try
        {
            var tasks = items.Select(it => CleanupEngine.ScanAsync(it, ct)).ToArray();
            foreach (var t in tasks)
            {
                var res = await t;
                UpdateItemSize(res.Item, res.Bytes);
                _lblStatus.Text = $"已扫描：{res.Item.Name}";
            }
            AppendReport($"[{Now()}] 扫描完成。");
        }
        catch (OperationCanceledException) { AppendReport($"[{Now()}] 扫描已取消。"); }
        catch (Exception ex) { AppendReport($"[{Now()}] 扫描出错：{ex.Message}"); }
        finally
        {
            _progress.MarqueeAnimationSpeed = 0;
            _progress.Style = ProgressBarStyle.Continuous;
            _progress.Value = 0;
            SetBusy(false, "就绪");
            _cts = null;
        }
    }

    private async Task CleanAsync()
    {
        if (_busy) return;
        var items = GetSelectedItems();
        if (items.Count == 0) { MessageBox.Show(this, "请先勾选要清理的项目。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }

        // 1) 管理员权限检查
        if (!IsAdmin())
        {
            var needAdmin = items.Where(i => i.RequiresAdmin).ToList();
            if (needAdmin.Count > 0)
            {
                var names = string.Join("、", needAdmin.Select(i => i.Name));
                var r = MessageBox.Show(this,
                    $"以下项目需要管理员权限：\n{names}\n\n是否以管理员身份重启本程序？\n（选“否”将跳过这些项目，继续清理其余项）",
                    "需要管理员权限", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (r == DialogResult.Yes) { Elevate(); return; }
                if (r == DialogResult.Cancel) return;
                items = items.Where(i => !i.RequiresAdmin).ToList();
                if (items.Count == 0) { MessageBox.Show(this, "已取消：所选项目均需管理员权限。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            }
        }

        // 2) 二次确认
        bool toRecycle = _chkRecycle.Checked;
        bool hasRecycle = items.Any(i => i.IsRecycleBin);
        var lines = items.Select(i => $"  · {i.Name}{(i.RequiresAdmin ? "（管理员）" : "")}");
        string warn = "";
        if (hasRecycle) warn += "\n\n⚠ 清空回收站不可恢复！";
        if (hasRecycle && toRecycle) warn += "\n⚠ 已同时勾选“移入回收站”，文件将先入回收站再被清空，最终永久删除。";
        var confirm = MessageBox.Show(this,
            $"即将清理 {items.Count} 项：\n{string.Join("\n", lines)}\n\n删除方式：{(toRecycle ? "移入回收站（可恢复）" : "永久删除（不可恢复）")}{warn}\n\n是否继续？",
            "确认清理", MessageBoxButtons.YesNo, hasRecycle ? MessageBoxIcon.Warning : MessageBoxIcon.Question);
        if (confirm != DialogResult.Yes) return;

        // 3) 执行
        SetBusy(true, "正在清理...");
        _progress.Style = ProgressBarStyle.Marquee;
        _progress.MarqueeAnimationSpeed = 30;
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        var progress = new Progress<string>(m => _lblStatus.Text = m);

        long totalBytes = 0, totalFiles = 0, totalSkipped = 0;
        try
        {
            foreach (var item in items)
            {
                ct.ThrowIfCancellationRequested();
                var res = await CleanupEngine.CleanAsync(item, toRecycle, progress, ct);
                totalBytes += res.DeletedBytes;
                totalFiles += res.DeletedFiles;
                totalSkipped += res.SkippedFiles;
                AppendReport($"[{Now()}] {item.Name}：删除 {res.DeletedFiles} 个文件 / {Util.FormatBytes(res.DeletedBytes)}，跳过 {res.SkippedFiles} 个（占用或受保护）。");
                foreach (var w in res.Warnings.Take(10)) AppendReport("    ⚠ " + w);
                if (res.Warnings.Count > 10) AppendReport($"    … 另有 {res.Warnings.Count - 10} 条警告。");
                UpdateItemSize(item, 0);
            }
            AppendReport($"[{Now()}] 清理完成：共删除 {totalFiles} 个文件，释放 {Util.FormatBytes(totalBytes)}，跳过 {totalSkipped} 个文件。");
            MessageBox.Show(this,
                $"清理完成！\n\n删除文件：{totalFiles} 个\n释放空间：{Util.FormatBytes(totalBytes)}\n跳过（占用/受保护）：{totalSkipped} 个\n\n{(toRecycle ? "文件已移入回收站，可在回收站恢复。" : "文件已永久删除。")}",
                "清理完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (OperationCanceledException) { AppendReport($"[{Now()}] 清理已取消。"); }
        catch (Exception ex) { AppendReport($"[{Now()}] 清理出错：{ex.Message}"); }
        finally
        {
            _progress.MarqueeAnimationSpeed = 0;
            _progress.Style = ProgressBarStyle.Continuous;
            _progress.Value = 0;
            SetBusy(false, "就绪");
            _cts = null;
        }
    }

    // ==================== 内存 ====================

    private void RefreshMemory()
    {
        try
        {
            var s = MemoryInfo.Get();
            _lblMemTotal.Text = $"内存总量：{Util.FormatBytes(s.TotalBytes)}";
            _lblMemUsed.Text = $"已用：{Util.FormatBytes(s.UsedBytes)}";
            _lblMemAvail.Text = $"可用：{Util.FormatBytes(s.AvailBytes)}";
        }
        catch (Exception ex)
        {
            _lblMemTotal.Text = "内存读取失败：" + ex.Message;
        }
    }

    private async Task ReleaseMemoryAsync()
    {
        if (_busy) return;
        SetBusy(true, "正在整理内存...");
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        try
        {
            var progress = new Progress<string>(m => _lblStatus.Text = m);
            var res = await Task.Run(() => MemoryOptimizer.ReleaseWorkingSets(progress, ct), ct);
            long delta = res.ReleasedBytes;
            _lblMemDelta.Text = $"上次释放：{(delta >= 0 ? "+" : "")}{Util.FormatBytes(delta)}（整理 {res.TrimmedProcesses} 个进程）";
            _lblMemDelta.ForeColor = delta >= 0 ? Color.SeaGreen : Color.DarkOrange;
            AppendReport($"[{Now()}] 内存整理：整理 {res.TrimmedProcesses} 个进程的工作集，可用内存变化 {Util.FormatBytes(delta)}（系统可能立即回收，仅供参考）。");
            RefreshMemory();
        }
        catch (OperationCanceledException) { AppendReport($"[{Now()}] 内存整理已取消。"); }
        catch (Exception ex) { AppendReport($"[{Now()}] 内存整理出错：{ex.Message}"); }
        finally { SetBusy(false, "就绪"); _cts = null; }
    }

    private async Task ShowUnresponsiveAsync()
    {
        if (_busy) return;
        SetBusy(true, "正在检测无响应进程...");
        try
        {
            var procs = await Task.Run(MemoryOptimizer.GetUnresponsiveProcesses);
            SetBusy(false, "就绪");
            if (procs.Count == 0)
            {
                MessageBox.Show(this, "未发现无响应的进程。", "无响应进程", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using var dlg = new UnresponsiveForm(procs);
            if (dlg.ShowDialog(this) != DialogResult.OK || dlg.SelectedIds.Count == 0) return;

            int killed = 0;
            foreach (var id in dlg.SelectedIds)
            {
                if (MemoryOptimizer.KillProcess(id)) killed++;
                else AppendReport($"[{Now()}] 结束进程 {id} 失败。");
            }
            AppendReport($"[{Now()}] 已结束 {killed} 个无响应进程。");
            RefreshMemory();
        }
        catch (Exception ex)
        {
            SetBusy(false, "就绪");
            AppendReport($"[{Now()}] 检测无响应进程出错：{ex.Message}");
        }
    }

    // ==================== 权限 / 其它 ====================

    private static bool IsAdmin()
    {
        using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        var principal = new System.Security.Principal.WindowsPrincipal(identity);
        return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
    }

    private void UpdateAdminBadge()
    {
        if (IsAdmin())
        {
            _lblAdmin.Text = "● 管理员权限";
            _lblAdmin.ForeColor = Color.SeaGreen;
            _btnElevate.Visible = false;
        }
        else
        {
            _lblAdmin.Text = "○ 普通权限（部分项目需管理员）";
            _lblAdmin.ForeColor = Color.DarkOrange;
            _btnElevate.Visible = true;
        }
    }

    private void Elevate()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = Environment.ProcessPath!,
                UseShellExecute = true,
                Verb = "runas",
            };
            Process.Start(psi);
        }
        catch (Win32Exception)
        {
            MessageBox.Show(this, "已取消提权。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        Application.Exit();
    }

    private void SetBusy(bool busy, string status)
    {
        _busy = busy;
        _lblStatus.Text = status;
        UseWaitCursor = busy;
        _btnScan.Enabled = !busy;
        _btnClean.Enabled = !busy;
        _btnReleaseMem.Enabled = !busy;
        _btnKill.Enabled = !busy;
        _btnElevate.Enabled = !busy;
        _btnSelectAll.Enabled = !busy && !_rbQuick.Checked;
        _btnSelectNone.Enabled = !busy && !_rbQuick.Checked;
    }

    private void AppendReport(string line)
    {
        _reportLog.AppendLine(line);
        _report.Text = _reportLog.ToString();
        _report.SelectionStart = _report.TextLength;
        _report.ScrollToCaret();
    }

    private static string Now() => DateTime.Now.ToString("HH:mm:ss");

    private void SaveReport()
    {
        if (_reportLog.Length == 0) { MessageBox.Show(this, "暂无可保存的报告。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            $"QuickClean报告_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
        try
        {
            File.WriteAllText(path, _reportLog.ToString(), new UTF8Encoding(true));
            MessageBox.Show(this, $"报告已保存到：\n{path}", "保存成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { MessageBox.Show(this, "保存失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void CopyReport()
    {
        if (_reportLog.Length == 0) return;
        Clipboard.SetText(_reportLog.ToString());
        MessageBox.Show(this, "报告已复制到剪贴板。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _memTimer.Stop();
        _cts?.Cancel();
        base.OnFormClosing(e);
    }
}
