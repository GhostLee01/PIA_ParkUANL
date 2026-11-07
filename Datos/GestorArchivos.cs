using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PIA_ParkUANL;

/// <summary>Archivos que participan en un guardado. Se combinan con | para guardarlos juntos.</summary>
[Flags]
public enum ArchivosDeDatos
{
    Usuarios = 1,
    Accesos = 2,
    AccesosAnulados = 4,
    Eventos = 8,
    Pases = 16,
    Bitacora = 32
}

// =====================================================================================
//  GESTORARCHIVOS.CS
//  Lee y guarda los datos en archivos de texto .json dentro de %LocalAppData%\ParkUANL\Data.
//  Solo la primera vez, con la carpeta sin datos, genera datos de prueba (DatosDePrueba.cs).
//  No se usa ninguna base de datos: las listas viven en memoria y se escriben al disco
//  cada vez que cambian.
// =====================================================================================
public static class GestorArchivos
{
    // === [RÚBRICA 2] ALMACENAMIENTO EN ARCHIVOS DE TEXTO (.JSON) ===

    /// <summary>Carpeta de los datos. Las pruebas automáticas la cambian por una carpeta temporal.</summary>
    public static string CarpetaDatos { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ParkUANL", "Data");

    // Antes los datos vivían junto al .exe: se copian una sola vez a la carpeta nueva.
    private static readonly string CarpetaDatosVersionAnterior = Path.Combine(AppContext.BaseDirectory, "Data");

    private const string ArchivoUsuarios = "usuarios.json";
    private const string ArchivoAccesos = "accesos.json";
    private const string ArchivoAccesosAnulados = "accesos_anulados.json";
    private const string ArchivoEventos = "eventos.json";
    private const string ArchivoPases = "pases_visitantes.json";
    private const string ArchivoBitacora = "bitacora.json";
    private const string ArchivoConfiguracionAntigua = "configuracion_ia.json";
    private const string ArchivoClaveFirma = "clave_firma.dat";
    private const string ArchivoErrores = "errores.log";

    private const string ExtensionRespaldo = ".respaldo";
    private const string ExtensionTemporal = ".tmp";
    private const string ExtensionDanado = ".danado.bak";

    private const int BytesDeClaveFirma = 32;
    private const long TamanoMaximoDelLog = 1024 * 1024;

    // Datos cargados en memoria. Se conservan las mismas listas y objetos al deshacer un guardado
    // fallido: así la sesión y las pantallas nunca se quedan con copias sueltas.
    public static List<Usuario> Usuarios = new List<Usuario>();
    public static List<RegistroAcceso> Accesos = new List<RegistroAcceso>();
    public static List<RegistroAcceso> AccesosAnulados = new List<RegistroAcceso>();
    public static List<EventoCampus> Eventos = new List<EventoCampus>();
    public static List<PaseVisitante> Pases = new List<PaseVisitante>();
    public static List<RegistroBitacora> Bitacora = new List<RegistroBitacora>();

    /// <summary>Avisos de la carga (archivo dañado, cuenta de emergencia...). VentanaPrincipal los muestra al usuario.</summary>
    public static List<string> AvisosDeCarga = new List<string>();

    /// <summary>Cómo se avisa de un error al guardar: App lo conecta con la ventana y las pruebas automáticas lo capturan.</summary>
    public static Action<string> MostrarErrorDeGuardado = _ => { };

    /// <summary>true si usuarios.json no coincide con su sello: se desconfía de los hashes del formato anterior.</summary>
    public static bool UsuariosAlterados { get; private set; }

    private static byte[]? claveDeFirma;
    private static string carpetaDeLaClave = "";
    private static bool claveSeRegenero;

    // Opciones del serializador: JSON con sangría y acentos legibles (á, ñ).
    private static readonly JsonSerializerOptions OpcionesJson = new JsonSerializerOptions
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly DefinicionDeArchivo[] Definiciones =
    {
        new DefinicionDeArchivo<Usuario>(ArchivosDeDatos.Usuarios, ArchivoUsuarios, () => Usuarios, usuario => usuario.NombreUsuario),
        new DefinicionDeArchivo<RegistroAcceso>(ArchivosDeDatos.Accesos, ArchivoAccesos, () => Accesos, acceso => acceso.Id.ToString()),
        new DefinicionDeArchivo<RegistroAcceso>(ArchivosDeDatos.AccesosAnulados, ArchivoAccesosAnulados, () => AccesosAnulados, acceso => acceso.Id.ToString()),
        new DefinicionDeArchivo<EventoCampus>(ArchivosDeDatos.Eventos, ArchivoEventos, () => Eventos, evento => evento.Id.ToString()),
        new DefinicionDeArchivo<PaseVisitante>(ArchivosDeDatos.Pases, ArchivoPases, () => Pases, pase => pase.Id),
        new DefinicionDeArchivo<RegistroBitacora>(ArchivosDeDatos.Bitacora, ArchivoBitacora, () => Bitacora, registro => registro.Id.ToString())
    };

    // ----------------------------- Carga inicial -----------------------------

    /// <summary>Se llama una vez al iniciar la app (VentanaPrincipal.cs).</summary>
    public static void Inicializar()
    {
        Directory.CreateDirectory(CarpetaDatos);
        AvisosDeCarga.Clear();
        claveDeFirma = null;
        claveSeRegenero = false;
        UsuariosAlterados = false;
        CopiarDatosDeVersionAnterior();
        BorrarConfiguracionAntigua();

        // Se lee aquí y no al firmar el primer QR: su aviso debe alcanzar a mostrarse al abrir la app.
        ObtenerClaveDeFirma();
        ControlDeIntentos.Cargar();

        if (EsCarpetaSinDatos())
        {
            // Primera ejecución: se crean todos los archivos con datos de prueba.
            SelloDeArchivos.Cargar();
            DatosDePrueba.Generar();
            return;
        }

        CargarSellosDeArchivosExistentes();
        Usuarios = LeerLista<Usuario>(ArchivoUsuarios);
        Accesos = LeerLista<RegistroAcceso>(ArchivoAccesos);
        AccesosAnulados = LeerLista<RegistroAcceso>(ArchivoAccesosAnulados);
        Eventos = LeerLista<EventoCampus>(ArchivoEventos);
        Pases = LeerLista<PaseVisitante>(ArchivoPases);
        Bitacora = LeerLista<RegistroBitacora>(ArchivoBitacora);

        if (Usuarios.Count == 0)
        {
            CrearAdministradorDeEmergencia();
        }
    }

    /// <summary>
    /// Con una clave nueva ninguna huella anterior se puede comprobar: se empieza de cero, y el aviso de la clave
    /// ya lo explica. Sin registro de integridad tampoco se confía en los hashes del formato anterior.
    /// </summary>
    private static void CargarSellosDeArchivosExistentes()
    {
        if (claveSeRegenero)
        {
            SelloDeArchivos.Reiniciar();
            return;
        }

        if (SelloDeArchivos.Cargar() == false)
        {
            AvisosDeCarga.Add("No se encontró un registro de integridad válido: se creó uno nuevo con los archivos actuales.");
            UsuariosAlterados = true;
        }
    }

    /// <summary>
    /// Solo una carpeta sin ningún archivo de datos es una primera ejecución. Si falta uno solo,
    /// generar datos de prueba reemplazaría los registros reales por datos inventados.
    /// </summary>
    private static bool EsCarpetaSinDatos()
    {
        foreach (DefinicionDeArchivo definicion in Definiciones)
        {
            string ruta = RutaDe(definicion.Nombre);
            if (File.Exists(ruta) || File.Exists(ruta + ExtensionRespaldo))
            {
                return false;
            }
        }
        return true;
    }

    public static string RutaDe(string nombreArchivo)
    {
        return Path.Combine(CarpetaDatos, nombreArchivo);
    }

    private static void CopiarDatosDeVersionAnterior()
    {
        bool hayDatosNuevos = File.Exists(RutaDe(ArchivoUsuarios));
        bool hayDatosAnteriores = File.Exists(Path.Combine(CarpetaDatosVersionAnterior, ArchivoUsuarios));
        if (hayDatosNuevos || hayDatosAnteriores == false)
        {
            return;
        }

        foreach (string rutaOrigen in Directory.GetFiles(CarpetaDatosVersionAnterior, "*.json"))
        {
            string rutaDestino = RutaDe(Path.GetFileName(rutaOrigen));
            if (File.Exists(rutaDestino) == false)
            {
                File.Copy(rutaOrigen, rutaDestino);
            }
        }
    }

    /// <summary>
    /// Versiones anteriores guardaban aquí la llave de un servicio externo que ya no se usa.
    /// Se borra (también su respaldo) para que esa llave no se quede guardada en el equipo.
    /// </summary>
    private static void BorrarConfiguracionAntigua()
    {
        foreach (string carpeta in new[] { CarpetaDatos, CarpetaDatosVersionAnterior }.Where(Directory.Exists))
        {
            string ruta = Path.Combine(carpeta, ArchivoConfiguracionAntigua);
            try
            {
                File.Delete(ruta);
                File.Delete(ruta + ExtensionRespaldo);
            }
            catch (Exception ex) when (EsErrorDeDisco(ex))
            {
                RegistrarError(ex);
            }
        }
    }

    /// <summary>
    /// Si no hay cuentas nadie podría entrar: se crea un administrador
    /// con una contraseña temporal aleatoria, que se muestra una sola vez.
    /// </summary>
    private static void CrearAdministradorDeEmergencia()
    {
        string contrasenaTemporal = SeguridadContrasenas.GenerarContrasenaTemporal();
        Usuarios.Add(new Usuario
        {
            NombreUsuario = "admin",
            ContrasenaHash = SeguridadContrasenas.CalcularHash(contrasenaTemporal),
            NombreCompleto = "Administrador de emergencia",
            Correo = "admin@uanl.edu.mx",
            Rol = Roles.Administrador,
            DebeCambiarContrasena = true
        });
        GuardarUsuarios();
        RegistrarEnBitacora("sistema", "Administrador de emergencia", "No había cuentas al iniciar.");
        AvisosDeCarga.Add("No se encontraron cuentas. Se creó el usuario admin con la contraseña temporal: " + contrasenaTemporal);
    }

    /// <summary>
    /// Lee un archivo .json y lo convierte en una lista. Si no existe o está dañado, usa el respaldo
    /// de la versión anterior, y solo inicia vacío cuando tampoco hay respaldo.
    /// Los errores de acceso al disco NO se atrapan aquí: arrancar con listas vacías
    /// sobrescribiría los datos reales al primer guardado (VentanaPrincipal los muestra y cierra la app).
    /// </summary>
    private static List<T> LeerLista<T>(string nombreArchivo) where T : class
    {
        string ruta = RutaDe(nombreArchivo);
        bool existe = File.Exists(ruta);

        if (existe)
        {
            try
            {
                List<T> lista = LeerArchivoJson<T>(ruta, out string texto);
                RevisarSello(nombreArchivo, texto);
                return lista;
            }
            catch (JsonException)
            {
                File.Copy(ruta, ruta + ExtensionDanado, true);
            }
        }

        string rutaRespaldo = ruta + ExtensionRespaldo;
        if (File.Exists(rutaRespaldo))
        {
            try
            {
                List<T> recuperada = LeerArchivoJson<T>(rutaRespaldo, out _);
                AvisosDeCarga.Add(existe
                    ? "El archivo " + nombreArchivo + " estaba dañado. Se recuperó la versión anterior (se guardó una copia " + ExtensionDanado + ")."
                    : "Faltaba el archivo " + nombreArchivo + ". Se recuperó la versión anterior.");
                return recuperada;
            }
            catch (JsonException)
            {
                // El respaldo también está dañado: se sigue con la lista vacía.
            }
        }

        if (existe)
        {
            AvisosDeCarga.Add("El archivo " + nombreArchivo + " estaba dañado y no tenía respaldo. Se guardó una copia (" + ExtensionDanado + ") y se inició vacío.");
        }
        return new List<T>();
    }

    private static List<T> LeerArchivoJson<T>(string ruta, out string texto) where T : class
    {
        texto = File.ReadAllText(ruta, Encoding.UTF8);

        // Un archivo que solo dice "null" es JSON válido pero no es una lista: se trata como dañado.
        List<T>? lista = JsonSerializer.Deserialize<List<T>>(texto, OpcionesJson);
        if (lista == null)
        {
            throw new JsonException("El archivo no contiene una lista.");
        }
        return DatosConfiables.Normalizar(lista);
    }

    private static void RevisarSello(string nombreArchivo, string texto)
    {
        if (SelloDeArchivos.Coincide(nombreArchivo, texto))
        {
            return;
        }

        AvisosDeCarga.Add("El archivo " + nombreArchivo + " se modificó fuera de ParkUANL. Revisa sus datos y la bitácora.");
        if (nombreArchivo == ArchivoUsuarios)
        {
            UsuariosAlterados = true;
        }
    }

    /// <summary>
    /// Clave secreta con la que se firman los QR. Se genera al azar la primera vez y se guarda
    /// cifrada para este usuario de Windows: no está en el código ni se puede leer en el archivo.
    /// </summary>
    public static byte[] ObtenerClaveDeFirma()
    {
        if (claveDeFirma != null && carpetaDeLaClave == CarpetaDatos)
        {
            return claveDeFirma;
        }

        Directory.CreateDirectory(CarpetaDatos);
        string ruta = RutaDe(ArchivoClaveFirma);
        if (File.Exists(ruta))
        {
            try
            {
                claveDeFirma = ProteccionLocal.Descifrar(File.ReadAllBytes(ruta));
                carpetaDeLaClave = CarpetaDatos;
                return claveDeFirma;
            }
            catch (CryptographicException)
            {
                // Se conserva la clave ilegible: en otro usuario de Windows todavía podría servir.
                File.Move(ruta, ruta + ExtensionDanado, true);
                claveSeRegenero = true;
                AvisosDeCarga.Add("No se pudo leer la clave de firma: se generó una nueva y los QR anteriores dejan de ser válidos.");
            }
        }

        byte[] claveNueva = RandomNumberGenerator.GetBytes(BytesDeClaveFirma);
        string rutaTemporal = ruta + ExtensionTemporal;
        File.WriteAllBytes(rutaTemporal, ProteccionLocal.Cifrar(claveNueva));
        File.Move(rutaTemporal, ruta, true);
        claveDeFirma = claveNueva;
        carpetaDeLaClave = CarpetaDatos;
        return claveDeFirma;
    }

    // ----------------------------- Guardado -----------------------------

    public static bool EsErrorDeDisco(Exception error)
    {
        return error is IOException || error is UnauthorizedAccessException;
    }

    /// <summary>
    /// Aplica varios cambios como una sola operación: todos los archivos indicados se guardan o ninguno.
    /// Si el disco falla, las listas regresan al estado anterior, se avisa y se regresa false.
    /// </summary>
    public static bool Transaccion(ArchivosDeDatos archivos, Action cambios)
    {
        ArgumentNullException.ThrowIfNull(cambios);

        List<DefinicionDeArchivo> definiciones = Definiciones.Where(definicion => archivos.HasFlag(definicion.Tipo)).ToList();
        Dictionary<DefinicionDeArchivo, string> estadoAnterior = definiciones.ToDictionary(definicion => definicion, definicion => definicion.Serializar(OpcionesJson));

        // La foto se toma antes de los cambios: incluye los objetos que esos cambios vayan a quitar de la lista.
        Dictionary<DefinicionDeArchivo, object> objetosAnteriores = definiciones.ToDictionary(definicion => definicion, definicion => definicion.TomarFoto());

        bool terminoBien = false;
        try
        {
            cambios();
            EscribirJuntos(definiciones);
            terminoBien = true;
            return true;
        }
        catch (Exception ex) when (EsErrorDeDisco(ex))
        {
            RegistrarError(ex);
            MostrarErrorDeGuardado("No se pudo guardar: " + ex.Message + "\nEl último cambio no se aplicó.");
            return false;
        }
        finally
        {
            if (terminoBien == false)
            {
                foreach (DefinicionDeArchivo definicion in definiciones)
                {
                    definicion.Restaurar(estadoAnterior[definicion], objetosAnteriores[definicion], OpcionesJson);
                }
            }
        }
    }

    /// <summary>
    /// Primero escribe todos los temporales: si alguno falla, ningún archivo real se tocó.
    /// Después reemplaza los originales; si uno falla, los ya reemplazados vuelven a su respaldo.
    /// </summary>
    private static void EscribirJuntos(List<DefinicionDeArchivo> definiciones)
    {
        List<(DefinicionDeArchivo Definicion, string Texto)> pendientes = new List<(DefinicionDeArchivo, string)>();
        try
        {
            foreach (DefinicionDeArchivo definicion in definiciones)
            {
                string texto = definicion.Serializar(OpcionesJson);
                EscribirEnDisco(RutaDe(definicion.Nombre) + ExtensionTemporal, texto);
                pendientes.Add((definicion, texto));
            }
        }
        catch (Exception ex) when (EsErrorDeDisco(ex))
        {
            BorrarTemporales(definiciones);
            throw;
        }

        // El sello conserva la huella anterior: si la app se cierra justo aquí, el archivo sigue siendo válido.
        foreach ((DefinicionDeArchivo definicion, string texto) in pendientes)
        {
            SelloDeArchivos.Registrar(definicion.Nombre, texto);
        }

        List<DefinicionDeArchivo> reemplazados = new List<DefinicionDeArchivo>();
        try
        {
            foreach ((DefinicionDeArchivo definicion, string _) in pendientes)
            {
                ReemplazarConTemporal(RutaDe(definicion.Nombre));
                reemplazados.Add(definicion);
            }
        }
        catch (Exception ex) when (EsErrorDeDisco(ex))
        {
            foreach (DefinicionDeArchivo definicion in reemplazados)
            {
                string ruta = RutaDe(definicion.Nombre);
                if (File.Exists(ruta + ExtensionRespaldo))
                {
                    File.Copy(ruta + ExtensionRespaldo, ruta, true);
                }
            }
            BorrarTemporales(definiciones);
            throw;
        }
    }

    /// <summary>Flush(true) obliga a Windows a escribir en el disco: un corte de luz no deja el archivo a medias.</summary>
    private static void EscribirEnDisco(string ruta, string texto)
    {
        using FileStream archivo = new FileStream(ruta, FileMode.Create, FileAccess.Write, FileShare.None);
        byte[] bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(texto)).ToArray();
        archivo.Write(bytes, 0, bytes.Length);
        archivo.Flush(true);
    }

