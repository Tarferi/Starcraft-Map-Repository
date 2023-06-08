using GUILib.data;

namespace GUILib.db {
    
    public class RemoteAsset {

        public string Name { get; set; }
        public string Path { get; set; }
        public string Category { get; set; }
        public string Size { get; set; }
        public bool ExistsLocally { get; set; }
        public bool DownloadAvailable { get => !ExistsLocally; }
        public int RawSize { get; }

        public RemoteAsset(string name, string file, int size, int type) {
            this.Name = name;
            this.Path = file;
            this.RawSize = size;
            this.Size = ObservableObject<RemoteAsset>.FormatFileSize(size);
            this.Category = ResourceTypes.TypeToName((byte)type);
            this.ExistsLocally = false;
        }

        public bool IsValid() {
            return Category != null;
        }
    }
}
