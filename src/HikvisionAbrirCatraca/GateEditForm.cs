using System.Drawing;
using HikvisionAbrirCatraca.Models;

namespace HikvisionAbrirCatraca;

public sealed class GateEditForm : Form
{
    private readonly TextBox _name = new();
    private readonly TextBox _host = new();
    private readonly NumericUpDown _port = new();
    private readonly CheckBox _https = new();
    private readonly CheckBox _verifyTls = new();
    private readonly TextBox _username = new();
    private readonly TextBox _password = new();
    private readonly NumericUpDown _doorNo = new();
    private readonly ComboBox _group = new();
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
        ClientSize = new Size(610, 585);
        Font = new Font("Segoe UI", 10F);
        BackColor = Color.White;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(24),
            AutoScroll = true
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 165));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Controls.Add(root);

        var title = new Label
        {
            Text = "CONEXÃO DIRETA ISAPI",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 16F, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 14)
        };
        root.Controls.Add(title, 0, 0);
        root.SetColumnSpan(title, 2);

        AddField(root, 1, "Nome amigável", _name);
        AddField(root, 2, "IP / hostname", _host);

        _port.Minimum = 1;
        _port.Maximum = 65535;
        AddField(root, 3, "Porta HTTP(S)", _port);

        _https.Text = "Usar HTTPS";
        _https.AutoSize = true;
        _https.CheckedChanged += (_, _) =>
        {
            if (_https.Checked && _port.Value == 80) _port.Value = 443;
            if (!_https.Checked && _port.Value == 443) _port.Value = 80;
            _verifyTls.Enabled = _https.Checked;
        };
        AddField(root, 4, "Protocolo", _https);

        _verifyTls.Text = "Validar certificado TLS";
        _verifyTls.AutoSize = true;
        AddField(root, 5, "Certificado", _verifyTls);

        AddField(root, 6, "Usuário", _username);

        _password.UseSystemPasswordChar = true;
        AddField(root, 7, "Senha", _password);

        _doorNo.Minimum = 1;
        _doorNo.Maximum = 64;
        AddField(root, 8, "Porta/door Nº", _doorNo);

        _group.DropDownStyle = ComboBoxStyle.DropDownList;
        _group.Items.AddRange(groups.Select(x => x.Name).Cast<object>().ToArray());
        AddField(root, 9, "Grupo operacional", _group);

        _enabled.Text = "Ativa para abertura";
        _enabled.AutoSize = true;
        AddField(root, 10, "Status", _enabled);

        var hint = new Label
        {
            Text = "O aplicativo acessa diretamente: PUT /ISAPI/AccessControl/RemoteControl/door/{doorNo} usando autenticação HTTP Digest.",
            AutoSize = true,
            MaximumSize = new Size(530, 0),
            ForeColor = Color.DimGray,
            Margin = new Padding(0, 12, 0, 10)
        };
        root.Controls.Add(hint, 0, 11);
        root.SetColumnSpan(hint, 2);

        _name.Text = _original.Name;
        _host.Text = _original.Host;
        _port.Value = _original.Port is > 0 and <= 65535 ? _original.Port : 80;
        _https.Checked = _original.UseHttps;
        _verifyTls.Checked = _original.VerifyTls;
        _verifyTls.Enabled = _https.Checked;
        _username.Text = string.IsNullOrWhiteSpace(_original.Username) ? "admin" : _original.Username;
        _password.Text = _original.Password;
        _doorNo.Value = _original.DoorNo > 0 ? _original.DoorNo : 1;
        _group.SelectedItem = groups.Any(x => x.Name == _original.GroupName)
            ? _original.GroupName
            : groups.FirstOrDefault()?.Name;
        _enabled.Checked = gate is null || _original.Enabled;

        var actions = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Top,
            Margin = new Padding(0, 18, 0, 0)
        };

        var save = new Button
        {
            Text = "Salvar",
            Width = 105,
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
        root.Controls.Add(actions, 0, 12);
        root.SetColumnSpan(actions, 2);

        AcceptButton = save;
        CancelButton = cancel;
    }

    private void SaveGate()
    {
        if (string.IsNullOrWhiteSpace(_name.Text) ||
            string.IsNullOrWhiteSpace(_host.Text) ||
            string.IsNullOrWhiteSpace(_username.Text) ||
            string.IsNullOrWhiteSpace(_password.Text) ||
            _group.SelectedItem is null)
        {
            MessageBox.Show(
                this,
                "Nome, IP/hostname, usuário, senha e grupo são obrigatórios.",
                "Cadastro",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        Gate = _original.Clone();
        Gate.Name = _name.Text.Trim();
        Gate.Host = _host.Text.Trim()
            .Replace("http://", "", StringComparison.OrdinalIgnoreCase)
            .Replace("https://", "", StringComparison.OrdinalIgnoreCase)
            .TrimEnd('/');
        Gate.Port = (int)_port.Value;
        Gate.UseHttps = _https.Checked;
        Gate.VerifyTls = _verifyTls.Checked;
        Gate.Username = _username.Text.Trim();
        Gate.Password = _password.Text;
        Gate.DoorNo = (int)_doorNo.Value;
        Gate.GroupName = _group.SelectedItem.ToString()!;
        Gate.Enabled = _enabled.Checked;

        DialogResult = DialogResult.OK;
    }

    private static void AddField(TableLayoutPanel root, int row, string label, Control control)
    {
        control.Dock = DockStyle.Top;
        control.Margin = new Padding(0, 4, 0, 9);
        root.Controls.Add(new Label
        {
            Text = label,
            AutoSize = true,
            Margin = new Padding(0, 10, 8, 0)
        }, 0, row);
        root.Controls.Add(control, 1, row);
    }
}