    private static void ReemplazarConTemporal(string ruta)
    {
        // La versión anterior se conserva como .respaldo por si el archivo se daña después.
        string rutaTemporal = ruta + ExtensionTemporal;
        if (File.Exists(ruta))
        {
            File.Replace(rutaTemporal, ruta, ruta + ExtensionRespaldo);
        }
        else
        {
            File.Move(rutaTemporal, ruta);
        }
    }

    private static void BorrarTemporales(List<DefinicionDeArchivo> definiciones)
    {
        foreach (DefinicionDeArchivo definicion in definiciones)
        {
            string rutaTemporal = RutaDe(definicion.Nombre) + ExtensionTemporal;
            try
            {
                if (File.Exists(rutaTemporal))
                {
                    File.Delete(rutaTemporal);
                }
            }
            catch (Exception ex) when (EsErrorDeDisco(ex))
            {
                RegistrarError(ex);
            }
        }
    }

    /// <summary>
    /// Guarda una lista ya modificada. Si el disco falla, avisa, deshace el cambio en memoria volviendo
    /// a leer el archivo y regresa false: la pantalla y el disco nunca quedan distintos.
    /// </summary>
    private static bool GuardarDesdeMemoria(ArchivosDeDatos archivo)
    {
        DefinicionDeArchivo definicion = Definiciones.First(candidata => candidata.Tipo == archivo);
        try
        {
            EscribirJuntos(new List<DefinicionDeArchivo> { definicion });
            return true;
        }
        catch (Exception ex) when (EsErrorDeDisco(ex))
        {
            RegistrarError(ex);
            VolverALeerSinFallar(definicion);
            MostrarErrorDeGuardado("No se pudo guardar " + definicion.Nombre + ": " + ex.Message + "\nEl último cambio no se aplicó.");
            return false;
        }
    }

