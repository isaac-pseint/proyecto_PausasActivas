using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using PausasCore.Configuration;
using PausasCore.Models;

namespace PausasCore.Repositories;

/// <summary>
/// Implementación de IEmpleadoRepository con Microsoft.Data.SqlClient.
/// Respeta rigurosamente los permisos del usuario SQL de empleados:
/// - Solo SELECT en usuarios, imagenes, usuario_imagenes.
/// - INSERT en usuarios únicamente con (usuario_windows, pc, ultima_conexion).
/// - UPDATE en usuarios únicamente con pc y ultima_conexion.
/// - Ninguna consulta a la tabla admins.
/// </summary>
public class EmpleadoRepository : IEmpleadoRepository
{
    private readonly string _connectionString;

    public EmpleadoRepository(DbConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _connectionString = config.ObtenerCadenaConexion();
    }

    public EmpleadoRepository(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("La cadena de conexión no puede estar vacía.", nameof(connectionString));
        _connectionString = connectionString;
    }

    public async Task<Usuario?> ObtenerUsuarioPorWindowsAsync(string usuarioWindows)
    {
        if (string.IsNullOrWhiteSpace(usuarioWindows))
            return null;

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        const string sql = @"
            SELECT id, usuario_windows, nombre, pc, activo, intervalo_min, duracion_seg, orden, hora_inicio, hora_fin, actualizado, ultima_conexion
            FROM usuarios
            WHERE usuario_windows = @usuario_windows;";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.Add(new SqlParameter("@usuario_windows", SqlDbType.NVarChar, 100) { Value = usuarioWindows });

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return MapearUsuario(reader);
        }

        return null;
    }

    public async Task<Usuario> RegistrarOActualizarConexionAsync(string usuarioWindows, string? nombrePc)
    {
        if (string.IsNullOrWhiteSpace(usuarioWindows))
            throw new ArgumentException("El usuario de Windows no puede estar vacío.", nameof(usuarioWindows));

        var existente = await ObtenerUsuarioPorWindowsAsync(usuarioWindows);
        if (existente != null)
        {
            // Solo actualiza pc y ultima_conexion
            await ActualizarConexionAsync(existente.Id, nombrePc);
            existente.Pc = nombrePc;
            existente.UltimaConexion = DateTime.UtcNow;
            return existente;
        }

        // Si no existe, autorregistra insertando ÚNICAMENTE usuario_windows, pc, ultima_conexion
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        const string sqlInsert = @"
            INSERT INTO usuarios (usuario_windows, pc, ultima_conexion)
            VALUES (@usuario_windows, @pc, SYSUTCDATETIME());";

        await using (var cmd = new SqlCommand(sqlInsert, conn))
        {
            cmd.Parameters.Add(new SqlParameter("@usuario_windows", SqlDbType.NVarChar, 100) { Value = usuarioWindows });
            cmd.Parameters.Add(new SqlParameter("@pc", SqlDbType.NVarChar, 100) { Value = (object?)nombrePc ?? DBNull.Value });
            await cmd.ExecuteNonQueryAsync();
        }

        // Recuperar el registro completo con los valores por defecto del esquema SQL
        var nuevo = await ObtenerUsuarioPorWindowsAsync(usuarioWindows);
        return nuevo ?? throw new InvalidOperationException($"No se pudo recuperar el usuario '{usuarioWindows}' recién registrado.");
    }

    public async Task ActualizarConexionAsync(int usuarioId, string? nombrePc)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        // IMPORTANTE: Solo actualizar pc y ultima_conexion.
        // NUNCA incluir otras columnas como actualizado o SQL Server rechazará la operación por permisos.
        const string sql = @"
            UPDATE usuarios
            SET pc = @pc, ultima_conexion = SYSUTCDATETIME()
            WHERE id = @id;";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.Add(new SqlParameter("@id", SqlDbType.Int) { Value = usuarioId });
        cmd.Parameters.Add(new SqlParameter("@pc", SqlDbType.NVarChar, 100) { Value = (object?)nombrePc ?? DBNull.Value });

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<ImagenMetadata>> ObtenerMetadatosImagenesAsync(int usuarioId)
    {
        var lista = new List<ImagenMetadata>();

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        // 1. Consultar si existen imágenes asignadas específicamente a este usuario
        const string sqlAsignadas = @"
            SELECT i.id, i.nombre, i.categoria, i.tipo_contenido, i.actualizado, ui.posicion
            FROM imagenes i
            INNER JOIN usuario_imagenes ui ON i.id = ui.imagen_id
            WHERE ui.usuario_id = @usuario_id
            ORDER BY ui.posicion ASC, i.id ASC;";

        await using (var cmd = new SqlCommand(sqlAsignadas, conn))
        {
            cmd.Parameters.Add(new SqlParameter("@usuario_id", SqlDbType.Int) { Value = usuarioId });
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
        }

        // 2. Si no tiene asignaciones particulares, devolver todas las imágenes del catálogo ordenadas por id
        if (lista.Count == 0)
        {
            const string sqlTodas = @"
                SELECT id, nombre, categoria, tipo_contenido, actualizado, 0 AS posicion
                FROM imagenes
                ORDER BY id ASC;";

            await using var cmdTodas = new SqlCommand(sqlTodas, conn);
            await using var reader = await cmdTodas.ExecuteReaderAsync();
            int pos = 0;
            while (await reader.ReadAsync())
            {
                lista.Add(new ImagenMetadata
                {
                    Id = reader.GetInt32(0),
                    Nombre = reader.GetString(1),
                    Categoria = reader.IsDBNull(2) ? null : reader.GetString(2),
                    TipoContenido = reader.GetString(3),
                    Actualizado = reader.GetDateTime(4),
                    Posicion = pos++
                });
            }
        }

        return lista;
    }

    public async Task<byte[]?> ObtenerDatosImagenAsync(int imagenId)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        const string sql = "SELECT datos FROM imagenes WHERE id = @id;";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.Add(new SqlParameter("@id", SqlDbType.Int) { Value = imagenId });

        var resultado = await cmd.ExecuteScalarAsync();
        if (resultado == null || resultado == DBNull.Value)
            return null;

        return (byte[])resultado;
    }

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
