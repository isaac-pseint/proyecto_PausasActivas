using System.Collections.Generic;
using System.Threading.Tasks;
using PausasCore.Models;

namespace PausasCore.Repositories;

/// <summary>
/// Contrato de repositorio para la aplicación de empleado.
/// Cumple estrictamente con los permisos limitados del usuario db81083_pausas_emp.
/// </summary>
public interface IEmpleadoRepository
{
    /// <summary>
    /// Obtiene los datos de configuración del usuario según su cuenta de Windows (ej: PC01\maria).
    /// </summary>
    Task<Usuario?> ObtenerUsuarioPorWindowsAsync(string usuarioWindows);

    /// <summary>
    /// Autorregistra al usuario si no existe (insertando solo usuario_windows, pc, ultima_conexion)
    /// o actualiza únicamente su pc y última conexión.
    /// </summary>
    Task<Usuario> RegistrarOActualizarConexionAsync(string usuarioWindows, string? nombrePc);

    /// <summary>
    /// Actualiza únicamente las columnas pc y ultima_conexion del usuario.
    /// </summary>
    Task ActualizarConexionAsync(int usuarioId, string? nombrePc);

    /// <summary>
    /// Obtiene la lista de metadatos de imágenes para el usuario (sin descargar la columna de datos pesados).
    /// Si el usuario no tiene imágenes asignadas en usuario_imagenes, devuelve todas las del catálogo ordenadas por id.
    /// </summary>
    Task<List<ImagenMetadata>> ObtenerMetadatosImagenesAsync(int usuarioId);

    /// <summary>
    /// Descarga únicamente el payload binario (VARBINARY(MAX)) de una imagen por su ID.
    /// </summary>
    Task<byte[]?> ObtenerDatosImagenAsync(int imagenId);
}
