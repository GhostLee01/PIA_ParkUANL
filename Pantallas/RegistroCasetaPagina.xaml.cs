using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PIA_ParkUANL_Lite;

/// <summary>Solo lo visual: cursor, teclado y temporizador. La lógica está en CasetaVistaModelo.</summary>
public sealed partial class RegistroCasetaPagina : Page
{
    private const int SegundosEntreActualizaciones = 15;

    private readonly DispatcherTimer temporizador = new DispatcherTimer { Interval = TimeSpan.FromSeconds(SegundosEntreActualizaciones) };

    public CasetaVistaModelo Vista { get; }

    public RegistroCasetaPagina(CasetaVistaModelo vista)
    {
        ArgumentNullException.ThrowIfNull(vista);
        Vista = vista;

        InitializeComponent();

        // Enter dentro de Matrícula o Placa busca sus datos; en la caja de salida busca el vehículo.
        Tema.AlPresionarEnter(cajaMatricula, Vista.AutocompletarCommand);
        Tema.AlPresionarEnter(cajaPlaca, Vista.AutocompletarCommand);
        Tema.AlPresionarEnter(cajaBuscarSalida, Vista.BuscarSalidaCommand);
        Vista.CampoConError += EnfocarCampo;

        temporizador.Tick += (_, _) => Vista.ActualizarAforo();
        Loaded += (_, _) =>
        {
            Vista.ActualizarTodo();
            temporizador.Start();
        };
        Unloaded += (_, _) => temporizador.Stop();
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
            _ => null
        };
        control?.Focus(FocusState.Programmatic);
    }
}
