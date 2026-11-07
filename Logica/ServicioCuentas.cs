namespace PIA_ParkUANL;

/// <summary>Lo que escribe una persona al crear su cuenta.</summary>
public sealed record DatosDeRegistro(string Categoria, string Nombre, string Correo, string Matricula, string Placa, string Contrasena, string Confirmacion);

// =====================================================================================
//  SERVICIOCUENTAS.CS
//  Reglas de las cuentas: inicio de sesión, contraseñas y vehículos.
//  Los métodos que validan regresan "" si todo salió bien, o el mensaje de error.
// =====================================================================================
public static class ServicioCuentas
{
    public const int MaximoVehiculosPorCuenta = 3;
    public const int HorasDeVigenciaContrasenaTemporal = 24;

    private const string MensajeCredencialesIncorrectas = "Usuario o contraseña incorrectos.";
    private const string MensajeNoSeGuardo = "No se pudo guardar el cambio. Intenta de nuevo.";

    // Se verifica también cuando la cuenta no existe: si no, la respuesta instantánea revelaría qué cuentas son reales.
    private static readonly Lazy<string> HashFicticio = new Lazy<string>(() => SeguridadContrasenas.CalcularHash(Guid.NewGuid().ToString()));

    // ----------------------------- Inicio de sesión -----------------------------

    public static string IniciarSesion(string identificador, string contrasena, DateTime ahora, out Usuario? usuarioAutenticado)
    {
        ArgumentNullException.ThrowIfNull(identificador);
        ArgumentNullException.ThrowIfNull(contrasena);

        usuarioAutenticado = null;
        string identificadorMinusculas = identificador.Trim().ToLowerInvariant();
        Usuario? cuenta = BuscarPorUsuarioOCorreo(identificadorMinusculas);

        // El bloqueo se lleva por cuenta: así no se esquiva alternando el usuario y el correo.
        string claveDeIntentos = cuenta?.NombreUsuario.ToLowerInvariant() ?? identificadorMinusculas;

        int minutosRestantes = ControlDeIntentos.ObtenerMinutosRestantes(claveDeIntentos, ahora);
        if (minutosRestantes > 0)
        {
            return "Demasiados intentos fallidos. Espera " + minutosRestantes + " min y vuelve a intentar.";
        }

        // Mismo mensaje y mismo tiempo si la cuenta no existe o la contraseña no coincide.
        bool contrasenaCorrecta = SeguridadContrasenas.VerificarContrasena(contrasena, cuenta?.ContrasenaHash ?? HashFicticio.Value);
        if (cuenta == null || contrasenaCorrecta == false)
        {
            bool seBloqueo = ControlDeIntentos.RegistrarFallo(claveDeIntentos, ahora);
            if (seBloqueo && cuenta != null)
            {
                GestorArchivos.RegistrarEnBitacora("sistema", "Cuenta bloqueada por intentos", cuenta.NombreUsuario);
            }
            return MensajeCredencialesIncorrectas;
        }

        ControlDeIntentos.Reiniciar(claveDeIntentos);
        if (cuenta.Activo == false)
        {
            return "Tu cuenta está desactivada. Contacta al administrador.";
        }
        if (cuenta.TemporalVenceEl != null && cuenta.TemporalVenceEl < ahora)
        {
            return "Tu contraseña temporal venció. Pide al administrador una nueva.";
        }
        if (Roles.EsValido(cuenta.Rol) == false)
        {
            return "Tu cuenta no tiene un rol válido. Contacta al administrador.";
        }

        if (SeguridadContrasenas.EsHashDeVersionAnterior(cuenta.ContrasenaHash))
        {
            ActualizarHashDeVersionAnterior(cuenta, contrasena);
        }

        GestorArchivos.RegistrarEnBitacora(cuenta.NombreUsuario, "Inicio de sesión", cuenta.Rol);
        usuarioAutenticado = cuenta;
        return "";
    }

    private static Usuario? BuscarPorUsuarioOCorreo(string identificadorMinusculas)
    {
        return GestorArchivos.Usuarios.FirstOrDefault(usuario => usuario.NombreUsuario.ToLowerInvariant() == identificadorMinusculas
                                                                 || usuario.Correo.ToLowerInvariant() == identificadorMinusculas);
    }

    /// <summary>Pasa la cuenta al hash con sal y, si aún usa la contraseña de fábrica (admin123...), obliga a cambiarla.</summary>
    private static void ActualizarHashDeVersionAnterior(Usuario cuenta, string contrasena)
    {
        GestorArchivos.Transaccion(ArchivosDeDatos.Usuarios, () =>
        {
            cuenta.ContrasenaHash = SeguridadContrasenas.CalcularHash(contrasena);
            if (contrasena == cuenta.NombreUsuario + "123")
            {
                cuenta.DebeCambiarContrasena = true;
            }
        });
    }

