using System.Collections.Specialized;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PIA_ParkUANL_Lite;

/// <summary>Solo lo visual: el cursor y bajar el chat al último mensaje. La lógica está en AsistenteVistaModelo.</summary>
public sealed partial class PestanaAsistente : UserControl
{
    public AsistenteVistaModelo Vista { get; }

    public PestanaAsistente(AsistenteVistaModelo vista)
    {
        ArgumentNullException.ThrowIfNull(vista);
        Vista = vista;

        InitializeComponent();

        Tema.AlPresionarEnter(cajaPregunta, Vista.EnviarCommand);
        Vista.PreguntaEnfocada += () => cajaPregunta.Focus(FocusState.Programmatic);

        // La colección vive tanto como esta vista, pero se suelta al salir para no recibir avisos sin estar en pantalla.
        Loaded += (_, _) => Vista.Chat.CollectionChanged += AlCambiarChat;
        Unloaded += (_, _) => Vista.Chat.CollectionChanged -= AlCambiarChat;
    }

    private void AlCambiarChat(object? remitente, NotifyCollectionChangedEventArgs argumentos)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            desplazamientoChat.UpdateLayout();
            desplazamientoChat.ChangeView(null, desplazamientoChat.ScrollableHeight, null, true);
        });
    }

    private void AlElegirSugerencia(object remitente, RoutedEventArgs argumentos)
    {
        if (remitente is Button { Content: string sugerencia })
        {
            Vista.EnviarSugerenciaCommand.Execute(sugerencia);
        }
    }
}
