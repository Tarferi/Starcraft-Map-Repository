using System;

namespace GUILib.data {
    class Debugger {

        public static void LogRequest(String endpoint) {
            Console.WriteLine("Requesting " + endpoint);
        }

        public static void Log(Exception e) {
            Console.WriteLine(e.ToString());
            throw e;
        }

    }
}
