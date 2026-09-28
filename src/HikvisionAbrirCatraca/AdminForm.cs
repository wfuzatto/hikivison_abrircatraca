using System.Drawing;
using System.Text.RegularExpressions;
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

        Text = "Administração de Catracas";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(1080, 700);
        MinimumSize = new Size(900, 600);
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

        var title = new Label
        {
            Text = "CONTROLE DE CATRACAS",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 20F, FontStyle.Bold),
            ForeColor = Color.FromArgb(35, 42, 52),
            Margin = new Padding(0, 0, 0, 12)
        };
        root.Controls.Add(title, 0, 0);

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
        actions.Controls.Add(MakeButton("Testar abertura", async (_, _) => await TestSelectedAsync()));
        actions.Controls.Add(MakeButton("Sincronizar do HikCentral", async (_, _) => await SyncFromHikCentralAsync()));
        actions.Controls.Add(MakeButton("Alterar senha", (_, _) => ChangePassword()));
        root.Controls.Add(actions, 0, 1);

        ConfigureGrid();
        root.Controls.Add(_grid, 0, 2);

        _status.AutoSize = false;
        _status.Height = 42;
        _status.Dock = DockStyle.Top;
        _status.TextAlign = ContentAlignment.MiddleLeft;
        _status.Padding = new Padding(10, 0, 10, 0);
        _status.BackColor = Color.White;
        _status.Text = "As alterações são salvas localmente neste computador.";
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
            Width = 220
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(ManagedGate.GroupName),
            HeaderText = "Grupo",
            Width = 190
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(ManagedGate.DoorIndexCode),
            HeaderText = "Door Index Code",
            Width = 170
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(ManagedGate.IpAddress),
            HeaderText = "IP / referência",
            Width = 140
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(ManagedGate.ControlDirection),
            HeaderText = "Direção",
            Width = 80
        });
        _grid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            DataPropertyName = nameof(ManagedGate.Enabled),
            HeaderText = "Ativa",
            Width = 65
        });
        _grid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            DataPropertyName = nameof(ManagedGate.ImportedFromHikCentral),
            HeaderText = "Importada",
            Width = 85
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

        if (_settings.ManagedGates.Any(x =>
                x.DoorIndexCode.Equals(form.Gate.DoorIndexCode, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show(this, "Já existe uma catraca com esse Door Index Code.", "Cadastro", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _settings.ManagedGates.Add(form.Gate);
        SaveAndRefresh(form.Gate.Id, $"Catraca adicionada: {form.Gate.Name}");
        SettingsService.Log($"ADMIN GATE ADD name={form.Gate.Name} door={form.Gate.DoorIndexCode} group={form.Gate.GroupName}");
    }

    private void EditSelected()
    {
        var selected = SelectedGate;
        if (selected is null)
        {
            MessageBox.Show(this, "Selecione uma catraca.", "Administração", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var form = new GateEditForm(selected, _settings.Groups);
        if (form.ShowDialog(this) != DialogResult.OK) return;

        if (_settings.ManagedGates.Any(x =>
                x.Id != selected.Id &&
                x.DoorIndexCode.Equals(form.Gate.DoorIndexCode, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show(this, "Já existe outra catraca com esse Door Index Code.", "Cadastro", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var index = _settings.ManagedGates.FindIndex(x => x.Id == selected.Id);
        if (index >= 0) _settings.ManagedGates[index] = form.Gate;

        SaveAndRefresh(form.Gate.Id, $"Catraca atualizada: {form.Gate.Name}");
        SettingsService.Log($"ADMIN GATE EDIT name={form.Gate.Name} door={form.Gate.DoorIndexCode} group={form.Gate.GroupName}");
    }

    private void DeleteSelected()
    {
        var selected = SelectedGate;
        if (selected is null)
        {
            MessageBox.Show(this, "Selecione uma catraca.", "Administração", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var confirm = MessageBox.Show(
            this,
            $"Excluir a catraca '{selected.Name}'?\n\nEla deixará de participar da abertura do grupo {selected.GroupName}.",
            "Confirmar exclusão",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (confirm != DialogResult.Yes) return;

        _settings.ManagedGates.RemoveAll(x => x.Id == selected.Id);
        SettingsService.Save(_settings);
        RefreshGrid();
        SetStatus($"Catraca excluída: {selected.Name}", true);
        SettingsService.Log($"ADMIN GATE DELETE name={selected.Name} door={selected.DoorIndexCode} group={selected.GroupName}");
    }

    private async Task TestSelectedAsync()
    {
        var selected = SelectedGate;
        if (selected is null)
        {
            MessageBox.Show(this, "Selecione uma catraca.", "Administração", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (!_settings.IsConfigured)
        {
            SetStatus("Configure a OpenAPI do HikCentral antes do teste.", false);
            return;
        }

        var confirm = MessageBox.Show(
            this,
            $"Enviar agora um comando de abertura SOMENTE para:\n\n{selected.Name}\n{selected.DoorIndexCode}",
            "Testar abertura individual",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (confirm != DialogResult.Yes) return;

        try
        {
            SetStatus($"Testando abertura de {selected.Name}...", null);
            using var client = new HikCentralClient(_settings);
            var result = await client.OpenDoorsAsync(
                [new DoorInfo(selected.DoorIndexCode, selected.Name)],
                selected.ControlDirection);

            var ok = result.All(x => x.Success);
            SetStatus(ok ? $"Abertura enviada para {selected.Name}." : $"HikCentral retornou falha para {selected.Name}.", ok);
            SettingsService.Log($"ADMIN GATE TEST {(ok ? "OK" : "FAIL")} name={selected.Name} door={selected.DoorIndexCode}");
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, false);
            SettingsService.Log($"ADMIN GATE TEST ERROR name={selected.Name} error={ex.Message}");
        }
    }

    private async Task SyncFromHikCentralAsync()
    {
        if (!_settings.IsConfigured)
        {
            SetStatus("Configure a OpenAPI do HikCentral antes de sincronizar.", false);
            return;
        }

        try
        {
            SetStatus("Consultando Access Levels e catracas no HikCentral...", null);
            using var client = new HikCentralClient(_settings);
            var imported = 0;
            var updated = 0;

            foreach (var group in _settings.Groups)
            {
                var doors = await client.GetDoorsForGroupAsync(group);
                foreach (var door in doors)
                {
                    var existing = _settings.ManagedGates.FirstOrDefault(x =>
                        x.DoorIndexCode.Equals(door.Id, StringComparison.OrdinalIgnoreCase));

                    var ip = ExtractIp(door.Name);
                    if (existing is null)
                    {
                        _settings.ManagedGates.Add(new ManagedGate
                        {
                            Name = door.Name,
                            DoorIndexCode = door.Id,
                            IpAddress = ip,
                            GroupName = group.Name,
                            ControlDirection = group.ControlDirection,
                            Enabled = true,
                            ImportedFromHikCentral = true
                        });
                        imported++;
                    }
                    else
                    {
                        existing.Name = string.IsNullOrWhiteSpace(existing.Name) ? door.Name : existing.Name;
                        if (string.IsNullOrWhiteSpace(existing.IpAddress)) existing.IpAddress = ip;
                        existing.GroupName = group.Name;
                        existing.ImportedFromHikCentral = true;
                        updated++;
                    }
                }
            }

            SettingsService.Save(_settings);
            RefreshGrid();
            SetStatus($"Sincronização concluída: {imported} adicionada(s), {updated} atualizada(s).", true);
            SettingsService.Log($"ADMIN SYNC OK imported={imported} updated={updated}");
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, false);
            SettingsService.Log($"ADMIN SYNC ERROR error={ex.Message}");
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

    private static string ExtractIp(string value)
    {
        var match = Regex.Match(value ?? "", @"\b(?:\d{1,3}\.){3}\d{1,3}\b");
        return match.Success ? match.Value : "";
    }
}
