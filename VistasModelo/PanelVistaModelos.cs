using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PIA_ParkUANL_Lite;

// =====================================================================================
//  PANELVISTAMODELO — PARTE 3 DE LA RÚBRICA: VISUALIZACIÓN POR CATEGORÍA + EVENTOS + IA
//  Panel del Administrador. Cada pestaña tiene su propia vista-modelo.
// =====================================================================================
public sealed partial class PanelVistaModelo : VistaModeloBase
{
    private readonly INavegador navegador;
    private readonly Usuario usuarioSesion;
    private readonly IPestanaDelPanel[] pestanas;
    private bool modeloIniciado;

    public AccesosVistaModelo Accesos { get; }
    public EventosVistaModelo Eventos { get; }
    public AsistenteVistaModelo Asistente { get; }
    public UsuariosVistaModelo Usuarios { get; }
    public ReportesVistaModelo Reportes { get; }
    public BitacoraVistaModelo Bitacora { get; }

    public string Subtitulo
    {
        get { return "Administrador: " + usuarioSesion.NombreCompleto; }
    }

    [ObservableProperty]
    private int indicePestana;

    [ObservableProperty]
    private string alerta = "";

    public PanelVistaModelo(INavegador navegador, Usuario usuarioSesion, AccesosVistaModelo accesos, EventosVistaModelo eventos,
        AsistenteVistaModelo asistente, UsuariosVistaModelo usuarios, ReportesVistaModelo reportes, BitacoraVistaModelo bitacora)
    {
        ArgumentNullException.ThrowIfNull(navegador);
        ArgumentNullException.ThrowIfNull(usuarioSesion);
        this.navegador = navegador;
        this.usuarioSesion = usuarioSesion;
        Accesos = accesos;
        Eventos = eventos;
        Asistente = asistente;
        Usuarios = usuarios;
        Reportes = reportes;
        Bitacora = bitacora;

        // Mismo orden que las pestañas de la pantalla.
        pestanas = new IPestanaDelPanel[] { accesos, eventos, asistente, usuarios, reportes, bitacora };
        foreach (IPestanaDelPanel pestana in pestanas)
        {
            pestana.DatosCambiados += ActualizarAlerta;
        }
    }

    partial void OnIndicePestanaChanged(int value)
    {
        Actualizar();
    }

    /// <summary>Solo se recarga la pestaña que se está viendo: recargar todas haría lento cada cambio de pestaña.</summary>
    [RelayCommand]
    private void Actualizar()
    {
        ActualizarAlerta();
        if (IndicePestana >= 0 && IndicePestana < pestanas.Length)
        {
            pestanas[IndicePestana].Cargar();
        }
    }

    private void ActualizarAlerta()
    {
        int olvidados = CalculadoraAforo.ObtenerVehiculosOlvidados(GestorArchivos.Accesos, DateTime.Now).Count;
        Alerta = olvidados == 0
            ? ""
            : "⚠ " + olvidados + " vehículo(s) con más de " + CalculadoraAforo.HorasParaConsiderarOlvidado + " h adentro (ver \"Reportes y alertas\")";
    }

    /// <summary>Se llama cada vez que la pantalla aparece: al abrirla o al regresar de la caseta.</summary>
    public async Task AlMostrarse()
    {
        Actualizar();
        if (modeloIniciado == false)
        {
            modeloIniciado = true;
            await Asistente.CargarModeloAsync();
        }
    }

    [RelayCommand]
    private void AbrirCaseta()
    {
        navegador.MostrarCaseta(usuarioSesion);
    }

    [RelayCommand]
    private void CambiarContrasena()
    {
        navegador.MostrarCambioDeContrasena(usuarioSesion, false);
    }

    [RelayCommand]
    private void CerrarSesion()
    {
        navegador.MostrarLogin();
    }
}

// =====================================================================================
//  PESTAÑA: BITÁCORA (quién hizo cada corrección y cambio, cuándo y por qué)
// =====================================================================================
public sealed partial class BitacoraVistaModelo : VistaModeloBase, IPestanaDelPanel
{
    [ObservableProperty]
    private IReadOnlyList<FilaTabla> filas = Array.Empty<FilaTabla>();

    public event Action? DatosCambiados
    {
        add { }
        remove { }
    }

    public void Cargar()
    {
        Filas = GestorArchivos.Bitacora
            .OrderByDescending(linea => linea.Id)
            .Select(registro => new FilaTabla(registro, registro.Fecha.ToString("dd/MM/yyyy HH:mm"), registro.Usuario, registro.Accion, registro.Detalle, registro.Motivo))
            .ToList();
    }
}

/// <summary>Un mensaje del chat con el asistente.</summary>
public sealed record MensajeDeChat(string Autor, string Texto, TipoDeMensaje Tipo);

// =====================================================================================
//  PESTAÑA: ASISTENTE IA
// =====================================================================================
public sealed partial class AsistenteVistaModelo : VistaModeloBase, IPestanaDelPanel
{
    public const int LongitudMaximaPregunta = 300;

