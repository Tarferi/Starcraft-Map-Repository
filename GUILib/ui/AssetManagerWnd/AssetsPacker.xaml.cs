using GUILib.data;
using GUILib.db;
using GUILib.ui.utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace GUILib.ui.AssetManagerWnd {
    
    public partial class AssetsPacker : UserControl {

        public static String FormatFileSize(int bytes) {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = (double)bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1) {
                order++;
                len = len / 1024;
            }
            return String.Format("{0:0.##} {1}", len, sizes[order]);
        }

        public class AssetItem {
            public uint originalSize = 0;
            public uint packedSize = 0;
            public string Path { get; set; }
            public string OriginalSize { get => FormatFileSize((int)originalSize); }
            public string PackedSize { get=> FormatFileSize((int)packedSize); }
            public string Ratio { get {
                    uint rx = (100 * packedSize) / originalSize;
                    return rx + "%";
                }
            }
        }

        public class AssetTransformation {
            public Stream InputBitmap;
            public Stream InputMapping;
            public Stream Output;
            public readonly string Name;

            public AssetTransformation(string name) {
                this.Name = name;
            }
        }

        private readonly Model model;
        private AssetPacker loadedAssetPacker = null;

        public List<string> AssetPackers {
            get {
                List<string> lst = model.GetAllAssetPackerNames();
                lst.Add("<new>");
                lst.Add("<edit>");
                return lst;
            }
        }

        private void updateLocal(AssetPacker packer) {
            AsyncManager.OnUIThread(()=> {
                if (packer == null) {
                    fileIn.Content = "";
                    fileOut.Content = "";
                    fileParts.Content = "";
                } else {
                    if (fileIn.Content != packer.Inputs) {
                        fileIn.Content = packer.Inputs;
                    }
                    if (fileOut.Content != packer.OutputFinal) {
                        fileOut.Content = packer.OutputFinal;
                    }
                    if (fileParts.Content != packer.OutputParts) {
                        fileParts.Content = packer.OutputParts;
                    }
                }
            }, ExecutionOption.DoOtherJobsWhileBlocking);
        }

        public AssetsPacker() {
            InitializeComponent();
            model = Model.Create();
            DataContext = this;
            
            fileIn.DirectoryPicker = true;
            fileParts.DirectoryPicker = true;
            fileOut.DirectoryPicker = false;
        }

        private void SetEnabled(bool enabled) {
            fileIn.IsEnabled = enabled;
            fileOut.IsEnabled = enabled;
            fileParts.IsEnabled = enabled;
            btnRun.IsEnabled = enabled;
            btnPack.IsEnabled = enabled;
        }

        private void LoadConfiguartion(string name) {
            if (loadedAssetPacker != null) {
                loadedAssetPacker.Unwatch(updateLocal);
                loadedAssetPacker.DecRef();
                loadedAssetPacker = null;
                updateLocal(null);
            }
            if (name != null) {
                loadedAssetPacker = model.GetAssetPacker(name);
                loadedAssetPacker.Watch(updateLocal);
                comboConfigs.ItemsSource = null;
                comboConfigs.ItemsSource = AssetPackers;
                comboConfigs.SelectedItem = name;
                updateLocal(loadedAssetPacker);
            }
        }

        private void Pack() {

        }

        private void Run() {
            List<AssetTransformation> inputFiles = new List<AssetTransformation> ();
            List<AssetItem> items = new List<AssetItem>();
            lstOut.ItemsSource = items;

            string[] eras = new string[] {
                "badlands",
                "platform",
                "install",
                "ashworld",
                "jungle",
                "desert",
                "ice",
                "twilight"
            };

            bool error = true;
            try {
                foreach (string era in eras) {
                    string fBitmap = fileIn.Content + "/" + era + ".png";
                    string fMapping = fileIn.Content + "/" + era + ".map";
                    string fOutput = fileOut.Content + "/" + era + ".bin";
                    if (!File.Exists(fBitmap)) {
                        ErrorMessage.Show("File " + fBitmap + "\ndoes not exist.");
                        return;
                    } else if (!File.Exists(fMapping)) {
                        ErrorMessage.Show("File " + fMapping + "\ndoes not exist.");
                        return;
                    } else {
                        AssetTransformation at = new AssetTransformation(era);
                        inputFiles.Add(at);
                        at.InputBitmap = File.OpenRead(fBitmap);
                        at.InputMapping = File.OpenRead(fMapping);
                        at.Output = File.OpenWrite(fOutput);
                    }
                }
                error = false;
            } catch (Exception e) {
                Debugger.Log(e);
                return;
            } finally {
                if (error) {
                    foreach (AssetTransformation at in inputFiles) {
                        if (at.InputBitmap != null) {
                            at.InputBitmap.Close();
                        }
                        if (at.InputMapping != null) {
                            at.InputMapping.Close();
                        }
                        if (at.Output != null) {
                            at.Output.Close();
                        }
                    }
                }
            }
            if (!error) {
                string t1 = fileIn.Content;
                string t2 = fileParts.Content;
                
                if(loadedAssetPacker.Inputs != t1) {;
                    loadedAssetPacker.Inputs = t1;
                }
                if(loadedAssetPacker.OutputParts != t2) {
                    loadedAssetPacker.OutputParts = t2;
                }

                SetEnabled(false);
                new AsyncJob(() => {
                    bool ok = true;
                    try {
                        foreach (AssetTransformation fs in inputFiles) {
                            if (ok) {
                                uint resultSize = 0;
                                Debugger.LogFun("Processing " + fs.Name);
                                ok &= ImageEncoder.AsyncWriteProcessFile(fs.InputBitmap, fs.InputMapping, fs.Output, out resultSize);
                                if (ok) {
                                    AsyncManager.OnUIThread(() => {
                                        AssetItem ai = new AssetItem();
                                        ai.Path = fs.Name;
                                        ai.originalSize = (uint)(fs.InputBitmap.Length + fs.InputMapping.Length);
                                        ai.packedSize = (uint)fs.Output.Length;
                                        items.Add(ai);
                                        lstOut.ItemsSource = null;
                                        lstOut.ItemsSource = items;
                                    }, ExecutionOption.Blocking);
                                } else {
                                    break;
                                }
                            }
                        }
                        return ok;
                    } finally {
                        foreach (AssetTransformation at in inputFiles) {
                            if (at.InputBitmap != null) {
                                at.InputBitmap.Close();
                            }
                            if (at.InputMapping != null) {
                                at.InputMapping.Close();
                            }
                            if (at.Output != null) {
                                at.Output.Close();
                            }
                        }
                    }
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

        private void btnPack_Click(object sender, RoutedEventArgs e) {
            Pack();
        }
        private void comboConfigs_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            ComboBox cb = (ComboBox)e.OriginalSource;
            if (cb.SelectedItem != null) {
                string sel = (string)cb.SelectedItem;
                if (sel == "<new>") {
                    string prompt = Prompt.Modal("New configuration name:", "Create new configuration");
                    if (prompt.Trim().Length > 0) {
                        LoadConfiguartion(prompt.Trim());
                    }
                } else if (sel == "<edit>") {

                } else {
                    if (loadedAssetPacker != null) {
                        if(loadedAssetPacker.Name == Name) {
                            return;
                        }
                    }
                    LoadConfiguartion(sel);
                }
            } else {
                if (loadedAssetPacker != null) {
                    LoadConfiguartion(null);
                }
            }
        }
    }
}
