using System.Diagnostics;
using System.Drawing;
using HikvisionAbrirCatraca.Models;
using HikvisionAbrirCatraca.Services;

namespace HikvisionAbrirCatraca;

public sealed class MainForm : Form
{
    private AppSettings _settings;
    private readonly Label _status;
    private readonly FlowLayoutPanel _cards;
    private readonly Button _adminButton;
    private readonly List<Button> _actionButtons = [];
    private CancellationTokenSource? _operationCts;
    private GateGroup? _selectedGroup;

    public MainForm()
    {
        _settings = SettingsService.Load();

        Text = "AcquaVale - Abrir Catracas";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(860, 620);
        Size = new Size(1000, 700);
        BackColor = Color.FromArgb(245, 247, 250);
        Font = new Font("Segoe UI", 10F);
        AutoScaleMode = AutoScaleMode.Dpi;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(24),
            BackColor = BackColor
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            Margin = new Padding(0, 0, 0, 18)
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var heading = new Panel { Dock = DockStyle.Fill, AutoSize = true };
        heading.Controls.Add(new Label
        {
            Text = "ABERTURA DE CATRACAS",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 22F, FontStyle.Bold),
            ForeColor = Color.FromArgb(35, 42, 52),
            Location = new Point(0, 0)
        });
        heading.Controls.Add(new Label
        {
            Text = "Selecione o bloco e depois a catraca que deseja abrir",
            AutoSize = true,
            ForeColor = Color.FromArgb(98, 108, 121),
            Location = new Point(3, 45)
        });

        _adminButton = HeaderButton("🔒 Administração");
        _adminButton.Click += (_, _) => OpenAdministration();

        header.Controls.Add(heading, 0, 0);
        header.Controls.Add(_adminButton, 1, 0);
        root.Controls.Add(header, 0, 0);

