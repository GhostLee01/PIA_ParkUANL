using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PIA_ParkUANL;

/// <summary>Elige un día y, si MostrarHora es true, también la hora (en formato de 24 horas).</summary>
public sealed partial class SelectorFecha : UserControl
{
    public static readonly DependencyProperty ValorProperty =
        DependencyProperty.Register(nameof(Valor), typeof(DateTime), typeof(SelectorFecha), new PropertyMetadata(DateTime.Today, (control, _) => ((SelectorFecha)control).MostrarValor()));

    private readonly CalendarDatePicker calendario = new CalendarDatePicker { DateFormat = "{day.integer(2)}/{month.integer(2)}/{year.full}", MinWidth = 150 };
    private readonly TimePicker reloj = new TimePicker { ClockIdentifier = "24HourClock", MinuteIncrement = 1, MinWidth = 150, Visibility = Visibility.Collapsed };
    private bool mostrandoValor;

    public DateTime Valor
    {
        get { return (DateTime)GetValue(ValorProperty); }
        set { SetValue(ValorProperty, value); }
    }

    public bool MostrarHora
    {
        get { return reloj.Visibility == Visibility.Visible; }
        set { reloj.Visibility = value ? Visibility.Visible : Visibility.Collapsed; }
    }

    public SelectorFecha()
    {
        StackPanel panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        panel.Children.Add(calendario);
        panel.Children.Add(reloj);
        Content = panel;

        MostrarValor();
        calendario.DateChanged += (_, _) => LeerControles();
        reloj.TimeChanged += (_, _) => LeerControles();
    }

    private void MostrarValor()
    {
        mostrandoValor = true;
        calendario.Date = new DateTimeOffset(DateTime.SpecifyKind(Valor.Date, DateTimeKind.Unspecified));
        reloj.Time = new TimeSpan(Valor.Hour, Valor.Minute, 0);
        mostrandoValor = false;
    }

    private void LeerControles()
    {
        if (mostrandoValor)
        {
            return;
        }

        // El calendario se puede quedar vacío: entonces se conserva el día anterior.
        DateTime dia = calendario.Date?.DateTime.Date ?? Valor.Date;
        Valor = MostrarHora ? dia + reloj.Time : dia;
    }
}
