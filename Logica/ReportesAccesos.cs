using System.Text;

namespace PIA_ParkUANL_Lite;

// =====================================================================================
//  REPORTESACCESOS.CS
//  Reportes de accesos por fechas, estacionamiento y categoría, en CSV (Excel) o PDF,
//  y el resumen del cierre de turno. El PDF se escribe a mano para no usar librerías.
// =====================================================================================

public class FiltroReporte
{
    public DateTime Desde { get; set; }
    public DateTime Hasta { get; set; }
    public string Estacionamiento { get; set; } = ReportesAccesos.TodasLasOpciones;
    public string Categoria { get; set; } = ReportesAccesos.TodasLasOpciones;
}

public static class ReportesAccesos
{
    public const string TodasLasOpciones = "Todos";

    // Una celda que empieza así puede ejecutarse como fórmula en Excel.
    private const string InicioDeFormula = "=+-@\t\r";

    /// <summary>Accesos cuya entrada cae dentro del periodo, del más antiguo al más reciente.</summary>
    public static List<RegistroAcceso> ObtenerRegistros(FiltroReporte filtro)
    {
        return GestorArchivos.Accesos
            .Where(acceso => acceso.FechaEntrada.Date >= filtro.Desde.Date && acceso.FechaEntrada.Date <= filtro.Hasta.Date)
            .Where(acceso => filtro.Estacionamiento == TodasLasOpciones || acceso.Estacionamiento == filtro.Estacionamiento)
            .Where(acceso => filtro.Categoria == TodasLasOpciones || acceso.Categoria == filtro.Categoria)
            .OrderBy(acceso => acceso.FechaEntrada)
            .ToList();
    }

    public static void ExportarCsv(string ruta, List<RegistroAcceso> registros, DateTime ahora)
    {
        StringBuilder contenido = new StringBuilder();
        contenido.AppendLine("Placa,Nombre,Categoría,Matrícula,Estacionamiento,Cajón,Evento,Método,Entrada,Salida,Estancia (min),Operador");

        foreach (RegistroAcceso registro in registros)
        {
            string salida = registro.FechaSalida?.ToString("dd/MM/yyyy HH:mm") ?? "Adentro";
            int minutos = (int)CalculadoraAforo.CalcularTiempoEstancia(registro, ahora).TotalMinutes;
            string[] celdas =
            {
                registro.Placa, registro.Nombre, registro.Categoria, registro.Matricula, registro.Estacionamiento, registro.Cajon,
                registro.CodigoEvento, registro.Metodo, registro.FechaEntrada.ToString("dd/MM/yyyy HH:mm"), salida, minutos.ToString(), registro.Operador
            };
            contenido.AppendLine(string.Join(",", celdas.Select(PrepararCeldaCsv)));
        }

        // UTF-8 con BOM: así Excel muestra bien los acentos.
        File.WriteAllText(ruta, contenido.ToString(), new UTF8Encoding(true));
    }

    /// <summary>
    /// Pone comillas si hace falta. Si el texto empieza como una fórmula de Excel,
    /// se le antepone un apóstrofe: un nombre capturado en la caseta nunca debe ejecutarse como fórmula.
    /// </summary>
    public static string PrepararCeldaCsv(string celda)
    {
        if (celda != "" && InicioDeFormula.Contains(celda[0]))
        {
            celda = "'" + celda;
        }
        if (celda.Contains(',') || celda.Contains('"') || celda.Contains('\n') || celda.Contains('\r'))
        {
            return "\"" + celda.Replace("\"", "\"\"") + "\"";
        }
        return celda;
    }

    public static void ExportarPdf(string ruta, FiltroReporte filtro, List<RegistroAcceso> registros, DateTime ahora)
    {
        List<string> lineas = new List<string>();
        lineas.Add("ParkUANL - Reporte de accesos");
        lineas.Add("Periodo: " + filtro.Desde.ToString("dd/MM/yyyy") + " a " + filtro.Hasta.ToString("dd/MM/yyyy") +
                   "   Estacionamiento: " + filtro.Estacionamiento + "   Categoría: " + filtro.Categoria +
                   "   Registros: " + registros.Count);
        lineas.Add("Generado: " + ahora.ToString("dd/MM/yyyy HH:mm"));
        lineas.Add("");
        lineas.Add(FormatearFilaPdf("Placa", "Nombre", "Categoría", "Lugar", "Cajón", "Método", "Entrada", "Salida", "Estancia"));
        lineas.Add(new string('-', 124));

        foreach (RegistroAcceso registro in registros)
        {
            lineas.Add(FormatearFilaPdf(registro.Placa, registro.Nombre, registro.Categoria, registro.Estacionamiento, registro.Cajon, registro.Metodo,
                registro.FechaEntrada.ToString("dd/MM/yyyy HH:mm"), registro.FechaSalida?.ToString("dd/MM/yyyy HH:mm") ?? "Adentro",
                CalculadoraAforo.FormatearDuracion(CalculadoraAforo.CalcularTiempoEstancia(registro, ahora))));
        }

        File.WriteAllBytes(ruta, EscritorPdf.CrearDocumento(lineas));
    }

    private static string FormatearFilaPdf(string placa, string nombre, string categoria, string lugar, string cajon, string metodo,
        string entrada, string salida, string estancia)
    {
        return Columna(placa, 11) + Columna(nombre, 28) + Columna(categoria, 10) + Columna(lugar, 8) + Columna(cajon, 7) + Columna(metodo, 13) +
               Columna(entrada, 17) + Columna(salida, 17) + Columna(estancia, 12);
    }

