namespace PIA_ParkUANL;

// =====================================================================================
//  ASISTENTEIA.CS
//  Decide cómo se responde cada pregunta. Todo funciona en este equipo, sin internet:
//    1) Reglas para datos concretos (placa, cajón, matrícula).
//    2) Modelo ML.NET, que predice la intención de la pregunta.
//    3) Reglas de palabras clave, si el modelo no está seguro o no está listo.
// =====================================================================================

public class RespuestaIA
{
    public string Texto { get; set; } = "";
    public string Motor { get; set; } = "";
}

public class AsistenteIA
{
    // === [EXENCIÓN] AGENTE ANALÍTICO DE INTELIGENCIA ARTIFICIAL ===

    // Confianza mínima (0 a 1) para aceptar la intención que predice el modelo ML.NET; abajo de esto se usan las reglas.
    private const float ConfianzaMinimaModelo = 0.45f;

    // Qué parte del asistente respondió la última pregunta (se muestra en el chat).
    private string motorUsado = "";

    public RespuestaIA Preguntar(string pregunta)
    {
        string texto = Responder(pregunta);
        return new RespuestaIA { Texto = texto, Motor = motorUsado };
    }

    /// <summary>
    /// Detecta la intención de la pregunta y llama al método que la responde.
    /// Los datos NO se leen del disco aquí: se consultan las listas en memoria de GestorArchivos.
    /// </summary>
    private string Responder(string pregunta)
    {
        DateTime ahora = DateTime.Now;
        string texto = InterpretePreguntas.NormalizarTexto(pregunta);
        string preguntaEnMayusculas = pregunta.ToUpper();
        FiltrosDePregunta filtros = InterpretePreguntas.ExtraerFiltros(texto, ahora);

        // ---------- REGLAS CON DATOS CONCRETOS (van primero: el modelo no puede "extraer" una placa) ----------
        string placa = InterpretePreguntas.ExtraerPlaca(preguntaEnMayusculas);
        if (placa != "")
        {
            motorUsado = "Reglas (placa)";
            return RespuestasAsistente.TiempoDePlaca(placa, ahora);
        }

        string cajon = InterpretePreguntas.ExtraerCajon(preguntaEnMayusculas);
        if (cajon != "")
        {
            motorUsado = "Reglas (cajón)";
            return RespuestasAsistente.QuienEstaEnCajon(cajon, ahora);
        }

        string matricula = InterpretePreguntas.ExtraerMatricula(pregunta);
        if (matricula != "")
        {
            motorUsado = "Reglas (matrícula)";
            return RespuestasAsistente.BuscarPorMatricula(matricula, ahora);
        }

        // La fecha se revisa después de placa/cajón/matrícula para no confundirlas con fechas.
        if (filtros.FechaInvalida)
        {
            motorUsado = "Reglas (fecha inválida)";
            return "La fecha que escribiste no existe (por ejemplo, noviembre solo tiene 30 días). " +
                   "Escríbela como \"30 de octubre\" o \"30/10/2026\".";
        }

        // ---------- MODELO ENTRENADO (ML.NET) ----------
        // Predice la intención de la pregunta. Si está suficientemente seguro, responde con el método de esa intención.
        var prediccion = ClasificadorIntencion.Predecir(pregunta);
        if (prediccion.Confianza >= ConfianzaMinimaModelo)
        {
            string? respuestaModelo = ResponderPorIntencion(prediccion.Intencion, texto, ahora, filtros);
            if (respuestaModelo != null)
            {
                int porcentaje = (int)Math.Round(prediccion.Confianza * 100);
                motorUsado = "Modelo ML.NET · intención '" + prediccion.Intencion + "' (" + porcentaje + " %)";
                return respuestaModelo;
            }
        }

        // ---------- RESPALDO: REGLAS DE PALABRAS CLAVE ----------
        // Se usan si el modelo no está listo o si su confianza fue baja.
        if (ClasificadorIntencion.EstaListo)
        {
            motorUsado = "Reglas (respaldo)";
        }
        else
        {
            motorUsado = "Reglas (respaldo: modelo ML.NET no disponible)";
        }
        return ResponderPorPalabrasClave(texto, ahora, filtros);
    }

