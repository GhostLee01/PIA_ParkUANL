using System.Globalization;
using System.Text;

namespace PIA_ParkUANL_Lite;

// =====================================================================================
//  RESPUESTASASISTENTE.CS
//  Un método por cada tipo de pregunta. Solo consultan las listas en memoria de
//  GestorArchivos y arman el texto de la respuesta: nunca modifican datos.
// =====================================================================================
public static class RespuestasAsistente
{
    private const string FormatoFecha = "dd/MM/yyyy";
    private const string FormatoHora = "HH:mm";
    private const string FormatoDiaYHora = "dd/MM HH:mm";
    private const int MinimoLetrasDeNombre = 3;

    /// <summary>"hoy", "ayer" o "el 30/10/2026", para escribir las respuestas.</summary>
    private static string NombrarDia(DateTime dia, DateTime ahora)
    {
        if (dia.Date == ahora.Date)
        {
            return "hoy";
        }
        if (dia.Date == ahora.Date.AddDays(-1))
        {
            return "ayer";
        }
        return "el " + dia.ToString(FormatoFecha);
    }

    /// <summary>Texto para indicar los filtros aplicados, ej. " (FACPYA · Alumno)".</summary>
    private static string DescribirFiltros(FiltrosDePregunta filtros)
    {
        if (filtros.Estacionamiento == "" && filtros.Categoria == "")
        {
            return "";
        }
        if (filtros.Estacionamiento != "" && filtros.Categoria != "")
        {
            return " (" + filtros.Estacionamiento + " · " + filtros.Categoria + ")";
        }
        return " (" + filtros.Estacionamiento + filtros.Categoria + ")";
    }

    /// <summary>Filtro común: estacionamiento y categoría vacíos = sin filtro.</summary>
    private static bool CumpleFiltros(RegistroAcceso acceso, FiltrosDePregunta filtros)
    {
        if (filtros.Estacionamiento != "" && acceso.Estacionamiento != filtros.Estacionamiento)
        {
            return false;
        }
        if (filtros.Categoria != "" && acceso.Categoria != filtros.Categoria)
        {
            return false;
        }
        return true;
    }

    private static string DescribirLlegada(RegistroAcceso acceso, string lugarYCajon)
    {
        return "• Conductor: " + acceso.Nombre + " (" + acceso.Categoria + ")\n• " + lugarYCajon;
    }

    /// <summary>Últimas 5 entradas. Si filtros.Fecha no es null, solo las de ese día (la última de ese día va primero).</summary>
    public static string UltimasEntradas(FiltrosDePregunta filtros, DateTime ahora)
    {
        DateTime? fecha = filtros.Fecha;

        // LINQ: ordena por fecha de entrada de la más nueva a la más vieja y toma 5.
        List<RegistroAcceso> ultimas = GestorArchivos.Accesos
            .Where(acceso => CumpleFiltros(acceso, filtros) && (fecha == null || acceso.FechaEntrada.Date == fecha.Value.Date))
            .OrderByDescending(acceso => acceso.FechaEntrada)
            .Take(5)
            .ToList();

        string textoDelDia = fecha == null ? "" : " " + NombrarDia(fecha.Value, ahora);
        if (ultimas.Count == 0)
        {
            return "No hay entradas registradas" + textoDelDia + DescribirFiltros(filtros) + ".";
        }

        RegistroAcceso masReciente = ultimas[0];
        string estado = masReciente.EstaAdentro ? "sigue adentro" : "ya salió a las " + masReciente.FechaSalida!.Value.ToString(FormatoHora);
        StringBuilder respuesta = new StringBuilder();
        respuesta.AppendLine("La placa más reciente en ingresar" + textoDelDia + DescribirFiltros(filtros) + " es " + masReciente.Placa + ":");
        respuesta.AppendLine(DescribirLlegada(masReciente, masReciente.Estacionamiento + ", cajón " + masReciente.Cajon + ", método " + masReciente.Metodo));
        respuesta.AppendLine("• Entró el " + masReciente.FechaEntrada.ToString(FormatoFecha) + " a las " + masReciente.FechaEntrada.ToString(FormatoHora) + " y " + estado);
        respuesta.AppendLine();
        respuesta.AppendLine("Últimas " + ultimas.Count + " entradas:");
        for (int i = 0; i < ultimas.Count; i++)
        {
            RegistroAcceso acceso = ultimas[i];
            respuesta.AppendLine((i + 1) + ". " + acceso.Placa + " · " + acceso.Categoria + " · " + acceso.Estacionamiento + " · " + acceso.FechaEntrada.ToString(FormatoDiaYHora));
        }
        return respuesta.ToString().TrimEnd();
    }

