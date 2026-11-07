using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PIA_ParkUANL_Lite;

// =====================================================================================
//  SELLODEARCHIVOS.CS
//  Guarda una huella HMAC de cada .json. Sin la clave de firma no se puede calcular una huella
//  válida, así que un archivo editado por fuera de la app se detecta al abrirla.
//  Detecta cambios, no los impide: quien tenga la sesión de Windows puede borrar este registro,
//  pero entonces la app también lo avisa.
// =====================================================================================
public static class SelloDeArchivos
{
    private const string ArchivoDeSellos = "integridad.json";

    private sealed class Huellas
    {
        public string Actual { get; set; } = "";
        public string Anterior { get; set; } = "";
    }

    private static Dictionary<string, Huellas> huellas = new Dictionary<string, Huellas>();

    /// <summary>Regresa false si el registro no existe o está dañado.</summary>
    public static bool Cargar()
    {
        huellas = new Dictionary<string, Huellas>();
        string ruta = GestorArchivos.RutaDe(ArchivoDeSellos);
        if (File.Exists(ruta) == false)
        {
            return false;
        }

        try
        {
            huellas = JsonSerializer.Deserialize<Dictionary<string, Huellas>>(File.ReadAllText(ruta, Encoding.UTF8)) ?? new Dictionary<string, Huellas>();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>Olvida las huellas: se usa cuando cambió la clave y ya no se pueden comprobar.</summary>
    public static void Reiniciar()
    {
        huellas = new Dictionary<string, Huellas>();
        File.Delete(GestorArchivos.RutaDe(ArchivoDeSellos));
    }

    /// <summary>
    /// true si el texto es la versión actual o la anterior (la app pudo cerrarse entre el sello y el reemplazo).
    /// Un archivo sin huella se acepta y se registra; uno alterado se sella para avisarlo una sola vez.
    /// </summary>
    public static bool Coincide(string nombreArchivo, string texto)
    {
        string huella = CalcularHuella(texto);
        if (huellas.TryGetValue(nombreArchivo, out Huellas? guardadas) == false)
        {
            Registrar(nombreArchivo, texto);
            return true;
        }

        bool coincide = huella == guardadas.Actual || huella == guardadas.Anterior;
        if (coincide == false)
        {
            Registrar(nombreArchivo, texto);
        }
        return coincide;
    }

    public static void Registrar(string nombreArchivo, string texto)
    {
        string anterior = huellas.TryGetValue(nombreArchivo, out Huellas? guardadas) ? guardadas.Actual : "";
        huellas[nombreArchivo] = new Huellas { Actual = CalcularHuella(texto), Anterior = anterior };

        string ruta = GestorArchivos.RutaDe(ArchivoDeSellos);
        string rutaTemporal = ruta + ".tmp";
        File.WriteAllText(rutaTemporal, JsonSerializer.Serialize(huellas), Encoding.UTF8);
        File.Move(rutaTemporal, ruta, true);
    }

    private static string CalcularHuella(string texto)
    {
        byte[] huella = HMACSHA256.HashData(GestorArchivos.ObtenerClaveDeFirma(), Encoding.UTF8.GetBytes(texto));
        return Convert.ToHexString(huella);
    }
}