    private const string NombreDelAsistente = "ParkUANL IA";

    private readonly AsistenteIA asistente = new AsistenteIA();

    public IReadOnlyList<string> Sugerencias { get; } = new[]
    {
        "¿Cuál es la placa más reciente en ingresar?",
        "¿Cuántas entradas y salidas hubo hoy?",
        "¿Cuánto tiempo lleva la placa RMS-221-B?",
        "Reporte de entradas por hora",
        "¿Cuántos hay por categoría en FACPYA?",
        "¿Hay vehículos olvidados?",
        "¿Cómo estará FACPYA mañana a las 10?",
        "¿Cuántos lugares disponibles hay?"
    };

    public ObservableCollection<MensajeDeChat> Chat { get; } = new ObservableCollection<MensajeDeChat>();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EnviarCommand))]
    private string pregunta = "";

    [ObservableProperty]
    private string metricas = "Cargando modelo…";

    [ObservableProperty]
    private bool metricasConError;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ReentrenarCommand))]
    private bool modeloListo;

    [ObservableProperty]
    private string textoBotonReentrenar = "Reentrenar modelo";

    public event Action? DatosCambiados
    {
        add { }
        remove { }
    }

    /// <summary>La pantalla lo atiende llevando el cursor a la caja de pregunta.</summary>
    public event Action? PreguntaEnfocada;

    public AsistenteVistaModelo()
    {
        Chat.Add(new MensajeDeChat(NombreDelAsistente, "¡Hola! Pregúntame sobre placas, entradas por hora, categorías, estancias, eventos o pronósticos.", TipoDeMensaje.Informativo));
    }

    public void Cargar()
    {
        PreguntaEnfocada?.Invoke();
    }

    /// <summary>Al abrir el panel: carga o entrena el modelo en segundo plano para no congelar la ventana.</summary>
    public async Task CargarModeloAsync()
    {
        await Task.Run(ClasificadorIntencion.Inicializar);
        MostrarEstadoDelModelo();
        ModeloListo = true;
    }

    private bool PuedeReentrenar()
    {
        return ModeloListo;
    }

    [RelayCommand(CanExecute = nameof(PuedeReentrenar))]
    private async Task Reentrenar(CancellationToken cancelacion)
    {
        TextoBotonReentrenar = "Entrenando…";
        Metricas = "Entrenando el modelo con el archivo de preguntas…";
        try
        {
            await Task.Run(ClasificadorIntencion.Reentrenar, cancelacion);
            MostrarEstadoDelModelo();

            if (ClasificadorIntencion.Error == "")
            {
                Chat.Add(new MensajeDeChat(NombreDelAsistente, "Modelo reentrenado.\n" + ClasificadorIntencion.Metricas, TipoDeMensaje.Exito));
            }
            else
            {
                Chat.Add(new MensajeDeChat(NombreDelAsistente, "No se pudo reentrenar el modelo: " + ClasificadorIntencion.Error +
                                                               "\nMientras tanto respondo con las reglas de palabras clave.", TipoDeMensaje.Error));
            }
        }
        catch (OperationCanceledException)
        {
            MostrarEstadoDelModelo();
        }
        finally
        {
            TextoBotonReentrenar = "Reentrenar modelo";
        }
    }

    private void MostrarEstadoDelModelo()
    {
        Metricas = ClasificadorIntencion.Metricas;
        MetricasConError = ClasificadorIntencion.Error != "";
    }

    private bool PuedeEnviar()
    {
        return string.IsNullOrWhiteSpace(Pregunta) == false;
    }

    [RelayCommand(CanExecute = nameof(PuedeEnviar))]
    private void Enviar()
    {
        Preguntar(Pregunta);
    }

    [RelayCommand]
    private void EnviarSugerencia(string sugerencia)
    {
        Preguntar(sugerencia);
    }

    private void Preguntar(string texto)
    {
        string preguntaLimpia = texto.Trim();
        if (preguntaLimpia == "")
        {
            return;
        }

        Chat.Add(new MensajeDeChat("Tú", preguntaLimpia, TipoDeMensaje.Neutro));
        Pregunta = "";

        RespuestaIA respuesta = asistente.Preguntar(preguntaLimpia);
        Chat.Add(new MensajeDeChat(NombreDelAsistente + "  [" + respuesta.Motor + "]", respuesta.Texto, TipoDeMensaje.Informativo));
    }
}

// =====================================================================================
//  PESTAÑA: ACCESOS POR CATEGORÍA (tarjetas, tabla filtrable y correcciones)
// =====================================================================================
public sealed partial class AccesosVistaModelo : VistaModeloBase, IPestanaDelPanel
{
    private const string TituloCorreccion = "Corrección";

    private readonly IDialogos dialogos;
    private readonly Usuario usuarioSesion;
    private bool limpiandoFiltros;

