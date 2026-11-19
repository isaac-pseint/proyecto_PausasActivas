using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace PausasEmpleado.Helpers;

/// <summary>
/// Genera un ícono limpio para la bandeja del sistema usando GDI+,
/// evitando dependencias de archivos .ico externos y gestionando la liberación de handles nativos.
/// </summary>
public static class TrayIconHelper
{
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool DestroyIcon(IntPtr handle);

    public static Icon CrearIconoBandeja()
    {
        // Resolución estándar de alta densidad para system tray
        const int size = 32;
        using var bmp = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.Clear(Color.Transparent);

            // Fondo circular moderno (azul oscuro corporativo #0F172A)
            using var brushBg = new SolidBrush(Color.FromArgb(15, 23, 42));
            g.FillEllipse(brushBg, 1, 1, size - 3, size - 3);

            // Anillo exterior de acento (azul brillante #38BDF8)
            using var penBorder = new Pen(Color.FromArgb(56, 189, 248), 2f);
            g.DrawEllipse(penBorder, 2, 2, size - 5, size - 5);

            // Barras de pausa verticales estilizadas (blanco suave #F8FAFC)
            using var brushBars = new SolidBrush(Color.FromArgb(248, 250, 252));
            g.FillRectangle(brushBars, 10, 9, 4, 14);
            g.FillRectangle(brushBars, 18, 9, 4, 14);
        }

        IntPtr hIcon = bmp.GetHicon();
        try
        {
            using var tempIcon = Icon.FromHandle(hIcon);
            return (Icon)tempIcon.Clone();
        }
        finally
        {
            // Liberar el recurso unmanaged para evitar fugas de memoria GDI en Windows
            DestroyIcon(hIcon);
        }
    }
}
