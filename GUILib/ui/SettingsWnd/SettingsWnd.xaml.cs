using GUILib.data;
using GUILib.db;
using GUILib.ui.utils;
using System.Windows.Controls;
using System.Windows.Media;

namespace GUILib.ui.SettingsWnd {

    public partial class SettingsWnd : UserControl {

        private Model model;

        private Config config;

        private Path pathMaps;
        private Path pathTemp;
        private Path pathProtector;
        private Path pathEuddraft;

        private Brush cDefault = null;
        private Brush cError = new SolidColorBrush(Colors.Red);
        private Brush cSuccess = new SolidColorBrush(Colors.LimeGreen);

        private void SetEnabled(bool enabled) {
            txtUsername.IsEnabled = enabled;
            txtPassword.IsEnabled = enabled;
            txtAPIToken.IsEnabled = enabled;
            
            fileMaps.IsEnabled = enabled;
            fileTemp.IsEnabled = enabled;
            fileProtector.IsEnabled = enabled;
            fileEuddraft.IsEnabled = enabled;
            
            btnLogin.IsEnabled = enabled;
            btnValidate.IsEnabled = enabled;
        }

        public SettingsWnd() {
            InitializeComponent();

            model = Model.Create();
            config = model.GetConfig();

            pathMaps = model.GetPath("maps");
            pathTemp = model.GetPath("temp");
            pathProtector = model.GetPath("protector");
            pathEuddraft= model.GetPath("euddraft");

            txtUsername.Text = config.Username;
            txtPassword.Password = config.Password;
            txtAPIToken.Text = config.API;

            fileMaps.DirectoryPicker = true;
            fileMaps.Content = pathMaps.Value;
            fileTemp.DirectoryPicker = true;
            fileTemp.Content = pathTemp.Value;
            fileProtector.DirectoryPicker = false;
            fileProtector.FileExtensionDefaultExtension = "*.exe";
            fileProtector.FileExtensionAllFilters = "ScProtectionToolchain.exe|*.exe";
            fileProtector.Content = pathProtector.Value;
            fileEuddraft.DirectoryPicker = true;
            fileEuddraft.Content = pathEuddraft.Value;

            fileMaps.FileInputChangeEvent += (e) => { pathMaps.Value = fileMaps.Content; };
            fileTemp.FileInputChangeEvent += (e) => { pathTemp.Value = fileTemp.Content; };
            fileProtector.FileInputChangeEvent += (e) => { pathProtector.Value = fileProtector.Content; };
            fileEuddraft.FileInputChangeEvent += (e) => { pathEuddraft.Value = fileEuddraft.Content; };
        }

        private void btnLogin_Click(object sender, System.Windows.RoutedEventArgs e) {
            config.Username = txtUsername.Text;
            config.Password = txtPassword.Password;

            txtUsername.Background = cDefault;
            txtPassword.Background = cDefault;
            txtAPIToken.Background = cDefault;
            SetEnabled(false);
            new AsyncJob(() => {
                return model.GetRemoteClient().GetToken(config.Username, config.Password);
            }, (object res) => {
                SetEnabled(true);
                if(res is string) {
                    config.API = (string)res;
                    txtAPIToken.Text = config.API;

                    txtUsername.Background = cSuccess;
                    txtPassword.Background = cSuccess;
                    txtAPIToken.Background = cSuccess;
                } else {
                    txtUsername.Background = cError;
                    txtPassword.Background = cError;
                    ErrorMessage.Show("Invalid username or password");
                }
            }).Run();
        }

        private void btnValidate_Click(object sender, System.Windows.RoutedEventArgs e) {
            config.API = txtAPIToken.Text;

            if(txtUsername.Text == null) {
                ErrorMessage.Show("Fill username as well");
                return;
            }

            SetEnabled(false);
            txtUsername.Background = cDefault;
            txtPassword.Background = cDefault;
            txtAPIToken.Background = cDefault;
            string username = txtUsername.Text;
            new AsyncJob(() => {
                return model.GetRemoteClient().TokenValid(config.API, username, out username);
            }, (object res) => {
                SetEnabled(true);
                if (res is true) {
                    txtUsername.Text = username;
                    config.Username = username;
                    txtAPIToken.Background = cSuccess;
                    txtUsername.Background = cSuccess;
                } else {
                    txtUsername.Background = cError;
                    txtPassword.Background = cError;
                    txtAPIToken.Background = cError;
                    ErrorMessage.Show("Token invalid");
                }
            }).Run();
        }

    }
}
