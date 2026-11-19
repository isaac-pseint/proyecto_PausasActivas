using System;

namespace PausasCore.Models;

/// <summary>
/// Representa a un usuario / empleado corporativo en el sistema de pausas activas.
/// Mapea exactamente a la tabla 'usuarios'.
/// </summary>
public class Usuario
{
    public int Id { get; set; }
    public string UsuarioWindows { get; set; } = string.Empty;
    public string? Nombre { get; set; }
    public string? Pc { get; set; }
    public bool Activo { get; set; } = true;
    public int IntervaloMin { get; set; } = 60;
    public int DuracionSeg { get; set; } = 120;
    public string Orden { get; set; } = "secuencial"; // 'secuencial' | 'aleatorio'
    public TimeSpan HoraInicio { get; set; } = new TimeSpan(8, 0, 0);
    public TimeSpan HoraFin { get; set; } = new TimeSpan(17, 0, 0);
    public DateTime Actualizado { get; set; } = DateTime.UtcNow;
    public DateTime? UltimaConexion { get; set; }
}
