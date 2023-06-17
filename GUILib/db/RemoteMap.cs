using GUILib.libs.json;
using System;
using System.IO;
using System.Windows.Media.Imaging;

namespace GUILib.db {

    class RemoteMap : ObservableObject<RemoteMap> {

        public int ID { get => GET(INT); set => SET(value); }

        public String RemoteID { get => GET(STRING); set => SET(value); }
        public String Name { get => GET(STRING); set => SET(value); }
        public String Thumbnail { get => GET(STRING); set => SET(value); }

        
        private JsonObject otherData = new JsonObject();
        
        public string OtherData { get=>ToJsonString(otherData); set {
                JsonObject tmp = FromJsonString(value);
                if (tmp == null) {
                    throw new Exception("Invalid JSON");
                }
                otherData = tmp;
                NotifyAlways(); 
            } }

        public String Title { get => GET(STRING, otherData); }
        public String Version { get => GET(STRING, otherData); }
        public String Map_Tileset { get => GET(STRING, otherData); }
        public String Map_Dimensions { get => GET(STRING, otherData); }
        public String Views { get => GET(STRING, otherData); }
        public String Downloads { get => GET(STRING, otherData); }
        public String MPQ_Size { get => FormatFileSize(GET(STRING, otherData)); }
        public String MPQ_Hash { get => GET(STRING, otherData); }
        public String CHK_Hash { get => GET(STRING, otherData); }
        public String FirstKnownFileName { get => GET(STRING, otherData); }

        public String MapPreviewData { get => GET(STRING, otherData); set => SET(value, otherData); }

        public RemoteMap(int ID, String remoteID, String name, String thumbnail, String otherData) {
            this.ID = ID;
            this.RemoteID = remoteID;
            this.Name = name;
            this.Thumbnail = thumbnail;
            this.OtherData = otherData;
        }

        private BitmapImage thumbnailCache = null;

        public BitmapImage ThumbnailImageSource {
            get {
                if (thumbnailCache == null) {
                    byte[] binaryData = Convert.FromBase64String(Thumbnail);
                    thumbnailCache = new BitmapImage();
                    thumbnailCache.BeginInit();
                    thumbnailCache.StreamSource = new MemoryStream(binaryData);
                    thumbnailCache.EndInit();
                }
                return thumbnailCache;
            }
        }

        private BitmapImage previewCache = null;

        public BitmapImage PreviewImageSource {
            get {
                if (previewCache == null) {
                    if (MapPreviewData != null) {
                        byte[] binaryData = Convert.FromBase64String(MapPreviewData);
                        previewCache = new BitmapImage();
                        previewCache.BeginInit();
                        previewCache.StreamSource = new MemoryStream(binaryData);
                        previewCache.EndInit();
                    }
                }
                return previewCache;
            }
        }
    }
}
