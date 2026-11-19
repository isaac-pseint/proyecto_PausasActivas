using System;
using System.Security.Cryptography;
using System.Text;

namespace PausasCore.Security;

/// <summary>
/// Maneja la generación y verificación de contraseñas seguras usando PBKDF2-HMAC-SHA256.
/// Compatible con el esquema: pbkdf2$<iteraciones>$<salt_base64>$<hash_base64>
/// </summary>
public static class PasswordHasher
{
    private const int LongitudSaltBytes = 16;
    private const int LongitudClaveBytes = 32;
    private const int IteracionesPorDefecto = 100_000;
    private static readonly HashAlgorithmName Algoritmo = HashAlgorithmName.SHA256;

    /// <summary>
    /// Genera un hash PBKDF2 compatible con formato: pbkdf2$<iteraciones>$<salt_base64>$<hash_base64>
    /// </summary>
    /// <param name="clave">Contraseña en texto plano.</param>
    /// <param name="iteraciones">Cantidad de iteraciones (por defecto 100.000).</param>
    /// <returns>Cadena formateada con el hash PBKDF2.</returns>
    public static string GenerarHash(string clave, int iteraciones = IteracionesPorDefecto)
    {
        if (clave == null)
            throw new ArgumentNullException(nameof(clave));
        if (iteraciones <= 0)
            throw new ArgumentOutOfRangeException(nameof(iteraciones), "Las iteraciones deben ser mayores a 0.");

        byte[] salt = RandomNumberGenerator.GetBytes(LongitudSaltBytes);
        byte[] claveDerivada = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(clave),
            salt,
            iteraciones,
            Algoritmo,
            LongitudClaveBytes);

        return $"pbkdf2${iteraciones}${Convert.ToBase64String(salt)}${Convert.ToBase64String(claveDerivada)}";
    }

    /// <summary>
    /// Verifica una contraseña en texto plano contra un hash guardado.
    /// Utiliza CryptographicOperations.FixedTimeEquals para prevenir ataques de tiempo (timing attacks).
    /// </summary>
    /// <param name="clave">Contraseña en texto plano a verificar.</param>
    /// <param name="hashGuardado">Hash guardado en la base de datos.</param>
    /// <returns>True si la contraseña coincide; de lo contrario, False.</returns>
    public static bool Verificar(string clave, string hashGuardado)
    {
        if (string.IsNullOrEmpty(clave) || string.IsNullOrEmpty(hashGuardado))
            return false;

        string[] partes = hashGuardado.Split('$');
        if (partes.Length != 4)
            return false;

        if (!string.Equals(partes[0], "pbkdf2", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!int.TryParse(partes[1], out int iteraciones) || iteraciones <= 0)
            return false;

        byte[] salt;
        byte[] hashEsperado;
        try
        {
            salt = Convert.FromBase64String(partes[2]);
            hashEsperado = Convert.FromBase64String(partes[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        if (salt.Length != LongitudSaltBytes || hashEsperado.Length != LongitudClaveBytes)
            return false;

        byte[] hashCalculado = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(clave),
            salt,
            iteraciones,
            Algoritmo,
            LongitudClaveBytes);

        return CryptographicOperations.FixedTimeEquals(hashCalculado, hashEsperado);
    }

    /// <summary>
    /// Método de comprobación interna para verificar que el algoritmo PBKDF2
    /// valida correctamente 'admin123' contra el hash preconfigurado en la base de datos.
    /// </summary>
    public static bool ComprobarHashPrueba()
    {
        const string clavePrueba = "admin123";
        const string hashPrueba = "pbkdf2$100000$TaLsHLXcyDDuZMQxhVuthg==$LZ9Dd7I78M/oMvnrMZ3qCs73Tc2Iei1ddIp2SY5g3cc=";
        return Verificar(clavePrueba, hashPrueba);
    }
}
