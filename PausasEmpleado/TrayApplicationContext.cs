using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using PausasCore.Configuration;
using PausasCore.Repositories;
using PausasEmpleado.Helpers;
using PausasEmpleado.Managers;
using PausasEmpleado.Scheduling;
using PausasEmpleado.Services;

namespace PausasEmpleado;

/// <summary>
/// Contexto de ejecución principal de la aplicación en segundo plano.
/// Administra el ciclo de vida del NotifyIcon, el menú contextual, la sincronización y la programación.
/// </summary>
public class TrayApplicationContext : ApplicationContext
{
    private readonly SynchronizationContext? _syncContext;
    private readonly Control _syncControl;

    private readonly DbConfig _dbConfig;
    private readonly CacheService _cacheService;
    private readonly SyncService _syncService;
    private readonly PauseManager _pauseManager;
    private readonly PausaScheduler _scheduler;

    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _contextMenu;
    private readonly ToolStripMenuItem _itemEstado;
    private readonly ToolStripMenuItem _itemSincronizar;
    private readonly ToolStripMenuItem _itemProbarDemo;
    private readonly ToolStripMenuItem _itemAutoStart;
    private readonly ToolStripMenuItem _itemSalir;

    private readonly Icon _appIcon;
    private bool _disposed;

    public TrayApplicationContext(DbConfig? config = null)
    {
        Logger.Info("Iniciando contexto de aplicación en segundo plano...");

        // Capturar el SynchronizationContext del hilo principal de UI y forzar creación de handle auxiliar
        _syncContext = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        _syncControl = new Control();
        _ = _syncControl.Handle; // Garantiza que existe un HWND en el hilo principal antes de iniciar servicios

        // 1. Cargar configuración de base de datos
        _dbConfig = config ?? DbConfig.CargarDesdeArchivo();

        // 2. Inicializar servicios de datos y lógica
        var repository = new EmpleadoRepository(_dbConfig);
        _cacheService = new CacheService();
        _syncService = new SyncService(repository, _cacheService);
        _pauseManager = new PauseManager(_dbConfig.PinEmergencia);
        _scheduler = new PausaScheduler(_syncService, _cacheService, _pauseManager);

        // 3. Crear ícono en memoria con GDI+
        _appIcon = TrayIconHelper.CrearIconoBandeja();

        // 4. Configurar menú contextual del NotifyIcon
        _contextMenu = new ContextMenuStrip();

        _itemEstado = new ToolStripMenuItem("Iniciando servicio...")
        {
            Enabled = false,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold)
        };

        _itemSincronizar = new ToolStripMenuItem("Sincronizar ahora", null, OnSincronizarClick);
        _itemProbarDemo = new ToolStripMenuItem("Probar pausa ahora (Demo)", null, OnProbarDemoClick);

        _itemAutoStart = new ToolStripMenuItem("Iniciar con Windows", null, OnAutoStartClick)
        {
            CheckOnClick = true,
            Checked = AutoStartHelper.EstaHabilitado()
        };

        _itemSalir = new ToolStripMenuItem("Salir", null, OnSalirClick);

        _contextMenu.Items.Add(_itemEstado);
        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add(_itemSincronizar);
        _contextMenu.Items.Add(_itemProbarDemo);
        _contextMenu.Items.Add(_itemAutoStart);
        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add(_itemSalir);

        // 5. Configurar el NotifyIcon
        _notifyIcon = new NotifyIcon
        {
            Icon = _appIcon,
            Text = "Pausas Activas Corporativas",
            ContextMenuStrip = _contextMenu,
            Visible = true
        };

        // 6. Suscribir eventos de ciclo de vida
        _pauseManager.PausaIniciada += OnPausaIniciada;
        _pauseManager.PausaFinalizada += OnPausaFinalizada;
        _syncService.SincronizacionTerminada += OnSincronizacionTerminada;
        _scheduler.EstadoCambiado += ActualizarMenuEstado;

        // 7. Iniciar servicios en segundo plano
        _syncService.Iniciar();
        _scheduler.Iniciar();

        ActualizarMenuEstado();
        Logger.Info("PausasEmpleado iniciado exitosamente en la bandeja del sistema.");
    }

    private void ActualizarMenuEstado()
    {
        if (_disposed) return;

        // Si se llama desde un hilo secundario (ej. temporizador de sincronización),
        // despachar al hilo principal de WinForms usando SynchronizationContext sin depender de handles no creados
        if (_syncContext != null && SynchronizationContext.Current != _syncContext)
        {
            _syncContext.Post(_ => ActualizarMenuEstado(), null);
            return;
        }

        try
        {
            string texto = _scheduler.ObtenerTextoEstado();
            _itemEstado.Text = texto;

            // Limitar texto para evitar desborde del tooltip de Windows (máximo 63 caracteres)
            string textoTooltip = texto.Length > 63 ? texto[..60] + "..." : texto;
            _notifyIcon.Text = $"Pausas Activas\n{textoTooltip}";
        }
        catch (Exception ex)
        {
            Logger.Error("Error al actualizar estado del menú contextual", ex);
        }
    }

    private void OnSincronizacionTerminada(bool exito, string estado)
    {
        ActualizarMenuEstado();
    }

    private void OnPausaIniciada()
    {
        // Deshabilitar opción de salir mientras la pausa esté en pantalla
        _itemSalir.Enabled = false;
        ActualizarMenuEstado();
    }

    private void OnPausaFinalizada(bool completadaNormal)
    {
        // Re-habilitar opción de salir al terminar la pausa
        _itemSalir.Enabled = true;
        ActualizarMenuEstado();
    }

    private async void OnSincronizarClick(object? sender, EventArgs e)
    {
        try
        {
            _itemSincronizar.Enabled = false;
            _itemEstado.Text = "Sincronizando con el servidor...";
            await _syncService.SincronizarAhoraAsync();
        }
        catch (Exception ex)
        {
            Logger.Error("Error al sincronizar manualmente", ex);
        }
        finally
        {
            _itemSincronizar.Enabled = true;
            ActualizarMenuEstado();
        }
    }

    private void OnProbarDemoClick(object? sender, EventArgs e)
    {
        try
        {
            Logger.Info("Prueba manual (Demo) de pausa activa solicitada.");
            _scheduler.DispararPausaInmediata(esDemo: true);
        }
        catch (Exception ex)
        {
            Logger.Error("Error al iniciar prueba demo de pausa activa", ex);
        }
    }

    private void OnAutoStartClick(object? sender, EventArgs e)
    {
        bool habilitar = _itemAutoStart.Checked;
        AutoStartHelper.SetHabilitado(habilitar);
    }

    private void OnSalirClick(object? sender, EventArgs e)
    {
        if (_pauseManager.HayPausaActiva)
        {
            return;
        }

        Logger.Info("Cerrando PausasEmpleado a petición del usuario.");
        _notifyIcon.Visible = false;
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (_disposed) return;
        _disposed = true;

        if (disposing)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _contextMenu.Dispose();
            _appIcon.Dispose();
            _syncControl.Dispose();
            _scheduler.Dispose();
            _syncService.Dispose();
            _pauseManager.Dispose();
        }

        base.Dispose(disposing);
    }
}
