using System.Text.RegularExpressions;

namespace PIA_ParkUANL_Lite;

// =====================================================================================
//  INTERPRETEPREGUNTAS.CS
//  Saca de una pregunta escrita los datos que importan: fecha, hora, placa, estacionamiento...
// =====================================================================================

/// <summary>Filtros que se detectaron en la pregunta. Vacío o null = la pregunta no los mencionó.</summary>
public class FiltrosDePregunta
{
    public string Estacionamiento { get; set; } = "";
    public string Categoria { get; set; } = "";
    public DateTime? Fecha { get; set; }
    public bool FechaInvalida { get; set; }
}

public static class InterpretePreguntas
{
    private static readonly Regex PatronPlaca = new Regex(@"[A-Z]{3}-[0-9]{3,4}(-[A-Z])?");   // ABC-123, SRT-458-A
    private static readonly Regex PatronCajon = new Regex(@"\b[FE]-R?[0-9]{2,4}\b");          // F-001, E-0259, F-R01
    private static readonly Regex PatronMatricula = new Regex(@"\b[0-9]{7}\b");               // 2077345

    private static readonly Regex PatronFechaConMes = new Regex(
        @"\b([0-9]{1,2}) de (enero|febrero|marzo|abril|mayo|junio|julio|agosto|septiembre|setiembre|octubre|noviembre|diciembre)(?: de(?:l)? ([0-9]{4}))?\b");

    // Con diagonal el año es opcional (30/10). Con guiones debe llevar año (30-10-2026): "13-14 horas" no es una fecha.
    private static readonly Regex PatronFechaNumerica = new Regex(
        @"\b(?<dia>[0-9]{1,2})/(?<mes>[0-9]{1,2})(?:/(?<anio>[0-9]{2,4}))?\b|\b(?<dia>[0-9]{1,2})-(?<mes>[0-9]{1,2})-(?<anio>[0-9]{2,4})\b");

    private static readonly Regex PatronHora = new Regex(@"\b(?:a las|a la|las)\s+([0-9]{1,2})(?:\s+[0-9]{2})?\s*(am|pm)?\b");

    private static readonly string[] NombresDeMeses =
    {
        "enero", "febrero", "marzo", "abril", "mayo", "junio", "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre"
    };

    // En el orden de DayOfWeek: domingo = 0.
    private static readonly string[] NombresDeDias = { "domingo", "lunes", "martes", "miercoles", "jueves", "viernes", "sabado" };

    private static readonly string[] SignosDePuntuacion = { "¿", "?", "¡", "!", ".", ",", ";", ":", "(", ")", "\"" };

    /// <summary>
    /// Minúsculas, sin acentos, sin signos y con un espacio en cada orilla.
    /// Así " ayer " se encuentra aunque la pregunta diga "¿...ayer?" y las reglas pueden buscar palabras completas.
    /// </summary>
    public static string NormalizarTexto(string pregunta)
    {
        string texto = QuitarAcentos(pregunta.ToLower());
        foreach (string signo in SignosDePuntuacion)
        {
            texto = texto.Replace(signo, " ");
        }
        return " " + texto + " ";
    }

    public static string QuitarAcentos(string texto)
    {
        return texto.Replace("á", "a").Replace("é", "e").Replace("í", "i").Replace("ó", "o").Replace("ú", "u").Replace("ü", "u");
    }

    /// <summary>true si el texto contiene AL MENOS UNA de las palabras.</summary>
    public static bool ContieneAlguna(string texto, params string[] palabras)
    {
        foreach (string palabra in palabras)
        {
            if (texto.Contains(palabra))
            {
                return true;
            }
        }
        return false;
    }

    public static FiltrosDePregunta ExtraerFiltros(string textoNormalizado, DateTime ahora)
    {
        FiltrosDePregunta filtros = new FiltrosDePregunta();

        if (textoNormalizado.Contains("facpya"))
        {
            filtros.Estacionamiento = Estacionamientos.Facpya;
        }
        else if (textoNormalizado.Contains("estadio"))
        {
            filtros.Estacionamiento = Estacionamientos.Estadio;
        }

        if (ContieneAlguna(textoNormalizado, "alumno", "estudiante"))
        {
            filtros.Categoria = Categorias.Alumno;
        }
        else if (ContieneAlguna(textoNormalizado, "docente", "maestro", "profesor"))
        {
            filtros.Categoria = Categorias.Docente;
        }
        else if (textoNormalizado.Contains("visitante"))
        {
            filtros.Categoria = Categorias.Visitante;
        }

        bool fechaInvalida;
        filtros.Fecha = ExtraerFecha(textoNormalizado, ahora, out fechaInvalida);
        filtros.FechaInvalida = fechaInvalida;
        return filtros;
    }

    /// <summary>
    /// Regresa la primera placa de la pregunta (o ""). Primero busca una palabra que sea una placa
    /// ya registrada (cualquier formato); si no, una con el formato común ABC-123.
    /// </summary>
    public static string ExtraerPlaca(string preguntaEnMayusculas)
    {
        string textoSinSignos = preguntaEnMayusculas;
        foreach (string signo in SignosDePuntuacion)
        {
            textoSinSignos = textoSinSignos.Replace(signo, " ");
        }

        foreach (string palabra in textoSinSignos.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (Validaciones.EsPlacaValida(palabra) && EsPlacaRegistrada(palabra))
            {
                return palabra;
            }
        }

        return PatronPlaca.Match(preguntaEnMayusculas).Value;
    }

    private static bool EsPlacaRegistrada(string placa)
    {
        return GestorArchivos.Accesos.Any(acceso => Validaciones.EsMismaPlaca(acceso.Placa, placa))
               || ServicioCuentas.BuscarDuenoDePlaca(placa) != null;
    }

