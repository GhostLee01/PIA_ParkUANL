namespace PIA_ParkUANL;

// =====================================================================================
//  SERVICIOPERMISOS.CS
//  Lo que el Administrador puede hacer con las cuentas: cambiar su rol y activarlas o desactivarlas.
//  Los métodos que validan regresan "" si todo salió bien, o el mensaje de error.
// =====================================================================================
public static class ServicioPermisos
{
    private const string MensajeNoSeGuardo = "No se pudo guardar el cambio. Intenta de nuevo.";

    /// <summary>Busca la cuenta por correo; regresa el mensaje de error si el correo no es válido o no existe.</summary>
    public static string BuscarCuentaPorCorreo(string correoEscrito, out Usuario? cuenta)
    {
        ArgumentNullException.ThrowIfNull(correoEscrito);

        // === [RÚBRICA 1] VALIDACIONES DE CAMPO ===
        string correo = correoEscrito.Trim().ToLowerInvariant();
        if (correo == "")
        {
            cuenta = null;
            return "Escribe el correo de la cuenta o selecciónala en la tabla.";
        }

        // Primero se busca: las cuentas ya registradas se aceptan aunque su correo tenga un formato anterior (ej. admin@uanl.edu.mx).
        cuenta = GestorArchivos.Usuarios.FirstOrDefault(usuario => usuario.Correo.ToLowerInvariant() == correo);
        if (cuenta != null)
        {
            return "";
        }

        if (Validaciones.EsCorreoUanl(correo) == false && ServicioCuentas.EsCorreoRegistrado(correo) == false)
        {
            return Validaciones.MensajeCorreoUanlInvalido;
        }
        return "No existe una cuenta con ese correo. La persona primero debe crearla desde \"Crear cuenta\" en el login.";
    }

    public static bool EsLaMismaCuenta(Usuario cuenta, Usuario otra)
    {
        ArgumentNullException.ThrowIfNull(cuenta);
        ArgumentNullException.ThrowIfNull(otra);
        return cuenta.NombreUsuario == otra.NombreUsuario;
    }

    /// <summary>Revisa si el cambio de rol se puede hacer, antes de pedir confirmación.</summary>
    public static string ValidarCambioDeRol(Usuario cuenta, string nuevoRol, Usuario administrador)
    {
        ArgumentNullException.ThrowIfNull(cuenta);
        ArgumentNullException.ThrowIfNull(administrador);

        // === [RÚBRICA 1] VALIDACIONES DE CAMPO ===
        if (Roles.EsValido(nuevoRol) == false)
        {
            return "Elige un rol válido.";
        }
        if (EsLaMismaCuenta(cuenta, administrador) && nuevoRol != Roles.Administrador)
        {
            return "No puedes quitarte tu propio rol de Administrador (el sistema se quedaría sin quien lo administre).";
        }
        if (cuenta.Rol == nuevoRol)
        {
            return cuenta.Correo + " ya tiene el rol " + nuevoRol + ".";
        }
        if (nuevoRol == Roles.Comunidad && (cuenta.Categoria == "" || cuenta.Matricula == ""))
        {
            return "Solo un alumno o docente con matrícula puede ser Comunidad (lo necesita para su pase QR).";
        }
        return "";
    }

    public static string AsignarRol(Usuario cuenta, string nuevoRol, Usuario administrador)
    {
        string error = ValidarCambioDeRol(cuenta, nuevoRol, administrador);
        if (error != "")
        {
            return error;
        }

        string rolAnterior = cuenta.Rol;

        // === [RÚBRICA 2] ALMACENAMIENTO EN ARCHIVOS DE TEXTO (.JSON) ===
        bool guardado = GestorArchivos.Transaccion(ArchivosDeDatos.Usuarios | ArchivosDeDatos.Bitacora, () =>
        {
            cuenta.Rol = nuevoRol;
            GestorArchivos.AnotarEnBitacora(administrador.NombreUsuario, "Cambiar rol", cuenta.Correo + ": " + rolAnterior + " → " + nuevoRol);
        });
        return guardado ? "" : MensajeNoSeGuardo;
    }

    public static string ValidarCambioDeEstado(Usuario cuenta, Usuario administrador)
    {
        ArgumentNullException.ThrowIfNull(cuenta);
        ArgumentNullException.ThrowIfNull(administrador);
        return EsLaMismaCuenta(cuenta, administrador) ? "No puedes desactivar tu propia cuenta." : "";
    }

    public static string CambiarEstadoCuenta(Usuario cuenta, Usuario administrador)
    {
        string error = ValidarCambioDeEstado(cuenta, administrador);
        if (error != "")
        {
            return error;
        }

        bool quedaActiva = cuenta.Activo == false;
        bool guardado = GestorArchivos.Transaccion(ArchivosDeDatos.Usuarios | ArchivosDeDatos.Bitacora, () =>
        {
            cuenta.Activo = quedaActiva;
            GestorArchivos.AnotarEnBitacora(administrador.NombreUsuario, quedaActiva ? "Activar cuenta" : "Desactivar cuenta", cuenta.Correo);
        });
        return guardado ? "" : MensajeNoSeGuardo;
    }
}
