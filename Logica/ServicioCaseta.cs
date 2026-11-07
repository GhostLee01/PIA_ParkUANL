namespace PIA_ParkUANL;

/// <summary>Lo que captura el operador en la entrada manual.</summary>
public sealed record DatosEntradaManual(string Categoria, string Nombre, string Matricula, string Correo, string Placa, string CodigoEvento);

/// <summary>Datos conocidos de una persona, para llenar el formulario de entrada.</summary>
public sealed record DatosDePersona(string Categoria, string Nombre, string Matricula, string Correo, string Placa, string Origen);

/// <summary>Resultado de leer un QR: el registro listo para confirmar, o el motivo del rechazo.</summary>
public sealed record EntradaPorQr(RegistroAcceso? Registro, EventoCampus? EventoConReserva, string Error);

// =====================================================================================
//  SERVICIOCASETA.CS
//  Reglas de la caseta: quién puede entrar, qué cajón le toca y cómo se registra la salida.
//  No conoce la pantalla: los métodos regresan "" si todo salió bien, o el mensaje de error.
// =====================================================================================
public static class ServicioCaseta
{
    public const int MaximoMovimientosRecientes = 50;
    public const string CajonSinNumero = "S/N";

    private const string MensajeNoSeGuardo = "No se pudo guardar el movimiento. Intenta de nuevo.";

    // ----------------------------- Reglas de acceso -----------------------------

    /// <summary>
    /// Ningún método de entrada debe servir para saltarse los controles: se rechazan las cuentas
    /// desactivadas y las placas con un pase revocado de un evento en curso.
    /// </summary>
    public static string ObtenerMotivoDeBloqueo(string matricula, string correo, string placa, DateTime ahora)
    {
        // === [RÚBRICA 1] VALIDACIONES DE CAMPO ===
        foreach (Usuario usuario in GestorArchivos.Usuarios)
        {
            bool esMismaMatricula = matricula != "" && usuario.Matricula == matricula;
            bool esMismoCorreo = correo != "" && usuario.Correo.ToLowerInvariant() == correo;
            bool esMismaPlaca = usuario.ObtenerPlacas().Any(placaDeLaCuenta => Validaciones.EsMismaPlaca(placaDeLaCuenta, placa));

            if ((esMismaMatricula || esMismoCorreo || esMismaPlaca) && usuario.Activo == false)
            {
                return "La cuenta de " + usuario.NombreCompleto + " está desactivada. No se permite el acceso.";
            }
        }

        bool tienePaseActivo = false;
        PaseVisitante? paseRevocado = null;
        foreach (PaseVisitante pase in GestorArchivos.Pases)
        {
            if (Validaciones.EsMismaPlaca(pase.Placa, placa))
            {
                if (pase.ObtenerEstado(ahora) == EstadosDePase.Activo)
                {
                    tienePaseActivo = true;
                }
                else if (pase.Revocado && EventoNoHaTerminado(pase.CodigoEvento, ahora))
                {
                    paseRevocado = pase;
                }
            }
        }

        if (paseRevocado != null && tienePaseActivo == false)
        {
            return "La placa " + placa + " tiene el pase " + paseRevocado.Id + " REVOCADO. No se permite el acceso.";
        }
        return "";
    }

    private static bool EventoNoHaTerminado(string codigoEvento, DateTime ahora)
    {
        EventoCampus? evento = GestorArchivos.Eventos.FirstOrDefault(candidato => candidato.Codigo == codigoEvento);
        return evento != null && evento.AdmitePases(ahora);
    }

    /// <summary>Bloqueo de doble entrada: la misma placa, matrícula o correo no puede estar adentro dos veces.</summary>
    public static string BuscarDobleEntrada(string placa, string matricula, string correo = "")
    {
        foreach (RegistroAcceso acceso in GestorArchivos.Accesos)
        {
            if (acceso.EstaAdentro == false)
            {
                continue;
            }
            if (Validaciones.EsMismaPlaca(acceso.Placa, placa))
            {
                return "La placa " + placa + " ya está adentro de " + acceso.Estacionamiento + " (cajón " + acceso.Cajon + ").";
            }
            if (matricula != "" && acceso.Matricula == matricula)
            {
                return "La matrícula " + matricula + " ya tiene un vehículo adentro (placa " + acceso.Placa + ").";
            }
            if (correo != "" && acceso.Correo.ToLowerInvariant() == correo.ToLowerInvariant())
            {
                return "El correo " + correo + " ya tiene un vehículo adentro (placa " + acceso.Placa + ").";
            }
        }
        return "";
    }

