using DrawingTrainer.Models;
using DrawingTrainer.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DrawingTrainer.Views;

public partial class LibraryView : UserControl
{
    public LibraryView()
    {
        InitializeComponent();
    }

    private void TagFilter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: Tag tag } && DataContext is LibraryViewModel vm)
        {
            vm.SelectedTag = vm.SelectedTag?.Id == tag.Id ? null : tag;
        }
    }

    private void ClearFilter_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is LibraryViewModel vm)
        {
            vm.SelectedTag = null;
        }
    }

    private void Photo_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: LibraryPhotoItem item }
            && DataContext is LibraryViewModel vm)
        {
            vm.TogglePhotoSelectionCommand.Execute(item);
        }
    }
}