    private static string Columna(string texto, int ancho)
    {
        if (texto.Length >= ancho)
        {
            texto = texto.Substring(0, ancho - 1);
        }
        return texto.PadRight(ancho);
    }

    /// <summary>Resumen del día para entregar el turno: movimientos, autos adentro y autos olvidados.</summary>
    public static string GenerarCierreDeTurno(DateTime ahora)
    {
        StringBuilder texto = new StringBuilder();
        texto.AppendLine("Cierre de turno del " + ahora.ToString("dd/MM/yyyy HH:mm"));
        texto.AppendLine();

        foreach (string estacionamiento in Estacionamientos.Todos)
        {
            List<RegistroAcceso> accesos = GestorArchivos.Accesos.Where(acceso => acceso.Estacionamiento == estacionamiento).ToList();
            int entradas = accesos.Count(acceso => acceso.FechaEntrada.Date == ahora.Date);
            int salidas = accesos.Count(acceso => acceso.FechaSalida != null && acceso.FechaSalida.Value.Date == ahora.Date);
            int adentro = accesos.Count(acceso => acceso.EstaAdentro);
            texto.AppendLine(estacionamiento + ": " + entradas + " entradas · " + salidas + " salidas · " + adentro + " adentro");
        }

        int olvidados = CalculadoraAforo.ObtenerVehiculosOlvidados(GestorArchivos.Accesos, ahora).Count;
        texto.AppendLine();
        texto.Append("Vehículos con más de " + CalculadoraAforo.HorasParaConsiderarOlvidado + " h adentro: " + olvidados);
        return texto.ToString();
    }
}

/// <summary>
/// Crea un PDF de solo texto con letra de ancho fijo, en hojas horizontales.
/// Un PDF es una lista de objetos numerados y al final una tabla (xref) con la posición de cada uno.
/// </summary>
public static class EscritorPdf
{
    private const int AnchoHoja = 842;
    private const int AltoHoja = 595;
    private const int Margen = 36;
    private const int TamanoLetra = 8;
    private const int AltoLinea = 11;
    private const int LineasPorHoja = (AltoHoja - 2 * Margen) / AltoLinea;

    public static byte[] CrearDocumento(List<string> lineas)
    {
        List<List<string>> hojas = lineas.Chunk(LineasPorHoja).Select(hoja => hoja.ToList()).ToList();
        if (hojas.Count == 0)
        {
            hojas.Add(new List<string>());
        }

        // Objetos: 1 = catálogo, 2 = lista de hojas, 3 = letra; después, una hoja y su contenido por cada página.
        List<string> objetos = new List<string>();
        objetos.Add("<< /Type /Catalog /Pages 2 0 R >>");

        IEnumerable<string> referenciasDeHojas = hojas.Select((hoja, indice) => (4 + indice * 2) + " 0 R");
        objetos.Add("<< /Type /Pages /Kids [" + string.Join(" ", referenciasDeHojas) + "] /Count " + hojas.Count + " >>");
        objetos.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Courier /Encoding /WinAnsiEncoding >>");

        for (int indice = 0; indice < hojas.Count; indice++)
        {
            int numeroContenido = 5 + indice * 2;
            objetos.Add("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 " + AnchoHoja + " " + AltoHoja + "] " +
                        "/Resources << /Font << /F1 3 0 R >> >> /Contents " + numeroContenido + " 0 R >>");

            string contenido = CrearContenidoDeHoja(hojas[indice]);
            objetos.Add("<< /Length " + Encoding.Latin1.GetByteCount(contenido) + " >>\nstream\n" + contenido + "\nendstream");
        }

        return EnsamblarArchivo(objetos);
    }

    private static string CrearContenidoDeHoja(List<string> lineas)
    {
        StringBuilder contenido = new StringBuilder();
        contenido.Append("BT\n/F1 " + TamanoLetra + " Tf\n" + AltoLinea + " TL\n" + Margen + " " + (AltoHoja - Margen) + " Td\n");
        foreach (string linea in lineas)
        {
            contenido.Append("(" + linea.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)") + ") Tj T*\n");
        }
        contenido.Append("ET");
        return contenido.ToString();
    }

    private static byte[] EnsamblarArchivo(List<string> objetos)
    {
        using MemoryStream archivo = new MemoryStream();
        List<long> posiciones = new List<long>();

        EscribirTexto(archivo, "%PDF-1.4\n");
        for (int indice = 0; indice < objetos.Count; indice++)
        {
            posiciones.Add(archivo.Position);
            EscribirTexto(archivo, (indice + 1) + " 0 obj\n" + objetos[indice] + "\nendobj\n");
        }

        long posicionTabla = archivo.Position;
        EscribirTexto(archivo, "xref\n0 " + (objetos.Count + 1) + "\n0000000000 65535 f \n");
        foreach (long posicion in posiciones)
        {
            EscribirTexto(archivo, posicion.ToString("0000000000") + " 00000 n \n");
        }
        EscribirTexto(archivo, "trailer\n<< /Size " + (objetos.Count + 1) + " /Root 1 0 R >>\nstartxref\n" + posicionTabla + "\n%%EOF\n");
        return archivo.ToArray();
    }

    private static void EscribirTexto(MemoryStream archivo, string texto)
    {
        byte[] bytes = Encoding.Latin1.GetBytes(texto);
        archivo.Write(bytes, 0, bytes.Length);
    }
}
