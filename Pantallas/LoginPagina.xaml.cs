using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PIA_ParkUANL;

/// <summary>Solo lo visual: el cursor y el teclado. La lógica está en LoginVistaModelo.</summary>
public sealed partial class LoginPagina : Page
{
    public LoginVistaModelo Vista { get; }

    public LoginPagina(LoginVistaModelo vista)
    {
        ArgumentNullException.ThrowIfNull(vista);
        Vista = vista;

        InitializeComponent();

        Tema.AlPresionarEnter(cajaUsuario, Vista.IniciarSesionCommand);
        Tema.AlPresionarEnter(cajaContrasena, Vista.IniciarSesionCommand);
        Vista.CampoConError += _ => cajaContrasena.Focus(FocusState.Programmatic);
        Loaded += (_, _) => EnfocarCampoInicial();
    }

    private void EnfocarCampoInicial()
    {
        Control campo = Vista.Usuario == "" ? cajaUsuario : cajaContrasena;
        campo.Focus(FocusState.Programmatic);
    }
}