        _cards = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };
        root.Controls.Add(_cards, 0, 1);

        _status = new Label
        {
            Text = _settings.ManagedGates.Count > 0
                ? "Selecione um bloco."
                : "Nenhuma catraca cadastrada. Entre em Administração para configurar os IPs.",
            Dock = DockStyle.Top,
            AutoSize = false,
            Height = 46,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(14, 0, 14, 0),
            Font = new Font("Segoe UI Semibold", 10.5F),
            BackColor = Color.White,
            ForeColor = _settings.ManagedGates.Count > 0
                ? Color.FromArgb(34, 94, 58)
                : Color.FromArgb(156, 97, 20),
            Margin = new Padding(0, 16, 0, 10)
        };
        root.Controls.Add(_status, 0, 2);

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };

        var logButton = new Button
        {
            Text = "Abrir log",
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White
        };
        logButton.Click += (_, _) => OpenLog();

        footer.Controls.Add(logButton);
        footer.Controls.Add(new Label
        {
            Text = "Abra uma catraca individualmente ou use ABRIR TODAS dentro do bloco selecionado.",
            AutoSize = true,
            ForeColor = Color.FromArgb(98, 108, 121),
            Padding = new Padding(12, 7, 0, 0)
        });
        root.Controls.Add(footer, 0, 3);

        BuildGroupCards();
        FormClosing += (_, _) => _operationCts?.Cancel();
    }

    private Button HeaderButton(string text)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            Height = 42,
            Padding = new Padding(12, 0, 12, 0),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            Cursor = Cursors.Hand,
            Margin = new Padding(8, 0, 0, 0)
        };
        button.FlatAppearance.BorderColor = Color.FromArgb(210, 215, 223);
        return button;
    }

    private void BuildGroupCards()
    {
        _selectedGroup = null;
        _cards.SuspendLayout();
        _cards.Controls.Clear();
        _actionButtons.Clear();

        foreach (var group in _settings.Groups)
        {
            var gates = _settings.ManagedGates
                .Where(x =>
                    x.Enabled &&
                    x.GroupName.Equals(group.Name, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.Name)
                .ToList();

            var card = new Panel
            {
                Width = 440,
                Height = 215,
                Margin = new Padding(0, 0, 18, 18),
                BackColor = Color.White
            };

            card.Controls.Add(new Label
            {
                Text = group.Name,
                AutoSize = false,
                Width = 400,
                Height = 32,
                Location = new Point(20, 18),
                Font = new Font("Segoe UI Semibold", 13F, FontStyle.Bold),
                ForeColor = Color.FromArgb(55, 63, 74)
            });

            card.Controls.Add(new Label
            {
                Text = gates.Count == 0
                    ? "Nenhuma catraca ativa cadastrada"
                    : $"{gates.Count} catraca(s) disponível(is)",
                AutoSize = false,
                Width = 400,
                Height = 24,
                Location = new Point(20, 52),
                ForeColor = Color.FromArgb(105, 114, 126)
            });

            var button = new Button
            {
                Text = "SELECIONAR BLOCO",
                Width = 400,
                Height = 105,
                Location = new Point(20, 88),
                Font = new Font("Segoe UI Semibold", 16F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(47, 102, 176),
                ForeColor = Color.White,
                Cursor = Cursors.Hand,
                Enabled = gates.Count > 0,
                Tag = group
            };
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(38, 83, 145);
            button.Click += (_, _) =>
            {
                if (button.Tag is GateGroup selected)
                    BuildGateCards(selected);
            };

            card.Controls.Add(button);
            _cards.Controls.Add(card);
            _actionButtons.Add(button);
        }

        _cards.ResumeLayout();
        SetStatus("Selecione o bloco onde está a catraca.", null);
    }

    private void BuildGateCards(GateGroup group)
    {
        _selectedGroup = group;
        _cards.SuspendLayout();
        _cards.Controls.Clear();
        _actionButtons.Clear();

        var top = new Panel
        {
            Width = 900,
            Height = 70,
            Margin = new Padding(0, 0, 18, 18),
            BackColor = Color.Transparent
        };

        var back = new Button
        {
            Text = "← Voltar aos blocos",
            Width = 170,
            Height = 42,
            Location = new Point(0, 6),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            Cursor = Cursors.Hand
        };
        back.FlatAppearance.BorderColor = Color.FromArgb(205, 211, 220);
        back.Click += (_, _) => BuildGroupCards();

        top.Controls.Add(back);
        top.Controls.Add(new Label
        {
            Text = group.Name,
            AutoSize = true,
            Location = new Point(192, 11),
            Font = new Font("Segoe UI Semibold", 18F, FontStyle.Bold),
            ForeColor = Color.FromArgb(35, 42, 52)
        });

        var gates = _settings.ManagedGates
            .Where(x =>
                x.Enabled &&
                x.GroupName.Equals(group.Name, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Name)
            .ToList();

        var openAll = new Button
        {
            Text = "ABRIR TODAS",
            Width = 170,
            Height = 42,
            Location = new Point(710, 6),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(220, 52, 56),
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 10.5F, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Enabled = gates.Count > 0,
            Tag = group
        };
        openAll.FlatAppearance.BorderSize = 0;
        openAll.FlatAppearance.MouseOverBackColor = Color.FromArgb(195, 40, 44);
        openAll.Click += async (_, _) => await OpenAllGroupAsync(group);

        top.Controls.Add(openAll);
        _cards.Controls.Add(top);
        _actionButtons.Add(back);
        _actionButtons.Add(openAll);

        foreach (var gate in gates)
        {
            var card = new Panel
            {
                Width = 440,
                Height = 225,
                Margin = new Padding(0, 0, 18, 18),
                BackColor = Color.White
            };

            card.Controls.Add(new Label
            {
                Text = gate.Name,
                AutoSize = false,
                Width = 400,
                Height = 32,
                Location = new Point(20, 18),
                Font = new Font("Segoe UI Semibold", 13F, FontStyle.Bold),
                ForeColor = Color.FromArgb(55, 63, 74)
            });

            card.Controls.Add(new Label
            {
                Text = $"{gate.Host}:{gate.Port}  ·  Door {gate.DoorNo}",
                AutoSize = false,
                Width = 400,
                Height = 24,
                Location = new Point(20, 52),
                ForeColor = Color.FromArgb(105, 114, 126)
            });

            var open = new Button
            {
                Text = "ABRIR ESTA CATRACA",
                Width = 400,
                Height = 116,
                Location = new Point(20, 88),
                Font = new Font("Segoe UI Semibold", 17F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(220, 52, 56),
                ForeColor = Color.White,
                Cursor = Cursors.Hand,
                Tag = gate
            };
            open.FlatAppearance.BorderSize = 0;
            open.FlatAppearance.MouseOverBackColor = Color.FromArgb(195, 40, 44);
            open.Click += GateButton_Click;

            card.Controls.Add(open);
            _cards.Controls.Add(card);
            _actionButtons.Add(open);
        }

        _cards.ResumeLayout();
        SetStatus($"{group.Name}: escolha qual catraca deseja abrir.", null);
    }

    private async Task OpenAllGroupAsync(GateGroup group)
    {
        var gates = _settings.ManagedGates
            .Where(x =>
                x.Enabled &&
                x.GroupName.Equals(group.Name, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Name)
            .ToList();

        if (gates.Count == 0)
        {
            SetStatus($"Nenhuma catraca ativa cadastrada em {group.Name}.", false);
            return;
        }

        var confirm = MessageBox.Show(
            this,
            $"Abrir TODAS as {gates.Count} catraca(s) de {group.Name}?\n\n" +
            string.Join("\n", gates.Select(x => $"• {x.Name} ({x.Host})")),
            "Confirmar abertura de todas",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);

        if (confirm != DialogResult.Yes)
            return;

        SetBusy(true);
        _operationCts?.Dispose();
        _operationCts = new CancellationTokenSource(TimeSpan.FromSeconds(20));

        try
        {
            SetStatus($"Abrindo todas as {gates.Count} catraca(s) de {group.Name}...", null);

            var tasks = gates.Select(async gate =>
            {
                using var client = new DirectIsapiClient(gate);
                return await client.OpenAsync(_operationCts.Token);
            });

            var results = await Task.WhenAll(tasks);
            var failures = results.Where(x => !x.Success).ToList();

            foreach (var result in results)
            {
                SettingsService.Log(
                    $"DIRECT OPEN ALL {(result.Success ? "OK" : "FAIL")} group={group.Name} gate={result.GateName} host={result.Host} detail={result.Description}");
            }

            if (failures.Count == 0)
            {
                SetStatus($"TODAS ABERTAS: {group.Name} — {results.Length} catraca(s).", true);
                System.Media.SystemSounds.Asterisk.Play();
            }
            else
            {
                var detail = string.Join(
                    " | ",
                    failures.Select(x => $"{x.GateName} ({x.Host}): {x.Description}"));

                SetStatus(
                    $"{results.Length - failures.Count}/{results.Length} abriram em {group.Name}. Falhas: {detail}",
                    false);

                System.Media.SystemSounds.Exclamation.Play();
            }
        }
        catch (OperationCanceledException)
        {
            SetStatus($"Tempo esgotado ao abrir todas de {group.Name}.", false);
            SettingsService.Log($"DIRECT OPEN ALL TIMEOUT group={group.Name}");
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, false);
            SettingsService.Log($"DIRECT OPEN ALL ERROR group={group.Name} error={ex.Message}");
            System.Media.SystemSounds.Hand.Play();
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void GateButton_Click(object? sender, EventArgs e)
    {
        if (sender is not Button button || button.Tag is not ManagedGate gate)
            return;

        SetBusy(true);
        _operationCts?.Dispose();
        _operationCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        try
        {
            SetStatus($"Abrindo somente {gate.Name} ({gate.Host})...", null);

            using var client = new DirectIsapiClient(gate);
            var result = await client.OpenAsync(_operationCts.Token);

            SettingsService.Log(
                $"DIRECT OPEN {(result.Success ? "OK" : "FAIL")} group={gate.GroupName} gate={result.GateName} host={result.Host} detail={result.Description}");

            if (result.Success)
            {
                SetStatus($"ABERTURA ENVIADA: {gate.Name} ({gate.Host}).", true);
                System.Media.SystemSounds.Asterisk.Play();
            }
            else
            {
                SetStatus($"{gate.Name} ({gate.Host}): {result.Description}", false);
                System.Media.SystemSounds.Exclamation.Play();
            }
        }
        catch (OperationCanceledException)
        {
            SetStatus($"Tempo esgotado ao abrir {gate.Name}.", false);
            SettingsService.Log($"DIRECT OPEN TIMEOUT group={gate.GroupName} gate={gate.Name} host={gate.Host}");
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, false);
            SettingsService.Log(
                $"DIRECT OPEN ERROR group={gate.GroupName} gate={gate.Name} host={gate.Host} error={ex.Message}");
            System.Media.SystemSounds.Hand.Play();
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void OpenAdministration()
    {
        using var login = new AdminLoginForm(_settings);
        if (login.ShowDialog(this) != DialogResult.OK)
            return;

        using var admin = new AdminForm(_settings);
        admin.ShowDialog(this);

        _settings = SettingsService.Load();

        if (_selectedGroup is not null &&
            _settings.Groups.FirstOrDefault(x =>
                x.Name.Equals(_selectedGroup.Name, StringComparison.OrdinalIgnoreCase)) is { } currentGroup)
        {
            BuildGateCards(currentGroup);
        }
        else
        {
            BuildGroupCards();
        }

        SetStatus("Cadastro administrativo atualizado.", true);
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

            Process.Start(new ProcessStartInfo
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

    private void SetBusy(bool busy)
    {
        foreach (var button in _actionButtons)
            button.Enabled = !busy;

        _adminButton.Enabled = !busy;
        UseWaitCursor = busy;
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