    /// <summary>
    /// Llama al método que responde cada intención predicha por el modelo ML.NET.
    /// Regresa null si la intención no se reconoce (entonces se usan las reglas).
    /// </summary>
    private static string? ResponderPorIntencion(string intencion, string texto, DateTime ahora, FiltrosDePregunta filtros)
    {
        // filtros.Fecha es null si la pregunta no mencionó ningún día; entonces se usa hoy.
        DateTime dia = filtros.Fecha ?? ahora.Date;

        switch (intencion)
        {
            case "aforo":
                return RespuestasAsistente.ResumenGeneral(ahora, filtros.Estacionamiento);
            case "comparar":
                return RespuestasAsistente.CompararEstacionamientos(ahora);
            case "conteo_categoria":
                return RespuestasAsistente.ConteoPorCategoria(filtros);
            case "ultima_entrada":
                return RespuestasAsistente.UltimasEntradas(filtros, ahora);
            case "ultima_salida":
                return RespuestasAsistente.UltimasSalidas(filtros, ahora);
            case "primera_entrada":
                return RespuestasAsistente.PrimeraEntradaDelDia(dia, filtros, ahora);
            case "mayor_estancia":
                return RespuestasAsistente.MayorEstancia(filtros, ahora);
            case "entradas_por_hora":
                return RespuestasAsistente.ReporteEntradasPorHora(dia, filtros, ahora);
            case "promedio_estancia":
                return RespuestasAsistente.PromediosDeEstancia(filtros, ahora);
            case "movimientos_dia":
                return RespuestasAsistente.MovimientosDelDia(dia, filtros, ahora);
            case "pases_por_vencer":
                return RespuestasAsistente.PasesPorVencer(ahora);
            case "pases_revocados":
                return RespuestasAsistente.PasesRevocados();
            case "vehiculos_evento":
                return RespuestasAsistente.VehiculosDeEventos(ahora);
            case "vehiculos_olvidados":
                return RespuestasAsistente.VehiculosOlvidados(filtros, ahora);
            case "estado_eventos":
                return RespuestasAsistente.EstadoEventos(ahora);
            case "buscar_nombre":
                return RespuestasAsistente.BuscarPorNombre(texto, ahora);
            case "pronostico":
                return RespuestasAsistente.Pronostico(texto, filtros, ahora);
            case "resumen":
                return RespuestasAsistente.ResumenGeneral(ahora) + "\n\n" + RespuestasAsistente.ConteoPorCategoria(new FiltrosDePregunta());
            case "saludo":
                return RespuestasAsistente.Saludo();
            case "agradecimiento":
                return RespuestasAsistente.Agradecimiento();
            case "ayuda":
                return RespuestasAsistente.Ayuda();
            case "fuera_de_dominio":
                return RespuestasAsistente.FueraDelAlcance();
            default:
                return null;
        }
    }

