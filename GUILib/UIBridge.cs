using RGiesecke.DllExport;
using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using System.Linq;
using GUILib.ui.utils;
using System.Text;

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

    public class DLLResources {

        private DLLResources() {
            //StringBuilder sb = new StringBuilder();
            //foreach (String str in Assembly.GetAssembly(this.GetType()).GetManifestResourceNames()) {
            //    sb.Append("\n" + str);
            //}
            //ErrorMessage.Show("Have:" + sb.ToString());
        }

        private static DLLResources instance = new DLLResources();

        private static Assembly OnResolveAssembly(object sender, ResolveEventArgs e) {
            var thisAssembly = Assembly.GetAssembly(instance.GetType());
            var assemblyName = new AssemblyName(e.Name);
            var dllName = assemblyName.Name + ".dll";
            var resources = thisAssembly.GetManifestResourceNames().Where(s => s.EndsWith(dllName));
            //ErrorMessage.Show("Looking for " + dllName);
            if (resources.Any()) {
                //ErrorMessage.Show("Looking for " + dllName+": investigating");
                var resourceName = resources.First();
                using (var stream = thisAssembly.GetManifestResourceStream(resourceName)) {
                    if (stream == null) return null;
                    var block = new byte[stream.Length];
                    try {
                        stream.Read(block, 0, block.Length);
                        Assembly a = Assembly.Load(block);
                        if (a != null) {
                            //ErrorMessage.Show("Looking for " + dllName+": found");
                            return a;

                        }
                    } catch (IOException) {
                        //ErrorMessage.Show("Looking for " + dllName+": exception");
                        return null;
                    } catch (BadImageFormatException) {
                        //ErrorMessage.Show("Looking for " + dllName+": bad format exception");
                        return null;
                    }
                }
            }
            //ErrorMessage.Show("Looking for " + dllName+": not found");
            return null;
        }

        public static void Hook() {
            AppDomain.CurrentDomain.AssemblyResolve += OnResolveAssembly;
        }

    }

    internal class UIBridge {


        private static List<Interface> ifcs = new List<Interface>();

        private static Dictionary<String, Assembly> loadedAssemblies = new Dictionary<string, Assembly>();

        [DllExport("UIAction", CallingConvention = CallingConvention.StdCall)]
        public static UInt32 UIAction(UInt32 action, UInt32 source, UInt32 code, UInt32 param, UInt32 param2) {
            if(action == 0) {
                if (System.Windows.Application.Current == null) {
                    DLLResources.Hook();
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
