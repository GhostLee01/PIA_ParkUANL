namespace PIA_ParkUANL;

// =====================================================================================
//  CALCULADORAAFORO.CS
//  Todas las fórmulas del sistema, escritas paso por paso en variables separadas
//  para que cada operación se pueda revisar (y reparar) línea por línea.
// =====================================================================================
public static class CalculadoraAforo
{
    // === [RÚBRICA 3] CÁLCULOS MATEMÁTICOS DE AFORO Y ESTANCIA ===

    public const int CapacidadFacpya = 500;
    public const int CapacidadEstadio = 1000;
    public const double UmbralDorado = 70.0;
    public const double UmbralRojo = 90.0;
    public const int HorasParaConsiderarOlvidado = 12;

    public static int ObtenerCapacidadTotal(string estacionamiento)
    {
        if (estacionamiento == Estacionamientos.Facpya)
        {
            return CapacidadFacpya;
        }
        else
        {
            return CapacidadEstadio;
        }
    }

    /// <summary>
    /// Suma los cajones reservados por los eventos vigentes. Solo FACPYA tiene reservas;
    /// el Estadio siempre regresa 0.
    /// </summary>
    public static int CalcularLugaresReservados(string estacionamiento, List<EventoCampus> eventos, DateTime ahora)
    {
        if (estacionamiento != Estacionamientos.Facpya)
        {
            return 0;
        }

        int suma = 0;
        foreach (EventoCampus evento in eventos)
        {
            if (evento.EstaVigente(ahora))
            {
                suma = suma + evento.LugaresReservadosFacpya;
            }
        }

        // Nunca se puede reservar más que la capacidad total.
        if (suma > CapacidadFacpya)
        {
            suma = CapacidadFacpya;
        }
        return suma;
    }

    /// <summary>
    /// Cuenta los vehículos adentro que ocupan cajones GENERALES.
    /// Los visitantes en cajón reservado de un evento vigente NO se cuentan aquí
    /// (ya están descontados en la capacidad efectiva).
    /// </summary>
    public static int ContarVehiculosAdentro(string estacionamiento, List<RegistroAcceso> accesos, List<EventoCampus> eventos, DateTime ahora)
    {
        int contador = 0;
        foreach (RegistroAcceso acceso in accesos)
        {
            if (acceso.EstaAdentro == false || acceso.Estacionamiento != estacionamiento)
            {
                continue;
            }
            if (acceso.UsaCajonReservado && EventoSigueVigente(acceso.CodigoEvento, eventos, ahora))
            {
                continue;
            }
            contador = contador + 1;
        }
        return contador;
    }

    /// <summary>Cuenta los visitantes que están adentro ocupando un cajón reservado de un evento vigente.</summary>
    public static int ContarReservadosOcupados(string estacionamiento, string codigoEvento, List<RegistroAcceso> accesos, List<EventoCampus> eventos, DateTime ahora)
    {
        int contador = 0;
        foreach (RegistroAcceso acceso in accesos)
        {
            if (acceso.EstaAdentro && acceso.Estacionamiento == estacionamiento && acceso.UsaCajonReservado
                && EventoSigueVigente(acceso.CodigoEvento, eventos, ahora))
            {
                // codigoEvento vacío = todos los eventos.
                if (codigoEvento == "" || acceso.CodigoEvento == codigoEvento)
                {
                    contador = contador + 1;
                }
            }
        }
        return contador;
    }

    private static bool EventoSigueVigente(string codigoEvento, List<EventoCampus> eventos, DateTime ahora)
    {
        foreach (EventoCampus evento in eventos)
        {
            if (evento.Codigo == codigoEvento)
            {
                return evento.EstaVigente(ahora);
            }
        }
        return false;
    }

