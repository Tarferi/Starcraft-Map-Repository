using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows.Controls;
using System.Windows.Media;
using GUILib.data;
using GUILib.db;
using GUILib.starcraft;
using GUILib.ui.PreviewWnd;
using GUILib.ui.utils;

namespace GUILib.ui.RemoteMapsWnd {

    public partial class RemoteMapsWnd : UserControl {

        RemoteMapCollection data;

        private int currentPage = 0;
        private int pageSize = 10;

        public int pageSizePicker { get { return Array.IndexOf(pageSizes, pageSize); } set { pageSize = pageSizes[value]; OnPageDataChanged(); } }
        private int[] pageSizes = new int[] { 5, 10, 20, 50, 100 };

        public string CurrentPage { get => "" + (currentPage + 1); }
        public int TotalPages { get => (data.Count / pageSize) + (data.Count % pageSize > 0 ? 1 : 0); }
        public bool IsLastPage { get => currentPage + 1 == TotalPages || TotalPages == 0; }
        public bool IsFirstPage { get => currentPage == 0; }

        private bool _loading = false;
        private bool loading { get => _loading; set { _loading = value; UpdateBinding(); } }

        public bool NotLoading { get => !loading; }
        public bool EnableButtonsBack { get => !IsFirstPage && NotLoading; }
        public bool EnableButtonsNext { get => !IsLastPage && NotLoading; }

        Model model;

        public RemoteMapsWnd() {
            InitializeComponent();
            DataContext = this;
            data = new RemoteMapCollection();
            model = Model.Create();

            if (GUILib.data.Debugger.IsDebuggingMapPreview || GUILib.data.Debugger.IsDebuggingMapDownload) {
                //txtFilter.Text = "3v3_Shared_Base_2000_Be_MZ3";
                txtFilter.Text = "Sniper Blue";
                Search(txtFilter.Text);
            }
        }
        
        private void DisposeCurrentMaps() {
            if (lstData.ItemsSource != null) {
                foreach (object m in lstData.ItemsSource) {
                    RemoteMap rm = (RemoteMap)m;
                    rm.DecRef();
                }
            }
        }

        private void OnPageDataChanged() {
            loading = true;
            this.data.QueryPage(currentPage * pageSize, pageSize, (maps) => {
                loading = false;
                if (maps != null) {
                    DisposeCurrentMaps();
                    lstData.ItemsSource = maps;

                    if (GUILib.data.Debugger.IsDebuggingMapPreview || GUILib.data.Debugger.IsDebuggingMapDownload) {
                        int idx = 0;
                        RemoteMap rm = null;
                        foreach (object m in lstData.ItemsSource) {
                            if (rm == null) {
                                if (idx > 0) {
                                    idx--;
                                } else {
                                    rm = (RemoteMap)m;
                                }
                            }
                        }
                        if (GUILib.data.Debugger.IsDebuggingMapPreview) {
                            comboPreviewTileset.SelectedValue = "Carbot";
                            ShowMapPreview(rm);
                        } else if (GUILib.data.Debugger.IsDebuggingMapDownload) {
                            Download(rm, true, true);
                        }
                    }
                } else {
                    ErrorMessage.Show("Failed to search remote maps");
                }
            });
        }

        private void UpdateBinding() {
            comboPageSize.GetBindingExpression(ComboBox.SelectedIndexProperty).UpdateTarget();
            comboPageSize.GetBindingExpression(ComboBox.IsEnabledProperty).UpdateTarget();
            lblCurrentPage.GetBindingExpression(TextBlock.TextProperty).UpdateTarget();
            btnFirst.GetBindingExpression(Button.IsEnabledProperty).UpdateTarget();
            btnPrev.GetBindingExpression(Button.IsEnabledProperty).UpdateTarget();
            btnNext.GetBindingExpression(Button.IsEnabledProperty).UpdateTarget();
            btnLast.GetBindingExpression(Button.IsEnabledProperty).UpdateTarget();
            txtFilter.GetBindingExpression(TextBox.IsEnabledProperty).UpdateTarget();
            lstData.GetBindingExpression(ListView.IsEnabledProperty).UpdateTarget();
            comboPreviewTileset.GetBindingExpression(ComboBox.IsEnabledProperty).UpdateTarget();
        }

        private void Search(string txt) {
            loading = true;
            new AsyncJob(() => {
                return model.SearchMaps(txt == "" ? null : txt);
            }, (object res) => {
                loading = false;
                if (res is List<RemoteSearchedMap>) {
                    List<RemoteSearchedMap> data = (List<RemoteSearchedMap>)res;
                    this.data.Reset(data);
                    currentPage = 0;
                    OnPageDataChanged();
                } else {
                    ErrorMessage.Show("Failed to search remote maps");
                }
                
            }).Run();
            
        }

