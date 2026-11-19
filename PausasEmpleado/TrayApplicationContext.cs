using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
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
/// Administra el ciclo de vida del NotifyIcon, el menú contextual, la sincronización y la programación de pausas.
/// </summary>
public class TrayApplicationContext : ApplicationContext
{
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    private const uint WM_NULL = 0x0000;

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

    // Estado atómico para evitar tocar ToolStripMenuItems desde hilos secundarios
    private volatile string _estadoTexto = "Iniciando servicio...";
    private volatile bool _sincronizando;

    public TrayApplicationContext(DbConfig? config = null)
    {
        Logger.Info("Iniciando contexto de aplicación en segundo plano...");

        // 1. Capturar SynchronizationContext del hilo principal de UI y control auxiliar
        _syncContext = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        _syncControl = new Control();
        _ = _syncControl.Handle;

        // 2. Crear ícono de bandeja en memoria con GDI+
        _appIcon = TrayIconHelper.CrearIconoBandeja();

        // 3. Crear ContextMenuStrip e ítems primero en el hilo principal de UI
        _contextMenu = new ContextMenuStrip();

        _itemEstado = new ToolStripMenuItem(_estadoTexto)
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

        // Actualizar el estado de los ítems al abrir el menú (siempre corre en el hilo de UI)
        _contextMenu.Opening += ContextMenu_Opening;
        _contextMenu.Opened += ContextMenu_Opened;
        _contextMenu.Closed += ContextMenu_Closed;

        // 4. Crear y configurar NotifyIcon en el hilo de UI
        _notifyIcon = new NotifyIcon
        {
            Icon = _appIcon,
            Text = "Pausas Activas Corporativas",
            ContextMenuStrip = _contextMenu,
            Visible = true
        };

        // Manejar MouseUp para desplegar el menú de forma confiable en Windows 11 (incluso en overflow ^)
        _notifyIcon.MouseUp += NotifyIcon_MouseUp;

        // 5. Cargar configuración de base de datos
        _dbConfig = config ?? DbConfig.CargarDesdeArchivo();

        // 6. Inicializar servicios de datos y lógica
        var repository = new EmpleadoRepository(_dbConfig);
        _cacheService = new CacheService();
        _syncService = new SyncService(repository, _cacheService);
        _pauseManager = new PauseManager(_dbConfig.PinEmergencia);
        _scheduler = new PausaScheduler(_syncService, _cacheService, _pauseManager);

        // 7. Suscribir eventos de ciclo de vida
        _pauseManager.PausaIniciada += OnPausaIniciada;
        _pauseManager.PausaFinalizada += OnPausaFinalizada;
        _syncService.SincronizacionTerminada += OnSincronizacionTerminada;
        _scheduler.EstadoCambiado += ActualizarEstado;

        // 8. Iniciar scheduler y sincronización inicial de forma puramente asíncrona sin bloquear el message loop
        _scheduler.Iniciar();
        _ = IniciarSincronizacionInicialAsync();

        ActualizarEstado();
        Logger.Info("PausasEmpleado iniciado exitosamente en la bandeja del sistema.");
    }

