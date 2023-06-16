using GUILib.data;
using GUILib.db;
using GUILib.ui.utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Debugger = GUILib.data.Debugger;

namespace GUILib.ui.AssetPackerWnd {

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

        public class AssetPack {
            public Stream Binary;
            public readonly string Name;

            public AssetPack(string name) {
                this.Name = name;
            }
        }

        private readonly Model model;
        private AssetPacker loadedAssetPacker = null;

        private bool ShowList {
            set {
                lstOut.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
                txtOut.Visibility = value ? Visibility.Collapsed : Visibility.Visible;
            }
        }

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
            fileOut.SaveDialog = true;
            fileCompr.DirectoryPicker = false;
            fileCompr.FileExtensionAllFilters = "AssetsPacker.exe (*.exe)|*.exe";
            fileCompr.FileExtensionDefaultExtension = "*.exe";

            fileIn.FileInputChangeEvent += (e) => {
                if (loadedAssetPacker != null) {
                    if (loadedAssetPacker.Inputs != fileIn.Content) {
                        loadedAssetPacker.Inputs = fileIn.Content;
                    }
                }
            };
            fileParts.FileInputChangeEvent += (e) => {
                if (loadedAssetPacker != null) {
                    if (loadedAssetPacker.OutputParts != fileParts.Content) {
                        loadedAssetPacker.OutputParts = fileParts.Content;
                    }
                }
            };
            fileOut.FileInputChangeEvent += (e) => {
                if (loadedAssetPacker != null) {
                    if (loadedAssetPacker.OutputFinal != fileOut.Content) {
                        loadedAssetPacker.OutputFinal = fileOut.Content;
                    }
                }
            };
            fileCompr.FileInputChangeEvent += (e) => {
                if (loadedAssetPacker != null) {
                    if (loadedAssetPacker.Compressor != fileCompr.Content) {
                        loadedAssetPacker.Compressor = fileCompr.Content;
                    }
                }
            };
            txtPublish.TextChanged += (o, e) => {
                if (loadedAssetPacker != null) {
                    if (loadedAssetPacker.PublishURL != txtPublish.Text.Trim()) {
                        loadedAssetPacker.PublishURL = txtPublish.Text.Trim();
                    }
                }
            };
            ShowList = true;
        }

        private void SetEnabled(bool enabled) {
            fileIn.IsEnabled = enabled;
            fileOut.IsEnabled = enabled;
            fileParts.IsEnabled = enabled;
            btnRun.IsEnabled = enabled;
            btnPack.IsEnabled = enabled;
            fileCompr.IsEnabled = enabled;
            comboConfigs.IsEnabled = enabled;
            txtPublish.IsEnabled = enabled;
            btnPublish.IsEnabled = enabled;
        }

        private bool updatingConfigurationList = false;

        private void LoadConfiguartion(string name) {
            if (!updatingConfigurationList) {
                updatingConfigurationList = true;
                if (loadedAssetPacker != null) {
                    loadedAssetPacker.Unwatch(updateLocal);
                    loadedAssetPacker.DecRef();
                    loadedAssetPacker = null;
                    fileIn.Content = "";
                    fileParts.Content = "";
                    fileOut.Content = "";
                    fileCompr.Content = "";
                    txtPublish.Text = "";
                    updateLocal(null);
                }
                if (name != null) {
                    loadedAssetPacker = model.GetAssetPacker(name);
                    loadedAssetPacker.Watch(updateLocal);
                    comboConfigs.ItemsSource = null;
                    comboConfigs.ItemsSource = AssetPackers;
                    comboConfigs.SelectedItem = name;
                    fileIn.Content = loadedAssetPacker.Inputs;
                    fileParts.Content = loadedAssetPacker.OutputParts;
                    fileOut.Content = loadedAssetPacker.OutputFinal;
                    fileCompr.Content = loadedAssetPacker.Compressor;
                    txtPublish.Text = loadedAssetPacker.PublishURL;
                    updateLocal(loadedAssetPacker);
                }
                updatingConfigurationList = false;
            }
        }

        private void Publish() {
            if(loadedAssetPacker == null) {
                return;
            }
            if(loadedAssetPacker.PublishURL.Trim().Length == 0) {
                ErrorMessage.Show("Invalid publish token");
                return;
            }
            txtOut.Text = "";
            ShowList = false;

            string pu = loadedAssetPacker.PublishURL.Trim();
            FileStream finclose = null;
            try {
                FileStream fin = File.OpenRead(fileOut.Content);
                finclose = fin;
                SetEnabled(false);
                finclose = null;
                new AsyncJob(() => {
                    try {
                        return model.Publish(loadedAssetPacker, fin, pu);
                    } finally {
                        fin.Close();
                    }
                }, (res) => {
                    SetEnabled(true);
                    if(res is true) {
                        txtOut.Text = "File published successfully";
                    } else {
                        ErrorMessage.Show("Publishing failed");
                    }
                }).Run();
            } finally {
                if (finclose != null) {
                    finclose.Close();
                    finclose = null;
                }
            }

        }

        private void Pack() {
            ShowList = false;
            txtOut.Text = "";

            Action<string> log = (string str) => {
                AsyncManager.OnUIThread(() => {
                    txtOut.Text += str + "\n";
                    txtOut.ScrollToEnd();
                }, ExecutionOption.Blocking);
            };

            List<Stream> inputFiles = new List<Stream>();
            Stream output = null;
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
                output = File.OpenWrite(fileOut.Content);
                if (output == null) {
                    ErrorMessage.Show("File " + output + "\nfailed to open for writing.");
                    return;
                }

                foreach (string era in eras) {
                    string fOut = fileParts.Content + "/" + era + ".bin";
                    if (!File.Exists(fOut)) {
                        ErrorMessage.Show("File " + fOut + "\ndoes not exist.");
                        return;
                    } else {
                        Stream fsOut = File.OpenRead(fOut);
                        if (fsOut == null) {
                            ErrorMessage.Show("File " + fOut + "\nfailed to open for reading.");
                            return;
                        }
                        inputFiles.Add(fsOut);
                    }
                }
                error = false;
            } catch (Exception e) {
                Debugger.Log(e);
                return;
            } finally {
                if (error) {
                    foreach (Stream at in inputFiles) {
                        at.Close();
                    }
                    if (output != null) {
                        output.Close();
                    }
                }
            }
            if (!error) {
                string t1 = fileParts.Content;
                string t2 = fileOut.Content;

                SetEnabled(false);
                new AsyncJob(() => {
                    bool ok = false;
                    try {
                        if (ImageEncoder.PackAsync(inputFiles, output, ResourceTypes.TILESET)) {
                            log("File packed to " + t2);
                            ok = true;
                        }
                    } finally {
                        foreach (Stream at in inputFiles) {
                            at.Close();
                        }
                        if (output != null) {
                            output.Close();
                        }
                    }
                    return ok;
                }, (res) => {
                    SetEnabled(true);
                    if (res is true) {
                    } else {
                        ErrorMessage.Show("Failed to pack data");
                    }
                }).Run();
            }
        }

        private static string EscapeArguments(params string[] args) {
            StringBuilder arguments = new StringBuilder();
            Regex invalidChar = new Regex("[\x00\x0a\x0d]");//  these can not be escaped
            Regex needsQuotes = new Regex(@"\s|""");//          contains whitespace or two quote characters
            Regex escapeQuote = new Regex(@"(\\*)(""|$)");//    one or more '\' followed with a quote or end of string
            for (int carg = 0; args != null && carg < args.Length; carg++) {
                if (args[carg] == null) { throw new ArgumentNullException("args[" + carg + "]"); }
                if (invalidChar.IsMatch(args[carg])) { throw new ArgumentOutOfRangeException("args[" + carg + "]"); }
                if (args[carg] == String.Empty) { arguments.Append("\"\""); } else if (!needsQuotes.IsMatch(args[carg])) { arguments.Append(args[carg]); } else {
                    arguments.Append('"');
                    arguments.Append(escapeQuote.Replace(args[carg], m =>
                    m.Groups[1].Value + m.Groups[1].Value +
                    (m.Groups[2].Value == "\"" ? "\\\"" : "")
                    ));
                    arguments.Append('"');
                }
                if (carg + 1 < args.Length)
                    arguments.Append(' ');
            }
            return arguments.ToString();
        }

        private void RunCompr() {
            ProcessStartInfo info = new ProcessStartInfo(fileCompr.Content);
            info.RedirectStandardError = true;
            info.RedirectStandardOutput = true;
            info.UseShellExecute = false;
            info.Arguments = EscapeArguments(fileIn.Content, fileParts.Content);
            info.CreateNoWindow = true;
            Console.WriteLine(info.Arguments);
            txtOut.Text = "";
            SetEnabled(false);
            new AsyncJob(() => {
                Debugger.LogFun("Running external packing tool");
                Process p = Process.Start(info);

                Func<StreamReader, string> readLine = (StreamReader s) => {
                    List<byte> bytes = new List<byte>();
                    while (true) {
                        int b = s.Read();
                        if (b > 0 && b <= 0xff) {
                            if (b == '\r') {
                                continue;
                            } else if (b == '\n') {
                                break;
                            } else {
                                bytes.Add((byte)b);
                            }
                        } else {
                            if (bytes.Count > 0) {
                                break;
                            } else {
                                throw new Exception("end of stream");
                            }
                        }
                    }
                    return Encoding.UTF8.GetString(bytes.ToArray());
                };

                Thread readStdout = new Thread(new ThreadStart(() => {
                    while (true) {
                        try {
                            string line = readLine(p.StandardOutput);
                            AsyncManager.OnUIThread(() => {
                                txtOut.Text += line + "\n";
                                txtOut.ScrollToEnd();
                            }, ExecutionOption.Blocking);
                            continue;
                        } catch (Exception) {
                            break;
                        }
                    }
                }));
                
                Thread readStderr = new Thread(new ThreadStart(() => {
                    while (true) {
                        try {
                            string line = readLine(p.StandardError);
                            AsyncManager.OnUIThread(() => {
                                txtOut.Text += line + "\n";
                                txtOut.ScrollToEnd();
                            }, ExecutionOption.Blocking);
                            continue;
                        } catch (Exception) {
                            break;
                        }
                    }
                }));
                readStdout.Start();
                readStderr.Start();
                p.WaitForExit();
                return p.ExitCode == 0;
            }, (e) => {
                SetEnabled(true);
                if (e is true) {

                } else {
                    ErrorMessage.Show("Failed to pack data");
                }
            }).Run();
        }

        private void Run() {
            if(fileCompr.Content != "") {
                ShowList = false;
                RunCompr();
                return;
            }
            ShowList = true;
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
                    string fOutput = fileParts.Content + "/" + era + ".bin";
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
                    if (prompt != null) {
                        if (prompt.Trim().Length > 0) {
                            LoadConfiguartion(prompt.Trim());
                        }
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

        private bool comboConfigs_LoadedFirst = true;

        private void comboConfigs_Loaded(object sender, RoutedEventArgs e) {
            if (comboConfigs_LoadedFirst && comboConfigs.Items.Count > 2) {
                comboConfigs_LoadedFirst = false;
                comboConfigs.SelectedIndex = 0;
            }
        }

        private void btnPublish_Click(object sender, RoutedEventArgs e) {
            Publish();
        }
    }
}