    // ----------------------------- Entrada manual -----------------------------

    /// <summary>
    /// Busca los datos a partir de la matrícula o la placa en dos lugares:
    ///   1) usuarios.json → Comunidad UANL registrada (Alumnos y Docentes con cuenta).
    ///   2) accesos.json  → la visita más reciente con esa matrícula o placa.
    /// </summary>
    public static DatosDePersona? BuscarDatosDePersona(string matricula, string placa)
    {
        ArgumentNullException.ThrowIfNull(matricula);
        ArgumentNullException.ThrowIfNull(placa);

        foreach (Usuario usuario in GestorArchivos.Usuarios.Where(cuenta => cuenta.Rol == Roles.Comunidad))
        {
            string? placaEncontrada = usuario.ObtenerPlacas().FirstOrDefault(placaDeLaCuenta => placa != "" && Validaciones.EsMismaPlaca(placaDeLaCuenta, placa));
            if ((matricula != "" && usuario.Matricula == matricula) || placaEncontrada != null)
            {
                return new DatosDePersona(usuario.Categoria, usuario.NombreCompleto, usuario.Matricula, usuario.Correo, placaEncontrada ?? usuario.Placa,
                    "Datos cargados del registro de la Comunidad UANL: " + usuario.NombreCompleto + ". Revisa la placa y registra la entrada.");
            }
        }

        // LINQ: la entrada más reciente con esa matrícula o placa.
        RegistroAcceso? ultimaVisita = GestorArchivos.Accesos
            .Where(acceso => (matricula != "" && acceso.Matricula == matricula) || (placa != "" && Validaciones.EsMismaPlaca(acceso.Placa, placa)))
            .OrderByDescending(acceso => acceso.FechaEntrada)
            .FirstOrDefault();

        if (ultimaVisita == null)
        {
            return null;
        }
        return new DatosDePersona(ultimaVisita.Categoria, ultimaVisita.Nombre, ultimaVisita.Matricula, ultimaVisita.Correo, ultimaVisita.Placa,
            "Datos cargados de su última visita (" + ultimaVisita.FechaEntrada.ToString("dd/MM/yyyy") + "): " + ultimaVisita.Nombre + ". Revisa la placa y registra la entrada.");
    }

    /// <summary>Valida la captura y, si todo está bien, registra la entrada. La entrada manual siempre usa un cajón general.</summary>
    public static ResultadoValidacion RegistrarEntradaManual(DatosEntradaManual datos, string estacionamiento, string nombreOperador, DateTime ahora, out RegistroAcceso? registro)
    {
        ArgumentNullException.ThrowIfNull(datos);

        // === [RÚBRICA 1] VALIDACIONES DE CAMPO ===
        registro = null;
        string nombre = datos.Nombre.Trim();
        string matricula = datos.Matricula.Trim();
        string correo = datos.Correo.Trim().ToLowerInvariant();
        string placa = datos.Placa.Trim().ToUpperInvariant();

        if (nombre == "")
        {
            return new ResultadoValidacion("El nombre es obligatorio.", CampoFormulario.Nombre);
        }
        if (nombre.Length > Validaciones.LongitudMaximaNombre)
        {
            return new ResultadoValidacion("El nombre admite hasta " + Validaciones.LongitudMaximaNombre + " caracteres.", CampoFormulario.Nombre);
        }
        if (Validaciones.EsNombreValido(nombre) == false)
        {
            return new ResultadoValidacion("El nombre solo puede llevar letras.", CampoFormulario.Nombre);
        }
        if (placa == "")
        {
            return new ResultadoValidacion("La placa es obligatoria.", CampoFormulario.Placa);
        }
        if (Validaciones.EsPlacaValida(placa) == false)
        {
            return new ResultadoValidacion(Validaciones.MensajePlacaInvalida, CampoFormulario.Placa);
        }

        ResultadoValidacion categoria = ValidarDatosDeCategoria(datos.Categoria, ref matricula, correo);
        if (categoria.EsCorrecto == false)
        {
            return categoria;
        }

        ResultadoValidacion identidad = ValidarIdentidad(datos.Categoria, nombre, matricula, correo);
        if (identidad.EsCorrecto == false)
        {
            return identidad;
        }

        string motivoDeBloqueo = ObtenerMotivoDeBloqueo(matricula, correo, placa, ahora);
        if (motivoDeBloqueo != "")
        {
            return new ResultadoValidacion(motivoDeBloqueo);
        }

        string codigoEvento = datos.CodigoEvento == OpcionesDeFiltro.NingunEvento ? "" : datos.CodigoEvento;
        if (codigoEvento != "" && GestorArchivos.Eventos.Any(evento => evento.Codigo == codigoEvento && evento.EstaVigente(ahora)) == false)
        {
            return new ResultadoValidacion("El evento " + codigoEvento + " ya no está vigente.");
        }

        RegistroAcceso nuevo = new RegistroAcceso
        {
            Nombre = nombre,
            Placa = placa,
            Matricula = matricula,
            Correo = correo,
            Categoria = datos.Categoria,
            Metodo = MetodosDeEntrada.Manual,
            CodigoEvento = codigoEvento
        };

        string error = RegistrarEntrada(nuevo, null, estacionamiento, nombreOperador, ahora);
        if (error != "")
        {
            return new ResultadoValidacion(error);
        }
        registro = nuevo;
        return ResultadoValidacion.Correcto;
    }