    /// <summary>Si tampoco se puede leer, solo se anota: el aviso principal al usuario sigue siendo el error de guardado.</summary>
    private static void VolverALeerSinFallar(DefinicionDeArchivo definicion)
    {
        try
        {
            string ruta = RutaDe(definicion.Nombre);
            definicion.Restaurar(File.Exists(ruta) ? File.ReadAllText(ruta, Encoding.UTF8) : "[]", definicion.TomarFoto(), OpcionesJson);
        }
        catch (Exception ex) when (EsErrorDeDisco(ex) || ex is JsonException)
        {
            RegistrarError(ex);
        }
    }

    public static bool GuardarUsuarios()
    {
        return GuardarDesdeMemoria(ArchivosDeDatos.Usuarios);
    }

    public static bool GuardarAccesos()
    {
        return GuardarDesdeMemoria(ArchivosDeDatos.Accesos);
    }

    public static bool GuardarAccesosAnulados()
    {
        return GuardarDesdeMemoria(ArchivosDeDatos.AccesosAnulados);
    }

    public static bool GuardarEventos()
    {
        return GuardarDesdeMemoria(ArchivosDeDatos.Eventos);
    }

    public static bool GuardarPases()
    {
        return GuardarDesdeMemoria(ArchivosDeDatos.Pases);
    }

