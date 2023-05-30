using System;

namespace GUILib.db {

    class Path : ObservableObject<Path> {

        public int ID { get => GET(INT); set => SET(value); }

        public String Purpose { get => GET(STRING); set => SET(value); }
        public String Value { get => GET(STRING); set => SET(value); }

        public Path(int ID, String path, String purpose) {
            this.ID = ID;
            this.Value = path;
            this.Purpose = purpose;
        }
    }
}
