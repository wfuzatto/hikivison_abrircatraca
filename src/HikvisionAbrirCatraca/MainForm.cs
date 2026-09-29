using System.Diagnostics;
using System.Drawing;
using HikvisionAbrirCatraca.Models;
using HikvisionAbrirCatraca.Services;
using HikvisionAbrirCatraca.Ui;

namespace HikvisionAbrirCatraca;

public sealed class MainForm : Form
{
    private AppSettings _settings;
    private readonly StatusBanner _status;
    private readonly FlowLayoutPanel _cards;
    private readonly RoundedButton _adminButton;
    private readonly List<Button> _actionButtons = [];
    private CancellationTokenSource? _operationCts;
    private GateGroup? _selectedGroup;

    public MainForm()
    {
        _settings = SettingsService.Load();

        Text = "AcquaVale - Abrir Catracas";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(980, 700);
        ClientSize = new Size(1010, 760);
        BackColor = AppTheme.Background;
        Font = AppTheme.Font(10F);
        AutoScaleMode = AutoScaleMode.Dpi;
        DoubleBuffered = true;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(24, 22, 24, 18),
            BackColor = AppTheme.Background
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        Controls.Add(root);

        var header = BuildHeader();
        root.Controls.Add(header, 0, 0);

        _cards = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(0, 4, 0, 0),
            Margin = new Padding(0),
            BackColor = AppTheme.Background
        };
        root.Controls.Add(_cards, 0, 1);