    public static bool GuardarBitacora()
    {
        return GuardarDesdeMemoria(ArchivosDeDatos.Bitacora);
    }

    /// <summary>Agrega la línea solo en memoria: se usa dentro de una Transaccion para guardarla junto con el cambio.</summary>
    public static void AnotarEnBitacora(string usuario, string accion, string detalle, string motivo = "")
    {
        Bitacora.Add(new RegistroBitacora
        {
            Id = Bitacora.Count == 0 ? 1 : Bitacora.Max(registro => registro.Id) + 1,
            Fecha = DateTime.Now,
            Usuario = usuario,
            Accion = accion,
            Detalle = detalle,
            Motivo = motivo
        });
    }

    /// <summary>Agrega una línea a la bitácora y la guarda: quién, qué y por qué.</summary>
    public static bool RegistrarEnBitacora(string usuario, string accion, string detalle, string motivo = "")
    {
        return Transaccion(ArchivosDeDatos.Bitacora, () => AnotarEnBitacora(usuario, accion, detalle, motivo));
    }

    /// <summary>Anota un error inesperado en errores.log para poder revisarlo después.</summary>
    public static void RegistrarError(Exception error)
    {
        try
        {
            string ruta = RutaDe(ArchivoErrores);
            if (File.Exists(ruta) && new FileInfo(ruta).Length > TamanoMaximoDelLog)
            {
                File.Move(ruta, ruta + ".anterior", true);
            }

            string linea = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + error + Environment.NewLine;
            File.AppendAllText(ruta, linea, Encoding.UTF8);
        }
        catch (Exception ex) when (EsErrorDeDisco(ex))
        {
            // Si tampoco se puede escribir el log, no hay dónde más anotarlo.
        }
    }

