namespace PIA_ParkUANL;

// =====================================================================================
//  DATOSDEPRUEBA.CS
//  Datos de ejemplo que se crean solo la primera vez que se abre la app.
// =====================================================================================
public static class DatosDePrueba
{
    private const int DiasDeHistorial = 7;
    private const int EntradasPorDiaDeHistorial = 120;
    private const int VehiculosAdentroFacpya = 330;
    private const int VehiculosAdentroEstadio = 380;
    private const int VisitantesEnCajonReservado = 12;
    private const int VehiculosOlvidados = 3;

    private static readonly string[] NombresDePila =
    {
        "Ana", "Luis", "María", "José", "Fernanda", "Carlos", "Sofía", "Diego", "Valeria", "Jorge",
        "Daniela", "Miguel", "Paola", "Ricardo", "Andrea", "Héctor", "Lucía", "Eduardo", "Mariana", "Raúl"
    };

    private static readonly string[] Apellidos =
    {
        "Garza", "Martínez", "Treviño", "González", "Rodríguez", "Hernández", "Cantú", "Villarreal",
        "Salinas", "Pérez", "Leal", "Elizondo", "Sánchez", "Ramírez", "Flores", "Guerra"
    };

    public static void Generar()
    {
        Random azar = new Random(2026);
        DateTime ahora = DateTime.Now;
        DateTime hoy = ahora.Date;
        HashSet<string> placasUsadas = new HashSet<string> { "SRT-458-A", "RMS-221-B" };

        GestorArchivos.Usuarios = CrearUsuarios();
        GestorArchivos.Eventos = CrearEventos(hoy);
        GestorArchivos.Accesos = new List<RegistroAcceso>();
        GestorArchivos.AccesosAnulados = new List<RegistroAcceso>();
        GestorArchivos.Pases = new List<PaseVisitante>();
        GestorArchivos.Bitacora = new List<RegistroBitacora>();

        // ---------- Historial de los días anteriores (estancias ya concluidas) ----------
        for (int diasAtras = DiasDeHistorial; diasAtras >= 1; diasAtras--)
        {
            DateTime dia = hoy.AddDays(-diasAtras);
            for (int i = 0; i < EntradasPorDiaDeHistorial; i++)
            {
                DateTime entrada = dia.AddHours(7).AddMinutes(azar.Next(0, 13 * 60));
                DateTime salida = entrada.AddMinutes(azar.Next(30, 300));
                AgregarAcceso(azar, placasUsadas, EstacionamientoAlAzar(azar), CategoriaAlAzar(azar), entrada, salida, i + 1);
            }
        }

        // Minutos transcurridos del día de hoy (si es muy temprano, algunas entradas quedan de ayer en la noche).
        int minutosDeHoy = (int)(ahora - hoy).TotalMinutes;
        int ventanaDeMinutos = Math.Clamp(minutosDeHoy, 60, 10 * 60);

        // ---------- Hoy: estancias que ya terminaron ----------
        for (int i = 0; i < 60; i++)
        {
            DateTime entrada = ahora.AddMinutes(-azar.Next(40, ventanaDeMinutos + 40));
            DateTime salida = entrada.AddMinutes(azar.Next(20, 35));
            AgregarAcceso(azar, placasUsadas, EstacionamientoAlAzar(azar), CategoriaAlAzar(azar), entrada, salida, i + 1);
        }

        // ---------- Hoy: vehículos que siguen adentro ----------
        for (int i = 1; i <= VehiculosAdentroFacpya; i++)
        {
            DateTime entrada = ahora.AddMinutes(-azar.Next(5, ventanaDeMinutos));
            AgregarAcceso(azar, placasUsadas, Estacionamientos.Facpya, CategoriaAlAzar(azar), entrada, null, i);
        }
        for (int i = 1; i <= VehiculosAdentroEstadio; i++)
        {
            DateTime entrada = ahora.AddMinutes(-azar.Next(5, ventanaDeMinutos));
            AgregarAcceso(azar, placasUsadas, Estacionamientos.Estadio, CategoriaAlAzar(azar), entrada, null, i);
        }

        MarcarVehiculosOlvidados(ahora);
        ColocarDocenteDePruebaAdentro();
        AgregarPasesDelCongreso(azar, placasUsadas, ahora);

        GestorArchivos.GuardarUsuarios();
        GestorArchivos.GuardarAccesos();
        GestorArchivos.GuardarAccesosAnulados();
        GestorArchivos.GuardarEventos();
        GestorArchivos.GuardarPases();
        GestorArchivos.GuardarBitacora();
    }