        private void Download(RemoteMap map, bool open, bool terrainOnly) {
            db.Path pTemp = model.GetPath("temp");
            db.Path pMaps = model.GetPath("maps");
            try {
                string temp = model.WorkingDir + "\\temp";
                if (pTemp.Value == "") {
                    WarningMessage.Show("Temporary folder not specified. Will be using\n" + temp+"\nIf you wish to use custom temporary folder, please do so in settings tab.");
                } else {
                    temp = pTemp.Value;
                }
                if (!Directory.Exists(temp)) {
                    Directory.CreateDirectory(temp);
                }
                if (!Directory.Exists(temp)) {
                    ErrorMessage.Show("Failed to create temporary folder at\n" + temp + "\nIf you wish to use custom temporary folder, please do so in settings tab.");
                    return;
                }
                if (pTemp.Value != temp) {
                    pTemp.Value = temp;
                }

                string maps = model.WorkingDir + "\\maps";
                if (pMaps.Value == "") {
                    WarningMessage.Show("Maps folder not specified. Will be using\n" + maps+"\nIf you wish to use custom maps folder, please do so in settings tab.");
                } else {
                    maps = pMaps.Value;
                }
                if (!Directory.Exists(maps)) {
                    Directory.CreateDirectory(maps);
                }
                if (!Directory.Exists(maps)) {
                    ErrorMessage.Show("Failed to create maps folder at\n" + temp + "\nIf you wish to use custom maps folder, please do so in settings tab.");
                    return;
                }
                if (pMaps.Value != maps) {
                    pMaps.Value = maps;
                }

                FileStream sTmp = null;
                string tempFile = temp + "\\map_" + map.MPQ_Hash + ".tmp";
                string mapsFile = maps + "\\" + map.FirstKnownFileName;

                if (terrainOnly) {
                    int idx = mapsFile.LastIndexOf(".");
                    if (idx >= 0) {
                        string part1 = mapsFile.Substring(0, idx);
                        mapsFile = part1 + "_terrain_only." + (model.IsPlugin ? "chk" : "scx");
                    }
                }

                try {
                    sTmp = File.OpenWrite(tempFile);
                } catch(Exception e) {
                    GUILib.data.Debugger.Log(e);
                    ErrorMessage.Show("Failed to open temporary file for writing");
                    return;
                }

                loading = true;
                new AsyncJob(() => {
                    Stream stream = null;
                    try {
                        if (terrainOnly) {
                            byte[] chk = model.GetMapMainCHK(map.CHK_Hash);
                            if (chk != null) {
                                chk = CHKFixer.TerrainOnly(chk);
                            }
                            if (chk != null) {
                                stream = new MemoryStream(chk);
                            }
                        } else {
                            stream = model.DownloadMap(map);
                        }
                        if (stream != null) {
                            long readTotal = 0;
                            int readTotalPercent = 0;
                            string task = "Downloading map " + map.Name;
                            Action upt = () => {
                                AsyncManager.OnUIThread(() => {
                                    string txt = task + " (" + readTotalPercent + "%)";
                                    GUILib.data.Debugger.LogFun(txt);
                                }, ExecutionOption.Blocking);
                            };

                            byte[] buffer = new byte[4096];
                            int bytesRead = stream.Read(buffer, 0, buffer.Length);
                            int rawSize;
                            if (terrainOnly) {
                                // TODO: make this CHK_Size in the future
                                rawSize = (int)stream.Length;
                            } else {
                                if (!Int32.TryParse(map.MPQ_Size, out rawSize)) {
                                    rawSize = 0xffffff; // placeholder value
                                }
                            }
                            while (bytesRead > 0) {
                                readTotal += bytesRead;
                                long tmpx = (100 * readTotal) / (long)rawSize;
                                if (tmpx != readTotalPercent) {
                                    readTotalPercent = (int)tmpx;
                                    upt();
                                }
                                sTmp.Write(buffer, 0, bytesRead);
                                bytesRead = stream.Read(buffer, 0, buffer.Length);
                            }
                            if (readTotal != rawSize && rawSize != 0xffffff) {
                                ErrorMessage.Show("Downloaded " + readTotal + " bytes of " + rawSize + " bytes total");
                                return false;
                            }
                            sTmp.Close();
                            bool askOverwrite = true;
                            if (GUILib.data.Debugger.IsDebuggingMapDownload) {
                                askOverwrite = false;
                            } else if(open && terrainOnly) {
                                askOverwrite = false;
                            }
                            if (File.Exists(mapsFile)) {
                                if (askOverwrite) {
                                    if (Prompt.ConfirmModal("File " + mapsFile + " already exists.\nReplace existing file?", "File exists") == true) {
                                        File.Replace(tempFile, mapsFile, null);
                                    }
                                } else {
                                    File.Replace(tempFile, mapsFile, null);
                                }
                            } else {
                                File.Move(tempFile, mapsFile);
                            }
                            return true;
                        }


                    } catch(Exception e) {
                        GUILib.data.Debugger.Log(e);
                    } finally {
                        sTmp.Close();
                        if (stream != null) {
                            stream.Close();
                        }
                        try {
                            File.Delete(tempFile);
                        } catch (Exception) { }
                    } 
                    return false;
                }, (res) => {
                    loading = false;
                    if(res is true) {
                        if (open) {
                            if (model.IsPlugin) {
                                model.PluginInterface.CallOpenMap(mapsFile);
                            } else {
                                using (Process fileopener = new Process()) {
                                    fileopener.StartInfo.FileName = "explorer";
                                    fileopener.StartInfo.Arguments = "\"" + mapsFile + "\"";
                                    fileopener.Start();
                                }
                            }
                        }
                    } else {
                        ErrorMessage.Show("Failed to download map");
                    }
                }).Run();


            } finally {
                pTemp.DecRef();
                pMaps.DecRef();
            }
            return;
        }

