using System;
using System.IO;

namespace PausasAdmin.Helpers;

/// <summary>
/// Registrador de eventos en disco para PausasAdmin.
/// Escribe en %LocalAppData%\PausasActivas\admin.log con rotación automática a los 2 MB.
/// </summary>
public static class Logger
{
    private static readonly object _lock = new();
    private static readonly string _logDirectory;
    private static readonly string _logFilePath;
    private const long MaxSizeBytes = 2 * 1024 * 1024; // 2 MB

    static Logger()
    {
        try
        {
            _logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PausasActivas");

            if (!Directory.Exists(_logDirectory))
            {
                Directory.CreateDirectory(_logDirectory);
            }

            _logFilePath = Path.Combine(_logDirectory, "admin.log");
        }
        catch
        {
            _logDirectory = AppContext.BaseDirectory;
            _logFilePath = Path.Combine(_logDirectory, "admin.log");
        }
    }

    public static void Info(string mensaje)
    {
        Escribir("INFO", mensaje);
    }

    public static void Advertencia(string mensaje)
    {
        Escribir("WARN", mensaje);
    }

    public static void Error(string mensaje, Exception? ex = null)
    {
        string detalle = ex != null ? $"{mensaje} -> Detalle: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}" : mensaje;
        Escribir("ERROR", detalle);
    }

    private static void Escribir(string nivel, string mensaje)
    {
        try
        {
            lock (_lock)
            {
                RotarSiEsNecesario();
                string linea = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{nivel}] {mensaje}{Environment.NewLine}";
                File.AppendAllText(_logFilePath, linea);
            }
        }
        catch
        {
            // Silencioso por diseño para no interrumpir el flujo del usuario
        }
    }

    private static void RotarSiEsNecesario()
    {
        try
        {
            if (File.Exists(_logFilePath))
            {
                var fi = new FileInfo(_logFilePath);
                if (fi.Length > MaxSizeBytes)
                {
                    string oldFile = Path.Combine(_logDirectory, "admin.log.old");
                    if (File.Exists(oldFile))
                    {
                        File.Delete(oldFile);
                    }
                    File.Move(_logFilePath, oldFile);
                }
            }
        }
        catch
        {
            // Silencioso
        }
    }
}