    /// <summary>
    /// Maneja el despliegue manual del menú contextual ante clics sobre el ícono de bandeja.
    /// Resuelve la falta de respuesta en Windows 11 dentro del panel de desbordamiento (^).
    /// </summary>
    private void NotifyIcon_MouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right || e.Button == MouseButtons.Left)
        {
            MostrarMenuContextual();
        }
    }

    private void MostrarMenuContextual()
    {
        if (_disposed || _contextMenu == null) return;

        if (_contextMenu.Visible)
        {
            _contextMenu.Close();
            return;
        }

        try
        {
            if (!_contextMenu.IsHandleCreated)
            {
                _ = _contextMenu.Handle;
            }

            // Windows requiere que la ventana sea de primer plano antes de mostrar el menú contextual
            SetForegroundWindow(_contextMenu.Handle);
            _contextMenu.Show(Cursor.Position);
            SetForegroundWindow(_contextMenu.Handle);
        }
        catch (Exception ex)
        {
            Logger.Error("Error al mostrar menú contextual desde NotifyIcon", ex);
        }
    }

    private void ContextMenu_Opening(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        try
        {
            _estadoTexto = _scheduler.ObtenerTextoEstado();
            _itemEstado.Text = _estadoTexto;
            _itemSalir.Enabled = !_pauseManager.HayPausaActiva;
            _itemAutoStart.Checked = AutoStartHelper.EstaHabilitado();
            _itemSincronizar.Enabled = !_sincronizando;
        }
        catch (Exception ex)
        {
            Logger.Error("Error en ContextMenu_Opening", ex);
        }
    }

    private void ContextMenu_Opened(object? sender, EventArgs e)
    {
        try
        {
            if (_contextMenu.IsHandleCreated)
            {
                SetForegroundWindow(_contextMenu.Handle);
            }
        }
        catch { }
    }

    private void ContextMenu_Closed(object? sender, ToolStripDropDownClosedEventArgs e)
    {
        try
        {
            if (_syncControl.IsHandleCreated && !_syncControl.IsDisposed)
            {
                PostMessage(_syncControl.Handle, WM_NULL, IntPtr.Zero, IntPtr.Zero);
            }
        }
        catch { }
    }

    /// <summary>
    /// Sincronización inicial no bloqueante que permite al message loop de WinForms comenzar de inmediato.
    /// </summary>
    private async Task IniciarSincronizacionInicialAsync()
    {
        try
        {
            await Task.Yield();
            _syncService.Iniciar();
        }
        catch (Exception ex)
        {
            Logger.Error("Error en inicio asíncrono de sincronización", ex);
        }
    }

    /// <summary>
    /// Actualiza el estado en memoria y despacha de forma segura al hilo principal de UI.
    /// </summary>
    private void ActualizarEstado()
    {
        if (_disposed) return;

        try
        {
            _estadoTexto = _scheduler.ObtenerTextoEstado();
        }
        catch
        {
            _estadoTexto = "En espera";
        }

        void AplicarUI()
        {
            if (_disposed) return;
            try
            {
                string texto = _estadoTexto;
                string textoTooltip = texto.Length > 63 ? texto[..60] + "..." : texto;
                _notifyIcon.Text = $"Pausas Activas\n{textoTooltip}";

                _itemEstado.Text = texto;
                _itemSalir.Enabled = !_pauseManager.HayPausaActiva;
            }
            catch (Exception ex)
            {
                Logger.Error("Error al refrescar estado de UI", ex);
            }
        }

        if (_syncContext != null && SynchronizationContext.Current != _syncContext)
        {
            _syncContext.Post(_ => AplicarUI(), null);
        }
        else
        {
            AplicarUI();
        }
    }

    private void OnSincronizacionTerminada(bool exito, string estado)
    {
        ActualizarEstado();
    }

    private void OnPausaIniciada()
    {
        ActualizarEstado();
    }

    private void OnPausaFinalizada(bool completadaNormal)
    {
        ActualizarEstado();
    }

    private async void OnSincronizarClick(object? sender, EventArgs e)
    {
        if (_sincronizando) return;
        _sincronizando = true;

        try
        {
            _itemSincronizar.Enabled = false;
            _estadoTexto = "Sincronizando con el servidor...";
            _itemEstado.Text = _estadoTexto;

            await _syncService.SincronizarAhoraAsync();
        }
        catch (Exception ex)
        {
            Logger.Error("Error al sincronizar manualmente", ex);
        }
        finally
        {
            _sincronizando = false;
            _itemSincronizar.Enabled = true;
            ActualizarEstado();
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
            _notifyIcon.MouseUp -= NotifyIcon_MouseUp;
            _notifyIcon.Dispose();

            _contextMenu.Opening -= ContextMenu_Opening;
            _contextMenu.Opened -= ContextMenu_Opened;
            _contextMenu.Closed -= ContextMenu_Closed;
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
