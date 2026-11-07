using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PIA_ParkUANL;

// =====================================================================================
//  LOGINVISTAMODELO — PARTE 1 DE LA RÚBRICA: INICIO DE SESIÓN
//  Valida usuario (o correo) y contraseña contra usuarios.json (3 roles).
//  Las reglas viven en ServicioCuentas.cs.
// =====================================================================================
public sealed partial class LoginVistaModelo : VistaModeloBase
{
    private readonly INavegador navegador;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(IniciarSesionCommand))]
    private string usuario = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(IniciarSesionCommand))]
    private string contrasena = "";

    [ObservableProperty]
    private bool mostrarContrasena;

    public LoginVistaModelo(INavegador navegador)
    {
        ArgumentNullException.ThrowIfNull(navegador);
        this.navegador = navegador;
    }

    private bool PuedeIniciarSesion()
    {
        return string.IsNullOrWhiteSpace(Usuario) == false && Contrasena != "";
    }

    /// <summary>La verificación tarda: corre fuera de la ventana y el comando no acepta un segundo clic mientras tanto.</summary>
    [RelayCommand(CanExecute = nameof(PuedeIniciarSesion))]
    private async Task IniciarSesion()
    {
        // === [RÚBRICA 1] VALIDACIONES DE CAMPO ===
        string usuarioEscrito = Usuario;
        string contrasenaEscrita = Contrasena;
        Usuario? autenticado = null;

        string error = await Task.Run(() => ServicioCuentas.IniciarSesion(usuarioEscrito, contrasenaEscrita, DateTime.Now, out autenticado));
        if (error != "" || autenticado == null)
        {
            MostrarError(error, CampoFormulario.Contrasena);
            return;
        }

        Contrasena = "";
        navegador.EntrarConUsuario(autenticado);
    }

    [RelayCommand]
    private void CrearCuenta()
    {
        navegador.MostrarRegistroDeCuenta(correoRegistrado =>
        {
            // Cuenta creada: se deja el correo escrito para que solo falte la contraseña.
            Usuario = correoRegistrado;
            Contrasena = "";
            MostrarExito("Cuenta creada. Inicia sesión con tu correo y contraseña.");
        });
    }
}
