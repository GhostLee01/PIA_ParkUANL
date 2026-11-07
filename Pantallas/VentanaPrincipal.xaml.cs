using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Graphics;

namespace PIA_ParkUANL;

// =====================================================================================
//  VENTANAPRINCIPAL.XAML.CS — La única ventana de la aplicación y quien cambia de pantalla.
//  El diseño está en VentanaPrincipal.xaml: un contenedor donde se muestra cada pantalla.
//  1) Carga (o genera) los archivos .json.  2) Muestra el Login.
//  3) Abre la pantalla según el rol. Al cerrar sesión regresa al Login; al cerrar con la X, termina.
//  La sesión se cierra sola tras un tiempo sin actividad: la caseta puede quedar desatendida.
// =====================================================================================
public sealed partial class VentanaPrincipal : Window, INavegador
{
    private const int MinutosDeInactividad = 15;
    private const int SegundosEntreRevisiones = 30;

    private sealed record Pantalla(Page Pagina, string Titulo, int Ancho, int Alto);

    private readonly IServiceProvider servicios;
    private readonly Stack<Pantalla> pantallas = new Stack<Pantalla>();
    private readonly DispatcherTimer revisionDeInactividad = new DispatcherTimer { Interval = TimeSpan.FromSeconds(SegundosEntreRevisiones) };
    private DateTime ultimaActividad = DateTime.Now;