    /// <summary>Últimas 5 salidas. Si filtros.Fecha no es null, solo las de ese día.</summary>
    public static string UltimasSalidas(FiltrosDePregunta filtros, DateTime ahora)
    {
        DateTime? fecha = filtros.Fecha;

        // LINQ: solo los que ya salieron, ordenados por fecha de salida (la más nueva primero).
        List<RegistroAcceso> ultimas = GestorArchivos.Accesos
            .Where(acceso => acceso.FechaSalida != null && CumpleFiltros(acceso, filtros)
                             && (fecha == null || acceso.FechaSalida.Value.Date == fecha.Value.Date))
            .OrderByDescending(acceso => acceso.FechaSalida)
            .Take(5)
            .ToList();

        string textoDelDia = fecha == null ? "" : " " + NombrarDia(fecha.Value, ahora);
        if (ultimas.Count == 0)
        {
            return "No hay salidas registradas" + textoDelDia + DescribirFiltros(filtros) + ".";
        }

        StringBuilder respuesta = new StringBuilder();
        respuesta.AppendLine("Últimas " + ultimas.Count + " salidas" + textoDelDia + DescribirFiltros(filtros) + ":");
        for (int i = 0; i < ultimas.Count; i++)
        {
            RegistroAcceso acceso = ultimas[i];
            TimeSpan estancia = CalculadoraAforo.CalcularTiempoEstancia(acceso, ahora);
            respuesta.AppendLine((i + 1) + ". " + acceso.Placa + " · " + acceso.Categoria + " · " + acceso.Estacionamiento + " · salió " +
                                 acceso.FechaSalida!.Value.ToString(FormatoDiaYHora) + " · estancia " + CalculadoraAforo.FormatearDuracion(estancia));
        }
        return respuesta.ToString().TrimEnd();
    }

    /// <summary>Primer vehículo en entrar en el día indicado (hoy, ayer, 30/10...).</summary>
    public static string PrimeraEntradaDelDia(DateTime dia, FiltrosDePregunta filtros, DateTime ahora)
    {
        // LINQ: entradas de ese día ordenadas de la más vieja a la más nueva; la primera es la que llegó antes.
        RegistroAcceso? primera = GestorArchivos.Accesos
            .Where(acceso => acceso.FechaEntrada.Date == dia.Date && CumpleFiltros(acceso, filtros))
            .OrderBy(acceso => acceso.FechaEntrada)
            .FirstOrDefault();

        string nombreDelDia = NombrarDia(dia, ahora);
        if (primera == null)
        {
            return "No hay entradas registradas " + nombreDelDia + DescribirFiltros(filtros) + ".";
        }

        string estado = primera.EstaAdentro ? "sigue adentro" : "salió a las " + primera.FechaSalida!.Value.ToString(FormatoHora);
        return "El primer vehículo en entrar " + nombreDelDia + DescribirFiltros(filtros) + " fue la placa " + primera.Placa + ":\n" +
               DescribirLlegada(primera, primera.Estacionamiento + ", cajón " + primera.Cajon) + "\n" +
               "• Entró a las " + primera.FechaEntrada.ToString(FormatoHora) + " y " + estado;
    }

    public static string MovimientosDelDia(DateTime dia, FiltrosDePregunta filtros, DateTime ahora)
    {
        int entradas = 0;
        int salidas = 0;
        int siguenAdentro = 0;
        int entradasFacpya = 0;
        int entradasEstadio = 0;

        foreach (RegistroAcceso acceso in GestorArchivos.Accesos)
        {
            if (CumpleFiltros(acceso, filtros) == false)
            {
                continue;
            }
            if (acceso.FechaEntrada.Date == dia.Date)
            {
                entradas = entradas + 1;
                if (acceso.EstaAdentro)
                {
                    siguenAdentro = siguenAdentro + 1;
                }
                if (acceso.Estacionamiento == Estacionamientos.Facpya)
                {
                    entradasFacpya = entradasFacpya + 1;
                }
                else
                {
                    entradasEstadio = entradasEstadio + 1;
                }
            }
            if (acceso.FechaSalida != null && acceso.FechaSalida.Value.Date == dia.Date)
            {
                salidas = salidas + 1;
            }
        }

        string nombreDelDia = NombrarDia(dia, ahora);
        string titulo = "del " + dia.ToString(FormatoFecha);
        if (nombreDelDia == "hoy" || nombreDelDia == "ayer")
        {
            titulo = "de " + nombreDelDia + " (" + dia.ToString(FormatoFecha) + ")";
        }

        StringBuilder respuesta = new StringBuilder();
        respuesta.AppendLine("Movimientos " + titulo + DescribirFiltros(filtros) + ":");
        respuesta.AppendLine("• Entradas: " + entradas);
        respuesta.AppendLine("• Salidas: " + salidas);
        respuesta.AppendLine("• De los que entraron " + nombreDelDia + ", siguen adentro: " + siguenAdentro);
        if (filtros.Estacionamiento == "")
        {
            respuesta.AppendLine("• Entradas por estacionamiento: FACPYA " + entradasFacpya + " · Estadio " + entradasEstadio);
        }
        return respuesta.ToString().TrimEnd();
    }

