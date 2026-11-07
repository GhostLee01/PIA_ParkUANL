# ParkUANL Lite

Control de acceso de los estacionamientos gratuitos de la UANL (FACPYA y Estadio).
Aplicación de escritorio en C# con WinUI 3. Guarda todo en archivos `.json`, sin base de datos.

## Cómo ejecutarla

Se necesita Windows 10 (versión 1809) o más reciente, de 64 bits, y el [SDK de .NET 10](https://dotnet.microsoft.com/download).
No hace falta instalar nada más: WinUI se descarga con NuGet y se copia junto al `.exe`.

```
dotnet run
```

También se puede abrir `PIA_ParkUANL_Lite.csproj` en Visual Studio y presionar F5.

Conviene que la carpeta del proyecto tenga una ruta corta: con una ruta muy larga, la compilación de WinUI falla
al copiar sus archivos (límite de 260 caracteres de Windows).

La primera vez se crean datos de prueba con estos usuarios. Al entrar, cada uno debe cambiar su contraseña.

| Usuario    | Contraseña inicial | Rol                                | Pantalla           |
|------------|--------------------|------------------------------------|--------------------|
| `admin`    | `admin123`         | Administrador                      | Panel de control   |
| `operador` | `operador123`      | Operador                           | Caseta de acceso   |
| `alumno`   | `alumno123`        | Comunidad (alumno)                 | Mi pase QR         |
| `docente`  | `docente123`       | Comunidad (docente)                | Mi pase QR         |

## Dónde se guardan los datos

En `%LocalAppData%\ParkUANL\Data`. Si existe una carpeta `Data` de una versión anterior junto al `.exe`,
sus archivos `.json` se copian ahí la primera vez.

Para volver a los datos de prueba: cierra la app y borra esa carpeta.

| Archivo                 | Contenido                                                     |
|-------------------------|---------------------------------------------------------------|
| `usuarios.json`         | Cuentas. Las contraseñas se guardan con hash y sal.           |
| `accesos.json`          | Entradas y salidas.                                           |
| `accesos_anulados.json` | Registros anulados (ya no cuentan en el aforo).               |
| `eventos.json`          | Eventos del campus.                                           |
| `pases_visitantes.json` | Pases de visitante.                                           |
| `bitacora.json`         | Quién hizo cada corrección o cambio, cuándo y por qué.        |
| `clave_firma.dat`       | Clave con la que se firman los QR (cifrada).                  |
| `*.respaldo`            | Versión anterior de cada archivo, por si alguno se daña.      |
| `errores.log`           | Errores inesperados.                                          |

La clave de firma se cifra para el usuario de Windows que la creó, así que no sirve en otra computadora.
Si se copia la carpeta a otro equipo, los QR emitidos dejan de ser válidos.

## Organización del código

| Carpeta        | Qué contiene                                                                        |
|----------------|-------------------------------------------------------------------------------------|
| `Modelos/`     | Las clases de datos y las constantes (roles, categorías, estacionamientos).         |
| `Datos/`       | Lectura y guardado de archivos, y los datos de prueba.                              |
| `Logica/`      | Las reglas del sistema, sin pantallas. Aquí están las fórmulas de aforo.            |
| `Asistente/`   | El asistente: interpretar la pregunta, el modelo ML.NET y las respuestas.           |
| `VistasModelo/`| Lo que hace cada pantalla (MVVM), sin depender de WinUI: se prueba con xUnit.        |
| `Pantallas/`   | XAML, controles propios y servicios de interfaz. El code-behind solo es visual.     |

Dentro de `Logica/`, los métodos que validan regresan `""` si todo salió bien, o el mensaje de error.

Para ubicar una regla:

| Busco...                                    | Archivo                              |
|---------------------------------------------|--------------------------------------|
| Fórmulas de aforo, estancia y semáforo      | `Logica/CalculadoraAforo.cs`         |
| Quién puede entrar y qué cajón le toca      | `Logica/ServicioCaseta.cs`           |
| Inicio de sesión, contraseñas y vehículos   | `Logica/ServicioCuentas.cs`          |
| Firma y validación de los QR                | `Logica/CodigosDeAcceso.cs`          |
| Anular o corregir movimientos               | `Logica/CorreccionMovimientos.cs`    |
| Reservas y vigencia de pases                | `Logica/ReglasEventos.cs`            |
| Reportes CSV y PDF, cierre de turno         | `Logica/ReportesAccesos.cs`          |
| Pronóstico de ocupación                     | `Logica/PronosticoOcupacion.cs`      |

Los comentarios `=== [RÚBRICA n] ===` marcan dónde se cumple cada punto de la rúbrica.

## Asistente

Funciona en el equipo, sin internet. Responde con estas fuentes, en orden:

1. Reglas para datos concretos: placa, cajón y matrícula.
2. Un modelo ML.NET que clasifica la intención de la pregunta. Se entrena con `Entrenamiento/preguntas_entrenamiento.csv`.
3. Reglas de palabras clave, si el modelo no está seguro.

Para que entienda mejor una pregunta, agrégala al CSV con su intención y presiona "Reentrenar modelo".
