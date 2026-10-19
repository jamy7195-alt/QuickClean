using System.Drawing;
using System.Windows.Forms;

namespace QuickClean;

/// <summary>“结束无响应进程”对话框：默认全不选，用户显式勾选后才可结束。</summary>
internal sealed class UnresponsiveForm : Form
{
    private readonly ListView _list;

    public List<int> SelectedIds { get; } = new();

    public UnresponsiveForm(IEnumerable<(int Id, string Name, string Title)> procs)
    {
        Text = "结束无响应进程";
        ClientSize = new Size(540, 380);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(12),
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));

        var hint = new Label
        {
            Text = "请勾选要结束的进程（默认全不选）。系统关键进程已自动排除。",
            Dock = DockStyle.Fill,
            ForeColor = Color.DarkOrange,
        };

        _list = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            CheckBoxes = true,
            FullRowSelect = true,
            GridLines = true,
            MultiSelect = false,
        };
        _list.Columns.Add("进程名", 150);
        _list.Columns.Add("PID", 70);
        _list.Columns.Add("窗口标题", 260);

        foreach (var (id, name, title) in procs)
        {
            var lvi = new ListViewItem(name) { Tag = id, Checked = false };
            lvi.SubItems.Add(id.ToString());
            lvi.SubItems.Add(string.IsNullOrEmpty(title) ? "（无标题）" : title);
            _list.Items.Add(lvi);
        }

        var btnFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
        };
        var btnCancel = new Button { Text = "取消", AutoSize = true, Margin = new Padding(6, 8, 0, 8), DialogResult = DialogResult.Cancel };
        var btnOk = new Button
        {
            Text = "结束所选进程",
            AutoSize = true,
            Margin = new Padding(0, 8, 10, 8),
            BackColor = Color.IndianRed,
            ForeColor = Color.White,
        };
        btnOk.Click += (_, _) =>
        {
            SelectedIds.Clear();
            foreach (ListViewItem lvi in _list.Items)
                if (lvi.Checked) SelectedIds.Add((int)lvi.Tag!);

            if (SelectedIds.Count == 0)
            {
                MessageBox.Show(this, "请至少勾选一个进程。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            DialogResult = DialogResult.OK;
            Close();
        };
        btnFlow.Controls.Add(btnOk);
        btnFlow.Controls.Add(btnCancel);

        root.Controls.Add(hint, 0, 0);
        root.Controls.Add(_list, 0, 1);
        root.Controls.Add(btnFlow, 0, 2);
        Controls.Add(root);

        AcceptButton = btnOk;
        CancelButton = btnCancel;
    }
}