    private static ResultadoValidacion ValidarDatosDeCategoria(string categoria, ref string matricula, string correo)
    {
        if (categoria == Categorias.Alumno || categoria == Categorias.Docente)
        {
            if (matricula == "" && correo == "")
            {
                return new ResultadoValidacion("Para Alumno o Docente escribe la matrícula (7 dígitos) o el correo @uanl.edu.mx.", CampoFormulario.Matricula);
            }
            if (matricula != "" && Validaciones.EsMatriculaValida(matricula) == false)
            {
                return new ResultadoValidacion("La matrícula debe tener exactamente 7 dígitos numéricos.", CampoFormulario.Matricula);
            }
            // Un correo que ya está registrado se acepta tal cual: puede venir de la última visita, con un formato anterior.
            if (correo != "" && Validaciones.EsCorreoUanl(correo) == false && ServicioCuentas.EsCorreoRegistrado(correo) == false)
            {
                return new ResultadoValidacion(Validaciones.MensajeCorreoUanlInvalido, CampoFormulario.Correo);
            }
            return ResultadoValidacion.Correcto;
        }

        if (categoria != Categorias.Visitante)
        {
            return new ResultadoValidacion("Elige una categoría válida.", CampoFormulario.Categoria);
        }

        // Visitante: el correo es opcional, pero si lo escriben debe tener formato válido.
        if (correo != "" && Validaciones.EsCorreoValido(correo) == false)
        {
            return new ResultadoValidacion("El correo del visitante no tiene un formato válido.", CampoFormulario.Correo);
        }

        // Los visitantes no tienen matrícula: lo que se haya escrito no se guarda.
        matricula = "";
        return ResultadoValidacion.Correcto;
    }

    /// <summary>Si la matrícula o el correo son de una cuenta, el nombre y la categoría deben ser los de esa persona.</summary>
    private static ResultadoValidacion ValidarIdentidad(string categoria, string nombre, string matricula, string correo)
    {
        Usuario? porMatricula = matricula == "" ? null : GestorArchivos.Usuarios.FirstOrDefault(usuario => usuario.Matricula == matricula);
        Usuario? porCorreo = correo == "" ? null : GestorArchivos.Usuarios.FirstOrDefault(usuario => usuario.Correo.ToLowerInvariant() == correo);

        if (porMatricula != null && porCorreo != null && porMatricula != porCorreo)
        {
            return new ResultadoValidacion("La matrícula y el correo son de dos cuentas distintas.", CampoFormulario.Correo);
        }

        Usuario? cuenta = porMatricula ?? porCorreo;
        if (cuenta != null && cuenta.Categoria != "" && (cuenta.Categoria != categoria || ServicioCuentas.EsMismaPersona(cuenta.NombreCompleto, nombre) == false))
        {
            return new ResultadoValidacion("Esa cuenta es de " + cuenta.NombreCompleto + " (" + cuenta.Categoria + "). Presiona Buscar para cargar sus datos.", CampoFormulario.Nombre);
        }
        return ResultadoValidacion.Correcto;
    }

    // ----------------------------- Entrada con QR -----------------------------

