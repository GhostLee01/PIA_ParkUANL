using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PIA_ParkUANL;

/// <summary>Solo arma las pestañas. La lógica está en PanelVistaModelo y en la vista-modelo de cada pestaña.</summary>
public sealed partial class DashboardPagina : Page
{
    public PanelVistaModelo Vista { get; }

    public DashboardPagina(PanelVistaModelo vista)
    {
        ArgumentNullException.ThrowIfNull(vista);
        Vista = vista;

        InitializeComponent();

        pestanaAccesos.Content = new PestanaAccesos(Vista.Accesos);
        pestanaEventos.Content = new PestanaEventos(Vista.Eventos);
        pestanaAsistente.Content = new PestanaAsistente(Vista.Asistente);
        pestanaUsuarios.Content = new PestanaUsuarios(Vista.Usuarios);
        pestanaReportes.Content = new PestanaReportes(Vista.Reportes);
        pestanaBitacora.Content = new PestanaBitacora(Vista.Bitacora);

        Loaded += AlMostrarse;
    }

    /// <summary>async void solo por ser un evento: por eso atrapa aquí sus propios errores de disco.</summary>
    private async void AlMostrarse(object remitente, RoutedEventArgs argumentos)
    {
        try
        {
            await Vista.AlMostrarse();
        }
        catch (Exception ex) when (GestorArchivos.EsErrorDeDisco(ex))
        {
            GestorArchivos.RegistrarError(ex);
        }
    }
}
