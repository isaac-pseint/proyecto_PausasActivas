using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using PausasCore.Models;
using PausasCore.Repositories;
using PausasAdmin.Forms;
using PausasAdmin.Theme;

namespace PausasAdmin.Views;

/// <summary>
/// Vista de gestión del catálogo de imágenes para pausas activas.
/// </summary>
public class ImagenesView : UserControl
{
    private readonly IAdminRepository _adminRepo;

    private DataGridView _dgv = null!;
    private ComboBox _cmbFiltroCategoria = null!;
    private Button _btnSubir = null!;
    private Button _btnRefrescar = null!;
    private Label _lblConteo = null!;

    private List<ImagenMetadata> _todasImagenes = new();

    public ImagenesView(IAdminRepository adminRepo)
    {
        _adminRepo = adminRepo;
        InicializarComponentes();
        _ = CargarImagenesAsync();
    }

    private void InicializarComponentes()
    {
        Dock = DockStyle.Fill;
        BackColor = AppTheme.MainBg;
        DoubleBuffered = true;
        Padding = new Padding(24);

        // ========== Encabezado ==========
        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 90,
            BackColor = AppTheme.MainBg,
            Padding = new Padding(0, 0, 0, 12)
        };

        var lblTitle = new Label
        {
            Text = "Biblioteca de Imágenes",
            Font = AppTheme.SubtitleFont,
            ForeColor = AppTheme.TextPrimary,
            Location = new Point(0, 0),
            AutoSize = true
        };

        var lblDesc = new Label
        {
            Text = "Gestione las imágenes que se mostrarán en las pausas activas de los empleados.",
            Font = AppTheme.SmallFont,
            ForeColor = AppTheme.TextSecondary,
            Location = new Point(0, 26),
            AutoSize = true
        };

        // Barra de herramientas
        _btnSubir = new Button { Text = "+ Subir imagen", Size = new Size(140, 36), Location = new Point(0, 56) };
        AppTheme.EstilizarBotonPrimario(_btnSubir);
        _btnSubir.Click += async (s, e) => await SubirImagenAsync();

        var lblFiltro = new Label
        {
            Text = "Categoría:",
            Font = AppTheme.SmallFont,
            ForeColor = AppTheme.TextSecondary,
            Location = new Point(160, 64),
            AutoSize = true
        };

        _cmbFiltroCategoria = new ComboBox
        {
            Font = AppTheme.BodyFont,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(230, 59),
            Size = new Size(160, 28)
        };
        _cmbFiltroCategoria.Items.Add("Todas");
        _cmbFiltroCategoria.SelectedIndex = 0;
        _cmbFiltroCategoria.SelectedIndexChanged += (s, e) => AplicarFiltro();

        _btnRefrescar = new Button { Text = "⟳ Refrescar", Size = new Size(110, 36), Location = new Point(410, 56) };
        AppTheme.EstilizarBotonSecundario(_btnRefrescar);
        _btnRefrescar.Click += async (s, e) => await CargarImagenesAsync();

        _lblConteo = new Label
        {
            Text = "",
            Font = AppTheme.SmallFont,
            ForeColor = AppTheme.TextMuted,
            Location = new Point(540, 64),
            AutoSize = true
        };

        pnlHeader.Controls.AddRange(new Control[]
        {
            lblTitle, lblDesc, _btnSubir, lblFiltro, _cmbFiltroCategoria, _btnRefrescar, _lblConteo
        });

        // ========== DataGridView ==========
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

        _dgv = new DataGridView
        {
            Dock = DockStyle.Fill
        };
        AppTheme.EstilizarDataGridView(_dgv);
        ConfigurarColumnas();
        _dgv.CellContentClick += Dgv_CellContentClick;

