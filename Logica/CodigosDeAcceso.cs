using System.Security.Cryptography;
using System.Text;

namespace PIA_ParkUANL;

// =====================================================================================
//  CODIGOSDEACCESO.CS
//  Textos que van dentro de los códigos QR. Todos llevan una firma: sin la clave secreta
//  nadie puede inventar un código ni cambiar sus datos.
//    • TokenUanl           → Alumnos y Docentes, dura 30 segundos y sirve una sola vez.
//    • CodigoPaseVisitante → Visitantes de un evento.
// =====================================================================================

public static class FirmaDigital
{
    private const int CaracteresDeFirma = 16;

    /// <summary>HMAC-SHA256 del texto (primeros 16 caracteres, para que quepa en el QR).</summary>
    public static string Firmar(string datos)
    {
        byte[] firma = HMACSHA256.HashData(GestorArchivos.ObtenerClaveDeFirma(), Encoding.UTF8.GetBytes(datos));
        return Convert.ToHexString(firma).Substring(0, CaracteresDeFirma).ToLowerInvariant();
    }

    public static bool EsFirmaValida(string datos, string firma)
    {
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(Firmar(datos)), Encoding.UTF8.GetBytes(firma));
    }
}

/// <summary>Formato:  UANL|matricula|placa|expiracionUnix|firma</summary>
public static class TokenUanl
{
    public const string Prefijo = "UANL|";
    public const int SegundosVigencia = 30;

    private const int PartesDelToken = 5;

    // Tokens que ya dieron una entrada, con su expiración. Solo hace falta recordarlos mientras siguen vigentes.
    private static readonly Dictionary<string, long> tokensUsados = new Dictionary<string, long>();

    public static string Generar(Usuario usuario, string placa, DateTime ahora)
    {
        ArgumentNullException.ThrowIfNull(usuario);

        long expiracion = new DateTimeOffset(ahora.AddSeconds(SegundosVigencia)).ToUnixTimeSeconds();
        string datos = Prefijo + usuario.Matricula + "|" + placa + "|" + expiracion;
        return datos + "|" + FirmaDigital.Firmar(datos);
    }

    /// <summary>
    /// Valida el token. Regresa "" si es válido (con el usuario y la placa elegida),
    /// o el mensaje de error si no lo es.
    /// </summary>
    public static string Validar(string token, DateTime ahora, out Usuario? usuario, out string placa)
    {
        ArgumentNullException.ThrowIfNull(token);

        usuario = null;
        placa = "";
        string[] partes = token.Trim().Split('|');

        if (partes.Length != PartesDelToken || partes[0] + "|" != Prefijo)
        {
            return "El código no es un token UANL válido.";
        }

        string datos = partes[0] + "|" + partes[1] + "|" + partes[2] + "|" + partes[3];
        if (FirmaDigital.EsFirmaValida(datos, partes[4]) == false)
        {
            return "La firma del token no es válida (el código fue alterado).";
        }

        long expiracion;
        if (long.TryParse(partes[3], out expiracion) == false)
        {
            return "El token tiene una fecha de expiración inválida.";
        }
        long ahoraUnix = new DateTimeOffset(ahora).ToUnixTimeSeconds();
        if (ahoraUnix > expiracion)
        {
            return "El token expiró (dura " + SegundosVigencia + " s). Pide al usuario que muestre un QR nuevo.";
        }
        if (tokensUsados.ContainsKey(partes[4]))
        {
            return "Este QR ya se usó. Pide al usuario que muestre uno nuevo.";
        }

        string matricula = partes[1];
        foreach (Usuario cuenta in GestorArchivos.Usuarios)
        {
            if (cuenta.Matricula == matricula && cuenta.Rol == Roles.Comunidad)
            {
                if (cuenta.Activo == false)
                {
                    return "El usuario de este token está desactivado.";
                }
                if (cuenta.ObtenerPlacas().Contains(partes[2]) == false)
                {
                    return "La placa " + partes[2] + " ya no está registrada en la cuenta de " + cuenta.NombreCompleto + ".";
                }
                usuario = cuenta;
                placa = partes[2];
                return "";
            }
        }
        return "No existe un usuario de la Comunidad UANL con la matrícula " + matricula + ".";
    }

    /// <summary>Se llama cuando el token ya dio una entrada: no vuelve a servir aunque le queden segundos.</summary>
    public static void MarcarUsado(string token, DateTime ahora)
    {
        ArgumentNullException.ThrowIfNull(token);

        string[] partes = token.Trim().Split('|');
        if (partes.Length != PartesDelToken || long.TryParse(partes[3], out long expiracion) == false)
        {
            return;
        }

        long ahoraUnix = new DateTimeOffset(ahora).ToUnixTimeSeconds();
        foreach (string vencido in tokensUsados.Where(par => par.Value < ahoraUnix).Select(par => par.Key).ToList())
        {
            tokensUsados.Remove(vencido);
        }
        tokensUsados[partes[4]] = expiracion;
    }
}

