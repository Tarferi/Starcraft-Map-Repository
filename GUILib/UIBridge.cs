using RGiesecke.DllExport;
using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using System.Linq;
using System.Collections.Concurrent;
using System.Text;
using GUILib.data;

namespace GUILib {

    enum InterfaceEvents {
        ButtonClick = 0
    };

    enum InterfaceObjects {
        RepositoryButtons = 0
    };

    public class CallEvent {
        public int eventID;
        public IntPtr param1;
        public IntPtr param2;
        public IntPtr param3;
        public IntPtr param4;

        private CallEvent(int eventID, IntPtr param1, IntPtr param2, IntPtr param3, IntPtr param4) {
            this.eventID = eventID;
            this.param1 = param1;
            this.param2 = param2;
            this.param3 = param3;
            this.param4 = param4;
        }

        public static CallEvent CallOpenMap(string name) {
            byte[] raw = Encoding.UTF8.GetBytes(name);
            unsafe {
                IntPtr p = Marshal.AllocHGlobal(raw.Length + 1);
                byte* b = (byte*)p;
                for (int i = 0; i < raw.Length; i++) {
                    b[i] = raw[i];
                }
                b[raw.Length] = 0;
                CallEvent evt = new CallEvent(0, p, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                return evt;
            }
        }

        private static void UnCallOpenMap(IntPtr param1, IntPtr param2, IntPtr param3, IntPtr param4) {
            unsafe {
                byte* b = (byte*)param1;
                IntPtr p = (IntPtr)b;
                Marshal.FreeHGlobal(p);
            }
        }

        public static void Dispose(int eventID, IntPtr param1, IntPtr param2, IntPtr param3, IntPtr param4) {
            switch (eventID) {
                case 0:
                    UnCallOpenMap(param1, param2, param3, param4);
                    break;
            }
        }
    }
    public class Interface {

        private BlockingCollection<CallEvent> events = new BlockingCollection<CallEvent>(new ConcurrentQueue<CallEvent>());

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
                Model m = Model.Create();
                m.IsPlugin = true;
                m.PluginInterface = this;
                wnd.InfoPanelVisible = true;
                wnd.Closing += (sender, e) => {
                    e.Cancel = true;
                    wnd.Visibility = System.Windows.Visibility.Hidden;
                };
                wnd.Visibility = System.Windows.Visibility.Visible;
            } else if(wnd.Visibility == System.Windows.Visibility.Hidden) {
                wnd.Visibility = System.Windows.Visibility.Visible;
            } else {
                wnd.Focus();
            }
        }
    
        public void CallOpenMap(string target) {
            events.Add(CallEvent.CallOpenMap(target));
        }
    
        public CallEvent PollEvent() {
            return events.Take();
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
            if (resources.Any()) {
                var resourceName = resources.First();
                using (var stream = thisAssembly.GetManifestResourceStream(resourceName)) {
                    if (stream == null) return null;
                    var block = new byte[stream.Length];
                    try {
                        stream.Read(block, 0, block.Length);
                        Assembly a = Assembly.Load(block);
                        if (a != null) {
                            return a;

                        }
                    } catch (IOException) {
                        return null;
                    } catch (BadImageFormatException) {
                        return null;
                    }
                }
            }
            return null;
        }

        public static void Hook() {
            AppDomain.CurrentDomain.AssemblyResolve += OnResolveAssembly;
        }

    }

    internal class UIBridge {


        private static List<Interface> ifcs = new List<Interface>();

        private static Dictionary<String, Assembly> loadedAssemblies = new Dictionary<string, Assembly>();

        [DllExport("PollEvent", CallingConvention = CallingConvention.StdCall)]
        public static void PollEvent(UInt32 type, IntPtr eventID, IntPtr param1, IntPtr param2, IntPtr param3, IntPtr param4) {
            Interface ifc = ifcs[0];
            if (type == 0) {
                CallEvent evt = ifc.PollEvent();
                unsafe {
                    *((int*)eventID) = evt.eventID;
                    *((int**)param1) = (int*)evt.param1;
                    *((int**)param2) = (int*)evt.param2;
                    *((int**)param3) = (int*)evt.param3;
                    *((int**)param4) = (int*)evt.param4;
                }
            } else if (type == 1) {
                int eventID_ = 0;
                IntPtr param1_ = IntPtr.Zero;
                IntPtr param2_ = IntPtr.Zero;
                IntPtr param3_ = IntPtr.Zero;
                IntPtr param4_ = IntPtr.Zero;
                
                unsafe {
                    eventID_ = *((int*)eventID);
                    param1_ = (IntPtr)(*((int**)param1));
                    param2_ = (IntPtr)(*((int**)param2));
                    param3_ = (IntPtr)(*((int**)param3));
                    param4_ = (IntPtr)(*((int**)param4));
                }

                CallEvent.Dispose(eventID_, param1_, param2_, param3_, param4_);
            }
        }

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
