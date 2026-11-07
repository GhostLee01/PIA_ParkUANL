using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Transforms.Text;

namespace PIA_ParkUANL_Lite;

// =====================================================================================
//  CLASIFICADORINTENCION.CS
//  Modelo de aprendizaje automático (ML.NET) que lee una pregunta y predice su INTENCIÓN
//  ("aforo", "ultima_entrada", "buscar_nombre"...). Se entrena con las preguntas de ejemplo
//  de Entrenamiento/preguntas_entrenamiento.csv.
//
//  Pasos del pipeline:
//    1) MapValueToKey  → convierte el texto de la intención en un número (la "etiqueta").
//    2) FeaturizeText  → convierte la pregunta en números (palabras, pares de palabras y fragmentos de 4 letras).
//    3) SdcaMaximumEntropy → algoritmo que aprende a clasificar en varias categorías.
//    4) MapKeyToValue  → convierte el número predicho otra vez en el texto de la intención.
// =====================================================================================

// === [EXENCIÓN] AGENTE ANALÍTICO DE INTELIGENCIA ARTIFICIAL ===

/// <summary>Una fila del archivo de entrenamiento: la pregunta y su intención.</summary>
public class EjemploPregunta
{
    public string Texto { get; set; } = "";
    public string Intencion { get; set; } = "";
}

/// <summary>Lo que regresa el modelo: la intención predicha y la probabilidad de cada intención.</summary>
public class PrediccionIntencion
{
    [ColumnName("PredictedLabel")]
    public string Intencion { get; set; } = "";

    public float[] Score { get; set; } = Array.Empty<float>();
}

public static class ClasificadorIntencion
{
    private static readonly string RutaPreguntas = Path.Combine(AppContext.BaseDirectory, "Entrenamiento", "preguntas_entrenamiento.csv");
    private static string RutaModelo
    {
        get { return Path.Combine(GestorArchivos.CarpetaDatos, "modelo_intencion.zip"); }
    }

    private static string RutaMetricas
    {
        get { return Path.Combine(GestorArchivos.CarpetaDatos, "metricas_modelo.txt"); }
    }

    // Semilla fija: entrenar con los mismos datos siempre da el mismo modelo.
    private static readonly MLContext contextoML = new MLContext(seed: 2026);

    // El PredictionEngine NO es seguro para usarse desde varios hilos a la vez: por eso se usa con lock.
    private static PredictionEngine<EjemploPregunta, PrediccionIntencion>? motorPrediccion;
    private static readonly object bloqueoPrediccion = new object();
    private static readonly object bloqueoEntrenamiento = new object();

    /// <summary>Texto con las métricas del modelo (se muestra en la pestaña del asistente).</summary>
    public static string Metricas { get; private set; } = "El modelo todavía no se ha cargado.";

    /// <summary>Último error al entrenar, cargar o predecir ("" si no hubo error).</summary>
    public static string Error { get; private set; } = "";

    /// <summary>true cuando el modelo ya está listo para predecir.</summary>
    public static bool EstaListo
    {
        get { return motorPrediccion != null; }
    }

    /// <summary>
    /// Carga el modelo guardado si existe y es más nuevo que el CSV; si no, entrena uno nuevo.
    /// Tarda unos segundos: llamarlo con Task.Run para no congelar la ventana.
    /// </summary>
    public static void Inicializar()
    {
        lock (bloqueoEntrenamiento)
        {
            try
            {
                Error = "";
                if (File.Exists(RutaPreguntas) == false)
                {
                    throw new FileNotFoundException("No se encontró el archivo de entrenamiento: " + RutaPreguntas);
                }

                bool hayModeloGuardado = File.Exists(RutaModelo);
                if (hayModeloGuardado && File.GetLastWriteTime(RutaModelo) > File.GetLastWriteTime(RutaPreguntas))
                {
                    CargarOReentrenarSiEstaDanado();
                }
                else
                {
                    Entrenar();
                }
            }
            catch (Exception ex) when (EsFalloDelModelo(ex))
            {
                // Si algo falla, la app sigue funcionando con las reglas de palabras clave.
                Error = ex.Message;
                Metricas = "No se pudo preparar el modelo: " + ex.Message;
            }
        }
    }

