using System.Windows;

namespace GUILib.ui.utils {

    public partial class Prompt : Window {

        private bool confirmed = false;

        public static bool? ConfirmModal(string prompt, string title) {
            var res = MessageBox.Show(prompt, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res == MessageBoxResult.Yes) {
                return true;
            } else if (res  == MessageBoxResult.No) {
                return false;
            } else {
                return null;
            }
        }

        public static string Modal(string prompt, string title) {
            Prompt p = new Prompt(prompt, title);
            p.ShowDialog();
            return p.confirmed ? p.txtContents.Text : null;
        }

        public Prompt(string prompt, string title) {
            InitializeComponent();
            txtPrompt.Text = prompt;
            this.Title = title;
        }

        private void btnConfirm_Click(object sender, RoutedEventArgs e) {
            confirmed = txtContents.Text.Trim().Length > 0;
            Close();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e) {
            Close();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) {
            MoveFocus(new System.Windows.Input.TraversalRequest(System.Windows.Input.FocusNavigationDirection.First));
        }

        private void txtContents_KeyDown(object sender, System.Windows.Input.KeyEventArgs e) {
            if(e.Key == System.Windows.Input.Key.Enter) {
                confirmed = txtContents.Text.Trim().Length > 0;
                Close();
            }
        }
    }
}
