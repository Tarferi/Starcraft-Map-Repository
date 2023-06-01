using System.Windows;

namespace GUILib {

    public partial class MainWindow : Window {

        public bool InfoPanelVisible { set {
                content.InfoPanelVisible = value;
            } }

        public MainWindow() {
            InitializeComponent();
        }

    }
}