    // ----------------------------- Identificadores -----------------------------

    /// <summary>También se cuentan los anulados, para que un Id nunca se repita.</summary>
    public static int SiguienteIdAcceso()
    {
        int mayor = 0;
        foreach (RegistroAcceso acceso in Accesos.Concat(AccesosAnulados))
        {
            if (acceso.Id > mayor)
            {
                mayor = acceso.Id;
            }
        }
        return mayor + 1;
    }

    public static int SiguienteIdEvento()
    {
        int mayor = 0;
        foreach (EventoCampus evento in Eventos)
        {
            if (evento.Id > mayor)
            {
                mayor = evento.Id;
            }
        }
        return mayor + 1;
    }

    /// <summary>
    /// Ids de pase con formato "P-0001". Se revisan también los accesos: así el Id de un pase
    /// eliminado no se confunde con uno nuevo en el historial. El QR no depende solo del Id:
    /// lleva el CodigoUnico del pase, por eso un QR viejo no sirve aunque el Id se repita.
    /// </summary>
    public static string SiguienteIdPase()
    {
        IEnumerable<string> idsUsados = Pases.Select(pase => pase.Id).Concat(Accesos.Select(acceso => acceso.IdPase));

        int mayor = 0;
        foreach (string id in idsUsados)
        {
            int numero;
            if (id.StartsWith("P-") && int.TryParse(id.Substring(2), out numero) && numero > mayor)
            {
                mayor = numero;
            }
        }
        return "P-" + (mayor + 1).ToString("0000");
    }

