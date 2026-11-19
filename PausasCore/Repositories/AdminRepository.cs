using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using PausasCore.Configuration;
using PausasCore.Models;
using PausasCore.Security;

namespace PausasCore.Repositories;

/// <summary>
/// Implementación de IAdminRepository para db81083_pausas_adm.
/// Gestiona operaciones administrativas completas:
/// - SELECT en admins para autenticación.
/// - CRUD de usuarios e imágenes (actualizando siempre la columna 'actualizado').
/// - Gestión transaccional de asignaciones de imágenes a usuarios.
/// </summary>
public class AdminRepository : IAdminRepository
{
    private readonly string _connectionString;

    public AdminRepository(DbConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _connectionString = config.ObtenerCadenaConexion();
    }

    public AdminRepository(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("La cadena de conexión no puede estar vacía.", nameof(connectionString));
        _connectionString = connectionString;
    }

    #region Autenticación de Administrador

    public async Task<Admin?> ValidarLoginAsync(string usuario, string clavePlana)
    {
        if (string.IsNullOrWhiteSpace(usuario) || string.IsNullOrWhiteSpace(clavePlana))
            return null;

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        const string sql = @"
            SELECT id, usuario, nombre, clave_hash, creado
            FROM admins
            WHERE usuario = @usuario;";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.Add(new SqlParameter("@usuario", SqlDbType.NVarChar, 50) { Value = usuario });

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        var admin = new Admin
        {
            Id = reader.GetInt32(0),
            Usuario = reader.GetString(1),
            Nombre = reader.IsDBNull(2) ? null : reader.GetString(2),
            ClaveHash = reader.GetString(3),
            Creado = reader.GetDateTime(4)
        };

        if (PasswordHasher.Verificar(clavePlana, admin.ClaveHash))
        {
            return admin;
        }

        return null;
    }

    #endregion

    #region CRUD de Usuarios

    public async Task<List<Usuario>> ObtenerTodosUsuariosAsync()
    {
        var lista = new List<Usuario>();

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        const string sql = @"
            SELECT id, usuario_windows, nombre, pc, activo, intervalo_min, duracion_seg, orden, hora_inicio, hora_fin, actualizado, ultima_conexion
            FROM usuarios
            ORDER BY nombre ASC, usuario_windows ASC;";

        await using var cmd = new SqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            lista.Add(MapearUsuario(reader));
        }

