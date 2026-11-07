using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PIA_ParkUANL_Lite;

/// <summary>Solo lo visual: el cursor y el teclado. La lógica está en RegistroCuentaVistaModelo.</summary>
public sealed partial class RegistroCuentaPagina : Page
{
    public RegistroCuentaVistaModelo Vista { get; }

    public RegistroCuentaPagina(RegistroCuentaVistaModelo vista)
    {
        ArgumentNullException.ThrowIfNull(vista);
        Vista = vista;

        InitializeComponent();

        Tema.AlPresionarEnter(cajaConfirmacion, Vista.CrearCuentaCommand);
        Tema.AlPresionarEscape(this, Vista.CancelarCommand);
        Vista.CampoConError += EnfocarCampo;
        Loaded += (_, _) => cajaNombre.Focus(FocusState.Programmatic);
    }

    private void EnfocarCampo(CampoFormulario campo)
    {
        Control? control = campo switch
        {
            CampoFormulario.Categoria => comboCategoria,
            CampoFormulario.Nombre => cajaNombre,
            CampoFormulario.Correo => cajaCorreo,
            CampoFormulario.Matricula => cajaMatricula,
            CampoFormulario.Placa => cajaPlaca,
            CampoFormulario.Contrasena => cajaContrasena,
            CampoFormulario.Confirmacion => cajaConfirmacion,
            _ => null
        };
        control?.Focus(FocusState.Programmatic);
    }
}
