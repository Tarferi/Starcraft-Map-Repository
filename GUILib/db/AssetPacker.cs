using GUILib.libs.json;
using System;

namespace GUILib.db {

    class AssetPacker : ObservableObject<AssetPacker> {

        public int ID { get => GET(INT); set => SET(value); }

        public String Name { get => GET(STRING); set => SET(value); }
        public String Inputs { get => GET(STRING); set => SET(value); }
        public String OutputParts { get => GET(STRING); set => SET(value); }
        public String OutputFinal { get => GET(STRING); set => SET(value); }
        public String Compressor { get => GET(STRING); set => SET(value); }

        private JsonObject otherData = new JsonObject();

        public string OtherData {
            get => ToJsonString(otherData); set {
                JsonObject tmp = FromJsonString(value);
                if (tmp == null) {
                    throw new Exception("Invalid JSON");
                }
                otherData = tmp;
                NotifyAlways();
            }
        }

        public String Title { get => GET(STRING, otherData); set => SET(value, otherData); }

        public AssetPacker(int ID, String name, String inputs, String outputParts, String outputFinal, String compressor, String otherData) {
            this.ID = ID;
            this.Name = name;
            this.Inputs = inputs;
            this.OutputParts = outputParts;
            this.OutputFinal = outputFinal;
            this.Compressor = compressor;
            this.OtherData = otherData;
        }
    }
}
