using System;

namespace PausasCore.Models;

/// <summary>
/// Representa a un administrador con credenciales en el sistema.
/// Mapea exactamente a la tabla 'admins'.
/// </summary>
public class Admin
{
    public int Id { get; set; }
    public string Usuario { get; set; } = string.Empty;
    public string? Nombre { get; set; }
    public string ClaveHash { get; set; } = string.Empty;
    public DateTime Creado { get; set; } = DateTime.UtcNow;
}
