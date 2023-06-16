using GUILib.data;
using GUILib.db;
using GUILib.ui.AssetPackerWnd;
using GUILib.ui.utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Controls;

namespace GUILib.ui.AssetManagerWnd {
    
    public partial class AssetManager : UserControl {

        private static int max(int x, int y) {
            return x > y ? x : y;
        }

        Model model;

        class LocalAsset {

            public string Path { get; set; }
            public string Name { get; set; }
            public string Category { get; set; }
            public string Size { get; set; }

            public LocalAsset(string path, string name, string category, string size) {
                this.Path = path;
                this.Name = name;
                this.Category = category;
                this.Size = size;
            }
        }

        public AssetManager() {
            InitializeComponent();
            model = Model.Create();
        }

        private void SetEnabled(bool enabled) {
            btnDetect.IsEnabled = enabled;
            btnUpdate.IsEnabled = enabled;
            lstLocal.IsEnabled = enabled;
            lstRemote.IsEnabled = enabled;
        }

        private static void iterFiles(string dir, Action<string> fun) {
            if (!Directory.Exists(dir)) {
                Directory.CreateDirectory(dir);
            }
            foreach (string file in Directory.GetFiles(dir)) {
                fun(file);
            }
        }

        private static string WithoutPath(string path) {
            int idx = max(path.LastIndexOf("/"), path.LastIndexOf("\\"));
            if (idx >= 0) {
                string name = path.Substring(idx + 1);
                return name;
            }
            return path;
        }

        private static string WithoutExtension(string name) {
            int idx = name.LastIndexOf(".");
            if (idx >= 0) {
                name = name.Substring(0, idx);
            }
            return name;
        }

        private static LocalAsset ReadFile(string path) {
            FileStream fs = null;
            try {
                fs = File.OpenRead(path);
                if (fs != null) {
                    int csize = (int)fs.Length;
                    byte[] data = Encoding.UTF8.GetBytes(ResourceTypes.MAGIC);
                    byte[] lr = new byte[data.Length];
                    if (fs.Read(lr, 0, lr.Length) == lr.Length) {
                        string lrs = Encoding.UTF8.GetString(lr);
                        byte ctype = (byte)fs.ReadByte();
                        int dsize = ImageEncoder.ReadInt(fs);
                        int position = (int)fs.Position;
                        if(lrs == ResourceTypes.MAGIC && dsize == csize - position) {
                            string type = ResourceTypes.TypeToName(ctype);
                            if (type != null) {
                                string strsize = ObservableObject<AssetManager>.FormatFileSize(csize);
                                string name = WithoutPath(path);
                                LocalAsset la = new LocalAsset(name, WithoutExtension(name), type, strsize);
                                return la;
                            }
                        }
                    }
                }
            } catch(Exception e) {
                Debugger.Log(e);
            } finally {
                if (fs != null) {
                    fs.Close();
                    fs = null;
                }
            }
            return null;
        }

        private string ResourcesDirectory { get => model.WorkingDir + "\\resources"; }

        private List<LocalAsset> readLocals() {
            List<LocalAsset> data = new List<LocalAsset>();
            try {
                string wd = ResourcesDirectory;
                iterFiles(wd, (path) => {
                    LocalAsset la = ReadFile(path);
                    if (la != null) {
                        data.Add(la);
                    }
                });
                return data;
            } catch (Exception ex) {
                Debugger.Log(ex);
                ErrorMessage.Show("Failde to list local resources");
            }
            return null;
        }

        private void RunDetect() {
            lstLocal.ItemsSource = null;
            List<LocalAsset> data = readLocals();
            if (data != null) {
                lstLocal.ItemsSource = data;
            }
        }

        private void btnDetect_Click(object sender, System.Windows.RoutedEventArgs e) {
            RunDetect();
        }

