using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.Storage.Pickers;
using Windows.ApplicationModel.DataTransfer;

namespace PIA_ParkUANL_Lite;

// =====================================================================================
//  DIALOGOS — Avisos y confirmaciones sobre la ventana (equivalen a un cuadro de mensaje).
//  WinUI solo permite un ContentDialog abierto a la vez: si ya hay uno, el siguiente espera su turno.
// =====================================================================================
public sealed class Dialogos : IDialogos, IDisposable
{
    private const int AnchoMaximoDialogo = 640;

    private readonly VentanaPrincipal ventana;
    private readonly SemaphoreSlim turno = new SemaphoreSlim(1, 1);

    public Dialogos(VentanaPrincipal ventana)
    {
        ArgumentNullException.ThrowIfNull(ventana);
        this.ventana = ventana;
    }

    public void Dispose()
    {
        turno.Dispose();
    }

    private static ContentDialog Crear(string titulo, UIElement contenido)
    {
        ContentDialog dialogo = new ContentDialog
        {
            Title = titulo,
            Content = contenido,
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
        };
        dialogo.Resources["ContentDialogMaxWidth"] = (double)AnchoMaximoDialogo;
        return dialogo;
    }

    private async Task<ContentDialogResult> Mostrar(ContentDialog dialogo)
    {
        await turno.WaitAsync();
        try
        {
            dialogo.XamlRoot = ventana.Content.XamlRoot;
            return await dialogo.ShowAsync();
        }
        finally
        {
            turno.Release();
        }
    }

    private static TextBlock CrearTexto(string texto)
    {
        return new TextBlock { Text = texto, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true };
    }

    public async Task Avisar(string titulo, string mensaje)
    {
        ContentDialog dialogo = Crear(titulo, CrearTexto(mensaje));
        dialogo.CloseButtonText = "Aceptar";
        dialogo.DefaultButton = ContentDialogButton.Close;
        await Mostrar(dialogo);
    }

    public async Task<bool> Confirmar(string titulo, string pregunta)
    {
        ContentDialog dialogo = Crear(titulo, CrearTexto(pregunta));
        dialogo.PrimaryButtonText = "Sí";
        dialogo.CloseButtonText = "No";
        dialogo.DefaultButton = ContentDialogButton.Primary;
        return await Mostrar(dialogo) == ContentDialogResult.Primary;
    }

    public async Task<string?> PedirTexto(string titulo, string instruccion, string textoInicial = "", bool enMayusculas = false)
    {
        TextBox cajaTexto = new TextBox { MaxLength = Validaciones.LongitudMaximaTextoLibre, Text = textoInicial };
        if (enMayusculas)
        {
            cajaTexto.CharacterCasing = CharacterCasing.Upper;
        }

        StackPanel contenido = new StackPanel { Spacing = 10 };
        contenido.Children.Add(CrearTexto(instruccion));
        contenido.Children.Add(cajaTexto);

        ContentDialog dialogo = Crear(titulo, contenido);
        dialogo.PrimaryButtonText = "Aceptar";
        dialogo.CloseButtonText = "Cancelar";
        dialogo.DefaultButton = ContentDialogButton.Primary;

        return await Mostrar(dialogo) == ContentDialogResult.Primary ? cajaTexto.Text.Trim() : null;
    }

    public async Task<OpcionCierreDeTurno> PreguntarCierreDeTurno(string resumen)
    {
        StackPanel contenido = new StackPanel { Spacing = 12 };
        contenido.Children.Add(CrearTexto(resumen));
        contenido.Children.Add(CrearTexto("Registra la salida de los vehículos olvidados solo si ya confirmaste que " +
                                          "no están en el estacionamiento. Si alguno sigue ahí, cierra sin registrar salidas: " +
                                          "quedarán en la lista para el siguiente turno."));

        ContentDialog dialogo = Crear("Cierre de turno", new ScrollViewer { Content = contenido });
        dialogo.PrimaryButtonText = "Cerrar y registrar salidas";
        dialogo.SecondaryButtonText = "Cerrar sin registrar salidas";
        dialogo.CloseButtonText = "Cancelar";

        // El botón por defecto es Cancelar: un Enter por accidente no debe registrar salidas.
        dialogo.DefaultButton = ContentDialogButton.Close;

        return await Mostrar(dialogo) switch
        {
            ContentDialogResult.Primary => OpcionCierreDeTurno.RegistrarSalidas,
            ContentDialogResult.Secondary => OpcionCierreDeTurno.CerrarSinRegistrar,
            _ => OpcionCierreDeTurno.Cancelar
        };
    }
}

public sealed class Portapapeles : IPortapapeles
{
    public bool Copiar(string texto)
    {
        ArgumentNullException.ThrowIfNull(texto);

        try
        {
            DataPackage paquete = new DataPackage();
            paquete.SetText(texto);
            Clipboard.SetContent(paquete);
            return true;
        }
        catch (COMException ex)
        {
            GestorArchivos.RegistrarError(ex);
            return false;
        }
    }
}

public sealed class SelectorDeArchivos : ISelectorDeArchivos
{
    private readonly VentanaPrincipal ventana;

    public SelectorDeArchivos(VentanaPrincipal ventana)
    {
        ArgumentNullException.ThrowIfNull(ventana);
        this.ventana = ventana;
    }

    public async Task<string?> ElegirDestino(string nombreSugerido, string descripcion, string extension)
    {
        FileSavePicker selector = new FileSavePicker(ventana.AppWindow.Id) { SuggestedFileName = nombreSugerido };
        selector.FileTypeChoices.Add(descripcion, new List<string> { extension });

        PickFileResult? archivo = await selector.PickSaveFileAsync();
        return archivo?.Path;
    }
}
