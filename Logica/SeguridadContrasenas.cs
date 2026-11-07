using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PIA_ParkUANL_Lite;

// =====================================================================================
//  SEGURIDADCONTRASENAS.CS
//  • SeguridadContrasenas → convierte las contraseñas en un hash con sal (PBKDF2).
//  • ProteccionLocal      → cifra datos para que solo este usuario de Windows los lea.
//  • ControlDeIntentos    → bloquea por unos minutos tras varios intentos fallidos.
// =====================================================================================

public static class SeguridadContrasenas
{
    // Formato guardado:  pbkdf2$iteraciones$sal$hash
    private const string PrefijoHash = "pbkdf2$";
    private const char SeparadorHash = '$';
    private const int PartesDelHash = 4;
    private const int Iteraciones = 600000;
    private const int IteracionesMinimas = 100000;
    private const int IteracionesMaximas = 10000000;   // Un valor editado a mano no debe congelar la app
    private const int BytesDeSal = 16;
    private const int BytesDeHash = 32;
    private const int CaracteresHashAnterior = 64;

    private const string LetrasTemporales = "abcdefghjkmnpqrstuvwxyz";
    private const string NumerosTemporales = "23456789";

    /// <summary>
    /// Hash con sal aleatoria: dos personas con la misma contraseña tienen hashes distintos,
    /// y las miles de iteraciones hacen muy lento adivinarla a la fuerza.
    /// </summary>
    public static string CalcularHash(string contrasena)
    {
        ArgumentNullException.ThrowIfNull(contrasena);

        byte[] sal = RandomNumberGenerator.GetBytes(BytesDeSal);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(contrasena, sal, Iteraciones, HashAlgorithmName.SHA256, BytesDeHash);
        return PrefijoHash + Iteraciones + SeparadorHash + Convert.ToBase64String(sal) + SeparadorHash + Convert.ToBase64String(hash);
    }

    /// <summary>Un hash dañado nunca coincide: la cuenta queda sin acceso, pero la app no falla.</summary>
    public static bool VerificarContrasena(string contrasena, string hashGuardado)
    {
        ArgumentNullException.ThrowIfNull(contrasena);
        ArgumentNullException.ThrowIfNull(hashGuardado);

        if (EsHashDeVersionAnterior(hashGuardado))
        {
            return VerificarHashDeVersionAnterior(contrasena, hashGuardado);
        }

        string[] partes = hashGuardado.Split(SeparadorHash);
        if (partes.Length != PartesDelHash || int.TryParse(partes[1], out int iteraciones) == false
            || iteraciones < IteracionesMinimas || iteraciones > IteracionesMaximas)
        {
            return false;
        }

        byte[]? sal = LeerBase64(partes[2]);
        byte[]? esperado = LeerBase64(partes[3]);
        if (sal == null || esperado == null || sal.Length == 0 || esperado.Length == 0)
        {
            return false;
        }

        byte[] calculado = Rfc2898DeriveBytes.Pbkdf2(contrasena, sal, iteraciones, HashAlgorithmName.SHA256, esperado.Length);
        return CryptographicOperations.FixedTimeEquals(calculado, esperado);
    }

    /// <summary>
    /// SHA-256 sin sal es trivial de escribir a mano en usuarios.json: solo se acepta
    /// si el archivo conserva su sello, es decir, si nadie lo editó por fuera de la app.
    /// </summary>
    private static bool VerificarHashDeVersionAnterior(string contrasena, string hashGuardado)
    {
        if (GestorArchivos.UsuariosAlterados || hashGuardado.Length != CaracteresHashAnterior)
        {
            return false;
        }

        byte[] calculado = Encoding.UTF8.GetBytes(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(contrasena))).ToLowerInvariant());
        return CryptographicOperations.FixedTimeEquals(calculado, Encoding.UTF8.GetBytes(hashGuardado));
    }

    private static byte[]? LeerBase64(string texto)
    {
        byte[] bytes = new byte[texto.Length];
        return Convert.TryFromBase64String(texto, bytes, out int escritos) ? bytes[..escritos] : null;
    }

    /// <summary>Las cuentas creadas antes tienen SHA-256 sin sal; al iniciar sesión se actualizan solas.</summary>
    public static bool EsHashDeVersionAnterior(string hashGuardado)
    {
        return hashGuardado.StartsWith(PrefijoHash) == false;
    }

    /// <summary>Contraseña aleatoria fácil de dictar, ej. "kmtz-4827". Cumple la regla de letras y números.</summary>
    public static string GenerarContrasenaTemporal()
    {
        return RandomNumberGenerator.GetString(LetrasTemporales, 4) + "-" + RandomNumberGenerator.GetString(NumerosTemporales, 4);
    }
}

