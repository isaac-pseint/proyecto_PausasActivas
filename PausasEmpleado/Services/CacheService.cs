using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using PausasEmpleado.Helpers;
using PausasEmpleado.Models;

namespace PausasEmpleado.Services;

/// <summary>
/// Gestiona la caché local en %LocalAppData%\PausasActivas\cache\
/// Almacena config.json y las imágenes descargadas img_{id}.bin.
/// </summary>
public class CacheService
{
    private readonly string _cacheDirectory;
    private readonly string _imagenesDirectory;
    private readonly string _configFilePath;
    private readonly JsonSerializerOptions _jsonOptions;

    public CacheService()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _cacheDirectory = Path.Combine(localAppData, "PausasActivas", "cache");
        _imagenesDirectory = Path.Combine(_cacheDirectory, "imagenes");
        _configFilePath = Path.Combine(_cacheDirectory, "config.json");

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        AsegurarDirectorios();
    }

    private void AsegurarDirectorios()
    {
        try
        {
            if (!Directory.Exists(_cacheDirectory))
                Directory.CreateDirectory(_cacheDirectory);

            if (!Directory.Exists(_imagenesDirectory))
                Directory.CreateDirectory(_imagenesDirectory);
        }
        catch (Exception ex)
        {
            Logger.Error("Error al inicializar directorios de caché local", ex);
        }
    }

    public LocalCacheData? CargarConfig()
    {
        try
        {
            if (!File.Exists(_configFilePath))
                return null;

            string json = File.ReadAllText(_configFilePath);
            return JsonSerializer.Deserialize<LocalCacheData>(json, _jsonOptions);
        }
        catch (Exception ex)
        {
            Logger.Error("Error al leer config.json de la caché local", ex);
            return null;
        }
    }

    public void GuardarConfig(LocalCacheData data)
    {
        try
        {
            AsegurarDirectorios();
            string json = JsonSerializer.Serialize(data, _jsonOptions);
            File.WriteAllText(_configFilePath, json);
        }
        catch (Exception ex)
        {
            Logger.Error("Error al guardar config.json en la caché local", ex);
        }
    }

    public bool ExisteImagen(int id)
    {
        string ruta = ObtenerRutaImagen(id);
        return File.Exists(ruta) && new FileInfo(ruta).Length > 0;
    }

    public byte[]? LeerImagen(int id)
    {
        try
        {
            string ruta = ObtenerRutaImagen(id);
            if (File.Exists(ruta))
            {
                return File.ReadAllBytes(ruta);
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Error al leer la imagen {id} de caché", ex);
        }
        return null;
    }

    public void GuardarImagen(int id, byte[] datos)
    {
        try
        {
            AsegurarDirectorios();
            string ruta = ObtenerRutaImagen(id);
            File.WriteAllBytes(ruta, datos);
        }
        catch (Exception ex)
        {
            Logger.Error($"Error al guardar la imagen {id} en disco", ex);
        }
    }

    public void EliminarImagen(int id)
    {
        try
        {
            string ruta = ObtenerRutaImagen(id);
            if (File.Exists(ruta))
            {
                File.Delete(ruta);
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Error al eliminar la imagen {id} obsoleta", ex);
        }
    }

    /// <summary>
    /// Elimina del disco todas las imágenes que ya no forman parte de la lista válida asignada.
    /// </summary>
    public void LimpiarImagenesObsoletas(IEnumerable<int> idsValidos)
    {
        try
        {
            if (!Directory.Exists(_imagenesDirectory))
                return;

            var validosSet = new HashSet<int>(idsValidos);
            var archivos = Directory.GetFiles(_imagenesDirectory, "img_*.bin");

            foreach (var archivo in archivos)
            {
                string nombre = Path.GetFileNameWithoutExtension(archivo); // ej: "img_5"
                if (nombre.StartsWith("img_") && int.TryParse(nombre[4..], out int id))
                {
                    if (!validosSet.Contains(id))
                    {
                        try
                        {
                            File.Delete(archivo);
                            Logger.Info($"Imagen obsoleta eliminada de caché: {archivo}");
                        }
                        catch (Exception ex)
                        {
                            Logger.Advertencia($"No se pudo eliminar {archivo}: {ex.Message}");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Error al limpiar imágenes obsoletas de caché", ex);
        }
    }

    public List<int> ObtenerIdsImagenesDisponibles()
    {
        var lista = new List<int>();
        try
        {
            if (Directory.Exists(_imagenesDirectory))
            {
                var archivos = Directory.GetFiles(_imagenesDirectory, "img_*.bin");
                foreach (var archivo in archivos)
                {
                    string nombre = Path.GetFileNameWithoutExtension(archivo);
                    if (nombre.StartsWith("img_") && int.TryParse(nombre[4..], out int id))
                    {
                        if (new FileInfo(archivo).Length > 0)
                        {
                            lista.Add(id);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Error al listar imágenes en caché", ex);
        }
        return lista;
    }

    public string ObtenerRutaImagen(int id)
    {
        return Path.Combine(_imagenesDirectory, $"img_{id}.bin");
    }
}
