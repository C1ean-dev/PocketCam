using PocketCam.Desktop.ViewModels;

namespace PocketCam.Desktop;

public partial class App : Application
{
    private MainWindowViewModel? _viewModel;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _viewModel = new MainWindowViewModel(Dispatcher);
        MainWindow = new MainWindow { DataContext = _viewModel };
        MainWindow.Show();
        _ = _viewModel.StartAsync();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _viewModel?.Dispose();
        base.OnExit(e);
    }
}