        private void VisitMap(RemoteMap map) {
            System.Diagnostics.Process.Start("https://scmscx.com/map/" + map.RemoteID);
        }

        private void ShowMapPreview(RemoteMap map) {
            ImageSource s = map.PreviewImageSource;
            bool asyncRender = true;
            string tileset = comboPreviewTileset.SelectedValue + ".bin";
            if (s != null) {
                MapPreviewWnd wnd = new MapPreviewWnd(new ImageSource[] { s });
                wnd.ShowDialog();
            } else {
                // Download map
                new AsyncJob(() => {
                    byte[] chk = model.GetMapMainCHK(map.CHK_Hash);
                    if (asyncRender) {
                        if (chk != null) {
                            return MapRenderer.RenderMap(chk, tileset, "Remaster");
                        }
                    } else {
                        return chk;
                    }
                    return null;
                }, (object res) => {
                    if (asyncRender) {
                        if (res is ImageSource[]) {
                            ImageSource[] srcs = (ImageSource[])res;
                            MapPreviewWnd wnd = new MapPreviewWnd(srcs);
                            wnd.ShowDialog();
                            return;
                        }
                    } else {
                        if(res is byte[]) {
                            byte[] chk = (byte[])res;
                            ImageSource[] src = MapRenderer.RenderMap(chk, tileset, "Remaster");
                            if (src != null) {
                                MapPreviewWnd wnd = new MapPreviewWnd(src);
                                wnd.ShowDialog();
                                return;
                            }
                        }
                    }
                    ErrorMessage.Show("Failed to download remote map preview");
                }).Run();
            }
        }

        private void txtFilter_KeyDown(object sender, System.Windows.Input.KeyEventArgs e) {
            if(e.Key == System.Windows.Input.Key.Enter) {
                Search(txtFilter.Text.Trim());
            }
        }

        private void btnFirst_Click(object sender, System.Windows.RoutedEventArgs e) {
            if (currentPage != 0) {
                currentPage = 0;
                OnPageDataChanged();
            }
        }

        private void btnPrev_Click(object sender, System.Windows.RoutedEventArgs e) {
            if (currentPage > 0) {
                currentPage--;
                OnPageDataChanged();
            }
        }

        private void btnNext_Click(object sender, System.Windows.RoutedEventArgs e) {
            if (currentPage + 1 < TotalPages) {
                currentPage++;
                OnPageDataChanged();
            }
        }

        private void btnLast_Click(object sender, System.Windows.RoutedEventArgs e) {
            if (currentPage + 1 != TotalPages) {
                currentPage = TotalPages - 1;
                OnPageDataChanged();
            }
        }

        private void Image_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e) {
            Image b = (Image)e.OriginalSource;
            ShowMapPreview((RemoteMap)b.DataContext);
        }

        private void btnVisit_Click(object sender, System.Windows.RoutedEventArgs e) {
            Button b = (Button)e.OriginalSource;
            VisitMap((RemoteMap)b.DataContext);
        }

        private void btnDownload_Click(object sender, System.Windows.RoutedEventArgs e) {
            Button b = (Button)e.OriginalSource;
            Download((RemoteMap)b.DataContext, false, false);
        }

        private void btnOpenTerrain_Click(object sender, System.Windows.RoutedEventArgs e) {
            Button b = (Button)e.OriginalSource;
            Download((RemoteMap)b.DataContext, true, true);
        }

        private void btnDownloadOpen_Click(object sender, System.Windows.RoutedEventArgs e) {
            Button b = (Button)e.OriginalSource;
            Download((RemoteMap)b.DataContext, true, false);
        }
    }

}