        return lista;
    }

    public async Task<Usuario?> ObtenerUsuarioPorIdAsync(int id)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        const string sql = @"
            SELECT id, usuario_windows, nombre, pc, activo, intervalo_min, duracion_seg, orden, hora_inicio, hora_fin, actualizado, ultima_conexion
            FROM usuarios
            WHERE id = @id;";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.Add(new SqlParameter("@id", SqlDbType.Int) { Value = id });

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return MapearUsuario(reader);
        }

        return null;
    }

    public async Task<int> CrearUsuarioAsync(Usuario usuario)
    {
        ArgumentNullException.ThrowIfNull(usuario);

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        const string sql = @"
            INSERT INTO usuarios (usuario_windows, nombre, pc, activo, intervalo_min, duracion_seg, orden, hora_inicio, hora_fin, actualizado)
            OUTPUT INSERTED.id
            VALUES (@usuario_windows, @nombre, @pc, @activo, @intervalo_min, @duracion_seg, @orden, @hora_inicio, @hora_fin, SYSUTCDATETIME());";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.Add(new SqlParameter("@usuario_windows", SqlDbType.NVarChar, 100) { Value = usuario.UsuarioWindows });
        cmd.Parameters.Add(new SqlParameter("@nombre", SqlDbType.NVarChar, 100) { Value = (object?)usuario.Nombre ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@pc", SqlDbType.NVarChar, 100) { Value = (object?)usuario.Pc ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@activo", SqlDbType.Bit) { Value = usuario.Activo });
        cmd.Parameters.Add(new SqlParameter("@intervalo_min", SqlDbType.Int) { Value = usuario.IntervaloMin });
        cmd.Parameters.Add(new SqlParameter("@duracion_seg", SqlDbType.Int) { Value = usuario.DuracionSeg });
        cmd.Parameters.Add(new SqlParameter("@orden", SqlDbType.NVarChar, 12) { Value = usuario.Orden });
        cmd.Parameters.Add(new SqlParameter("@hora_inicio", SqlDbType.Time) { Value = usuario.HoraInicio });
        cmd.Parameters.Add(new SqlParameter("@hora_fin", SqlDbType.Time) { Value = usuario.HoraFin });

        var nuevoId = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(nuevoId);
    }

    public async Task<bool> ActualizarUsuarioAsync(Usuario usuario)
    {
        ArgumentNullException.ThrowIfNull(usuario);

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        const string sql = @"
            UPDATE usuarios
            SET usuario_windows = @usuario_windows,
                nombre = @nombre,
                pc = @pc,
                activo = @activo,
                intervalo_min = @intervalo_min,
                duracion_seg = @duracion_seg,
                orden = @orden,
                hora_inicio = @hora_inicio,
                hora_fin = @hora_fin,
                actualizado = SYSUTCDATETIME()
            WHERE id = @id;";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.Add(new SqlParameter("@id", SqlDbType.Int) { Value = usuario.Id });
        cmd.Parameters.Add(new SqlParameter("@usuario_windows", SqlDbType.NVarChar, 100) { Value = usuario.UsuarioWindows });
        cmd.Parameters.Add(new SqlParameter("@nombre", SqlDbType.NVarChar, 100) { Value = (object?)usuario.Nombre ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@pc", SqlDbType.NVarChar, 100) { Value = (object?)usuario.Pc ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@activo", SqlDbType.Bit) { Value = usuario.Activo });
        cmd.Parameters.Add(new SqlParameter("@intervalo_min", SqlDbType.Int) { Value = usuario.IntervaloMin });
        cmd.Parameters.Add(new SqlParameter("@duracion_seg", SqlDbType.Int) { Value = usuario.DuracionSeg });
        cmd.Parameters.Add(new SqlParameter("@orden", SqlDbType.NVarChar, 12) { Value = usuario.Orden });
        cmd.Parameters.Add(new SqlParameter("@hora_inicio", SqlDbType.Time) { Value = usuario.HoraInicio });
        cmd.Parameters.Add(new SqlParameter("@hora_fin", SqlDbType.Time) { Value = usuario.HoraFin });

        int rows = await cmd.ExecuteNonQueryAsync();
        return rows > 0;
    }

    public async Task<bool> EliminarUsuarioAsync(int id)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        const string sql = "DELETE FROM usuarios WHERE id = @id;";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.Add(new SqlParameter("@id", SqlDbType.Int) { Value = id });

        int rows = await cmd.ExecuteNonQueryAsync();
        return rows > 0;
    }

    #endregion

    #region CRUD de Imágenes

    public async Task<List<ImagenMetadata>> ObtenerMetadatosTodasImagenesAsync()
    {
        var lista = new List<ImagenMetadata>();

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        const string sql = @"
            SELECT id, nombre, categoria, tipo_contenido, actualizado, 0 AS posicion
            FROM imagenes
            ORDER BY categoria ASC, nombre ASC;";

        await using var cmd = new SqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            lista.Add(new ImagenMetadata
            {
                Id = reader.GetInt32(0),
                Nombre = reader.GetString(1),
                Categoria = reader.IsDBNull(2) ? null : reader.GetString(2),
                TipoContenido = reader.GetString(3),
                Actualizado = reader.GetDateTime(4),
                Posicion = reader.GetInt32(5)
            });
        }

        return lista;
    }

    public async Task<Imagen?> ObtenerImagenPorIdAsync(int id)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        const string sql = @"
            SELECT id, nombre, categoria, tipo_contenido, datos, actualizado
            FROM imagenes
            WHERE id = @id;";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.Add(new SqlParameter("@id", SqlDbType.Int) { Value = id });

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new Imagen
            {
                Id = reader.GetInt32(0),
                Nombre = reader.GetString(1),
                Categoria = reader.IsDBNull(2) ? null : reader.GetString(2),
                TipoContenido = reader.GetString(3),
                Datos = (byte[])reader["datos"],
                Actualizado = reader.GetDateTime(5)
            };
        }

        return null;
    }

    public async Task<int> CrearImagenAsync(Imagen imagen)
    {
        ArgumentNullException.ThrowIfNull(imagen);

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        const string sql = @"
            INSERT INTO imagenes (nombre, categoria, tipo_contenido, datos, actualizado)
            OUTPUT INSERTED.id
            VALUES (@nombre, @categoria, @tipo_contenido, @datos, SYSUTCDATETIME());";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.Add(new SqlParameter("@nombre", SqlDbType.NVarChar, 100) { Value = imagen.Nombre });
        cmd.Parameters.Add(new SqlParameter("@categoria", SqlDbType.NVarChar, 50) { Value = (object?)imagen.Categoria ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@tipo_contenido", SqlDbType.NVarChar, 50) { Value = imagen.TipoContenido });
        cmd.Parameters.Add(new SqlParameter("@datos", SqlDbType.VarBinary, -1) { Value = imagen.Datos });

        var nuevoId = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(nuevoId);
    }

    public async Task<bool> ActualizarImagenAsync(Imagen imagen)
    {
        ArgumentNullException.ThrowIfNull(imagen);

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        string sql;
        if (imagen.Datos != null && imagen.Datos.Length > 0)
        {
            sql = @"
                UPDATE imagenes
                SET nombre = @nombre,
                    categoria = @categoria,
                    tipo_contenido = @tipo_contenido,
                    datos = @datos,
                    actualizado = SYSUTCDATETIME()
                WHERE id = @id;";
        }
        else
        {
            sql = @"
                UPDATE imagenes
                SET nombre = @nombre,
                    categoria = @categoria,
                    tipo_contenido = @tipo_contenido,
                    actualizado = SYSUTCDATETIME()
                WHERE id = @id;";
        }

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.Add(new SqlParameter("@id", SqlDbType.Int) { Value = imagen.Id });
        cmd.Parameters.Add(new SqlParameter("@nombre", SqlDbType.NVarChar, 100) { Value = imagen.Nombre });
        cmd.Parameters.Add(new SqlParameter("@categoria", SqlDbType.NVarChar, 50) { Value = (object?)imagen.Categoria ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@tipo_contenido", SqlDbType.NVarChar, 50) { Value = imagen.TipoContenido });
        if (imagen.Datos != null && imagen.Datos.Length > 0)
        {
            cmd.Parameters.Add(new SqlParameter("@datos", SqlDbType.VarBinary, -1) { Value = imagen.Datos });
        }

        int rows = await cmd.ExecuteNonQueryAsync();
        return rows > 0;
    }

    public async Task<bool> EliminarImagenAsync(int id)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        const string sql = "DELETE FROM imagenes WHERE id = @id;";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.Add(new SqlParameter("@id", SqlDbType.Int) { Value = id });

        int rows = await cmd.ExecuteNonQueryAsync();
        return rows > 0;
    }

    #endregion

    #region Asignaciones (usuario_imagenes)

    public async Task<List<int>> ObtenerIdsImagenesAsignadasAsync(int usuarioId)
    {
        var lista = new List<int>();

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        const string sql = @"
            SELECT imagen_id
            FROM usuario_imagenes
            WHERE usuario_id = @usuario_id
            ORDER BY posicion ASC;";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.Add(new SqlParameter("@usuario_id", SqlDbType.Int) { Value = usuarioId });

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            lista.Add(reader.GetInt32(0));
        }

        return lista;
    }

    public async Task GuardarAsignacionesAsync(int usuarioId, IEnumerable<int> imagenIds)
    {
        ArgumentNullException.ThrowIfNull(imagenIds);

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var trans = (SqlTransaction)await conn.BeginTransactionAsync();
        try
        {
            // 1. DELETE asignaciones previas
            const string sqlDelete = "DELETE FROM usuario_imagenes WHERE usuario_id = @usuario_id;";
            await using (var cmdDelete = new SqlCommand(sqlDelete, conn, trans))
            {
                cmdDelete.Parameters.Add(new SqlParameter("@usuario_id", SqlDbType.Int) { Value = usuarioId });
                await cmdDelete.ExecuteNonQueryAsync();
            }

            // 2. INSERT con posición ordenada
            const string sqlInsert = @"
                INSERT INTO usuario_imagenes (usuario_id, imagen_id, posicion)
                VALUES (@usuario_id, @imagen_id, @posicion);";

            int posicion = 0;
            foreach (int imagenId in imagenIds)
            {
                await using var cmdInsert = new SqlCommand(sqlInsert, conn, trans);
                cmdInsert.Parameters.Add(new SqlParameter("@usuario_id", SqlDbType.Int) { Value = usuarioId });
                cmdInsert.Parameters.Add(new SqlParameter("@imagen_id", SqlDbType.Int) { Value = imagenId });
                cmdInsert.Parameters.Add(new SqlParameter("@posicion", SqlDbType.Int) { Value = posicion++ });
                await cmdInsert.ExecuteNonQueryAsync();
            }

            // 3. UPDATE usuarios SET actualizado = SYSUTCDATETIME()
            const string sqlUpdateUser = @"
                UPDATE usuarios
                SET actualizado = SYSUTCDATETIME()
                WHERE id = @usuario_id;";

            await using (var cmdUpdateUser = new SqlCommand(sqlUpdateUser, conn, trans))
            {
                cmdUpdateUser.Parameters.Add(new SqlParameter("@usuario_id", SqlDbType.Int) { Value = usuarioId });
                await cmdUpdateUser.ExecuteNonQueryAsync();
            }

            await trans.CommitAsync();
        }
        catch
        {
            await trans.RollbackAsync();
            throw;
        }
    }

    #endregion

    private static Usuario MapearUsuario(SqlDataReader reader)
    {
        return new Usuario
        {
            Id = reader.GetInt32(0),
            UsuarioWindows = reader.GetString(1),
            Nombre = reader.IsDBNull(2) ? null : reader.GetString(2),
            Pc = reader.IsDBNull(3) ? null : reader.GetString(3),
            Activo = reader.GetBoolean(4),
            IntervaloMin = reader.GetInt32(5),
            DuracionSeg = reader.GetInt32(6),
            Orden = reader.GetString(7),
            HoraInicio = reader.GetTimeSpan(8),
            HoraFin = reader.GetTimeSpan(9),
            Actualizado = reader.GetDateTime(10),
            UltimaConexion = reader.IsDBNull(11) ? null : reader.GetDateTime(11)
        };
    }
}
