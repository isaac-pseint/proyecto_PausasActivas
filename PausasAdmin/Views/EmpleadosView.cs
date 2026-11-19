using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using PausasCore.Models;
using PausasCore.Repositories;
using PausasAdmin.Theme;

namespace PausasAdmin.Views;

/// <summary>
/// Vista de gestión de empleados, configuración de pausas y asignación de imágenes.
/// </summary>
public class EmpleadosView : UserControl
{
    private readonly IAdminRepository _adminRepo;
    private SplitContainer _split = null!;

    // Panel izquierdo: lista de empleados
    private DataGridView _dgvEmpleados = null!;
    private TextBox _txtBuscar = null!;
    private Button _btnRefrescar = null!;

    // Panel derecho: editor del empleado seleccionado
    private Panel _pnlEditorContenido = null!;
    private Label _lblUsuarioWindows = null!;
    private Label _lblPc = null!;
    private TextBox _txtNombre = null!;
    private CheckBox _chkActivo = null!;
    private NumericUpDown _nudIntervalo = null!;
    private NumericUpDown _nudDuracion = null!;
    private ComboBox _cmbOrden = null!;
    private DateTimePicker _dtpHoraInicio = null!;
    private DateTimePicker _dtpHoraFin = null!;
    private Button _btnGuardar = null!;

    // Asignador de imágenes
    private ListBox _lstDisponibles = null!;
    private ListBox _lstAsignadas = null!;
    private Button _btnAgregar = null!;
    private Button _btnQuitar = null!;
    private Button _btnSubir = null!;
    private Button _btnBajar = null!;
    private PictureBox _picPreview = null!;

    private List<Usuario> _todosEmpleados = new();
    private Usuario? _empleadoSeleccionado;
    private List<ImagenMetadata> _todasImagenes = new();
    private bool _cargando;

    public EmpleadosView(IAdminRepository adminRepo)
    {
        _adminRepo = adminRepo;
        InicializarComponentes();
        _ = CargarDatosAsync();
    }

    private void InicializarComponentes()
    {
        Size = new Size(1060, 700);
        Dock = DockStyle.Fill;
        BackColor = AppTheme.MainBg;
        DoubleBuffered = true;
        Padding = new Padding(24);

        // ========== Encabezado general ==========
        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 50,
            BackColor = AppTheme.MainBg
        };

        var lblTitle = new Label
        {
            Text = "Empleados y Configuración de Pausas",
            Font = AppTheme.SubtitleFont,
            ForeColor = AppTheme.TextPrimary,
            Location = new Point(0, 0),
            AutoSize = true
        };

        var lblDesc = new Label
        {
            Text = "Administre los parámetros de pausas activas y asigne imágenes a cada empleado.",
            Font = AppTheme.SmallFont,
            ForeColor = AppTheme.TextSecondary,
            Location = new Point(0, 26),
            AutoSize = true
        };

        pnlHeader.Controls.AddRange(new Control[] { lblTitle, lblDesc });