        private void btnUpdate_Click(object sender, System.Windows.RoutedEventArgs e) {
            SetEnabled(false);
            lstRemote.ItemsSource = null;
            new AsyncJob(() => {
                List<LocalAsset> locals = readLocals();
                if (locals == null) {
                    ErrorMessage.Show("Failed to read local asset list");
                    return null;
                }
                List<RemoteAsset> lst = model.GetRemoteAssets();
                foreach(RemoteAsset ra in lst) {
                    ra.ExistsLocally = false;
                    foreach (LocalAsset local in locals) {
                        if (local.Size == ra.Size && local.Path == ra.Path) {
                            ra.ExistsLocally = true;
                        }
                    }
                }
                return lst;
            }, (res) => {
                SetEnabled(true);
                if (res is List<RemoteAsset>) {
                    List<RemoteAsset> data = (List<RemoteAsset>)res;
                    lstRemote.ItemsSource = data;
                } else {
                    ErrorMessage.Show("Failed to get remote assets");
                }
            }).Run();
        }

        private void btnDownload_Click(object sender, System.Windows.RoutedEventArgs e) {
            Button btn = (Button)sender;
            RemoteAsset ra = (RemoteAsset)btn.DataContext;
            List<LocalAsset> locals = readLocals();
            foreach(LocalAsset local in locals) {
                if (local.Size == ra.Size && local.Path == ra.Path) {
                    ErrorMessage.Show("Remote resource exists locally");
                    return;
                }
            }
            SetEnabled(false);
            new AsyncJob(() => {
                string resultPath = ResourcesDirectory + "\\" + ra.Path;
                string resultPathTmp = ResourcesDirectory + "\\" + ra.Path + "_tmp";
                try {
                    if (File.Exists(resultPathTmp)) {
                        File.Delete(resultPathTmp);
                    }
                } catch(Exception ex) {
                    Debugger.Log(ex);
                    return false;
                }
                
                long readTotal = 0;
                int readTotalPercent = 0;

                string task = "Downloading remote asset " + ra.Name;
                Action upt = () => {
                    AsyncManager.OnUIThread(() => {
                        string txt = task + " (" + readTotalPercent + "%)";
                        Debugger.LogFun(txt);
                    }, ExecutionOption.Blocking);
                };

                byte[] buffer = new byte[4096];
                using (FileStream tmp = File.OpenWrite(resultPathTmp)) {
                    Stream rawFile = null;
                    for (int partI = 0; partI < ra.Parts; partI++) {
                        try {
                            rawFile = model.GetRemoteAsset(ra, partI);
                            if (rawFile != null) {
                                int bytesRead = rawFile.Read(buffer, 0, buffer.Length);
                                while (bytesRead > 0) {
                                    readTotal += bytesRead;
                                    long tmpx = (100 * readTotal) / (long)ra.RawSize;
                                    if (tmpx != readTotalPercent) {
                                        readTotalPercent = (int)tmpx;
                                        upt();
                                    }
                                    tmp.Write(buffer, 0, bytesRead);
                                    bytesRead = rawFile.Read(buffer, 0, buffer.Length);
                                }
                            }
                        } catch (Exception ex) {
                            Debugger.Log(ex);
                            break;
                        } finally {
                            if (rawFile != null) {
                                rawFile.Close();
                                rawFile = null;
                            }
                        }
                    }
                }
              
                if (readTotal != ra.RawSize) {
                    ErrorMessage.Show("Downloaded " + readTotal + " bytes of " + ra.RawSize + " bytes total");
                    return false;
                }

                try {
                    if (!Directory.Exists(ResourcesDirectory)) {
                        Directory.CreateDirectory(ResourcesDirectory);
                    }

                    LocalAsset la = ReadFile(resultPathTmp);
                    if (la == null) {
                        ErrorMessage.Show("Downloaded file is not valid");
                        File.Delete(resultPathTmp);
                        return false;
                    }

                    if (File.Exists(resultPath)) {
                        File.Replace(resultPathTmp, resultPath, null);
                    } else {
                        File.Move(resultPathTmp, resultPath);
                    }
                    return true;
                } catch (Exception ex) {
                    Debugger.Log(ex);
                }

                
                return false;
            }, (res) => {
                SetEnabled(true);
                if(res is true) {
                    ra.ExistsLocally = true;
                    List<RemoteAsset> ras = (List<RemoteAsset>)lstRemote.ItemsSource;
                    lstRemote.ItemsSource = null;
                    lstRemote.ItemsSource = ras;
                    RunDetect();
                } else {
                    ErrorMessage.Show("Failed to download remote asset");
                }
            }
            ).Run();
        }
    }
}
