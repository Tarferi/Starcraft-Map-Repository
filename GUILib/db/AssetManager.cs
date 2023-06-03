using System;

namespace GUILib.db {

    class AssetManager : ObservableObject<AssetManager> {

        public int ID { get => GET(INT); set => SET(value); }

        public String Inputs { get => GET(STRING); set => SET(value); }
        public String Output { get => GET(STRING); set => SET(value); }

        public AssetManager(int ID, String inputs, String output) {
            this.ID = ID;
            this.Inputs = inputs;
            this.Output = output;
        }
    }
}
