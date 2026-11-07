using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace PIA_ParkUANL_Lite;

/// <summary>Barra de ocupación: el ancho de color = % de ocupación, el color = semáforo.</summary>
public sealed partial class BarraOcupacion : UserControl
{
    private const double PorcentajeTotal = 100.0;

    public static readonly DependencyProperty ResumenProperty =
        DependencyProperty.Register(nameof(Resumen), typeof(ResumenAforo), typeof(BarraOcupacion), new PropertyMetadata(null, (control, _) => ((BarraOcupacion)control).Mostrar()));

    private readonly ColumnDefinition parteLlena = new ColumnDefinition { Width = new GridLength(0, GridUnitType.Star) };
    private readonly ColumnDefinition parteVacia = new ColumnDefinition { Width = new GridLength(PorcentajeTotal, GridUnitType.Star) };
    private readonly Rectangle relleno = new Rectangle();
    private readonly TextBlock texto = new TextBlock
    {
        FontWeight = FontWeights.SemiBold,
        FontSize = 13,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center
    };

    public ResumenAforo? Resumen
    {
        get { return GetValue(ResumenProperty) as ResumenAforo; }
        set { SetValue(ResumenProperty, value); }
    }

    public BarraOcupacion()
    {
        Grid fondo = new Grid { Height = 24, Background = Application.Current.Resources["BarraVacia"] as Brush };
        fondo.ColumnDefinitions.Add(parteLlena);
        fondo.ColumnDefinitions.Add(parteVacia);
        fondo.Children.Add(relleno);
        Grid.SetColumnSpan(texto, 2);
        fondo.Children.Add(texto);
        Content = fondo;
    }

    private void Mostrar()
    {
        ResumenAforo? resumen = Resumen;
        if (resumen == null)
        {
            return;
        }

        double porcentaje = Math.Clamp(resumen.PorcentajeOcupacion, 0.0, PorcentajeTotal);
        parteLlena.Width = new GridLength(porcentaje, GridUnitType.Star);
        parteVacia.Width = new GridLength(PorcentajeTotal - porcentaje, GridUnitType.Star);
        relleno.Fill = Tema.ColorSemaforo(resumen.Semaforo);
        texto.Text = resumen.PorcentajeOcupacion + " %  (" + resumen.Semaforo + ")";
    }
}
