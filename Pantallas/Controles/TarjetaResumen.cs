using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PIA_ParkUANL_Lite;

/// <summary>Tarjeta del panel: título, valor grande, detalle y una franja con el color del semáforo.</summary>
public sealed partial class TarjetaResumen : UserControl
{
    private const double GrosorFranja = 6;

    public static readonly DependencyProperty TituloProperty =
        DependencyProperty.Register(nameof(Titulo), typeof(string), typeof(TarjetaResumen), new PropertyMetadata("", (control, _) => ((TarjetaResumen)control).Mostrar()));

    public static readonly DependencyProperty DatosProperty =
        DependencyProperty.Register(nameof(Datos), typeof(DatosDeTarjeta), typeof(TarjetaResumen), new PropertyMetadata(null, (control, _) => ((TarjetaResumen)control).Mostrar()));

    private readonly Border franja = new Border { Width = GrosorFranja, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly TextBlock etiquetaTitulo = new TextBlock();
    private readonly TextBlock etiquetaValor = new TextBlock { FontSize = 21, FontWeight = FontWeights.Bold };
    private readonly TextBlock etiquetaDetalle = new TextBlock();

    public string Titulo
    {
        get { return (string)GetValue(TituloProperty); }
        set { SetValue(TituloProperty, value); }
    }

    public DatosDeTarjeta? Datos
    {
        get { return GetValue(DatosProperty) as DatosDeTarjeta; }
        set { SetValue(DatosProperty, value); }
    }

    public TarjetaResumen()
    {
        Style? estiloEtiqueta = Application.Current.Resources["Etiqueta"] as Style;
        etiquetaTitulo.Style = estiloEtiqueta;
        etiquetaDetalle.Style = estiloEtiqueta;
        etiquetaValor.Foreground = Tema.AzulUanl;
        franja.Background = Tema.OroUanl;

        StackPanel textos = new StackPanel { Padding = new Thickness(10, 6, 8, 6), Margin = new Thickness(GrosorFranja, 0, 0, 0) };
        textos.Children.Add(etiquetaTitulo);
        textos.Children.Add(etiquetaValor);
        textos.Children.Add(etiquetaDetalle);

        Grid tarjeta = new Grid { Background = Tema.Blanco };
        tarjeta.Children.Add(franja);
        tarjeta.Children.Add(textos);
        Content = tarjeta;
    }

    private void Mostrar()
    {
        etiquetaTitulo.Text = Titulo;
        DatosDeTarjeta? datos = Datos;
        if (datos != null)
        {
            etiquetaValor.Text = datos.Valor;
            etiquetaDetalle.Text = datos.Detalle;
            franja.Background = Tema.ColorSemaforo(datos.Semaforo);
        }
    }
}
