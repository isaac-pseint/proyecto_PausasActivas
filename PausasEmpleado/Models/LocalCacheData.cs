using System;
using System.Collections.Generic;
using PausasCore.Models;

namespace PausasEmpleado.Models;

/// <summary>
/// Representa la estructura guardada en el archivo local config.json.
/// Almacena la última configuración del usuario y los metadatos de imágenes disponibles en caché.
/// </summary>
public class LocalCacheData
{
    public Usuario? Usuario { get; set; }
    public List<ImagenMetadata> Imagenes { get; set; } = new();
    public DateTime UltimaSincronizacionUtc { get; set; } = DateTime.MinValue;
    public bool SincronizadoConExito { get; set; }
}