    /// <summary>Usuarios de los 3 roles. Sus contraseñas se deben cambiar al primer inicio de sesión.</summary>
    private static List<Usuario> CrearUsuarios()
    {
        return new List<Usuario>
        {
            new Usuario { NombreUsuario = "admin", ContrasenaHash = SeguridadContrasenas.CalcularHash("admin123"), NombreCompleto = "Administrador ParkUANL",
                          Correo = "admin@uanl.edu.mx", Rol = Roles.Administrador, DebeCambiarContrasena = true },
            new Usuario { NombreUsuario = "operador", ContrasenaHash = SeguridadContrasenas.CalcularHash("operador123"), NombreCompleto = "Operador de Caseta",
                          Correo = "operador.caseta@uanl.edu.mx", Rol = Roles.Operador, DebeCambiarContrasena = true },
            new Usuario { NombreUsuario = "alumno", ContrasenaHash = SeguridadContrasenas.CalcularHash("alumno123"), NombreCompleto = "Ana Garza Treviño",
                          Correo = "ana.garzat@uanl.edu.mx", Matricula = "2077345", Placa = "SRT-458-A", Rol = Roles.Comunidad, Categoria = Categorias.Alumno,
                          DebeCambiarContrasena = true },
            new Usuario { NombreUsuario = "docente", ContrasenaHash = SeguridadContrasenas.CalcularHash("docente123"), NombreCompleto = "Roberto Martínez Salinas",
                          Correo = "roberto.martinezs@uanl.edu.mx", Matricula = "1045780", Placa = "RMS-221-B", Rol = Roles.Comunidad, Categoria = Categorias.Docente,
                          DebeCambiarContrasena = true }
        };
    }

    private static List<EventoCampus> CrearEventos(DateTime hoy)
    {
        return new List<EventoCampus>
        {
            new EventoCampus { Id = 1, Codigo = "congreso-gumersindo", Titulo = "Congreso Gumersindo Cantú Hinojosa",
                               Ubicacion = Estacionamientos.Facpya, FechaInicio = hoy.AddDays(-1).AddHours(7), FechaFin = hoy.AddDays(2).AddHours(20),
                               LugaresReservadosFacpya = 40, MaxVisitantes = 150, VigenciaHorasPase = 12 },
            new EventoCampus { Id = 2, Codigo = "feria-empleo-facpya", Titulo = "Feria del Empleo FACPYA",
                               Ubicacion = Estacionamientos.Facpya, FechaInicio = hoy.AddDays(-8).AddHours(9), FechaFin = hoy.AddDays(-8).AddHours(17),
                               LugaresReservadosFacpya = 30, MaxVisitantes = 100, VigenciaHorasPase = 8 },
            new EventoCampus { Id = 3, Codigo = "partido-tigres-femenil", Titulo = "Partido Tigres Femenil",
                               Ubicacion = Estacionamientos.Estadio, FechaInicio = hoy.AddDays(5).AddHours(18), FechaFin = hoy.AddDays(5).AddHours(22),
                               LugaresReservadosFacpya = 0, MaxVisitantes = 300, VigenciaHorasPase = 6 }
        };
    }

