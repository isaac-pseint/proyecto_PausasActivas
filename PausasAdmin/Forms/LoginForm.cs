using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using PausasCore.Models;
using PausasCore.Repositories;
using PausasAdmin.Theme;

namespace PausasAdmin.Forms;

/// <summary>
/// Pantalla de inicio de sesión con diseño limpio y moderno.
/// </summary>
public class LoginForm : Form
{
    private readonly IAdminRepository _adminRepo;

    private TextBox _txtUsuario = null!;
    private TextBox _txtClave = null!;
    private CheckBox _chkMostrarClave = null!;
    private Button _btnLogin = null!;
    private Label _lblError = null!;

    public Admin? AdminAutenticado { get; private set; }

    public LoginForm(IAdminRepository adminRepo)
    {
        _adminRepo = adminRepo ?? throw new ArgumentNullException(nameof(adminRepo));
        InicializarComponentes();
    }

    private void InicializarComponentes()
    {
        Text = "Pausas Activas — Administración";
        Size = new Size(460, 420);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = AppTheme.MainBg;
        DoubleBuffered = true;

        // ---- Tarjeta central ----
        var card = new Panel
        {
            Size = new Size(380, 330),
            Location = new Point(30, 30),
            BackColor = AppTheme.CardBg,
            Padding = new Padding(32, 24, 32, 24)
        };
        card.Paint += (s, e) =>
        {
            using var pen = new Pen(AppTheme.CardBorder, 1);
            e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
        };

        // ---- Logo / Título ----
        var lblTag = new Label
        {
            Text = "PAUSAS ACTIVAS",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = AppTheme.Accent,
            Location = new Point(32, 24),
            AutoSize = true
        };

        var lblTitle = new Label
        {
            Text = "Iniciar sesión",
            Font = AppTheme.TitleFont,
            ForeColor = AppTheme.TextPrimary,
            Location = new Point(32, 46),
            AutoSize = true
        };

        var lblSub = new Label
        {
            Text = "Panel de Administración Corporativa",
            Font = AppTheme.SmallFont,
            ForeColor = AppTheme.TextSecondary,
            Location = new Point(32, 76),
            AutoSize = true
        };

        // ---- Campo Usuario ----
        var lblUsuario = new Label
        {
            Text = "Usuario",
            Font = AppTheme.SmallBoldFont,
            ForeColor = AppTheme.TextPrimary,
            Location = new Point(32, 110),
            AutoSize = true
        };

        _txtUsuario = new TextBox
        {
            Font = AppTheme.BodyFont,
            BackColor = AppTheme.InputBg,
            BorderStyle = BorderStyle.FixedSingle,
            Location = new Point(32, 130),
            Size = new Size(316, 30)
        };

        // ---- Campo Contraseña ----
        var lblClave = new Label
        {
            Text = "Contraseña",
            Font = AppTheme.SmallBoldFont,
            ForeColor = AppTheme.TextPrimary,
            Location = new Point(32, 168),
            AutoSize = true
        };

        _txtClave = new TextBox
        {
            Font = AppTheme.BodyFont,
            BackColor = AppTheme.InputBg,
            BorderStyle = BorderStyle.FixedSingle,
            Location = new Point(32, 188),
            Size = new Size(316, 30),
            PasswordChar = '●'
        };

        _chkMostrarClave = new CheckBox
        {
            Text = "Mostrar contraseña",
            Font = AppTheme.SmallFont,
            ForeColor = AppTheme.TextSecondary,
            Location = new Point(32, 220),
            AutoSize = true
        };
        _chkMostrarClave.CheckedChanged += (s, e) =>
        {
            _txtClave.PasswordChar = _chkMostrarClave.Checked ? '\0' : '●';
        };

        // ---- Error ----
        _lblError = new Label
        {
            Text = "",
            Font = AppTheme.SmallFont,
            ForeColor = AppTheme.Danger,
            Location = new Point(32, 248),
            Size = new Size(316, 20),
            Visible = false
        };

        // ---- Botón Login ----
        _btnLogin = new Button
        {
            Text = "Iniciar sesión",
            Size = new Size(316, 40),
            Location = new Point(32, 272)
        };
        AppTheme.EstilizarBotonPrimario(_btnLogin);
        _btnLogin.Click += async (s, e) => await ValidarLoginAsync();

        // ---- Ensamblar ----
        card.Controls.AddRange(new Control[]
        {
            lblTag, lblTitle, lblSub,
            lblUsuario, _txtUsuario,
            lblClave, _txtClave, _chkMostrarClave,
            _lblError,
            _btnLogin
        });

        Controls.Add(card);
        AcceptButton = _btnLogin;
        ActiveControl = _txtUsuario;
    }

    private async Task ValidarLoginAsync()
    {
        string usuario = _txtUsuario.Text.Trim();
        string clave = _txtClave.Text;

        if (string.IsNullOrWhiteSpace(usuario) || string.IsNullOrWhiteSpace(clave))
        {
            MostrarError("Ingrese usuario y contraseña.");
            return;
        }

        _btnLogin.Enabled = false;
        _btnLogin.Text = "Validando...";
        _lblError.Visible = false;

        try
        {
            var admin = await _adminRepo.ValidarLoginAsync(usuario, clave);
            if (admin != null)
            {
                AdminAutenticado = admin;
                DialogResult = DialogResult.OK;
                Close();
            }
            else
            {
                MostrarError("Usuario o contraseña incorrectos.");
            }
        }
        catch (Exception ex)
        {
            MostrarError($"Error de conexión: {ex.Message}");
        }
        finally
        {
            _btnLogin.Enabled = true;
            _btnLogin.Text = "Iniciar sesión";
        }
    }

    private void MostrarError(string mensaje)
    {
        _lblError.Text = mensaje;
        _lblError.Visible = true;
        _txtClave.SelectAll();
        _txtClave.Focus();
    }
}