    public static string ExtraerCajon(string preguntaEnMayusculas)
    {
        return PatronCajon.Match(preguntaEnMayusculas).Value;
    }

    public static string ExtraerMatricula(string pregunta)
    {
        return PatronMatricula.Match(pregunta).Value;
    }

    /// <summary>
    /// Busca una fecha en el texto normalizado.
    /// Entiende "hoy", "ayer", "anteayer", "30 de octubre (de 2026)" y "30/10(/2026)".
    /// Si no trae año se usa el año actual; si esa fecha todavía no llega, se usa la del año pasado.
    /// Regresa null si no menciona fecha. Si menciona una que no existe (ej. "31 de noviembre"),
    /// regresa null y pone fechaInvalida = true para avisarle al usuario.
    /// </summary>
    public static DateTime? ExtraerFecha(string textoNormalizado, DateTime ahora, out bool fechaInvalida)
    {
        return ExtraerFecha(textoNormalizado, ahora, out fechaInvalida, out _);
    }

    private static DateTime? ExtraerFecha(string textoNormalizado, DateTime ahora, out bool fechaInvalida, out bool esFechaExacta)
    {
        fechaInvalida = false;
        esFechaExacta = false;

        // Palabras relativas ("anteayer" se revisa primero porque también contiene "ayer").
        if (ContieneAlguna(textoNormalizado, " anteayer ", " antier ", " antes de ayer "))
        {
            esFechaExacta = true;
            return ahora.Date.AddDays(-2);
        }
        if (textoNormalizado.Contains(" ayer "))
        {
            esFechaExacta = true;
            return ahora.Date.AddDays(-1);
        }
        if (textoNormalizado.Contains(" hoy "))
        {
            return ahora.Date;
        }

        int dia;
        int mes;
        string anioTexto;

        Match fechaConMes = PatronFechaConMes.Match(textoNormalizado);
        Match fechaNumerica = PatronFechaNumerica.Match(textoNormalizado);
        if (fechaConMes.Success)
        {
            dia = int.Parse(fechaConMes.Groups[1].Value);
            mes = Array.IndexOf(NombresDeMeses, fechaConMes.Groups[2].Value.Replace("setiembre", "septiembre")) + 1;
            anioTexto = fechaConMes.Groups[3].Value;
        }
        else if (fechaNumerica.Success)
        {
            dia = int.Parse(fechaNumerica.Groups["dia"].Value);
            mes = int.Parse(fechaNumerica.Groups["mes"].Value);
            anioTexto = fechaNumerica.Groups["anio"].Value;
        }
        else
        {
            return null;
        }

        int anio;
        if (anioTexto == "")
        {
            anio = ahora.Year;
        }
        else
        {
            esFechaExacta = true;
            anio = int.Parse(anioTexto);
            if (anio < 100)
            {
                anio = anio + 2000;   // "26" → 2026
            }
        }

        // Se valida que la fecha exista (no hay 31 de noviembre ni mes 13).
        if (mes < 1 || mes > 12 || anio < 2000 || anio > 2100 || dia < 1 || dia > DateTime.DaysInMonth(anio, mes))
        {
            fechaInvalida = true;
            return null;
        }

        DateTime fecha = new DateTime(anio, mes, dia);
        if (anioTexto == "" && fecha > ahora.Date)
        {
            fecha = fecha.AddYears(-1);   // "30 de diciembre" preguntado en noviembre = el del año pasado
        }
        return fecha;
    }

    /// <summary>Hora mencionada ("a las 10", "a las 3 pm") en formato de 0 a 23, o null si no menciona ninguna.</summary>
    public static int? ExtraerHora(string textoNormalizado)
    {
        Match coincidencia = PatronHora.Match(textoNormalizado);
        if (coincidencia.Success == false)
        {
            return null;
        }

        int hora = int.Parse(coincidencia.Groups[1].Value);
        string periodo = coincidencia.Groups[2].Value;
        if (periodo == "pm" && hora < 12)
        {
            hora = hora + 12;
        }
        else if (periodo == "am" && hora == 12)
        {
            hora = 0;   // las 12 am son la medianoche
        }
        if (hora > 23)
        {
            return null;
        }
        return hora;
    }

    /// <summary>
    /// Día de un pronóstico: "mañana", "pasado mañana", un día de la semana o una fecha ("10/11", "20 de noviembre").
    /// Una fecha sin año que ya pasó se entiende como la del año siguiente; con año, o si dice "ayer", es exactamente esa. Si no dice ninguno, hoy.
    /// </summary>
    public static DateTime ExtraerDiaDePronostico(string textoNormalizado, DateTime ahora)
    {
        if (textoNormalizado.Contains(" pasado mañana "))
        {
            return ahora.Date.AddDays(2);
        }

        // "la mañana" es una parte del día, no el día siguiente: se quita antes de buscar "mañana".
        if (textoNormalizado.Replace(" la mañana ", "  ").Contains(" mañana "))
        {
            return ahora.Date.AddDays(1);
        }

        for (int numeroDia = 0; numeroDia < NombresDeDias.Length; numeroDia++)
        {
            if (textoNormalizado.Contains(" " + NombresDeDias[numeroDia] + " "))
            {
                int diasQueFaltan = (numeroDia - (int)ahora.DayOfWeek + 7) % 7;
                return ahora.Date.AddDays(diasQueFaltan);
            }
        }

        DateTime? fecha = ExtraerFecha(textoNormalizado, ahora, out _, out bool esFechaExacta);
        if (fecha == null)
        {
            return ahora.Date;
        }
        return fecha.Value < ahora.Date && esFechaExacta == false ? fecha.Value.AddYears(1) : fecha.Value;
    }
}
