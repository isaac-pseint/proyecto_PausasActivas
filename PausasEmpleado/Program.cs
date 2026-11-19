using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using PausasCore.Configuration;
using PausasEmpleado.Helpers;

namespace PausasEmpleado;

internal static class Program
{
    private const string MutexName = @"Global\PausasActivasEmpleado_SingleInstance";

    /// <summary>
    /// Punto de entrada principal de la aplicación de empleado.
    /// Configura manejadores globales de excepciones, valida la configuración y garantiza instancia única.
    /// </summary>
    [STAThread]
    static void Main()
    {
        // 1. Manejadores globales de excepciones para evitar cierres silenciosos
        ConfigurarManejadoresGlobales();

        // 2. Garantizar instancia única global por máquina Windows
        using var mutex = new Mutex(true, MutexName, out bool creadoNuevo);
        if (!creadoNuevo)
        {
            // Cierre silencioso si ya hay una instancia en ejecución
            return;
        }

        try
        {
            ApplicationConfiguration.Initialize();

            // 3. Validar existencia de appsettings.json
            string rutaConfig = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (!File.Exists(rutaConfig))
            {
                string mensaje = "No se encontró el archivo de configuración obligatorio 'appsettings.json'.\n\n" +
                                 $"Ruta esperada:\n{rutaConfig}\n\n" +
                                 "Para configurar la aplicación:\n" +
                                 "1. Copie el archivo 'appsettings.example.json' y renómbrelo como 'appsettings.json'.\n" +
                                 "2. Abra 'appsettings.json' y configure la contraseña de la base de datos ('Password').\n" +
                                 "3. Vuelva a iniciar la aplicación.";

                Logger.Error("Inicio cancelado: no existe appsettings.json.");
                MessageBox.Show(
                    mensaje,
                    "Configuración Requerida - Pausas Activas Empleado",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            // 4. Cargar y verificar configuración
            DbConfig config;
            try
            {
                config = DbConfig.CargarDesdeArchivo(rutaConfig);
            }
            catch (Exception ex)
            {
                Logger.Error("Error al leer appsettings.json", ex);
                MessageBox.Show(
                    $"Error al leer el archivo de configuración 'appsettings.json':\n\n{ex.Message}\n\n" +
                    "Verifique que el formato JSON sea válido.",
                    "Error de Configuración",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            // Advertencia amigable si la contraseña es la plantilla por defecto
            if (string.Equals(config.Password, "PON_AQUI_LA_CLAVE", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(
                    "Atención: En 'appsettings.json' la contraseña de la base de datos aún tiene el valor de ejemplo ('PON_AQUI_LA_CLAVE').\n\n" +
                    "Configure la contraseña real para conectarse al servidor SQL remoto de MonsterASP.NET.",
                    "Contraseña no configurada",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }

            // 5. Iniciar la aplicación en segundo plano
            Application.Run(new TrayApplicationContext(config));
        }
        catch (Exception ex)
        {
            Logger.Error("Error no controlado durante la ejecución principal", ex);
            MessageBox.Show(
                $"Ocurrió un error inesperado al ejecutar la aplicación:\n\n{ex.Message}\n\n" +
                "Consulte el archivo de log para más información.",
                "Error Inesperado",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            try
            {
                mutex.ReleaseMutex();
            }
            catch
            {
                // Manejo seguro al cerrar la aplicación
            }
        }
    }

    private static void ConfigurarManejadoresGlobales()
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

        Application.ThreadException += (sender, e) =>
        {
            Logger.Error("Excepción no controlada en hilo de UI (ThreadException)", e.Exception);
            MessageBox.Show(
                $"Ocurrió un error inesperado en la interfaz:\n\n{e.Exception.Message}\n\n" +
                "El detalle técnico ha sido guardado en el archivo de log.",
                "Error en Pausas Activas",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        };

        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                Logger.Error("Excepción no controlada en dominio (UnhandledException)", ex);
                MessageBox.Show(
                    $"Ocurrió un error crítico inesperado:\n\n{ex.Message}\n\n" +
                    "La aplicación debe cerrarse. El detalle técnico ha sido guardado en el log.",
                    "Error Crítico",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Stop);
            }
            else
            {
                Logger.Error($"Excepción crítica no controlada: {e.ExceptionObject}");
            }
        };
    }
}
