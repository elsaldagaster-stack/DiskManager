using DiskManager.ViewModels;
using System.Windows.Controls;

namespace DiskManager.Views;

public partial class DuplicateFinderView : UserControl
{
    public DuplicateFinderView()
    {
        InitializeComponent();
        DataContext = App.Current.GetService<DuplicateFinderViewModel>()!;
    }
}
