using System.Drawing;
using HikvisionAbrirCatraca.Models;
using HikvisionAbrirCatraca.Services;

namespace HikvisionAbrirCatraca;

public sealed class SettingsForm : Form
{
    private readonly TextBox _baseUrl = new();
    private readonly TextBox _appKey = new();
    private readonly TextBox _appSecret = new();
    private readonly TextBox _userId = new();
    private readonly CheckBox _verifyTls = new();
    private readonly Label _testStatus = new();
    private readonly Dictionary<string, ComboBox> _directionBoxes = new(StringComparer.OrdinalIgnoreCase);
    private readonly AppSettings _source;

    public AppSettings Settings { get; private set; }

    public SettingsForm(AppSettings current)
    {
        _source = Clone(current);
        Settings = Clone(current);

        Text = "Configurações - HikCentral";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(700, 650);
        MinimumSize = new Size(650, 590);
        BackColor = Color.White;
        Font = new Font("Segoe UI", 10F);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            ColumnCount = 2,
            Padding = new Padding(24)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 185));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Controls.Add(root);

        var heading = new Label
        {
            Text = "HikCentral Professional OpenAPI",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 17F, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 18)
        };
        root.Controls.Add(heading, 0, 0);
        root.SetColumnSpan(heading, 2);

        ConfigureTextBox(_baseUrl, current.BaseUrl);
        ConfigureTextBox(_appKey, current.AppKey);
        ConfigureTextBox(_appSecret, current.AppSecret);
        _appSecret.UseSystemPasswordChar = true;
        ConfigureTextBox(_userId, current.UserId);

        AddField(root, 1, "URL do HikCentral", _baseUrl);
        AddField(root, 2, "AppKey", _appKey);
        AddField(root, 3, "AppSecret", _appSecret);
        AddField(root, 4, "User ID", _userId);

        _verifyTls.Text = "Validar certificado TLS";
        _verifyTls.Checked = current.VerifyTls;
        _verifyTls.AutoSize = true;
        _verifyTls.Margin = new Padding(0, 8, 0, 12);
        root.Controls.Add(new Label { Text = "HTTPS", AutoSize = true, Margin = new Padding(0, 10, 0, 0) }, 0, 5);
        root.Controls.Add(_verifyTls, 1, 5);

        var directionsTitle = new Label
        {
            Text = "Direção do comando",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 12F, FontStyle.Bold),
            Margin = new Padding(0, 18, 0, 10)
        };
        root.Controls.Add(directionsTitle, 0, 6);
        root.SetColumnSpan(directionsTitle, 2);

        var row = 7;
        foreach (var group in current.Groups)
        {
            var box = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Dock = DockStyle.Top,
                Margin = new Padding(0, 4, 0, 8)
            };
            box.Items.Add(new DirectionItem(0, "0 - Entrada / direção padrão"));
            box.Items.Add(new DirectionItem(1, "1 - Saída"));
            box.SelectedIndex = group.ControlDirection == 1 ? 1 : 0;
            _directionBoxes[group.Name] = box;
            AddField(root, row++, group.Name, box);
        }

        _testStatus.AutoSize = true;
        _testStatus.MaximumSize = new Size(600, 0);
        _testStatus.Margin = new Padding(0, 12, 0, 12);
        _testStatus.ForeColor = Color.FromArgb(70, 78, 90);
        root.Controls.Add(_testStatus, 0, row);
        root.SetColumnSpan(_testStatus, 2);
        row++;

        var actions = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Margin = new Padding(0, 10, 0, 0)
        };

        var save = new Button
        {
            Text = "Salvar",
            AutoSize = true,
            Height = 40,
            Padding = new Padding(18, 0, 18, 0),
            BackColor = Color.FromArgb(220, 52, 56),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        save.FlatAppearance.BorderSize = 0;
        save.Click += (_, _) => SaveAndClose();

        var cancel = new Button
        {
            Text = "Cancelar",
            AutoSize = true,
            Height = 40,
            Padding = new Padding(14, 0, 14, 0)
        };
        cancel.Click += (_, _) => DialogResult = DialogResult.Cancel;

        var test = new Button
        {
            Text = "Testar conexão e grupos",
            AutoSize = true,
            Height = 40,
            Padding = new Padding(14, 0, 14, 0)
        };
        test.Click += async (_, _) => await TestConnectionAsync(test);

        actions.Controls.Add(save);
        actions.Controls.Add(cancel);
        actions.Controls.Add(test);
        root.Controls.Add(actions, 0, row);
        root.SetColumnSpan(actions, 2);

        AcceptButton = save;
        CancelButton = cancel;
    }

    private async Task TestConnectionAsync(Button button)
    {
        button.Enabled = false;
        _testStatus.Text = "Consultando Access Levels no HikCentral...";
        _testStatus.ForeColor = Color.FromArgb(70, 78, 90);

        try
        {
            var candidate = BuildSettingsFromForm();
            using var client = new HikCentralClient(candidate);
            var result = await client.TestGroupsAsync(candidate.Groups);
            _testStatus.Text = "Conexão OK — " + string.Join(" | ", result.Select(x => $"{x.Key}: {x.Value}"));
            _testStatus.ForeColor = Color.FromArgb(34, 94, 58);
        }
        catch (Exception ex)
        {
            _testStatus.Text = ex.Message;
            _testStatus.ForeColor = Color.FromArgb(164, 43, 43);
        }
        finally
        {
            button.Enabled = true;
        }
    }

    private void SaveAndClose()
    {
        var candidate = BuildSettingsFromForm();
        if (string.IsNullOrWhiteSpace(candidate.BaseUrl) ||
            !Uri.TryCreate(candidate.BaseUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            MessageBox.Show(this, "Informe uma URL válida do HikCentral.", "Configuração", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        Settings = candidate;
        DialogResult = DialogResult.OK;
    }

    private AppSettings BuildSettingsFromForm()
    {
        var settings = Clone(_source);
        settings.BaseUrl = _baseUrl.Text.Trim().TrimEnd('/');
        settings.AppKey = _appKey.Text.Trim();
        settings.AppSecret = _appSecret.Text;
        settings.UserId = string.IsNullOrWhiteSpace(_userId.Text) ? "admin" : _userId.Text.Trim();
        settings.VerifyTls = _verifyTls.Checked;

        foreach (var group in settings.Groups)
        {
            if (_directionBoxes.TryGetValue(group.Name, out var box) && box.SelectedItem is DirectionItem item)
                group.ControlDirection = item.Value;
        }

        return settings;
    }

    private static void AddField(TableLayoutPanel root, int row, string label, Control control)
    {
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.Controls.Add(new Label
        {
            Text = label,
            AutoSize = true,
            Margin = new Padding(0, 10, 12, 0)
        }, 0, row);
        root.Controls.Add(control, 1, row);
    }

    private static void ConfigureTextBox(TextBox box, string value)
    {
        box.Text = value;
        box.Dock = DockStyle.Top;
        box.Margin = new Padding(0, 4, 0, 8);
    }

    private static AppSettings Clone(AppSettings source) =>
        new()
        {
            BaseUrl = source.BaseUrl,
            AppKey = source.AppKey,
            AppSecret = source.AppSecret,
            UserId = source.UserId,
            VerifyTls = source.VerifyTls,
            Groups = source.Groups.Select(x => new GateGroup
            {
                Name = x.Name,
                Aliases = [.. x.Aliases],
                ButtonText = x.ButtonText,
                ControlDirection = x.ControlDirection,
                Reference = x.Reference
            }).ToList()
        };

    private sealed record DirectionItem(int Value, string Text)
    {
        public override string ToString() => Text;
    }
}