    // ----------------------------- Crear cuenta -----------------------------

    /// <summary>Solo Alumnos y Docentes con correo universitario. Toda cuenta nueva nace con el rol Comunidad.</summary>
    public static ResultadoValidacion RegistrarCuenta(DatosDeRegistro datos, out string correoRegistrado)
    {
        ArgumentNullException.ThrowIfNull(datos);

        // === [RÚBRICA 1] VALIDACIONES DE CAMPO ===
        correoRegistrado = "";
        string nombre = datos.Nombre.Trim();
        string correo = datos.Correo.Trim().ToLowerInvariant();
        string matricula = datos.Matricula.Trim();
        string placa = datos.Placa.Trim().ToUpperInvariant();

        if (datos.Categoria != Categorias.Alumno && datos.Categoria != Categorias.Docente)
        {
            return new ResultadoValidacion("Elige si eres alumno o docente.", CampoFormulario.Categoria);
        }
        if (Validaciones.EsNombreValido(nombre) == false || nombre.Contains(' ') == false)
        {
            return new ResultadoValidacion("Escribe tu nombre completo (nombre y apellido), solo con letras.", CampoFormulario.Nombre);
        }
        if (correo == "")
        {
            return new ResultadoValidacion("Escribe tu correo universitario.", CampoFormulario.Correo);
        }
        if (Validaciones.EsCorreoUanl(correo) == false)
        {
            return new ResultadoValidacion(Validaciones.MensajeCorreoUanlInvalido, CampoFormulario.Correo);
        }
        if (Validaciones.EsMatriculaValida(matricula) == false)
        {
            return new ResultadoValidacion("La matrícula o número de empleado debe tener exactamente 7 dígitos.", CampoFormulario.Matricula);
        }
        if (Validaciones.EsPlacaValida(placa) == false)
        {
            return new ResultadoValidacion(Validaciones.MensajePlacaInvalida, CampoFormulario.Placa);
        }
        if (Validaciones.EsContrasenaSegura(datos.Contrasena, correo.Split('@')[0]) == false)
        {
            return new ResultadoValidacion(Validaciones.MensajeContrasenaInvalida, CampoFormulario.Contrasena);
        }
        if (datos.Contrasena != datos.Confirmacion)
        {
            return new ResultadoValidacion("Las contraseñas no coinciden.", CampoFormulario.Confirmacion);
        }

        // No se permiten cuentas repetidas: mismo correo, matrícula o placa.
        foreach (Usuario usuario in GestorArchivos.Usuarios)
        {
            if (usuario.Correo.ToLowerInvariant() == correo || usuario.NombreUsuario.ToLowerInvariant() == correo)
            {
                return new ResultadoValidacion("Ya existe una cuenta con ese correo.", CampoFormulario.Correo);
            }
            if (usuario.Matricula == matricula)
            {
                return new ResultadoValidacion("Ya existe una cuenta con esa matrícula o número de empleado.", CampoFormulario.Matricula);
            }
        }
        if (BuscarDuenoDePlaca(placa) != null)
        {
            return new ResultadoValidacion("Esa placa ya está registrada en otra cuenta.", CampoFormulario.Placa);
        }

        // Una matrícula con historial de otra persona no se puede reclamar para una cuenta nueva.
        RegistroAcceso? visitaConEsaMatricula = GestorArchivos.Accesos.LastOrDefault(acceso => acceso.Matricula == matricula);
        if (visitaConEsaMatricula != null && EsMismaPersona(visitaConEsaMatricula.Nombre, nombre) == false)
        {
            return new ResultadoValidacion("Esa matrícula ya aparece en los accesos con otro nombre. Acude a la caseta para aclararlo.", CampoFormulario.Matricula);
        }

        Usuario nuevo = new Usuario
        {
            NombreUsuario = correo,   // en las cuentas nuevas, el usuario es el mismo correo
            ContrasenaHash = SeguridadContrasenas.CalcularHash(datos.Contrasena),
            NombreCompleto = nombre,
            Correo = correo,
            Matricula = matricula,
            Placa = placa,
            Rol = Roles.Comunidad,    // siempre Comunidad: solo el Administrador puede cambiarlo
            Categoria = datos.Categoria
        };

        // === [RÚBRICA 2] ALMACENAMIENTO EN ARCHIVOS DE TEXTO (.JSON) ===
        bool guardado = GestorArchivos.Transaccion(ArchivosDeDatos.Usuarios | ArchivosDeDatos.Bitacora, () =>
        {
            GestorArchivos.Usuarios.Add(nuevo);
            GestorArchivos.AnotarEnBitacora(nuevo.NombreUsuario, "Crear cuenta", nuevo.Categoria + " · " + nuevo.Matricula);
        });
        if (guardado == false)
        {
            return new ResultadoValidacion(MensajeNoSeGuardo);
        }

        correoRegistrado = correo;
        return ResultadoValidacion.Correcto;
    }

