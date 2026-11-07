namespace PIA_ParkUANL;

// =====================================================================================
//  BLOQUEODEINSTANCIA.CS
//  Las listas viven en memoria: con dos copias de la app abiertas, la última en guardar
//  borraría lo de la otra sin avisar. Por eso la carpeta de datos se bloquea mientras la app
//  está abierta. Windows suelta el bloqueo aunque la app se cierre de golpe.
// =====================================================================================
public static class BloqueoDeInstancia
{
    private const string ArchivoDeBloqueo = "app.lock";

    // Se conserva abierto durante toda la vida del proceso: cerrarlo soltaría el bloqueo.
    private static FileStream? bloqueo;

    /// <summary>Regresa false si otra copia de la app ya está usando esta carpeta de datos.</summary>
    public static bool Tomar()
    {
        if (bloqueo != null)
        {
            return true;
        }

        Directory.CreateDirectory(GestorArchivos.CarpetaDatos);
        try
        {
            bloqueo = new FileStream(GestorArchivos.RutaDe(ArchivoDeBloqueo), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }
}
