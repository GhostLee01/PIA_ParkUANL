using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PIA_ParkUANL;

/// <summary>Una fila de una tabla: el texto de cada celda y el dato que representa (el acceso, el evento...).</summary>
public sealed class FilaTabla
{
    public object? Dato { get; }
    public IReadOnlyList<string> Celdas { get; }

    public FilaTabla(object? dato, params string[] celdas)
    {
        ArgumentNullException.ThrowIfNull(celdas);
        Dato = dato;
        Celdas = celdas;
    }
}

// =====================================================================================
//  PESTAÑA: EVENTOS Y PASES
// =====================================================================================
public sealed partial class EventosVistaModelo : VistaModeloBase, IPestanaDelPanel
{
    private const int PixelesPorModulo = 6;
    private const int MaximoVisitantesInicial = 100;
    private const int VigenciaInicial = 12;
    private const int HoraDeInicioSugerida = 9;
    private const int HoraDeFinSugerida = 18;
    private const string UbicacionSugerida = Estacionamientos.Facpya;

    private readonly IDialogos dialogos;
    private readonly IPortapapeles portapapeles;
    private readonly Usuario usuarioSesion;

    public double LimiteReservados { get; } = CalculadoraAforo.CapacidadFacpya;
    public double MinimoVisitantes { get; } = ServicioEventos.MinimoVisitantes;
    public double LimiteVisitantes { get; } = ServicioEventos.MaximoVisitantes;
    public double MinimoVigencia { get; } = ServicioEventos.MinimoVigenciaHoras;
    public double LimiteVigencia { get; } = ServicioEventos.MaximoVigenciaHoras;
    public IReadOnlyList<string> OpcionesDeUbicacion { get; } = Estacionamientos.Todos;

