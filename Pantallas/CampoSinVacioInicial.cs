using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;

namespace PIA_ParkUANL_Lite;

// =====================================================================================
//  CAMPOSINVACIOINICIAL.CS — Evita que un campo empiece con un espacio, un tabulador o
//  un carácter invisible, ya sea al escribir, al pegar o al borrar la primera palabra.
//  Estilos.xaml lo activa en todos los TextBox y PasswordBox. Los NumberBox traen su
//  propia caja con otro estilo, así que cada uno lo activa en su XAML.
//
//  PasswordBox no permite mover el cursor: si se limpiara lo pegado, lo que se escriba
//  después quedaría al principio. Por eso, en una contraseña, un pegado que empieza con
//  vacíos se rechaza completo y se avisa junto al campo.
// =====================================================================================
public static class CampoSinVacioInicial
{
    private const double AnchoDelAviso = 280;
    private static readonly TimeSpan DuracionDelAviso = TimeSpan.FromSeconds(5);

    // Lo que tenía el campo justo antes de pegar. Solo tiene valor mientras se aplica el pegado.
    private static readonly DependencyProperty ContrasenaAntesDePegarProperty =
        DependencyProperty.RegisterAttached("ContrasenaAntesDePegar", typeof(string), typeof(CampoSinVacioInicial), new PropertyMetadata(null));

    public static readonly DependencyProperty ActivoProperty =
        DependencyProperty.RegisterAttached("Activo", typeof(bool), typeof(CampoSinVacioInicial), new PropertyMetadata(false, AlCambiarActivo));

    public static bool GetActivo(DependencyObject control)
    {
        return (bool)control.GetValue(ActivoProperty);
    }

    public static void SetActivo(DependencyObject control, bool activo)
    {
        control.SetValue(ActivoProperty, activo);
    }

    // Los manejadores son estáticos: no retienen la pantalla en memoria cuando se cierra.
    private static void AlCambiarActivo(DependencyObject control, DependencyPropertyChangedEventArgs cambio)
    {
        bool activo = cambio.NewValue is true;

        if (control is TextBox caja)
        {
            caja.TextChanging -= LimpiarTexto;
            if (activo)
            {
                caja.TextChanging += LimpiarTexto;
            }
        }
        else if (control is PasswordBox cajaContrasena)
        {
            cajaContrasena.PasswordChanging -= LimpiarContrasena;
            cajaContrasena.Paste -= RecordarContrasenaAlPegar;
            if (activo)
            {
                cajaContrasena.PasswordChanging += LimpiarContrasena;
                cajaContrasena.Paste += RecordarContrasenaAlPegar;
            }
        }
        else if (control is NumberBox cajaNumero)
        {
            cajaNumero.Loaded -= ActivarCajaDelNumero;
            if (activo)
            {
                cajaNumero.Loaded += ActivarCajaDelNumero;
            }
        }
    }

    private static void LimpiarTexto(TextBox caja, TextBoxTextChangingEventArgs argumentos)
    {
        string texto = caja.Text;
        string limpio = Validaciones.QuitarVaciosIniciales(texto);
        if (limpio.Length == texto.Length)
        {
            return;
        }

        // El cursor se recorre lo mismo que se quitó, para que el usuario siga escribiendo donde iba.
        int posicionDelCursor = Math.Max(caja.SelectionStart - (texto.Length - limpio.Length), 0);
        caja.Text = limpio;
        caja.SelectionStart = posicionDelCursor;
    }

    private static void LimpiarContrasena(PasswordBox caja, PasswordBoxPasswordChangingEventArgs argumentos)
    {
        string? contrasenaAntesDePegar = caja.GetValue(ContrasenaAntesDePegarProperty) as string;
        caja.ClearValue(ContrasenaAntesDePegarProperty);

        string contrasena = caja.Password;
        string limpia = Validaciones.QuitarVaciosIniciales(contrasena);
        if (limpia.Length == contrasena.Length)
        {
            return;
        }

        if (contrasenaAntesDePegar == null)
        {
            caja.Password = limpia;
            return;
        }

        caja.Password = contrasenaAntesDePegar;
        AvisarPegadoRechazado(caja);
    }

    private static void RecordarContrasenaAlPegar(object control, TextControlPasteEventArgs argumentos)
    {
        if (control is PasswordBox caja)
        {
            caja.SetValue(ContrasenaAntesDePegarProperty, caja.Password);
        }
    }

    private static void AvisarPegadoRechazado(PasswordBox caja)
    {
        TextBlock mensaje = new TextBlock { Text = Validaciones.MensajePegadoRechazado, TextWrapping = TextWrapping.Wrap, MaxWidth = AnchoDelAviso };
        Flyout aviso = new Flyout { Content = mensaje };

        // Transitorio: el aviso aparece sin quitarle el foco al campo.
        aviso.ShowAt(caja, new FlyoutShowOptions { ShowMode = FlyoutShowMode.Transient, Placement = FlyoutPlacementMode.Bottom });

        // Se cierra solo: queda encima de los campos y botones de abajo.
        DispatcherTimer temporizador = new DispatcherTimer { Interval = DuracionDelAviso };
        temporizador.Tick += (_, _) =>
        {
            temporizador.Stop();
            aviso.Hide();
        };
        temporizador.Start();
    }

    private static void ActivarCajaDelNumero(object control, RoutedEventArgs argumentos)
    {
        if (control is DependencyObject cajaNumero && BuscarCajaInterna(cajaNumero) is TextBox cajaInterna)
        {
            SetActivo(cajaInterna, true);
        }
    }

    private static TextBox? BuscarCajaInterna(DependencyObject padre)
    {
        for (int indice = 0; indice < VisualTreeHelper.GetChildrenCount(padre); indice++)
        {
            DependencyObject hijo = VisualTreeHelper.GetChild(padre, indice);
            if ((hijo as TextBox ?? BuscarCajaInterna(hijo)) is TextBox encontrada)
            {
                return encontrada;
            }
        }
        return null;
    }
}
