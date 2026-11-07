namespace PIA_ParkUANL;

/// <summary>Lo que se captura de un evento. Los números son null si el campo se dejó vacío.</summary>
public sealed record DatosEvento(string Codigo, string Titulo, string Ubicacion, DateTime Inicio, DateTime Fin,
    int? Reservados, int? MaximoVisitantes, int? VigenciaHoras, bool PermiteReingreso);

/// <summary>Lo que se captura de un pase de visitante.</summary>
public sealed record DatosPase(string Nombre, string Correo, string Placa);

// =====================================================================================
//  SERVICIOEVENTOS.CS — Crear, editar y eliminar eventos, y emitir, revocar y eliminar pases.
//  Cada cambio se guarda junto con su línea de bitácora.
// =====================================================================================
public static class ServicioEventos
{
    public const int MinimoVisitantes = 1;
    public const int MaximoVisitantes = 5000;
    public const int MinimoVigenciaHoras = 1;
    public const int MaximoVigenciaHoras = 72;

    private const string MensajeNoSeGuardo = "No se pudo guardar el cambio. Intenta de nuevo.";

    // ----------------------------- Eventos -----------------------------

    /// <summary>Crea (eventoExistente = null) o actualiza un evento, con sus validaciones.</summary>
    public static ResultadoValidacion GuardarEvento(DatosEvento datos, EventoCampus? eventoExistente, string nombreUsuario, DateTime ahora, out EventoCampus? guardado)
    {
        ArgumentNullException.ThrowIfNull(datos);

        // === [RÚBRICA 1] VALIDACIONES DE CAMPO ===
        guardado = null;
        bool esNuevo = eventoExistente == null;
        string codigo = datos.Codigo.Trim().ToLowerInvariant();
        string titulo = datos.Titulo.Trim();
        string ubicacion = datos.Ubicacion.Trim();

        if (Validaciones.EsCodigoEventoValido(codigo) == false)
        {
            return new ResultadoValidacion("El código es obligatorio, admite hasta " + Validaciones.LongitudMaximaCodigoEvento +
                                           " caracteres y solo minúsculas, números y guiones (ej. expo-2026).", CampoFormulario.Codigo);
        }
        if (titulo == "")
        {
            return new ResultadoValidacion("El título es obligatorio.", CampoFormulario.Titulo);
        }
        if (titulo.Length > Validaciones.LongitudMaximaNombre || Validaciones.EsTextoLibreValido(titulo) == false)
        {
            return new ResultadoValidacion("El título admite hasta " + Validaciones.LongitudMaximaNombre + " caracteres.", CampoFormulario.Titulo);
        }
        if (ubicacion.Length > Validaciones.LongitudMaximaNombre || Validaciones.EsTextoLibreValido(ubicacion) == false)
        {
            return new ResultadoValidacion("La ubicación admite hasta " + Validaciones.LongitudMaximaNombre + " caracteres.", CampoFormulario.Titulo);
        }
        if (datos.Fin <= datos.Inicio)
        {
            return new ResultadoValidacion("La fecha de fin debe ser posterior a la de inicio.", CampoFormulario.Fechas);
        }
        if (esNuevo && datos.Fin <= ahora)
        {
            return new ResultadoValidacion("El evento ya habría terminado: revisa la fecha de fin.", CampoFormulario.Fechas);
        }

        if (datos.Reservados == null || datos.Reservados < 0 || datos.Reservados > CalculadoraAforo.CapacidadFacpya)
        {
            return new ResultadoValidacion("Escribe los cajones reservados, de 0 a " + CalculadoraAforo.CapacidadFacpya + ".", CampoFormulario.Reservados);
        }
        if (datos.MaximoVisitantes == null || datos.MaximoVisitantes < MinimoVisitantes || datos.MaximoVisitantes > MaximoVisitantes)
        {
            return new ResultadoValidacion("Escribe el máximo de visitantes, de " + MinimoVisitantes + " a " + MaximoVisitantes + ".", CampoFormulario.MaximoVisitantes);
        }
        if (datos.VigenciaHoras == null || datos.VigenciaHoras < MinimoVigenciaHoras || datos.VigenciaHoras > MaximoVigenciaHoras)
        {
            return new ResultadoValidacion("Escribe la vigencia del pase, de " + MinimoVigenciaHoras + " a " + MaximoVigenciaHoras + " horas.", CampoFormulario.VigenciaHoras);
        }

        int reservados = datos.Reservados.Value;
        int maximoVisitantes = datos.MaximoVisitantes.Value;
        int vigencia = datos.VigenciaHoras.Value;

        if (esNuevo && GestorArchivos.Eventos.Any(evento => evento.Codigo == codigo))
        {
            return new ResultadoValidacion("Ya existe un evento con el código " + codigo + ".", CampoFormulario.Codigo);
        }

        // Reserva: la suma de reservas de los eventos en las mismas fechas no puede pasar de los 500 cajones de FACPYA.
        int reservasDeOtros = ReglasEventos.SumarReservasTraslapadas(datos.Inicio, datos.Fin, eventoExistente, ahora);
        if (reservasDeOtros + reservados > CalculadoraAforo.CapacidadFacpya)
        {
            return new ResultadoValidacion("Reserva excesiva: otros eventos en esas fechas apartan " + reservasDeOtros + " cajones; quedan " +
                                           (CalculadoraAforo.CapacidadFacpya - reservasDeOtros) + " para reservar.", CampoFormulario.Reservados);
        }

        bool activoAlGuardar = eventoExistente?.Activo ?? true;
        if (activoAlGuardar && datos.Inicio <= ahora && datos.Fin >= ahora)
        {
            ResultadoValidacion cupo = ValidarReservaContraOcupacion(reservados, eventoExistente, ahora);
            if (cupo.EsCorrecto == false)
            {
                return cupo;
            }
        }

        if (eventoExistente != null)
        {
            ResultadoValidacion cambios = ValidarCambiosDeEventoExistente(eventoExistente, datos.Inicio, reservados, maximoVisitantes, ahora);
            if (cambios.EsCorrecto == false)
            {
                return cambios;
            }
        }

        EventoCampus evento = eventoExistente ?? new EventoCampus { Id = GestorArchivos.SiguienteIdEvento(), Codigo = codigo };

        // === [RÚBRICA 2] ALMACENAMIENTO EN ARCHIVOS DE TEXTO (.JSON) ===
        bool seGuardo = GestorArchivos.Transaccion(ArchivosDeDatos.Eventos | ArchivosDeDatos.Bitacora, () =>
        {
            if (esNuevo)
            {
                GestorArchivos.Eventos.Add(evento);
            }
            evento.Titulo = titulo;
            evento.Ubicacion = ubicacion;
            evento.FechaInicio = datos.Inicio;
            evento.FechaFin = datos.Fin;
            evento.LugaresReservadosFacpya = reservados;
            evento.MaxVisitantes = maximoVisitantes;
            evento.VigenciaHorasPase = vigencia;
            evento.PermiteReingreso = datos.PermiteReingreso;
            GestorArchivos.AnotarEnBitacora(nombreUsuario, esNuevo ? "Crear evento" : "Editar evento", evento.Codigo);
        });
        if (seGuardo == false)
        {
            return new ResultadoValidacion(MensajeNoSeGuardo);
        }

        guardado = evento;
        return ResultadoValidacion.Correcto;
    }

