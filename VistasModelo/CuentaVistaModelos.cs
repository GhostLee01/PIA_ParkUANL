using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PIA_ParkUANL_Lite;

// =====================================================================================
//  CREAR CUENTA: solo Alumnos y Docentes con correo universitario @uanl.edu.mx.
//  Toda cuenta nueva nace con el rol "Comunidad". Solo el Administrador puede
//  convertirla en Operador o Administrador (pestaña "Usuarios" del panel de control).
// =====================================================================================
public sealed partial class RegistroCuentaVistaModelo : VistaModeloBase
{
    private readonly INavegador navegador;
    private readonly Action<string> alCrearCuenta;

    public IReadOnlyList<string> CategoriasDisponibles { get; } = new[] { Categorias.Alumno, Categorias.Docente };

    [ObservableProperty]
    private string categoria = Categorias.Alumno;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CrearCuentaCommand))]
    private string nombre = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CrearCuentaCommand))]
    private string correo = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CrearCuentaCommand))]
    private string matricula = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CrearCuentaCommand))]
    private string placa = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CrearCuentaCommand))]
    private string contrasena = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CrearCuentaCommand))]
    private string confirmacion = "";

    public RegistroCuentaVistaModelo(INavegador navegador, Action<string> alCrearCuenta)
    {
        ArgumentNullException.ThrowIfNull(navegador);
        ArgumentNullException.ThrowIfNull(alCrearCuenta);
        this.navegador = navegador;
        this.alCrearCuenta = alCrearCuenta;
    }

    private bool PuedeCrearCuenta()
    {
        return new[] { Nombre, Correo, Matricula, Placa, Contrasena, Confirmacion }.All(campo => string.IsNullOrWhiteSpace(campo) == false);
    }

    [RelayCommand(CanExecute = nameof(PuedeCrearCuenta))]
    private async Task CrearCuenta()
    {
        DatosDeRegistro datos = new DatosDeRegistro(Categoria, Nombre, Correo, Matricula, Placa, Contrasena, Confirmacion);
        string correoRegistrado = "";

        ResultadoValidacion resultado = await Task.Run(() => ServicioCuentas.RegistrarCuenta(datos, out correoRegistrado));
        if (resultado.EsCorrecto == false)
        {
            MostrarError(resultado);
            return;
        }

        alCrearCuenta(correoRegistrado);
        navegador.Regresar();
    }

    [RelayCommand]
    private void Cancelar()
    {
        navegador.Regresar();
    }
}

// =====================================================================================
//  CAMBIAR CONTRASEÑA: la abre el usuario desde su pantalla, o se abre sola (esObligatorio)
//  al entrar con una contraseña temporal o de fábrica.
// =====================================================================================
public sealed partial class CambiarContrasenaVistaModelo : VistaModeloBase
{
    private readonly IDialogos dialogos;
    private readonly INavegador navegador;
    private readonly Usuario usuario;
    private readonly bool esObligatorio;

    public string Subtitulo
    {
        get { return esObligatorio ? "Debes cambiarla para continuar" : usuario.NombreCompleto; }
    }

    public string TextoBotonSalir
    {
        get { return esObligatorio ? "Salir" : "Cancelar"; }
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GuardarCommand))]
    private string contrasenaActual = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GuardarCommand))]
    private string contrasenaNueva = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GuardarCommand))]
    private string confirmacion = "";

    public CambiarContrasenaVistaModelo(IDialogos dialogos, INavegador navegador, Usuario usuario, bool esObligatorio)
    {
        ArgumentNullException.ThrowIfNull(dialogos);
        ArgumentNullException.ThrowIfNull(navegador);
        ArgumentNullException.ThrowIfNull(usuario);
        this.dialogos = dialogos;
        this.navegador = navegador;
        this.usuario = usuario;
        this.esObligatorio = esObligatorio;
    }

    private bool PuedeGuardar()
    {
        return ContrasenaActual != "" && ContrasenaNueva != "" && Confirmacion != "";
    }

    [RelayCommand(CanExecute = nameof(PuedeGuardar))]
    private async Task Guardar()
    {
        string actual = ContrasenaActual;
        string nueva = ContrasenaNueva;
        string confirmada = Confirmacion;

        string error = await Task.Run(() => ServicioCuentas.CambiarContrasena(usuario, actual, nueva, confirmada));
        if (error != "")
        {
            MostrarError(error);
            return;
        }

        await dialogos.Avisar("ParkUANL", "Tu contraseña se cambió correctamente.");
        if (esObligatorio)
        {
            navegador.EntrarConUsuario(usuario);
        }
        else
        {
            navegador.Regresar();
        }
    }

    [RelayCommand]
    private void Salir()
    {
        if (esObligatorio)
        {
            navegador.MostrarLogin();
        }
        else
        {
            navegador.Regresar();
        }
    }
}

