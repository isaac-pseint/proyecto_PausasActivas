using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using PausasCore.Models;
using PausasEmpleado.Helpers;
using PausasEmpleado.Managers;
using PausasEmpleado.Services;

namespace PausasEmpleado.Scheduling;

/// <summary>
/// Planificador de pausas activas. Evalúa cada segundo en el hilo de UI
/// las condiciones de activación, horario laboral, tiempo transcurrido y selección de imagen.
/// </summary>
public class PausaScheduler : IDisposable
{
    private readonly SyncService _syncService;
    private readonly CacheService _cacheService;
    private readonly PauseManager _pauseManager;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly Random _rng = new();

    private DateTime _ultimaPausaUtc;
    private int _indiceSecuencial;
    private int _ultimoIndiceAleatorio = -1;
    private bool _disposed;

    public event Action? EstadoCambiado;

    public PausaScheduler(
        SyncService syncService,
        CacheService cacheService,
        PauseManager pauseManager)
    {
        _syncService = syncService ?? throw new ArgumentNullException(nameof(syncService));
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _pauseManager = pauseManager ?? throw new ArgumentNullException(nameof(pauseManager));

        _ultimaPausaUtc = DateTime.UtcNow;

        _pauseManager.PausaFinalizada += (normal) =>
        {
            _ultimaPausaUtc = DateTime.UtcNow;
            EstadoCambiado?.Invoke();
        };

        _timer = new System.Windows.Forms.Timer
        {
            Interval = 1000
        };
        _timer.Tick += Timer_Tick;
    }

    public void Iniciar()
    {
        _ultimaPausaUtc = DateTime.UtcNow;
        _timer.Start();
        EstadoCambiado?.Invoke();
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        EvaluarPausaProgramada();
    }

    private void EvaluarPausaProgramada()
    {
        if (_pauseManager.HayPausaActiva)
            return;

        var usuario = _syncService.DatosActuales.Usuario;
        if (usuario == null || !usuario.Activo)
            return;

        TimeSpan ahora = DateTime.Now.TimeOfDay;
        if (ahora < usuario.HoraInicio || ahora > usuario.HoraFin)
            return;

        var imagenesDisponibles = ObtenerImagenesDisponibles();
        if (imagenesDisponibles.Count == 0)
            return;

        var transcurrido = DateTime.UtcNow - _ultimaPausaUtc;
        if (transcurrido >= TimeSpan.FromMinutes(usuario.IntervaloMin))
        {
            EjecutarPausa(imagenesDisponibles, usuario.DuracionSeg, usuario.Orden);
        }
    }

    /// <summary>
    /// Dispara inmediatamente una pausa activa (por ejemplo, para demostración en sustentación).
    /// </summary>
    public void DispararPausaInmediata(bool esDemo = false)
    {
        if (_pauseManager.HayPausaActiva)
            return;

        var usuario = _syncService.DatosActuales.Usuario;
        int duracion = (usuario?.DuracionSeg > 0) ? usuario.DuracionSeg : 120;
        string orden = usuario?.Orden ?? "secuencial";

        var imagenesDisponibles = ObtenerImagenesDisponibles();

        // En demo, si no hay imágenes descargadas aún, podemos usar un placeholder
        if (imagenesDisponibles.Count == 0)
        {
            _pauseManager.IniciarPausa(
                null,
                esDemo ? "Pausa de Demostración" : "Pausa Activa",
                "Ergonomía y Salud Laboral",
                duracion);
            return;
        }

        EjecutarPausa(imagenesDisponibles, duracion, orden);
    }

    private void EjecutarPausa(List<ImagenMetadata> imagenes, int duracionSeg, string orden)
    {
        var imagenSeleccionada = SeleccionarSiguienteImagen(imagenes, orden);
        byte[]? datosImagen = _cacheService.LeerImagen(imagenSeleccionada.Id);

        _pauseManager.IniciarPausa(
            datosImagen,
            imagenSeleccionada.Nombre,
            imagenSeleccionada.Categoria,
            duracionSeg);

        _ultimaPausaUtc = DateTime.UtcNow;
        EstadoCambiado?.Invoke();
    }

    private ImagenMetadata SeleccionarSiguienteImagen(List<ImagenMetadata> imagenes, string orden)
    {
        if (imagenes.Count == 1)
        {
            return imagenes[0];
        }

        if (string.Equals(orden, "aleatorio", StringComparison.OrdinalIgnoreCase))
        {
            int index;
            do
            {
                index = _rng.Next(imagenes.Count);
            } while (index == _ultimoIndiceAleatorio && imagenes.Count > 1);

            _ultimoIndiceAleatorio = index;
            return imagenes[index];
        }

        // Secuencial por defecto
        int idx = _indiceSecuencial % imagenes.Count;
        _indiceSecuencial = (idx + 1) % imagenes.Count;
        return imagenes[idx];
    }

    private List<ImagenMetadata> ObtenerImagenesDisponibles()
    {
        return _syncService.DatosActuales.Imagenes
            .Where(m => _cacheService.ExisteImagen(m.Id))
            .ToList();
    }

    /// <summary>
    /// Devuelve un texto descriptivo del estado actual para mostrar en el menú del NotifyIcon.
    /// </summary>
    public string ObtenerTextoEstado()
    {
        if (_pauseManager.HayPausaActiva)
            return "Pausa activa en curso...";

        var usuario = _syncService.DatosActuales.Usuario;
        if (usuario == null)
            return "Inicializando...";

        if (!usuario.Activo)
            return "Pausas desactivadas por administración";

        TimeSpan ahora = DateTime.Now.TimeOfDay;
        if (ahora < usuario.HoraInicio || ahora > usuario.HoraFin)
        {
            return $"Fuera de horario laboral ({usuario.HoraInicio:hh\\:mm} - {usuario.HoraFin:hh\\:mm})";
        }

        var disponibles = ObtenerImagenesDisponibles();
        if (disponibles.Count == 0)
        {
            return _syncService.DatosActuales.SincronizadoConExito
                ? "Sin imágenes asignadas en catálogo"
                : "Sin conexión (usando caché)";
        }

        var tiempoTranscurrido = DateTime.UtcNow - _ultimaPausaUtc;
        var tiempoRestante = TimeSpan.FromMinutes(usuario.IntervaloMin) - tiempoTranscurrido;

        if (tiempoRestante <= TimeSpan.Zero)
        {
            return "Próxima pausa: En cualquier momento";
        }

        DateTime proximaHora = DateTime.Now.Add(tiempoRestante);
        string textoProxima = $"Próxima pausa: {proximaHora:HH:mm}";

        if (!_syncService.DatosActuales.SincronizadoConExito)
        {
            textoProxima += " (Caché local)";
        }

        return textoProxima;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();
        _timer.Dispose();
        GC.SuppressFinalize(this);
    }
}