    [ObservableProperty]
    private IReadOnlyList<FilaTabla> eventos = Array.Empty<FilaTabla>();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GuardarCambiosCommand))]
    [NotifyCanExecuteChangedFor(nameof(CambiarEstadoCommand))]
    [NotifyCanExecuteChangedFor(nameof(EliminarEventoCommand))]
    [NotifyCanExecuteChangedFor(nameof(CrearEventoCommand))]
    [NotifyCanExecuteChangedFor(nameof(EmitirPaseCommand))]
    [NotifyPropertyChangedFor(nameof(CodigoSoloLectura))]
    private object? eventoElegido;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CrearEventoCommand))]
    private string codigo = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CrearEventoCommand))]
    [NotifyCanExecuteChangedFor(nameof(GuardarCambiosCommand))]
    private string titulo = "";

    /// <summary>FACPYA o Estadio; null si no hay ninguna elegida (un evento guardado con otro texto).</summary>
    [ObservableProperty]
    private string? ubicacion;

    [ObservableProperty]
    private DateTime inicio;

    [ObservableProperty]
    private DateTime fin;

    [ObservableProperty]
    private double reservados;

    [ObservableProperty]
    private double maximoVisitantes;

    [ObservableProperty]
    private double vigenciaHoras;

    [ObservableProperty]
    private bool? permiteReingreso = true;

    [ObservableProperty]
    private IReadOnlyList<FilaTabla> pases = Array.Empty<FilaTabla>();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RevocarPaseCommand))]
    [NotifyCanExecuteChangedFor(nameof(EliminarPaseCommand))]
    [NotifyCanExecuteChangedFor(nameof(NuevoPaseCommand))]
    [NotifyCanExecuteChangedFor(nameof(EmitirPaseCommand))]
    [NotifyPropertyChangedFor(nameof(CamposDelPaseBloqueados))]
    private object? paseElegido;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EmitirPaseCommand))]
    private string nombreVisitante = "";

    [ObservableProperty]
    private string correoVisitante = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EmitirPaseCommand))]
    private string placaVisitante = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CopiarCodigoCommand))]
    private string codigoDelPase = "";

    [ObservableProperty]
    private ImagenQr? imagenDelPase;

    /// <summary>De quién es el pase elegido, o por qué ya no tiene QR.</summary>
    [ObservableProperty]
    private string avisoDelPase = "";

    [ObservableProperty]
    private bool paseSinQrValido;

    public event Action? DatosCambiados;

    /// <summary>El código no se cambia: los pases lo usan para ligarse al evento.</summary>
    public bool CodigoSoloLectura
    {
        get { return EventoElegido is EventoCampus; }
    }

    /// <summary>Con un pase elegido, los campos muestran sus datos y no se editan: para emitir otro se usa "Nuevo pase".</summary>
    public bool CamposDelPaseBloqueados
    {
        get { return PaseElegido is PaseVisitante; }
    }

    public EventosVistaModelo(IDialogos dialogos, IPortapapeles portapapeles, Usuario usuarioSesion)
    {
        ArgumentNullException.ThrowIfNull(dialogos);
        ArgumentNullException.ThrowIfNull(portapapeles);
        ArgumentNullException.ThrowIfNull(usuarioSesion);
        this.dialogos = dialogos;
        this.portapapeles = portapapeles;
        this.usuarioSesion = usuarioSesion;
        LimpiarEditor();
    }

    /// <summary>Recarga la tabla de eventos conservando el evento elegido.</summary>
    public void Cargar()
    {
        DateTime ahora = DateTime.Now;
        Eventos = GestorArchivos.Eventos
            .OrderBy(evento => evento.FechaInicio)
            .Select(evento => new FilaTabla(evento, evento.Codigo, evento.Titulo, evento.FechaInicio.ToString("dd/MM HH:mm"),
                evento.FechaFin.ToString("dd/MM HH:mm"), evento.ObtenerEstado(ahora), evento.LugaresReservadosFacpya.ToString(),
                CalculadoraAforo.ContarPasesActivos(evento, GestorArchivos.Pases, ahora) + "/" + evento.MaxVisitantes,
                CalculadoraAforo.CalcularPorcentajeCupoEvento(evento, GestorArchivos.Pases, ahora) + " %"))
            .ToList();
        CargarEditor();
    }

    partial void OnEventoElegidoChanged(object? value)
    {
        CargarEditor();
    }

    private void CargarEditor()
    {
        if (EventoElegido is EventoCampus evento)
        {
            Codigo = evento.Codigo;
            Titulo = evento.Titulo;
            Ubicacion = ReglasEventos.InterpretarUbicacionAntigua(evento.Ubicacion);
            Inicio = evento.FechaInicio;
            Fin = evento.FechaFin;
            Reservados = Math.Clamp(evento.LugaresReservadosFacpya, 0, CalculadoraAforo.CapacidadFacpya);
            MaximoVisitantes = Math.Clamp(evento.MaxVisitantes, ServicioEventos.MinimoVisitantes, ServicioEventos.MaximoVisitantes);
            VigenciaHoras = Math.Clamp(evento.VigenciaHorasPase, ServicioEventos.MinimoVigenciaHoras, ServicioEventos.MaximoVigenciaHoras);
            PermiteReingreso = evento.PermiteReingreso;
        }
        CargarPases();
    }

    [RelayCommand]
    private void LimpiarEditor()
    {
        DateTime manana = DateTime.Today.AddDays(1);
        EventoElegido = null;
        Codigo = "";
        Titulo = "";
        Ubicacion = UbicacionSugerida;
        Inicio = manana.AddHours(HoraDeInicioSugerida);
        Fin = manana.AddHours(HoraDeFinSugerida);
        Reservados = 0;
        MaximoVisitantes = MaximoVisitantesInicial;
        VigenciaHoras = VigenciaInicial;
        PermiteReingreso = true;
        LimpiarMensaje();
        CargarPases();
    }

    /// <summary>Un NumberBox vacío vale NaN, y uno enorme no cabe en int: en los dos casos se reporta como vacío.</summary>
    private static int? LeerEntero(double valor)
    {
        if (double.IsNaN(valor) || valor < int.MinValue || valor > int.MaxValue)
        {
            return null;
        }
        return (int)Math.Round(valor, MidpointRounding.AwayFromZero);
    }

    private DatosEvento LeerDatos()
    {
        return new DatosEvento(Codigo, Titulo, Ubicacion ?? "", Inicio, Fin, LeerEntero(Reservados), LeerEntero(MaximoVisitantes), LeerEntero(VigenciaHoras), PermiteReingreso == true);
    }

    private bool PuedeCrear()
    {
        return EventoElegido == null && string.IsNullOrWhiteSpace(Codigo) == false && string.IsNullOrWhiteSpace(Titulo) == false;
    }

    [RelayCommand(CanExecute = nameof(PuedeCrear))]
    private void CrearEvento()
    {
        Guardar(null);
    }

    private bool PuedeGuardarCambios()
    {
        return EventoElegido is EventoCampus && string.IsNullOrWhiteSpace(Titulo) == false;
    }

    [RelayCommand(CanExecute = nameof(PuedeGuardarCambios))]
    private void GuardarCambios()
    {
        if (EventoElegido is EventoCampus evento)
        {
            Guardar(evento);
        }
    }

    private void Guardar(EventoCampus? existente)
    {
        ResultadoValidacion resultado = ServicioEventos.GuardarEvento(LeerDatos(), existente, usuarioSesion.NombreUsuario, DateTime.Now, out EventoCampus? guardado);
        if (resultado.EsCorrecto == false || guardado == null)
        {
            MostrarError(resultado);
            Cargar();
            return;
        }

        EventoElegido = guardado;
        Cargar();
        MostrarExito(existente == null ? "Evento " + guardado.Codigo + " creado." : "Cambios guardados en " + guardado.Codigo + ".");
        DatosCambiados?.Invoke();
    }

    private bool HayEventoElegido()
    {
        return EventoElegido is EventoCampus;
    }

    [RelayCommand(CanExecute = nameof(HayEventoElegido))]
    private void CambiarEstado()
    {
        if (EventoElegido is not EventoCampus evento)
        {
            return;
        }

        ResultadoValidacion resultado = ServicioEventos.CambiarEstadoEvento(evento, usuarioSesion.NombreUsuario, DateTime.Now);
        Cargar();
        if (resultado.EsCorrecto == false)
        {
            MostrarError(resultado);
            return;
        }
        MostrarExito("Evento " + evento.Codigo + (evento.Activo ? " activado." : " desactivado (sus cajones reservados se liberan)."));
    }

    [RelayCommand(CanExecute = nameof(HayEventoElegido))]
    private async Task EliminarEvento()
    {
        if (EventoElegido is not EventoCampus evento)
        {
            return;
        }

        string motivo = ServicioEventos.ObtenerMotivoParaNoEliminar(evento);
        if (motivo != "")
        {
            MostrarError(motivo);
            return;
        }

        if (await dialogos.Confirmar("Confirmar", "¿Eliminar el evento " + evento.Titulo + " y sus " + ServicioEventos.ContarPasesDe(evento) + " pases?") == false)
        {
            return;
        }

        ResultadoValidacion resultado = ServicioEventos.EliminarEvento(evento, usuarioSesion.NombreUsuario);
        if (resultado.EsCorrecto == false)
        {
            MostrarError(resultado);
            Cargar();
            return;
        }

        LimpiarEditor();
        Cargar();
        MostrarExito("Evento " + evento.Codigo + " eliminado.");
    }

    // ----------------------------- Pases -----------------------------

    private void CargarPases()
    {
        DateTime ahora = DateTime.Now;
        string? codigoDelEvento = (EventoElegido as EventoCampus)?.Codigo;

        Pases = GestorArchivos.Pases
            .Where(pase => pase.CodigoEvento == codigoDelEvento)
            .Select(pase => new FilaTabla(pase, pase.Id, pase.NombreVisitante, pase.Placa, pase.FechaEmision.ToString("dd/MM HH:mm"),
                pase.FechaExpiracion.ToString("dd/MM HH:mm"), pase.ObtenerEstado(ahora)))
            .ToList();
        MostrarQrDelPase();
    }

    partial void OnPaseElegidoChanged(object? value)
    {
        MostrarDatosDelPase();
        MostrarQrDelPase();
    }

    /// <summary>El QR siempre corresponde a los datos que se ven: sin pase elegido, los campos quedan libres para emitir uno.</summary>
    private void MostrarDatosDelPase()
    {
        PaseVisitante? pase = PaseElegido as PaseVisitante;
        NombreVisitante = pase?.NombreVisitante ?? "";
        CorreoVisitante = pase?.Correo ?? "";
        PlacaVisitante = pase?.Placa ?? "";
    }

    private void MostrarQrDelPase()
    {
        string codigo = "";
        ImagenQr? imagen = null;
        string aviso = "";
        bool sinQrValido = false;

        if (PaseElegido is PaseVisitante pase && GestorArchivos.Pases.Contains(pase))
        {
            string estado = pase.ObtenerEstado(DateTime.Now);
            if (estado == EstadosDePase.Activo)
            {
                codigo = CodigoPaseVisitante.Generar(pase);
                byte[] pixeles = GeneradorQR.CrearPixeles(codigo, PixelesPorModulo, out int lado);
                imagen = new ImagenQr(pixeles, lado);
                aviso = "Pase " + pase.Id + ", vence el " + pase.FechaExpiracion.ToString("dd/MM HH:mm") + ". Para emitir otro, usa \"Nuevo pase\".";
            }
            else
            {
                // La caseta ya no lo acepta: mostrar o copiar su QR solo invitaría a usarlo.
                sinQrValido = true;
                aviso = "Pase " + pase.Id + " " + estado.ToLowerInvariant() + ": ya no tiene un QR válido.";
            }
        }

        CodigoDelPase = codigo;
        ImagenDelPase = imagen;
        AvisoDelPase = aviso;
        PaseSinQrValido = sinQrValido;
    }

    [RelayCommand(CanExecute = nameof(HayPaseElegido))]
    private void NuevoPase()
    {
        PaseElegido = null;
        LimpiarMensaje();
    }

    private bool PuedeEmitir()
    {
        return EventoElegido is EventoCampus && PaseElegido == null
            && string.IsNullOrWhiteSpace(NombreVisitante) == false && string.IsNullOrWhiteSpace(PlacaVisitante) == false;
    }

    [RelayCommand(CanExecute = nameof(PuedeEmitir))]
    private void EmitirPase()
    {
        DatosPase datos = new DatosPase(NombreVisitante, CorreoVisitante, PlacaVisitante);
        ResultadoValidacion resultado = ServicioEventos.EmitirPase(datos, EventoElegido as EventoCampus, usuarioSesion.NombreUsuario, DateTime.Now, out PaseVisitante? pase);
        if (resultado.EsCorrecto == false || pase == null)
        {
            MostrarError(resultado);
            return;
        }

        // Al elegirlo, los campos muestran los datos con los que quedó guardado y se bloquean.
        Cargar();
        PaseElegido = pase;
        MostrarExito("Pase " + pase.Id + " emitido; vence el " + pase.FechaExpiracion.ToString("dd/MM HH:mm") + ".");
    }

    private bool HayPaseElegido()
    {
        return PaseElegido is PaseVisitante;
    }

    [RelayCommand(CanExecute = nameof(HayPaseElegido))]
    private async Task RevocarPase()
    {
        if (PaseElegido is not PaseVisitante pase)
        {
            return;
        }
        if (await dialogos.Confirmar("Confirmar", "¿Revocar el pase " + pase.Id + " de " + pase.NombreVisitante + "?") == false)
        {
            return;
        }

        ResultadoValidacion resultado = ServicioEventos.RevocarPase(pase, usuarioSesion.NombreUsuario);
        Cargar();
        if (resultado.EsCorrecto == false)
        {
            MostrarError(resultado);
            return;
        }
        MostrarExito("Pase " + pase.Id + " revocado: la caseta lo rechazará.");
    }

    [RelayCommand(CanExecute = nameof(HayPaseElegido))]
    private async Task EliminarPase()
    {
        if (PaseElegido is not PaseVisitante pase)
        {
            return;
        }
        if (await dialogos.Confirmar("Confirmar", "¿Eliminar definitivamente el pase " + pase.Id + "?") == false)
        {
            return;
        }

        ResultadoValidacion resultado = ServicioEventos.EliminarPase(pase, usuarioSesion.NombreUsuario);
        Cargar();
        if (resultado.EsCorrecto == false)
        {
            MostrarError(resultado);
            return;
        }
        MostrarExito("Pase " + pase.Id + " eliminado.");
    }

    private bool HayCodigo()
    {
        return CodigoDelPase != "";
    }

    [RelayCommand(CanExecute = nameof(HayCodigo))]
    private async Task CopiarCodigo()
    {
        if (portapapeles.Copiar(CodigoDelPase) == false)
        {
            await dialogos.Avisar("ParkUANL", "El portapapeles está ocupado por otro programa. Intenta de nuevo.");
        }
    }
}
