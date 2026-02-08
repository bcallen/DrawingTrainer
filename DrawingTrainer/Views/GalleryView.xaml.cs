using DrawingTrainer.Models;
using DrawingTrainer.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DrawingTrainer.Views;

public partial class GalleryView : UserControl
{
    public GalleryView()
    {
        InitializeComponent();
        Loaded += (_, _) => Focus();
    }

    private void TagFilter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: Tag tag } && DataContext is GalleryViewModel vm)
        {
            vm.SelectedTag = vm.SelectedTag?.Id == tag.Id ? null : tag;
        }
    }

    private void ClearFilter_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is GalleryViewModel vm)
        {
            vm.SelectedTag = null;
        }
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not GalleryViewModel vm) return;

        switch (e.Key)
        {
            case Key.Left:
                vm.NavigatePreviousCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Right:
                vm.NavigateNextCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Escape:
                vm.ClearSelectionCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }
}