    /// <summary>Borra el modelo guardado y entrena uno nuevo con el CSV actual.</summary>
    public static void Reentrenar()
    {
        lock (bloqueoEntrenamiento)
        {
            try
            {
                if (File.Exists(RutaModelo))
                {
                    File.Delete(RutaModelo);
                }
            }
            catch (Exception ex) when (EsFalloDelModelo(ex))
            {
                Error = ex.Message;
            }
            Inicializar();
        }
    }

    /// <summary>Predice la intención de una pregunta y su confianza (0 a 1). Si el modelo no está listo regresa ("", 0).</summary>
    public static (string Intencion, float Confianza) Predecir(string texto)
    {
        lock (bloqueoPrediccion)
        {
            if (motorPrediccion == null)
            {
                return ("", 0f);
            }

            try
            {
                PrediccionIntencion prediccion = motorPrediccion.Predict(new EjemploPregunta { Texto = texto });

                // La confianza es la probabilidad más alta entre todas las intenciones.
                float confianza = 0f;
                foreach (float probabilidad in prediccion.Score)
                {
                    if (probabilidad > confianza)
                    {
                        confianza = probabilidad;
                    }
                }
                return (prediccion.Intencion, confianza);
            }
            catch (Exception ex) when (EsFalloDelModelo(ex))
            {
                Error = ex.Message;
                return ("", 0f);
            }
        }
    }

    // ----------------------------- Entrenamiento -----------------------------

    private static void Entrenar()
    {
        List<EjemploPregunta> ejemplos = LeerPreguntasDeEntrenamiento();
        if (ejemplos.Count == 0)
        {
            throw new InvalidOperationException("El archivo de entrenamiento no tiene preguntas.");
        }

        IDataView datos = contextoML.Data.LoadFromEnumerable(ejemplos);

        // Cómo se convierte el texto en números (se eligió comparando varias opciones con validación cruzada):
        TextFeaturizingEstimator.Options opcionesTexto = new TextFeaturizingEstimator.Options
        {
            CaseMode = TextNormalizingEstimator.CaseMode.Lower,   // "FACPYA" y "facpya" cuentan igual
            KeepDiacritics = false,                               // "último" y "ultimo" cuentan igual
            KeepPunctuations = false,                             // se ignoran ¿ ? ¡ ! , .
            KeepNumbers = true,
            // Palabras sueltas y pares de palabras seguidas ("ultimo", "en salir", "ultimo en"...).
            WordFeatureExtractor = new WordBagEstimator.Options { NgramLength = 2, UseAllLengths = true },
            // Fragmentos de 4 letras ("ulti", "ltim"...): ayudan con faltas de ortografía y palabras parecidas.
            CharFeatureExtractor = new WordBagEstimator.Options { NgramLength = 4, UseAllLengths = false },
            Norm = TextFeaturizingEstimator.NormFunction.L2
        };

        var pipeline = contextoML.Transforms.Conversion.MapValueToKey("Label", "Intencion")
            .Append(contextoML.Transforms.Text.FeaturizeText("Features", opcionesTexto, "Texto"))
            .Append(contextoML.MulticlassClassification.Trainers.SdcaMaximumEntropy("Label", "Features"))
            .Append(contextoML.Transforms.Conversion.MapKeyToValue("PredictedLabel"));

        // 1) Evaluación con validación cruzada de 5 pliegues: se entrena 5 veces con el 80 % de los
        //    datos y se prueba con el 20 % restante; las métricas son el promedio de las 5 pruebas.
        var resultados = contextoML.MulticlassClassification.CrossValidate(datos, pipeline, numberOfFolds: 5, labelColumnName: "Label");

        double sumaMicro = 0;
        double sumaMacro = 0;
        double sumaLogLoss = 0;
        foreach (var resultado in resultados)
        {
            sumaMicro = sumaMicro + resultado.Metrics.MicroAccuracy;
            sumaMacro = sumaMacro + resultado.Metrics.MacroAccuracy;
            sumaLogLoss = sumaLogLoss + resultado.Metrics.LogLoss;
        }
        double exactitudMicro = sumaMicro / resultados.Count;
        double exactitudMacro = sumaMacro / resultados.Count;
        double logLoss = sumaLogLoss / resultados.Count;

        int numeroIntenciones = ejemplos.Select(ejemplo => ejemplo.Intencion).Distinct().Count();

        Metricas = "Ejemplos de entrenamiento: " + ejemplos.Count + "\n" +
                   "Intenciones: " + numeroIntenciones + "\n" +
                   "Exactitud micro: " + (exactitudMicro * 100).ToString("0.0") + " %\n" +
                   "Exactitud macro: " + (exactitudMacro * 100).ToString("0.0") + " %\n" +
                   "Log-loss: " + logLoss.ToString("0.000") + "\n" +
                   "(validación cruzada de 5 pliegues)\n" +
                   "Entrenado: " + DateTime.Now.ToString("dd/MM/yyyy HH:mm");

        Directory.CreateDirectory(GestorArchivos.CarpetaDatos);
        File.WriteAllText(RutaMetricas, Metricas);

        // 2) Modelo final: se entrena con TODAS las preguntas y se guarda para no reentrenar cada vez.
        ITransformer modelo = pipeline.Fit(datos);
        contextoML.Model.Save(modelo, datos.Schema, RutaModelo);
        CrearMotorPrediccion(modelo);
    }

