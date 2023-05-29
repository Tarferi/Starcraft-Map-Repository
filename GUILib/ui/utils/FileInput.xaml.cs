using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GUILib.ui.utils {

    public partial class FileInput : UserControl {

        public delegate void FileInputChangeHandler(String selectionChanged);

        public event FileInputChangeHandler FileInputChangeEvent;

        public FileInput() {
            InitializeComponent();
        }

        public bool DirectoryPicker = false;
        public String FileExtensionDefaultExtension = "*.*";
        public String FileExtensionAllFilters = "All files (*.*)|*.*";

        new public String Content { get { return txt.Text; } set { txt.Text = value; } }

        private void openPicker() {
            if (DirectoryPicker) {
                System.Windows.Forms.FolderBrowserDialog dlg = new System.Windows.Forms.FolderBrowserDialog();
                System.Windows.Forms.DialogResult res = dlg.ShowDialog();
                if (res == System.Windows.Forms.DialogResult.OK) {
                    string filename = dlg.SelectedPath;
                    Content = filename;
                    if (FileInputChangeEvent != null) {
                        FileInputChangeEvent(Content);
                    }
                }
            } else {
                Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
                dlg.DefaultExt = FileExtensionDefaultExtension;
                dlg.Filter = FileExtensionAllFilters;
                Nullable<bool> result = dlg.ShowDialog();
                if (result == true) {
                    string filename = dlg.FileName;
                    Content = filename;
                    if (FileInputChangeEvent != null) {
                        FileInputChangeEvent(Content);
                    }
                }
            }
        }

        private void btn_Click(object sender, RoutedEventArgs e) {
            openPicker();
        }

        private void txt_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            openPicker();
        }

        private void txt_TextChanged(object sender, TextChangedEventArgs e) {
            if (FileInputChangeEvent != null) {
                FileInputChangeEvent(Content);
            }
        }
    }
}
