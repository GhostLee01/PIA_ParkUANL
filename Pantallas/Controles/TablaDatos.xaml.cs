using System.Text;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Markup;

namespace PIA_ParkUANL;

// =====================================================================================
//  TABLADATOS — Tabla de solo lectura con encabezado azul UANL y filas alternadas.
//  WinUI no trae una tabla de datos: esta usa un ListView, que solo crea las filas visibles
//  y por eso sigue siendo rápido con miles de registros.
//  La pantalla enlaza Columnas, Filas y DatoSeleccionado.
// =====================================================================================
public sealed partial class TablaDatos : UserControl
{
    private const string NamespaceXaml = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private const char SeparadorDeColumnas = ',';

    public static readonly DependencyProperty ColumnasProperty =
        DependencyProperty.Register(nameof(Columnas), typeof(string), typeof(TablaDatos), new PropertyMetadata("", (control, _) => ((TablaDatos)control).DefinirColumnas()));

    public static readonly DependencyProperty FilasProperty =
        DependencyProperty.Register(nameof(Filas), typeof(object), typeof(TablaDatos), new PropertyMetadata(null, (control, _) => ((TablaDatos)control).CargarFilas()));

    public static readonly DependencyProperty DatoSeleccionadoProperty =
        DependencyProperty.Register(nameof(DatoSeleccionado), typeof(object), typeof(TablaDatos), new PropertyMetadata(null, (control, _) => ((TablaDatos)control).ElegirFilaDelDato()));

    private IReadOnlyList<FilaTabla> filas = Array.Empty<FilaTabla>();
    private bool sincronizando;

    /// <summary>Títulos separados por comas.</summary>
    public string Columnas
    {
        get { return (string)GetValue(ColumnasProperty); }
        set { SetValue(ColumnasProperty, value); }
    }

    /// <summary>La lista de FilaTabla. Es object porque XAML no admite propiedades de tipos genéricos.</summary>
    public object? Filas
    {
        get { return GetValue(FilasProperty); }
        set { SetValue(FilasProperty, value); }
    }

    /// <summary>Dato de la fila elegida, o null si no hay ninguna.</summary>
    public object? DatoSeleccionado
    {
        get { return GetValue(DatoSeleccionadoProperty); }
        set { SetValue(DatoSeleccionadoProperty, value); }
    }

    public TablaDatos()
    {
        InitializeComponent();
        lista.ContainerContentChanging += AlMostrarFila;
        lista.SelectionChanged += AlCambiarSeleccion;
    }

    private void DefinirColumnas()
    {
        string[] columnas = Columnas.Split(SeparadorDeColumnas);
        encabezadoTabla.ColumnDefinitions.Clear();
        encabezadoTabla.Children.Clear();

        StringBuilder plantilla = new StringBuilder();
        plantilla.Append("<DataTemplate xmlns=\"" + NamespaceXaml + "\"><Grid ColumnSpacing=\"8\" Padding=\"8,0\" MinHeight=\"30\"><Grid.ColumnDefinitions>");
        for (int i = 0; i < columnas.Length; i++)
        {
            plantilla.Append("<ColumnDefinition Width=\"*\" />");
        }
        plantilla.Append("</Grid.ColumnDefinitions>");

        for (int i = 0; i < columnas.Length; i++)
        {
            plantilla.Append("<TextBlock Grid.Column=\"" + i + "\" FontSize=\"13\" VerticalAlignment=\"Center\" TextTrimming=\"CharacterEllipsis\" />");

            encabezadoTabla.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            TextBlock titulo = new TextBlock { Text = columnas[i], Foreground = Tema.Blanco, FontSize = 13, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis };
            Grid.SetColumn(titulo, i);
            encabezadoTabla.Children.Add(titulo);
        }
        plantilla.Append("</Grid></DataTemplate>");

        lista.ItemTemplate = XamlReader.Load(plantilla.ToString()) as DataTemplate;
    }

    /// <summary>Reemplaza todas las filas conservando elegido el mismo dato, si sigue en la tabla.</summary>
    private void CargarFilas()
    {
        sincronizando = true;
        filas = Filas as IReadOnlyList<FilaTabla> ?? Array.Empty<FilaTabla>();
        lista.ItemsSource = filas;
        FilaTabla? filaElegida = BuscarFila(DatoSeleccionado);
        lista.SelectedItem = filaElegida;
        sincronizando = false;

        if (filaElegida == null && DatoSeleccionado != null)
        {
            DatoSeleccionado = null;
        }
    }

    private void ElegirFilaDelDato()
    {
        if (sincronizando)
        {
            return;
        }

        FilaTabla? fila = BuscarFila(DatoSeleccionado);
        if (lista.SelectedItem != fila)
        {
            lista.SelectedItem = fila;
        }
        if (fila != null)
        {
            lista.ScrollIntoView(fila);
        }
    }

    private FilaTabla? BuscarFila(object? dato)
    {
        return dato == null ? null : filas.FirstOrDefault(fila => ReferenceEquals(fila.Dato, dato));
    }

    /// <summary>El ListView reutiliza las filas al desplazarse: aquí se escribe el texto de la que va a aparecer.</summary>
    private void AlMostrarFila(ListViewBase remitente, ContainerContentChangingEventArgs argumentos)
    {
        if (argumentos.InRecycleQueue || argumentos.Item is not FilaTabla fila || argumentos.ItemContainer.ContentTemplateRoot is not Grid celdas)
        {
            return;
        }

        for (int i = 0; i < celdas.Children.Count && i < fila.Celdas.Count; i++)
        {
            if (celdas.Children[i] is TextBlock celda)
            {
                celda.Text = fila.Celdas[i];
                ToolTipService.SetToolTip(celda, fila.Celdas[i] == "" ? null : fila.Celdas[i]);
            }
        }
        PintarFila(argumentos.ItemContainer, argumentos.ItemIndex);
        argumentos.Handled = true;
    }

    /// <summary>
    /// Filas alternadas. La plantilla de ListViewItem ignora su Background, por eso se pinta el contenido de la fila;
    /// la fila elegida queda transparente para que se vea su color dorado.
    /// </summary>
    private static void PintarFila(SelectorItem fila, int indice)
    {
        if (fila.ContentTemplateRoot is Grid celdas)
        {
            celdas.Background = indice % 2 == 1 && fila.IsSelected == false ? Tema.FilaAlterna : null;
        }
    }

    private void AlCambiarSeleccion(object remitente, SelectionChangedEventArgs argumentos)
    {
        foreach (object fila in argumentos.AddedItems.Concat(argumentos.RemovedItems))
        {
            if (lista.ContainerFromItem(fila) is SelectorItem contenedor)
            {
                PintarFila(contenedor, lista.IndexFromContainer(contenedor));
            }
        }

        if (sincronizando == false)
        {
            DatoSeleccionado = (lista.SelectedItem as FilaTabla)?.Dato;
        }
    }
}
