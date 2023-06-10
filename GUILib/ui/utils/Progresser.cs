using System;

namespace GUILib.ui.utils {

    public class Progresser {

        private long total = 0;

        private long lastCurrent = -1;
        private string current = "";

        private Action<string> updater = null;

        private Progresser(long total, Action<string> updater) {
            this.total = total;
            this.updater = updater;
        }

        public void Tick(long current) {
            long tmp = (100 * current) / total;

            if (tmp != lastCurrent) {
                lastCurrent = tmp;
                this.current = tmp + "%";
                updater(this.current);
            }
        }
        
        public static Progresser Percentage(long total, Action<string> updater) {
            return new Progresser(total, updater);
        }

    }
}