    /// <summary>Lee el texto del QR y regresa el registro listo para que el operador confirme el vehículo.</summary>
    public static EntradaPorQr PrepararEntradaQr(string codigo, DateTime ahora)
    {
        ArgumentNullException.ThrowIfNull(codigo);

        // === [RÚBRICA 1] VALIDACIONES DE CAMPO ===
        string codigoLimpio = codigo.Trim();
        if (codigoLimpio == "")
        {
            return new EntradaPorQr(null, null, "Pega el texto del código QR.");
        }
        if (codigoLimpio.Length > GeneradorQR.MaximoCaracteres)
        {
            return new EntradaPorQr(null, null, "Código QR no reconocido: el texto es demasiado largo.");
        }

        RegistroAcceso nuevo;
        EventoCampus? eventoConReserva = null;

        if (codigoLimpio.StartsWith(TokenUanl.Prefijo))
        {
            // Token de 30 s de un Alumno o Docente.
            string error = TokenUanl.Validar(codigoLimpio, ahora, out Usuario? usuario, out string placa);
            if (error != "" || usuario == null)
            {
                return new EntradaPorQr(null, null, error);
            }

            nuevo = new RegistroAcceso
            {
                Nombre = usuario.NombreCompleto,
                Placa = placa,
                Matricula = usuario.Matricula,
                Correo = usuario.Correo,
                Categoria = usuario.Categoria,
                Metodo = MetodosDeEntrada.QrUanl
            };
        }
        else if (codigoLimpio.StartsWith(CodigoPaseVisitante.Prefijo))
        {
            string error = CodigoPaseVisitante.Validar(codigoLimpio, ahora, out PaseVisitante? pase, out eventoConReserva);
            if (error != "" || pase == null)
            {
                return new EntradaPorQr(null, null, error);
            }

            nuevo = new RegistroAcceso
            {
                Nombre = pase.NombreVisitante,
                Placa = pase.Placa,
                Correo = pase.Correo,
                Categoria = Categorias.Visitante,
                CodigoEvento = pase.CodigoEvento,
                IdPase = pase.Id,
                Metodo = MetodosDeEntrada.QrVisitante
            };
        }
        else
        {
            return new EntradaPorQr(null, null, "Código QR no reconocido: debe ser un token UANL o un pase de visitante.");
        }

        string motivoDeBloqueo = ObtenerMotivoDeBloqueo(nuevo.Matricula, nuevo.Correo.ToLowerInvariant(), nuevo.Placa, ahora);
        if (motivoDeBloqueo != "")
        {
            return new EntradaPorQr(null, null, motivoDeBloqueo);
        }
        return new EntradaPorQr(nuevo, eventoConReserva, "");
    }

    /// <summary>Registra la entrada ya confirmada por el operador. El token queda usado: no sirve para otra entrada.</summary>
    public static string RegistrarEntradaQr(EntradaPorQr entrada, string codigo, string estacionamiento, string nombreOperador, DateTime ahora)
    {
        ArgumentNullException.ThrowIfNull(entrada);
        ArgumentNullException.ThrowIfNull(codigo);

        if (entrada.Registro == null)
        {
            return entrada.Error;
        }

        string error = RegistrarEntrada(entrada.Registro, entrada.EventoConReserva, estacionamiento, nombreOperador, ahora);
        if (error == "" && codigo.Trim().StartsWith(TokenUanl.Prefijo))
        {
            TokenUanl.MarcarUsado(codigo, ahora);
        }
        return error;
    }

    // ----------------------------- Registro común de entrada -----------------------------

    /// <summary>
    /// Revisa doble entrada y cupo, asigna cajón y guarda en accesos.json.
    /// Si "eventoConReserva" no es null, intenta usar un cajón reservado del evento en FACPYA.
    /// </summary>
    public static string RegistrarEntrada(RegistroAcceso nuevo, EventoCampus? eventoConReserva, string estacionamiento, string nombreOperador, DateTime ahora)
    {
        ArgumentNullException.ThrowIfNull(nuevo);

        // === [RÚBRICA 1] VALIDACIONES DE CAMPO ===
        if (Estacionamientos.Todos.Contains(estacionamiento) == false)
        {
            return "Elige un estacionamiento válido.";
        }

        string errorDobleEntrada = BuscarDobleEntrada(nuevo.Placa, nuevo.Matricula, nuevo.Correo);
        if (errorDobleEntrada != "")
        {
            return errorDobleEntrada;
        }

        bool usaReservado = HayCajonReservadoLibre(eventoConReserva, estacionamiento, ahora);

        // Cupo lleno: sin cajón reservado, se necesita al menos un lugar general disponible.
        ResumenAforo resumen = CalculadoraAforo.CalcularResumen(estacionamiento, GestorArchivos.Accesos, GestorArchivos.Eventos, ahora);
        if (usaReservado == false && resumen.LugaresDisponibles <= 0)
        {
            return "Cupo lleno: " + estacionamiento + " no tiene lugares disponibles (" + resumen.PorcentajeOcupacion + " %).";
        }

        string cajon = usaReservado ? AsignarCajonReservado() : AsignarCajonGeneral(estacionamiento);
        if (cajon == CajonSinNumero)
        {
            return "Cupo lleno: " + estacionamiento + " no tiene cajones libres.";
        }

        // === [RÚBRICA 2] ALMACENAMIENTO EN ARCHIVOS DE TEXTO (.JSON) ===
        bool guardado = GestorArchivos.Transaccion(ArchivosDeDatos.Accesos, () =>
        {
            nuevo.Id = GestorArchivos.SiguienteIdAcceso();
            nuevo.Estacionamiento = estacionamiento;
            nuevo.UsaCajonReservado = usaReservado;
            nuevo.Cajon = cajon;
            nuevo.FechaEntrada = ahora;
            nuevo.FechaSalida = null;
            nuevo.Operador = nombreOperador;
            GestorArchivos.Accesos.Add(nuevo);
        });
        return guardado ? "" : MensajeNoSeGuardo;
    }

