using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media.Imaging;

namespace GUILib.db {

    class RemoteMap : ObservableObject<RemoteMap> {

        public int ID { get => GET(INT); set => SET(value); }

        public String RemoteID { get => GET(STRING); set => SET(value); }
        public String Name { get => GET(STRING); set => SET(value); }
        public String Thumbnail { get => GET(STRING); set => SET(value); }

   
        public string OtherData { get=>ToJsonString(otherData); set { otherData = FromJsonString(value); NotifyAlways(); } }
        private IDictionary<string, object> otherData = new Dictionary<string, object>();
        
        public String Title { get => GET(STRING, otherData); }
        public String Version { get => GET(STRING, otherData); }
        public String Map_Tileset { get => GET(STRING, otherData); }
        public String Map_Dimensions { get => GET(STRING, otherData); }
        public String Views { get => GET(STRING, otherData); }
        public String Downloads { get => GET(STRING, otherData); }
        public String MPQ_Size { get => FormatFileSize(GET(STRING, otherData)); }
        public String MPQ_Hash { get => GET(STRING, otherData); }

        public RemoteMap(int ID, String remoteID, String name, String thumbnail, String otherData) {
            this.ID = ID;
            this.RemoteID = remoteID;
            this.Name = name;
            this.Thumbnail = thumbnail;
            this.OtherData = otherData;
        }

        private BitmapImage cache = null;

        public BitmapImage ThumbnailImageSource {
            get {
                if (cache == null) {
                    byte[] binaryData = Convert.FromBase64String(Thumbnail);
                    BitmapImage bi = new BitmapImage();
                    bi.BeginInit();
                    bi.StreamSource = new MemoryStream(binaryData);
                    bi.EndInit();
                    cache = bi;
                }
                return cache;
            }
        }
    }
}
