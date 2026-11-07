using System.Text;

namespace PIA_ParkUANL;

// =====================================================================================
//  GENERADOR DE CÓDIGO QR SIN LIBRERÍAS: calcula cada módulo y pinta directamente los píxeles.
//  Para mantenerlo corto se usa siempre la misma configuración del estándar QR:
//  Versión 5 (37 x 37 módulos), corrección de errores nivel L, modo byte y máscara 0.
//  Capacidad: hasta 106 caracteres (suficiente para el token UANL y el pase de visitante).
// =====================================================================================
public static class GeneradorQR
{
    private const int LadoEnModulos = 37;               // 17 + 4 × versión(5)
    private const int BytesDeDatos = 108;      // Versión 5-L: 108 bytes de datos...
    private const int BytesDeCorreccion = 26;  // ...y 26 bytes de corrección Reed-Solomon (un solo bloque)
    public const int MaximoCaracteres = 106;

    /// <summary>
    /// Pinta el QR en una lista de píxeles (azul, verde, rojo, alfa): cada módulo oscuro es un cuadrito
    /// negro de "pixelesPorModulo". En "lado" regresa el ancho y alto de la imagen.
    /// </summary>
    public static byte[] CrearPixeles(string texto, int pixelesPorModulo, out int lado)
    {
        const int bytesPorPixel = 4;
        const int margen = 4;   // zona blanca obligatoria alrededor del código

        bool[,] modulos = CrearMatriz(texto);
        lado = (LadoEnModulos + margen * 2) * pixelesPorModulo;

        byte[] pixeles = new byte[lado * lado * bytesPorPixel];
        Array.Fill(pixeles, (byte)255);   // todo blanco y opaco

        for (int y = 0; y < lado; y++)
        {
            for (int x = 0; x < lado; x++)
            {
                int fila = y / pixelesPorModulo - margen;
                int columna = x / pixelesPorModulo - margen;
                bool estaDentroDelCodigo = fila >= 0 && fila < LadoEnModulos && columna >= 0 && columna < LadoEnModulos;
                if (estaDentroDelCodigo && modulos[fila, columna])
                {
                    int posicion = (y * lado + x) * bytesPorPixel;
                    pixeles[posicion] = 0;       // azul
                    pixeles[posicion + 1] = 0;   // verde
                    pixeles[posicion + 2] = 0;   // rojo
                }
            }
        }
        return pixeles;
    }

    /// <summary>Matriz de módulos: true = negro.</summary>
    private static bool[,] CrearMatriz(string texto)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(texto);
        if (bytes.Length > MaximoCaracteres)
        {
            throw new ArgumentException("El texto es demasiado largo para el QR (máximo " + MaximoCaracteres + " caracteres).");
        }

        bool[,] modulos = new bool[LadoEnModulos, LadoEnModulos];
        bool[,] esModuloFijo = new bool[LadoEnModulos, LadoEnModulos];   // módulos de patrones que no llevan datos

        DibujarPatronesFijos(modulos, esModuloFijo);
        DibujarInformacionDeFormato(modulos, esModuloFijo);

        byte[] datos = CodificarDatos(bytes);
        byte[] correccion = CalcularReedSolomon(datos);
        byte[] bytesCompletos = new byte[datos.Length + correccion.Length];
        datos.CopyTo(bytesCompletos, 0);
        correccion.CopyTo(bytesCompletos, datos.Length);

