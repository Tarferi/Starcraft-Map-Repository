using GUILib.data;
using GUILib.ui.utils;
using System.Windows.Controls;
using System.Windows;
using System.Reflection;
using System;

namespace GUILib.ui {
    
    public partial class MainContent : UserControl {

        private Model model;

        public bool InfoPanelVisible {
            set {
                if (value) {
                    Debugger.LogFun = (e) => {
                        AsyncManager.OnUIThread(() => {
                            txtCurrentOperation.Text = e;
                        }, ExecutionOption.Blocking);
                    };
                    infoPanel.Visibility = Visibility.Visible;
                    progress.Visibility = Visibility.Visible;
                } else {
                    Debugger.LogFun = (e) => { };
                    infoPanel.Visibility = Visibility.Collapsed;
                    progress.Visibility = Visibility.Collapsed;
                }
            }
        }

        public MainContent() {
            InitializeComponent();
            AsyncManager.Bootstrap(this);

            model = Model.Create();
            if (model == null) {
                ErrorMessage.Show("Failed to create model");
            }

            InfoPanelVisible = false;
            progress.Visibility = Visibility.Hidden;

            Debugger.WorkStatus = (e) => {
                AsyncManager.OnUIThread(() => {
                    //progress.Visibility = e ? Visibility.Visible : Visibility.Hidden;
                    progress.IsIndeterminate = e;
                    txtCurrentOperation.Text = "Ready";
                }, ExecutionOption.Blocking);
            };

            if (Debugger.IsDebugging) {
                tabs.SelectedIndex = 1;
            }
        }
    }
}