    // ----------------------------- Definición de cada archivo -----------------------------

    /// <summary>Lo que se necesita saber de un archivo para guardarlo y deshacer sus cambios.</summary>
    private abstract class DefinicionDeArchivo
    {
        public abstract ArchivosDeDatos Tipo { get; }
        public abstract string Nombre { get; }
        public abstract string Serializar(JsonSerializerOptions opciones);
        public abstract object TomarFoto();
        public abstract void Restaurar(string texto, object objetosAnteriores, JsonSerializerOptions opciones);
    }

    private sealed class DefinicionDeArchivo<T> : DefinicionDeArchivo where T : class
    {
        private static readonly PropertyInfo[] PropiedadesGuardadas = typeof(T).GetProperties()
            .Where(propiedad => propiedad.CanWrite && propiedad.GetCustomAttribute<JsonIgnoreAttribute>() == null)
            .ToArray();

        private readonly Func<List<T>> lista;
        private readonly Func<T, string> clave;

        public override ArchivosDeDatos Tipo { get; }
        public override string Nombre { get; }

        public DefinicionDeArchivo(ArchivosDeDatos tipo, string nombre, Func<List<T>> lista, Func<T, string> clave)
        {
            Tipo = tipo;
            Nombre = nombre;
            this.lista = lista;
            this.clave = clave;
        }

