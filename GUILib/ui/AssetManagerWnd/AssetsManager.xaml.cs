using GUILib.data;
using GUILib.db;
using GUILib.ui.utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace GUILib.ui.AssetManagerWnd {
    
    public partial class AssetsManager : UserControl {

        private AssetManager am = null;

        public AssetsManager() {
            InitializeComponent();
            StringBuilder sb = new StringBuilder();
            Model model = Model.Create();
            am = model.GetAssetManager();

            Action updateLocal = () => {
                if (txtInput.Text != am.Inputs) {
                    txtInput.Text = am.Inputs;
                }
                if (fileMaps.Content != am.Output) {
                    fileMaps.Content = am.Output;
                }
            };

            am.Watch((eam) => {
                AsyncManager.OnUIThread(updateLocal, ExecutionOption.DoOtherJobsWhileBlocking);
            });
            updateLocal();
        }

        private void SetEnabled(bool enabled) {
            txtInput.IsEnabled = enabled;
            lstOut.IsEnabled = enabled;
            btnRun.IsEnabled = enabled;
        }

        private bool AsyncProcessFile(string output, string path, FileStream fs) {
            return true;
        }

        private void Run() {
            List<Pair<string, FileStream>> inputFiles = new List<Pair<string, FileStream>>();
            bool error = true;
            try {
                foreach (string input in txtInput.Text.Split('\n')) {
                    if (input.Length > 0) {
                        if (File.Exists(input.Trim())) {
                            FileStream fs = File.OpenRead(input.Trim());
                            inputFiles.Add(new Pair<string, FileStream>(input.Trim(), fs));
                        } else {
                            MessageBox.Show("Cannot open file:\n" + input.Trim());
                            return;
                        }
                    }
                }
                error = false;
            } catch(Exception e) {
                Debugger.Log(e);
            } finally {
                if (error) {
                    foreach(Pair<string, FileStream> fs in inputFiles) {
                        fs.right.Close();
                    }
                    inputFiles.Clear();
                }
            }
            if (!error) {
                string t1 = txtInput.Text;
                string t2 = fileMaps.Content;
                
                if(am.Inputs != t1) {
                    am.Inputs = t1;
                }
                if(am.Output != t2) {
                    am.Output = t2;
                }

                SetEnabled(false);
                new AsyncJob(() => {
                    bool ok = true;
                    try {
                        foreach (Pair<string, FileStream> fs in inputFiles) {
                            if (ok) {
                                ok &= AsyncProcessFile(t2, fs.Left, fs.right);
                            }
                        }
                        return ok;
                    } finally {
                        foreach (Pair<string, FileStream> fs in inputFiles) {
                            fs.right.Close();
                        }
                        inputFiles.Clear();
                    }
                    return ok;
                }, (res) => {
                    SetEnabled(true);
                    if(res is true) {
                        
                    } else {
                        ErrorMessage.Show("Assets pack failed");
                    }
                }).Run();
            }
        }

        private void btnRun_Click(object sender, RoutedEventArgs e) {
            Run();
        }

    }
}
