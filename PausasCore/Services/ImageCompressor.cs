using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;

namespace PausasCore.Services;

/// <summary>
/// Proporciona compresión y redimensionamiento de imágenes para asegurar que no superen los 500 KB.
/// </summary>
[SupportedOSPlatform("windows")]
public static class ImageCompressor
{
    public const int MaxSizeBytes = 500 * 1024; // 512,000 bytes (< 500 KB)

    /// <summary>
    /// Comprime un arreglo de bytes de imagen para que pese menos de 500 KB.
    /// Si ya pesa menos de 500 KB, conserva los datos originales y detecta el tipo MIME.
    /// </summary>
    /// <param name="datosOriginales">Bytes de la imagen original.</param>
    /// <param name="tipoContenidoOriginal">MIME type sugerido opcional (ej: image/png, image/jpeg).</param>
    /// <returns>Tupla con los bytes procesados y el MIME type final.</returns>
    public static (byte[] Datos, string TipoContenido) ComprimirA500Kb(byte[] datosOriginales, string? tipoContenidoOriginal = null)
    {
        if (datosOriginales == null || datosOriginales.Length == 0)
            throw new ArgumentException("Los datos de la imagen no pueden ser nulos ni estar vacíos.", nameof(datosOriginales));

        // Si ya está por debajo del límite de 500 KB, mantener intacto
        if (datosOriginales.Length < MaxSizeBytes)
        {
            string tipo = !string.IsNullOrWhiteSpace(tipoContenidoOriginal)
                ? tipoContenidoOriginal
                : DetectarTipoContenido(datosOriginales);
            return (datosOriginales, tipo);
        }

        // Si excede los 500 KB, redimensionar y recomprimir progresivamente como JPEG
        using var msIn = new MemoryStream(datosOriginales);
        using var original = Image.FromStream(msIn);

        var encoder = ObtenerEncoder(ImageFormat.Jpeg)
            ?? throw new InvalidOperationException("No se encontró el codificador JPEG en el sistema.");

        int maxWidth = 1920;
        int maxHeight = 1080;
        long quality = 85;

        byte[]? resultado = null;

        while (quality >= 30)
        {
            using var resized = Redimensionar(original, maxWidth, maxHeight);
            using var msOut = new MemoryStream();

            using var encoderParameters = new EncoderParameters(1);
            using var param = new EncoderParameter(Encoder.Quality, quality);
            encoderParameters.Param[0] = param;

            resized.Save(msOut, encoder, encoderParameters);
            byte[] bytes = msOut.ToArray();

            if (bytes.Length < MaxSizeBytes)
            {
                resultado = bytes;
                break;
            }

            // Reducir dimensiones y calidad progresivamente
            maxWidth = (int)(maxWidth * 0.85);
            maxHeight = (int)(maxHeight * 0.85);
            quality -= 15;
        }

        // Si aún así no bajó de 500 KB, último intento con dimensiones 1280x720 y calidad 30
        if (resultado == null)
        {
            using var finalResized = Redimensionar(original, 1280, 720);
            using var msOut = new MemoryStream();
            using var encoderParameters = new EncoderParameters(1);
            using var param = new EncoderParameter(Encoder.Quality, 30L);
            encoderParameters.Param[0] = param;

            finalResized.Save(msOut, encoder, encoderParameters);
            resultado = msOut.ToArray();
        }

        return (resultado, "image/jpeg");
    }

    /// <summary>
    /// Carga un archivo de imagen desde disco, lo comprime a menos de 500 KB y devuelve los bytes y MIME type.
    /// </summary>
    public static (byte[] Datos, string TipoContenido) ComprimirArchivo(string rutaArchivo)
    {
        if (!File.Exists(rutaArchivo))
            throw new FileNotFoundException("El archivo de imagen no existe.", rutaArchivo);

        byte[] bytes = File.ReadAllBytes(rutaArchivo);
        string extension = Path.GetExtension(rutaArchivo).ToLowerInvariant();
        string tipo = extension switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            _ => "application/octet-stream"
        };

        return ComprimirA500Kb(bytes, tipo);
    }

    private static Bitmap Redimensionar(Image image, int maxWidth, int maxHeight)
    {
        double ratioX = (double)maxWidth / image.Width;
        double ratioY = (double)maxHeight / image.Height;
        double ratio = Math.Min(ratioX, ratioY);

        if (ratio > 1.0)
        {
            ratio = 1.0; // No agrandar imágenes pequeñas
        }

        int newWidth = Math.Max(1, (int)(image.Width * ratio));
        int newHeight = Math.Max(1, (int)(image.Height * ratio));

        var newImage = new Bitmap(newWidth, newHeight, PixelFormat.Format24bppRgb);
        using var graphics = Graphics.FromImage(newImage);
        graphics.CompositingQuality = CompositingQuality.HighQuality;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.SmoothingMode = SmoothingMode.HighQuality;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.DrawImage(image, 0, 0, newWidth, newHeight);

        return newImage;
    }

    private static ImageCodecInfo? ObtenerEncoder(ImageFormat format)
    {
        return ImageCodecInfo.GetImageEncoders()
            .FirstOrDefault(codec => codec.FormatID == format.Guid);
    }

    private static string DetectarTipoContenido(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            return "image/jpeg";
        if (bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
            return "image/png";
        if (bytes.Length >= 6 && bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46)
            return "image/gif";
        if (bytes.Length >= 2 && bytes[0] == 0x42 && bytes[1] == 0x4D)
            return "image/bmp";
        return "image/jpeg";
    }
}
