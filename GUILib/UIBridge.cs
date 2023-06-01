using RGiesecke.DllExport;
using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using System.Linq;
using GUILib.ui.utils;

namespace GUILib {
    enum InterfaceEvents {
        ButtonClick = 0
    };

    enum InterfaceObjects {
        RepositoryButtons = 0
    };

    class Interface {

        private static int IDX = 0;

        public readonly int ID;

        private MainWindow wnd = null;

        public Interface() {
            ID = IDX++;
        }

        public void Destroy() {
            if (this.wnd != null) {
                this.wnd.Close();
                this.wnd = null;
            }
        }

        public void OnEvent(int code, int param, int param2) {
            InterfaceEvents evt = (InterfaceEvents)param;
            switch (evt) {
                case InterfaceEvents.ButtonClick:
                    InterfaceObjects target = (InterfaceObjects)param2;
                    if(target == InterfaceObjects.RepositoryButtons) {
                        OpenWindow();
                    }
                    break;
            }
        }

        private void OpenWindow() {
            if (wnd == null) {
                this.wnd = new MainWindow() { Visibility = System.Windows.Visibility.Hidden };
                wnd.InfoPanelVisible = true;
                wnd.Closing += (sender, e) => {
                    e.Cancel = true;
                    wnd.Visibility = System.Windows.Visibility.Hidden;
                };
                wnd.Visibility = System.Windows.Visibility.Visible;
            } else if(wnd.Visibility == System.Windows.Visibility.Hidden) {
                wnd.Visibility = System.Windows.Visibility.Visible;
            } else {
                ErrorMessage.Show("Invalid main window state");
            }
        }
    }

    internal class UIBridge {

        private static Assembly OnResolveAssembly(object sender, ResolveEventArgs e) {
            var thisAssembly = Assembly.GetExecutingAssembly();
            var assemblyName = new AssemblyName(e.Name);
            var dllName = assemblyName.Name + ".dll";
            var resources = thisAssembly.GetManifestResourceNames().Where(s => s.EndsWith(dllName));
            if (resources.Any()) {
                var resourceName = resources.First();
                using (var stream = thisAssembly.GetManifestResourceStream(resourceName)) {
                    if (stream == null) return null;
                    var block = new byte[stream.Length];
                    try {
                        stream.Read(block, 0, block.Length);
                        return Assembly.Load(block);
                    } catch (IOException) {
                        return null;
                    } catch (BadImageFormatException) {
                        return null;
                    }
                }
            }
            return null;
        }

        private static List<Interface> ifcs = new List<Interface>();

        private static Dictionary<String, Assembly> loadedAssemblies = new Dictionary<string, Assembly>();

        [DllExport("UIAction", CallingConvention = CallingConvention.StdCall)]
        public static UInt32 UIAction(UInt32 action, UInt32 source, UInt32 code, UInt32 param, UInt32 param2) {
            if(action == 0) {
                if (System.Windows.Application.Current == null) {
                    AppDomain.CurrentDomain.AssemblyResolve += OnResolveAssembly;
                    new System.Windows.Application();
                }

                Interface ifc = new Interface();
                ifcs.Add(ifc);
                UInt32 res = (UInt32)ifc.ID;
                return res;
            } else if(action == 1) {
                int ifcID = (int)source;
                foreach (Interface ifc in ifcs) {
                    if (ifc.ID == ifcID) {
                        ifc.OnEvent((int)code, (int)param, (int)param2);
                    }
                }
            } else if(action==2) {
                Interface ifcDel = null;
                int ifcID = (int)source;
                foreach (Interface ifc in ifcs) {
                    if (ifc.ID == ifcID) {
                        ifcDel = ifc;
                    }
                }
                if (ifcDel != null) {
                    ifcs.Remove(ifcDel);
                    ifcDel.Destroy();
                }
            }
            return 0;
        }
    }
}