    public IReadOnlyList<string> OpcionesDeCategoria { get; } = new[] { OpcionesDeFiltro.Todos, Categorias.Alumno, Categorias.Docente, Categorias.Visitante };
    public IReadOnlyList<string> OpcionesDeEstacionamiento { get; } = new[] { OpcionesDeFiltro.Todos, Estacionamientos.Facpya, Estacionamientos.Estadio };
    public IReadOnlyList<string> OpcionesDeEstado { get; } = new[] { OpcionesDeFiltro.Todos, OpcionesDeFiltro.Adentro, OpcionesDeFiltro.Finalizado };

    [ObservableProperty]
    private DatosDeTarjeta? tarjetaFacpya;

    [ObservableProperty]
    private DatosDeTarjeta? tarjetaEstadio;

    [ObservableProperty]
    private DatosDeTarjeta? tarjetaAlumnos;

    [ObservableProperty]
    private DatosDeTarjeta? tarjetaDocentes;

    [ObservableProperty]
    private DatosDeTarjeta? tarjetaVisitantes;

    [ObservableProperty]
    private string filtroCategoria = OpcionesDeFiltro.Todos;

    [ObservableProperty]
    private string filtroEstacionamiento = OpcionesDeFiltro.Todos;

    [ObservableProperty]
    private string filtroEstado = OpcionesDeFiltro.Todos;

    [ObservableProperty]
    private string busqueda = "";

