using System;

namespace GUILib.db {

    class AssetPacker : ObservableObject<AssetPacker> {

        public int ID { get => GET(INT); set => SET(value); }

        public String Name { get => GET(STRING); set => SET(value); }
        public String Inputs { get => GET(STRING); set => SET(value); }
        public String OutputParts { get => GET(STRING); set => SET(value); }
        public String OutputFinal { get => GET(STRING); set => SET(value); }

        public AssetPacker(int ID, String name, String inputs, String outputParts, String outputFinal) {
            this.ID = ID;
            this.Name = name;
            this.Inputs = inputs;
            this.OutputParts = outputParts;
            this.OutputFinal = outputFinal;
        }
    }
}
