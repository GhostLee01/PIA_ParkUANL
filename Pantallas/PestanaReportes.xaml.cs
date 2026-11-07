using Microsoft.UI.Xaml.Controls;

namespace PIA_ParkUANL;

/// <summary>Sin lógica: todo está enlazado a ReportesVistaModelo.</summary>
public sealed partial class PestanaReportes : UserControl
{
    public ReportesVistaModelo Vista { get; }

    public PestanaReportes(ReportesVistaModelo vista)
    {
        ArgumentNullException.ThrowIfNull(vista);
        Vista = vista;
        InitializeComponent();
    }
}
