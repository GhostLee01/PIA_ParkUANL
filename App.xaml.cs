using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

namespace PIA_ParkUANL_Lite;

// =====================================================================================
//  APP.XAML.CS — Punto de entrada y raíz de composición: aquí se decide qué clase
//  implementa cada contrato. Un error inesperado se anota en errores.log y se avisa,
//  en lugar de cerrar la app de golpe.
// =====================================================================================
public partial class App : Application
{
    private ServiceProvider? servicios;
    private VentanaPrincipal? ventana;

    public App()
    {
        InitializeComponent();

        UnhandledException += (_, argumentos) =>
        {
            argumentos.Handled = true;
            MostrarErrorInesperado(argumentos.Exception);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, argumentos) =>
        {
            if (argumentos.ExceptionObject is Exception error)
            {
                GestorArchivos.RegistrarError(error);
            }
        };
        TaskScheduler.UnobservedTaskException += (_, argumentos) =>
        {
            GestorArchivos.RegistrarError(argumentos.Exception);
            argumentos.SetObserved();
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs argumentos)
    {
        ServiceCollection coleccion = new ServiceCollection();
        coleccion.AddSingleton<VentanaPrincipal>();
        coleccion.AddSingleton<INavegador>(proveedor => proveedor.GetRequiredService<VentanaPrincipal>());
        coleccion.AddSingleton<IDialogos, Dialogos>();
        coleccion.AddSingleton<IPortapapeles, Portapapeles>();
        coleccion.AddSingleton<ISelectorDeArchivos, SelectorDeArchivos>();

        servicios = coleccion.BuildServiceProvider();
        ventana = servicios.GetRequiredService<VentanaPrincipal>();
        IDialogos dialogos = servicios.GetRequiredService<IDialogos>();

        // Un guardado puede fallar fuera del hilo de la ventana: el aviso siempre se muestra desde ella.
        GestorArchivos.MostrarErrorDeGuardado = mensaje => ventana.DispatcherQueue.TryEnqueue(() => _ = AvisarSinFallar(dialogos, mensaje));

        ventana.Activate();
    }

    private void MostrarErrorInesperado(Exception error)
    {
        GestorArchivos.RegistrarError(error);

        VentanaPrincipal? ventanaAbierta = ventana;
        ServiceProvider? proveedor = servicios;
        if (ventanaAbierta == null || proveedor == null)
        {
            return;
        }

        IDialogos dialogos = proveedor.GetRequiredService<IDialogos>();
        ventanaAbierta.DispatcherQueue.TryEnqueue(() =>
            _ = AvisarSinFallar(dialogos, "Ocurrió un error inesperado: " + error.Message + "\nEl detalle se guardó en errores.log."));
    }

    /// <summary>Si la ventana ya se cerró no hay dónde mostrar el aviso: se anota y la app sigue su curso.</summary>
    private static async Task AvisarSinFallar(IDialogos dialogos, string mensaje)
    {
        try
        {
            await dialogos.Avisar("ParkUANL", mensaje);
        }
        catch (InvalidOperationException ex)
        {
            GestorArchivos.RegistrarError(ex);
        }
    }
}