    /// <summary>Unos autos del Estadio llevan más de un día adentro (sirven para probar la alerta).</summary>
    private static void MarcarVehiculosOlvidados(DateTime ahora)
    {
        List<RegistroAcceso> accesos = GestorArchivos.Accesos;
        for (int i = 1; i <= VehiculosOlvidados; i++)
        {
            accesos[accesos.Count - i].FechaEntrada = ahora.AddHours(-(CalculadoraAforo.HorasParaConsiderarOlvidado + i * 6));
        }
    }

    /// <summary>El docente de prueba está adentro de FACPYA (sirve para preguntarle a la IA por la placa RMS-221-B).</summary>
    private static void ColocarDocenteDePruebaAdentro()
    {
        List<RegistroAcceso> accesos = GestorArchivos.Accesos;
        RegistroAcceso docente = accesos[accesos.Count - VehiculosAdentroEstadio - 20];
        docente.Placa = "RMS-221-B";
        docente.Nombre = "Roberto Martínez Salinas";
        docente.Categoria = Categorias.Docente;
        docente.Matricula = "1045780";
        docente.Correo = "roberto.martinezs@uanl.edu.mx";
        docente.Estacionamiento = Estacionamientos.Facpya;
        docente.Metodo = MetodosDeEntrada.QrUanl;
    }

    private static void AgregarPasesDelCongreso(Random azar, HashSet<string> placasUsadas, DateTime ahora)
    {
        // 12 visitantes adentro en cajón reservado.
        EventoCampus congreso = GestorArchivos.Eventos[0];
        for (int i = 1; i <= VisitantesEnCajonReservado; i++)
        {
            PaseVisitante pase = CrearPase(azar, placasUsadas, congreso, ahora.AddMinutes(-azar.Next(60, 240)));
            GestorArchivos.Pases.Add(pase);

            GestorArchivos.Accesos.Add(new RegistroAcceso
            {
                Id = GestorArchivos.SiguienteIdAcceso(),
                Estacionamiento = Estacionamientos.Facpya,
                Placa = pase.Placa,
                Correo = pase.Correo,
                Nombre = pase.NombreVisitante,
                Categoria = Categorias.Visitante,
                CodigoEvento = congreso.Codigo,
                IdPase = pase.Id,
                Metodo = MetodosDeEntrada.QrVisitante,
                Cajon = "F-R" + i.ToString("00"),
                UsaCajonReservado = true,
                FechaEntrada = pase.FechaEmision.AddMinutes(azar.Next(5, 50)),
                Operador = "operador"
            });
        }

        // 6 pases activos sin usar, 1 revocado y 1 expirado.
        for (int i = 0; i < 6; i++)
        {
            GestorArchivos.Pases.Add(CrearPase(azar, placasUsadas, congreso, ahora.AddMinutes(-azar.Next(10, 120))));
        }
        PaseVisitante revocado = CrearPase(azar, placasUsadas, congreso, ahora.AddHours(-2));
        revocado.Revocado = true;
        GestorArchivos.Pases.Add(revocado);
        GestorArchivos.Pases.Add(CrearPase(azar, placasUsadas, congreso, ahora.AddHours(-20)));

        // Pases de la feria (evento terminado): todos expirados.
        EventoCampus feria = GestorArchivos.Eventos[1];
        for (int i = 0; i < 5; i++)
        {
            GestorArchivos.Pases.Add(CrearPase(azar, placasUsadas, feria, feria.FechaInicio.AddMinutes(-azar.Next(30, 600))));
        }
    }

    private static string EstacionamientoAlAzar(Random azar)
    {
        return azar.Next(100) < 60 ? Estacionamientos.Facpya : Estacionamientos.Estadio;
    }

    private static string CategoriaAlAzar(Random azar)
    {
        int numero = azar.Next(100);
        if (numero < 70)
        {
            return Categorias.Alumno;
        }
        else if (numero < 90)
        {
            return Categorias.Docente;
        }
        else
        {
            return Categorias.Visitante;
        }
    }

