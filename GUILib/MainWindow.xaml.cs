using GUILib.data;
using GUILib.ui.utils;
using System.IO;
using System.Reflection;
using System.Windows;

namespace GUILib {

    public partial class MainWindow : Window {

        public bool InfoPanelVisible { set {
                content.InfoPanelVisible = value;
            } }

        public MainWindow() {
            ModelInitData.RootDirGetter = () => {
                string exePath = Assembly.GetExecutingAssembly().Location;
                string workPath = Path.GetDirectoryName(exePath);
                return workPath;
            };
            ErrorMessage.Show("Path:\n" + ModelInitData.RootDirGetter());

            InitializeComponent();
        }

    }
}
