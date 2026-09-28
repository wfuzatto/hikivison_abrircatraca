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
    private readonly Button _settingsButton;
    private readonly List<Button> _gateButtons = [];
    private CancellationTokenSource? _operationCts;

    public MainForm()
    {
        _settings = SettingsService.Load();

        Text = "AcquaVale - Abrir Catracas";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(860, 620);
        Size = new Size(980, 700);
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
        var title = new Label
        {
            Text = "ABERTURA DE CATRACAS",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 22F, FontStyle.Bold),
            ForeColor = Color.FromArgb(35, 42, 52),
            Location = new Point(0, 0)
        };
        var subtitle = new Label
        {
            Text = "Operação rápida via HikCentral Professional",
            AutoSize = true,
            ForeColor = Color.FromArgb(98, 108, 121),
            Location = new Point(3, 45)
        };
        heading.Controls.Add(title);
        heading.Controls.Add(subtitle);

        _settingsButton = new Button
        {
            Text = "⚙ Configurações",
            AutoSize = true,
            Height = 42,
            Padding = new Padding(12, 0, 12, 0),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            Cursor = Cursors.Hand
        };
        _settingsButton.FlatAppearance.BorderColor = Color.FromArgb(210, 215, 223);
        _settingsButton.Click += (_, _) => OpenSettings();

        header.Controls.Add(heading, 0, 0);
        header.Controls.Add(_settingsButton, 1, 0);
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
            Text = _settings.IsConfigured ? "Pronto para operar." : "Configure a OpenAPI do HikCentral antes do primeiro uso.",
            Dock = DockStyle.Top,
            AutoSize = false,
            Height = 46,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(14, 0, 14, 0),
            Font = new Font("Segoe UI Semibold", 10.5F),
            BackColor = Color.White,
            ForeColor = _settings.IsConfigured ? Color.FromArgb(34, 94, 58) : Color.FromArgb(156, 97, 20),
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
            Text = "Um clique abre somente as catracas do grupo selecionado.",
            AutoSize = true,
            ForeColor = Color.FromArgb(98, 108, 121),
            Padding = new Padding(12, 7, 0, 0)
        });
        root.Controls.Add(footer, 0, 3);

        BuildGateCards();
        FormClosing += (_, _) => _operationCts?.Cancel();
    }

    private void BuildGateCards()
    {
        _cards.SuspendLayout();
        _cards.Controls.Clear();
        _gateButtons.Clear();

        foreach (var group in _settings.Groups)
        {
            var card = new Panel
            {
                Width = 430,
                Height = 215,
                Margin = new Padding(0, 0, 18, 18),
                BackColor = Color.White
            };

            var name = new Label
            {
                Text = group.Name,
                AutoSize = false,
                Width = 390,
                Height = 30,
                Location = new Point(20, 18),
                Font = new Font("Segoe UI Semibold", 12F, FontStyle.Bold),
                ForeColor = Color.FromArgb(55, 63, 74)
            };

            var reference = new Label
            {
                Text = "Referência: " + group.Reference,
                AutoSize = false,
                Width = 390,
                Height = 24,
                Location = new Point(20, 49),
                ForeColor = Color.FromArgb(105, 114, 126)
            };

            var button = new Button
            {
                Text = string.IsNullOrWhiteSpace(group.ButtonText) ? "ABRIR " + group.Name : group.ButtonText,
                Width = 390,
                Height = 112,
                Location = new Point(20, 82),
                Font = new Font("Segoe UI Semibold", 17F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(220, 52, 56),
                ForeColor = Color.White,
                Cursor = Cursors.Hand,
                Tag = group
            };
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(195, 40, 44);
            button.Click += GateButton_Click;

            card.Controls.Add(name);
            card.Controls.Add(reference);
            card.Controls.Add(button);
            _cards.Controls.Add(card);
            _gateButtons.Add(button);
        }

        _cards.ResumeLayout();
    }

    private async void GateButton_Click(object? sender, EventArgs e)
    {
        if (sender is not Button button || button.Tag is not GateGroup group) return;

        if (!_settings.IsConfigured)
        {
            SetStatus("Configure URL, AppKey e AppSecret antes de abrir as catracas.", false);
            OpenSettings();
            return;
        }

        SetBusy(true);
        _operationCts?.Dispose();
        _operationCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        try
        {
            SetStatus($"Localizando as catracas de {group.Name}...", null);
            using var client = new HikCentralClient(_settings);
            var doors = await client.GetDoorsForGroupAsync(group, _operationCts.Token);

            SetStatus($"Enviando abertura para {doors.Count} catraca(s) de {group.Name}...", null);
            var result = await client.OpenDoorsAsync(doors, group.ControlDirection, _operationCts.Token);

            var failures = result.Where(x => !x.Success).ToArray();
            var doorNames = string.Join(", ", doors.Select(x => x.Name));

            if (failures.Length == 0)
            {
                SetStatus($"ABERTURA ENVIADA: {group.Name} — {doors.Count} catraca(s).", true);
                SettingsService.Log($"OPEN OK group={group.Name} direction={group.ControlDirection} doors={string.Join(",", doors.Select(x => x.Id))} names={doorNames}");
                System.Media.SystemSounds.Asterisk.Play();
            }
            else
            {
                var detail = string.Join("; ", failures.Select(x => $"{x.DoorId}: {x.Description}"));
                SetStatus($"HikCentral retornou falha em {failures.Length} catraca(s): {detail}", false);
                SettingsService.Log($"OPEN PARTIAL group={group.Name} failures={detail}");
                System.Media.SystemSounds.Exclamation.Play();
            }
        }
        catch (OperationCanceledException)
        {
            SetStatus("Operação cancelada ou tempo esgotado.", false);
            SettingsService.Log($"OPEN TIMEOUT group={group.Name}");
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, false);
            SettingsService.Log($"OPEN ERROR group={group.Name} error={ex.Message}");
            System.Media.SystemSounds.Hand.Play();
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void OpenSettings()
    {
        using var form = new SettingsForm(_settings);
        if (form.ShowDialog(this) != DialogResult.OK) return;

        _settings = form.Settings;
        SettingsService.Save(_settings);
        BuildGateCards();
        SetStatus("Configurações salvas. Pronto para operar.", true);
    }

    private void OpenLog()
    {
        try
        {
            Directory.CreateDirectory(SettingsService.DataDirectory);
            if (!File.Exists(SettingsService.LogPath))
                File.WriteAllText(SettingsService.LogPath, "Log de operações - Hikvision Abrir Catraca" + Environment.NewLine);

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
        foreach (var button in _gateButtons) button.Enabled = !busy;
        _settingsButton.Enabled = !busy;
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
