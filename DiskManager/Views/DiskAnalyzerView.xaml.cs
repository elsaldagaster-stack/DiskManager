using DiskManager.ViewModels;
using System.Windows.Controls;

namespace DiskManager.Views;

public partial class DiskAnalyzerView : UserControl
{
    public DiskAnalyzerView()
    {
        InitializeComponent();
        DataContext = App.Current.GetService<DiskAnalyzerViewModel>()!;
    }
}