        _status = new StatusBanner
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 9, 0, 5),
            Message = _settings.ManagedGates.Count > 0
                ? "Selecione o bloco."
                : "Nenhuma catraca cadastrada. Entre em Administração para configurar os IPs.",
            Success = _settings.ManagedGates.Count > 0 ? null : false
        };
        root.Controls.Add(_status, 0, 2);

        root.Controls.Add(BuildFooter(), 0, 3);

        BuildGroupCards();
        FormClosing += (_, _) => _operationCts?.Cancel();
    }

    private Control BuildHeader()
    {
        var header = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = AppTheme.Background,
            Margin = new Padding(0)
        };

        var icon = new TurnstileIcon
        {
            Variant = TurnstileIconVariant.Header,
            Location = new Point(0, 7),
            Size = new Size(74, 68)
        };

        var title = new Label
        {
            Text = "ABERTURA DE CATRACAS",
            AutoSize = true,
            Font = AppTheme.SemiBold(23F),
            ForeColor = AppTheme.Navy,
            Location = new Point(86, 11),
            BackColor = Color.Transparent
        };

        var subtitle = new Label
        {
            Text = "Selecione o bloco e depois a catraca que deseja abrir",
            AutoSize = true,
            Font = AppTheme.Font(11.5F),
            ForeColor = AppTheme.Muted,
            Location = new Point(88, 53),
            BackColor = Color.Transparent
        };

        _adminButton = new RoundedButton
        {
            Text = "🔒  Administração",
            Width = 172,
            Height = 48,
            CornerRadius = 11,
            FillColor = Color.FromArgb(239, 245, 252),
            HoverColor = Color.FromArgb(226, 237, 249),
            ForeColor = AppTheme.Navy,
            BorderColor = Color.FromArgb(203, 218, 236),
            BorderSize = 1,
            Font = AppTheme.SemiBold(10.5F),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(ClientSize.Width - 24 - 172 - 24, 13)
        };
        _adminButton.Click += (_, _) => OpenAdministration();

        header.Resize += (_, _) =>
        {
            _adminButton.Left = Math.Max(650, header.ClientSize.Width - _adminButton.Width);
        };

        header.Controls.Add(icon);
        header.Controls.Add(title);
        header.Controls.Add(subtitle);
        header.Controls.Add(_adminButton);
        return header;
    }

    private Control BuildFooter()
    {
        var footer = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = AppTheme.Background,
            Margin = new Padding(0)
        };

        var info = new InfoIcon { Location = new Point(3, 8) };
        var note = new Label
        {
            Text = "Abra uma catraca individualmente ou use ABRIR TODAS dentro do bloco selecionado.",
            AutoSize = true,
            Font = AppTheme.Font(9.6F),
            ForeColor = AppTheme.Muted,
            Location = new Point(38, 10),
            BackColor = Color.Transparent
        };

        var logButton = new LinkLabel
        {
            Text = "Abrir log",
            AutoSize = true,
            Font = AppTheme.SemiBold(9.3F),
            LinkColor = AppTheme.Muted,
            ActiveLinkColor = AppTheme.Navy,
            VisitedLinkColor = AppTheme.Muted,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(870, 10)
        };
        logButton.LinkClicked += (_, _) => OpenLog();

        footer.Resize += (_, _) =>
        {
            logButton.Left = Math.Max(760, footer.ClientSize.Width - logButton.Width - 2);
        };

        footer.Controls.Add(info);
        footer.Controls.Add(note);
        footer.Controls.Add(logButton);
        return footer;
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

            _cards.Controls.Add(BuildGroupCard(group, gates.Count));
        }

        _cards.ResumeLayout();
        SetStatus("Selecione o bloco.", null);
    }

    private Control BuildGroupCard(GateGroup group, int available)
    {
        var card = new CardPanel
        {
            Width = 456,
            Height = 224,
            Margin = new Padding(0, 0, 16, 16),
            CornerRadius = 17
        };

        var variant = group.Name switch
        {
            "ENTRADA ACQUAVALE" => TurnstileIconVariant.Entry,
            "SAIDA ACQUAVALE" => TurnstileIconVariant.Exit,
            "LOJA ACQUAVALE" => TurnstileIconVariant.Shop,
            _ => TurnstileIconVariant.Generic
        };

        card.Controls.Add(new TurnstileIcon
        {
            Variant = variant,
            Location = new Point(20, 22),
            Size = new Size(100, 100)
        });

        card.Controls.Add(new Label
        {
            Text = group.Name,
            AutoSize = false,
            Width = 300,
            Height = 31,
            Location = new Point(140, 31),
            Font = AppTheme.SemiBold(13.2F),
            ForeColor = AppTheme.Navy,
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleLeft
        });

        var count = new Label
        {
            Text = available.ToString(),
            AutoSize = true,
            Location = new Point(140, 73),
            Font = AppTheme.SemiBold(16F),
            ForeColor = available > 0 ? AppTheme.Green : Color.FromArgb(171, 80, 80),
            BackColor = Color.Transparent
        };

        var availability = new Label
        {
            Text = available > 0 ? "catraca(s) disponível(is)" : "nenhuma catraca disponível",
            AutoSize = true,
            Location = new Point(164, 78),
            Font = AppTheme.Font(10.8F),
            ForeColor = AppTheme.Muted,
            BackColor = Color.Transparent
        };

        if (available == 0)
            availability.Left = 140;

        var select = new RoundedButton
        {
            Text = "›   SELECIONAR BLOCO",
            Width = 412,
            Height = 56,
            Location = new Point(20, 145),
            CornerRadius = 9,
            FillColor = AppTheme.Blue,
            HoverColor = AppTheme.BlueHover,
            Font = AppTheme.SemiBold(11.2F),
            Enabled = available > 0,
            Tag = group
        };
        select.Click += (_, _) =>
        {
            if (select.Tag is GateGroup selected)
                BuildGateCards(selected);
        };

        card.Controls.Add(count);
        card.Controls.Add(availability);
        card.Controls.Add(select);
        _actionButtons.Add(select);

        return card;
    }

    private void BuildGateCards(GateGroup group)
    {
        _selectedGroup = group;
        _cards.SuspendLayout();
        _cards.Controls.Clear();
        _actionButtons.Clear();

        var gates = _settings.ManagedGates
            .Where(x =>
                x.Enabled &&
                x.GroupName.Equals(group.Name, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Name)
            .ToList();

        _cards.Controls.Add(BuildGroupTopBar(group, gates));

        foreach (var gate in gates)
            _cards.Controls.Add(BuildGateCard(gate));

        _cards.ResumeLayout();
        SetStatus($"{group.Name}: escolha qual catraca deseja abrir.", true);
    }

    private Control BuildGroupTopBar(GateGroup group, IReadOnlyList<ManagedGate> gates)
    {
        var top = new Panel
        {
            Width = 930,
            Height = 72,
            Margin = new Padding(0, 0, 16, 14),
            BackColor = AppTheme.Background
        };

        var back = new RoundedButton
        {
            Text = "←  Voltar aos blocos",
            Width = 165,
            Height = 43,
            Location = new Point(0, 9),
            CornerRadius = 9,
            FillColor = Color.White,
            HoverColor = Color.FromArgb(242, 246, 251),
            ForeColor = AppTheme.Navy,
            BorderColor = Color.FromArgb(203, 215, 229),
            BorderSize = 1,
            Font = AppTheme.SemiBold(9.6F)
        };
        back.Click += (_, _) => BuildGroupCards();

        var title = new Label
        {
            Text = group.Name,
            AutoSize = false,
            Width = 450,
            Height = 44,
            Location = new Point(190, 9),
            Font = AppTheme.SemiBold(18F),
            ForeColor = AppTheme.Navy,
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleLeft
        };

        var openAll = new RoundedButton
        {
            Text = "▣  ABRIR TODAS",
            Width = 168,
            Height = 50,
            Location = new Point(750, 5),
            CornerRadius = 10,
            FillColor = AppTheme.Red,
            HoverColor = AppTheme.RedHover,
            Font = AppTheme.SemiBold(10.5F),
            Enabled = gates.Count > 0,
            Tag = group
        };
        openAll.Click += async (_, _) => await OpenAllGroupAsync(group);

        top.Controls.Add(back);
        top.Controls.Add(title);
        top.Controls.Add(openAll);

        _actionButtons.Add(back);
        _actionButtons.Add(openAll);
        return top;
    }

    private Control BuildGateCard(ManagedGate gate)
    {
        var card = new CardPanel
        {
            Width = 456,
            Height = 205,
            Margin = new Padding(0, 0, 16, 16),
            CornerRadius = 17
        };

        card.Controls.Add(new TurnstileIcon
        {
            Variant = TurnstileIconVariant.Generic,
            Location = new Point(20, 20),
            Size = new Size(72, 72)
        });

        card.Controls.Add(new Label
        {
            Text = gate.Name,
            AutoSize = false,
            Width = 330,
            Height = 30,
            Location = new Point(106, 24),
            Font = AppTheme.SemiBold(12F),
            ForeColor = AppTheme.Navy,
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleLeft
        });

        card.Controls.Add(new Label
        {
            Text = $"{gate.Host}:{gate.Port}  ·  Door {gate.DoorNo}",
            AutoSize = false,
            Width = 330,
            Height = 26,
            Location = new Point(106, 55),
            Font = AppTheme.Font(9.7F),
            ForeColor = AppTheme.Muted,
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleLeft
        });

        var open = new RoundedButton
        {
            Text = "⌑   ABRIR ESTA CATRACA",
            Width = 414,
            Height = 64,
            Location = new Point(20, 116),
            CornerRadius = 9,
            FillColor = AppTheme.Red,
            HoverColor = AppTheme.RedHover,
            Font = AppTheme.SemiBold(11.3F),
            Tag = gate
        };
        open.Click += GateButton_Click;

        card.Controls.Add(open);
        _actionButtons.Add(open);
        return card;
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
        _status.Message = text;
        _status.Success = success;
    }
}
