using System.Drawing;
using HikvisionAbrirCatraca.Models;
using HikvisionAbrirCatraca.Services;

namespace HikvisionAbrirCatraca;

public sealed class AdminForm : Form
{
    private readonly AppSettings _settings;
    private readonly DataGridView _grid = new();
    private readonly BindingSource _binding = new();
    private readonly Label _status = new();

    public AdminForm(AppSettings settings)
    {
        _settings = settings;

        Text = "Administração de Catracas - Conexão Direta";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(1180, 720);
        MinimumSize = new Size(980, 600);
        Font = new Font("Segoe UI", 10F);
        BackColor = Color.FromArgb(245, 247, 250);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(18)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        var heading = new Panel { Dock = DockStyle.Top, Height = 66 };
        heading.Controls.Add(new Label
        {
            Text = "CONTROLE DE CATRACAS",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 20F, FontStyle.Bold),
            ForeColor = Color.FromArgb(35, 42, 52),
            Location = new Point(0, 0)
        });
        heading.Controls.Add(new Label
        {
            Text = "Conexão direta ISAPI — sem HikCentral",
            AutoSize = true,
            ForeColor = Color.FromArgb(95, 105, 118),
            Location = new Point(3, 39)
        });
        root.Controls.Add(heading, 0, 0);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Margin = new Padding(0, 0, 0, 12)
        };

        actions.Controls.Add(MakeButton("+ Adicionar", (_, _) => AddGate()));
        actions.Controls.Add(MakeButton("Editar", (_, _) => EditSelected()));
        actions.Controls.Add(MakeButton("Excluir", (_, _) => DeleteSelected()));
        actions.Controls.Add(MakeButton("Testar conexão", async (_, _) => await TestConnectionAsync()));
        actions.Controls.Add(MakeButton("Testar abertura", async (_, _) => await TestOpeningAsync()));
        actions.Controls.Add(MakeButton("Testar todas", async (_, _) => await TestAllAsync()));
        actions.Controls.Add(MakeButton("Abrir log", (_, _) => OpenLog()));
        actions.Controls.Add(MakeButton("Alterar senha admin", (_, _) => ChangePassword()));
        root.Controls.Add(actions, 0, 1);

        ConfigureGrid();
        root.Controls.Add(_grid, 0, 2);

        _status.AutoSize = false;
        _status.Height = 46;
        _status.Dock = DockStyle.Top;
        _status.TextAlign = ContentAlignment.MiddleLeft;
        _status.Padding = new Padding(10, 0, 10, 0);
        _status.BackColor = Color.White;
        _status.Text = "Cadastre IP, usuário, senha e door Nº de cada catraca.";
        root.Controls.Add(_status, 0, 3);

        RefreshGrid();
    }

    private void ConfigureGrid()
    {
        _grid.Dock = DockStyle.Fill;
        _grid.BackgroundColor = Color.White;
        _grid.BorderStyle = BorderStyle.FixedSingle;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AllowUserToResizeRows = false;
        _grid.ReadOnly = true;
        _grid.MultiSelect = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.AutoGenerateColumns = false;
        _grid.RowHeadersVisible = false;

        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(ManagedGate.Name),
            HeaderText = "Nome",
            Width = 185
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(ManagedGate.GroupName),
            HeaderText = "Grupo",
            Width = 180
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(ManagedGate.Host),
            HeaderText = "IP / host",
            Width = 140
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(ManagedGate.Port),
            HeaderText = "Porta",
            Width = 65
        });
        _grid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            DataPropertyName = nameof(ManagedGate.UseHttps),
            HeaderText = "HTTPS",
            Width = 65
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(ManagedGate.Username),
            HeaderText = "Usuário",
            Width = 90
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(ManagedGate.DoorNo),
            HeaderText = "Door Nº",
            Width = 70
        });
        _grid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            DataPropertyName = nameof(ManagedGate.Enabled),
            HeaderText = "Ativa",
            Width = 60
        });

        _grid.DataSource = _binding;
        _grid.CellDoubleClick += (_, _) => EditSelected();
    }

    private Button MakeButton(string text, EventHandler click)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            Height = 38,
            Padding = new Padding(12, 0, 12, 0),
            BackColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(0, 0, 8, 8)
        };
        button.FlatAppearance.BorderColor = Color.FromArgb(205, 211, 220);
        button.Click += click;
        return button;
    }

    private ManagedGate? SelectedGate =>
        _grid.CurrentRow?.DataBoundItem as ManagedGate;

    private void RefreshGrid(string? selectId = null)
    {
        var rows = _settings.ManagedGates
            .OrderBy(x => x.GroupName)
            .ThenBy(x => x.Name)
            .ToList();

        _binding.DataSource = rows;

        if (!string.IsNullOrWhiteSpace(selectId))
        {
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.DataBoundItem is ManagedGate g && g.Id == selectId)
                {
                    row.Selected = true;
                    _grid.CurrentCell = row.Cells[0];
                    break;
                }
            }
        }
    }

    private void AddGate()
    {
        using var form = new GateEditForm(null, _settings.Groups);
        if (form.ShowDialog(this) != DialogResult.OK) return;

        if (HasDuplicate(form.Gate, null))
        {
            MessageBox.Show(
                this,
                "Já existe uma catraca com o mesmo IP/porta e Door Nº.",
                "Cadastro",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        _settings.ManagedGates.Add(form.Gate);
        SaveAndRefresh(form.Gate.Id, $"Catraca adicionada: {form.Gate.Name}");
        SettingsService.Log($"ADMIN GATE ADD name={form.Gate.Name} endpoint={form.Gate.Endpoint} door={form.Gate.DoorNo}");
    }

    private void EditSelected()
    {
        var selected = SelectedGate;
        if (selected is null)
        {
            InfoSelect();
            return;
        }

        using var form = new GateEditForm(selected, _settings.Groups);
        if (form.ShowDialog(this) != DialogResult.OK) return;

        if (HasDuplicate(form.Gate, selected.Id))
        {
            MessageBox.Show(
                this,
                "Já existe outra catraca com o mesmo IP/porta e Door Nº.",
                "Cadastro",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        var index = _settings.ManagedGates.FindIndex(x => x.Id == selected.Id);
        if (index >= 0) _settings.ManagedGates[index] = form.Gate;

        SaveAndRefresh(form.Gate.Id, $"Catraca atualizada: {form.Gate.Name}");
        SettingsService.Log($"ADMIN GATE EDIT name={form.Gate.Name} endpoint={form.Gate.Endpoint} door={form.Gate.DoorNo}");
    }

    private bool HasDuplicate(ManagedGate gate, string? ignoreId) =>
        _settings.ManagedGates.Any(x =>
            x.Id != ignoreId &&
            x.Host.Equals(gate.Host, StringComparison.OrdinalIgnoreCase) &&
            x.Port == gate.Port &&
            x.DoorNo == gate.DoorNo);

    private void DeleteSelected()
    {
        var selected = SelectedGate;
        if (selected is null)
        {
            InfoSelect();
            return;
        }

        var confirm = MessageBox.Show(
            this,
            $"Excluir a catraca '{selected.Name}'?\n\n{selected.Endpoint} / door {selected.DoorNo}",
            "Confirmar exclusão",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (confirm != DialogResult.Yes) return;

        _settings.ManagedGates.RemoveAll(x => x.Id == selected.Id);
        SettingsService.Save(_settings);
        RefreshGrid();
        SetStatus($"Catraca excluída: {selected.Name}", true);
        SettingsService.Log($"ADMIN GATE DELETE name={selected.Name} endpoint={selected.Endpoint} door={selected.DoorNo}");
    }

    private async Task TestConnectionAsync()
    {
        var gate = SelectedGate;
        if (gate is null)
        {
            InfoSelect();
            return;
        }

        try
        {
            SetStatus($"Conectando diretamente em {gate.Endpoint}...", null);
            using var client = new DirectIsapiClient(gate);
            var info = await client.GetDeviceInfoAsync();

            var model = string.IsNullOrWhiteSpace(info.Model) ? "modelo não informado" : info.Model;
            var firmware = string.IsNullOrWhiteSpace(info.FirmwareVersion) ? "" : $" · FW {info.FirmwareVersion}";
            SetStatus($"Conexão OK: {gate.Name} · {model}{firmware}", true);
            SettingsService.Log($"DIRECT TEST OK name={gate.Name} endpoint={gate.Endpoint} model={info.Model}");
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, false);
            SettingsService.Log($"DIRECT TEST ERROR name={gate.Name} endpoint={gate.Endpoint} error={ex.Message}");
        }
    }

    private async Task TestOpeningAsync()
    {
        var gate = SelectedGate;
        if (gate is null)
        {
            InfoSelect();
            return;
        }

        var confirm = MessageBox.Show(
            this,
            $"ABRIR AGORA esta catraca?\n\n{gate.Name}\n{gate.Endpoint}\nDoor Nº {gate.DoorNo}",
            "Teste real de abertura",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (confirm != DialogResult.Yes) return;

        SetStatus($"Enviando abertura direta para {gate.Name}...", null);
        using var client = new DirectIsapiClient(gate);
        var result = await client.OpenAsync();

        SetStatus(result.Success
            ? $"Abertura confirmada por {gate.Name}."
            : $"{gate.Name}: {result.Description}", result.Success);

        SettingsService.Log(
            $"DIRECT OPEN TEST {(result.Success ? "OK" : "FAIL")} name={gate.Name} endpoint={gate.Endpoint} door={gate.DoorNo} detail={result.Description}");
    }

    private async Task TestAllAsync()
    {
        var gates = _settings.ManagedGates.Where(x => x.Enabled).ToList();
        if (gates.Count == 0)
        {
            SetStatus("Nenhuma catraca ativa cadastrada.", false);
            return;
        }

        SetStatus($"Testando conexão com {gates.Count} catraca(s), sem abrir...", null);

        var tasks = gates.Select(async gate =>
        {
            try
            {
                using var client = new DirectIsapiClient(gate);
                var info = await client.GetDeviceInfoAsync();
                return (gate, ok: true, detail: string.IsNullOrWhiteSpace(info.Model) ? "OK" : info.Model);
            }
            catch (Exception ex)
            {
                return (gate, ok: false, detail: ex.Message);
            }
        });

        var results = await Task.WhenAll(tasks);
        var failed = results.Where(x => !x.ok).ToList();

        if (failed.Count == 0)
        {
            SetStatus($"Todas as {results.Length} catracas responderam diretamente.", true);
        }
        else
        {
            SetStatus($"{results.Length - failed.Count}/{results.Length} responderam. Falhas: " +
                      string.Join(" | ", failed.Select(x => $"{x.gate.Name}: {x.detail}")), false);
        }
    }

    private void OpenLog()
    {
        try
        {
            Directory.CreateDirectory(SettingsService.DataDirectory);
            if (!File.Exists(SettingsService.LogPath))
            {
                File.WriteAllText(
                    SettingsService.LogPath,
                    "Log de operações - Hikvision Abrir Catraca" + Environment.NewLine);
            }

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = SettingsService.LogPath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            SetStatus("Não foi possível abrir o log: " + ex.Message, false);
        }
    }

    private void ChangePassword()
    {
        using var form = new Form
        {
            Text = "Alterar senha administrativa",
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            ClientSize = new Size(420, 245),
            Font = new Font("Segoe UI", 10F),
            BackColor = Color.White
        };

        var p1 = new TextBox { Left = 24, Top = 78, Width = 365, UseSystemPasswordChar = true };
        var p2 = new TextBox { Left = 24, Top = 142, Width = 365, UseSystemPasswordChar = true };
        form.Controls.Add(new Label { Text = "Nova senha", Left = 24, Top = 53, AutoSize = true });
        form.Controls.Add(p1);
        form.Controls.Add(new Label { Text = "Confirmar nova senha", Left = 24, Top = 117, AutoSize = true });
        form.Controls.Add(p2);

        var save = new Button { Text = "Salvar", Left = 289, Top = 190, Width = 100, Height = 34 };
        save.Click += (_, _) =>
        {
            if (p1.Text.Length < 4)
            {
                MessageBox.Show(form, "Use pelo menos 4 caracteres.");
                return;
            }
            if (p1.Text != p2.Text)
            {
                MessageBox.Show(form, "As senhas não conferem.");
                return;
            }

            AdminSecurity.SetPassword(_settings, p1.Text);
            SettingsService.Save(_settings);
            SettingsService.Log("ADMIN PASSWORD CHANGED");
            form.DialogResult = DialogResult.OK;
        };
        form.Controls.Add(save);
        form.AcceptButton = save;

        if (form.ShowDialog(this) == DialogResult.OK)
            SetStatus("Senha administrativa alterada.", true);
    }

    private void InfoSelect() =>
        MessageBox.Show(
            this,
            "Selecione uma catraca.",
            "Administração",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);

    private void SaveAndRefresh(string id, string status)
    {
        SettingsService.Save(_settings);
        RefreshGrid(id);
        SetStatus(status, true);
    }

    private void SetStatus(string text, bool? success)
    {
        _status.Text = text;
        _status.ForeColor = success switch
        {
            true => Color.FromArgb(34, 94, 58),
            false => Color.FromArgb(164, 43, 43),
            _ => Color.FromArgb(57, 67, 80)
        };
        _status.BackColor = success switch
        {
            true => Color.FromArgb(234, 247, 238),
            false => Color.FromArgb(253, 238, 238),
            _ => Color.White
        };
    }
}
