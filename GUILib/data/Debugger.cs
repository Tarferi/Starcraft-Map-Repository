using System;

namespace GUILib.data {
    class Debugger {

        public static bool ShowAssetsManager {
            get {
                return IsDebuggingPack;
            }
        }

        public static bool IsDebuggingPack {
            get {
#if DEBUG
                return true;
#else
                //return false;
                return true;
#endif
            }
        }

        public static bool IsDebuggingMapPreview {
            get {
#if DEBUG
                return false;
                //return true;
#else
                return false;
#endif
            }
        }

        public static Action<String> LogFun = (e) => { };
        
        public static Action<bool> WorkStatus = (e) => { };

        private static void SetStatus(String s) {
            Console.WriteLine(s);
            LogFun(s);
        }

        public static void LogRequest(String endpoint) {
            SetStatus("Requesting " + endpoint);
        }

        public static void Log(Exception e) {
            SetStatus("Error: " + e.ToString());
        }

        public static void WorkBegin() {
            WorkStatus(true);
        }

        public static void WorkEnd() {
            WorkStatus(false);
        }
    }
}