    private static void CargarOReentrenarSiEstaDanado()
    {
        try
        {
            CargarModelo();
        }
        catch (Exception ex) when (EsFalloDelModelo(ex))
        {
            // Con un .zip dañado basta con entrenar otro; el motivo queda en errores.log.
            GestorArchivos.RegistrarError(ex);
            Entrenar();
        }
    }

    /// <summary>Errores esperables de ML.NET o del disco. Cualquier otro es un defecto y debe llegar al manejador global.</summary>
    private static bool EsFalloDelModelo(Exception error)
    {
        return GestorArchivos.EsErrorDeDisco(error) || error is InvalidOperationException || error is ArgumentException
            || error is FormatException || error is OperationCanceledException;
    }

    private static void CargarModelo()
    {
        ITransformer modelo = contextoML.Model.Load(RutaModelo, out _);
        CrearMotorPrediccion(modelo);

        if (File.Exists(RutaMetricas))
        {
            Metricas = File.ReadAllText(RutaMetricas);
        }
        else
        {
            Metricas = "Modelo cargado desde disco (sin archivo de métricas).";
        }
    }

    private static void CrearMotorPrediccion(ITransformer modelo)
    {
        PredictionEngine<EjemploPregunta, PrediccionIntencion> nuevoMotor =
            contextoML.Model.CreatePredictionEngine<EjemploPregunta, PrediccionIntencion>(modelo);

        lock (bloqueoPrediccion)
        {
            motorPrediccion?.Dispose();
            motorPrediccion = nuevoMotor;
        }
    }

    /// <summary>
    /// Lee el CSV línea por línea. Se ignoran las líneas vacías, los comentarios (#) y el encabezado.
    /// El texto y la intención se separan por la ÚLTIMA coma (la pregunta puede tener comas).
    /// </summary>
    private static List<EjemploPregunta> LeerPreguntasDeEntrenamiento()
    {
        List<EjemploPregunta> ejemplos = new List<EjemploPregunta>();
        string[] lineas = File.ReadAllLines(RutaPreguntas);

        foreach (string lineaOriginal in lineas)
        {
            string linea = lineaOriginal.Trim();
            if (linea == "" || linea.StartsWith("#") || linea == "Texto,Intencion")
            {
                continue;
            }

            int posicionComa = linea.LastIndexOf(',');
            if (posicionComa <= 0)
            {
                continue;   // línea sin coma: no tiene intención
            }

            string texto = linea.Substring(0, posicionComa).Trim().Trim('"');
            string intencion = linea.Substring(posicionComa + 1).Trim();
            if (texto != "" && intencion != "")
            {
                ejemplos.Add(new EjemploPregunta { Texto = texto, Intencion = intencion });
            }
        }
        return ejemplos;
    }
}
