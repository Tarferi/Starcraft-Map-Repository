using GUILib.data;
using GUILib.db;
using GUILib.ui.utils;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Threading;

namespace GUILib.ui.SettingsWnd {

    public partial class SettingsWnd : UserControl {

        private Model model;

        private Path pathMaps;
        private Path pathTemp;
        private Path pathProtector;
        private Path pathEuddraft;

        private void SetEnabled(bool enabled) {
            fileMaps.IsEnabled = enabled;
            fileTemp.IsEnabled = enabled;
            fileProtector.IsEnabled = enabled;
            fileEuddraft.IsEnabled = enabled;
            btnLogout.IsEnabled = enabled;
        }

        private void setupPath(FileInput input, ref Path p, string name, bool dir=false, string ext=null, string def=null) {
            p = model.GetPath(name);
            input.DirectoryPicker = dir;

            if (dir) {
            } else {
                if (def != null) {
                    fileProtector.FileExtensionDefaultExtension = def;
                }
                if (ext != null) { 
                    fileProtector.FileExtensionAllFilters = ext;
                }
            }
            input.Content = p.Value;
            Path path = p;
            input.FileInputChangeEvent += (e) => {
                if (path.Value != input.Content) {
                    path.Value = input.Content;
                }
            };
            path.Watch((pth) => {
                AsyncManager.OnUIThread(() => {
                    if (pth.Value != input.Content) {
                        input.Content = pth.Value;
                    }
                }, ExecutionOption.Blocking);
            });
        }

        public SettingsWnd() {
            InitializeComponent();

            model = Model.Create();

            setupPath(fileMaps, ref pathMaps, "maps", dir: true);
            setupPath(fileTemp, ref pathTemp, "temp", dir: true);
            setupPath(fileProtector, ref pathProtector, "protector", ext: "ScProtectionToolchain.exe|*.exe", def: "*.exe");
            setupPath(fileEuddraft, ref pathEuddraft, "euddraft", dir: true);

            model.GetConfig().Watch((cfg) => {
                AsyncManager.OnUIThread(() => {
                    if (cfg.Username != txtUsername.Text) {
                        txtUsername.Text = cfg.Username;
                    }
                    if (cfg.Username == null || cfg.Username == "") {
                        btnLogout.IsEnabled = false;
                    } else {
                        btnLogout.IsEnabled = true;
                    }
                }, ExecutionOption.Blocking);
            });

            txtUsername.Text = model.GetConfig().Username;
            if (txtUsername.Text == null || txtUsername.Text == "") {
                btnLogout.IsEnabled = false;
            } else {
                btnLogout.IsEnabled = true;
            }
        }

        private void btnLogout_Click(object sender, System.Windows.RoutedEventArgs e) {
            model.ResetConfig();
        }
    }
}
