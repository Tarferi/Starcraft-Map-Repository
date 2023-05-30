using RGiesecke.DllExport;
using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Reflection;

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
                wnd.Closed += (sender, e) => {
                    wnd = null;
                };
            }
            wnd.Visibility = System.Windows.Visibility.Visible;
        }
    }

    internal class UIBridge {

        private static List<Interface> ifcs = new List<Interface>();

        private static Dictionary<String, Assembly> loadedAssemblies = new Dictionary<string, Assembly>();

        [DllExport("UIAction", CallingConvention = CallingConvention.StdCall)]
        public static UInt32 UIAction(UInt32 action, UInt32 source, UInt32 code, UInt32 param, UInt32 param2) {
            if(action == 0) {
                if (System.Windows.Application.Current == null) {
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
