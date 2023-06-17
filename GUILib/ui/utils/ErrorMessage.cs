using System.Windows;

namespace GUILib.ui.utils {

    class ErrorMessage {

        public static void Show(string contents) {
            MessageBox.Show(contents, "Starcraft Map Repository", MessageBoxButton.OK, MessageBoxImage.Error);
        }

    }

    class WarningMessage {

        public static void Show(string contents) {
            MessageBox.Show(contents, "Starcraft Map Repository", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

    }
}
