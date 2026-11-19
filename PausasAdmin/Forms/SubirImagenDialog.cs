using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using PausasCore.Services;
using PausasAdmin.Theme;

namespace PausasAdmin.Forms;

/// <summary>
/// Diálogo de subida de imagen con vista previa, compresión automática y campos de metadatos.
/// </summary>
public class SubirImagenDialog : Form
{
    private static readonly string[] CategoriasDefecto =
        { "Cuello", "Manos", "Espalda", "Ojos", "Piernas", "General" };

    private PictureBox _picPreview = null!;
    private TextBox _txtNombre = null!;
    private ComboBox _cmbCategoria = null!;
    private Label _lblInfoSize = null!;
    private Button _btnAceptar = null!;
    private Button _btnCancelar = null!;

    public string NombreImagen => _txtNombre.Text.Trim();
    public string? CategoriaImagen => string.IsNullOrWhiteSpace(_cmbCategoria.Text) ? null : _cmbCategoria.Text.Trim();
    public byte[] DatosComprimidos { get; private set; } = Array.Empty<byte>();
    public string TipoContenido { get; private set; } = "image/jpeg";

    private byte[] _datosOriginales = Array.Empty<byte>();
    private string _tipoOriginal = "image/jpeg";

    public SubirImagenDialog(string? rutaArchivo = null, string? nombreInicial = null, string? categoriaInicial = null)
    {
        InicializarComponentes(nombreInicial, categoriaInicial);
        if (!string.IsNullOrWhiteSpace(rutaArchivo) && File.Exists(rutaArchivo))
        {
            CargarArchivo(rutaArchivo, nombreInicial);
        }
    }

    private void InicializarComponentes(string? nombreInicial, string? categoriaInicial)
    {
        Text = "Subir Imagen al Catálogo";
        Size = new Size(600, 520);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = AppTheme.MainBg;
        DoubleBuffered = true;

        // ---- Vista previa ----
        var lblPreview = new Label
        {
            Text = "Vista previa",
            Font = AppTheme.SmallBoldFont,
            ForeColor = AppTheme.TextSecondary,
            Location = new Point(24, 16),
            AutoSize = true
        };

        _picPreview = new PictureBox
        {
            Location = new Point(24, 36),
            Size = new Size(260, 200),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = AppTheme.CardBg,
            BorderStyle = BorderStyle.FixedSingle
        };

        _lblInfoSize = new Label
        {
            Text = "Seleccione una imagen",
            Font = AppTheme.SmallFont,
            ForeColor = AppTheme.TextSecondary,
            Location = new Point(24, 244),
            Size = new Size(260, 36)
        };

        // Botón seleccionar archivo
        var btnSeleccionarArchivo = new Button
        {
            Text = "Seleccionar archivo...",
            Location = new Point(24, 284),
            Size = new Size(260, 36)
        };
        AppTheme.EstilizarBotonSecundario(btnSeleccionarArchivo);
        btnSeleccionarArchivo.Click += (s, e) => SeleccionarArchivo();

        // ---- Formulario derecho ----
        var lblNombre = new Label
        {
            Text = "Nombre *",
            Font = AppTheme.SmallBoldFont,
            ForeColor = AppTheme.TextPrimary,
            Location = new Point(310, 36),
            AutoSize = true
        };

        _txtNombre = new TextBox
        {
            Font = AppTheme.BodyFont,
            BackColor = AppTheme.InputBg,
            BorderStyle = BorderStyle.FixedSingle,
            Location = new Point(310, 58),
            Size = new Size(250, 28),
            MaxLength = 100,
            Text = nombreInicial ?? ""
        };

        var lblCategoria = new Label
        {
            Text = "Categoría",
            Font = AppTheme.SmallBoldFont,
            ForeColor = AppTheme.TextPrimary,
            Location = new Point(310, 100),
            AutoSize = true
        };

        _cmbCategoria = new ComboBox
        {
            Font = AppTheme.BodyFont,
            BackColor = AppTheme.InputBg,
            FlatStyle = FlatStyle.Flat,
            Location = new Point(310, 122),
            Size = new Size(250, 28),
            DropDownStyle = ComboBoxStyle.DropDown,
            MaxLength = 50
        };
        _cmbCategoria.Items.AddRange(CategoriasDefecto);
        if (!string.IsNullOrWhiteSpace(categoriaInicial))
        {
            _cmbCategoria.Text = categoriaInicial;
        }

        var lblNota = new Label
        {
            Text = "Las imágenes se comprimen automáticamente\na menos de 500 KB (JPEG) para optimizar\nel ancho de banda del servidor.",
            Font = AppTheme.SmallFont,
            ForeColor = AppTheme.TextMuted,
            Location = new Point(310, 180),
            Size = new Size(250, 60)
        };

        // ---- Botones ----
        _btnAceptar = new Button
        {
            Text = "Subir imagen",
            Location = new Point(310, 420),
            Size = new Size(120, 40),
            Enabled = false
        };
        AppTheme.EstilizarBotonPrimario(_btnAceptar);
        _btnAceptar.Click += (s, e) => Aceptar();

        _btnCancelar = new Button
        {
            Text = "Cancelar",
            Location = new Point(440, 420),
            Size = new Size(120, 40)
        };
        AppTheme.EstilizarBotonSecundario(_btnCancelar);
        _btnCancelar.Click += (s, e) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        Controls.AddRange(new Control[]
        {
            lblPreview, _picPreview, _lblInfoSize, btnSeleccionarArchivo,
            lblNombre, _txtNombre,
            lblCategoria, _cmbCategoria,
            lblNota,
            _btnAceptar, _btnCancelar
        });

        CancelButton = _btnCancelar;
    }