    public static string QuienEstaEnCajon(string cajon, DateTime ahora)
    {
        RegistroAcceso? ocupante = GestorArchivos.Accesos.FirstOrDefault(acceso => acceso.EstaAdentro && acceso.Cajon == cajon);
        if (ocupante == null)
        {
            return "El cajón " + cajon + " está libre en este momento.";
        }

        TimeSpan tiempo = CalculadoraAforo.CalcularTiempoEstancia(ocupante, ahora);
        return "El cajón " + cajon + " está ocupado por la placa " + ocupante.Placa + ":\n" +
               "• Conductor: " + ocupante.Nombre + " (" + ocupante.Categoria + ")\n" +
               "• Entró a las " + ocupante.FechaEntrada.ToString(FormatoHora) + " y lleva " + CalculadoraAforo.FormatearDuracion(tiempo);
    }

    public static string BuscarPorMatricula(string matricula, DateTime ahora)
    {
        // Primero se busca si está adentro; si no, su última visita.
        RegistroAcceso? registro = GestorArchivos.Accesos
            .Where(acceso => acceso.Matricula == matricula)
            .OrderByDescending(acceso => acceso.EstaAdentro)
            .ThenByDescending(acceso => acceso.FechaEntrada)
            .FirstOrDefault();

        if (registro != null)
        {
            return "Matrícula " + matricula + " → placa " + registro.Placa + ".\n" + TiempoDePlaca(registro.Placa, ahora);
        }

        Usuario? usuario = GestorArchivos.Usuarios.FirstOrDefault(cuenta => cuenta.Matricula == matricula);
        if (usuario != null)
        {
            return "La matrícula " + matricula + " pertenece a " + usuario.NombreCompleto + " (" + usuario.Categoria + "), " +
                   "pero no tiene visitas registradas.";
        }
        return "No encontré la matrícula " + matricula + " en los registros.";
    }

    /// <summary>Palabras de un nombre tal como quedan en una pregunta normalizada: minúsculas y sin acentos.</summary>
    private static string[] ObtenerPalabrasDelNombre(string nombre)
    {
        return InterpretePreguntas.QuitarAcentos(nombre.ToLower()).Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }

    public static string BuscarPorNombre(string textoNormalizado, DateTime ahora)
    {
        // De la pregunta solo se conservan las palabras que aparecen en algún nombre registrado:
        // así "¿sabes si llegó Miguel Hernández?" busca únicamente "miguel hernandez".
        HashSet<string> palabrasDeNombres = GestorArchivos.Accesos.SelectMany(acceso => ObtenerPalabrasDelNombre(acceso.Nombre)).ToHashSet();
        List<string> palabras = textoNormalizado.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(palabra => palabra.Length >= MinimoLetrasDeNombre && palabrasDeNombres.Contains(palabra))
            .Distinct()
            .ToList();

        if (palabras.Count == 0)
        {
            return "No encontré ese nombre en los registros. Escribe nombre y apellido, por ejemplo: \"busca a Roberto Martínez\".";
        }

        // LINQ: personas cuyo nombre contiene TODAS las palabras escritas (los que están adentro aparecen primero).
        List<RegistroAcceso> encontrados = GestorArchivos.Accesos
            .Where(acceso => palabras.All(palabra => ObtenerPalabrasDelNombre(acceso.Nombre).Contains(palabra)))
            .OrderByDescending(acceso => acceso.EstaAdentro)
            .ThenByDescending(acceso => acceso.FechaEntrada)
            .Take(5)
            .ToList();

        string nombreBuscado = string.Join(" ", palabras);
        if (encontrados.Count == 0)
        {
            return "No encontré a nadie llamado \"" + nombreBuscado + "\" en los registros.";
        }

        StringBuilder respuesta = new StringBuilder();
        respuesta.AppendLine("Coincidencias para \"" + nombreBuscado + "\":");
        foreach (RegistroAcceso acceso in encontrados)
        {
            string estado = acceso.EstaAdentro
                ? "ADENTRO desde las " + acceso.FechaEntrada.ToString(FormatoHora) + " (" + CalculadoraAforo.FormatearDuracion(CalculadoraAforo.CalcularTiempoEstancia(acceso, ahora)) + ")"
                : "visita del " + acceso.FechaEntrada.ToString(FormatoDiaYHora);
            respuesta.AppendLine("• " + acceso.Nombre + " (" + acceso.Categoria + ") · " + acceso.Placa + " · " + acceso.Estacionamiento + " " + acceso.Cajon + " · " + estado);
        }
        return respuesta.ToString().TrimEnd();
    }