    /// <summary>Compara dos nombres sin importar mayúsculas, acentos ni espacios de más.</summary>
    public static bool EsMismaPersona(string nombreUno, string nombreDos)
    {
        return string.Join(" ", PrepararParaComparar(nombreUno).Split(' ', StringSplitOptions.RemoveEmptyEntries))
            == string.Join(" ", PrepararParaComparar(nombreDos).Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    // ----------------------------- Contraseñas -----------------------------

    public static string CambiarContrasena(Usuario usuario, string contrasenaActual, string contrasenaNueva, string confirmacion)
    {
        ArgumentNullException.ThrowIfNull(usuario);
        ArgumentNullException.ThrowIfNull(contrasenaActual);
        ArgumentNullException.ThrowIfNull(contrasenaNueva);
        ArgumentNullException.ThrowIfNull(confirmacion);

        // === [RÚBRICA 1] VALIDACIONES DE CAMPO ===
        DateTime ahora = DateTime.Now;
        string claveDeIntentos = usuario.NombreUsuario.ToLowerInvariant();

        int minutosRestantes = ControlDeIntentos.ObtenerMinutosRestantes(claveDeIntentos, ahora);
        if (minutosRestantes > 0)
        {
            return "Demasiados intentos fallidos. Espera " + minutosRestantes + " min y vuelve a intentar.";
        }
        if (SeguridadContrasenas.VerificarContrasena(contrasenaActual, usuario.ContrasenaHash) == false)
        {
            ControlDeIntentos.RegistrarFallo(claveDeIntentos, ahora);
            return "La contraseña actual no es correcta.";
        }
        if (Validaciones.EsContrasenaSegura(contrasenaNueva, usuario.NombreUsuario.Split('@')[0]) == false)
        {
            return Validaciones.MensajeContrasenaInvalida;
        }
        if (contrasenaNueva != confirmacion)
        {
            return "Las contraseñas nuevas no coinciden.";
        }
        if (contrasenaNueva == contrasenaActual)
        {
            return "La contraseña nueva debe ser diferente a la actual.";
        }

        ControlDeIntentos.Reiniciar(claveDeIntentos);
        bool guardado = GestorArchivos.Transaccion(ArchivosDeDatos.Usuarios | ArchivosDeDatos.Bitacora, () =>
        {
            usuario.ContrasenaHash = SeguridadContrasenas.CalcularHash(contrasenaNueva);
            usuario.DebeCambiarContrasena = false;
            usuario.TemporalVenceEl = null;
            GestorArchivos.AnotarEnBitacora(usuario.NombreUsuario, "Cambio de contraseña", usuario.Correo);
        });
        return guardado ? "" : MensajeNoSeGuardo;
    }

    /// <summary>
    /// El administrador da una contraseña temporal a quien olvidó la suya. Regresa esa contraseña
    /// ("" si no se pudo guardar). Vence pronto, la persona debe cambiarla al entrar y nunca se escribe en la bitácora.
    /// </summary>
    public static string RestablecerContrasena(Usuario cuenta, Usuario administrador, string motivo)
    {
        ArgumentNullException.ThrowIfNull(cuenta);
        ArgumentNullException.ThrowIfNull(administrador);

        string contrasenaTemporal = SeguridadContrasenas.GenerarContrasenaTemporal();
        bool guardado = GestorArchivos.Transaccion(ArchivosDeDatos.Usuarios | ArchivosDeDatos.Bitacora, () =>
        {
            cuenta.ContrasenaHash = SeguridadContrasenas.CalcularHash(contrasenaTemporal);
            cuenta.DebeCambiarContrasena = true;
            cuenta.TemporalVenceEl = DateTime.Now.AddHours(HorasDeVigenciaContrasenaTemporal);
            GestorArchivos.AnotarEnBitacora(administrador.NombreUsuario, "Restablecer contraseña", cuenta.Correo, motivo);
        });
        if (guardado == false)
        {
            return "";
        }

        ControlDeIntentos.Reiniciar(cuenta.NombreUsuario.ToLowerInvariant());
        return contrasenaTemporal;
    }

    // ----------------------------- Búsqueda de personas -----------------------------

    /// <summary>
    /// Alumnos y docentes de los accesos que no tienen cuenta, sin repetir personas.
    /// De cada persona se toman los datos de su visita más reciente.
    /// </summary>
    public static List<PersonaSinCuenta> ObtenerPersonasSinCuenta()
    {
        HashSet<string> matriculasConCuenta = GestorArchivos.Usuarios.Select(usuario => usuario.Matricula).Where(matricula => matricula != "").ToHashSet();
        HashSet<string> correosConCuenta = GestorArchivos.Usuarios.Select(usuario => usuario.Correo.ToLowerInvariant()).ToHashSet();

        return GestorArchivos.Accesos
            .Where(acceso => acceso.Categoria != Categorias.Visitante && (acceso.Matricula != "" || acceso.Correo != ""))
            .Where(acceso => matriculasConCuenta.Contains(acceso.Matricula) == false && correosConCuenta.Contains(acceso.Correo.ToLowerInvariant()) == false)
            .GroupBy(acceso => acceso.Matricula != "" ? acceso.Matricula : acceso.Correo.ToLowerInvariant())
            .Select(visitas => visitas.OrderByDescending(acceso => acceso.FechaEntrada).First())
            .Select(acceso => new PersonaSinCuenta
            {
                Nombre = acceso.Nombre,
                Correo = acceso.Correo,
                Matricula = acceso.Matricula,
                Categoria = acceso.Categoria,
                Placa = acceso.Placa
            })
            .OrderBy(persona => persona.Nombre)
            .ToList();
    }

    /// <summary>
    /// true si alguno de los campos contiene todas las palabras buscadas, en cualquier orden:
    /// "carlos carreño" encuentra "Carlos Gustavo Carreño Ponce". Ignora mayúsculas, acentos y guiones:
    /// "martinez" encuentra "Martínez" y "ABC123" encuentra "ABC-123". Sin búsqueda, todo coincide.
    /// </summary>
    public static bool CoincideConBusqueda(string busqueda, params string[] campos)
    {
        string[] palabras = PrepararParaComparar(busqueda).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return palabras.Length == 0
            || campos.Select(PrepararParaComparar).Any(campo => palabras.All(palabra => campo.Contains(palabra)));
    }

    private static string PrepararParaComparar(string texto)
    {
        return InterpretePreguntas.QuitarAcentos(texto.Trim().ToLowerInvariant()).Replace("-", "");
    }

    /// <summary>true si el correo ya está en una cuenta o en una visita (recibe el correo en minúsculas).</summary>
    public static bool EsCorreoRegistrado(string correo)
    {
        return GestorArchivos.Usuarios.Any(usuario => usuario.Correo.ToLowerInvariant() == correo)
            || GestorArchivos.Accesos.Any(acceso => acceso.Correo.ToLowerInvariant() == correo);
    }

    // ----------------------------- Vehículos -----------------------------

    /// <summary>Regresa la cuenta dueña de una placa, o null si ninguna la tiene registrada.</summary>
    public static Usuario? BuscarDuenoDePlaca(string placa)
    {
        foreach (Usuario usuario in GestorArchivos.Usuarios)
        {
            foreach (string placaDeLaCuenta in usuario.ObtenerPlacas())
            {
                if (Validaciones.EsMismaPlaca(placaDeLaCuenta, placa))
                {
                    return usuario;
                }
            }
        }
        return null;
    }

    public static string AgregarVehiculo(Usuario usuario, string placa)
    {
        ArgumentNullException.ThrowIfNull(usuario);

        // === [RÚBRICA 1] VALIDACIONES DE CAMPO ===
        if (Validaciones.EsPlacaValida(placa) == false)
        {
            return Validaciones.MensajePlacaInvalida;
        }
        if (usuario.ObtenerPlacas().Count >= MaximoVehiculosPorCuenta)
        {
            return "Solo puedes registrar " + MaximoVehiculosPorCuenta + " vehículos. Quita uno para agregar otro.";
        }
        if (BuscarDuenoDePlaca(placa) != null)
        {
            return "Esa placa ya está registrada en una cuenta.";
        }

        bool guardado = GestorArchivos.Transaccion(ArchivosDeDatos.Usuarios | ArchivosDeDatos.Bitacora, () =>
        {
            usuario.PlacasAdicionales.Add(placa);
            GestorArchivos.AnotarEnBitacora(usuario.NombreUsuario, "Agregar vehículo", placa);
        });
        return guardado ? "" : MensajeNoSeGuardo;
    }

    public static string QuitarVehiculo(Usuario usuario, string placa)
    {
        ArgumentNullException.ThrowIfNull(usuario);

        List<string> placas = usuario.ObtenerPlacas();
        if (placas.Count <= 1)
        {
            return "Tu cuenta debe tener al menos un vehículo.";
        }
        if (placas.Remove(placa) == false)
        {
            return "Esa placa ya no está en tu cuenta.";
        }

        bool guardado = GestorArchivos.Transaccion(ArchivosDeDatos.Usuarios | ArchivosDeDatos.Bitacora, () =>
        {
            usuario.Placa = placas[0];
            usuario.PlacasAdicionales = placas.Skip(1).ToList();
            GestorArchivos.AnotarEnBitacora(usuario.NombreUsuario, "Quitar vehículo", placa);
        });
        return guardado ? "" : MensajeNoSeGuardo;
    }
}
