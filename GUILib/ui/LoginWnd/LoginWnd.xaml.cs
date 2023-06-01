using GUILib.data;
using System;
using System.Windows;

namespace GUILib.ui.LoginWnd {

    public partial class LoginWnd : Window {

        private Func<string, string, bool> onLogin;

        private bool cancelled = false;
        private bool loggedin = false;

        public bool Cancelled {
            get { return cancelled; }
        }

        public LoginWnd(Func<string, string, bool> onLogin) {
            InitializeComponent();
            this.onLogin = onLogin;

            String username = Model.Create().GetConfig().Username;
            if(username != null&& username != "") {
                txtUsername.Text = username;
            }
        }

        void SetEnabled(bool enabled) {
            txtUsername.IsEnabled = enabled;
            txtPassword.IsEnabled = enabled;
            btnLogin.IsEnabled = enabled;
        }

        private void tryLogin() {
            SetEnabled(false);
            txtUsername.Background = Model.ColorDefault;
            txtPassword.Background = Model.ColorDefault;

            string username = txtUsername.Text;
            string password = txtPassword.Password;
            new AsyncJob(() => onLogin(username, password),
                (obj) => {
                    SetEnabled(true);
                    if (obj is true) {
                        loggedin = true;
                        this.Close();
                    } else {
                        txtUsername.Background = Model.ColorError;
                        txtPassword.Background = Model.ColorError;
                    }
                }).Run();
        }

        private void Button_Click(object sender, RoutedEventArgs e) {
            tryLogin();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
            if (!loggedin) {
                cancelled = true;
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) {
            MoveFocus(new System.Windows.Input.TraversalRequest(System.Windows.Input.FocusNavigationDirection.First));
        }

        private void txtUsername_KeyDown(object sender, System.Windows.Input.KeyEventArgs e) {
            if (e.Key == System.Windows.Input.Key.Enter) {
                tryLogin();
            }
        }

        private void txtPassword_KeyDown(object sender, System.Windows.Input.KeyEventArgs e) {
            if (e.Key == System.Windows.Input.Key.Enter) {
                tryLogin();
            }
        }
    }
}
