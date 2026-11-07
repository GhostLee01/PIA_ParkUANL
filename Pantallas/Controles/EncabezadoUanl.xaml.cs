using System.Windows.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PIA_ParkUANL_Lite;

/// <summary>Franja azul con el título de la pantalla y un enlace opcional para cambiar la contraseña.</summary>
public sealed partial class EncabezadoUanl : UserControl
{
    public static readonly DependencyProperty TituloProperty =
        DependencyProperty.Register(nameof(Titulo), typeof(string), typeof(EncabezadoUanl), new PropertyMetadata(""));

    public static readonly DependencyProperty SubtituloProperty =
        DependencyProperty.Register(nameof(Subtitulo), typeof(string), typeof(EncabezadoUanl), new PropertyMetadata(""));

    public static readonly DependencyProperty ComandoCuentaProperty =
        DependencyProperty.Register(nameof(ComandoCuenta), typeof(ICommand), typeof(EncabezadoUanl), new PropertyMetadata(null));

    public string Titulo
    {
        get { return (string)GetValue(TituloProperty); }
        set { SetValue(TituloProperty, value); }
    }

    public string Subtitulo
    {
        get { return (string)GetValue(SubtituloProperty); }
        set { SetValue(SubtituloProperty, value); }
    }

    public ICommand? ComandoCuenta
    {
        get { return (ICommand?)GetValue(ComandoCuentaProperty); }
        set { SetValue(ComandoCuentaProperty, value); }
    }

    public EncabezadoUanl()
    {
        InitializeComponent();
    }

    public Visibility TieneComando(ICommand? comando)
    {
        return Conversiones.VisibleSi(comando != null);
    }
}