/// <summary>Formato:  PASE|idPase|codigoEvento|expiracionUnix|codigoUnico|firma</summary>
public static class CodigoPaseVisitante
{
    public const string Prefijo = "PASE|";

    private const int PartesDelPase = 6;
    private const int CaracteresDeCodigoUnico = 8;

    /// <summary>Identifica a este pase para siempre: dos pases nunca comparten QR aunque se repita su Id.</summary>
    public static string CrearCodigoUnico()
    {
        return RandomNumberGenerator.GetHexString(CaracteresDeCodigoUnico, true);
    }

    public static string Generar(PaseVisitante pase)
    {
        ArgumentNullException.ThrowIfNull(pase);

        long expiracion = new DateTimeOffset(pase.FechaExpiracion).ToUnixTimeSeconds();
        string datos = Prefijo + pase.Id + "|" + pase.CodigoEvento + "|" + expiracion + "|" + pase.CodigoUnico;
        return datos + "|" + FirmaDigital.Firmar(datos);
    }

    /// <summary>Regresa "" si el pase es válido (con el pase y su evento); si no, el motivo del rechazo.</summary>
    public static string Validar(string codigo, DateTime ahora, out PaseVisitante? pase, out EventoCampus? evento)
    {
        ArgumentNullException.ThrowIfNull(codigo);

        // === [RÚBRICA 1] VALIDACIONES DE CAMPO ===
        pase = null;
        evento = null;
        string[] partes = codigo.Trim().Split('|');

        if (partes.Length != PartesDelPase || partes[0] + "|" != Prefijo)
        {
            return "El código no es un pase de visitante válido.";
        }

        string datos = partes[0] + "|" + partes[1] + "|" + partes[2] + "|" + partes[3] + "|" + partes[4];
        if (FirmaDigital.EsFirmaValida(datos, partes[5]) == false)
        {
            return "La firma del pase no es válida (el código fue alterado o es falso).";
        }

        string idPase = partes[1];
        string codigoEvento = partes[2];

        PaseVisitante? encontrado = GestorArchivos.Pases.FirstOrDefault(candidato => candidato.Id == idPase);
        if (encontrado == null)
        {
            return "El pase " + idPase + " no existe.";
        }
        if (encontrado.CodigoUnico != partes[4])
        {
            return "Este QR pertenece a un pase anterior que ya no es válido.";
        }
        if (encontrado.CodigoEvento != codigoEvento)
        {
            return "El pase no corresponde al evento indicado en el QR.";
        }
        if (partes[3] != new DateTimeOffset(encontrado.FechaExpiracion).ToUnixTimeSeconds().ToString())
        {
            return "Este QR pertenece a una versión anterior del pase " + encontrado.Id + ".";
        }

        pase = encontrado;
        string estado = pase.ObtenerEstado(ahora);
        if (estado == EstadosDePase.Revocado)
        {
            return "El pase " + pase.Id + " fue REVOCADO. No se permite el acceso.";
        }
        if (estado == EstadosDePase.Expirado)
        {
            return "El pase " + pase.Id + " EXPIRÓ el " + pase.FechaExpiracion.ToString("dd/MM/yyyy HH:mm") + ".";
        }

        evento = GestorArchivos.Eventos.FirstOrDefault(candidato => candidato.Codigo == codigoEvento);
        if (evento == null)
        {
            return "El evento " + codigoEvento + " no existe.";
        }
        if (evento.Activo && ahora < evento.FechaInicio)
        {
            return "El evento " + evento.Titulo + " inicia hasta el " + evento.FechaInicio.ToString("dd/MM/yyyy HH:mm") + ".";
        }
        if (evento.EstaVigente(ahora) == false)
        {
            return "El evento " + evento.Titulo + " no está vigente.";
        }

        return ValidarUsoDelPase(pase, evento);
    }

    private static string ValidarUsoDelPase(PaseVisitante pase, EventoCampus evento)
    {
        foreach (RegistroAcceso acceso in GestorArchivos.Accesos)
        {
            if (acceso.IdPase != pase.Id)
            {
                continue;
            }
            if (acceso.EstaAdentro)
            {
                return "El pase " + pase.Id + " ya se usó: el vehículo sigue adentro.";
            }
            if (evento.PermiteReingreso == false)
            {
                return "El pase " + pase.Id + " ya se usó y este evento no permite reingresos.";
            }
        }
        return "";
    }
}
