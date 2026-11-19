using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace PausasEmpleado.Forms;

/// <summary>
/// Ventana a pantalla completa sin bordes para la reproducción de la pausa activa en un monitor.
/// </summary>
public class VentanaPausaForm : Form
{
    private readonly int _duracionTotalSeg;
    private readonly Action _onEmergenciaSolicitada;
    private Image? _imagenCargada;
    private bool _puedeCerrar;

    // Componentes UI
    private Label _lblNombreImagen = null!;
    private Label _lblCategoria = null!;
    private PictureBox _picImagen = null!;
    private Label _lblContador = null!;
    private Panel _pnlProgresoFondo = null!;
    private float _porcentajeProgreso;

    public VentanaPausaForm(
        Screen screen,
        byte[]? imagenBytes,
        string nombreImagen,
        string? categoriaImagen,
        int duracionTotalSeg,
        Action onEmergenciaSolicitada)
    {
        ArgumentNullException.ThrowIfNull(screen);
        _duracionTotalSeg = duracionTotalSeg > 0 ? duracionTotalSeg : 120;
        _onEmergenciaSolicitada = onEmergenciaSolicitada;

        ConfigurarVentanaMultiMonitor(screen);
        InicializarDiseno(nombreImagen, categoriaImagen, imagenBytes);
    }

    private void ConfigurarVentanaMultiMonitor(Screen screen)
    {
        // Requisitos indispensables para posicionamiento exacto multi-monitor
        StartPosition = FormStartPosition.Manual;
        Bounds = screen.Bounds;
        FormBorderStyle = FormBorderStyle.None;
        WindowState = FormWindowState.Normal;
        TopMost = true;
        ShowInTaskbar = false;
        KeyPreview = true;
        BackColor = Color.FromArgb(15, 23, 42); // #0F172A (Fondo corporativo oscuro)
    }

    private void InicializarDiseno(string nombreImagen, string? categoriaImagen, byte[]? imagenBytes)
    {
        // 1. Panel de Encabezado Superior (Superior)
        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 85,
            BackColor = Color.FromArgb(15, 23, 42),
            Padding = new Padding(30, 15, 30, 5)
        };

        var lblTag = new Label
        {
            Text = "PAUSA ACTIVA CORPORATIVA",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(56, 189, 248), // Sky 400
            AutoSize = true,
            Location = new Point(30, 12)
        };

        _lblNombreImagen = new Label
        {
            Text = !string.IsNullOrWhiteSpace(nombreImagen) ? nombreImagen : "Momento de pausa activa",
            Font = new Font("Segoe UI", 16f, FontStyle.Bold),
            ForeColor = Color.FromArgb(248, 250, 252), // Slate 50
            AutoSize = true,
            Location = new Point(30, 32)
        };

        _lblCategoria = new Label
        {
            Text = !string.IsNullOrWhiteSpace(categoriaImagen) ? $"Categoría: {categoriaImagen}" : "Bienestar y Ergonomía",
            Font = new Font("Segoe UI", 10f, FontStyle.Regular),
            ForeColor = Color.FromArgb(148, 163, 184), // Slate 400
            AutoSize = true,
            Location = new Point(30, 60)
        };

        pnlHeader.Controls.Add(lblTag);
        pnlHeader.Controls.Add(_lblNombreImagen);
        pnlHeader.Controls.Add(_lblCategoria);