// =====================================================================================
//  MIS VEHÍCULOS: un alumno o docente administra las placas de su cuenta.
// =====================================================================================
public sealed partial class MisVehiculosVistaModelo : VistaModeloBase
{
    private readonly INavegador navegador;
    private readonly Usuario usuario;

    public string Subtitulo
    {
        get { return "Hasta " + ServicioCuentas.MaximoVehiculosPorCuenta + " por cuenta"; }
    }

    public ObservableCollection<string> Placas { get; } = new ObservableCollection<string>();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(QuitarCommand))]
    private string? placaElegida;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AgregarCommand))]
    private string placaNueva = "";

    public MisVehiculosVistaModelo(INavegador navegador, Usuario usuario)
    {
        ArgumentNullException.ThrowIfNull(navegador);
        ArgumentNullException.ThrowIfNull(usuario);
        this.navegador = navegador;
        this.usuario = usuario;
        CargarPlacas();
    }

    private void CargarPlacas()
    {
        Placas.Clear();
        foreach (string placa in usuario.ObtenerPlacas())
        {
            Placas.Add(placa);
        }
    }

    private bool PuedeAgregar()
    {
        return string.IsNullOrWhiteSpace(PlacaNueva) == false;
    }

    [RelayCommand(CanExecute = nameof(PuedeAgregar))]
    private void Agregar()
    {
        string error = ServicioCuentas.AgregarVehiculo(usuario, PlacaNueva.Trim().ToUpperInvariant());
        if (error != "")
        {
            MostrarError(error, CampoFormulario.Placa);
            return;
        }

        LimpiarMensaje();
        PlacaNueva = "";
        CargarPlacas();
    }

    private bool PuedeQuitar()
    {
        return PlacaElegida != null;
    }

    [RelayCommand(CanExecute = nameof(PuedeQuitar))]
    private void Quitar()
    {
        string? placa = PlacaElegida;
        if (placa == null)
        {
            return;
        }

        string error = ServicioCuentas.QuitarVehiculo(usuario, placa);
        if (error != "")
        {
            MostrarError(error);
            return;
        }

        LimpiarMensaje();
        CargarPlacas();
    }

    [RelayCommand]
    private void Cerrar()
    {
        navegador.Regresar();
    }
}

// =====================================================================================
//  PASE QR TEMPORAL (Comunidad UANL): se regenera cada 30 segundos.
//  En la caseta, el operador pega el texto del QR para registrar la entrada.
//  También muestra cuántos lugares hay antes de salir rumbo al campus.
// =====================================================================================
public sealed partial class PaseQrVistaModelo : VistaModeloBase
{
    private const int SegundosEntreActualizaciones = 15;
    private const int SegundosParaAlerta = 10;
    private const int PixelesPorModulo = 8;

    private readonly IDialogos dialogos;
    private readonly INavegador navegador;
    private readonly IPortapapeles portapapeles;
    private readonly Usuario usuario;
    private int segundosDesdeActualizacion;

    public string Subtitulo
    {
        get { return "Comunidad UANL — " + usuario.Categoria; }
    }

    public string DatosDeLaPersona
    {
        get { return usuario.NombreCompleto + "  ·  Matrícula: " + usuario.Matricula; }
    }

    public double SegundosDeVigencia
    {
        get { return TokenUanl.SegundosVigencia; }
    }

    [ObservableProperty]
    private IReadOnlyList<string> vehiculos = Array.Empty<string>();

    [ObservableProperty]
    private string vehiculoElegido = "";

    [ObservableProperty]
    private string token = "";