    /// <summary>Solo visitantes con pase, en FACPYA, si quedan reservados libres de su evento.</summary>
    private static bool HayCajonReservadoLibre(EventoCampus? evento, string estacionamiento, DateTime ahora)
    {
        if (evento == null || estacionamiento != Estacionamientos.Facpya || evento.LugaresReservadosFacpya <= 0)
        {
            return false;
        }
        int ocupados = CalculadoraAforo.ContarReservadosOcupados(Estacionamientos.Facpya, evento.Codigo, GestorArchivos.Accesos, GestorArchivos.Eventos, ahora);
        return ocupados < evento.LugaresReservadosFacpya;
    }

    /// <summary>Primer cajón general libre: F-001...F-500 o E-0001...E-1000. Regresa CajonSinNumero si no queda ninguno.</summary>
    public static string AsignarCajonGeneral(string estacionamiento)
    {
        HashSet<string> cajonesOcupados = new HashSet<string>();
        foreach (RegistroAcceso acceso in GestorArchivos.Accesos)
        {
            if (acceso.EstaAdentro && acceso.Estacionamiento == estacionamiento)
            {
                cajonesOcupados.Add(acceso.Cajon);
            }
        }

        int capacidad = CalculadoraAforo.ObtenerCapacidadTotal(estacionamiento);
        for (int numero = 1; numero <= capacidad; numero++)
        {
            string cajon = estacionamiento == Estacionamientos.Facpya ? "F-" + numero.ToString("000") : "E-" + numero.ToString("0000");
            if (cajonesOcupados.Contains(cajon) == false)
            {
                return cajon;
            }
        }
        return CajonSinNumero;
    }

    /// <summary>Primer cajón reservado libre en FACPYA: F-R01, F-R02...</summary>
    private static string AsignarCajonReservado()
    {
        HashSet<string> cajonesOcupados = new HashSet<string>();
        foreach (RegistroAcceso acceso in GestorArchivos.Accesos)
        {
            if (acceso.EstaAdentro && acceso.UsaCajonReservado)
            {
                cajonesOcupados.Add(acceso.Cajon);
            }
        }

        int numero = 1;
        while (cajonesOcupados.Contains("F-R" + numero.ToString("00")))
        {
            numero++;
        }
        return "F-R" + numero.ToString("00");
    }

    // ----------------------------- Salida -----------------------------

    /// <summary>Busca el vehículo que está adentro con esa placa o matrícula.</summary>
    public static RegistroAcceso? BuscarVehiculoAdentro(string placaOMatricula)
    {
        ArgumentNullException.ThrowIfNull(placaOMatricula);

        foreach (RegistroAcceso acceso in GestorArchivos.Accesos)
        {
            bool esMismaPlaca = Validaciones.EsMismaPlaca(acceso.Placa, placaOMatricula);
            bool esMismaMatricula = acceso.Matricula != "" && acceso.Matricula == placaOMatricula;
            if (acceso.EstaAdentro && (esMismaPlaca || esMismaMatricula))
            {
                return acceso;
            }
        }
        return null;
    }

    public static bool RegistrarSalida(RegistroAcceso registro, DateTime ahora, string nombreOperador = "")
    {
        ArgumentNullException.ThrowIfNull(registro);

        if (registro.EstaAdentro == false)
        {
            return false;
        }

        // === [RÚBRICA 2] ALMACENAMIENTO EN ARCHIVOS DE TEXTO (.JSON) ===
        return GestorArchivos.Transaccion(ArchivosDeDatos.Accesos, () =>
        {
            registro.FechaSalida = ahora;
            registro.OperadorSalida = nombreOperador;
        });
    }
}