    public static string PasesPorVencer(DateTime ahora)
    {
        // LINQ: pases activos ordenados por fecha de expiración (el que vence antes, primero).
        List<PaseVisitante> pases = GestorArchivos.Pases
            .Where(pase => pase.ObtenerEstado(ahora) == EstadosDePase.Activo)
            .OrderBy(pase => pase.FechaExpiracion)
            .Take(5)
            .ToList();

        if (pases.Count == 0)
        {
            return "No hay pases activos en este momento.";
        }

        StringBuilder respuesta = new StringBuilder();
        respuesta.AppendLine("Pases activos más próximos a vencer:");
        foreach (PaseVisitante pase in pases)
        {
            TimeSpan restante = pase.FechaExpiracion - ahora;
            respuesta.AppendLine("• " + pase.Id + " · " + pase.NombreVisitante + " · " + pase.Placa + " · " + pase.CodigoEvento +
                                 " · vence " + pase.FechaExpiracion.ToString(FormatoDiaYHora) + " (en " + CalculadoraAforo.FormatearDuracion(restante) + ")");
        }
        return respuesta.ToString().TrimEnd();
    }

    public static string PasesRevocados()
    {
        List<PaseVisitante> revocados = GestorArchivos.Pases.Where(pase => pase.Revocado).ToList();
        if (revocados.Count == 0)
        {
            return "No hay pases revocados.";
        }

        StringBuilder respuesta = new StringBuilder();
        respuesta.AppendLine("Pases revocados: " + revocados.Count);
        foreach (PaseVisitante pase in revocados.Take(10))
        {
            respuesta.AppendLine("• " + pase.Id + " · " + pase.NombreVisitante + " · " + pase.Placa + " · " + pase.CodigoEvento);
        }
        return respuesta.ToString().TrimEnd();
    }

    public static string VehiculosDeEventos(DateTime ahora)
    {
        StringBuilder respuesta = new StringBuilder();
        respuesta.AppendLine("Vehículos de eventos que están adentro:");
        int eventosVigentes = 0;

        foreach (EventoCampus evento in GestorArchivos.Eventos)
        {
            if (evento.EstaVigente(ahora) == false)
            {
                continue;
            }
            eventosVigentes++;

            // LINQ: vehículos adentro que llegaron con este evento.
            List<RegistroAcceso> delEvento = GestorArchivos.Accesos
                .Where(acceso => acceso.EstaAdentro && acceso.CodigoEvento == evento.Codigo)
                .ToList();
            int enReservado = delEvento.Count(acceso => acceso.UsaCajonReservado);

            respuesta.AppendLine("• " + evento.Titulo + ": " + delEvento.Count + " vehículos (" + enReservado + " en cajón reservado de " +
                                 evento.LugaresReservadosFacpya + ")");
            if (delEvento.Count > 0)
            {
                respuesta.AppendLine("    Placas: " + string.Join(", ", delEvento.Take(8).Select(acceso => acceso.Placa)) + (delEvento.Count > 8 ? "…" : ""));
            }
        }

        if (eventosVigentes == 0)
        {
            return "No hay eventos vigentes.";
        }
        return respuesta.ToString().TrimEnd();
    }

    public static string CompararEstacionamientos(DateTime ahora)
    {
        ResumenAforo facpya = CalculadoraAforo.CalcularResumen(Estacionamientos.Facpya, GestorArchivos.Accesos, GestorArchivos.Eventos, ahora);
        ResumenAforo estadio = CalculadoraAforo.CalcularResumen(Estacionamientos.Estadio, GestorArchivos.Accesos, GestorArchivos.Eventos, ahora);

        return CalculadoraAforo.RecomendarEstacionamiento(facpya, estadio) + "\n" +
               "• FACPYA: " + facpya.LugaresDisponibles + " disponibles (" + facpya.PorcentajeOcupacion + " %, " + facpya.Semaforo + ")\n" +
               "• Estadio: " + estadio.LugaresDisponibles + " disponibles (" + estadio.PorcentajeOcupacion + " %, " + estadio.Semaforo + ")";
    }

