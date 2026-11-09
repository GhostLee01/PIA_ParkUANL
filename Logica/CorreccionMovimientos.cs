namespace PIA_ParkUANL;

// =====================================================================================
//  CORRECCIONMOVIMIENTOS.CS
//  Arregla los errores de captura. Cada corrección pide un motivo y queda en la bitácora.
//  El cambio y su línea de bitácora se guardan juntos: no puede existir uno sin el otro.
//  Los métodos regresan "" si todo salió bien, o el mensaje de error.
//  Ninguna corrección cambia una placa: las placas de una cuenta solo las agrega o quita
//  su dueño en "Mis vehículos". Un acceso con la placa equivocada se anula y se captura de nuevo.
// =====================================================================================
public static class CorreccionMovimientos
{
    private const string MensajeNoSeGuardo = "No se pudo guardar la corrección. Intenta de nuevo.";
    private const string MensajeFaltaMotivo = "Escribe el motivo de la corrección.";
    private const string MensajeMotivoInvalido = "El motivo admite hasta 200 caracteres, sin saltos de línea.";

    private static string ValidarMotivo(string motivo)
    {
        if (string.IsNullOrWhiteSpace(motivo))
        {
            return MensajeFaltaMotivo;
        }
        return Validaciones.EsTextoLibreValido(motivo) ? "" : MensajeMotivoInvalido;
    }

    /// <summary>
    /// Quita un registro que nunca debió existir. No se borra: pasa a accesos_anulados.json,
    /// así deja de contar en el aforo y en los reportes, pero se puede revisar después.
    /// </summary>
    public static string AnularRegistro(RegistroAcceso registro, string nombreUsuario, string motivo)
    {
        ArgumentNullException.ThrowIfNull(registro);

        string errorDeMotivo = ValidarMotivo(motivo);
        if (errorDeMotivo != "")
        {
            return errorDeMotivo;
        }
        if (GestorArchivos.Accesos.Contains(registro) == false)
        {
            return "Ese registro ya no existe. Actualiza la tabla.";
        }

        bool guardado = GestorArchivos.Transaccion(ArchivosDeDatos.Accesos | ArchivosDeDatos.AccesosAnulados | ArchivosDeDatos.Bitacora, () =>
        {
            GestorArchivos.AccesosAnulados.Add(registro);
            GestorArchivos.Accesos.Remove(registro);
            GestorArchivos.AnotarEnBitacora(nombreUsuario, "Anular registro", DescribirRegistro(registro), motivo);
        });
        return guardado ? "" : MensajeNoSeGuardo;
    }

    /// <summary>Regresa adentro a un vehículo al que se le marcó la salida por error.</summary>
    public static string DeshacerSalida(RegistroAcceso registro, string nombreUsuario, string motivo)
    {
        ArgumentNullException.ThrowIfNull(registro);

        string errorDeMotivo = ValidarMotivo(motivo);
        if (errorDeMotivo != "")
        {
            return errorDeMotivo;
        }
        if (registro.EstaAdentro)
        {
            return "Ese vehículo sigue adentro: no tiene salida que deshacer.";
        }

        string errorDobleEntrada = ServicioCaseta.BuscarDobleEntrada(registro.Placa, registro.Matricula, registro.Correo);
        if (errorDobleEntrada != "")
        {
            return errorDobleEntrada;
        }

        // Su cajón pudo ocuparse mientras tanto: entonces se le da el primero que esté libre.
        bool cajonOcupado = GestorArchivos.Accesos.Any(acceso => acceso.EstaAdentro && acceso.Estacionamiento == registro.Estacionamiento
                                                                 && acceso.Cajon == registro.Cajon);
        string cajon = cajonOcupado ? ServicioCaseta.AsignarCajonGeneral(registro.Estacionamiento) : registro.Cajon;

        DateTime ahora = DateTime.Now;
        ResumenAforo resumen = CalculadoraAforo.CalcularResumen(registro.Estacionamiento, GestorArchivos.Accesos, GestorArchivos.Eventos, ahora);
        if (cajon == ServicioCaseta.CajonSinNumero || (registro.UsaCajonReservado == false && resumen.LugaresDisponibles <= 0))
        {
            return "No se puede deshacer: " + registro.Estacionamiento + " está lleno.";
        }

        bool guardado = GestorArchivos.Transaccion(ArchivosDeDatos.Accesos | ArchivosDeDatos.Bitacora, () =>
        {
            registro.UsaCajonReservado = registro.UsaCajonReservado && cajonOcupado == false;
            registro.Cajon = cajon;
            registro.FechaSalida = null;
            registro.OperadorSalida = "";
            GestorArchivos.AnotarEnBitacora(nombreUsuario, "Deshacer salida", DescribirRegistro(registro), motivo);
        });
        return guardado ? "" : MensajeNoSeGuardo;
    }

    /// <summary>Registra la salida de un vehículo olvidado.</summary>
    public static string CerrarEstancia(RegistroAcceso registro, string nombreUsuario, string motivo, DateTime ahora)
    {
        ArgumentNullException.ThrowIfNull(registro);

        string errorDeMotivo = ValidarMotivo(motivo);
        if (errorDeMotivo != "")
        {
            return errorDeMotivo;
        }
        if (registro.EstaAdentro == false)
        {
            return "Ese vehículo ya tiene salida registrada.";
        }

        bool guardado = GestorArchivos.Transaccion(ArchivosDeDatos.Accesos | ArchivosDeDatos.Bitacora, () =>
        {
            registro.FechaSalida = ahora;
            registro.OperadorSalida = nombreUsuario;
            GestorArchivos.AnotarEnBitacora(nombreUsuario, "Cerrar estancia", DescribirRegistro(registro), motivo);
        });
        return guardado ? "" : MensajeNoSeGuardo;
    }

    /// <summary>Cierra el turno: registra todas las salidas indicadas y el cierre en una sola operación.</summary>
    public static string CerrarTurno(List<RegistroAcceso> olvidadosConSalida, string motivoDelCierre, string nombreUsuario, DateTime ahora)
    {
        ArgumentNullException.ThrowIfNull(olvidadosConSalida);

        if (olvidadosConSalida.Count > 0)
        {
            string errorDeMotivo = ValidarMotivo(motivoDelCierre);
            if (errorDeMotivo != "")
            {
                return errorDeMotivo;
            }
        }

        bool guardado = GestorArchivos.Transaccion(ArchivosDeDatos.Accesos | ArchivosDeDatos.Bitacora, () =>
        {
            foreach (RegistroAcceso registro in olvidadosConSalida.Where(olvidado => olvidado.EstaAdentro))
            {
                registro.FechaSalida = ahora;
                registro.OperadorSalida = nombreUsuario;
                GestorArchivos.AnotarEnBitacora(nombreUsuario, "Cerrar estancia", DescribirRegistro(registro), motivoDelCierre);
            }

            // Se genera al final: las salidas recién registradas cambian los conteos.
            string resumenFinal = ReportesAccesos.GenerarCierreDeTurno(ahora).ReplaceLineEndings(" · ");
            GestorArchivos.AnotarEnBitacora(nombreUsuario, "Cierre de turno", resumenFinal, motivoDelCierre);
        });
        return guardado ? "" : MensajeNoSeGuardo;
    }

    private static string DescribirRegistro(RegistroAcceso registro)
    {
        return "Registro #" + registro.Id + " · " + registro.Placa + " · " + registro.Estacionamiento + " · entró " +
               registro.FechaEntrada.ToString("dd/MM/yyyy HH:mm");
    }
}