    /// <summary>
    /// IMPORTANTE: las reglas se revisan EN ORDEN y gana la primera que coincide; por eso las más
    /// específicas ("último en salir"...) van antes que las generales ("cuántos", "hay").
    /// </summary>
    private static string ResponderPorPalabrasClave(string texto, DateTime ahora, FiltrosDePregunta filtros)
    {
        DateTime dia = filtros.Fecha ?? ahora.Date;

        if (InterpretePreguntas.ContieneAlguna(texto, "pronostic", "prediccion", "estara", "va a estar", "se espera", "se esperan"))
        {
            return RespuestasAsistente.Pronostico(texto, filtros, ahora);
        }
        if (InterpretePreguntas.ContieneAlguna(texto, "olvidad", "abandonad", "estancia larga", "sin salida"))
        {
            return RespuestasAsistente.VehiculosOlvidados(filtros, ahora);
        }
        if (InterpretePreguntas.ContieneAlguna(texto, "ultim", "reciente") && InterpretePreguntas.ContieneAlguna(texto, "sali", "salio"))
        {
            return RespuestasAsistente.UltimasSalidas(filtros, ahora);
        }
        if (InterpretePreguntas.ContieneAlguna(texto, "primero en", "primer vehiculo", "primer auto", "primer carro", "primera entrada"))
        {
            return RespuestasAsistente.PrimeraEntradaDelDia(dia, filtros, ahora);
        }
        if (InterpretePreguntas.ContieneAlguna(texto, "ultim", "reciente", "acaba de entrar", "acaban de entrar"))
        {
            return RespuestasAsistente.UltimasEntradas(filtros, ahora);
        }

        // Va antes que "hora" para no confundir "lleva más horas".
        if (InterpretePreguntas.ContieneAlguna(texto, "mas tiempo", "duro mas", "mayor estancia", "lleva mas", "llevan mas", "mas horas", "mas antiguo", "estancia mas larga"))
        {
            return RespuestasAsistente.MayorEstancia(filtros, ahora);
        }

        // Con espacios para no confundir "ahora" con "hora".
        if (InterpretePreguntas.ContieneAlguna(texto, " hora ", " horas ", "por hora", "hora pico", "horario"))
        {
            return RespuestasAsistente.ReporteEntradasPorHora(dia, filtros, ahora);
        }
        if (InterpretePreguntas.ContieneAlguna(texto, "promedio", "cuanto tiempo se quedan", "cuanto duran"))
        {
            return RespuestasAsistente.PromediosDeEstancia(filtros, ahora);
        }
        if (InterpretePreguntas.ContieneAlguna(texto, "entrada", "entraron", "ingres", "llegaron", "salida", "salieron", "movimiento"))
        {
            return RespuestasAsistente.MovimientosDelDia(dia, filtros, ahora);
        }
        if (InterpretePreguntas.ContieneAlguna(texto, "vencer", "vence", "expira", "caduc"))
        {
            return RespuestasAsistente.PasesPorVencer(ahora);
        }
        if (texto.Contains("revocad"))
        {
            return RespuestasAsistente.PasesRevocados();
        }
        if (InterpretePreguntas.ContieneAlguna(texto, "evento", "congreso")
            && InterpretePreguntas.ContieneAlguna(texto, "vehiculo", "auto", "carro", "adentro", "estacionad", "cajon"))
        {
            return RespuestasAsistente.VehiculosDeEventos(ahora);
        }
        if (InterpretePreguntas.ContieneAlguna(texto, "evento", "pase", "congreso", "feria", "partido"))
        {
            return RespuestasAsistente.EstadoEventos(ahora);
        }

        // ¿Qué estacionamiento conviene? Va antes que aforo, porque también dice "lugares".
        if (InterpretePreguntas.ContieneAlguna(texto, "mas lugares", "mas espacio", "conviene", "mas libre", "menos lleno", "mas vacio", "cual tiene mas", "mas disponible"))
        {
            return RespuestasAsistente.CompararEstacionamientos(ahora);
        }
        if (InterpretePreguntas.ContieneAlguna(texto, "aforo", "disponible", "lugares", "ocupacion", "capacidad", "lleno", "libres", "espacio"))
        {
            return RespuestasAsistente.ResumenGeneral(ahora, filtros.Estacionamiento);
        }
        if (InterpretePreguntas.ContieneAlguna(texto, "busca", "nombre", "donde esta", "se llama"))
        {
            return RespuestasAsistente.BuscarPorNombre(texto, ahora);
        }
        if (InterpretePreguntas.ContieneAlguna(texto, "cuantos", "cuantas", "categoria", "alumno", "docente", "visitante", "maestro", "profesor", " hay ", "total"))
        {
            return RespuestasAsistente.ConteoPorCategoria(filtros);
        }
        if (InterpretePreguntas.ContieneAlguna(texto, "resumen", "estado", "como esta", "como va", "situacion", "reporte"))
        {
            return RespuestasAsistente.ResumenGeneral(ahora) + "\n\n" + RespuestasAsistente.ConteoPorCategoria(new FiltrosDePregunta());
        }
        if (InterpretePreguntas.ContieneAlguna(texto, " hola ", "buenos dias", "buenas tardes", "buenas noches"))
        {
            return RespuestasAsistente.Saludo();
        }
        if (texto.Contains("gracias"))
        {
            return RespuestasAsistente.Agradecimiento();
        }

        // Ninguna regla coincidió.
        return "No entendí la pregunta. " + RespuestasAsistente.Ayuda();
    }
}
