using System.Runtime.InteropServices.WindowsRuntime;
using System.Windows.Input;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.System;

namespace PIA_ParkUANL_Lite;

// =====================================================================================
//  TEMA VISUAL UANL: colores de Estilos.xaml y ayudas puramente visuales de las pantallas.
// =====================================================================================
public static class Tema
{
    public static Brush AzulUanl { get { return Color("AzulUanl"); } }
    public static Brush OroUanl { get { return Color("OroUanl"); } }
    public static Brush Verde { get { return Color("Verde"); } }
    public static Brush Rojo { get { return Color("Rojo"); } }
    public static Brush GrisTexto { get { return Color("GrisTexto"); } }
    public static Brush Blanco { get { return Color("Blanco"); } }
    public static Brush FilaAlterna { get { return Color("FilaAlterna"); } }

    /// <summary>Un nombre que no esté en Estilos.xaml no debe romper la pantalla: se usa negro.</summary>
    private static Brush Color(string nombre)
    {
        return Application.Current.Resources[nombre] as Brush ?? new SolidColorBrush(Colors.Black);
    }

    /// <summary>Color del semáforo: Verde &lt; 70 %, Dorado 70–90 %, Rojo &gt; 90 %.</summary>
    public static Brush ColorSemaforo(string semaforo)
    {
        return semaforo switch
        {
            Semaforos.Rojo => Rojo,
            Semaforos.Dorado => OroUanl,
            _ => Verde
        };
    }

    /// <summary>Ejecuta el comando al presionar Enter dentro del control, si en este momento se puede.</summary>
    public static void AlPresionarEnter(UIElement control, ICommand comando)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(comando);
        control.KeyDown += (_, tecla) => EjecutarSiEsLaTecla(tecla, VirtualKey.Enter, comando);
    }

    /// <summary>Ejecuta el comando al presionar Esc dentro del control o de los campos que contiene.</summary>
    public static void AlPresionarEscape(UIElement control, ICommand comando)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(comando);
        control.KeyDown += (_, tecla) => EjecutarSiEsLaTecla(tecla, VirtualKey.Escape, comando);
    }

    private static void EjecutarSiEsLaTecla(KeyRoutedEventArgs tecla, VirtualKey teclaEsperada, ICommand comando)
    {
        if (tecla.Key == teclaEsperada)
        {
            tecla.Handled = true;
            if (comando.CanExecute(null))
            {
                comando.Execute(null);
            }
        }
    }

    public static WriteableBitmap CrearImagenQr(ImagenQr imagen)
    {
        ArgumentNullException.ThrowIfNull(imagen);

        WriteableBitmap mapaDeBits = new WriteableBitmap(imagen.Lado, imagen.Lado);
        using (Stream flujo = mapaDeBits.PixelBuffer.AsStream())
        {
            flujo.Write(imagen.Pixeles, 0, imagen.Pixeles.Length);
        }
        mapaDeBits.Invalidate();
        return mapaDeBits;
    }
}
