using System.Text.Json.Serialization;

namespace PIA_ParkUANL;

// =====================================================================================
//  MODELOS.CS
//  Clases de datos del sistema ParkUANL. Cada clase se guarda tal cual en un archivo .json
//  (ver GestorArchivos.cs). Las constantes evitan escribir el mismo texto en muchos lugares.
// =====================================================================================

public static class Roles
{
    public const string Administrador = "Administrador";
    public const string Operador = "Operador";
    public const string Comunidad = "Comunidad";

    public static bool EsValido(string rol)
    {
        return rol == Administrador || rol == Operador || rol == Comunidad;
    }
}

public static class Categorias
{
    public const string Alumno = "Alumno";
    public const string Docente = "Docente";
    public const string Visitante = "Visitante";
}

public static class Estacionamientos
{
    public const string Facpya = "FACPYA";
    public const string Estadio = "Estadio";
    public static readonly string[] Todos = { Facpya, Estadio };
}

public static class MetodosDeEntrada
{
    public const string Manual = "Manual";
    public const string QrUanl = "QR UANL";
    public const string QrVisitante = "QR Visitante";
}

public static class EstadosDePase
{
    public const string Activo = "Activo";
    public const string Revocado = "Revocado";
    public const string Expirado = "Expirado";
}

/// <summary>Textos fijos de los filtros de las tablas.</summary>
public static class OpcionesDeFiltro
{
    public const string Todos = "Todos";
    public const string Adentro = "Adentro";
    public const string Finalizado = "Finalizado";
    public const string ConCuenta = "Con cuenta";
    public const string SinCuenta = "Sin cuenta";
    public const string NingunEvento = "(Ninguno)";
}

public static class Semaforos
{
    public const string Verde = "Verde";
    public const string Dorado = "Dorado";
    public const string Rojo = "Rojo";
}

/// <summary>
/// Persona que puede iniciar sesión.
/// Rol: Administrador, Operador o Comunidad. Categoria (solo para Comunidad): Alumno o Docente.
/// </summary>
public class Usuario
{
    public string NombreUsuario { get; set; } = "";
    public string ContrasenaHash { get; set; } = "";   // Ver SeguridadContrasenas.cs (nunca se guarda en texto plano)
    public string NombreCompleto { get; set; } = "";
    public string Correo { get; set; } = "";
    public string Matricula { get; set; } = "";
    public string Placa { get; set; } = "";            // Vehículo principal
    public List<string> PlacasAdicionales { get; set; } = new List<string>();
    public string Rol { get; set; } = "";
    public string Categoria { get; set; } = "";
    public bool Activo { get; set; } = true;
    public bool DebeCambiarContrasena { get; set; }    // true = al entrar se le obliga a cambiarla
    public DateTime? TemporalVenceEl { get; set; }     // Solo mientras tenga una contraseña temporal

    /// <summary>Todas las placas de la cuenta: primero la principal y luego las adicionales.</summary>
    public List<string> ObtenerPlacas()
    {
        List<string> placas = new List<string>();
        if (Placa != "")
        {
            placas.Add(Placa);
        }
        placas.AddRange(PlacasAdicionales);
        return placas;
    }
}

/// <summary>
/// Un vehículo que entró a un estacionamiento. Si FechaSalida es null, el vehículo sigue adentro.
/// </summary>
public class RegistroAcceso
{
    public int Id { get; set; }
    public string Estacionamiento { get; set; } = "";   // "FACPYA" o "Estadio"
    public string Placa { get; set; } = "";
    public string Matricula { get; set; } = "";
    public string Correo { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string Categoria { get; set; } = "";         // "Alumno", "Docente" o "Visitante"
    public string CodigoEvento { get; set; } = "";      // Solo visitantes de evento
    public string IdPase { get; set; } = "";            // Solo visitantes con pase QR
    public string Metodo { get; set; } = "";            // "Manual", "QR UANL" o "QR Visitante"
    public string Cajon { get; set; } = "";
    public bool UsaCajonReservado { get; set; }         // true = ocupa un cajón reservado por un evento
    public DateTime FechaEntrada { get; set; }
    public DateTime? FechaSalida { get; set; }
    public string Operador { get; set; } = "";
    public string OperadorSalida { get; set; } = "";    // Quien registró la salida

    // Propiedad calculada: no se guarda en el JSON.
    [JsonIgnore]
    public bool EstaAdentro
    {
        get { return FechaSalida == null; }
    }
}

/// <summary>
/// Evento del campus. Mientras está vigente aparta cajones del estacionamiento FACPYA
/// y limita cuántos pases de visitante se pueden emitir (MaxVisitantes).
/// </summary>
public class EventoCampus
{
    public int Id { get; set; }
    public string Codigo { get; set; } = "";            // Ej. "congreso-gumersindo"
    public string Titulo { get; set; } = "";
    public string Ubicacion { get; set; } = "";
    public DateTime FechaInicio { get; set; }
    public DateTime FechaFin { get; set; }
    public bool Activo { get; set; } = true;
    public int LugaresReservadosFacpya { get; set; }
    public int MaxVisitantes { get; set; }
    public int VigenciaHorasPase { get; set; } = 12;
    public bool PermiteReingreso { get; set; } = true;  // false = el pase solo sirve para una entrada

