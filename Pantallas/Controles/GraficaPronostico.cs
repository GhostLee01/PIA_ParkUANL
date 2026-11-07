using System.Globalization;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace PIA_ParkUANL;

/// <summary>Gráfica de barras del pronóstico: la altura = % de ocupación estimado, el color = semáforo.</summary>
public sealed partial class GraficaPronostico : UserControl
{
    private const int PrimeraHora = 6;
    private const int UltimaHora = 22;
    private const double MargenInferior = 26;
    private const double MargenSuperior = 44;
    private const double AlturaTextoPequeno = 16;
    private const double TamanoTextoPequeno = 10;
    private const double TamanoTitulo = 13;
    private const double EspacioEntreBarras = 6;

    public static readonly DependencyProperty PronosticoProperty =
        DependencyProperty.Register(nameof(Pronostico), typeof(PronosticoDelDia), typeof(GraficaPronostico), new PropertyMetadata(null, (control, _) => ((GraficaPronostico)control).Dibujar()));

    private readonly Canvas lienzo = new Canvas();

    public PronosticoDelDia? Pronostico
    {
        get { return GetValue(PronosticoProperty) as PronosticoDelDia; }
        set { SetValue(PronosticoProperty, value); }
    }

    public GraficaPronostico()
    {
        lienzo.Background = Tema.Blanco;
        lienzo.SizeChanged += (_, _) => Dibujar();
        Content = lienzo;
    }

    private void Dibujar()
    {
        lienzo.Children.Clear();
        PronosticoDelDia? pronostico = Pronostico;
        if (pronostico == null || lienzo.ActualWidth <= 0 || lienzo.ActualHeight <= MargenInferior + MargenSuperior)
        {
            return;
        }

        CultureInfo espanol = new CultureInfo("es-MX");
        string descripcion = pronostico.EsOcupacionReal ? "% de ocupación real por hora" : "% de ocupación estimada por hora";
        AgregarTexto(pronostico.Estacionamiento + " · " + pronostico.Dia.ToString("dddd dd/MM/yyyy", espanol) + " · " + descripcion,
            8, 6, lienzo.ActualWidth - 16, TamanoTitulo, Tema.AzulUanl, FontWeights.SemiBold, TextAlignment.Left);

        int numeroBarras = UltimaHora - PrimeraHora + 1;
        double anchoBarra = lienzo.ActualWidth / numeroBarras;
        double alturaDisponible = lienzo.ActualHeight - MargenInferior - MargenSuperior;
        double lineaBase = lienzo.ActualHeight - MargenInferior;

        // Escala: la barra más alta llena la gráfica, para que se note la diferencia entre horas aunque haya pocos autos.
        double porcentajeMaximo = Math.Max(1.0, pronostico.Horas.Max(punto => punto.PorcentajeOcupacion));

        for (int hora = PrimeraHora; hora <= UltimaHora && hora < pronostico.Horas.Count; hora++)
        {
            PuntoPronostico punto = pronostico.Horas[hora];
            double x = (hora - PrimeraHora) * anchoBarra;
            double altura = alturaDisponible * punto.PorcentajeOcupacion / porcentajeMaximo;

            Rectangle barra = new Rectangle { Width = Math.Max(anchoBarra - EspacioEntreBarras, 1), Height = altura, Fill = Tema.ColorSemaforo(punto.Semaforo) };
            Canvas.SetLeft(barra, x + EspacioEntreBarras / 2);
            Canvas.SetTop(barra, lineaBase - altura);
            lienzo.Children.Add(barra);

            AgregarTexto(hora.ToString(), x, lineaBase + 4, anchoBarra, TamanoTextoPequeno, Tema.GrisTexto, FontWeights.Normal, TextAlignment.Center);
            AgregarTexto(Math.Round(punto.PorcentajeOcupacion).ToString(), x, lineaBase - altura - AlturaTextoPequeno, anchoBarra, TamanoTextoPequeno, Tema.GrisTexto, FontWeights.Normal, TextAlignment.Center);
        }
    }

    private void AgregarTexto(string texto, double x, double y, double ancho, double tamano, Brush color, Windows.UI.Text.FontWeight grosor, TextAlignment alineacion)
    {
        TextBlock etiqueta = new TextBlock { Text = texto, Width = ancho, FontSize = tamano, Foreground = color, FontWeight = grosor, TextAlignment = alineacion };
        Canvas.SetLeft(etiqueta, x);
        Canvas.SetTop(etiqueta, y);
        lienzo.Children.Add(etiqueta);
    }
}
