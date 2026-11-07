using Microsoft.UI.Xaml.Controls;

namespace PIA_ParkUANL;

/// <summary>Sin lógica: todo está enlazado a BitacoraVistaModelo.</summary>
public sealed partial class PestanaBitacora : UserControl
{
    public BitacoraVistaModelo Vista { get; }

    public PestanaBitacora(BitacoraVistaModelo vista)
    {
        ArgumentNullException.ThrowIfNull(vista);
        Vista = vista;
        InitializeComponent();
    }
}