    public static string TiempoDePlaca(string placa, DateTime ahora)
    {
        // LINQ: el registro de esa placa que sigue adentro (si hay).
        RegistroAcceso? adentro = GestorArchivos.Accesos
            .FirstOrDefault(acceso => Validaciones.EsMismaPlaca(acceso.Placa, placa) && acceso.EstaAdentro);

        if (adentro != null)
        {
            TimeSpan tiempo = CalculadoraAforo.CalcularTiempoEstancia(adentro, ahora);
            double promedio = CalculadoraAforo.CalcularPromedioEstanciaMinutos(adentro.Categoria, GestorArchivos.Accesos, ahora);
            string respuesta = "La placa " + placa + " SÍ está dentro de " + adentro.Estacionamiento + ".\n" +
                               "• Conductor: " + adentro.Nombre + " (" + adentro.Categoria + ")\n" +
                               "• Cajón: " + adentro.Cajon + "\n" +
                               "• Entrada: " + adentro.FechaEntrada.ToString(FormatoFecha + " " + FormatoHora) + "\n" +
                               "• Tiempo que lleva: " + CalculadoraAforo.FormatearDuracion(tiempo) +
                               "  (" + ahora.ToString(FormatoHora) + " − " + adentro.FechaEntrada.ToString(FormatoHora) + ")\n";
            if (promedio > 0)
            {
                respuesta += "• Promedio de estancia de " + adentro.Categoria + ": " + promedio + " min";
            }
            return respuesta;
        }

        // LINQ: la última visita registrada de esa placa.
        RegistroAcceso? ultimaVisita = GestorArchivos.Accesos
            .Where(acceso => Validaciones.EsMismaPlaca(acceso.Placa, placa))
            .OrderByDescending(acceso => acceso.FechaEntrada)
            .FirstOrDefault();

        if (ultimaVisita != null)
        {
            TimeSpan duracion = CalculadoraAforo.CalcularTiempoEstancia(ultimaVisita, ahora);
            return "La placa " + placa + " no está adentro en este momento.\n" +
                   "• Última visita: " + ultimaVisita.Estacionamiento + ", " + ultimaVisita.FechaEntrada.ToString(FormatoFecha + " " + FormatoHora) +
                   " a " + ultimaVisita.FechaSalida?.ToString(FormatoHora) + " (" + CalculadoraAforo.FormatearDuracion(duracion) + ")";
        }

        return "No encontré la placa " + placa + " en los registros.";
    }

    /// <summary>Entradas agrupadas por hora del día indicado.</summary>
    public static string ReporteEntradasPorHora(DateTime dia, FiltrosDePregunta filtros, DateTime ahora)
    {
        // LINQ: solo los accesos del estacionamiento y la categoría pedidos (o todos).
        List<RegistroAcceso> accesos = GestorArchivos.Accesos.Where(acceso => CumpleFiltros(acceso, filtros)).ToList();
        int[] entradasPorHora = CalculadoraAforo.ContarEntradasPorHora(accesos, dia.Date);

        string nombreDelDia = NombrarDia(dia, ahora);
        int total = entradasPorHora.Sum();
        if (total == 0)
        {
            return "No hay entradas registradas " + nombreDelDia + DescribirFiltros(filtros) + ".";
        }
        int maximo = entradasPorHora.Max();
        int horaPico = Array.IndexOf(entradasPorHora, maximo);

        StringBuilder respuesta = new StringBuilder();
        respuesta.AppendLine("Entradas por hora " + nombreDelDia + DescribirFiltros(filtros) + ":");
        for (int hora = 0; hora < 24; hora++)
        {
            if (entradasPorHora[hora] > 0)
            {
                int largoBarra = (int)Math.Ceiling(entradasPorHora[hora] * 25.0 / maximo);
                respuesta.AppendLine(hora.ToString("00") + ":00  " + entradasPorHora[hora].ToString().PadLeft(4) + "  " + new string('█', largoBarra));
            }
        }
        respuesta.AppendLine("• Total: " + total + " entradas");
        respuesta.Append("• Hora pico: " + horaPico.ToString("00") + ":00 con " + maximo + " entradas");
        return respuesta.ToString();
    }

    /// <summary>Vehículos adentro por categoría. Si la pregunta nombra una categoría, solo se muestra esa.</summary>
    public static string ConteoPorCategoria(FiltrosDePregunta filtros)
    {
        StringBuilder respuesta = new StringBuilder();
        respuesta.AppendLine("Vehículos adentro por categoría:");

        string[] lotes = filtros.Estacionamiento == "" ? Estacionamientos.Todos : new[] { filtros.Estacionamiento };
        foreach (string lote in lotes)
        {
            // LINQ: los que están adentro de este estacionamiento.
            List<RegistroAcceso> adentro = GestorArchivos.Accesos
                .Where(acceso => acceso.EstaAdentro && acceso.Estacionamiento == lote)
                .ToList();

            respuesta.AppendLine(lote + " (" + adentro.Count + " en total):");
            AgregarLineaDeCategoria(respuesta, "Alumnos", Categorias.Alumno, adentro, filtros);
            AgregarLineaDeCategoria(respuesta, "Docentes", Categorias.Docente, adentro, filtros);
            AgregarLineaDeCategoria(respuesta, "Visitantes", Categorias.Visitante, adentro, filtros);
        }
        return respuesta.ToString().TrimEnd();
    }

