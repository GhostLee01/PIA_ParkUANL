namespace PIA_ParkUANL;

// =====================================================================================
//  PRONOSTICOOCUPACION.CS
//  Ocupación de un día, hora por hora, usando el historial de accesos:
//    - Antes del primer ingreso no hay datos.
//    - Un día que ya pasó muestra lo que realmente ocurrió, no una estimación.
//    - Hoy y los días futuros se estiman así:
//        1) Toma los días pasados del mismo día de la semana (si no hay, todos los días pasados).
//        2) Cuenta cuántos vehículos había adentro a la mitad de cada hora de esos días.
//        3) El pronóstico de esa hora es el promedio de esos conteos.
// =====================================================================================

/// <summary>Pronóstico de una hora del día.</summary>
public class PuntoPronostico
{
    public int Hora { get; set; }
    public double VehiculosEstimados { get; set; }
    public double PorcentajeOcupacion { get; set; }
    public string Semaforo { get; set; } = Semaforos.Verde;
}

/// <summary>Pronóstico completo de un día: una posición por cada hora (0 a 23).</summary>
public class PronosticoDelDia
{
    public string Estacionamiento { get; set; } = "";
    public DateTime Dia { get; set; }
    public int DiasDeHistorial { get; set; }
    public bool UsaMismoDiaDeLaSemana { get; set; }

    /// <summary>True si el día ya pasó: los datos son lo que ocurrió, no una estimación.</summary>
    public bool EsOcupacionReal { get; set; }
    public List<PuntoPronostico> Horas { get; set; } = new List<PuntoPronostico>();
}

public static class PronosticoOcupacion
{
    /// <summary>
    /// Regresa null si todavía no hay días pasados con accesos o si el día es anterior
    /// al primer ingreso del estacionamiento: de esas fechas no existen datos.
    /// </summary>
    public static PronosticoDelDia? Calcular(string estacionamiento, DateTime dia, List<RegistroAcceso> accesos, DateTime ahora)
    {
        List<RegistroAcceso> accesosDelLote = accesos.Where(acceso => acceso.Estacionamiento == estacionamiento).ToList();

        List<DateTime> diasPasados = accesosDelLote
            .Select(acceso => acceso.FechaEntrada.Date)
            .Where(fecha => fecha < ahora.Date)
            .Distinct()
            .ToList();
        if (diasPasados.Count == 0 || dia.Date < diasPasados.Min())
        {
            return null;
        }

        if (dia.Date < ahora.Date)
        {
            return CalcularOcupacionReal(estacionamiento, dia.Date, accesosDelLote, ahora);
        }

        List<DateTime> diasIguales = diasPasados.Where(fecha => fecha.DayOfWeek == dia.DayOfWeek).ToList();
        bool usaMismoDia = diasIguales.Count > 0;
        List<DateTime> diasDeReferencia = usaMismoDia ? diasIguales : diasPasados;

        PronosticoDelDia pronostico = new PronosticoDelDia();
        pronostico.Estacionamiento = estacionamiento;
        pronostico.Dia = dia.Date;
        pronostico.DiasDeHistorial = diasDeReferencia.Count;
        pronostico.UsaMismoDiaDeLaSemana = usaMismoDia;

        int capacidad = CalculadoraAforo.ObtenerCapacidadTotal(estacionamiento);
        for (int hora = 0; hora < 24; hora++)
        {
            double sumaVehiculos = 0;
            foreach (DateTime fecha in diasDeReferencia)
            {
                DateTime momento = fecha.AddHours(hora).AddMinutes(30);
                sumaVehiculos = sumaVehiculos + ContarVehiculosEn(momento, accesosDelLote, ahora);
            }

            double vehiculosEstimados = Math.Round(sumaVehiculos / diasDeReferencia.Count, 1);
            pronostico.Horas.Add(CrearPunto(hora, vehiculosEstimados, capacidad));
        }
        return pronostico;
    }

    /// <summary>Lo que ocurrió un día que ya pasó: los vehículos que había adentro a la mitad de cada hora.</summary>
    private static PronosticoDelDia CalcularOcupacionReal(string estacionamiento, DateTime dia, List<RegistroAcceso> accesosDelLote, DateTime ahora)
    {
        PronosticoDelDia ocupacion = new PronosticoDelDia();
        ocupacion.Estacionamiento = estacionamiento;
        ocupacion.Dia = dia;
        ocupacion.DiasDeHistorial = 1;
        ocupacion.EsOcupacionReal = true;

        int capacidad = CalculadoraAforo.ObtenerCapacidadTotal(estacionamiento);
        for (int hora = 0; hora < 24; hora++)
        {
            DateTime momento = dia.AddHours(hora).AddMinutes(30);
            ocupacion.Horas.Add(CrearPunto(hora, ContarVehiculosEn(momento, accesosDelLote, ahora), capacidad));
        }
        return ocupacion;
    }

    private static PuntoPronostico CrearPunto(int hora, double vehiculos, int capacidad)
    {
        double porcentaje = Math.Round((vehiculos / capacidad) * 100.0, 1);
        return new PuntoPronostico
        {
            Hora = hora,
            VehiculosEstimados = vehiculos,
            PorcentajeOcupacion = porcentaje,
            Semaforo = CalculadoraAforo.ClasificarSemaforo(porcentaje)
        };
    }

    /// <summary>Mensaje para el usuario cuando Calcular regresa null.</summary>
    public static string ExplicarFaltaDePronostico(string estacionamiento, DateTime dia, List<RegistroAcceso> accesos)
    {
        DateTime? primerIngreso = accesos
            .Where(acceso => acceso.Estacionamiento == estacionamiento)
            .Select(acceso => (DateTime?)acceso.FechaEntrada.Date)
            .Min();

        if (primerIngreso != null && dia.Date < primerIngreso)
        {
            return "No hay datos del " + dia.ToString("dd/MM/yyyy") + ": el primer ingreso registrado en " + estacionamiento +
                   " fue el " + primerIngreso.Value.ToString("dd/MM/yyyy") + ".";
        }
        return "Todavía no hay historial suficiente para hacer un pronóstico.";
    }

    private static int ContarVehiculosEn(DateTime momento, List<RegistroAcceso> accesos, DateTime ahora)
    {
        int contador = 0;
        foreach (RegistroAcceso acceso in accesos)
        {
            DateTime salida = acceso.FechaSalida ?? ahora;
            if (acceso.FechaEntrada <= momento && salida > momento)
            {
                contador = contador + 1;
            }
        }
        return contador;
    }

    /// <summary>La hora con la ocupación más alta del día.</summary>
    public static PuntoPronostico ObtenerHoraPico(PronosticoDelDia pronostico)
    {
        return pronostico.Horas.OrderByDescending(punto => punto.VehiculosEstimados).First();
    }
}