    private static ResultadoValidacion ValidarCambiosDeEventoExistente(EventoCampus evento, DateTime inicio, int reservados, int maximoVisitantes, DateTime ahora)
    {
        int pasesActivos = CalculadoraAforo.ContarPasesActivos(evento, GestorArchivos.Pases, ahora);
        if (maximoVisitantes < pasesActivos)
        {
            return new ResultadoValidacion("El máximo de visitantes no puede ser menor a los " + pasesActivos + " pases activos.", CampoFormulario.MaximoVisitantes);
        }

        // Si la reserva queda por debajo de los cajones ocupados, esos autos no se contarían en el aforo.
        int reservadosOcupados = CalculadoraAforo.ContarReservadosOcupados(Estacionamientos.Facpya, evento.Codigo, GestorArchivos.Accesos, GestorArchivos.Eventos, ahora);
        if (reservados < reservadosOcupados)
        {
            return new ResultadoValidacion("No puedes reservar menos de los " + reservadosOcupados + " cajones que ya están ocupados por este evento.", CampoFormulario.Reservados);
        }

        // Un pase ya emitido no cambia: si el evento se pospone demasiado, vencería antes de empezar.
        int pasesPorVencer = ReglasEventos.ContarPasesQueVencenAntesDe(evento, inicio, ahora);
        if (pasesPorVencer > 0)
        {
            return new ResultadoValidacion(pasesPorVencer + " pases activos vencerían antes del nuevo inicio. Revócalos o elige una fecha más cercana.", CampoFormulario.Fechas);
        }
        return ResultadoValidacion.Correcto;
    }