    private static void AgregarLineaDeCategoria(StringBuilder respuesta, string titulo, string categoria, List<RegistroAcceso> adentro, FiltrosDePregunta filtros)
    {
        if (filtros.Categoria == "" || filtros.Categoria == categoria)
        {
            respuesta.AppendLine("  • " + titulo + ": " + adentro.Count(acceso => acceso.Categoria == categoria));
        }
    }

    /// <summary>
    /// Sin fecha (o con fecha de hoy): el vehículo que lleva más tiempo adentro AHORA.
    /// Con una fecha pasada: la estancia más larga de los vehículos que entraron ese día.
    /// </summary>
    public static string MayorEstancia(FiltrosDePregunta filtros, DateTime ahora)
    {
        DateTime? fecha = filtros.Fecha;
        if (fecha != null && fecha.Value.Date != ahora.Date)
        {
            // LINQ: estancias que empezaron ese día, ordenadas de la más larga a la más corta.
            RegistroAcceso? masLarga = GestorArchivos.Accesos
                .Where(acceso => acceso.FechaEntrada.Date == fecha.Value.Date && CumpleFiltros(acceso, filtros))
                .OrderByDescending(acceso => CalculadoraAforo.CalcularTiempoEstancia(acceso, ahora))
                .FirstOrDefault();

            string nombreDelDia = NombrarDia(fecha.Value, ahora);
            if (masLarga == null)
            {
                return "No hay entradas registradas " + nombreDelDia + DescribirFiltros(filtros) + ".";
            }

            TimeSpan duracion = CalculadoraAforo.CalcularTiempoEstancia(masLarga, ahora);
            string salida = masLarga.EstaAdentro ? "sigue adentro" : "salió a las " + masLarga.FechaSalida!.Value.ToString(FormatoHora);
            return "La estancia más larga de los que entraron " + nombreDelDia + DescribirFiltros(filtros) + " fue la placa " + masLarga.Placa + ":\n" +
                   DescribirLlegada(masLarga, masLarga.Estacionamiento + ", cajón " + masLarga.Cajon) + "\n" +
                   "• Entró a las " + masLarga.FechaEntrada.ToString(FormatoHora) + ", " + salida + " · estancia de " + CalculadoraAforo.FormatearDuracion(duracion);
        }

        // LINQ: el que entró primero entre los que siguen adentro = el que lleva más tiempo.
        RegistroAcceso? mayor = GestorArchivos.Accesos
            .Where(acceso => acceso.EstaAdentro && CumpleFiltros(acceso, filtros))
            .OrderBy(acceso => acceso.FechaEntrada)
            .FirstOrDefault();

        if (mayor == null)
        {
            return "No hay vehículos adentro" + DescribirFiltros(filtros) + " en este momento.";
        }

        TimeSpan tiempo = CalculadoraAforo.CalcularTiempoEstancia(mayor, ahora);
        return "Auto con mayor estancia actual" + DescribirFiltros(filtros) + ": placa " + mayor.Placa + "\n" +
               DescribirLlegada(mayor, mayor.Estacionamiento + ", cajón " + mayor.Cajon) + "\n" +
               "• Entró a las " + mayor.FechaEntrada.ToString(FormatoHora) + " y lleva " + CalculadoraAforo.FormatearDuracion(tiempo);
    }

    /// <summary>Promedio de estancia por categoría. Respeta el estacionamiento, la categoría y la fecha que diga la pregunta.</summary>
    public static string PromediosDeEstancia(FiltrosDePregunta filtros, DateTime ahora)
    {
        DateTime? fecha = filtros.Fecha;
        List<RegistroAcceso> accesos = GestorArchivos.Accesos
            .Where(acceso => filtros.Estacionamiento == "" || acceso.Estacionamiento == filtros.Estacionamiento)
            .Where(acceso => fecha == null || acceso.FechaEntrada.Date == fecha.Value.Date)
            .ToList();

        string textoDelDia = fecha == null ? "" : " " + NombrarDia(fecha.Value, ahora);
        StringBuilder respuesta = new StringBuilder();
        respuesta.AppendLine("Promedio de estancia" + textoDelDia + DescribirFiltros(filtros) + " (estancias concluidas):");
        AgregarLineaDePromedio(respuesta, "Alumnos", Categorias.Alumno, accesos, filtros, ahora);
        AgregarLineaDePromedio(respuesta, "Docentes", Categorias.Docente, accesos, filtros, ahora);
        AgregarLineaDePromedio(respuesta, "Visitantes", Categorias.Visitante, accesos, filtros, ahora);
        respuesta.Append("Fórmula: suma de (salida − entrada) ÷ número de estancias.");
        return respuesta.ToString();
    }

