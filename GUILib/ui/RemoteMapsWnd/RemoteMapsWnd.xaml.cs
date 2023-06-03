using System;
using System.Collections.Generic;
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

            if (Debugger.IsDebugging) {
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

                    if (Debugger.IsDebugging) {
                        RemoteMap rm = null;
                        foreach (object m in lstData.ItemsSource) {
                            if (rm == null) {
                                rm = (RemoteMap)m;
                            }
                        }
                        ShowMapPreview(rm);

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

        private void Download(RemoteMap map) {
            return;
        }

        private void VisitMap(RemoteMap map) {
            System.Diagnostics.Process.Start("https://scmscx.com/map/" + map.RemoteID);
        }

        private void ShowMapPreview(RemoteMap map) {
            ImageSource s = map.PreviewImageSource;
            if (s != null) {
                MapPreviewWnd wnd = new MapPreviewWnd(s);
                wnd.ShowDialog();
            } else {
                // Download map
                new AsyncJob(() => {
                    return model.GetMapMainCHK(map.CHK_Hash); 
                }, (object res) => {
                    if(res is byte[]) {
                        byte[] chk = (byte[])res;
                        ImageSource src = MapRenderer.RenderMap(chk);
                        if (src != null) {
                            MapPreviewWnd wnd = new MapPreviewWnd(src);
                            wnd.ShowDialog();
                            return;
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

        private void Button_Click(object sender, System.Windows.RoutedEventArgs e) {
            Button b = (Button)e.OriginalSource;
            Download((RemoteMap)b.DataContext);
        }

        private void Button_Click_1(object sender, System.Windows.RoutedEventArgs e) {
            Button b = (Button)e.OriginalSource;
            VisitMap((RemoteMap)b.DataContext);
        }

        private void Image_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e) {
            Image b = (Image)e.OriginalSource;
            ShowMapPreview((RemoteMap)b.DataContext);
        }
    }

}