    [ObservableProperty]
    private IReadOnlyList<FilaTabla> filas = Array.Empty<FilaTabla>();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AnularCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeshacerSalidaCommand))]
    [NotifyCanExecuteChangedFor(nameof(CorregirPlacaCommand))]
    private object? registroElegido;

    [ObservableProperty]
    private string conteo = "";

    public event Action? DatosCambiados;

    public AccesosVistaModelo(IDialogos dialogos, Usuario usuarioSesion)
    {
        ArgumentNullException.ThrowIfNull(dialogos);
        ArgumentNullException.ThrowIfNull(usuarioSesion);
        this.dialogos = dialogos;
        this.usuarioSesion = usuarioSesion;
    }

    public void Cargar()
    {
        ActualizarTarjetas();
        CargarTabla();
    }

    private void ActualizarTarjetas()
    {
        DateTime ahora = DateTime.Now;
        ResumenAforo facpya = CalculadoraAforo.CalcularResumen(Estacionamientos.Facpya, GestorArchivos.Accesos, GestorArchivos.Eventos, ahora);
        ResumenAforo estadio = CalculadoraAforo.CalcularResumen(Estacionamientos.Estadio, GestorArchivos.Accesos, GestorArchivos.Eventos, ahora);

        TarjetaFacpya = CrearTarjetaDeEstacionamiento(facpya);
        TarjetaEstadio = CrearTarjetaDeEstacionamiento(estadio);
        TarjetaAlumnos = CrearTarjetaDeCategoria(Categorias.Alumno, ahora);
        TarjetaDocentes = CrearTarjetaDeCategoria(Categorias.Docente, ahora);
        TarjetaVisitantes = CrearTarjetaDeCategoria(Categorias.Visitante, ahora);
    }

    private static DatosDeTarjeta CrearTarjetaDeEstacionamiento(ResumenAforo resumen)
    {
        return new DatosDeTarjeta(resumen.PorcentajeOcupacion + " %",
            resumen.VehiculosAdentro + " de " + resumen.CapacidadEfectiva + " · " + resumen.LugaresDisponibles + " libres", resumen.Semaforo);
    }

    private static DatosDeTarjeta CrearTarjetaDeCategoria(string categoria, DateTime ahora)
    {
        int adentro = GestorArchivos.Accesos.Count(acceso => acceso.EstaAdentro && acceso.Categoria == categoria);
        double promedio = CalculadoraAforo.CalcularPromedioEstanciaMinutos(categoria, GestorArchivos.Accesos, ahora);
        return new DatosDeTarjeta(adentro.ToString(), "Estancia promedio: " + promedio + " min", Semaforos.Dorado);
    }

    partial void OnFiltroCategoriaChanged(string value)
    {
        CargarTabla();
    }

    partial void OnFiltroEstacionamientoChanged(string value)
    {
        CargarTabla();
    }

    partial void OnFiltroEstadoChanged(string value)
    {
        CargarTabla();
    }

    partial void OnBusquedaChanged(string value)
    {
        if (limpiandoFiltros == false)
        {
            _ = EsperarEscrituraYEjecutar(CargarTabla);
        }
    }

    /// <summary>Regresa los 4 filtros a "Todos" / vacío y recarga la tabla una sola vez.</summary>
    [RelayCommand]
    private void LimpiarFiltros()
    {
        // Cada cambio de filtro recarga la tabla; mientras se limpian se pausa la recarga para no hacerlo 4 veces.
        limpiandoFiltros = true;
        FiltroCategoria = OpcionesDeFiltro.Todos;
        FiltroEstacionamiento = OpcionesDeFiltro.Todos;
        FiltroEstado = OpcionesDeFiltro.Todos;
        Busqueda = "";
        CancelarEsperaDeEscritura();
        limpiandoFiltros = false;

        CargarTabla();
    }

    private void CargarTabla()
    {
        if (limpiandoFiltros)
        {
            return;
        }

        DateTime ahora = DateTime.Now;
        List<FilaTabla> filasNuevas = new List<FilaTabla>();
        foreach (RegistroAcceso acceso in GestorArchivos.Accesos.OrderByDescending(registro => registro.FechaEntrada))
        {
            if (CumpleFiltros(acceso) == false)
            {
                continue;
            }

            string salida = acceso.FechaSalida == null ? "—" : acceso.FechaSalida.Value.ToString("dd/MM HH:mm");
            string estancia = CalculadoraAforo.FormatearDuracion(CalculadoraAforo.CalcularTiempoEstancia(acceso, ahora));
            filasNuevas.Add(new FilaTabla(acceso, acceso.Placa, acceso.Nombre, acceso.Categoria, acceso.Matricula, acceso.Estacionamiento, acceso.Cajon, acceso.CodigoEvento,
                acceso.Metodo, acceso.FechaEntrada.ToString("dd/MM HH:mm"), salida, estancia, acceso.EstaAdentro ? OpcionesDeFiltro.Adentro : OpcionesDeFiltro.Finalizado));
        }

        // Recargar no pierde la fila elegida: TablaDatos la vuelve a seleccionar, y si una corrección falla se puede reintentar.
        Filas = filasNuevas;
        Conteo = filasNuevas.Count + " registros";
    }

    private bool CumpleFiltros(RegistroAcceso acceso)
    {
        // Si el combo dice "Todos" no se filtra por ese campo.
        return (FiltroCategoria == OpcionesDeFiltro.Todos || acceso.Categoria == FiltroCategoria)
            && (FiltroEstacionamiento == OpcionesDeFiltro.Todos || acceso.Estacionamiento == FiltroEstacionamiento)
            && (FiltroEstado != OpcionesDeFiltro.Adentro || acceso.EstaAdentro)
            && (FiltroEstado != OpcionesDeFiltro.Finalizado || acceso.EstaAdentro == false)
            && ServicioCuentas.CoincideConBusqueda(Busqueda, acceso.Placa, acceso.Nombre, acceso.Matricula);
    }

    // ----------------------------- Correcciones -----------------------------

    private bool HayRegistroElegido()
    {
        return RegistroElegido is RegistroAcceso;
    }

    private Task<string?> PedirMotivo(string titulo)
    {
        return dialogos.PedirTexto(titulo, "Escribe el motivo. Quedará registrado en la bitácora con tu usuario.");
    }

    private async Task MostrarResultado(string error)
    {
        if (error != "")
        {
            await dialogos.Avisar(TituloCorreccion, error);
        }
        Cargar();
        DatosCambiados?.Invoke();
    }

    [RelayCommand(CanExecute = nameof(HayRegistroElegido))]
    private async Task Anular()
    {
        if (RegistroElegido is RegistroAcceso registro)
        {
            string? motivo = await PedirMotivo("Anular registro de " + registro.Placa);
            if (motivo != null)
            {
                await MostrarResultado(CorreccionMovimientos.AnularRegistro(registro, usuarioSesion.NombreUsuario, motivo));
            }
        }
    }

    [RelayCommand(CanExecute = nameof(HayRegistroElegido))]
    private async Task DeshacerSalida()
    {
        if (RegistroElegido is RegistroAcceso registro)
        {
            string? motivo = await PedirMotivo("Deshacer la salida de " + registro.Placa);
            if (motivo != null)
            {
                await MostrarResultado(CorreccionMovimientos.DeshacerSalida(registro, usuarioSesion.NombreUsuario, motivo));
            }
        }
    }

    [RelayCommand(CanExecute = nameof(HayRegistroElegido))]
    private async Task CorregirPlaca()
    {
        if (RegistroElegido is not RegistroAcceso registro)
        {
            return;
        }

        string? placaCorrecta = await dialogos.PedirTexto("Corregir placa", "Escribe la placa correcta:", registro.Placa, true);
        if (placaCorrecta == null)
        {
            return;
        }

        string? motivo = await PedirMotivo("Corregir placa de " + registro.Placa);
        if (motivo != null)
        {
            await MostrarResultado(CorreccionMovimientos.CorregirPlaca(registro, placaCorrecta, usuarioSesion.NombreUsuario, motivo));
        }
    }
}

// =====================================================================================
//  PESTAÑA: USUARIOS Y PERMISOS (solo el Administrador entra a este panel)
//  Las cuentas se crean desde el login como "Comunidad" (alumnos y docentes).
//  Aquí el Administrador puede convertir un correo en Operador o Administrador,
//  desactivar cuentas y dar una contraseña temporal a quien olvidó la suya.
//  La tabla también muestra a los alumnos y docentes que solo aparecen en los accesos
//  (estado "Sin cuenta"): no pueden iniciar sesión hasta crear su cuenta en el login.
// =====================================================================================
public sealed partial class UsuariosVistaModelo : VistaModeloBase, IPestanaDelPanel
{
    private readonly IDialogos dialogos;
    private readonly Usuario usuarioSesion;