    private static void AgregarLineaDePromedio(StringBuilder respuesta, string titulo, string categoria, List<RegistroAcceso> accesos, FiltrosDePregunta filtros, DateTime ahora)
    {
        if (filtros.Categoria == "" || filtros.Categoria == categoria)
        {
            respuesta.AppendLine("• " + titulo + ": " + CalculadoraAforo.CalcularPromedioEstanciaMinutos(categoria, accesos, ahora) + " min");
        }
    }

    public static string EstadoEventos(DateTime ahora)
    {
        if (GestorArchivos.Eventos.Count == 0)
        {
            return "No hay eventos registrados.";
        }

        StringBuilder respuesta = new StringBuilder();
        respuesta.AppendLine("Estado de los eventos:");
        foreach (EventoCampus evento in GestorArchivos.Eventos.OrderBy(candidato => candidato.FechaInicio))
        {
            int pasesActivos = CalculadoraAforo.ContarPasesActivos(evento, GestorArchivos.Pases, ahora);
            double cupo = CalculadoraAforo.CalcularPorcentajeCupoEvento(evento, GestorArchivos.Pases, ahora);
            int ocupados = CalculadoraAforo.ContarReservadosOcupados(Estacionamientos.Facpya, evento.Codigo, GestorArchivos.Accesos, GestorArchivos.Eventos, ahora);

            respuesta.AppendLine("• " + evento.Titulo + " [" + evento.Codigo + "] — " + evento.ObtenerEstado(ahora).ToUpper());
            respuesta.AppendLine("    Pases activos: " + pasesActivos + " de " + evento.MaxVisitantes + " (" + cupo + " % de cupo)");
            respuesta.AppendLine("    Cajones reservados en FACPYA: " + evento.LugaresReservadosFacpya + " (ocupados: " + ocupados + ")");
        }
        return respuesta.ToString().TrimEnd();
    }

    /// <summary>Aforo actual. Si la pregunta nombra un estacionamiento, solo se muestra ese.</summary>
    public static string ResumenGeneral(DateTime ahora, string estacionamiento = "")
    {
        StringBuilder respuesta = new StringBuilder();
        respuesta.AppendLine("Aforo al " + ahora.ToString(FormatoFecha + " " + FormatoHora) + ":");
        foreach (string lote in estacionamiento == "" ? Estacionamientos.Todos : new[] { estacionamiento })
        {
            ResumenAforo resumen = CalculadoraAforo.CalcularResumen(lote, GestorArchivos.Accesos, GestorArchivos.Eventos, ahora);
            respuesta.AppendLine("• " + lote + ": " + resumen.VehiculosAdentro + " de " + resumen.CapacidadEfectiva + " cajones (" + resumen.PorcentajeOcupacion +
                                 " %, semáforo " + resumen.Semaforo + "), " + resumen.LugaresDisponibles + " disponibles");
            respuesta.AppendLine("    Capacidad efectiva = " + resumen.CapacidadTotal + " − " + resumen.LugaresReservados + " reservados = " + resumen.CapacidadEfectiva);
        }
        return respuesta.ToString().TrimEnd();
    }

    /// <summary>Vehículos que rebasaron el límite de horas adentro.</summary>
    public static string VehiculosOlvidados(FiltrosDePregunta filtros, DateTime ahora)
    {
        List<RegistroAcceso> olvidados = CalculadoraAforo.ObtenerVehiculosOlvidados(GestorArchivos.Accesos, ahora)
            .Where(acceso => CumpleFiltros(acceso, filtros))
            .ToList();

        if (olvidados.Count == 0)
        {
            return "No hay vehículos con más de " + CalculadoraAforo.HorasParaConsiderarOlvidado + " h adentro" + DescribirFiltros(filtros) + ".";
        }

        StringBuilder respuesta = new StringBuilder();
        respuesta.AppendLine(olvidados.Count + " vehículos llevan más de " + CalculadoraAforo.HorasParaConsiderarOlvidado + " h adentro" + DescribirFiltros(filtros) + ":");
        foreach (RegistroAcceso acceso in olvidados.Take(10))
        {
            respuesta.AppendLine("• " + acceso.Placa + " · " + acceso.Nombre + " · " + acceso.Estacionamiento + " " + acceso.Cajon +
                                 " · lleva " + CalculadoraAforo.FormatearDuracion(CalculadoraAforo.CalcularTiempoEstancia(acceso, ahora)));
        }
        respuesta.Append("Puedes registrar su salida en la pestaña \"Reportes y alertas\".");
        return respuesta.ToString();
    }