    /// <summary>Una reserva que empieza a contar ahora no puede dejar la capacidad por debajo de los autos que ya están adentro.</summary>
    private static ResultadoValidacion ValidarReservaContraOcupacion(int reservados, EventoCampus? eventoExistente, DateTime ahora)
    {
        int reservasVigentes = CalculadoraAforo.CalcularLugaresReservados(Estacionamientos.Facpya, GestorArchivos.Eventos, ahora);
        int reservasDeOtros = reservasVigentes - (eventoExistente != null && eventoExistente.EstaVigente(ahora) ? eventoExistente.LugaresReservadosFacpya : 0);
        int adentro = CalculadoraAforo.ContarVehiculosAdentro(Estacionamientos.Facpya, GestorArchivos.Accesos, GestorArchivos.Eventos, ahora);
        int reservablesAhora = Math.Max(0, CalculadoraAforo.CapacidadFacpya - reservasDeOtros - adentro);

        if (reservados > reservablesAhora)
        {
            return new ResultadoValidacion("Hay " + adentro + " vehículos en FACPYA: por ahora solo se pueden reservar " + reservablesAhora + " cajones.", CampoFormulario.Reservados);
        }
        return ResultadoValidacion.Correcto;
    }

    public static ResultadoValidacion CambiarEstadoEvento(EventoCampus evento, string nombreUsuario, DateTime ahora)
    {
        ArgumentNullException.ThrowIfNull(evento);

        // Al reactivar, se vuelve a revisar que la reserva quepa en FACPYA.
        if (evento.Activo == false)
        {
            int reservasDeOtros = ReglasEventos.SumarReservasTraslapadas(evento.FechaInicio, evento.FechaFin, evento, ahora);
            if (reservasDeOtros + evento.LugaresReservadosFacpya > CalculadoraAforo.CapacidadFacpya)
            {
                return new ResultadoValidacion("No se puede activar: su reserva no cabe junto con la de los otros eventos.");
            }
            if (evento.FechaInicio <= ahora && evento.FechaFin >= ahora)
            {
                ResultadoValidacion cupo = ValidarReservaContraOcupacion(evento.LugaresReservadosFacpya, evento, ahora);
                if (cupo.EsCorrecto == false)
                {
                    return cupo;
                }
            }
        }

        bool quedaActivo = evento.Activo == false;
        bool guardado = GestorArchivos.Transaccion(ArchivosDeDatos.Eventos | ArchivosDeDatos.Bitacora, () =>
        {
            evento.Activo = quedaActivo;
            GestorArchivos.AnotarEnBitacora(nombreUsuario, quedaActivo ? "Activar evento" : "Desactivar evento", evento.Codigo);
        });
        return guardado ? ResultadoValidacion.Correcto : new ResultadoValidacion(MensajeNoSeGuardo);
    }

    /// <summary>Regresa el motivo por el que no se puede eliminar, o "" si se puede.</summary>
    public static string ObtenerMotivoParaNoEliminar(EventoCampus evento)
    {
        ArgumentNullException.ThrowIfNull(evento);

        RegistroAcceso? adentro = GestorArchivos.Accesos.FirstOrDefault(acceso => acceso.EstaAdentro && acceso.CodigoEvento == evento.Codigo);
        return adentro == null ? "" : "No se puede eliminar: hay vehículos del evento adentro (por ejemplo " + adentro.Placa + ").";
    }

    public static int ContarPasesDe(EventoCampus evento)
    {
        ArgumentNullException.ThrowIfNull(evento);
        return GestorArchivos.Pases.Count(pase => pase.CodigoEvento == evento.Codigo);
    }

    /// <summary>El evento y sus pases se eliminan juntos: nunca queda uno sin el otro.</summary>
    public static ResultadoValidacion EliminarEvento(EventoCampus evento, string nombreUsuario)
    {
        ArgumentNullException.ThrowIfNull(evento);

        string motivo = ObtenerMotivoParaNoEliminar(evento);
        if (motivo != "")
        {
            return new ResultadoValidacion(motivo);
        }

        int numeroPases = ContarPasesDe(evento);
        bool guardado = GestorArchivos.Transaccion(ArchivosDeDatos.Eventos | ArchivosDeDatos.Pases | ArchivosDeDatos.Bitacora, () =>
        {
            GestorArchivos.Pases.RemoveAll(pase => pase.CodigoEvento == evento.Codigo);
            GestorArchivos.Eventos.Remove(evento);
            GestorArchivos.AnotarEnBitacora(nombreUsuario, "Eliminar evento", evento.Codigo + " y " + numeroPases + " pases");
        });
        return guardado ? ResultadoValidacion.Correcto : new ResultadoValidacion(MensajeNoSeGuardo);
    }

    // ----------------------------- Pases -----------------------------

