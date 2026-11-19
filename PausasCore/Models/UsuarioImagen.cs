namespace PausasCore.Models;

/// <summary>
/// Representa la asociación n-a-n entre un usuario y una imagen con orden de reproducción.
/// Mapea exactamente a la tabla 'usuario_imagenes'.
/// </summary>
public class UsuarioImagen
{
    public int UsuarioId { get; set; }
    public int ImagenId { get; set; }
    public int Posicion { get; set; }
}
