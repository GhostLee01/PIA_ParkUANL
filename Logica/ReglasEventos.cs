namespace PIA_ParkUANL_Lite;

// =====================================================================================
//  REGLASEVENTOS.CS — Reglas de reservas y pases que no dependen de la pantalla.
// =====================================================================================
public static class ReglasEventos
{
    /// <summary>
    /// Suma los cajones que apartan los otros eventos activos cuyas fechas se cruzan con el periodo dado.
    /// Dos eventos en fechas distintas sí pueden reservar los mismos cajones.
    /// </summary>
    public static int SumarReservasTraslapadas(DateTime inicio, DateTime fin, EventoCampus? eventoIgnorado, DateTime ahora)
    {
        int suma = 0;
        foreach (EventoCampus evento in GestorArchivos.Eventos)
        {
            bool seTraslapa = evento.FechaInicio < fin && evento.FechaFin > inicio;
            if (evento != eventoIgnorado && evento.AdmitePases(ahora) && seTraslapa)
            {
                suma = suma + evento.LugaresReservadosFacpya;
            }
        }
        return suma;
    }

    /// <summary>
    /// El pase empieza a contar desde el inicio del evento (si se emite antes, no expira antes de que empiece)
    /// y nunca dura más que el evento.
    /// </summary>
    public static DateTime CalcularExpiracionPase(EventoCampus evento, DateTime ahora)
    {
        DateTime inicioDelConteo = ahora;
        if (evento.FechaInicio > ahora)
        {
            inicioDelConteo = evento.FechaInicio;
        }

        DateTime expiracion = inicioDelConteo.AddHours(evento.VigenciaHorasPase);
        if (expiracion > evento.FechaFin)
        {
            expiracion = evento.FechaFin;
        }
        return expiracion;
    }

    /// <summary>Pases activos del evento que vencerían antes de la fecha de inicio indicada (sirve al posponer un evento).</summary>
    public static int ContarPasesQueVencenAntesDe(EventoCampus evento, DateTime inicio, DateTime ahora)
    {
        return GestorArchivos.Pases.Count(pase => pase.CodigoEvento == evento.Codigo && pase.ObtenerEstado(ahora) == EstadosDePase.Activo
                                                  && pase.FechaExpiracion < inicio);
    }

    /// <summary>true si esa placa ya tiene un pase activo para el evento.</summary>
    public static bool PlacaYaTienePase(EventoCampus evento, string placa, DateTime ahora)
    {
        return GestorArchivos.Pases.Any(pase => pase.CodigoEvento == evento.Codigo && pase.ObtenerEstado(ahora) == EstadosDePase.Activo
                                                && Validaciones.EsMismaPlaca(pase.Placa, placa));
    }
}
