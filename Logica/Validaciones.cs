using System.Globalization;
using System.Text.RegularExpressions;

namespace PIA_ParkUANL;

/// <summary>Campo de un formulario al que pertenece un error: la pantalla lo usa para llevar ahí el cursor.</summary>
public enum CampoFormulario
{
    Ninguno,
    Categoria,
    Nombre,
    Correo,
    Matricula,
    Placa,
    Contrasena,
    Confirmacion,
    Codigo,
    Titulo,
    Reservados,
    MaximoVisitantes,
    VigenciaHoras,
    Fechas
}

/// <summary>Resultado de validar: sin mensaje significa correcto.</summary>
public sealed record ResultadoValidacion(string Mensaje, CampoFormulario Campo = CampoFormulario.Ninguno)
{
    public static readonly ResultadoValidacion Correcto = new ResultadoValidacion("");

    public bool EsCorrecto
    {
        get { return Mensaje == ""; }
    }
}

// =====================================================================================
//  VALIDACIONES DE FORMATO. Todas aceptan null y lo tratan como dato inválido.
//  Las expresiones usan \z y no $: en .NET, $ también acepta un salto de línea al final.
// =====================================================================================
public static partial class Validaciones
{
    // === [RÚBRICA 1] VALIDACIONES DE CAMPO ===

    public const string MensajePlacaInvalida = "Placa inválida. Usa de 5 a 9 letras y números, con guiones si los lleva (ej. ABC-123, 123-ABC o AB-1234).";
    public const string MensajeContrasenaInvalida = "La contraseña debe tener de 8 a 12 caracteres, incluir letras y números, y no ser una contraseña común.";
    public const string MensajeCorreoUanlInvalido = "Usa nombre.apellido más inicial del segundo apellido, sin acentos: ana.garzat@uanl.edu.mx";
    public const string MensajePegadoRechazado = "No se pegó: la contraseña no puede empezar con un espacio ni con caracteres vacíos. Cópiala sin ellos.";
    public const int LongitudMaximaCodigoEvento = 40;
    public const int LongitudMaximaCorreo = 40;
    public const int LongitudMinimaContrasena = 8;
    public const int LongitudMaximaContrasena = 12;
    public const int LongitudMaximaContrasenaGuardada = 64;   // Cuentas creadas cuando el límite era mayor: deben poder entrar
    public const int LongitudMaximaNombre = 60;
    public const int LongitudMaximaPlaca = 12;
    public const int LongitudMatricula = 7;
    public const int LongitudMaximaTextoLibre = 200;

    private const int MinimoCaracteresPlaca = 5;
    private const int MaximoCaracteresPlaca = 9;
    private const int MinimoCaracteresDistintos = 4;
    private const int MilisegundosMaximosRegex = 200;

    // Se ven como un espacio en blanco, pero Unicode no los clasifica como espacios.
    private const string CaracteresInvisibles = "͏ᅟᅠ⠀ㅤﾠ";

    // Las más usadas que cumplen con letras y números. Se comparan en minúsculas.
    private static readonly HashSet<string> ContrasenasComunes = new HashSet<string>
    {
        "password1", "password12", "password123", "passw0rd", "contrasena1", "contrasena12", "admin123", "admin1234", "admin12345",
        "qwerty123", "qwerty1234", "qwerty12", "abc12345", "abcd1234", "abc123456", "12345678a", "a12345678", "1234567a",
        "iloveyou1", "welcome1", "uanl2026", "uanl1234", "alumno123", "docente123", "operador123", "12345abc", "1q2w3e4r", "1qaz2wsx"
    };

    [GeneratedRegex(@"\A[A-Z0-9]+(-[A-Z0-9]+)*\z", RegexOptions.None, MilisegundosMaximosRegex)]
    private static partial Regex ExpresionPlaca();

    [GeneratedRegex(@"\A\p{L}\p{M}*(?:[\p{L}\p{M} .'-])*\z", RegexOptions.None, MilisegundosMaximosRegex)]
    private static partial Regex ExpresionNombre();

    [GeneratedRegex(@"\A[^@\s""<>,;:()\[\]\\]+@[^@\s""<>,;:()\[\]\\]+\.[^@\s""<>,;:()\[\]\\]+\z", RegexOptions.None, MilisegundosMaximosRegex)]
    private static partial Regex ExpresionCorreo();

    [GeneratedRegex(@"\A[a-z]+\.[a-z]{2,}@uanl\.edu\.mx\z", RegexOptions.None, MilisegundosMaximosRegex)]
    private static partial Regex ExpresionCorreoUanl();

    [GeneratedRegex(@"\A[a-z0-9-]+\z", RegexOptions.None, MilisegundosMaximosRegex)]
    private static partial Regex ExpresionCodigoEvento();

    /// <summary>Espacio, tabulador, salto de línea, carácter de control o carácter invisible (como el espacio de ancho cero).</summary>
    public static bool EsCaracterVacio(char caracter)
    {
        return char.IsWhiteSpace(caracter) || char.IsControl(caracter)
            || char.GetUnicodeCategory(caracter) == UnicodeCategory.Format
            || CaracteresInvisibles.Contains(caracter);
    }

