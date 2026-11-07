using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PIA_ParkUANL;

// =====================================================================================
//  CASETAVISTAMODELO — PARTE 2 DE LA RÚBRICA: REGISTRO DE INFORMACIÓN
//  Caseta de acceso: elige estacionamiento, ve la ocupación con semáforo y registra
//  entradas (manual o con QR) y salidas. Cada movimiento se guarda en accesos.json.
//  Las reglas viven en ServicioCaseta.cs.
// =====================================================================================
public sealed partial class CasetaVistaModelo : VistaModeloBase
{
    private const string OpcionFacpya = "FACPYA (500 cajones)";
    private const string OpcionEstadio = "Estadio (1000 cajones)";

    private readonly IDialogos dialogos;
    private readonly INavegador navegador;
    private readonly Usuario operador;

    public IReadOnlyList<string> OpcionesDeEstacionamiento { get; } = new[] { OpcionFacpya, OpcionEstadio };
    public IReadOnlyList<string> CategoriasDisponibles { get; } = new[] { Categorias.Alumno, Categorias.Docente, Categorias.Visitante };

    public string Subtitulo
    {
        get { return "Operador: " + operador.NombreCompleto + "   ·   Estacionamientos gratuitos UANL"; }
    }

    public string TextoBotonCerrar
    {
        get { return operador.Rol == Roles.Operador ? "Cerrar sesión" : "Volver"; }
    }

    [ObservableProperty]
    private string opcionEstacionamiento = OpcionFacpya;

    [ObservableProperty]
    private ResumenAforo? resumenFacpya;

    [ObservableProperty]
    private ResumenAforo? resumenEstadio;

    [ObservableProperty]
    private string detalleFacpya = "";

    [ObservableProperty]
    private string detalleEstadio = "";

    [ObservableProperty]
    private string resumenSeleccionado = "";