    public IReadOnlyList<string> OpcionesDeMostrar { get; } = new[] { OpcionesDeFiltro.Todos, OpcionesDeFiltro.ConCuenta, OpcionesDeFiltro.SinCuenta };
    public IReadOnlyList<string> RolesDisponibles { get; } = new[] { Roles.Comunidad, Roles.Operador, Roles.Administrador };

    public string Reglas { get; } =
        "• Las cuentas nuevas se crean desde el login y siempre son \"Comunidad\".\n" +
        "• Comunidad = alumno o docente (usa su pase QR).\n" +
        "• Operador = registra entradas y salidas en la caseta.\n" +
        "• Administrador = este panel completo.\n" +
        "• Sin cuenta = solo aparece en los accesos; no puede iniciar sesión.\n" +
        "• No puedes quitarte tu propio rol ni desactivar tu cuenta.";

    [ObservableProperty]
    private string busqueda = "";

    [ObservableProperty]
    private string mostrar = OpcionesDeFiltro.Todos;

    [ObservableProperty]
    private IReadOnlyList<FilaTabla> filas = Array.Empty<FilaTabla>();

    [ObservableProperty]
    private object? personaElegida;

    [ObservableProperty]
    private string conteo = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AsignarRolCommand))]
    [NotifyCanExecuteChangedFor(nameof(CambiarEstadoCommand))]
    [NotifyCanExecuteChangedFor(nameof(RestablecerContrasenaCommand))]
    private string correoPermiso = "";

    [ObservableProperty]
    private string rolPermiso = Roles.Comunidad;

    public event Action? DatosCambiados
    {
        add { }
        remove { }
    }

    public UsuariosVistaModelo(IDialogos dialogos, Usuario usuarioSesion)
    {
        ArgumentNullException.ThrowIfNull(dialogos);
        ArgumentNullException.ThrowIfNull(usuarioSesion);
        this.dialogos = dialogos;
        this.usuarioSesion = usuarioSesion;
    }

    partial void OnBusquedaChanged(string value)
    {
        _ = EsperarEscrituraYEjecutar(Cargar);
    }

    partial void OnMostrarChanged(string value)
    {
        Cargar();
    }

    /// <summary>Al seleccionar una fila, se copia su correo (y su rol, si tiene cuenta) al formulario de permisos.</summary>
    partial void OnPersonaElegidaChanged(object? value)
    {
        if (value is Usuario cuenta)
        {
            CorreoPermiso = cuenta.Correo;
            RolPermiso = cuenta.Rol;
            LimpiarMensaje();
        }
        else if (value is PersonaSinCuenta persona)
        {
            CorreoPermiso = persona.Correo;
            MostrarInformacion(persona.Nombre + " solo aparece en los accesos. Para asignarle un rol, primero debe crear su cuenta en el login.");
        }
    }

    public void Cargar()
    {
        List<FilaTabla> filasNuevas = new List<FilaTabla>();

        if (Mostrar != OpcionesDeFiltro.SinCuenta)
        {
            foreach (Usuario usuario in GestorArchivos.Usuarios.OrderBy(cuenta => cuenta.Rol).ThenBy(cuenta => cuenta.NombreCompleto))
            {
                string placas = string.Join(", ", usuario.ObtenerPlacas());
                if (ServicioCuentas.CoincideConBusqueda(Busqueda, usuario.NombreCompleto, usuario.NombreUsuario, usuario.Correo, usuario.Matricula, placas))
                {
                    filasNuevas.Add(new FilaTabla(usuario, usuario.Correo, usuario.NombreCompleto, usuario.Categoria, usuario.Matricula, placas,
                        usuario.Rol, usuario.Activo ? "Activa" : "Desactivada"));
                }
            }
        }

        if (Mostrar != OpcionesDeFiltro.ConCuenta)
        {
            foreach (PersonaSinCuenta persona in ServicioCuentas.ObtenerPersonasSinCuenta())
            {
                if (ServicioCuentas.CoincideConBusqueda(Busqueda, persona.Nombre, persona.Correo, persona.Matricula, persona.Placa))
                {
                    filasNuevas.Add(new FilaTabla(persona, persona.Correo, persona.Nombre, persona.Categoria, persona.Matricula, persona.Placa,
                        "—", OpcionesDeFiltro.SinCuenta));
                }
            }
        }

        Filas = filasNuevas;
        Conteo = filasNuevas.Count + " personas";
    }

    private bool HayCorreo()
    {
        return string.IsNullOrWhiteSpace(CorreoPermiso) == false;
    }

    private Usuario? BuscarCuenta()
    {
        string error = ServicioPermisos.BuscarCuentaPorCorreo(CorreoPermiso, out Usuario? cuenta);
        if (error != "")
        {
            MostrarError(error);
        }
        return cuenta;
    }

    [RelayCommand(CanExecute = nameof(HayCorreo))]
    private async Task AsignarRol()
    {
        Usuario? cuenta = BuscarCuenta();
        if (cuenta == null)
        {
            return;
        }

        string nuevoRol = RolPermiso;
        string errorDeValidacion = ServicioPermisos.ValidarCambioDeRol(cuenta, nuevoRol, usuarioSesion);
        if (errorDeValidacion != "")
        {
            MostrarError(errorDeValidacion);
            return;
        }

        string rolAnterior = cuenta.Rol;
        bool confirmado = await dialogos.Confirmar("Confirmar permisos", "¿Cambiar el rol de " + cuenta.NombreCompleto + " (" + cuenta.Correo + ")\nde " +
                                                                         rolAnterior + " a " + nuevoRol + "?");
        if (confirmado == false)
        {
            return;
        }

        string error = ServicioPermisos.AsignarRol(cuenta, nuevoRol, usuarioSesion);
        Cargar();
        if (error != "")
        {
            MostrarError(error);
            return;
        }
        MostrarExito(cuenta.Correo + ": " + rolAnterior + " → " + nuevoRol + ". El cambio aplica la próxima vez que inicie sesión.");
    }

    [RelayCommand(CanExecute = nameof(HayCorreo))]
    private async Task CambiarEstado()
    {
        Usuario? cuenta = BuscarCuenta();
        if (cuenta == null)
        {
            return;
        }

        string errorDeValidacion = ServicioPermisos.ValidarCambioDeEstado(cuenta, usuarioSesion);
        if (errorDeValidacion != "")
        {
            MostrarError(errorDeValidacion);
            return;
        }

        string accion = cuenta.Activo ? "desactivar" : "activar";
        if (await dialogos.Confirmar("Confirmar", "¿Quieres " + accion + " la cuenta de " + cuenta.NombreCompleto + "?") == false)
        {
            return;
        }

        string error = ServicioPermisos.CambiarEstadoCuenta(cuenta, usuarioSesion);
        Cargar();
        if (error != "")
        {
            MostrarError(error);
            return;
        }
        MostrarExito("Cuenta de " + cuenta.Correo + (cuenta.Activo ? " activada." : " desactivada: ya no puede iniciar sesión ni entrar a los estacionamientos."));
    }

    [RelayCommand(CanExecute = nameof(HayCorreo))]
    private async Task RestablecerContrasena()
    {
        Usuario? cuenta = BuscarCuenta();
        if (cuenta == null)
        {
            return;
        }
        if (ServicioPermisos.EsLaMismaCuenta(cuenta, usuarioSesion))
        {
            MostrarError("Para tu propia cuenta usa el enlace \"Cambiar contraseña\".");
            return;
        }

        string? motivo = await dialogos.PedirTexto("Restablecer contraseña de " + cuenta.Correo, "Escribe el motivo. Quedará registrado en la bitácora con tu usuario.");
        if (motivo == null)
        {
            return;
        }
        if (string.IsNullOrWhiteSpace(motivo))
        {
            MostrarError("Escribe el motivo para restablecer la contraseña.");
            return;
        }

        string contrasenaTemporal = await Task.Run(() => ServicioCuentas.RestablecerContrasena(cuenta, usuarioSesion, motivo));
        if (contrasenaTemporal == "")
        {
            Cargar();
            return;
        }

        await dialogos.Avisar("Contraseña temporal", "Contraseña temporal de " + cuenta.NombreCompleto + ":\n\n" + contrasenaTemporal +
                                                     "\n\nEntrégasela en persona. Vence en " + ServicioCuentas.HorasDeVigenciaContrasenaTemporal +
                                                     " horas y deberá cambiarla al iniciar sesión. No se volverá a mostrar.");
        MostrarExito("Se restableció la contraseña de " + cuenta.Correo + ".");
    }
}

