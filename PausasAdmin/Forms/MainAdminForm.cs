using System;
using System.Drawing;
using System.Windows.Forms;
using PausasCore.Models;
using PausasCore.Repositories;
using PausasAdmin.Theme;
using PausasAdmin.Views;

namespace PausasAdmin.Forms;

/// <summary>
/// Ventana principal de administración con barra lateral de navegación y panel de contenido dinámico.
/// </summary>
public class MainAdminForm : Form
{
    private readonly IAdminRepository _adminRepo;
    private readonly Admin _admin;

    private Panel _sidebar = null!;
    private Panel _mainPanel = null!;
    private Button _btnEmpleados = null!;
    private Button _btnImagenes = null!;
    private Button? _btnNavActivo;

    private UserControl? _vistaActual;

    public MainAdminForm(IAdminRepository adminRepo, Admin admin)
    {
        _adminRepo = adminRepo ?? throw new ArgumentNullException(nameof(adminRepo));
        _admin = admin ?? throw new ArgumentNullException(nameof(admin));
        InicializarComponentes();
    }

    private void InicializarComponentes()
    {
        Text = "Pausas Activas — Panel de Administración";
        Size = new Size(1280, 780);
        MinimumSize = new Size(1024, 600);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = AppTheme.MainBg;
        DoubleBuffered = true;

        // ========== SIDEBAR ==========
        _sidebar = new Panel
        {
            Dock = DockStyle.Left,
            Width = 220,
            BackColor = AppTheme.SidebarBg,
            Padding = new Padding(0)
        };

        // Logo / título app
        var pnlLogo = new Panel
        {
            Dock = DockStyle.Top,
            Height = 80,
            BackColor = Color.FromArgb(15, 23, 42), // #0F172A
            Padding = new Padding(16, 16, 16, 8)
        };

        var lblAppName = new Label
        {
            Text = "Pausas Activas",
            Font = new Font("Segoe UI", 13f, FontStyle.Bold),
            ForeColor = Color.White,
            AutoSize = true,
            Location = new Point(16, 14)
        };

        var lblAdminName = new Label
        {
            Text = $"{_admin.Nombre ?? _admin.Usuario}",
            Font = AppTheme.SmallFont,
            ForeColor = AppTheme.TextMuted,
            AutoSize = true,
            Location = new Point(16, 42)
        };

        pnlLogo.Controls.Add(lblAppName);
        pnlLogo.Controls.Add(lblAdminName);

        // Separador
        var sep1 = new Panel
        {
            Dock = DockStyle.Top,
            Height = 1,
            BackColor = Color.FromArgb(51, 65, 85) // Slate 700
        };

        // Espacio superior de navegación
        var pnlNavContainer = new Panel
        {
            Dock = DockStyle.Top,
            Height = 120,
            BackColor = AppTheme.SidebarBg,
            Padding = new Padding(8, 12, 8, 0)
        };

        _btnEmpleados = CrearBotonNav("👥  Empleados y Pausas", 0);
        _btnEmpleados.Click += (s, e) => NavegaA(_btnEmpleados, () => new EmpleadosView(_adminRepo));

        _btnImagenes = CrearBotonNav("🖼  Biblioteca de Imágenes", 1);
        _btnImagenes.Click += (s, e) => NavegaA(_btnImagenes, () => new ImagenesView(_adminRepo));

        pnlNavContainer.Controls.Add(_btnImagenes);
        pnlNavContainer.Controls.Add(_btnEmpleados);

        // Botón cerrar sesión (inferior)
        var pnlBottom = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 55,
            BackColor = AppTheme.SidebarBg,
            Padding = new Padding(8, 8, 8, 12)
        };

        var btnCerrarSesion = new Button
        {
            Text = "🚪  Cerrar sesión",
            Dock = DockStyle.Fill,
            FlatStyle = FlatStyle.Flat,
            ForeColor = AppTheme.TextMuted,
            Font = AppTheme.SmallFont,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 0, 0),
            Cursor = Cursors.Hand
        };
        btnCerrarSesion.FlatAppearance.BorderSize = 0;
        btnCerrarSesion.FlatAppearance.MouseOverBackColor = AppTheme.SidebarItemHover;
        btnCerrarSesion.Click += (s, e) =>
        {
            if (MessageBox.Show("¿Desea cerrar sesión?", "Confirmar",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                Close();
            }
        };
        pnlBottom.Controls.Add(btnCerrarSesion);

        _sidebar.Controls.Add(pnlNavContainer);
        _sidebar.Controls.Add(sep1);
        _sidebar.Controls.Add(pnlLogo);
        _sidebar.Controls.Add(pnlBottom);

        // ========== MAIN PANEL ==========
        _mainPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = AppTheme.MainBg,
            Padding = new Padding(0)
        };

        Controls.Add(_mainPanel);
        Controls.Add(_sidebar);

        // Cargar vista inicial de forma segura una vez que la ventana esté mostrada
        Shown += (s, e) =>
        {
            if (_vistaActual == null)
            {
                NavegaA(_btnEmpleados, () => new EmpleadosView(_adminRepo));
            }
        };
    }

    private Button CrearBotonNav(string texto, int indice)
    {
        var btn = new Button
        {
            Text = texto,
            Dock = DockStyle.Top,
            Height = 44,
            FlatStyle = FlatStyle.Flat,
            ForeColor = AppTheme.TextOnDark,
            Font = AppTheme.SmallBoldFont,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(12, 0, 0, 0),
            Cursor = Cursors.Hand
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.FlatAppearance.MouseOverBackColor = AppTheme.SidebarItemHover;
        return btn;
    }

    private void NavegaA(Button btn, Func<UserControl> factory)
    {
        // Resaltar botón activo en la sidebar
        if (_btnNavActivo != null)
        {
            _btnNavActivo.BackColor = AppTheme.SidebarBg;
        }

        _btnNavActivo = btn;
        btn.BackColor = AppTheme.SidebarItemActive;

        // Reemplazar vista
        if (_vistaActual != null)
        {
            _mainPanel.Controls.Remove(_vistaActual);
            _vistaActual.Dispose();
        }

        _vistaActual = factory();
        _vistaActual.Dock = DockStyle.Fill;
        _mainPanel.Controls.Add(_vistaActual);
    }
}