    /// <summary>Calcula el resumen completo de aforo de un estacionamiento.</summary>
    public static ResumenAforo CalcularResumen(string estacionamiento, List<RegistroAcceso> accesos, List<EventoCampus> eventos, DateTime ahora)
    {
        int capacidadTotal = ObtenerCapacidadTotal(estacionamiento);
        int lugaresReservadosEventos = CalcularLugaresReservados(estacionamiento, eventos, ahora);
        int vehiculosAdentro = ContarVehiculosAdentro(estacionamiento, accesos, eventos, ahora);
        int reservadosOcupados = ContarReservadosOcupados(estacionamiento, "", accesos, eventos, ahora);

        // Fórmula 1: Capacidad Efectiva = Capacidad Total - Lugares Reservados por eventos
        int capacidadEfectiva = capacidadTotal - lugaresReservadosEventos;

        // Fórmula 2: Lugares Disponibles = Capacidad Efectiva - Vehículos Adentro (nunca negativo)
        int lugaresDisponibles = capacidadEfectiva - vehiculosAdentro;
        if (lugaresDisponibles < 0)
        {
            lugaresDisponibles = 0;
        }

        // Fórmula 3: % Ocupación = Vehículos Adentro / Capacidad Efectiva * 100
        // (se convierte a double ANTES de dividir; si no, la división entera da 0)
        double porcentajeOcupacion = 0.0;
        if (capacidadEfectiva > 0)
        {
            porcentajeOcupacion = ((double)vehiculosAdentro / capacidadEfectiva) * 100.0;
        }
        else if (vehiculosAdentro > 0)
        {
            porcentajeOcupacion = 100.0;
        }
        porcentajeOcupacion = Math.Round(porcentajeOcupacion, 1);

        ResumenAforo resumen = new ResumenAforo();
        resumen.Estacionamiento = estacionamiento;
        resumen.CapacidadTotal = capacidadTotal;
        resumen.LugaresReservados = lugaresReservadosEventos;
        resumen.CapacidadEfectiva = capacidadEfectiva;
        resumen.VehiculosAdentro = vehiculosAdentro;
        resumen.ReservadosOcupados = reservadosOcupados;
        resumen.LugaresDisponibles = lugaresDisponibles;
        resumen.PorcentajeOcupacion = porcentajeOcupacion;
        resumen.Semaforo = ClasificarSemaforo(porcentajeOcupacion);
        return resumen;
    }

    /// <summary>Semáforo: Verde &lt; 70 %, Dorado de 70 % a 90 %, Rojo &gt; 90 %.</summary>
    public static string ClasificarSemaforo(double porcentaje)
    {
        if (porcentaje > UmbralRojo)
        {
            return Semaforos.Rojo;
        }
        else if (porcentaje >= UmbralDorado)
        {
            return Semaforos.Dorado;
        }
        else
        {
            return Semaforos.Verde;
        }
    }

    /// <summary>Regresa una frase con el estacionamiento que tiene más lugares disponibles en este momento.</summary>
    public static string RecomendarEstacionamiento(ResumenAforo facpya, ResumenAforo estadio)
    {
        ResumenAforo mejor = facpya;
        if (estadio.LugaresDisponibles > facpya.LugaresDisponibles)
        {
            mejor = estadio;
        }

        if (mejor.LugaresDisponibles <= 0)
        {
            return "Los dos estacionamientos están llenos.";
        }
        return "Conviene ir a " + mejor.Estacionamiento + ": tiene " + mejor.LugaresDisponibles + " lugares disponibles.";
    }

    /// <summary>
    /// Fórmula 4: Tiempo de estancia = Fecha de salida - Fecha de entrada.
    /// Si el vehículo sigue adentro, se usa la hora actual como salida.
    /// </summary>
    public static TimeSpan CalcularTiempoEstancia(RegistroAcceso registro, DateTime ahora)
    {
        DateTime fechaEntrada = registro.FechaEntrada;
        DateTime fechaSalida;
        if (registro.FechaSalida != null)
        {
            fechaSalida = registro.FechaSalida.Value;
        }
        else
        {
            fechaSalida = ahora;
        }

        TimeSpan tiempoEstancia = fechaSalida - fechaEntrada;
        if (tiempoEstancia < TimeSpan.Zero)
        {
            tiempoEstancia = TimeSpan.Zero;
        }
        return tiempoEstancia;
    }