    /// <summary>Ocupación estimada de un día (y hora, si la pregunta la menciona), según el historial.</summary>
    public static string Pronostico(string textoNormalizado, FiltrosDePregunta filtros, DateTime ahora)
    {
        DateTime dia = InterpretePreguntas.ExtraerDiaDePronostico(textoNormalizado, ahora);
        int? hora = InterpretePreguntas.ExtraerHora(textoNormalizado);
        bool yaPaso = dia < ahora.Date;
        string nombreDelDia = dia.ToString(yaPaso ? "dddd dd/MM/yyyy" : "dddd dd/MM", new CultureInfo("es-MX"));
        string[] lotes = filtros.Estacionamiento == "" ? Estacionamientos.Todos : new[] { filtros.Estacionamiento };

        StringBuilder respuesta = new StringBuilder();
        respuesta.AppendLine((yaPaso ? "Ocupación real del " : "Pronóstico para el ") + nombreDelDia + (hora == null ? "" : " a las " + hora.Value.ToString("00") + ":00") + ":");
        PronosticoDelDia? pronostico = null;
        foreach (string lote in lotes)
        {
            pronostico = PronosticoOcupacion.Calcular(lote, dia, GestorArchivos.Accesos, ahora);
            if (pronostico == null)
            {
                return PronosticoOcupacion.ExplicarFaltaDePronostico(lote, dia, GestorArchivos.Accesos);
            }

            PuntoPronostico punto = hora == null ? PronosticoOcupacion.ObtenerHoraPico(pronostico) : pronostico.Horas[hora.Value];
            string encabezado = hora == null ? "hora pico " + punto.Hora.ToString("00") + ":00 con " : "";
            respuesta.AppendLine("• " + lote + ": " + encabezado + (yaPaso ? "" : "unos ") + Math.Round(punto.VehiculosEstimados) + " vehículos (" +
                                 punto.PorcentajeOcupacion + " %, " + punto.Semaforo + ")");
        }

        if (yaPaso)
        {
            return respuesta.ToString().TrimEnd();
        }

        string baseDelCalculo = pronostico!.UsaMismoDiaDeLaSemana ? "el mismo día de la semana" : "todos los días registrados";
        respuesta.Append("Es una estimación: promedio de " + pronostico.DiasDeHistorial + " día(s) de historial (" + baseDelCalculo + ").");
        return respuesta.ToString();
    }

    public static string FueraDelAlcance()
    {
        return "Esa pregunta está fuera de lo que puedo responder: solo tengo datos de los estacionamientos FACPYA y Estadio, " +
               "que son gratuitos. Pregúntame por aforo, placas, entradas y salidas, estancias, eventos o pases.";
    }

    public static string Saludo()
    {
        return "¡Hola! Soy el asistente de ParkUANL.\n\n" + Ayuda();
    }

    public static string Agradecimiento()
    {
        return "¡Con gusto! Si necesitas otro dato de los estacionamientos, pregúntame.";
    }

    public static string Ayuda()
    {
        return "Puedo responder preguntas como:\n" +
               "Vehículos y personas\n" +
               "• ¿Cuánto tiempo lleva la placa RMS-221-B?\n" +
               "• Busca la matrícula 2077345  ·  Busca a Roberto Martínez\n" +
               "• ¿Quién está en el cajón F-001?\n" +
               "• ¿Cuál es la placa más reciente en ingresar?  ·  ¿Quién fue el último en salir?\n" +
               "• ¿Quién fue el primero en entrar hoy?  ·  ¿Qué auto lleva más tiempo estacionado?\n" +
               "• ¿Hay vehículos olvidados?\n" +
               "Conteos y aforo\n" +
               "• ¿Cuántos alumnos, docentes y visitantes hay en FACPYA?\n" +
               "• ¿Cuántas entradas y salidas hubo hoy? (o ayer)\n" +
               "• Reporte de entradas por hora\n" +
               "• ¿Cuántos lugares disponibles hay?  ·  ¿Qué estacionamiento tiene más lugares?\n" +
               "• Promedio de estancia por categoría\n" +
               "• ¿Cómo estará FACPYA mañana a las 10?\n" +
               "Eventos y pases\n" +
               "• ¿Cómo están los eventos?  ·  ¿Cuántos vehículos del congreso hay adentro?\n" +
               "• ¿Qué pases están por vencer?  ·  ¿Qué pases están revocados?\n" +
               "Puedes agregar FACPYA o Estadio y alumno, docente o visitante para filtrar (ej. \"último docente en entrar a FACPYA\").";
    }
}
