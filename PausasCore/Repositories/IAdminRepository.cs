using System.Collections.Generic;
using System.Threading.Tasks;
using PausasCore.Models;

namespace PausasCore.Repositories;

/// <summary>
/// Contrato de repositorio para el panel de administración.
/// Utiliza las credenciales de db81083_pausas_adm con permisos de administración.
/// </summary>
public interface IAdminRepository
{
    // Autenticación de Administrador
    Task<Admin?> ValidarLoginAsync(string usuario, string clavePlana);

    // CRUD de Usuarios
    Task<List<Usuario>> ObtenerTodosUsuariosAsync();
    Task<Usuario?> ObtenerUsuarioPorIdAsync(int id);
    Task<int> CrearUsuarioAsync(Usuario usuario);
    Task<bool> ActualizarUsuarioAsync(Usuario usuario);
    Task<bool> EliminarUsuarioAsync(int id);

    // CRUD de Imágenes
    Task<List<ImagenMetadata>> ObtenerMetadatosTodasImagenesAsync();
    Task<Imagen?> ObtenerImagenPorIdAsync(int id);
    Task<int> CrearImagenAsync(Imagen imagen);
    Task<bool> ActualizarImagenAsync(Imagen imagen);
    Task<bool> EliminarImagenAsync(int id);

    // Asignación de Imágenes a Usuarios (tabla usuario_imagenes)
    Task<List<int>> ObtenerIdsImagenesAsignadasAsync(int usuarioId);
    Task GuardarAsignacionesAsync(int usuarioId, IEnumerable<int> imagenIds);

    // Configuración masiva
    Task AplicarConfiguracionATodosAsync(
        bool activo,
        int intervaloMin,
        int duracionSeg,
        string orden,
        TimeSpan horaInicio,
        TimeSpan horaFin,
        IReadOnlyList<int> imagenIdsOrdenadas);
}