    /// <summary>Vigente = está activo y hoy cae entre su inicio y su fin.</summary>
    public bool EstaVigente(DateTime ahora)
    {
        return Activo && ahora >= FechaInicio && ahora <= FechaFin;
    }

    /// <summary>true si todavía se pueden emitir pases: está activo y no ha terminado (aunque no haya empezado).</summary>
    public bool AdmitePases(DateTime ahora)
    {
        return Activo && ahora <= FechaFin;
    }

    /// <summary>Estado para mostrar en pantalla.</summary>
    public string ObtenerEstado(DateTime ahora)
    {
        if (Activo == false)
        {
            return "Desactivado";
        }
        else if (ahora < FechaInicio)
        {
            return "Próximo";
        }
        else if (ahora <= FechaFin)
        {
            return "Vigente";
        }
        else
        {
            return "Terminado";
        }
    }
}

/// <summary>
/// Pase de visitante para un evento. Estados posibles: "Activo", "Revocado", "Expirado".
/// </summary>
public class PaseVisitante
{
    public string Id { get; set; } = "";                // Ej. "P-0001"
    public string CodigoUnico { get; set; } = "";       // Va dentro del QR: un QR viejo no sirve aunque el Id se repita
    public string CodigoEvento { get; set; } = "";
    public string NombreVisitante { get; set; } = "";
    public string Correo { get; set; } = "";
    public string Placa { get; set; } = "";
    public DateTime FechaEmision { get; set; }
    public DateTime FechaExpiracion { get; set; }
    public bool Revocado { get; set; }

    public string ObtenerEstado(DateTime ahora)
    {
        if (Revocado)
        {
            return EstadosDePase.Revocado;
        }
        else if (ahora > FechaExpiracion)
        {
            return EstadosDePase.Expirado;
        }
        else
        {
            return EstadosDePase.Activo;
        }
    }
}

/// <summary>Alumno o docente que aparece en los accesos pero no tiene cuenta para iniciar sesión.</summary>
public class PersonaSinCuenta
{
    public string Nombre { get; set; } = "";
    public string Correo { get; set; } = "";
    public string Matricula { get; set; } = "";
    public string Categoria { get; set; } = "";
    public string Placa { get; set; } = "";
}

/// <summary>Una línea de la bitácora: quién hizo qué, cuándo y por qué.</summary>
public class RegistroBitacora
{
    public int Id { get; set; }
    public DateTime Fecha { get; set; }
    public string Usuario { get; set; } = "";
    public string Accion { get; set; } = "";
    public string Detalle { get; set; } = "";
    public string Motivo { get; set; } = "";
}

/// <summary>Resultado de los cálculos de aforo de un estacionamiento (lo llena CalculadoraAforo).</summary>
public class ResumenAforo
{
    public string Estacionamiento { get; set; } = "";
    public int CapacidadTotal { get; set; }
    public int LugaresReservados { get; set; }
    public int CapacidadEfectiva { get; set; }
    public int VehiculosAdentro { get; set; }
    public int ReservadosOcupados { get; set; }
    public int LugaresDisponibles { get; set; }
    public double PorcentajeOcupacion { get; set; }
    public string Semaforo { get; set; } = Semaforos.Verde;
}
