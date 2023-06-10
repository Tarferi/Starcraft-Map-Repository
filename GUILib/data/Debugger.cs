using System;

namespace GUILib.data {
    
    public class Debugger {

        public static bool ShowAssetsPacker{
            get {
                return IsDebuggingPack;
            }
        }

        public static bool IsDebuggingManager {
            get {
#if DEBUG
                return false;
#else
                return false;
#endif
            }
        }
        
        public static bool IsDebuggingPack {
            get {
#if DEBUG
                return false;
#else
                return false;
#endif
            }
        }

        public static bool IsDebuggingMapPreview {
            get {
#if DEBUG
                return false;
#else
                return false;
#endif
            }
        }

        public static Action<String> LogFun = (e) => { };
        
        public static Action<bool> WorkStatus = (e) => { };

        private static string lastStatus = "";
        public static string LastStatus { get; }

        private static void SetStatus(String s) {
            lastStatus = s;
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
