using GUILib.data;
using GUILib.ui.utils;
using System.Windows.Controls;

namespace GUILib.ui {
    
    public partial class MainContent : UserControl {

        private Model model;

        public MainContent() {
            InitializeComponent();
            AsyncManager.Bootstrap(this);

            model = Model.Create();
            if (model == null) {
                ErrorMessage.Show("Failed to create model");
            }
        }
    }
}
