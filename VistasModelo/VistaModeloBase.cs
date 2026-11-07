using CommunityToolkit.Mvvm.ComponentModel;

namespace PIA_ParkUANL_Lite;

public enum TipoDeMensaje
{
    Neutro,
    Informativo,
    Exito,
    Error
}

/// <summary>Una tarjeta de resumen: el valor grande, su detalle y el semáforo.</summary>
public sealed record DatosDeTarjeta(string Valor, string Detalle, string Semaforo);

/// <summary>Imagen de un QR, en píxeles (azul, verde, rojo, alfa).</summary>
public sealed record ImagenQr(byte[] Pixeles, int Lado);

// =====================================================================================
//  VISTAMODELOBASE.CS — Lo que comparten todas las vista-modelos: el mensaje que se
//  muestra en la barra de la pantalla y la petición de llevar el cursor al campo con error.
// =====================================================================================
public abstract partial class VistaModeloBase : ObservableObject
{
    protected const int MilisegundosEsperaEscritura = 300;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayMensaje))]
    private string mensaje = "";

    [ObservableProperty]
    private TipoDeMensaje tipoMensaje = TipoDeMensaje.Informativo;

    private CancellationTokenSource? esperaDeEscritura;

    public bool HayMensaje
    {
        get { return Mensaje != ""; }
    }

    /// <summary>La pantalla lo atiende llevando el cursor a ese campo.</summary>
    public event Action<CampoFormulario>? CampoConError;

    protected void MostrarError(string texto, CampoFormulario campo = CampoFormulario.Ninguno)
    {
        TipoMensaje = TipoDeMensaje.Error;
        Mensaje = texto;
        if (campo != CampoFormulario.Ninguno)
        {
            CampoConError?.Invoke(campo);
        }
    }

    protected void MostrarError(ResultadoValidacion resultado)
    {
        ArgumentNullException.ThrowIfNull(resultado);
        MostrarError(resultado.Mensaje, resultado.Campo);
    }

    protected void MostrarExito(string texto)
    {
        TipoMensaje = TipoDeMensaje.Exito;
        Mensaje = texto;
    }

    protected void MostrarInformacion(string texto)
    {
        TipoMensaje = TipoDeMensaje.Informativo;
        Mensaje = texto;
    }

    protected void LimpiarMensaje()
    {
        Mensaje = "";
    }

    /// <summary>
    /// Espera a que se deje de escribir antes de ejecutar la acción, para no recargar con cada letra.
    /// Cada letra nueva cancela la espera anterior.
    /// </summary>
    protected async Task EsperarEscrituraYEjecutar(Action accion)
    {
        ArgumentNullException.ThrowIfNull(accion);

        esperaDeEscritura?.Cancel();
        esperaDeEscritura?.Dispose();
        CancellationTokenSource espera = new CancellationTokenSource();
        esperaDeEscritura = espera;

        try
        {
            await Task.Delay(MilisegundosEsperaEscritura, espera.Token);
            accion();
        }
        catch (OperationCanceledException)
        {
            // Llegó otra letra: la espera nueva se encarga.
        }
    }

    protected void CancelarEsperaDeEscritura()
    {
        esperaDeEscritura?.Cancel();
    }
}