    public static ResultadoValidacion EmitirPase(DatosPase datos, EventoCampus? evento, string nombreUsuario, DateTime ahora, out PaseVisitante? emitido)
    {
        ArgumentNullException.ThrowIfNull(datos);

        // === [RÚBRICA 1] VALIDACIONES DE CAMPO ===
        emitido = null;
        string nombre = datos.Nombre.Trim();
        string correo = datos.Correo.Trim().ToLowerInvariant();
        string placa = datos.Placa.Trim().ToUpperInvariant();

        if (evento == null)
        {
            return new ResultadoValidacion("Selecciona el evento para el que se emite el pase.");
        }
        if (evento.AdmitePases(ahora) == false)
        {
            return new ResultadoValidacion("No se pueden emitir pases: el evento ya terminó o está desactivado.");
        }
        if (nombre == "")
        {
            return new ResultadoValidacion("El nombre del visitante es obligatorio.", CampoFormulario.Nombre);
        }
        if (Validaciones.EsNombreValido(nombre) == false)
        {
            return new ResultadoValidacion("El nombre del visitante solo puede llevar letras.", CampoFormulario.Nombre);
        }
        if (Validaciones.EsPlacaValida(placa) == false)
        {
            return new ResultadoValidacion(Validaciones.MensajePlacaInvalida, CampoFormulario.Placa);
        }
        if (correo != "" && Validaciones.EsCorreoValido(correo) == false)
        {
            return new ResultadoValidacion("El correo del visitante no tiene un formato válido.", CampoFormulario.Correo);
        }
        if (ReglasEventos.PlacaYaTienePase(evento, placa, ahora))
        {
            return new ResultadoValidacion("La placa " + placa + " ya tiene un pase activo para este evento.", CampoFormulario.Placa);
        }

        string motivoDeBloqueo = ServicioCaseta.ObtenerMotivoDeBloqueo("", correo, placa, ahora);
        if (motivoDeBloqueo != "")
        {
            return new ResultadoValidacion(motivoDeBloqueo, CampoFormulario.Placa);
        }

        int pasesActivos = CalculadoraAforo.ContarPasesActivos(evento, GestorArchivos.Pases, ahora);
        if (pasesActivos >= evento.MaxVisitantes)
        {
            return new ResultadoValidacion("Cupo de visitantes lleno: " + pasesActivos + " de " + evento.MaxVisitantes + " pases activos.");
        }

        PaseVisitante pase = new PaseVisitante
        {
            Id = GestorArchivos.SiguienteIdPase(),
            CodigoUnico = CodigoPaseVisitante.CrearCodigoUnico(),
            CodigoEvento = evento.Codigo,
            NombreVisitante = nombre,
            Correo = correo,
            Placa = placa,
            FechaEmision = ahora,
            FechaExpiracion = ReglasEventos.CalcularExpiracionPase(evento, ahora)
        };

        // === [RÚBRICA 2] ALMACENAMIENTO EN ARCHIVOS DE TEXTO (.JSON) ===
        bool guardado = GestorArchivos.Transaccion(ArchivosDeDatos.Pases | ArchivosDeDatos.Bitacora, () =>
        {
            GestorArchivos.Pases.Add(pase);
            GestorArchivos.AnotarEnBitacora(nombreUsuario, "Emitir pase", pase.Id + " · " + pase.Placa + " · " + pase.CodigoEvento);
        });
        if (guardado == false)
        {
            return new ResultadoValidacion(MensajeNoSeGuardo);
        }

        emitido = pase;
        return ResultadoValidacion.Correcto;
    }

    public static ResultadoValidacion RevocarPase(PaseVisitante pase, string nombreUsuario)
    {
        ArgumentNullException.ThrowIfNull(pase);

        if (pase.Revocado)
        {
            return new ResultadoValidacion("El pase " + pase.Id + " ya estaba revocado.");
        }

        bool guardado = GestorArchivos.Transaccion(ArchivosDeDatos.Pases | ArchivosDeDatos.Bitacora, () =>
        {
            pase.Revocado = true;
            GestorArchivos.AnotarEnBitacora(nombreUsuario, "Revocar pase", pase.Id + " · " + pase.Placa);
        });
        return guardado ? ResultadoValidacion.Correcto : new ResultadoValidacion(MensajeNoSeGuardo);
    }

    public static ResultadoValidacion EliminarPase(PaseVisitante pase, string nombreUsuario)
    {
        ArgumentNullException.ThrowIfNull(pase);

        if (GestorArchivos.Accesos.Any(acceso => acceso.EstaAdentro && acceso.IdPase == pase.Id))
        {
            return new ResultadoValidacion("No se puede eliminar: el visitante con este pase sigue adentro.");
        }

        bool guardado = GestorArchivos.Transaccion(ArchivosDeDatos.Pases | ArchivosDeDatos.Bitacora, () =>
        {
            GestorArchivos.Pases.Remove(pase);
            GestorArchivos.AnotarEnBitacora(nombreUsuario, "Eliminar pase", pase.Id + " · " + pase.Placa);
        });
        return guardado ? ResultadoValidacion.Correcto : new ResultadoValidacion(MensajeNoSeGuardo);
    }
}