    private static void AgregarAcceso(Random azar, HashSet<string> placasUsadas, string estacionamiento, string categoria,
        DateTime entrada, DateTime? salida, int numeroCajon)
    {
        string nombre = NombresDePila[azar.Next(NombresDePila.Length)] + " " + Apellidos[azar.Next(Apellidos.Length)] + " " + Apellidos[azar.Next(Apellidos.Length)];
        string matricula = "";
        string correo = "";
        if (categoria == Categorias.Alumno)
        {
            matricula = azar.Next(1800000, 2199999).ToString();
            correo = CrearCorreoUniversitario(nombre);
        }
        else if (categoria == Categorias.Docente)
        {
            matricula = azar.Next(1000000, 1099999).ToString();
            correo = CrearCorreoUniversitario(nombre);
        }

        string cajon = estacionamiento == Estacionamientos.Facpya ? "F-" + numeroCajon.ToString("000") : "E-" + numeroCajon.ToString("0000");
        string metodo = MetodosDeEntrada.Manual;
        if (categoria != Categorias.Visitante && azar.Next(2) == 0)
        {
            metodo = MetodosDeEntrada.QrUanl;
        }

        GestorArchivos.Accesos.Add(new RegistroAcceso
        {
            Id = GestorArchivos.SiguienteIdAcceso(),
            Estacionamiento = estacionamiento,
            Placa = CrearPlacaAlAzar(azar, placasUsadas),
            Matricula = matricula,
            Correo = correo,
            Nombre = nombre,
            Categoria = categoria,
            Metodo = metodo,
            Cajon = cajon,
            FechaEntrada = entrada,
            FechaSalida = salida,
            Operador = "operador"
        });
    }

    private static PaseVisitante CrearPase(Random azar, HashSet<string> placasUsadas, EventoCampus evento, DateTime emision)
    {
        string nombre = NombresDePila[azar.Next(NombresDePila.Length)] + " " + Apellidos[azar.Next(Apellidos.Length)];
        return new PaseVisitante
        {
            Id = GestorArchivos.SiguienteIdPase(),
            CodigoUnico = CodigoPaseVisitante.CrearCodigoUnico(),
            CodigoEvento = evento.Codigo,
            NombreVisitante = nombre,
            Correo = QuitarAcentosParaCorreo(nombre.Split(' ')[0]) + "." + azar.Next(100, 999) + "@gmail.com",
            Placa = CrearPlacaAlAzar(azar, placasUsadas),
            FechaEmision = emision,
            FechaExpiracion = emision.AddHours(evento.VigenciaHorasPase)
        };
    }

    /// <summary>"Sofía Cantú Treviño" → sofia.cantut@uanl.edu.mx: primer nombre, punto, primer apellido e inicial del segundo.</summary>
    private static string CrearCorreoUniversitario(string nombreCompleto)
    {
        string[] partes = QuitarAcentosParaCorreo(nombreCompleto).Split(' ');
        return partes[0] + "." + partes[1] + partes[2][0] + "@uanl.edu.mx";
    }

    /// <summary>Minúsculas y sin acentos ni ñ, para usar un nombre dentro de un correo ("Sofía" → "sofia").</summary>
    private static string QuitarAcentosParaCorreo(string texto)
    {
        return texto.ToLowerInvariant().Replace("á", "a").Replace("é", "e").Replace("í", "i").Replace("ó", "o").Replace("ú", "u").Replace("ñ", "n");
    }

    /// <summary>Placa con formato ABC-123 que no se repita.</summary>
    private static string CrearPlacaAlAzar(Random azar, HashSet<string> placasUsadas)
    {
        const string letras = "ABCDEFGHJKLMNPRSTUVWXYZ";
        while (true)
        {
            string placa = "" + letras[azar.Next(letras.Length)] + letras[azar.Next(letras.Length)] + letras[azar.Next(letras.Length)]
                           + "-" + azar.Next(100, 1000);
            if (placasUsadas.Add(placa))
            {
                return placa;
            }
        }
    }
}