        ColocarDatosConMascara(bytesCompletos, modulos, esModuloFijo);
        return modulos;
    }

    private static void PonerModuloFijo(bool[,] modulos, bool[,] esModuloFijo, int x, int y, bool oscuro)
    {
        modulos[y, x] = oscuro;
        esModuloFijo[y, x] = true;
    }

    private static void DibujarPatronesFijos(bool[,] modulos, bool[,] esModuloFijo)
    {
        // Líneas de sincronización (alternan negro/blanco en la fila 6 y la columna 6).
        for (int i = 0; i < LadoEnModulos; i++)
        {
            PonerModuloFijo(modulos, esModuloFijo, 6, i, i % 2 == 0);
            PonerModuloFijo(modulos, esModuloFijo, i, 6, i % 2 == 0);
        }

        // Tres cuadros de posición (esquinas) con su borde blanco.
        int[,] centros = { { 3, 3 }, { LadoEnModulos - 4, 3 }, { 3, LadoEnModulos - 4 } };
        for (int c = 0; c < 3; c++)
        {
            for (int desfaseY = -4; desfaseY <= 4; desfaseY++)
            {
                for (int desfaseX = -4; desfaseX <= 4; desfaseX++)
                {
                    int x = centros[c, 0] + desfaseX;
                    int y = centros[c, 1] + desfaseY;
                    if (x >= 0 && x < LadoEnModulos && y >= 0 && y < LadoEnModulos)
                    {
                        int distanciaAlCentro = Math.Max(Math.Abs(desfaseX), Math.Abs(desfaseY));
                        PonerModuloFijo(modulos, esModuloFijo, x, y, distanciaAlCentro != 2 && distanciaAlCentro != 4);
                    }
                }
            }
        }

        // Patrón de alineación de la versión 5 (centro en 30, 30).
        for (int desfaseY = -2; desfaseY <= 2; desfaseY++)
        {
            for (int desfaseX = -2; desfaseX <= 2; desfaseX++)
            {
                int distanciaAlCentro = Math.Max(Math.Abs(desfaseX), Math.Abs(desfaseY));
                PonerModuloFijo(modulos, esModuloFijo, 30 + desfaseX, 30 + desfaseY, distanciaAlCentro != 1);
            }
        }
    }

    /// <summary>15 bits que indican nivel de corrección (L) y máscara (0), protegidos con un código BCH.</summary>
    private static void DibujarInformacionDeFormato(bool[,] modulos, bool[,] esModuloFijo)
    {
        int datosFormato = (1 << 3) | 0;   // nivel L = 01, máscara 0 = 000
        int residuo = datosFormato;
        for (int i = 0; i < 10; i++)
        {
            residuo = (residuo << 1) ^ ((residuo >> 9) * 0x537);
        }
        int bits = ((datosFormato << 10) | residuo) ^ 0x5412;

        // Primera copia (alrededor del cuadro superior izquierdo).
        for (int i = 0; i <= 5; i++)
        {
            PonerModuloFijo(modulos, esModuloFijo, 8, i, ObtenerBit(bits, i));
        }
        PonerModuloFijo(modulos, esModuloFijo, 8, 7, ObtenerBit(bits, 6));
        PonerModuloFijo(modulos, esModuloFijo, 8, 8, ObtenerBit(bits, 7));
        PonerModuloFijo(modulos, esModuloFijo, 7, 8, ObtenerBit(bits, 8));
        for (int i = 9; i < 15; i++)
        {
            PonerModuloFijo(modulos, esModuloFijo, 14 - i, 8, ObtenerBit(bits, i));
        }

        // Segunda copia (junto a los otros dos cuadros).
        for (int i = 0; i < 8; i++)
        {
            PonerModuloFijo(modulos, esModuloFijo, LadoEnModulos - 1 - i, 8, ObtenerBit(bits, i));
        }
        for (int i = 8; i < 15; i++)
        {
            PonerModuloFijo(modulos, esModuloFijo, 8, LadoEnModulos - 15 + i, ObtenerBit(bits, i));
        }
        PonerModuloFijo(modulos, esModuloFijo, 8, LadoEnModulos - 8, true);   // módulo oscuro obligatorio
    }

    private static bool ObtenerBit(int valor, int posicion)
    {
        return ((valor >> posicion) & 1) != 0;
    }

    /// <summary>Modo byte: 0100 + longitud (8 bits) + bytes + terminador + relleno (0xEC, 0x11).</summary>
    private static byte[] CodificarDatos(byte[] bytes)
    {
        List<bool> bitsDatos = new List<bool>();
        AgregarBits(bitsDatos, 0b0100, 4);
        AgregarBits(bitsDatos, bytes.Length, 8);
        foreach (byte valorByte in bytes)
        {
            AgregarBits(bitsDatos, valorByte, 8);
        }

        int capacidadBits = BytesDeDatos * 8;
        AgregarBits(bitsDatos, 0, Math.Min(4, capacidadBits - bitsDatos.Count));
        while (bitsDatos.Count % 8 != 0)
        {
            bitsDatos.Add(false);
        }

        byte[] resultado = new byte[BytesDeDatos];
        int indice = 0;
        for (int i = 0; i < bitsDatos.Count; i += 8)
        {
            int valor = 0;
            for (int j = 0; j < 8; j++)
            {
                valor = (valor << 1) | (bitsDatos[i + j] ? 1 : 0);
            }
            resultado[indice] = (byte)valor;
            indice++;
        }

        bool alternar = true;
        while (indice < BytesDeDatos)
        {
            resultado[indice] = alternar ? (byte)0xEC : (byte)0x11;
            alternar = !alternar;
            indice++;
        }
        return resultado;
    }

    private static void AgregarBits(List<bool> lista, int valor, int cantidad)
    {
        for (int i = cantidad - 1; i >= 0; i--)
        {
            lista.Add(((valor >> i) & 1) != 0);
        }
    }

    /// <summary>Bytes de corrección Reed-Solomon (permiten leer el QR aunque esté un poco dañado).</summary>
    private static byte[] CalcularReedSolomon(byte[] datos)
    {
        // Polinomio generador de grado 26.
        byte[] generador = new byte[BytesDeCorreccion];
        generador[BytesDeCorreccion - 1] = 1;
        int raiz = 1;
        for (int i = 0; i < BytesDeCorreccion; i++)
        {
            for (int j = 0; j < generador.Length; j++)
            {
                generador[j] = MultiplicarEnCampoGalois(generador[j], raiz);
                if (j + 1 < generador.Length)
                {
                    generador[j] ^= generador[j + 1];
                }
            }
            raiz = MultiplicarEnCampoGalois(raiz, 0x02);
        }

        // Residuo de dividir los datos entre el generador.
        byte[] residuo = new byte[BytesDeCorreccion];
        foreach (byte valorByte in datos)
        {
            int factor = valorByte ^ residuo[0];
            Array.Copy(residuo, 1, residuo, 0, residuo.Length - 1);
            residuo[residuo.Length - 1] = 0;
            for (int i = 0; i < residuo.Length; i++)
            {
                residuo[i] ^= MultiplicarEnCampoGalois(generador[i], factor);
            }
        }
        return residuo;
    }

    /// <summary>Multiplicación en el campo de Galois GF(256) usado por el estándar QR.</summary>
    private static byte MultiplicarEnCampoGalois(int x, int y)
    {
        int z = 0;
        for (int i = 7; i >= 0; i--)
        {
            z = (z << 1) ^ ((z >> 7) * 0x11D);
            z ^= ((y >> i) & 1) * x;
        }
        return (byte)z;
    }

    /// <summary>Recorre la matriz en zigzag (columnas de 2, de abajo hacia arriba y viceversa) y aplica la máscara 0.</summary>
    private static void ColocarDatosConMascara(byte[] bytesCompletos, bool[,] modulos, bool[,] esModuloFijo)
    {
        int bitActual = 0;
        int totalBits = bytesCompletos.Length * 8;

        for (int derecha = LadoEnModulos - 1; derecha >= 1; derecha -= 2)
        {
            if (derecha == 6)
            {
                derecha = 5;   // se salta la columna de sincronización
            }
            for (int vertical = 0; vertical < LadoEnModulos; vertical++)
            {
                for (int j = 0; j < 2; j++)
                {
                    int x = derecha - j;
                    bool haciaArriba = ((derecha + 1) & 2) == 0;
                    int y = haciaArriba ? LadoEnModulos - 1 - vertical : vertical;
                    if (esModuloFijo[y, x] == false && bitActual < totalBits)
                    {
                        modulos[y, x] = ((bytesCompletos[bitActual >> 3] >> (7 - (bitActual & 7))) & 1) != 0;
                        bitActual++;
                    }
                }
            }
        }

        // Máscara 0: se invierten los módulos de datos donde (x + y) es par.
        for (int y = 0; y < LadoEnModulos; y++)
        {
            for (int x = 0; x < LadoEnModulos; x++)
            {
                if (esModuloFijo[y, x] == false && (x + y) % 2 == 0)
                {
                    modulos[y, x] = !modulos[y, x];
                }
            }
        }
    }
}
