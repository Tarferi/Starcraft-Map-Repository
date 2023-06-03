using System.Windows;
using GUILib;
using GUILib.data;

namespace WPFRunner {

    public partial class RunnerMainWindow : Window {

        public RunnerMainWindow() {
            DLLResources.Hook();
            InitializeComponent();
            content.InfoPanelVisible = true;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
            AsyncManager.GetInstance().Stop();
        }
    }
}
