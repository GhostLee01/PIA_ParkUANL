using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PIA_ParkUANL_Lite;

/// <summary>Solo lo visual: el cursor y el teclado. La lógica está en MisVehiculosVistaModelo.</summary>
public sealed partial class MisVehiculosPagina : Page
{
    public MisVehiculosVistaModelo Vista { get; }

    public MisVehiculosPagina(MisVehiculosVistaModelo vista)
    {
        ArgumentNullException.ThrowIfNull(vista);
        Vista = vista;

        InitializeComponent();

        Tema.AlPresionarEnter(cajaPlaca, Vista.AgregarCommand);
        Tema.AlPresionarEscape(this, Vista.CerrarCommand);
        Vista.CampoConError += _ => cajaPlaca.Focus(FocusState.Programmatic);
        Loaded += (_, _) => cajaPlaca.Focus(FocusState.Programmatic);
    }
}
