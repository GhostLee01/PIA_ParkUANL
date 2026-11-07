using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PIA_ParkUANL;

/// <summary>Solo el ciclo de vida: el temporizador corre mientras la pantalla está visible. La lógica está en PaseQrVistaModelo.</summary>
public sealed partial class PaseQrPagina : Page
{
    private readonly DispatcherTimer temporizador = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };

    public PaseQrVistaModelo Vista { get; }

    public PaseQrPagina(PaseQrVistaModelo vista)
    {
        ArgumentNullException.ThrowIfNull(vista);
        Vista = vista;

        InitializeComponent();

        temporizador.Tick += (_, _) => Vista.AlPasarUnSegundo();
        Loaded += (_, _) =>
        {
            Vista.AlMostrarse();
            temporizador.Start();
        };
        Unloaded += (_, _) => temporizador.Stop();
    }
}
