using Microsoft.UI.Xaml.Controls;

namespace PIA_ParkUANL_Lite;

/// <summary>Sin lógica: todo está enlazado a UsuariosVistaModelo.</summary>
public sealed partial class PestanaUsuarios : UserControl
{
    public UsuariosVistaModelo Vista { get; }

    public PestanaUsuarios(UsuariosVistaModelo vista)
    {
        ArgumentNullException.ThrowIfNull(vista);
        Vista = vista;
        InitializeComponent();
    }
}
