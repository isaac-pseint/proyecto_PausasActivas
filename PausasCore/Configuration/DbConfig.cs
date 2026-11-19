using System;
using System.IO;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace PausasCore.Configuration;

/// <summary>
/// Configuración de conexión a SQL Server y parámetros de la aplicación.
/// Construye la cadena mediante SqlConnectionStringBuilder y lee desde appsettings.json.
/// </summary>
public class DbConfig
{
    public string DataSource { get; set; } = "db81083.public.databaseasp.net,1433";
    public string InitialCatalog { get; set; } = "db81083";
    public string UserId { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public int ConnectTimeout { get; set; } = 15;
    public bool Encrypt { get; set; } = true;
    public bool TrustServerCertificate { get; set; } = true;
    public string? PinEmergencia { get; set; }

    /// <summary>
    /// Construye la cadena de conexión usando SqlConnectionStringBuilder.
    /// </summary>
    public string ObtenerCadenaConexion()
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = DataSource,
            InitialCatalog = InitialCatalog,
            UserID = UserId,
            Password = Password,
            ConnectTimeout = ConnectTimeout,
            Encrypt = Encrypt,
            TrustServerCertificate = TrustServerCertificate
        };

        return builder.ConnectionString;
    }

    /// <summary>
    /// Crea y devuelve una nueva instancia de SqlConnection usando la configuración actual.
    /// </summary>
    public SqlConnection CrearConexion()
    {
        return new SqlConnection(ObtenerCadenaConexion());
    }

    /// <summary>
    /// Carga la configuración desde un archivo JSON (por defecto 'appsettings.json' en AppContext.BaseDirectory).
    /// Si el archivo no existe, lanza FileNotFoundException con instrucciones detalladas.
    /// </summary>
    public static DbConfig CargarDesdeArchivo(string? rutaArchivo = null)
    {
        string ruta = rutaArchivo ?? Path.Combine(AppContext.BaseDirectory, "appsettings.json");

        if (!File.Exists(ruta))
        {
            throw new FileNotFoundException(
                $"No se encontró el archivo de configuración obligatorio 'appsettings.json' en:\n'{AppContext.BaseDirectory}'.\n\n" +
                "Debe copiar 'appsettings.example.json' a 'appsettings.json' y colocar la contraseña real asignada para la base de datos.",
                ruta);
        }

        string json = File.ReadAllText(ruta);
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        DbConfig config;
        // Soporte para estructura anidada bajo 'Database'
        if (root.TryGetProperty("Database", out var dbProp))
        {
            config = JsonSerializer.Deserialize<DbConfig>(dbProp.GetRawText(), options) ?? new DbConfig();
            if (root.TryGetProperty("PinEmergencia", out var pinProp))
            {
                config.PinEmergencia = pinProp.GetString();
            }
        }
        else
        {
            // Soporte para estructura plana en la raíz
            config = JsonSerializer.Deserialize<DbConfig>(json, options) ?? new DbConfig();
        }

        return config;
    }
}
