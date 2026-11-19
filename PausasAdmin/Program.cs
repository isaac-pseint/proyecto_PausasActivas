using System;
using System.IO;
using System.Windows.Forms;
using PausasCore.Configuration;
using PausasCore.Repositories;
using PausasAdmin.Helpers;

namespace PausasAdmin;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        // Mantener compatibilidad de hash para comprobaciones rápidas
        if (args.Length > 0 && args[0] == "--test-hash")
        {
            bool testOk = PausasCore.Security.PasswordHasher.ComprobarHashPrueba();
            Console.WriteLine($"HASH_VERIFICATION_RESULT: {testOk}");
            return;
        }

        // 1. Configurar manejadores globales de excepciones
        ConfigurarManejadoresGlobales();

        try
        {
            ApplicationConfiguration.Initialize();

            // 2. Validar existencia de appsettings.json
            string rutaConfig = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (!File.Exists(rutaConfig))
            {
                string mensaje = "No se encontró el archivo de configuración obligatorio 'appsettings.json'.\n\n" +
                                 $"Ruta esperada:\n{rutaConfig}\n\n" +
                                 "Para configurar el panel de administración:\n" +
                                 "1. Copie el archivo 'appsettings.example.json' y renómbrelo como 'appsettings.json'.\n" +
                                 "2. Abra 'appsettings.json' y configure la contraseña de la base de datos ('Password').\n" +
                                 "3. Vuelva a iniciar la aplicación.";

                Logger.Error("Inicio cancelado: no existe appsettings.json.");
                MessageBox.Show(
                    mensaje,
                    "Configuración Requerida - Pausas Activas Administración",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            // 3. Cargar y verificar configuración
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
                    "Asegúrese de que el formato JSON sea válido.",
                    "Error de Configuración",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            if (string.Equals(config.Password, "PON_AQUI_LA_CLAVE", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(
                    "Atención: En 'appsettings.json' la contraseña de la base de datos aún tiene el valor de ejemplo ('PON_AQUI_LA_CLAVE').\n\n" +
                    "Configure la contraseña real asignada para el usuario db81083_pausas_adm antes de iniciar sesión.",
                    "Contraseña no configurada",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }

            var adminRepo = new AdminRepository(config);

            // 4. Pantalla de inicio de sesión
            using var loginForm = new Forms.LoginForm(adminRepo);
            if (loginForm.ShowDialog() != DialogResult.OK || loginForm.AdminAutenticado == null)
            {
                Logger.Info("Inicio de sesión cancelado o cerrado por el usuario.");
                return;
            }

            Logger.Info($"Sesión iniciada exitosamente para admin: {loginForm.AdminAutenticado.Usuario}");

            // 5. Ventana principal de administración
            Application.Run(new Forms.MainAdminForm(adminRepo, loginForm.AdminAutenticado));
        }
        catch (Exception ex)
        {
            Logger.Error("Error no controlado durante la ejecución de la aplicación de administración", ex);
            MessageBox.Show(
                $"Ocurrió un error inesperado:\n\n{ex.Message}\n\nConsulte el archivo de log para más detalles.",
                "Error Inesperado",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
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
                "Error en Pausas Activas Admin",
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
