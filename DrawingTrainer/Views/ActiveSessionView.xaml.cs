using DrawingTrainer.ViewModels;
using System.Windows.Controls;
using System.Windows.Input;

namespace DrawingTrainer.Views;

public partial class ActiveSessionView : UserControl
{
    public ActiveSessionView()
    {
        InitializeComponent();
        Loaded += (_, _) => Focus();
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not ActiveSessionViewModel vm) return;

        switch (e.Key)
        {
            case Key.Space:
                vm.PauseResumeCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Escape:
                vm.EndSessionCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }
}
