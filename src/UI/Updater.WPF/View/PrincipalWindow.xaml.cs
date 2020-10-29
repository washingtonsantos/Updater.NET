using MahApps.Metro.Controls;
using Updater.WPF.ViewModel;

namespace Updater.WPF.View
{
    /// <summary>
    /// Interaction logic for PrincipalWindow.xaml
    /// </summary>
    public partial class PrincipalWindow : MetroWindow
    {
        public PrincipalWindow()
        {
            InitializeComponent();
            DataContext = new PrincipalWindowViewModel();
        }
    }
}
