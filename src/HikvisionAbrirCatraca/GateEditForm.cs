using System.Drawing;
using HikvisionAbrirCatraca.Models;

namespace HikvisionAbrirCatraca;

public sealed class GateEditForm : Form
{
    private readonly TextBox _name = new();
    private readonly TextBox _doorCode = new();
    private readonly TextBox _ip = new();
    private readonly ComboBox _group = new();
    private readonly ComboBox _direction = new();
    private readonly CheckBox _enabled = new();
    private readonly ManagedGate _original;

    public ManagedGate Gate { get; private set; }

    public GateEditForm(ManagedGate? gate, IReadOnlyList<GateGroup> groups)
    {
        _original = gate?.Clone() ?? new ManagedGate();
        Gate = _original.Clone();

        Text = gate is null ? "Adicionar catraca" : "Editar catraca";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(560, 430);
        Font = new Font("Segoe UI", 10F);
        BackColor = Color.White;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(24)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Controls.Add(root);

        AddField(root, 0, "Nome amigável", _name);
        AddField(root, 1, "Door Index Code", _doorCode);
        AddField(root, 2, "IP / referência", _ip);

        _group.DropDownStyle = ComboBoxStyle.DropDownList;
        _group.Items.AddRange(groups.Select(x => x.Name).Cast<object>().ToArray());
        AddField(root, 3, "Grupo", _group);

        _direction.DropDownStyle = ComboBoxStyle.DropDownList;
        _direction.Items.Add(new DirectionItem(0, "0 - Entrada / padrão"));
        _direction.Items.Add(new DirectionItem(1, "1 - Saída"));
        AddField(root, 4, "Direção", _direction);

        _enabled.Text = "Ativa para abertura";
        _enabled.AutoSize = true;
        root.Controls.Add(new Label { Text = "Status", AutoSize = true, Margin = new Padding(0, 10, 8, 0) }, 0, 5);
        root.Controls.Add(_enabled, 1, 5);

        _name.Text = _original.Name;
        _doorCode.Text = _original.DoorIndexCode;
        _ip.Text = _original.IpAddress;
        _group.SelectedItem = groups.Any(x => x.Name == _original.GroupName) ? _original.GroupName : groups.FirstOrDefault()?.Name;
        _direction.SelectedIndex = _original.ControlDirection == 1 ? 1 : 0;
        _enabled.Checked = gate is null || _original.Enabled;

        var actions = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Top,
            Margin = new Padding(0, 20, 0, 0)
        };

        var save = new Button
        {
            Text = "Salvar",
            Width = 100,
            Height = 38,
            BackColor = Color.FromArgb(220, 52, 56),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        save.FlatAppearance.BorderSize = 0;
        save.Click += (_, _) => SaveGate();

        var cancel = new Button { Text = "Cancelar", Width = 100, Height = 38 };
        cancel.Click += (_, _) => DialogResult = DialogResult.Cancel;

        actions.Controls.Add(save);
        actions.Controls.Add(cancel);
        root.Controls.Add(actions, 0, 6);
        root.SetColumnSpan(actions, 2);

        AcceptButton = save;
        CancelButton = cancel;
    }

    private void SaveGate()
    {
        if (string.IsNullOrWhiteSpace(_name.Text) ||
            string.IsNullOrWhiteSpace(_doorCode.Text) ||
            _group.SelectedItem is null)
        {
            MessageBox.Show(this, "Nome, Door Index Code e Grupo são obrigatórios.", "Cadastro", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        Gate = _original.Clone();
        Gate.Name = _name.Text.Trim();
        Gate.DoorIndexCode = _doorCode.Text.Trim();
        Gate.IpAddress = _ip.Text.Trim();
        Gate.GroupName = _group.SelectedItem.ToString()!;
        Gate.ControlDirection = (_direction.SelectedItem as DirectionItem)?.Value ?? 0;
        Gate.Enabled = _enabled.Checked;
        DialogResult = DialogResult.OK;
    }

    private static void AddField(TableLayoutPanel root, int row, string label, Control control)
    {
        control.Dock = DockStyle.Top;
        control.Margin = new Padding(0, 4, 0, 10);
        root.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(0, 10, 8, 0) }, 0, row);
        root.Controls.Add(control, 1, row);
    }

    private sealed record DirectionItem(int Value, string Text)
    {
        public override string ToString() => Text;
    }
}