    private void SeleccionarArchivo()
    {
        using var ofd = new OpenFileDialog
        {
            Title = "Seleccionar imagen para pausa activa",
            Filter = "Imágenes|*.jpg;*.jpeg;*.png;*.bmp|JPEG|*.jpg;*.jpeg|PNG|*.png|BMP|*.bmp",
            FilterIndex = 1
        };

        if (ofd.ShowDialog() == DialogResult.OK)
        {
            CargarArchivo(ofd.FileName, null);
        }
    }

    private void CargarArchivo(string ruta, string? nombreSugerido)
    {
        try
        {
            _datosOriginales = File.ReadAllBytes(ruta);
            long tamOriginal = _datosOriginales.Length;

            string ext = Path.GetExtension(ruta).ToLowerInvariant();
            _tipoOriginal = ext switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".bmp" => "image/bmp",
                _ => "image/jpeg"
            };

            // Comprimir con ImageCompressor de PausasCore
            (DatosComprimidos, TipoContenido) = ImageCompressor.ComprimirA500Kb(_datosOriginales, _tipoOriginal);
            long tamComprimido = DatosComprimidos.Length;

            // Vista previa
            _picPreview.Image?.Dispose();
            using var ms = new MemoryStream(DatosComprimidos);
            _picPreview.Image = Image.FromStream(ms);

            _lblInfoSize.Text = $"Original: {tamOriginal / 1024.0:F0} KB → Comprimida: {tamComprimido / 1024.0:F0} KB\n" +
                                $"Tipo: {TipoContenido}";

            if (string.IsNullOrWhiteSpace(_txtNombre.Text))
            {
                string nombre = nombreSugerido ?? Path.GetFileNameWithoutExtension(ruta);
                _txtNombre.Text = nombre.Length > 100 ? nombre[..100] : nombre;
            }

            _btnAceptar.Enabled = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al cargar la imagen:\n{ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void Aceptar()
    {
        if (string.IsNullOrWhiteSpace(_txtNombre.Text))
        {
            MessageBox.Show("El nombre de la imagen es obligatorio.", "Validación",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtNombre.Focus();
            return;
        }

        if (DatosComprimidos.Length == 0)
        {
            MessageBox.Show("Debe seleccionar un archivo de imagen.", "Validación",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _picPreview.Image?.Dispose();
        }
        base.Dispose(disposing);
    }
}