    [ObservableProperty]
    private string categoria = Categorias.Alumno;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RegistrarEntradaManualCommand))]
    private string nombre = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AutocompletarCommand))]
    private string matricula = "";

    [ObservableProperty]
    private string correo = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AutocompletarCommand))]
    [NotifyCanExecuteChangedFor(nameof(RegistrarEntradaManualCommand))]
    private string placa = "";

    [ObservableProperty]
    private IReadOnlyList<string> eventosVigentes = new[] { OpcionesDeFiltro.NingunEvento };

    [ObservableProperty]
    private string eventoElegido = OpcionesDeFiltro.NingunEvento;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ValidarQrCommand))]
    private string codigoQr = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(BuscarSalidaCommand))]
    [NotifyCanExecuteChangedFor(nameof(RegistrarSalidaCommand))]
    private string busquedaSalida = "";

    [ObservableProperty]
    private string informacionSalida = "";

    [ObservableProperty]
    private IReadOnlyList<FilaTabla> movimientos = Array.Empty<FilaTabla>();

    public CasetaVistaModelo(IDialogos dialogos, INavegador navegador, Usuario operador)
    {
        ArgumentNullException.ThrowIfNull(dialogos);
        ArgumentNullException.ThrowIfNull(navegador);
        ArgumentNullException.ThrowIfNull(operador);
        this.dialogos = dialogos;
        this.navegador = navegador;
        this.operador = operador;
        ActualizarTodo();
    }

    partial void OnOpcionEstacionamientoChanged(string value)
    {
        ActualizarTodo();
    }

    partial void OnBusquedaSalidaChanged(string value)
    {
        InformacionSalida = "";
    }

    private string EstacionamientoElegido
    {
        get { return OpcionEstacionamiento == OpcionEstadio ? Estacionamientos.Estadio : Estacionamientos.Facpya; }
    }

    // ----------------------------- Actualización de datos en pantalla -----------------------------

    public void ActualizarTodo()
    {
        ActualizarAforo();
        CargarEventosVigentes();
        CargarMovimientos();
    }

    /// <summary>Lo llama el temporizador: la ocupación cambia sola porque los eventos y pases vencen con el tiempo.</summary>
    public void ActualizarAforo()
    {
        DateTime ahora = DateTime.Now;
        ResumenAforo facpya = CalculadoraAforo.CalcularResumen(Estacionamientos.Facpya, GestorArchivos.Accesos, GestorArchivos.Eventos, ahora);
        ResumenAforo estadio = CalculadoraAforo.CalcularResumen(Estacionamientos.Estadio, GestorArchivos.Accesos, GestorArchivos.Eventos, ahora);

        ResumenFacpya = facpya;
        ResumenEstadio = estadio;
        DetalleFacpya = facpya.VehiculosAdentro + " de " + facpya.CapacidadEfectiva + " · " + facpya.LugaresDisponibles + " disponibles";
        DetalleEstadio = estadio.VehiculosAdentro + " de " + estadio.CapacidadEfectiva + " · " + estadio.LugaresDisponibles + " disponibles";

        ResumenAforo seleccionado = EstacionamientoElegido == Estacionamientos.Facpya ? facpya : estadio;
        ResumenSeleccionado = "Capacidad efectiva = " + seleccionado.CapacidadTotal + " − " + seleccionado.LugaresReservados + " reservados = " + seleccionado.CapacidadEfectiva +
                              "\nDisponibles = " + seleccionado.CapacidadEfectiva + " − " + seleccionado.VehiculosAdentro + " = " + seleccionado.LugaresDisponibles +
                              "\nReservados ocupados por eventos: " + seleccionado.ReservadosOcupados + " de " + seleccionado.LugaresReservados;
    }

    /// <summary>Recarga los eventos conservando el que estaba elegido (si sigue vigente).</summary>
    private void CargarEventosVigentes()
    {
        string elegido = EventoElegido;
        DateTime ahora = DateTime.Now;

        List<string> eventos = new List<string> { OpcionesDeFiltro.NingunEvento };
        eventos.AddRange(GestorArchivos.Eventos.Where(evento => evento.EstaVigente(ahora)).Select(evento => evento.Codigo));

        EventosVigentes = eventos;
        EventoElegido = eventos.Contains(elegido) ? elegido : OpcionesDeFiltro.NingunEvento;
    }

    private void CargarMovimientos()
    {
        // Los registros con el movimiento (entrada o salida) más reciente.
        DateTime ahora = DateTime.Now;
        Movimientos = GestorArchivos.Accesos
            .OrderByDescending(acceso => acceso.FechaSalida ?? acceso.FechaEntrada)
            .Take(ServicioCaseta.MaximoMovimientosRecientes)
            .Select(acceso => new FilaTabla(acceso, acceso.Placa, acceso.Nombre, acceso.Categoria, acceso.Estacionamiento, acceso.Cajon, acceso.Metodo,
                acceso.FechaEntrada.ToString("dd/MM HH:mm"),
                acceso.FechaSalida == null ? OpcionesDeFiltro.Adentro : acceso.FechaSalida.Value.ToString("dd/MM HH:mm"),
                CalculadoraAforo.FormatearDuracion(CalculadoraAforo.CalcularTiempoEstancia(acceso, ahora))))
            .ToList();
    }

    // ----------------------------- Entrada manual -----------------------------

    private bool PuedeAutocompletar()
    {
        return string.IsNullOrWhiteSpace(Matricula) == false || string.IsNullOrWhiteSpace(Placa) == false;
    }

    [RelayCommand(CanExecute = nameof(PuedeAutocompletar))]
    private void Autocompletar()
    {
        DatosDePersona? persona = ServicioCaseta.BuscarDatosDePersona(Matricula.Trim(), Placa.Trim().ToUpperInvariant());
        if (persona == null)
        {
            MostrarError("No hay registros con esa matrícula o placa: es su primera visita, captura sus datos completos.");
            return;
        }

        if (CategoriasDisponibles.Contains(persona.Categoria))
        {
            Categoria = persona.Categoria;
        }
        Nombre = persona.Nombre;
        Matricula = persona.Matricula;
        Correo = persona.Correo;
        Placa = persona.Placa;
        MostrarExito(persona.Origen);
    }

    private bool PuedeRegistrarEntradaManual()
    {
        return string.IsNullOrWhiteSpace(Nombre) == false && string.IsNullOrWhiteSpace(Placa) == false;
    }

    [RelayCommand(CanExecute = nameof(PuedeRegistrarEntradaManual))]
    private void RegistrarEntradaManual()
    {
        DateTime ahora = DateTime.Now;
        DatosEntradaManual datos = new DatosEntradaManual(Categoria, Nombre, Matricula, Correo, Placa, EventoElegido);

        ResultadoValidacion resultado = ServicioCaseta.RegistrarEntradaManual(datos, EstacionamientoElegido, operador.NombreUsuario, ahora, out RegistroAcceso? registro);
        if (resultado.EsCorrecto == false || registro == null)
        {
            MostrarError(resultado);
            return;
        }

        // Entrada guardada: el formulario queda en blanco para la siguiente persona.
        MostrarEntradaRegistrada(registro, ahora);
        LimpiarCampos();
        ActualizarTodo();
    }

    private void MostrarEntradaRegistrada(RegistroAcceso registro, DateTime ahora)
    {
        MostrarExito("Entrada registrada: " + registro.Placa + " · " + registro.Categoria + " · " + registro.Estacionamiento + " · cajón " + registro.Cajon +
                     (registro.UsaCajonReservado ? " (reservado del evento)" : "") + " · " + ahora.ToString("HH:mm"));
    }

    private void LimpiarCampos()
    {
        Nombre = "";
        Matricula = "";
        Correo = "";
        Placa = "";
        Categoria = Categorias.Alumno;
        EventoElegido = OpcionesDeFiltro.NingunEvento;
    }

    [RelayCommand]
    private void LimpiarEntradaManual()
    {
        LimpiarCampos();
        LimpiarMensaje();
    }

    // ----------------------------- Entrada con QR -----------------------------

    private bool PuedeValidarQr()
    {
        return string.IsNullOrWhiteSpace(CodigoQr) == false;
    }

    [RelayCommand(CanExecute = nameof(PuedeValidarQr))]
    private async Task ValidarQr()
    {
        string codigo = CodigoQr;
        EntradaPorQr entrada = ServicioCaseta.PrepararEntradaQr(codigo, DateTime.Now);
        if (entrada.Registro == null)
        {
            MostrarError(entrada.Error);
            return;
        }

        // El QR identifica a la persona, no al auto: el operador debe ver que la placa coincida.
        bool confirmado = await dialogos.Confirmar("Confirmar vehículo",
            "¿El vehículo que está en la caseta tiene esta placa?\n\nPlaca: " + entrada.Registro.Placa + "\nConductor: " + entrada.Registro.Nombre);
        if (confirmado == false)
        {
            MostrarError("Entrada cancelada: el vehículo no coincide con el del código QR.");
            return;
        }

        DateTime ahora = DateTime.Now;
        string error = ServicioCaseta.RegistrarEntradaQr(entrada, codigo, EstacionamientoElegido, operador.NombreUsuario, ahora);
        if (error != "")
        {
            MostrarError(error);
            return;
        }

        MostrarEntradaRegistrada(entrada.Registro, ahora);
        CodigoQr = "";
        ActualizarTodo();
    }

    // ----------------------------- Salida -----------------------------

    private bool PuedeBuscarSalida()
    {
        return string.IsNullOrWhiteSpace(BusquedaSalida) == false;
    }

    [RelayCommand(CanExecute = nameof(PuedeBuscarSalida))]
    private void BuscarSalida()
    {
        BuscarVehiculoParaSalida();
    }

    /// <summary>Busca lo que está escrito AHORA en la caja, y regresa null si no hay nadie adentro con ese dato.</summary>
    private RegistroAcceso? BuscarVehiculoParaSalida()
    {
        // === [RÚBRICA 1] VALIDACIONES DE CAMPO ===
        InformacionSalida = "";
        string busqueda = BusquedaSalida.Trim().ToUpperInvariant();

        RegistroAcceso? registro = ServicioCaseta.BuscarVehiculoAdentro(busqueda);
        if (registro == null)
        {
            MostrarError("No hay ningún vehículo adentro con la placa o matrícula " + busqueda + ".");
            return null;
        }

        TimeSpan estancia = CalculadoraAforo.CalcularTiempoEstancia(registro, DateTime.Now);
        InformacionSalida = registro.Placa + " · " + registro.Nombre + " (" + registro.Categoria + ")\n" +
                            registro.Estacionamiento + " " + registro.Cajon + " · entró " +
                            registro.FechaEntrada.ToString("HH:mm") + " · lleva " + CalculadoraAforo.FormatearDuracion(estancia);
        LimpiarMensaje();
        return registro;
    }

    [RelayCommand(CanExecute = nameof(PuedeBuscarSalida))]
    private void RegistrarSalida()
    {
        // Siempre se busca lo que dice la caja en este momento: así nunca sale un vehículo de una búsqueda anterior.
        RegistroAcceso? registro = BuscarVehiculoParaSalida();
        if (registro == null)
        {
            return;
        }

        DateTime ahora = DateTime.Now;
        if (ServicioCaseta.RegistrarSalida(registro, ahora, operador.NombreUsuario) == false)
        {
            ActualizarTodo();
            return;
        }

        TimeSpan estancia = CalculadoraAforo.CalcularTiempoEstancia(registro, ahora);
        MostrarExito("Salida registrada: " + registro.Placa + " · estancia de " + CalculadoraAforo.FormatearDuracion(estancia) +
                     " (" + ahora.ToString("HH:mm") + " − " + registro.FechaEntrada.ToString("HH:mm") + ")");
        BusquedaSalida = "";
        ActualizarTodo();
    }

    [RelayCommand]
    private void CambiarContrasena()
    {
        navegador.MostrarCambioDeContrasena(operador, false);
    }

    [RelayCommand]
    private void Cerrar()
    {
        if (operador.Rol == Roles.Operador)
        {
            navegador.MostrarLogin();
        }
        else
        {
            navegador.Regresar();
        }
    }
}
