using Microsoft.UI.Xaml.Controls;

namespace PIA_ParkUANL_Lite;

/// <summary>Sin lógica: todo está enlazado a AccesosVistaModelo.</summary>
public sealed partial class PestanaAccesos : UserControl
{
    public AccesosVistaModelo Vista { get; }

    public PestanaAccesos(AccesosVistaModelo vista)
    {
        ArgumentNullException.ThrowIfNull(vista);
        Vista = vista;
        InitializeComponent();
    }
}
