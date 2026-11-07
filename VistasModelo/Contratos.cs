namespace PIA_ParkUANL;

// =====================================================================================
//  CONTRATOS.CS
//  Lo que una vista-modelo necesita de la interfaz, sin conocer WinUI. Por eso las
//  vista-modelos se pueden probar con xUnit usando versiones simuladas de estas interfaces.
// =====================================================================================

public enum OpcionCierreDeTurno
{
    RegistrarSalidas,
    CerrarSinRegistrar,
    Cancelar
}

public interface IDialogos
{
    Task Avisar(string titulo, string mensaje);

    /// <summary>Pregunta Sí o No. Regresa true solo si se eligió Sí.</summary>
    Task<bool> Confirmar(string titulo, string pregunta);

    /// <summary>Regresa lo que escribió el usuario, o null si canceló.</summary>
    Task<string?> PedirTexto(string titulo, string instruccion, string textoInicial = "", bool enMayusculas = false);

    Task<OpcionCierreDeTurno> PreguntarCierreDeTurno(string resumen);
}

public interface INavegador
{
    void MostrarLogin();
    void EntrarConUsuario(Usuario usuario);

    /// <param name="alCrearCuenta">Recibe el correo de la cuenta recién creada.</param>
    void MostrarRegistroDeCuenta(Action<string> alCrearCuenta);
    void MostrarCambioDeContrasena(Usuario usuario, bool esObligatorio);
    void MostrarMisVehiculos(Usuario usuario);
    void MostrarCaseta(Usuario usuario);

    /// <summary>Regresa a la pantalla anterior, tal como estaba.</summary>
    void Regresar();
}

public interface IPortapapeles
{
    /// <summary>Regresa false si otro programa tiene ocupado el portapapeles.</summary>
    bool Copiar(string texto);
}

public interface ISelectorDeArchivos
{
    /// <summary>Pregunta dónde guardar. Regresa la ruta, o null si el usuario canceló.</summary>
    Task<string?> ElegirDestino(string nombreSugerido, string descripcion, string extension);
}

/// <summary>Cada pestaña del panel: se recarga cuando se muestra y avisa cuando cambió datos.</summary>
public interface IPestanaDelPanel
{
    event Action? DatosCambiados;
    void Cargar();
}
