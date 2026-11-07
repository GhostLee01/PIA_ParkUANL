using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PIA_ParkUANL;

/// <summary>Solo lo visual: el cursor y el teclado. La lógica está en CambiarContrasenaVistaModelo.</summary>
public sealed partial class CambiarContrasenaPagina : Page
{
    public CambiarContrasenaVistaModelo Vista { get; }

    public CambiarContrasenaPagina(CambiarContrasenaVistaModelo vista)
    {
        ArgumentNullException.ThrowIfNull(vista);
        Vista = vista;

        InitializeComponent();

        Tema.AlPresionarEnter(cajaConfirmacion, Vista.GuardarCommand);
        Tema.AlPresionarEscape(this, Vista.SalirCommand);
        Loaded += (_, _) => cajaActual.Focus(FocusState.Programmatic);
    }
}
