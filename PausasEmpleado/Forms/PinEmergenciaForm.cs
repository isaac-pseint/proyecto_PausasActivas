using System;
using System.Drawing;
using System.Windows.Forms;

namespace PausasEmpleado.Forms;

/// <summary>
/// Diálogo modal estilizado para solicitar el PIN de desbloqueo de emergencia corporativo.
/// </summary>
public class PinEmergenciaForm : Form
{
    private readonly string _pinCorrecto;
    private TextBox _txtPin = null!;
    private Label _lblError = null!;
    private Button _btnDesbloquear = null!;
    private Button _btnCancelar = null!;

    public bool DesbloqueoExitoso { get; private set; }

    public PinEmergenciaForm(string pinCorrecto)
    {
        _pinCorrecto = string.IsNullOrEmpty(pinCorrecto) ? "1234" : pinCorrecto;
        InicializarComponentes();
    }

    private void InicializarComponentes()
    {
        Text = "Seguridad - Desbloqueo de Emergencia";
        Size = new Size(420, 260);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.FromArgb(15, 23, 42); // #0F172A

        var pnlContenedor = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24)
        };

        var lblTitulo = new Label
        {
            Text = "DESBLOQUEO DE EMERGENCIA",
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            ForeColor = Color.FromArgb(248, 250, 252),
            Location = new Point(24, 20),
            AutoSize = true
        };

        var lblInstruccion = new Label
        {
            Text = "Ingrese el PIN de administrador corporativo para suspender la pausa activa inmediatamente:",
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
            ForeColor = Color.FromArgb(148, 163, 184),
            Location = new Point(24, 52),
            Size = new Size(355, 40)
        };

        _txtPin = new TextBox
        {
            PasswordChar = '●',
            Font = new Font("Segoe UI", 14f, FontStyle.Bold),
            TextAlign = HorizontalAlignment.Center,
            BackColor = Color.FromArgb(30, 41, 59),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Location = new Point(24, 98),
            Size = new Size(355, 36)
        };
        _txtPin.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                ValidarPin();
            }
        };

        _lblError = new Label
        {
            Text = "PIN incorrecto. Intente de nuevo.",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
            ForeColor = Color.FromArgb(248, 113, 113), // Red 400
            Location = new Point(24, 140),
            AutoSize = true,
            Visible = false
        };

        _btnDesbloquear = new Button
        {
            Text = "Desbloquear",
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            BackColor = Color.FromArgb(37, 99, 235), // Blue 600
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Location = new Point(160, 165),
            Size = new Size(110, 36),
            Cursor = Cursors.Hand
        };
        _btnDesbloquear.FlatAppearance.BorderSize = 0;
        _btnDesbloquear.Click += (s, e) => ValidarPin();

        _btnCancelar = new Button
        {
            Text = "Cancelar",
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
            BackColor = Color.FromArgb(51, 65, 85), // Slate 700
            ForeColor = Color.FromArgb(226, 232, 240),
            FlatStyle = FlatStyle.Flat,
            Location = new Point(278, 165),
            Size = new Size(100, 36),
            Cursor = Cursors.Hand
        };
        _btnCancelar.FlatAppearance.BorderSize = 0;
        _btnCancelar.Click += (s, e) =>
        {
            DesbloqueoExitoso = false;
            DialogResult = DialogResult.Cancel;
            Close();
        };

        pnlContenedor.Controls.Add(lblTitulo);
        pnlContenedor.Controls.Add(lblInstruccion);
        pnlContenedor.Controls.Add(_txtPin);
        pnlContenedor.Controls.Add(_lblError);
        pnlContenedor.Controls.Add(_btnDesbloquear);
        pnlContenedor.Controls.Add(_btnCancelar);

        Controls.Add(pnlContenedor);
        AcceptButton = _btnDesbloquear;
        CancelButton = _btnCancelar;
    }

    private void ValidarPin()
    {
        if (string.Equals(_txtPin.Text.Trim(), _pinCorrecto, StringComparison.Ordinal))
        {
            DesbloqueoExitoso = true;
            DialogResult = DialogResult.OK;
            Close();
        }
        else
        {
            _lblError.Visible = true;
            _txtPin.SelectAll();
            _txtPin.Focus();
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _txtPin.Focus();
    }
}