    /// <summary>
    /// Promedio de estancia en minutos de una categoría ("Alumno", "Docente" o "Visitante"),
    /// usando solo las estancias que ya terminaron. Promedio = suma de minutos / número de estancias.
    /// </summary>
    public static double CalcularPromedioEstanciaMinutos(string categoria, List<RegistroAcceso> accesos, DateTime ahora)
    {
        double sumaMinutos = 0;
        int numeroEstancias = 0;

        foreach (RegistroAcceso acceso in accesos)
        {
            if (acceso.Categoria == categoria && acceso.EstaAdentro == false)
            {
                TimeSpan tiempoEstancia = CalcularTiempoEstancia(acceso, ahora);
                sumaMinutos = sumaMinutos + tiempoEstancia.TotalMinutes;
                numeroEstancias = numeroEstancias + 1;
            }
        }

        if (numeroEstancias == 0)
        {
            return 0;
        }

        double promedioMinutos = sumaMinutos / numeroEstancias;
        return Math.Round(promedioMinutos, 1);
    }

    /// <summary>Vehículos que siguen adentro y ya rebasaron el límite de horas, del que lleva más tiempo al que lleva menos.</summary>
    public static List<RegistroAcceso> ObtenerVehiculosOlvidados(List<RegistroAcceso> accesos, DateTime ahora)
    {
        return accesos
            .Where(acceso => acceso.EstaAdentro && CalcularTiempoEstancia(acceso, ahora).TotalHours >= HorasParaConsiderarOlvidado)
            .OrderBy(acceso => acceso.FechaEntrada)
            .ToList();
    }

    /// <summary>Pases del evento que están en estado "Activo" (no revocados ni expirados).</summary>
    public static int ContarPasesActivos(EventoCampus evento, List<PaseVisitante> pases, DateTime ahora)
    {
        int contador = 0;
        foreach (PaseVisitante pase in pases)
        {
            if (pase.CodigoEvento == evento.Codigo && pase.ObtenerEstado(ahora) == EstadosDePase.Activo)
            {
                contador = contador + 1;
            }
        }
        return contador;
    }

    /// <summary>Fórmula 5: % Cupo del evento = Pases Activos / Máximo de Visitantes * 100.</summary>
    public static double CalcularPorcentajeCupoEvento(EventoCampus evento, List<PaseVisitante> pases, DateTime ahora)
    {
        int pasesActivosEvento = ContarPasesActivos(evento, pases, ahora);
        int maxVisitantesEvento = evento.MaxVisitantes;

        if (maxVisitantesEvento <= 0)
        {
            return 0;
        }

        double porcentajeCupoEvento = ((double)pasesActivosEvento / maxVisitantesEvento) * 100.0;
        return Math.Round(porcentajeCupoEvento, 1);
    }

    /// <summary>Entradas de un día agrupadas por hora: posición 0 = 00:00-00:59 ... posición 23 = 23:00-23:59.</summary>
    public static int[] ContarEntradasPorHora(List<RegistroAcceso> accesos, DateTime dia)
    {
        int[] entradasPorHora = new int[24];
        foreach (RegistroAcceso acceso in accesos)
        {
            if (acceso.FechaEntrada.Date == dia.Date)
            {
                int hora = acceso.FechaEntrada.Hour;
                entradasPorHora[hora] = entradasPorHora[hora] + 1;
            }
        }
        return entradasPorHora;
    }

    /// <summary>Convierte un TimeSpan a texto: "2 h 05 min" o "45 min".</summary>
    public static string FormatearDuracion(TimeSpan duracion)
    {
        int totalMinutos = (int)Math.Floor(duracion.TotalMinutes);
        int horas = totalMinutos / 60;
        int minutos = totalMinutos % 60;

        if (horas > 0)
        {
            return horas + " h " + minutos.ToString("00") + " min";
        }
        else
        {
            return minutos + " min";
        }
    }
}