    /// <summary>Quita los caracteres vacíos del inicio: ningún campo puede empezar con ellos.</summary>
    public static string QuitarVaciosIniciales(string? texto)
    {
        if (string.IsNullOrEmpty(texto))
        {
            return "";
        }

        int inicio = 0;
        while (inicio < texto.Length && EsCaracterVacio(texto[inicio]))
        {
            inicio = inicio + 1;
        }
        return texto.Substring(inicio);
    }

    /// <summary>
    /// Placa de cualquier estado o país: letras y números, separados o no por guiones,
    /// de 5 a 9 caracteres y con al menos una letra y un número.
    /// </summary>
    public static bool EsPlacaValida(string? placa)
    {
        if (string.IsNullOrEmpty(placa) || placa.Length > LongitudMaximaPlaca || ExpresionPlaca().IsMatch(placa) == false)
        {
            return false;
        }

        string placaSinGuiones = placa.Replace("-", "");
        bool tieneLetra = placaSinGuiones.Any(char.IsAsciiLetter);
        bool tieneNumero = placaSinGuiones.Any(char.IsAsciiDigit);
        return placaSinGuiones.Length >= MinimoCaracteresPlaca && placaSinGuiones.Length <= MaximoCaracteresPlaca && tieneLetra && tieneNumero;
    }

    /// <summary>Compara dos placas sin importar los guiones: "ABC123" y "ABC-123" son la misma.</summary>
    public static bool EsMismaPlaca(string placaUno, string placaDos)
    {
        return placaUno.Replace("-", "") == placaDos.Replace("-", "");
    }

    /// <summary>Nombre de persona: letras (con sus acentos), espacios y los signos . ' - (sin números), de hasta LongitudMaximaNombre caracteres.</summary>
    public static bool EsNombreValido(string? nombre)
    {
        return string.IsNullOrEmpty(nombre) == false && nombre.Length <= LongitudMaximaNombre && ExpresionNombre().IsMatch(nombre);
    }

    /// <summary>Matrícula: exactamente 7 caracteres y todos son dígitos.</summary>
    public static bool EsMatriculaValida(string? matricula)
    {
        return matricula != null && matricula.Length == LongitudMatricula && matricula.All(char.IsAsciiDigit);
    }

    /// <summary>Correo con formato texto@dominio.ext, de hasta 40 caracteres.</summary>
    public static bool EsCorreoValido(string? correo)
    {
        return string.IsNullOrEmpty(correo) == false && correo.Length <= LongitudMaximaCorreo && ExpresionCorreo().IsMatch(correo);
    }

    /// <summary>
    /// Contraseña: de 8 a 12 caracteres, con letras y números, sin repetir casi el mismo carácter,
    /// que no sea de las más comunes ni contenga el nombre de usuario.
    /// </summary>
    public static bool EsContrasenaSegura(string? contrasena, string nombreUsuario = "")
    {
        if (contrasena == null || contrasena.Length < LongitudMinimaContrasena || contrasena.Length > LongitudMaximaContrasena)
        {
            return false;
        }

        string contrasenaMinusculas = contrasena.ToLowerInvariant();
        bool contieneElUsuario = nombreUsuario != "" && contrasenaMinusculas.Contains(nombreUsuario.ToLowerInvariant());
        return contrasena == QuitarVaciosIniciales(contrasena)
            && contrasena.Any(char.IsLetter) && contrasena.Any(char.IsDigit)
            && contrasena.Distinct().Count() >= MinimoCaracteresDistintos
            && ContrasenasComunes.Contains(contrasenaMinusculas) == false
            && contieneElUsuario == false;
    }

    /// <summary>
    /// Correo institucional: primer nombre, punto, primer apellido e inicial del segundo apellido,
    /// sin acentos ni ñ, y termina en @uanl.edu.mx (ej. carlos.carrenop@uanl.edu.mx).
    /// </summary>
    public static bool EsCorreoUanl(string? correo)
    {
        return string.IsNullOrEmpty(correo) == false && correo.Length <= LongitudMaximaCorreo && ExpresionCorreoUanl().IsMatch(correo.ToLowerInvariant());
    }

    /// <summary>Código de evento: minúsculas, números y guiones. El límite asegura que quepa en el QR del pase.</summary>
    public static bool EsCodigoEventoValido(string? codigo)
    {
        return string.IsNullOrEmpty(codigo) == false && codigo.Length <= LongitudMaximaCodigoEvento && ExpresionCodigoEvento().IsMatch(codigo);
    }

    /// <summary>Texto libre (motivo, título, ubicación): con límite de largo, sin caracteres de control y sin vacíos al inicio.</summary>
    public static bool EsTextoLibreValido(string? texto)
    {
        return texto != null && texto.Length <= LongitudMaximaTextoLibre && texto.Any(char.IsControl) == false
            && texto == QuitarVaciosIniciales(texto);
    }
}
