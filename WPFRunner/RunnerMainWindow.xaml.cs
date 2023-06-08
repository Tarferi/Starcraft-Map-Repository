using System.Windows;
using GUILib;
using GUILib.data;

namespace WPFRunner {

    public partial class RunnerMainWindow : Window {

        public RunnerMainWindow() {
            DLLResources.Hook();

            ModelInitData.RootDirGetter = () => {
                string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string workPath = System.IO.Path.GetDirectoryName(exePath);
                string mp = "\\Map Repository";
                return workPath + mp;
            };

            InitializeComponent();
            content.InfoPanelVisible = true;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
            AsyncManager.GetInstance().Stop();
        }
    }
}
