using DrawingTrainer.Models;
using DrawingTrainer.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace DrawingTrainer.Views;

public partial class PhotoImportView : UserControl
{
    public PhotoImportView()
    {
        InitializeComponent();
    }

    private void TagToggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton { Tag: Tag tag } && DataContext is PhotoImportViewModel vm)
        {
            vm.ToggleTagCommand.Execute(tag);
        }
    }
}