        public override string Serializar(JsonSerializerOptions opciones)
        {
            return JsonSerializer.Serialize(lista(), opciones);
        }

        /// <summary>Los objetos que había, por su clave.</summary>
        public override object TomarFoto()
        {
            Dictionary<string, T> foto = new Dictionary<string, T>();
            foreach (T elemento in lista())
            {
                foto[clave(elemento)] = elemento;
            }
            return foto;
        }

        /// <summary>Regresa la lista a ese estado reutilizando los mismos objetos: quien ya los tenía sigue viendo los datos reales.</summary>
        public override void Restaurar(string texto, object objetosAnteriores, JsonSerializerOptions opciones)
        {
            List<T> anteriores = DatosConfiables.Normalizar(JsonSerializer.Deserialize<List<T>>(texto, opciones) ?? new List<T>());
            Dictionary<string, T> actuales = (Dictionary<string, T>)objetosAnteriores;

            List<T> restaurada = new List<T>();
            foreach (T anterior in anteriores)
            {
                if (actuales.TryGetValue(clave(anterior), out T? existente))
                {
                    foreach (PropertyInfo propiedad in PropiedadesGuardadas)
                    {
                        propiedad.SetValue(existente, propiedad.GetValue(anterior));
                    }
                    restaurada.Add(existente);
                }
                else
                {
                    restaurada.Add(anterior);
                }
            }

            lista().Clear();
            lista().AddRange(restaurada);
        }
    }
}
