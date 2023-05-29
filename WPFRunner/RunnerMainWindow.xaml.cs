using System.Windows;
using GUILib.data;

namespace WPFRunner {

    public partial class RunnerMainWindow : Window {
        public RunnerMainWindow() {
            InitializeComponent();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
            AsyncManager.GetInstance().Stop();
        }
    }
}