    public VentanaPrincipal(IServiceProvider servicios)
    {
        ArgumentNullException.ThrowIfNull(servicios);
        this.servicios = servicios;

        InitializeComponent();

        // handledEventsToo = true: también cuentan las teclas y clics que atiende un control.
        contenedor.AddHandler(UIElement.PointerMovedEvent, new PointerEventHandler((_, _) => ultimaActividad = DateTime.Now), true);
        contenedor.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler((_, _) => ultimaActividad = DateTime.Now), true);
        revisionDeInactividad.Tick += (_, _) => RevisarInactividad();
        Closed += (_, _) => revisionDeInactividad.Stop();
    }

    private async void AlAbrirVentana(object remitente, RoutedEventArgs argumentos)
    {
        contenedor.Loaded -= AlAbrirVentana;

        // async void solo se admite aquí por ser un evento, y por eso atrapa sus propios errores.
        try
        {
            IDialogos dialogos = servicios.GetRequiredService<IDialogos>();
            if (BloqueoDeInstancia.Tomar() == false)
            {
                await dialogos.Avisar("ParkUANL", "ParkUANL ya está abierto en este equipo. Usa esa ventana para no perder datos.");
                Close();
                return;
            }

            if (await CargarDatos(dialogos) == false)
            {
                Close();
                return;
            }

            MostrarLogin();
            revisionDeInactividad.Start();
        }
        catch (Exception ex) when (GestorArchivos.EsErrorDeDisco(ex) || ex is InvalidOperationException)
        {
            GestorArchivos.RegistrarError(ex);
            Close();
        }
    }

    /// <summary>
    /// Regresa false si no se pudo leer el disco: es mejor cerrar que arrancar sin datos
    /// y sobrescribir los archivos reales al primer guardado.
    /// </summary>
    private static async Task<bool> CargarDatos(IDialogos dialogos)
    {
        try
        {
            GestorArchivos.Inicializar();
        }
        catch (Exception ex) when (GestorArchivos.EsErrorDeDisco(ex))
        {
            await dialogos.Avisar("ParkUANL", "No se pudieron leer los datos de " + GestorArchivos.CarpetaDatos + ":\n" + ex.Message);
            return false;
        }

        foreach (string aviso in GestorArchivos.AvisosDeCarga.ToList())
        {
            await dialogos.Avisar("ParkUANL", aviso);
        }
        return true;
    }

    private void RevisarInactividad()
    {
        bool hayUnaSesion = pantallas.Count > 0 && pantallas.Peek().Pagina is not LoginPagina;
        if (hayUnaSesion && DateTime.Now - ultimaActividad > TimeSpan.FromMinutes(MinutosDeInactividad))
        {
            GestorArchivos.RegistrarEnBitacora("sistema", "Sesión cerrada por inactividad", MinutosDeInactividad + " min");
            MostrarLogin();
        }
    }

    // ----------------------------- Navegación -----------------------------

    private T Crear<T>(params object[] parametros)
    {
        return ActivatorUtilities.CreateInstance<T>(servicios, parametros);
    }

    public void MostrarLogin()
    {
        pantallas.Clear();
        Mostrar(new LoginPagina(Crear<LoginVistaModelo>()), "ParkUANL — Inicio de sesión", 480, 600);
    }

    /// <summary>Después de iniciar sesión: pide cambiar la contraseña si hace falta y abre la pantalla del rol.</summary>
    public void EntrarConUsuario(Usuario usuario)
    {
        ArgumentNullException.ThrowIfNull(usuario);

        pantallas.Clear();
        if (usuario.DebeCambiarContrasena)
        {
            MostrarCambioDeContrasena(usuario, true);
        }
        else if (usuario.Rol == Roles.Administrador)
        {
            MostrarPanel(usuario);
        }
        else if (usuario.Rol == Roles.Operador)
        {
            MostrarCaseta(usuario);
        }
        else
        {
            Mostrar(new PaseQrPagina(Crear<PaseQrVistaModelo>(usuario)), "ParkUANL — Mi pase QR", 800, 620);
        }
    }

    private void MostrarPanel(Usuario usuario)
    {
        PanelVistaModelo panel = Crear<PanelVistaModelo>(usuario,
            Crear<AccesosVistaModelo>(usuario), Crear<EventosVistaModelo>(usuario), Crear<AsistenteVistaModelo>(),
            Crear<UsuariosVistaModelo>(usuario), Crear<ReportesVistaModelo>(usuario), Crear<BitacoraVistaModelo>());
        Mostrar(new DashboardPagina(panel), "ParkUANL — Panel de control", 1280, 860);
    }

    public void MostrarRegistroDeCuenta(Action<string> alCrearCuenta)
    {
        Mostrar(new RegistroCuentaPagina(Crear<RegistroCuentaVistaModelo>(alCrearCuenta)), "ParkUANL — Crear cuenta", 480, 840);
    }

    public void MostrarCambioDeContrasena(Usuario usuario, bool esObligatorio)
    {
        Mostrar(new CambiarContrasenaPagina(Crear<CambiarContrasenaVistaModelo>(usuario, esObligatorio)), "ParkUANL — Cambiar contraseña", 460, 540);
    }

    public void MostrarMisVehiculos(Usuario usuario)
    {
        Mostrar(new MisVehiculosPagina(Crear<MisVehiculosVistaModelo>(usuario)), "ParkUANL — Mis vehículos", 440, 560);
    }

    public void MostrarCaseta(Usuario usuario)
    {
        Mostrar(new RegistroCasetaPagina(Crear<CasetaVistaModelo>(usuario)), "ParkUANL — Caseta de acceso", 1240, 860);
    }

    public void Regresar()
    {
        if (pantallas.Count <= 1)
        {
            MostrarLogin();
            return;
        }

        pantallas.Pop();
        Presentar(pantallas.Peek());
    }

    private void Mostrar(Page pagina, string titulo, int ancho, int alto)
    {
        Pantalla pantalla = new Pantalla(pagina, titulo, ancho, alto);
        pantallas.Push(pantalla);
        Presentar(pantalla);
    }

    private void Presentar(Pantalla pantalla)
    {
        contenedor.Children.Clear();
        contenedor.Children.Add(pantalla.Pagina);
        Title = pantalla.Titulo;
        AjustarAPantalla(pantalla.Ancho, pantalla.Alto);
    }

    /// <summary>Si la ventana es más grande que la pantalla (por ejemplo con escala 150 %), se ajusta y se centra.</summary>
    private void AjustarAPantalla(int ancho, int alto)
    {
        double escala = contenedor.XamlRoot?.RasterizationScale ?? 1.0;
        RectInt32 area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        int anchoReal = Math.Min((int)(ancho * escala), area.Width);
        int altoReal = Math.Min((int)(alto * escala), area.Height);
        AppWindow.MoveAndResize(new RectInt32(area.X + (area.Width - anchoReal) / 2, area.Y + (area.Height - altoReal) / 2, anchoReal, altoReal));
    }
}
