using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PausasCore.Models;
using PausasCore.Repositories;
using PausasEmpleado.Helpers;
using PausasEmpleado.Models;

namespace PausasEmpleado.Services;

/// <summary>
/// Servicio de sincronización en segundo plano con SQL Server (MonsterASP.NET).
/// Ejecuta la sincronización al iniciar y periódicamente cada 5 minutos.
/// </summary>
public class SyncService : IDisposable
{
    private readonly IEmpleadoRepository _repository;
    private readonly CacheService _cacheService;
    private readonly System.Threading.Timer _timer;
    private readonly SemaphoreSlim _syncLock = new(1, 1);
    private bool _disposed;

    public string UsuarioWindows { get; }
    public string NombrePc { get; }

    public LocalCacheData DatosActuales { get; private set; }

    /// <summary>
    /// Evento disparado al finalizar una sincronización. Parámetros: (bool exito, string estado).
    /// </summary>
    public event Action<bool, string>? SincronizacionTerminada;

    public SyncService(IEmpleadoRepository repository, CacheService cacheService)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));

        UsuarioWindows = $@"{Environment.UserDomainName}\{Environment.UserName}";
        NombrePc = Environment.MachineName;

        // Cargar lo que tengamos en caché local inicialmente
        DatosActuales = _cacheService.CargarConfig() ?? new LocalCacheData
        {
            Usuario = new Usuario
            {
                UsuarioWindows = UsuarioWindows,
                Pc = NombrePc,
                Activo = true,
                IntervaloMin = 60,
                DuracionSeg = 120,
                Orden = "secuencial",
                HoraInicio = new TimeSpan(8, 0, 0),
                HoraFin = new TimeSpan(17, 0, 0)
            }
        };

        // Temporizador de 5 minutos (300,000 ms) sin disparo inicial (el inicio se hace explícito)
        _timer = new System.Threading.Timer(OnTimerCallback, null, Timeout.Infinite, Timeout.Infinite);
    }

    public void Iniciar()
    {
        // Disparar sincronización inmediata en segundo plano
        _ = SincronizarAhoraAsync();

        // Configurar intervalo de 5 minutos
        _timer.Change(TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    private void OnTimerCallback(object? state)
    {
        _ = SincronizarAhoraAsync();
    }

    /// <summary>
    /// Ejecuta una sincronización completa en segundo plano.
    /// Es seguro llamarlo concurrentemente gracias a _syncLock.
    /// </summary>
    public async Task SincronizarAhoraAsync()
    {
        if (!await _syncLock.WaitAsync(0))
        {
            Logger.Info("Sincronización omitida: ya hay una en curso.");
            return;
        }

        bool exito = false;
        string estado;

        try
        {
            Logger.Info($"Iniciando sincronización para '{UsuarioWindows}' en PC '{NombrePc}'...");

            // 1. Autorregistro o actualización de conexión en SQL Server
            var usuarioDb = await _repository.RegistrarOActualizarConexionAsync(UsuarioWindows, NombrePc);

            // 2. Obtención de metadatos de imágenes
            var metadatosNuevos = await _repository.ObtenerMetadatosImagenesAsync(usuarioDb.Id);

            // 3. Diccionario de imágenes existentes en caché para comparar timestamps
            var mapaCacheActual = DatosActuales.Imagenes.ToDictionary(i => i.Id, i => i.Actualizado);

            // 4. Descargar imágenes nuevas o con 'actualizado' distinto
            foreach (var meta in metadatosNuevos)
            {
                bool necesitaDescargar = !_cacheService.ExisteImagen(meta.Id) ||
                                         !mapaCacheActual.TryGetValue(meta.Id, out var fechaCache) ||
                                         fechaCache != meta.Actualizado;

                if (necesitaDescargar)
                {
                    Logger.Info($"Descargando imagen ID {meta.Id} ('{meta.Nombre}')...");
                    byte[]? datos = await _repository.ObtenerDatosImagenAsync(meta.Id);
                    if (datos != null && datos.Length > 0)
                    {
                        _cacheService.GuardarImagen(meta.Id, datos);
                    }
                    else
                    {
                        Logger.Advertencia($"No se recibieron datos para la imagen ID {meta.Id}");
                    }
                }
            }

            // 5. Eliminar del disco local las imágenes que ya no están asignadas
            var idsAsignados = metadatosNuevos.Select(m => m.Id).ToList();
            _cacheService.LimpiarImagenesObsoletas(idsAsignados);

            // 6. Actualizar estructura en memoria y guardar en config.json
            DatosActuales = new LocalCacheData
            {
                Usuario = usuarioDb,
                Imagenes = metadatosNuevos,
                UltimaSincronizacionUtc = DateTime.UtcNow,
                SincronizadoConExito = true
            };
            _cacheService.GuardarConfig(DatosActuales);

            exito = true;
            estado = $"Sincronizado: {DateTime.Now:hh:mm tt}";
            Logger.Info($"Sincronización exitosa. {metadatosNuevos.Count} imágenes en catálogo.");
        }
        catch (Exception ex)
        {
            // Falla de red o SQL Server: silencioso para el usuario corporativo, registro en app.log
            Logger.Error("Error durante la sincronización con el servidor remoto. Usando caché local.", ex);

            // Mantener datos de caché previa
            var cachePrevia = _cacheService.CargarConfig();
            if (cachePrevia != null)
            {
                DatosActuales = cachePrevia;
                DatosActuales.SincronizadoConExito = false;
            }

            exito = false;
            estado = "Sin conexión (usando caché)";
        }
        finally
        {
            _syncLock.Release();
        }

        SincronizacionTerminada?.Invoke(exito, estado);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Dispose();
        _syncLock.Dispose();
        GC.SuppressFinalize(this);
    }
}