// =====================================================================================
//  PESTAÑA: REPORTES Y ALERTAS
//  Vehículos olvidados y cierre de turno, reportes en CSV o PDF, y pronóstico de ocupación.
// =====================================================================================
public sealed partial class ReportesVistaModelo : VistaModeloBase, IPestanaDelPanel
{
    private const string TituloDelPronostico = "Pronóstico de ocupación";
    private const string TituloDeOcupacionReal = "Ocupación real";

    private readonly IDialogos dialogos;
    private readonly ISelectorDeArchivos selectorDeArchivos;
    private readonly Usuario usuarioSesion;

    public string TituloOlvidados { get; } = "Vehículos con más de " + CalculadoraAforo.HorasParaConsiderarOlvidado + " h adentro";
    public IReadOnlyList<string> OpcionesDeEstacionamiento { get; } = new[] { ReportesAccesos.TodasLasOpciones, Estacionamientos.Facpya, Estacionamientos.Estadio };
    public IReadOnlyList<string> OpcionesDeCategoria { get; } = new[] { ReportesAccesos.TodasLasOpciones, Categorias.Alumno, Categorias.Docente, Categorias.Visitante };
    public IReadOnlyList<string> EstacionamientosDelPronostico { get; } = Estacionamientos.Todos;

    [ObservableProperty]
    private IReadOnlyList<FilaTabla> olvidados = Array.Empty<FilaTabla>();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RegistrarSalidaDelElegidoCommand))]
    private object? olvidadoElegido;

    [ObservableProperty]
    private DateTime desde = DateTime.Today.AddDays(-7);

    [ObservableProperty]
    private DateTime hasta = DateTime.Today;

    [ObservableProperty]
    private string estacionamientoDelReporte = ReportesAccesos.TodasLasOpciones;

    [ObservableProperty]
    private string categoriaDelReporte = ReportesAccesos.TodasLasOpciones;

    [ObservableProperty]
    private string estacionamientoDelPronostico = Estacionamientos.Facpya;

    [ObservableProperty]
    private DateTime diaDelPronostico = DateTime.Today.AddDays(1);

    [ObservableProperty]
    private PronosticoDelDia? pronostico;

    [ObservableProperty]
    private string resumenDelPronostico = "";

    [ObservableProperty]
    private string tituloDeOcupacion = TituloDelPronostico;

    public event Action? DatosCambiados;

    public ReportesVistaModelo(IDialogos dialogos, ISelectorDeArchivos selectorDeArchivos, Usuario usuarioSesion)
    {
        ArgumentNullException.ThrowIfNull(dialogos);
        ArgumentNullException.ThrowIfNull(selectorDeArchivos);
        ArgumentNullException.ThrowIfNull(usuarioSesion);
        this.dialogos = dialogos;
        this.selectorDeArchivos = selectorDeArchivos;
        this.usuarioSesion = usuarioSesion;
    }

    public void Cargar()
    {
        CargarOlvidados();
        ActualizarPronostico();
    }

    partial void OnEstacionamientoDelPronosticoChanged(string value)
    {
        ActualizarPronostico();
    }

    partial void OnDiaDelPronosticoChanged(DateTime value)
    {
        ActualizarPronostico();
    }

    // ----------------------------- Vehículos olvidados -----------------------------

    private void CargarOlvidados()
    {
        DateTime ahora = DateTime.Now;
        Olvidados = CalculadoraAforo.ObtenerVehiculosOlvidados(GestorArchivos.Accesos, ahora)
            .Select(acceso => new FilaTabla(acceso, acceso.Placa, acceso.Nombre, acceso.Estacionamiento, acceso.Cajon, acceso.FechaEntrada.ToString("dd/MM HH:mm"),
                CalculadoraAforo.FormatearDuracion(CalculadoraAforo.CalcularTiempoEstancia(acceso, ahora))))
            .ToList();
    }

    private Task<string?> PedirMotivo(string titulo)
    {
        return dialogos.PedirTexto(titulo, "Escribe el motivo. Quedará registrado en la bitácora con tu usuario.");
    }

    private async Task RecargarTrasCorreccion(string error)
    {
        if (error != "")
        {
            await dialogos.Avisar("Corrección", error);
        }
        Cargar();
        DatosCambiados?.Invoke();
    }

    private bool HayOlvidadoElegido()
    {
        return OlvidadoElegido is RegistroAcceso;
    }

    [RelayCommand(CanExecute = nameof(HayOlvidadoElegido))]
    private async Task RegistrarSalidaDelElegido()
    {
        if (OlvidadoElegido is RegistroAcceso registro)
        {
            string? motivo = await PedirMotivo("Registrar salida de " + registro.Placa);
            if (motivo != null)
            {
                await RecargarTrasCorreccion(CorreccionMovimientos.CerrarEstancia(registro, usuarioSesion.NombreUsuario, motivo, DateTime.Now));
            }
        }
    }

    /// <summary>
    /// Muestra el resumen del turno y pide confirmar. Si hay vehículos olvidados, el operador elige si registra su salida.
    /// El cierre se anota en la bitácora solo al final: si se cancela, no cambia nada ni queda registro.
    /// </summary>
    [RelayCommand]
    private async Task CerrarTurno()
    {
        DateTime ahora = DateTime.Now;
        List<RegistroAcceso> olvidadosAhora = CalculadoraAforo.ObtenerVehiculosOlvidados(GestorArchivos.Accesos, ahora);
        string resumen = ReportesAccesos.GenerarCierreDeTurno(ahora);

        List<RegistroAcceso> conSalida = new List<RegistroAcceso>();
        string motivoDelCierre = "";

        if (olvidadosAhora.Count == 0)
        {
            if (await dialogos.Confirmar("Cierre de turno", resumen + "\n\n¿Confirmas el cierre de turno?") == false)
            {
                return;
            }
        }
        else
        {
            OpcionCierreDeTurno opcion = await dialogos.PreguntarCierreDeTurno(resumen);
            if (opcion == OpcionCierreDeTurno.Cancelar)
            {
                return;
            }

            if (opcion == OpcionCierreDeTurno.CerrarSinRegistrar)
            {
                motivoDelCierre = "Sin registrar salidas. Vehículos olvidados: " + olvidadosAhora.Count;
            }
            else
            {
                string? motivo = await PedirMotivo("Cierre de turno");
                if (motivo == null)
                {
                    return;
                }
                motivoDelCierre = motivo;
                conSalida = olvidadosAhora;
            }
        }

        await RecargarTrasCorreccion(CorreccionMovimientos.CerrarTurno(conSalida, motivoDelCierre, usuarioSesion.NombreUsuario, ahora));
    }

    // ----------------------------- Reportes -----------------------------

    [RelayCommand]
    private Task ExportarCsv(CancellationToken cancelacion)
    {
        return Exportar(true, cancelacion);
    }

    [RelayCommand]
    private Task ExportarPdf(CancellationToken cancelacion)
    {
        return Exportar(false, cancelacion);
    }

    private async Task Exportar(bool esCsv, CancellationToken cancelacion)
    {
        // === [RÚBRICA 1] VALIDACIONES DE CAMPO ===
        if (Hasta.Date < Desde.Date)
        {
            MostrarError("La fecha \"Hasta\" no puede ser anterior a \"Desde\".");
            return;
        }

        FiltroReporte filtro = new FiltroReporte
        {
            Desde = Desde,
            Hasta = Hasta,
            Estacionamiento = EstacionamientoDelReporte,
            Categoria = CategoriaDelReporte
        };
        List<RegistroAcceso> registros = ReportesAccesos.ObtenerRegistros(filtro);
        if (registros.Count == 0)
        {
            MostrarError("No hay accesos con esos filtros.");
            return;
        }

        string? ruta = await selectorDeArchivos.ElegirDestino("reporte_accesos_" + DateTime.Now.ToString("yyyyMMdd"),
            esCsv ? "Archivo CSV" : "Archivo PDF", esCsv ? ".csv" : ".pdf");
        if (ruta == null)
        {
            return;
        }

        try
        {
            DateTime ahora = DateTime.Now;
            await Task.Run(() =>
            {
                if (esCsv)
                {
                    ReportesAccesos.ExportarCsv(ruta, registros, ahora);
                }
                else
                {
                    ReportesAccesos.ExportarPdf(ruta, filtro, registros, ahora);
                }
            }, cancelacion);

            GestorArchivos.RegistrarEnBitacora(usuarioSesion.NombreUsuario, "Exportar reporte",
                registros.Count + " registros · " + filtro.Desde.ToString("dd/MM/yyyy") + " a " + filtro.Hasta.ToString("dd/MM/yyyy"));
            MostrarExito(registros.Count + " registros exportados a " + ruta);
        }
        catch (Exception ex) when (GestorArchivos.EsErrorDeDisco(ex))
        {
            GestorArchivos.RegistrarError(ex);
            MostrarError("No se pudo escribir el archivo (¿está abierto en otro programa?): " + ex.Message);
        }
    }

    // ----------------------------- Pronóstico -----------------------------

    private void ActualizarPronostico()
    {
        DateTime ahora = DateTime.Now;
        PronosticoDelDia? calculado = PronosticoOcupacion.Calcular(EstacionamientoDelPronostico, DiaDelPronostico.Date, GestorArchivos.Accesos, ahora);
        Pronostico = calculado;
        TituloDeOcupacion = calculado?.EsOcupacionReal == true ? TituloDeOcupacionReal : TituloDelPronostico;

        if (calculado == null)
        {
            ResumenDelPronostico = PronosticoOcupacion.ExplicarFaltaDePronostico(EstacionamientoDelPronostico, DiaDelPronostico.Date, GestorArchivos.Accesos);
            return;
        }

        PuntoPronostico horaPico = PronosticoOcupacion.ObtenerHoraPico(calculado);
        if (calculado.EsOcupacionReal)
        {
            ResumenDelPronostico = DescribirOcupacionReal(calculado, horaPico);
            return;
        }

        string baseDelCalculo = calculado.UsaMismoDiaDeLaSemana ? "el mismo día de la semana" : "todos los días registrados";
        ResumenDelPronostico = "Hora pico estimada: " + horaPico.Hora.ToString("00") + ":00, con unos " + Math.Round(horaPico.VehiculosEstimados) +
                               " vehículos (" + horaPico.PorcentajeOcupacion + " %).\n" +
                               "Es una estimación: promedio de " + calculado.DiasDeHistorial + " día(s) de historial (" + baseDelCalculo + ").";
    }

    private static string DescribirOcupacionReal(PronosticoDelDia ocupacion, PuntoPronostico horaPico)
    {
        string dia = ocupacion.Dia.ToString("dd/MM/yyyy");
        if (horaPico.VehiculosEstimados == 0)
        {
            return "No hubo vehículos en " + ocupacion.Estacionamiento + " el " + dia + ".";
        }
        return "Ocupación real del " + dia + ": a las " + horaPico.Hora.ToString("00") + ":00 hubo " + horaPico.VehiculosEstimados +
               " vehículos (" + horaPico.PorcentajeOcupacion + " %), la hora más llena.";
    }
}
