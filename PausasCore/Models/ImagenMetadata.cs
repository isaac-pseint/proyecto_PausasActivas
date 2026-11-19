using System;

namespace PausasCore.Models;

/// <summary>
/// Metadatos ligeros de una imagen para optimización de ancho de banda.
/// Omite el payload binario (VARBINARY(MAX)) e incluye la posición de orden asignada.
/// </summary>
public class ImagenMetadata
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Categoria { get; set; }
    public string TipoContenido { get; set; } = "image/jpeg";
    public DateTime Actualizado { get; set; } = DateTime.UtcNow;
    public int Posicion { get; set; }
}
