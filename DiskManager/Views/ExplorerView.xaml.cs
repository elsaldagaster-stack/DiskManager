using DiskManager.Models;
using DiskManager.ViewModels;
using System.Windows.Controls;
using System.Windows.Input;

namespace DiskManager.Views;

public partial class ExplorerView : UserControl
{
    public ExplorerView()
    {
        InitializeComponent();
        DataContext = App.Current.GetService<ExplorerViewModel>()
                     ?? new ExplorerViewModel(App.Current.GetService<Services.IFileSystemService>()!);
    }

    private void Item_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ExplorerViewModel vm && sender is ListViewItem { DataContext: FileItem item })
            vm.OpenItemCommand.Execute(item);
    }

    private void DriveItem_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ExplorerViewModel vm && sender is TreeViewItem { Tag: string path })
            vm.NavigateToCommand.Execute(path);
    }
}