        // ========== SplitContainer Seguro ==========
        // No se asignan Panel1MinSize, Panel2MinSize ni SplitterDistance aquí en el constructor
        // para evitar el error "SplitterDistance debe estar entre Panel1MinSize y Ancho - Panel2MinSize"
        // antes de que el control sea renderizado con su ancho real.
        _split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterWidth = 8,
            BackColor = AppTheme.MainBg
        };
        _split.HandleCreated += (s, e) =>
        {
            if (IsHandleCreated && !IsDisposed)
            {
                AjustarSplitterSeguro();
            }
        };

        CrearPanelIzquierdo(_split.Panel1);
        CrearPanelDerecho(_split.Panel2);

        Controls.Add(_split);
        Controls.Add(pnlHeader);
    }

    // ==================== PANEL IZQUIERDO: LISTA ====================
    private void CrearPanelIzquierdo(SplitterPanel panel)
    {
        panel.BackColor = AppTheme.MainBg;
        panel.Padding = new Padding(0, 8, 8, 0);

        // Barra de búsqueda
        var pnlBuscador = new Panel
        {
            Dock = DockStyle.Top,
            Height = 44,
            Padding = new Padding(0, 0, 0, 8)
        };

        _txtBuscar = new TextBox
        {
            PlaceholderText = "Buscar por nombre o usuario_windows...",
            Font = AppTheme.BodyFont,
            BackColor = AppTheme.InputBg,
            BorderStyle = BorderStyle.FixedSingle,
            Dock = DockStyle.Fill
        };
        _txtBuscar.TextChanged += (s, e) =>
        {
            AplicarFiltroBusqueda();
            if (_dgvEmpleados.Rows.Count > 0 && _dgvEmpleados.SelectedRows.Count > 0)
            {
                _ = CargarEmpleadoSeleccionadoAsync(forzarRecarga: true);
            }
            else
            {
                _empleadoSeleccionado = null;
                MostrarEditor(false);
            }
        };

        _btnRefrescar = new Button
        {
            Text = "⟳",
            Size = new Size(36, 28),
            Dock = DockStyle.Right
        };
        AppTheme.EstilizarBotonSecundario(_btnRefrescar);
        _btnRefrescar.Click += async (s, e) => await CargarDatosAsync();

        pnlBuscador.Controls.Add(_txtBuscar);
        pnlBuscador.Controls.Add(_btnRefrescar);

        // DataGridView de empleados
        var pnlCard = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = AppTheme.CardBg,
            Padding = new Padding(1)
        };
        pnlCard.Paint += (s, e) =>
        {
            using var pen = new Pen(AppTheme.CardBorder);
            e.Graphics.DrawRectangle(pen, 0, 0, pnlCard.Width - 1, pnlCard.Height - 1);
        };

        _dgvEmpleados = new DataGridView { Dock = DockStyle.Fill };
        AppTheme.EstilizarDataGridView(_dgvEmpleados);
        ConfigurarColumnasEmpleados();
        _dgvEmpleados.SelectionChanged += async (s, e) =>
        {
            if (_cargando) return;
            await CargarEmpleadoSeleccionadoAsync();
        };
        _dgvEmpleados.CellClick += async (s, e) =>
        {
            if (e.RowIndex >= 0)
            {
                await CargarEmpleadoSeleccionadoAsync();
            }
        };
        _dgvEmpleados.CellFormatting += DgvEmpleados_CellFormatting;

        pnlCard.Controls.Add(_dgvEmpleados);
        panel.Controls.Add(pnlCard);
        panel.Controls.Add(pnlBuscador);
    }

    private void ConfigurarColumnasEmpleados()
    {
        _dgvEmpleados.Columns.Clear();
        _dgvEmpleados.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "colNombreUsr",
            HeaderText = "Nombre / Usuario",
            FillWeight = 34,
            MinimumWidth = 140
        });
        _dgvEmpleados.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "colPc",
            HeaderText = "PC",
            FillWeight = 16,
            MinimumWidth = 75
        });
        _dgvEmpleados.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "colEstado",
            HeaderText = "Estado",
            Width = 65,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None
        });
        _dgvEmpleados.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "colIntervalo",
            HeaderText = "Intervalo",
            Width = 65,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None
        });
        _dgvEmpleados.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "colDuracion",
            HeaderText = "Duración",
            Width = 65,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None
        });
        _dgvEmpleados.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "colConexion",
            HeaderText = "Última conexión",
            FillWeight = 26,
            MinimumWidth = 135
        });
    }

    // ==================== PANEL DERECHO: EDITOR ====================
    private void CrearPanelDerecho(SplitterPanel panel)
    {
        panel.BackColor = AppTheme.MainBg;
        panel.Padding = new Padding(8, 8, 0, 0);
        panel.AutoScroll = true;

        _pnlEditorContenido = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = AppTheme.CardBg,
            Padding = new Padding(20)
        };
        _pnlEditorContenido.Paint += (s, e) =>
        {
            using var pen = new Pen(AppTheme.CardBorder);
            e.Graphics.DrawRectangle(pen, 0, 0, _pnlEditorContenido.Width - 1, _pnlEditorContenido.Height - 1);
        };

        var lblSeleccion = new Label
        {
            Text = "Seleccione un empleado de la lista para editar su configuración.",
            Font = AppTheme.BodyFont,
            ForeColor = AppTheme.TextMuted,
            Location = new Point(20, 20),
            AutoSize = true,
            Name = "lblPlaceholder"
        };

        // ---- Sección: Datos del empleado ----
        int y = 16;
        var lblSeccionInfo = CrearLabelSeccion("Información del Empleado", ref y);

        _lblUsuarioWindows = CrearLabelReadOnly("usuario_windows:", ref y);
        _lblPc = CrearLabelReadOnly("PC:", ref y);

        var lblNombre = CrearLabelCampo("Nombre visible", ref y);
        _txtNombre = new TextBox
        {
            Font = AppTheme.BodyFont, BackColor = AppTheme.InputBg,
            BorderStyle = BorderStyle.FixedSingle,
            Location = new Point(20, y), Size = new Size(300, 28), MaxLength = 100
        };
        y += 36;

        // ---- Sección: Configuración ----
        var lblSeccionConfig = CrearLabelSeccion("Configuración de Pausas", ref y);

        _chkActivo = new CheckBox
        {
            Text = "Pausas activas habilitadas",
            Font = AppTheme.BodyFont,
            ForeColor = AppTheme.TextPrimary,
            Location = new Point(20, y),
            AutoSize = true
        };
        y += 32;

        var lblIntervalo = CrearLabelCampo("Intervalo (minutos)", ref y);
        _nudIntervalo = new NumericUpDown
        {
            Font = AppTheme.BodyFont, Minimum = 1, Maximum = 480, Value = 60,
            Location = new Point(20, y), Size = new Size(120, 28)
        };
        y += 36;

        var lblDuracion = CrearLabelCampo("Duración (segundos)", ref y);
        _nudDuracion = new NumericUpDown
        {
            Font = AppTheme.BodyFont, Minimum = 5, Maximum = 1800, Value = 120,
            Location = new Point(20, y), Size = new Size(120, 28)
        };
        y += 36;

        var lblOrden = CrearLabelCampo("Orden de reproducción", ref y);
        _cmbOrden = new ComboBox
        {
            Font = AppTheme.BodyFont,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(20, y), Size = new Size(180, 28)
        };
        _cmbOrden.Items.AddRange(new object[] { "secuencial", "aleatorio" });
        _cmbOrden.SelectedIndex = 0;
        y += 36;

        var lblHInicio = CrearLabelCampo("Hora inicio", ref y);
        _dtpHoraInicio = new DateTimePicker
        {
            Font = AppTheme.BodyFont,
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "HH:mm",
            ShowUpDown = true,
            Location = new Point(20, y), Size = new Size(120, 28)
        };

        var lblHFin = new Label
        {
            Text = "Hora fin",
            Font = AppTheme.SmallBoldFont, ForeColor = AppTheme.TextPrimary,
            Location = new Point(160, y - 20), AutoSize = true
        };
        _dtpHoraFin = new DateTimePicker
        {
            Font = AppTheme.BodyFont,
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "HH:mm",
            ShowUpDown = true,
            Location = new Point(160, y), Size = new Size(120, 28)
        };
        y += 40;

        // ---- Sección: Asignación de imágenes ----
        var lblSeccionImg = CrearLabelSeccion("Imágenes Asignadas", ref y);

        var lblNota = new Label
        {
            Text = "ℹ Si la lista de asignadas está vacía, este empleado verá todas las imágenes de la biblioteca.",
            Font = AppTheme.SmallFont,
            ForeColor = AppTheme.Accent,
            Location = new Point(20, y),
            Size = new Size(500, 18)
        };
        y += 24;

        var lblDisponibles = new Label
        {
            Text = "Disponibles", Font = AppTheme.SmallBoldFont,
            ForeColor = AppTheme.TextSecondary,
            Location = new Point(20, y), AutoSize = true
        };
        var lblAsignadas = new Label
        {
            Text = "Asignadas (en orden)", Font = AppTheme.SmallBoldFont,
            ForeColor = AppTheme.TextSecondary,
            Location = new Point(260, y), AutoSize = true
        };
        y += 20;

        _lstDisponibles = new ListBox
        {
            Font = AppTheme.BodyFont,
            Location = new Point(20, y), Size = new Size(180, 140),
            SelectionMode = SelectionMode.One
        };
        _lstDisponibles.SelectedIndexChanged += LstImagenes_SelectedIndexChanged;

        // Botones de transferencia
        _btnAgregar = new Button { Text = "→", Size = new Size(40, 30), Location = new Point(210, y + 20) };
        AppTheme.EstilizarBotonPrimario(_btnAgregar);
        _btnAgregar.Click += BtnAgregar_Click;

        _btnQuitar = new Button { Text = "←", Size = new Size(40, 30), Location = new Point(210, y + 55) };
        AppTheme.EstilizarBotonSecundario(_btnQuitar);
        _btnQuitar.Click += BtnQuitar_Click;

        _lstAsignadas = new ListBox
        {
            Font = AppTheme.BodyFont,
            Location = new Point(260, y), Size = new Size(180, 140),
            SelectionMode = SelectionMode.One
        };
        _lstAsignadas.SelectedIndexChanged += LstImagenes_SelectedIndexChanged;

        // Botones reordenar
        _btnSubir = new Button { Text = "▲", Size = new Size(40, 30), Location = new Point(450, y + 20) };
        AppTheme.EstilizarBotonSecundario(_btnSubir);
        _btnSubir.Click += BtnSubirOrden_Click;

        _btnBajar = new Button { Text = "▼", Size = new Size(40, 30), Location = new Point(450, y + 55) };
        AppTheme.EstilizarBotonSecundario(_btnBajar);
        _btnBajar.Click += BtnBajarOrden_Click;

        y += 150;

        // Vista previa de imagen seleccionada
        _picPreview = new PictureBox
        {
            Location = new Point(20, y),
            Size = new Size(200, 120),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = AppTheme.MainBg,
            BorderStyle = BorderStyle.FixedSingle
        };
        y += 130;

        // ---- Botón Guardar ----
        _btnGuardar = new Button { Text = "Guardar cambios", Size = new Size(180, 40), Location = new Point(20, y) };
        AppTheme.EstilizarBotonPrimario(_btnGuardar);
        _btnGuardar.Click += async (s, e) => await GuardarCambiosAsync();
        y += 50;

        // Ensamblar todos los controles en el panel editor
        _pnlEditorContenido.Controls.AddRange(new Control[]
        {
            lblSeleccion,
            lblSeccionInfo, _lblUsuarioWindows, _lblPc,
            lblNombre, _txtNombre,
            lblSeccionConfig, _chkActivo,
            lblIntervalo, _nudIntervalo,
            lblDuracion, _nudDuracion,
            lblOrden, _cmbOrden,
            lblHInicio, _dtpHoraInicio, lblHFin, _dtpHoraFin,
            lblSeccionImg, lblNota,
            lblDisponibles, lblAsignadas,
            _lstDisponibles, _btnAgregar, _btnQuitar, _lstAsignadas,
            _btnSubir, _btnBajar,
            _picPreview,
            _btnGuardar
        });

        // Ocultar todo el editor hasta seleccionar un empleado
        foreach (Control c in _pnlEditorContenido.Controls)
        {
            if (c.Name != "lblPlaceholder")
                c.Visible = false;
        }

        panel.Controls.Add(_pnlEditorContenido);
    }

    // ==================== HELPERS DE LAYOUT ====================
    private Label CrearLabelSeccion(string text, ref int y)
    {
        y += 4;
        var lbl = new Label
        {
            Text = text,
            Font = AppTheme.HeadingFont,
            ForeColor = AppTheme.TextPrimary,
            Location = new Point(20, y),
            AutoSize = true
        };
        y += 28;
        return lbl;
    }

    private Label CrearLabelReadOnly(string prefijo, ref int y)
    {
        var lbl = new Label
        {
            Text = $"{prefijo} —",
            Font = AppTheme.SmallFont,
            ForeColor = AppTheme.TextSecondary,
            Location = new Point(20, y),
            Size = new Size(460, 18)
        };
        y += 22;
        return lbl;
    }

    private Label CrearLabelCampo(string text, ref int y)
    {
        var lbl = new Label
        {
            Text = text,
            Font = AppTheme.SmallBoldFont,
            ForeColor = AppTheme.TextPrimary,
            Location = new Point(20, y),
            AutoSize = true
        };
        y += 20;
        return lbl;
    }

    // ==================== CARGA DE DATOS ====================
    private async Task CargarDatosAsync()
    {
        _cargando = true;
        _btnRefrescar.Enabled = false;
        try
        {
            _todosEmpleados = await _adminRepo.ObtenerTodosUsuariosAsync();
            _todasImagenes = await _adminRepo.ObtenerMetadatosTodasImagenesAsync();

            int? idSeleccionPrev = _empleadoSeleccionado?.Id;
            AplicarFiltroBusqueda();

            // Reseleccionar el empleado que estaba seleccionado
            bool restaurado = false;
            if (idSeleccionPrev.HasValue)
            {
                for (int i = 0; i < _dgvEmpleados.Rows.Count; i++)
                {
                    if (_dgvEmpleados.Rows[i].Tag is Usuario u && u.Id == idSeleccionPrev.Value)
                    {
                        _dgvEmpleados.Rows[i].Selected = true;
                        _dgvEmpleados.FirstDisplayedScrollingRowIndex = i;
                        restaurado = true;
                        break;
                    }
                }
            }

            if (!restaurado && _dgvEmpleados.Rows.Count > 0)
            {
                _dgvEmpleados.Rows[0].Selected = true;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al cargar datos:\n{ex.Message}", "Error de conexión",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _cargando = false;
            _btnRefrescar.Enabled = true;
        }

        // Si hay filas y una está seleccionada, disparar explícitamente la carga del editor
        if (_dgvEmpleados.Rows.Count > 0 && _dgvEmpleados.SelectedRows.Count > 0)
        {
            await CargarEmpleadoSeleccionadoAsync(forzarRecarga: true);
        }
        else
        {
            _empleadoSeleccionado = null;
            MostrarEditor(false);
        }
    }

    private void AplicarFiltroBusqueda()
    {
        string filtro = _txtBuscar.Text.Trim().ToLowerInvariant();
        var filtrados = string.IsNullOrEmpty(filtro)
            ? _todosEmpleados
            : _todosEmpleados.Where(u =>
                (u.Nombre ?? "").ToLowerInvariant().Contains(filtro) ||
                u.UsuarioWindows.ToLowerInvariant().Contains(filtro)
            ).ToList();

        _dgvEmpleados.Rows.Clear();
        foreach (var emp in filtrados)
        {
            string display = !string.IsNullOrWhiteSpace(emp.Nombre) ? $"{emp.Nombre} ({emp.UsuarioWindows})" : emp.UsuarioWindows;
            string estado = emp.Activo ? "Activo" : "Inactivo";
            string intervalo = $"{emp.IntervaloMin} min";
            string duracion = $"{emp.DuracionSeg} s";

            string conexion;
            if (emp.UltimaConexion.HasValue)
            {
                var local = emp.UltimaConexion.Value.ToLocalTime();
                var dif = DateTime.Now - local;
                if (dif.TotalMinutes <= 10)
                {
                    int mins = Math.Max(0, (int)dif.TotalMinutes);
                    conexion = mins == 0 ? "En línea (ahora)" : $"En línea (hace {mins} min)";
                }
                else
                {
                    conexion = local.ToString("dd/MM/yyyy HH:mm");
                }
            }
            else
            {
                conexion = "Sin registro";
            }

            int idx = _dgvEmpleados.Rows.Add(display, emp.Pc ?? "—", estado, intervalo, duracion, conexion);
            _dgvEmpleados.Rows[idx].Tag = emp;
        }
    }

    private void DgvEmpleados_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= _dgvEmpleados.Rows.Count || e.CellStyle == null) return;

        string colName = _dgvEmpleados.Columns[e.ColumnIndex].Name;
        if (colName == "colEstado")
        {
            string val = e.Value?.ToString() ?? "";
            if (val == "Activo")
            {
                e.CellStyle.ForeColor = AppTheme.Success;
                e.CellStyle.SelectionForeColor = AppTheme.Success;
            }
            else if (val == "Inactivo")
            {
                e.CellStyle.ForeColor = AppTheme.Danger;
                e.CellStyle.SelectionForeColor = AppTheme.Danger;
            }
        }
        else if (colName == "colConexion")
        {
            string val = e.Value?.ToString() ?? "";
            if (val.StartsWith("En línea", StringComparison.OrdinalIgnoreCase))
            {
                e.CellStyle.ForeColor = AppTheme.Success;
                e.CellStyle.SelectionForeColor = AppTheme.Success;
                e.CellStyle.Font = AppTheme.SmallBoldFont;
            }
            else if (val == "Sin registro")
            {
                e.CellStyle.ForeColor = AppTheme.TextMuted;
                e.CellStyle.SelectionForeColor = AppTheme.TextMuted;
            }
            else
            {
                e.CellStyle.ForeColor = AppTheme.TextSecondary;
                e.CellStyle.SelectionForeColor = AppTheme.TextSecondary;
            }
        }
    }

    // ==================== SELECCIÓN DE EMPLEADO ====================
    private async Task CargarEmpleadoSeleccionadoAsync(bool forzarRecarga = false)
    {
        if (_dgvEmpleados.SelectedRows.Count == 0)
        {
            _empleadoSeleccionado = null;
            MostrarEditor(false);
            return;
        }

        var usuario = _dgvEmpleados.SelectedRows[0].Tag as Usuario;
        if (usuario == null)
        {
            _empleadoSeleccionado = null;
            MostrarEditor(false);
            return;
        }

        // Evitar recargas duplicadas solo si ya está cargado ese mismo usuario.Id y el panel de edición ya está visible
        if (!forzarRecarga && _empleadoSeleccionado?.Id == usuario.Id && EsEditorVisible())
        {
            return;
        }

        _empleadoSeleccionado = usuario;
        MostrarEditor(true);
        CargarDatosEnEditor(_empleadoSeleccionado);
        await CargarAsignacionesAsync(_empleadoSeleccionado.Id);
    }

    private bool EsEditorVisible()
    {
        return _txtNombre != null && _txtNombre.Visible;
    }

    private void MostrarEditor(bool visible)
    {
        foreach (Control c in _pnlEditorContenido.Controls)
        {
            if (c.Name == "lblPlaceholder")
                c.Visible = !visible;
            else
                c.Visible = visible;
        }
    }

    private void CargarDatosEnEditor(Usuario emp)
    {
        _lblUsuarioWindows.Text = $"usuario_windows: {emp.UsuarioWindows}";
        _lblPc.Text = $"PC: {emp.Pc ?? "—"}";
        _txtNombre.Text = emp.Nombre ?? "";
        _chkActivo.Checked = emp.Activo;
        _nudIntervalo.Value = Math.Clamp(emp.IntervaloMin, 1, 480);
        _nudDuracion.Value = Math.Clamp(emp.DuracionSeg, 5, 1800);
        _cmbOrden.SelectedItem = emp.Orden;
        _dtpHoraInicio.Value = DateTime.Today.Add(emp.HoraInicio);
        _dtpHoraFin.Value = DateTime.Today.Add(emp.HoraFin);
    }

    // ==================== ASIGNACIÓN DE IMÁGENES ====================
    private async Task CargarAsignacionesAsync(int usuarioId)
    {
        try
        {
            var asignadasIds = await _adminRepo.ObtenerIdsImagenesAsignadasAsync(usuarioId);
            var asignadasSet = new HashSet<int>(asignadasIds);

            _lstDisponibles.Items.Clear();
            _lstAsignadas.Items.Clear();

            // Asignadas en el orden de posición
            foreach (int id in asignadasIds)
            {
                var meta = _todasImagenes.FirstOrDefault(m => m.Id == id);
                if (meta != null)
                {
                    _lstAsignadas.Items.Add(new ImagenListItem(meta));
                }
            }

            // Disponibles: las que no están asignadas
            foreach (var meta in _todasImagenes)
            {
                if (!asignadasSet.Contains(meta.Id))
                {
                    _lstDisponibles.Items.Add(new ImagenListItem(meta));
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al cargar asignaciones:\n{ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void BtnAgregar_Click(object? sender, EventArgs e)
    {
        if (_lstDisponibles.SelectedItem is ImagenListItem item)
        {
            _lstDisponibles.Items.Remove(item);
            _lstAsignadas.Items.Add(item);
        }
    }

    private void BtnQuitar_Click(object? sender, EventArgs e)
    {
        if (_lstAsignadas.SelectedItem is ImagenListItem item)
        {
            _lstAsignadas.Items.Remove(item);
            _lstDisponibles.Items.Add(item);
        }
    }

    private void BtnSubirOrden_Click(object? sender, EventArgs e)
    {
        int idx = _lstAsignadas.SelectedIndex;
        if (idx <= 0) return;
        var item = _lstAsignadas.Items[idx];
        _lstAsignadas.Items.RemoveAt(idx);
        _lstAsignadas.Items.Insert(idx - 1, item);
        _lstAsignadas.SelectedIndex = idx - 1;
    }

    private void BtnBajarOrden_Click(object? sender, EventArgs e)
    {
        int idx = _lstAsignadas.SelectedIndex;
        if (idx < 0 || idx >= _lstAsignadas.Items.Count - 1) return;
        var item = _lstAsignadas.Items[idx];
        _lstAsignadas.Items.RemoveAt(idx);
        _lstAsignadas.Items.Insert(idx + 1, item);
        _lstAsignadas.SelectedIndex = idx + 1;
    }

    private async void LstImagenes_SelectedIndexChanged(object? sender, EventArgs e)
    {
        ImagenListItem? selected = null;
        if (sender == _lstDisponibles && _lstDisponibles.SelectedItem is ImagenListItem d)
            selected = d;
        else if (sender == _lstAsignadas && _lstAsignadas.SelectedItem is ImagenListItem a)
            selected = a;

        if (selected == null)
        {
            _picPreview.Image?.Dispose();
            _picPreview.Image = null;
            return;
        }

        // Cargar miniatura desde el servidor
        try
        {
            var imagen = await _adminRepo.ObtenerImagenPorIdAsync(selected.Meta.Id);
            if (imagen?.Datos != null && imagen.Datos.Length > 0)
            {
                _picPreview.Image?.Dispose();
                using var ms = new MemoryStream(imagen.Datos);
                using var img = Image.FromStream(ms);
                _picPreview.Image = new Bitmap(img);
            }
        }
        catch
        {
            _picPreview.Image?.Dispose();
            _picPreview.Image = null;
        }
    }

    // ==================== GUARDAR CAMBIOS ====================
    private async Task GuardarCambiosAsync()
    {
        if (_empleadoSeleccionado == null) return;

        // Validar hora_inicio < hora_fin
        TimeSpan horaInicio = _dtpHoraInicio.Value.TimeOfDay;
        TimeSpan horaFin = _dtpHoraFin.Value.TimeOfDay;

        if (horaInicio >= horaFin)
        {
            MessageBox.Show("La hora de inicio debe ser anterior a la hora de fin.\n" +
                "Esta restricción es validada tanto por la aplicación como por SQL Server (CHECK constraint).",
                "Validación de horario",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _dtpHoraInicio.Focus();
            return;
        }

        _btnGuardar.Enabled = false;
        _btnGuardar.Text = "Guardando...";

        try
        {
            // Actualizar datos del usuario
            _empleadoSeleccionado.Nombre = string.IsNullOrWhiteSpace(_txtNombre.Text) ? null : _txtNombre.Text.Trim();
            _empleadoSeleccionado.Activo = _chkActivo.Checked;
            _empleadoSeleccionado.IntervaloMin = (int)_nudIntervalo.Value;
            _empleadoSeleccionado.DuracionSeg = (int)_nudDuracion.Value;
            _empleadoSeleccionado.Orden = _cmbOrden.SelectedItem?.ToString() ?? "secuencial";
            _empleadoSeleccionado.HoraInicio = horaInicio;
            _empleadoSeleccionado.HoraFin = horaFin;

            await _adminRepo.ActualizarUsuarioAsync(_empleadoSeleccionado);

            // Guardar asignaciones de imágenes (transacción atómica en el repositorio)
            var idsAsignados = new List<int>();
            foreach (var item in _lstAsignadas.Items)
            {
                if (item is ImagenListItem ili)
                    idsAsignados.Add(ili.Meta.Id);
            }

            await _adminRepo.GuardarAsignacionesAsync(_empleadoSeleccionado.Id, idsAsignados);

            MessageBox.Show("Cambios guardados correctamente.", "Éxito",
                MessageBoxButtons.OK, MessageBoxIcon.Information);

            // Refrescar la tabla manteniendo la selección
            await CargarDatosAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al guardar los cambios:\n{ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _btnGuardar.Enabled = true;
            _btnGuardar.Text = "Guardar cambios";
        }
    }

    /// <summary>
    /// Ajusta las propiedades de SplitContainer de forma segura validando
    /// que el handle esté creado, el ancho real sea superior a la suma de los tamaños mínimos
    /// y acotando con Math.Clamp.
    /// </summary>
    private void AjustarSplitterSeguro()
    {
        if (!IsHandleCreated || IsDisposed || _split == null || _split.IsDisposed)
            return;

        int totalAncho = _split.Width;
        const int min1 = 340;
        const int min2 = 380;
        int distanciaDeseada = Math.Max(min1, (int)(totalAncho * 0.42));

        // Si el control aún no se ha medido o es menor a la suma requerida, evitar asignar mínimos que causen excepción
        if (totalAncho <= (min1 + min2))
        {
            return;
        }

        try
        {
            _split.Panel1MinSize = min1;
            _split.Panel2MinSize = min2;

            int maxDistancia = totalAncho - min2;
            int distanciaSegura = Math.Clamp(distanciaDeseada, min1, Math.Max(min1, maxDistancia));
            _split.SplitterDistance = distanciaSegura;
        }
        catch
        {
            // Silencioso ante condiciones transitorias de redimensionamiento de Windows
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        AjustarSplitterSeguro();
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        if (IsHandleCreated && !IsDisposed)
        {
            AjustarSplitterSeguro();
        }
    }

    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        if (IsHandleCreated && !IsDisposed)
        {
            AjustarSplitterSeguro();
        }
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        if (IsHandleCreated && !IsDisposed)
        {
            AjustarSplitterSeguro();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _picPreview.Image?.Dispose();
        }
        base.Dispose(disposing);
    }

    // ==================== ITEM WRAPPER PARA LISTBOX ====================
    private class ImagenListItem
    {
        public ImagenMetadata Meta { get; }

        public ImagenListItem(ImagenMetadata meta)
        {
            Meta = meta;
        }

        public override string ToString()
        {
            string cat = !string.IsNullOrWhiteSpace(Meta.Categoria) ? $" [{Meta.Categoria}]" : "";
            return $"#{Meta.Id} {Meta.Nombre}{cat}";
        }
    }
}