public static class ProteccionLocal
{
    public static byte[] Cifrar(byte[] datos)
    {
        return ProtectedData.Protect(datos, null, DataProtectionScope.CurrentUser);
    }

    public static byte[] Descifrar(byte[] datosCifrados)
    {
        return ProtectedData.Unprotect(datosCifrados, null, DataProtectionScope.CurrentUser);
    }
}

public static class ControlDeIntentos
{
    public const int MaximoIntentos = 5;
    public const int MinutosDeBloqueo = 5;

    private const string ArchivoDeIntentos = "intentos.json";

    private sealed class IntentosDeCuenta
    {
        public int Fallidos { get; set; }
        public DateTime BloqueadoHasta { get; set; }
    }

    // Se guarda por lo que se escribió en el login, exista o no la cuenta: así el bloqueo no revela qué
    // usuarios son reales. La clave es una firma para que el archivo nunca conserve lo que alguien tecleó.
    private static Dictionary<string, IntentosDeCuenta> intentosPorCuenta = new Dictionary<string, IntentosDeCuenta>();

    /// <summary>Recupera los bloqueos vigentes: reiniciar la app no debe dar intentos nuevos.</summary>
    public static void Cargar()
    {
        intentosPorCuenta = new Dictionary<string, IntentosDeCuenta>();
        string ruta = GestorArchivos.RutaDe(ArchivoDeIntentos);
        if (File.Exists(ruta) == false)
        {
            return;
        }

        try
        {
            intentosPorCuenta = JsonSerializer.Deserialize<Dictionary<string, IntentosDeCuenta>>(File.ReadAllText(ruta, Encoding.UTF8))
                                ?? new Dictionary<string, IntentosDeCuenta>();
        }
        catch (JsonException ex)
        {
            GestorArchivos.RegistrarError(ex);
        }
    }

    private static void Guardar()
    {
        try
        {
            string ruta = GestorArchivos.RutaDe(ArchivoDeIntentos);
            string rutaTemporal = ruta + ".tmp";
            File.WriteAllText(rutaTemporal, JsonSerializer.Serialize(intentosPorCuenta), Encoding.UTF8);
            File.Move(rutaTemporal, ruta, true);
        }
        catch (Exception ex) when (GestorArchivos.EsErrorDeDisco(ex))
        {
            // El bloqueo sigue funcionando en memoria aunque no se haya podido guardar.
            GestorArchivos.RegistrarError(ex);
        }
    }

    /// <summary>Minutos que faltan para poder intentar otra vez (0 = no está bloqueado).</summary>
    public static int ObtenerMinutosRestantes(string identificador, DateTime ahora)
    {
        IntentosDeCuenta? intentos;
        if (intentosPorCuenta.TryGetValue(FirmaDigital.Firmar(identificador), out intentos) == false || intentos.BloqueadoHasta <= ahora)
        {
            return 0;
        }
        return (int)Math.Ceiling((intentos.BloqueadoHasta - ahora).TotalMinutes);
    }

    /// <summary>Regresa true si este fallo acaba de bloquear la cuenta.</summary>
    public static bool RegistrarFallo(string identificador, DateTime ahora)
    {
        string clave = FirmaDigital.Firmar(identificador);
        IntentosDeCuenta? intentos;
        if (intentosPorCuenta.TryGetValue(clave, out intentos) == false)
        {
            intentos = new IntentosDeCuenta();
            intentosPorCuenta[clave] = intentos;
        }

        bool seBloqueo = false;
        intentos.Fallidos = intentos.Fallidos + 1;
        if (intentos.Fallidos >= MaximoIntentos)
        {
            intentos.BloqueadoHasta = ahora.AddMinutes(MinutosDeBloqueo);
            intentos.Fallidos = 0;
            seBloqueo = true;
        }

        QuitarBloqueosVencidos(ahora);
        Guardar();
        return seBloqueo;
    }

    /// <summary>Evita que el archivo crezca sin límite con intentos viejos.</summary>
    private static void QuitarBloqueosVencidos(DateTime ahora)
    {
        List<string> vencidos = intentosPorCuenta
            .Where(par => par.Value.Fallidos == 0 && par.Value.BloqueadoHasta <= ahora)
            .Select(par => par.Key)
            .ToList();
        foreach (string clave in vencidos)
        {
            intentosPorCuenta.Remove(clave);
        }
    }

    public static void Reiniciar(string identificador)
    {
        if (intentosPorCuenta.Remove(FirmaDigital.Firmar(identificador)))
        {
            Guardar();
        }
    }
}