    [ObservableProperty]
    private ImagenQr? imagen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TextoCuentaRegresiva))]
    [NotifyPropertyChangedFor(nameof(EstaPorVencer))]
    private int segundosRestantes;

    [ObservableProperty]
    private string textoFacpya = "";

    [ObservableProperty]
    private string semaforoFacpya = Semaforos.Verde;

    [ObservableProperty]
    private string textoEstadio = "";

    [ObservableProperty]
    private string semaforoEstadio = Semaforos.Verde;

    [ObservableProperty]
    private string recomendacion = "";

    public string TextoCuentaRegresiva
    {
        get { return "El código expira en " + SegundosRestantes + " s"; }
    }

    public bool EstaPorVencer
    {
        get { return SegundosRestantes <= SegundosParaAlerta; }
    }

    public PaseQrVistaModelo(IDialogos dialogos, INavegador navegador, IPortapapeles portapapeles, Usuario usuario)
    {
        ArgumentNullException.ThrowIfNull(dialogos);
        ArgumentNullException.ThrowIfNull(navegador);
        ArgumentNullException.ThrowIfNull(portapapeles);
        ArgumentNullException.ThrowIfNull(usuario);
        this.dialogos = dialogos;
        this.navegador = navegador;
        this.portapapeles = portapapeles;
        this.usuario = usuario;
    }

    /// <summary>Se llama cada vez que la pantalla aparece: al abrirla o al regresar de Mis vehículos.</summary>
    public void AlMostrarse()
    {
        string elegido = VehiculoElegido;
        Vehiculos = usuario.ObtenerPlacas();
        VehiculoElegido = Vehiculos.Contains(elegido) ? elegido : Vehiculos.FirstOrDefault() ?? "";
        GenerarQr();
        ActualizarDisponibilidad();
    }

    partial void OnVehiculoElegidoChanged(string value)
    {
        if (value != "")
        {
            GenerarQr();
        }
    }

    private void GenerarQr()
    {
        if (VehiculoElegido == "")
        {
            return;
        }

        Token = TokenUanl.Generar(usuario, VehiculoElegido, DateTime.Now);
        byte[] pixeles = GeneradorQR.CrearPixeles(Token, PixelesPorModulo, out int lado);
        Imagen = new ImagenQr(pixeles, lado);
        SegundosRestantes = TokenUanl.SegundosVigencia;
    }

    [RelayCommand]
    private void NuevoQr()
    {
        GenerarQr();
    }

    /// <summary>Lo llama el temporizador de la pantalla mientras está visible.</summary>
    public void AlPasarUnSegundo()
    {
        SegundosRestantes = SegundosRestantes - 1;
        if (SegundosRestantes <= 0)
        {
            GenerarQr();
        }

        segundosDesdeActualizacion = segundosDesdeActualizacion + 1;
        if (segundosDesdeActualizacion >= SegundosEntreActualizaciones)
        {
            segundosDesdeActualizacion = 0;
            ActualizarDisponibilidad();
        }
    }

    private void ActualizarDisponibilidad()
    {
        DateTime ahora = DateTime.Now;
        ResumenAforo facpya = CalculadoraAforo.CalcularResumen(Estacionamientos.Facpya, GestorArchivos.Accesos, GestorArchivos.Eventos, ahora);
        ResumenAforo estadio = CalculadoraAforo.CalcularResumen(Estacionamientos.Estadio, GestorArchivos.Accesos, GestorArchivos.Eventos, ahora);

        TextoFacpya = DescribirDisponibilidad(facpya);
        SemaforoFacpya = facpya.Semaforo;
        TextoEstadio = DescribirDisponibilidad(estadio);
        SemaforoEstadio = estadio.Semaforo;
        Recomendacion = CalculadoraAforo.RecomendarEstacionamiento(facpya, estadio);
    }

    private static string DescribirDisponibilidad(ResumenAforo resumen)
    {
        return "● " + resumen.Estacionamiento + ": " + resumen.LugaresDisponibles + " lugares (" + resumen.PorcentajeOcupacion + " % ocupado)";
    }

    [RelayCommand]
    private async Task CopiarToken()
    {
        if (Token != "" && portapapeles.Copiar(Token) == false)
        {
            await dialogos.Avisar("ParkUANL", "El portapapeles está ocupado por otro programa. Intenta de nuevo.");
        }
    }

    [RelayCommand]
    private void AbrirMisVehiculos()
    {
        navegador.MostrarMisVehiculos(usuario);
    }

    [RelayCommand]
    private void CambiarContrasena()
    {
        navegador.MostrarCambioDeContrasena(usuario, false);
    }

    [RelayCommand]
    private void CerrarSesion()
    {
        navegador.MostrarLogin();
    }
}
