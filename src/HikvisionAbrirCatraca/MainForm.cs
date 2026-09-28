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
    private readonly List<Button> _gateButtons = [];
    private CancellationTokenSource? _operationCts;

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
            Text = "Conexão direta com as catracas Hikvision · sem HikCentral",
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
                ? "Pronto para operar."
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
            Text = "O computador fala diretamente com cada IP via ISAPI.",
            AutoSize = true,
            ForeColor = Color.FromArgb(98, 108, 121),
            Padding = new Padding(12, 7, 0, 0)
        });
        root.Controls.Add(footer, 0, 3);

        BuildGateCards();
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

    private void BuildGateCards()
    {
        _cards.SuspendLayout();
        _cards.Controls.Clear();
        _gateButtons.Clear();

        foreach (var group in _settings.Groups)
        {
            var gates = _settings.ManagedGates
                .Where(x => x.GroupName.Equals(group.Name, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var activeCount = gates.Count(x => x.Enabled);
            var referenceText = gates.Count == 0
                ? "Nenhuma catraca cadastrada"
                : $"{activeCount} ativa(s) de {gates.Count} cadastrada(s)";

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
                Height = 30,
                Location = new Point(20, 18),
                Font = new Font("Segoe UI Semibold", 12F, FontStyle.Bold),
                ForeColor = Color.FromArgb(55, 63, 74)
            });

            card.Controls.Add(new Label
            {
                Text = referenceText,
                AutoSize = false,
                Width = 400,
                Height = 24,
                Location = new Point(20, 49),
                ForeColor = Color.FromArgb(105, 114, 126)
            });

            var button = new Button
            {
                Text = string.IsNullOrWhiteSpace(group.ButtonText)
                    ? "ABRIR " + group.Name
                    : group.ButtonText,
                Width = 400,
                Height = 112,
                Location = new Point(20, 82),
                Font = new Font("Segoe UI Semibold", 17F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(220, 52, 56),
                ForeColor = Color.White,
                Cursor = Cursors.Hand,
                Tag = group,
                Enabled = activeCount > 0
            };
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(195, 40, 44);
            button.Click += GateButton_Click;

            card.Controls.Add(button);
            _cards.Controls.Add(card);
            _gateButtons.Add(button);
        }

        _cards.ResumeLayout();
    }

    private async void GateButton_Click(object? sender, EventArgs e)
    {
        if (sender is not Button button || button.Tag is not GateGroup group) return;

        var gates = _settings.ManagedGates
            .Where(x =>
                x.Enabled &&
                x.GroupName.Equals(group.Name, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (gates.Count == 0)
        {
            SetStatus($"Nenhuma catraca ativa cadastrada em {group.Name}.", false);
            return;
        }

        SetBusy(true);
        _operationCts?.Dispose();
        _operationCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        try
        {
            SetStatus($"Abrindo {gates.Count} catraca(s) diretamente em {group.Name}...", null);

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
                    $"DIRECT OPEN {(result.Success ? "OK" : "FAIL")} group={group.Name} gate={result.GateName} host={result.Host} detail={result.Description}");
            }

            if (failures.Count == 0)
            {
                SetStatus($"ABERTURA ENVIADA: {group.Name} — {results.Length} catraca(s).", true);
                System.Media.SystemSounds.Asterisk.Play();
            }
            else
            {
                var detail = string.Join(
                    " | ",
                    failures.Select(x => $"{x.GateName} ({x.Host}): {x.Description}"));

                SetStatus(
                    $"{results.Length - failures.Count}/{results.Length} abriram. Falhas: {detail}",
                    false);

                System.Media.SystemSounds.Exclamation.Play();
            }
        }
        catch (OperationCanceledException)
        {
            SetStatus("Operação cancelada ou tempo esgotado.", false);
            SettingsService.Log($"DIRECT OPEN TIMEOUT group={group.Name}");
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, false);
            SettingsService.Log($"DIRECT OPEN ERROR group={group.Name} error={ex.Message}");
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
        if (login.ShowDialog(this) != DialogResult.OK) return;

        using var admin = new AdminForm(_settings);
        admin.ShowDialog(this);

        _settings = SettingsService.Load();
        BuildGateCards();
        SetStatus("Cadastro administrativo atualizado.", true);
    }

    private void OpenLog()
    {
        try
        {
            Directory.CreateDirectory(SettingsService.DataDirectory);
            if (!File.Exists(SettingsService.LogPath))
                File.WriteAllText(
                    SettingsService.LogPath,
                    "Log de operações - Hikvision Abrir Catraca" + Environment.NewLine);

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
        foreach (var button in _gateButtons)
        {
            if (button.Tag is GateGroup group)
            {
                var hasActive = _settings.ManagedGates.Any(x =>
                    x.Enabled &&
                    x.GroupName.Equals(group.Name, StringComparison.OrdinalIgnoreCase));
                button.Enabled = !busy && hasActive;
            }
        }

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
