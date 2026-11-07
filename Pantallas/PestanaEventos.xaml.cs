using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PIA_ParkUANL;

/// <summary>Solo lo visual: llevar el cursor al campo con error. La lógica está en EventosVistaModelo.</summary>
public sealed partial class PestanaEventos : UserControl
{
    public EventosVistaModelo Vista { get; }

    public PestanaEventos(EventosVistaModelo vista)
    {
        ArgumentNullException.ThrowIfNull(vista);
        Vista = vista;

        InitializeComponent();

        Vista.CampoConError += EnfocarCampo;
    }

    private void EnfocarCampo(CampoFormulario campo)
    {
        Control? control = campo switch
        {
            CampoFormulario.Codigo => cajaCodigo,
            CampoFormulario.Titulo => cajaTitulo,
            CampoFormulario.Reservados => numeroReservados,
            CampoFormulario.MaximoVisitantes => numeroMaxVisitantes,
            CampoFormulario.VigenciaHoras => numeroVigencia,
            CampoFormulario.Nombre => cajaPaseNombre,
            CampoFormulario.Correo => cajaPaseCorreo,
            CampoFormulario.Placa => cajaPasePlaca,
            _ => null
        };
        control?.Focus(FocusState.Programmatic);
    }
}
