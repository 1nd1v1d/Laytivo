using System.ComponentModel;
using System.Windows;

namespace Laytivo.App;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel) { InitializeComponent(); DataContext = viewModel; }
    protected override void OnClosing(CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
        base.OnClosing(e);
    }
}