        // 2. Panel Inferior (Pie de pantalla con contador y barra)
        var pnlFooter = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 110,
            BackColor = Color.FromArgb(15, 23, 42),
            Padding = new Padding(40, 10, 40, 15)
        };

        _lblContador = new Label
        {
            Text = TimeSpan.FromSeconds(_duracionTotalSeg).ToString(@"mm\:ss"),
            Font = new Font("Segoe UI", 32f, FontStyle.Bold),
            ForeColor = Color.FromArgb(56, 189, 248),
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Top,
            Height = 55
        };

        _pnlProgresoFondo = new PanelDoubleBuffered
        {
            Dock = DockStyle.Top,
            Height = 8,
            BackColor = Color.FromArgb(30, 41, 59) // Slate 800
        };
        _pnlProgresoFondo.Paint += PnlProgresoFondo_Paint;

        var lblSlogan = new Label
        {
            Text = "Tómate un respiro, estira el cuerpo y relaja la vista antes de continuar.",
            Font = new Font("Segoe UI", 9.5f, FontStyle.Italic),
            ForeColor = Color.FromArgb(100, 116, 139), // Slate 500
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Bottom,
            Height = 25
        };

        pnlFooter.Controls.Add(lblSlogan);
        pnlFooter.Controls.Add(_pnlProgresoFondo);
        pnlFooter.Controls.Add(_lblContador);

        // 3. Área Central (Imagen)
        var pnlCentro = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(40, 10, 40, 10),
            BackColor = Color.FromArgb(15, 23, 42)
        };

        _picImagen = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent
        };

        CargarImagenEnPictureBox(imagenBytes);
        pnlCentro.Controls.Add(_picImagen);

        // Ensamblar
        Controls.Add(pnlCentro);
        Controls.Add(pnlFooter);
        Controls.Add(pnlHeader);
    }

    private void CargarImagenEnPictureBox(byte[]? imagenBytes)
    {
        if (imagenBytes != null && imagenBytes.Length > 0)
        {
            try
            {
                // Cargar desde MemoryStream para no bloquear archivos en disco
                using var ms = new MemoryStream(imagenBytes);
                using var imgTemp = Image.FromStream(ms);
                _imagenCargada = new Bitmap(imgTemp);
                _picImagen.Image = _imagenCargada;
            }
            catch
            {
                _picImagen.Image = GenerarImagenPlaceholder();
            }
        }
        else
        {
            _picImagen.Image = GenerarImagenPlaceholder();
        }
    }

    private Image GenerarImagenPlaceholder()
    {
        var bmp = new Bitmap(800, 500);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.FromArgb(30, 41, 59));
        g.SmoothingMode = SmoothingMode.AntiAlias;

        using var font = new Font("Segoe UI", 18f, FontStyle.Bold);
        using var brush = new SolidBrush(Color.FromArgb(148, 163, 184));
        using var sf = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };

        g.DrawString("Realiza una pausa activa:\nInhala profundo, relaja los hombros y parpadea lentamente.",
            font, brush, new RectangleF(0, 0, 800, 500), sf);

        _imagenCargada = bmp;
        return bmp;
    }

    private void PnlProgresoFondo_Paint(object? sender, PaintEventArgs e)
    {
        if (_porcentajeProgreso <= 0f) return;

        int anchoProgreso = (int)(_pnlProgresoFondo.Width * Math.Clamp(_porcentajeProgreso, 0f, 1f));
        if (anchoProgreso <= 0) return;

        using var brush = new SolidBrush(Color.FromArgb(56, 189, 248)); // Sky 400
        e.Graphics.FillRectangle(brush, 0, 0, anchoProgreso, _pnlProgresoFondo.Height);
    }

    /// <summary>
    /// Actualiza el contador y la barra de progreso sincronizada por el PauseManager.
    /// </summary>
    public void ActualizarTiempo(int segundosRestantes)
    {
        if (IsDisposed) return;

        int segundos = Math.Max(0, segundosRestantes);
        _lblContador.Text = TimeSpan.FromSeconds(segundos).ToString(@"mm\:ss");

        float transcurrido = _duracionTotalSeg - segundos;
        _porcentajeProgreso = transcurrido / _duracionTotalSeg;
        _pnlProgresoFondo.Invalidate();
    }

    /// <summary>
    /// Permite el cierre controlado de la ventana al finalizar el tiempo o por emergencia.
    /// </summary>
    public void PermitirCierreYCerrar()
    {
        _puedeCerrar = true;
        Close();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        // Atajo de emergencia corporativo: Ctrl + Shift + Alt + E
        if (e.Control && e.Shift && e.Alt && e.KeyCode == Keys.E)
        {
            e.SuppressKeyPress = true;
            _onEmergenciaSolicitada?.Invoke();
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // Bloquea Alt+F4 y cualquier intento de cierre del usuario hasta finalizar la pausa
        if (!_puedeCerrar)
        {
            e.Cancel = true;
            return;
        }

        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _picImagen.Image = null;
            _imagenCargada?.Dispose();
            _imagenCargada = null;
        }
        base.Dispose(disposing);
    }

    // Panel con doble buffer para animación suave de la barra de progreso sin parpadeo
    private class PanelDoubleBuffered : Panel
    {
        public PanelDoubleBuffered()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }
    }
}