        pnlCard.Controls.Add(_dgv);
        Controls.Add(pnlCard);
        Controls.Add(pnlHeader);
    }

    private void ConfigurarColumnas()
    {
        _dgv.Columns.Clear();
        _dgv.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "colId", HeaderText = "ID", Width = 50,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None
        });
        _dgv.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "colNombre", HeaderText = "Nombre", FillWeight = 30
        });
        _dgv.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "colCategoria", HeaderText = "Categoría", FillWeight = 15
        });
        _dgv.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "colTipo", HeaderText = "Tipo", FillWeight = 10
        });
        _dgv.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "colActualizado", HeaderText = "Actualizado", FillWeight = 15
        });

        var colEditar = new DataGridViewButtonColumn
        {
            Name = "colEditar", HeaderText = "", Text = "Editar",
            UseColumnTextForButtonValue = true, Width = 70,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
            FlatStyle = FlatStyle.Flat
        };
        _dgv.Columns.Add(colEditar);

        var colEliminar = new DataGridViewButtonColumn
        {
            Name = "colEliminar", HeaderText = "", Text = "Eliminar",
            UseColumnTextForButtonValue = true, Width = 80,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
            FlatStyle = FlatStyle.Flat
        };
        _dgv.Columns.Add(colEliminar);
    }

    private async Task CargarImagenesAsync()
    {
        _btnRefrescar.Enabled = false;
        _btnSubir.Enabled = false;

        try
        {
            _todasImagenes = await _adminRepo.ObtenerMetadatosTodasImagenesAsync();
            ActualizarComboCategoria();
            AplicarFiltro();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al cargar imágenes:\n{ex.Message}", "Error de conexión",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _btnRefrescar.Enabled = true;
            _btnSubir.Enabled = true;
        }
    }

    private void ActualizarComboCategoria()
    {
        string seleccionActual = _cmbFiltroCategoria.SelectedItem?.ToString() ?? "Todas";
        _cmbFiltroCategoria.Items.Clear();
        _cmbFiltroCategoria.Items.Add("Todas");

        var categorias = _todasImagenes
            .Where(i => !string.IsNullOrWhiteSpace(i.Categoria))
            .Select(i => i.Categoria!)
            .Distinct()
            .OrderBy(c => c);

        foreach (var cat in categorias)
        {
            _cmbFiltroCategoria.Items.Add(cat);
        }

        int idx = _cmbFiltroCategoria.Items.IndexOf(seleccionActual);
        _cmbFiltroCategoria.SelectedIndex = idx >= 0 ? idx : 0;
    }

    private void AplicarFiltro()
    {
        string filtro = _cmbFiltroCategoria.SelectedItem?.ToString() ?? "Todas";
        var filtradas = filtro == "Todas"
            ? _todasImagenes
            : _todasImagenes.Where(i => string.Equals(i.Categoria, filtro, StringComparison.OrdinalIgnoreCase)).ToList();

        _dgv.Rows.Clear();
        foreach (var img in filtradas)
        {
            int rowIdx = _dgv.Rows.Add(
                img.Id,
                img.Nombre,
                img.Categoria ?? "—",
                img.TipoContenido,
                img.Actualizado.ToLocalTime().ToString("dd/MM/yyyy HH:mm")
            );
            _dgv.Rows[rowIdx].Tag = img;
        }

        _lblConteo.Text = $"{filtradas.Count} imagen(es)";
    }

    private async void Dgv_CellContentClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0) return;

        var meta = _dgv.Rows[e.RowIndex].Tag as ImagenMetadata;
        if (meta == null) return;

        if (_dgv.Columns[e.ColumnIndex].Name == "colEditar")
        {
            await EditarImagenAsync(meta);
        }
        else if (_dgv.Columns[e.ColumnIndex].Name == "colEliminar")
        {
            await EliminarImagenAsync(meta);
        }
    }

    private async Task SubirImagenAsync()
    {
        using var dlg = new SubirImagenDialog();
        if (dlg.ShowDialog() != DialogResult.OK) return;

        _btnSubir.Enabled = false;
        try
        {
            var imagen = new Imagen
            {
                Nombre = dlg.NombreImagen,
                Categoria = dlg.CategoriaImagen,
                TipoContenido = dlg.TipoContenido,
                Datos = dlg.DatosComprimidos
            };

            await _adminRepo.CrearImagenAsync(imagen);
            MessageBox.Show("Imagen subida correctamente.", "Éxito",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            await CargarImagenesAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al subir la imagen:\n{ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _btnSubir.Enabled = true;
        }
    }

    private async Task EditarImagenAsync(ImagenMetadata meta)
    {
        using var dlg = new SubirImagenDialog(rutaArchivo: null, nombreInicial: meta.Nombre, categoriaInicial: meta.Categoria);
        dlg.Text = $"Editar Imagen: {meta.Nombre}";

        if (dlg.ShowDialog() != DialogResult.OK) return;

        try
        {
            var imagen = new Imagen
            {
                Id = meta.Id,
                Nombre = dlg.NombreImagen,
                Categoria = dlg.CategoriaImagen,
                TipoContenido = dlg.DatosComprimidos.Length > 0 ? dlg.TipoContenido : meta.TipoContenido,
                Datos = dlg.DatosComprimidos
            };

            await _adminRepo.ActualizarImagenAsync(imagen);
            MessageBox.Show("Imagen actualizada correctamente.", "Éxito",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            await CargarImagenesAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al actualizar la imagen:\n{ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task EliminarImagenAsync(ImagenMetadata meta)
    {
        var resultado = MessageBox.Show(
            $"¿Está seguro de eliminar la imagen \"{meta.Nombre}\"?\n\n" +
            "Esta acción también la quitará de todos los empleados que la tengan asignada (ON DELETE CASCADE).",
            "Confirmar eliminación",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (resultado != DialogResult.Yes) return;

        try
        {
            await _adminRepo.EliminarImagenAsync(meta.Id);
            await CargarImagenesAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al eliminar la imagen:\n{ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
