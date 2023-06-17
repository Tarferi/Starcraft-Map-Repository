using GUILib.db;
using GUILib.ui.LoginWnd;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace GUILib.data {

    public class ResourceTypes {

        public const byte TILESET = 13;

        public const byte SPRITES = 17;

        public const string MAGIC = "{b64a86c5863a4fd89e71c561ab2df4b1}";

        public static string TypeToName(byte type) {
            switch (type) {
                case TILESET:
                    return "Tileset";

                case SPRITES:
                    return "Sprites";

                default:
                    return null;
            }
        }
    }

    public class ModelInitData {
   
        public static Func<string> RootDirGetter = null;

    }

    class Model {

        public static Brush ColorDefault = null;
        public static Brush ColorError = new SolidColorBrush(Colors.Red);
        public static Brush ColorSuccess = new SolidColorBrush(Colors.LimeGreen);

        private bool valid = false;
        private static Model instance = null;

        private MapDB db;
        private RemoteClient client;

        private Config cfg = null;
        private Dictionary<string, AssetPacker> assetPackers = new Dictionary<string, AssetPacker>();
        private Dictionary<String, db.Path> paths = new Dictionary<string, db.Path>();
        private Dictionary<String, RemoteMap> maps = new Dictionary<String, RemoteMap>();

      
        public readonly string WorkingDir;

        private Model() {
            WorkingDir = ModelInitData.RootDirGetter();

            db = MapDB.Create();
            if (db == null) {
                return;
            }
            client = new RemoteClient();
            valid = true;
        }

        private String Username { get { return GetConfig().Username; } }
        private String Token { get { return GetConfig().API; } }
        private String Password { get { return GetConfig().Password; } }

        public static Model Create() {
            if (instance == null) {
                Model m = new Model();
                if (m.valid) {
                    instance = m;
                    return instance;
                }
                return null;
            }
            return instance;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public Config GetConfig() {
            if(cfg == null) {
                cfg = db.GetConfig();
            }
            cfg.IncRef();
            return cfg;
        }

        public List<string> GetAllAssetPackerNames() {
            return db.GetAllAssetPackerNames();
        }
        
        [MethodImpl(MethodImplOptions.Synchronized)]
        public AssetPacker GetAssetPacker(string name) {
            AssetPacker ap = null;
            if (!assetPackers.TryGetValue(name, out ap)) {
                ap = db.GetAssetPacker(name);
                assetPackers[name] = ap;
            }
            ap.IncRef();
            return ap;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public db.Path GetPath(String purpose) {
            if (!paths.ContainsKey(purpose)) {
                db.Path p = db.GetPath(purpose);
                p.IncRef();
                paths[purpose] = p;
            }
            paths[purpose].IncRef();
            paths[purpose].AddDisposeListener((p) => {
                paths.Remove(p.Purpose);
            });
            return paths[purpose];
        }

        public bool TryLogin(String username, String password) {
            String token = client.GetToken(username, password);
            if (token != null) {
                GetConfig().Username = username;
                GetConfig().API = token;
                return true;
            }
            return false;
        }

        public void ResetConfig() {
            Config cfg = GetConfig();
            cfg.Username = "";
            cfg.Password = "";
            cfg.API = "";
        }

        private bool HandleNotLoggedIn() {
            bool ok = false;

            if (Username != null && Username != "" && Password != null && Password != "") {
                if(!TryLogin(Username, Password)) {
                    GetConfig().Username = "";
                    GetConfig().Password = "";
                    GetConfig().API = "";
                } else {
                    return true;
                }
            }

            AsyncManager.OnUIThread(() => {
                LoginWnd wnd = new LoginWnd(TryLogin);
                wnd.ShowDialog();
                ok = !wnd.Cancelled;
            }, ExecutionOption.DoOtherJobsWhileBlocking);
            return ok;
        }

        private bool EnsureLoggedIn() {
            Config cfg = GetConfig();
            if(Username == null || Username == "" || Token==null || Token=="") {
                if (!HandleNotLoggedIn()) {
                    return false;
                }
            }
            return true;
        }

        private T DoLoggedIn<T>(Func<T> fun, T defaultValue) {
            if (EnsureLoggedIn()) {
                while (true) {
                    try {
                        return fun();
                    } catch (NoLoggedInException e) {
                        Debugger.Log(e);
                        if (!HandleNotLoggedIn()) {
                            return defaultValue;
                        }
                    }
                }
            } else {
                return defaultValue;
            }
        }

        public List<RemoteSearchedMap> SearchMaps(String filter) {
            return DoLoggedIn<List<RemoteSearchedMap>>(() => client.SearchMaps(Token, Username, filter), null);
        }

        public String GetMapThumbnail(String remoteID) {
            return DoLoggedIn<String>(() => client.GetMapThumbnail(Token, Username, remoteID), null);
        }

        public String GetMapMainData(string remoteID) {
            return DoLoggedIn<String>(() => client.GetMapMainData(Token, Username, remoteID), null);
        }

        public byte[] GetMapMainCHK(string chkHash) {
            return DoLoggedIn<byte[]>(() => client.GetMapMainCHK(Token, Username, chkHash), null);
        }

        public RemoteMap GetMap(String remoteID) {
            RemoteMap map;
            if (maps.TryGetValue(remoteID, out map)) {
                map.IncRef();
                return map;
            }
            map = db.GetMap(remoteID);
            maps[remoteID] = map;
            map.IncRef();
            map.AddDisposeListener((m) => {
                maps.Remove(m.RemoteID);
            });
            return map;
        }

        public List<RemoteAsset> GetRemoteAssets() {
            return client.GetRemoteAssets();
        }

        public Stream GetRemoteAsset(RemoteAsset ra, int part) {
            return client.GetRemoteAsset(ra, part);
        }

        public Stream DownloadMap(RemoteMap map) {
            return DoLoggedIn<Stream>(() => client.DownloadMap(Token, Username, map), null);
        }

        public bool Publish(AssetPacker assetPacker, Stream contents, string publishingKey) {
            return client.Publish(assetPacker, contents, publishingKey);
        }
    }
}
