using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace PIA_ParkUANL;

// =====================================================================================
//  CONVERSIONES.CS — Traduce los valores de las vista-modelos, que no conocen WinUI,
//  a lo que necesita cada control. Se usan desde x:Bind.
// =====================================================================================
public static class Conversiones
{
    public static InfoBarSeverity Severidad(TipoDeMensaje tipo)
    {
        return tipo switch
        {
            TipoDeMensaje.Exito => InfoBarSeverity.Success,
            TipoDeMensaje.Error => InfoBarSeverity.Error,
            _ => InfoBarSeverity.Informational
        };
    }

    public static PasswordRevealMode ModoDeContrasena(bool mostrar)
    {
        return mostrar ? PasswordRevealMode.Visible : PasswordRevealMode.Hidden;
    }

    public static Visibility VisibleSi(bool visible)
    {
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public static Brush ColorDeSemaforo(string? semaforo)
    {
        return Tema.ColorSemaforo(semaforo ?? Semaforos.Verde);
    }

    public static Brush ColorDeAlerta(bool esAlerta)
    {
        return esAlerta ? Tema.Rojo : Tema.AzulUanl;
    }

    public static Brush ColorDeAutor(TipoDeMensaje tipo)
    {
        return tipo switch
        {
            TipoDeMensaje.Neutro => Tema.GrisTexto,
            TipoDeMensaje.Exito => Tema.Verde,
            TipoDeMensaje.Error => Tema.Rojo,
            _ => Tema.AzulUanl
        };
    }

    public static ImageSource? ImagenDeQr(ImagenQr? imagen)
    {
        return imagen == null ? null : Tema.CrearImagenQr(imagen);
    }
}

/// <summary>ComboBox.SelectedItem es de tipo object y x:Bind no lo convierte solo a string en los dos sentidos.</summary>
public sealed partial class ConvertidorDeTexto : IValueConverter
{
    public object? Convert(object? value, Type targetType, object parameter, string language)
    {
        return value;
    }

    public object ConvertBack(object? value, Type targetType, object parameter, string language)
    {
        return value as string ?? "";
    }
}

/// <summary>Igual que ConvertidorDeTexto, pero conserva el null: en una lista significa que no hay nada elegido.</summary>
public sealed partial class ConvertidorDeTextoOpcional : IValueConverter
{
    public object? Convert(object? value, Type targetType, object parameter, string language)
    {
        return value;
    }

    public object? ConvertBack(object? value, Type targetType, object parameter, string language)
    {
        return value as string;
    }
}
