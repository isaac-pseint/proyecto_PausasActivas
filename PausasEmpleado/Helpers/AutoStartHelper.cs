using System;
using Microsoft.Win32;

namespace PausasEmpleado.Helpers;

/// <summary>
/// Gestiona la configuración de inicio automático de la aplicación con Windows
/// a través del registro de usuario HKCU\Software\Microsoft\Windows\CurrentVersion\Run.
/// </summary>
public static class AutoStartHelper
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string EntryName = "PausasActivasEmpleado";

    public static bool EstaHabilitado()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(KeyPath, false);
            var valor = key?.GetValue(EntryName) as string;
            return !string.IsNullOrEmpty(valor);
        }
        catch (Exception ex)
        {
            Logger.Error("Error al consultar el registro de inicio automático", ex);
            return false;
        }
    }

    public static void SetHabilitado(bool habilitar)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(KeyPath, true);
            if (key == null) return;

            if (habilitar)
            {
                string? exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath))
                {
                    key.SetValue(EntryName, $"\"{exePath}\"");
                    Logger.Info($"Inicio automático habilitado: {exePath}");
                }
            }
            else
            {
                key.DeleteValue(EntryName, false);
                Logger.Info("Inicio automático deshabilitado");
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Error al modificar el registro de inicio automático", ex);
        }
    }
}
