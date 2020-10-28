using MahApps.Metro.Controls;
using Updater.WPFNETFramework472.ViewModel;

namespace Updater.WPFNETFramework472
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainWindowViewModel();
        }
    }
}
