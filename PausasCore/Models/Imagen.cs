using System;

namespace PausasCore.Models;

/// <summary>
/// Representa una imagen con su payload binario completo.
/// Mapea exactamente a la tabla 'imagenes'.
/// </summary>
public class Imagen
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Categoria { get; set; }
    public string TipoContenido { get; set; } = "image/jpeg";
    public byte[] Datos { get; set; } = Array.Empty<byte>();
    public DateTime Actualizado { get; set; } = DateTime.UtcNow;
}
