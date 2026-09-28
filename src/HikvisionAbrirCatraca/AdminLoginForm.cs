using System.Drawing;
using HikvisionAbrirCatraca.Models;
using HikvisionAbrirCatraca.Services;

namespace HikvisionAbrirCatraca;

public sealed class AdminLoginForm : Form
{
    private readonly AppSettings _settings;
    private readonly TextBox _password = new();
    private readonly TextBox _confirm = new();
    private readonly Label _message = new();

    public bool PasswordWasCreated { get; private set; }

    public AdminLoginForm(AppSettings settings)
    {
        _settings = settings;

        Text = settings.HasAdminPassword ? "Acesso administrativo" : "Criar senha administrativa";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(430, settings.HasAdminPassword ? 225 : 285);
        Font = new Font("Segoe UI", 10F);
        BackColor = Color.White;

        var title = new Label
        {
            Text = settings.HasAdminPassword ? "ADMINISTRAÇÃO" : "PRIMEIRO ACESSO ADMINISTRATIVO",
            Location = new Point(24, 22),
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 15F, FontStyle.Bold)
        };
        Controls.Add(title);

        var description = new Label
        {
            Text = settings.HasAdminPassword
                ? "Digite a senha para alterar o cadastro das catracas."
                : "Defina uma senha para proteger cadastro, edição e exclusão das catracas.",
            Location = new Point(27, 58),
            MaximumSize = new Size(370, 0),
            AutoSize = true,
            ForeColor = Color.DimGray
        };
        Controls.Add(description);

        Controls.Add(new Label { Text = "Senha", Location = new Point(27, 100), AutoSize = true });
        _password.Location = new Point(27, 123);
        _password.Width = 370;
        _password.UseSystemPasswordChar = true;
        Controls.Add(_password);

        var y = 163;
        if (!settings.HasAdminPassword)
        {
            Controls.Add(new Label { Text = "Confirmar senha", Location = new Point(27, 162), AutoSize = true });
            _confirm.Location = new Point(27, 185);
            _confirm.Width = 370;
            _confirm.UseSystemPasswordChar = true;
            Controls.Add(_confirm);
            y = 225;
        }

        _message.Location = new Point(27, y - 27);
        _message.AutoSize = true;
        _message.ForeColor = Color.Firebrick;
        Controls.Add(_message);

        var enter = new Button
        {
            Text = settings.HasAdminPassword ? "Entrar" : "Criar senha",
            Location = new Point(291, y),
            Width = 106,
            Height = 36,
            BackColor = Color.FromArgb(220, 52, 56),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        enter.FlatAppearance.BorderSize = 0;
        enter.Click += (_, _) => Submit();
        Controls.Add(enter);

        var cancel = new Button
        {
            Text = "Cancelar",
            Location = new Point(183, y),
            Width = 100,
            Height = 36
        };
        cancel.Click += (_, _) => DialogResult = DialogResult.Cancel;
        Controls.Add(cancel);

        AcceptButton = enter;
        CancelButton = cancel;
    }

    private void Submit()
    {
        if (!_settings.HasAdminPassword)
        {
            if (_password.Text.Length < 4)
            {
                _message.Text = "Use uma senha com pelo menos 4 caracteres.";
                return;
            }
            if (_password.Text != _confirm.Text)
            {
                _message.Text = "As senhas não conferem.";
                return;
            }

            AdminSecurity.SetPassword(_settings, _password.Text);
            SettingsService.Save(_settings);
            SettingsService.Log("ADMIN PASSWORD CREATED");
            PasswordWasCreated = true;
            DialogResult = DialogResult.OK;
            return;
        }

        if (!AdminSecurity.Verify(_settings, _password.Text))
        {
            _message.Text = "Senha incorreta.";
            _password.SelectAll();
            _password.Focus();
            SettingsService.Log("ADMIN LOGIN FAILED");
            return;
        }

        SettingsService.Log("ADMIN LOGIN OK");
        DialogResult = DialogResult.OK;
    }
}
