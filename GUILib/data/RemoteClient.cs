using GUILib.db;
using GUILib.libs.json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

namespace GUILib.data {

    class WebClientEx : WebClient {
        public WebClientEx(CookieContainer container) {
            this.container = container;
        }

        public CookieContainer CookieContainer {
            get { return container; }
            set { container = value; }
        }

        private CookieContainer container = new CookieContainer();

        protected override WebRequest GetWebRequest(Uri address) {
            WebRequest r = base.GetWebRequest(address);
            var request = r as HttpWebRequest;
            if (request != null) {
                request.CookieContainer = container;
            }
            return r;
        }

        protected override WebResponse GetWebResponse(WebRequest request, IAsyncResult result) {
            WebResponse response = base.GetWebResponse(request, result);
            ReadCookies(response);
            return response;
        }

        protected override WebResponse GetWebResponse(WebRequest request) {
            WebResponse response = base.GetWebResponse(request);
            ReadCookies(response);
            return response;
        }

        private void ReadCookies(WebResponse r) {
            var response = r as HttpWebResponse;
            if (response != null) {
                CookieCollection cookies = response.Cookies;
                container.Add(cookies);
            }
        }
    }

    class HTTPClient {

        private CookieContainer container = new CookieContainer();

        private HTTPClient() {

#if DEBUG
            ServicePointManager.ServerCertificateValidationCallback = ((sender, certificate, chain, sslPolicyErrors) => true);
#endif
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;

        }

        private static Dictionary<String, HTTPClient> instances = new Dictionary<string, HTTPClient>();

        public static HTTPClient GetInstance(String name) {
            HTTPClient hc = null;
            if (!instances.TryGetValue(name, out hc)) {
                hc = new HTTPClient();
                instances[name] = hc;
            }
            return hc;
        }

        private void SetupHeaders(WebHeaderCollection headers, string ContentType=null) {
            headers.Clear();
            if (ContentType != null) {
                headers.Add("Content-Type", ContentType);
            }
        }

        private CookieContainer GetCookieContainer(String endpoint, String token = null, String username = null) {
            container = new CookieContainer();
            if (token != null) {
                Uri ep = new Uri(endpoint);
                container.Add(new Cookie("token", token) { Domain = ep.Host });
                container.Add(new Cookie("username", username) { Domain = ep.Host });
            }
            return container;
        }

        public Stream GetStream(string endpoint) {
            Debugger.LogRequest(endpoint);
            try {
                WebRequest request = WebRequest.Create(endpoint);
                WebResponse response = request.GetResponse();
                return response.GetResponseStream();
            } catch(Exception e) {
                Debugger.Log(e);
            }
            return null;
        }

        public byte[] Get(String endpoint, String token = null, String username = null) {
            Debugger.LogRequest(endpoint);
            using (WebClient client = new WebClientEx(GetCookieContainer(endpoint, token, username))) {
                SetupHeaders(client.Headers);
                try { 
                    return client.DownloadData(endpoint);
                } catch (Exception e) {
                    Debugger.Log(e);
                    return null;
                }
            }
        }

        public byte[] Post(String endpoint, byte[] data, String token = null,  String username = null, string ContentType=null) {
            Debugger.LogRequest(endpoint);
            using (WebClient client = new WebClientEx(GetCookieContainer(endpoint, token, username))) {
                SetupHeaders(client.Headers, ContentType);
                try {
                    return client.UploadData(endpoint, "Post", data);
                } catch(Exception e) {
                    Debugger.Log(e);
                    return null;
                }
            }
        }

        public byte[] Post(String endpoint, String data, String token = null, String username = null, string ContentType = null) {
            return Post(endpoint, Encoding.UTF8.GetBytes(data), token, username, ContentType);
        }

        public String FindCookie(Uri uri, String name) {
            CookieCollection c = container.GetCookies(uri);
            for(int i = 0; i < c.Count; i++) {
                Cookie cookie = c[i];
                if (cookie.Name == name) {
                    return cookie.Value;
                }
            }
            return null;
        }

    }

    class RemoteSearchedMap {

        public String ID { get; }
        public String Name { get; set; }
        public String RawName { get; }
        public long ModifiedTime { get; }
        public long UploadedTime { get; }

        public RemoteSearchedMap(String id, String rawName, long mt, long ut) {
            this.ID = id;
            this.RawName = rawName;

            StringBuilder sb = new StringBuilder();
            foreach (char c in rawName) {
                int cp = (int)c;
                if (cp >= ' ') {
                    sb.Append(c);
                }
            }
            this.Name = sb.ToString();
            this.ModifiedTime = mt;
            this.UploadedTime = ut;
        }
    }

    class RemoteParse {

        public static String GetUsername(String raw) {
            String k1 = "<a class=\"navbar-element\" href=\"/user/";
            int idx1 = raw.IndexOf(k1);
            if (idx1 >= 0) {
                idx1 += k1.Length;
                idx1 = raw.IndexOf(">", idx1);
                if (idx1 >= 0) {
                    idx1 += 1;
                    int nameBegin = idx1;
                    idx1 = raw.IndexOf("<", idx1);
                    if (idx1 >= 0) {
                        int nameEnd = idx1;
                        String username = raw.Substring(nameBegin, nameEnd - nameBegin);
                        return username;

                    }
                }
            }
            return null;
        }

        public static List<RemoteSearchedMap> GetRemoteSearchMaps(String raw) {
            List<RemoteSearchedMap> res = new List<RemoteSearchedMap>();
            String p1 = "window.search_results =";
            int idx = raw.IndexOf(p1);
            if (idx >= 0) {
                idx += p1.Length;
                int beginPos = idx;
                String p2 = "}];";

                idx = raw.IndexOf(p2, idx);
                if (idx >= 0) {
                    idx += p2.Length;
                    int endPos = idx - 1;
                    String tmp = raw.Substring(beginPos, endPos - beginPos);

                    JsonValue obj = JsonValue.Parse(tmp);
                    if (obj != null) {
                        if (obj.IsArray()) {
                            foreach (JsonValue item in obj.AsArray().Values) {
                                if (!item.IsObject()) {
                                    return null;
                                }
                                string id = item.AsObject().GetRawString("id");
                                string scenario_name = item.AsObject().GetRawString("scenario_name");
                                int last_modified = item.AsObject().GetRawInt("last_modified").Value;
                                int uploaded_time = item.AsObject().GetRawInt("uploaded_time").Value;
                                RemoteSearchedMap m = new RemoteSearchedMap(id, scenario_name, last_modified, uploaded_time);
                                res.Add(m);
                            }
                            return res;
                        }
                    }
                }
            }
            return null;
        }

        public static void CheckLoggedIn(String raw, String username) {
            String iu = GetUsername(raw);
            if (iu != username) {
                throw new NoLoggedInException();
            }
        }

        private static String UnescapeHTML(String str) {
            return WebUtility.HtmlDecode(str);
        }

        public static String GetRemoteMapMain(String raw) {
            JsonObject obj = new JsonObject();
            String p1= "<h2><bn-lobbytext text=\"";
            int idx = raw.IndexOf(p1);
            if (idx >= 0) {
                idx += p1.Length;
                int titleBegin = idx;
                idx = raw.IndexOf("\">", idx);
                if (idx >= 0) {
                    int titleEnd = idx;
                    String title = UnescapeHTML(raw.Substring(titleBegin, titleEnd - titleBegin));

                    obj.Put("Title", title);

                    int idxDetailsBegin = raw.IndexOf("<table class=\"table-details\">");
                    if (idxDetailsBegin >= 0) {
                        int idxDetailsEnd = raw.IndexOf("</tbody>", idxDetailsBegin);
                        if (idxDetailsEnd >= 0) {
                            idx = idxDetailsBegin;
                            while (true) {
                                p1 = "<td class=\"table-key\">";
                                String p2 = "<td class=\"table-value\">";

                                idx = raw.IndexOf(p1, idx);

                                if (idx >= 0 && idx < idxDetailsEnd) {
                                    idx += p1.Length;
                                    int keyBegin = idx;
                                    idx = raw.IndexOf("</td>", idx);
                                    if (idx >= 0) {
                                        int keyEnd = idx;
                                        String key = raw.Substring(keyBegin, keyEnd - keyBegin);
                                        key = UnescapeHTML(key.Replace(" ", "_"));
                                        idx = raw.IndexOf(p2, idx);
                                        if (idx >= 0) {
                                            idx += p2.Length;
                                            int valueBegin = idx;
                                            idx = raw.IndexOf("</td>", idx);
                                            if (idx >= 0) {
                                                int valueEnd = idx;
                                                String value = UnescapeHTML(raw.Substring(valueBegin, valueEnd - valueBegin));
                                                obj.Put(key, value);
                                                continue;
                                            }
                                        }
                                    }
                                } else {
                                    break;
                                }
                                return null;
                            }

                            return obj.ToJson();
                        }
                    }
                }
            }
            return null;
        }
    }

    class RemoteClient {

        HTTPClient hc;
        static readonly String API = "https://scmscx.com";
        static readonly String API_RION = "https://rion.cz/scmdb/query.php";
        static readonly String API_RION_FILES = "https://rion.cz/scmdb/";

        public RemoteClient() {
            hc = HTTPClient.GetInstance("RemoteClient");
        }

        public bool TokenValid(String token, String iusername, out String username) {
            username = null;
            byte[] data = hc.Get(API, token: token, username: iusername);
            String str = Encoding.UTF8.GetString(data);
            username = RemoteParse.GetUsername(str);
            return username != null;
        }

        public String GetToken(String username, String password) {
            //String data = JsonConvert.SerializeObject(new { username = username, password = password });
            JsonObject obj = new JsonObject();
            obj.Put("username", username);
            obj.Put("password", password);
            String data = obj.ToJson();
            hc.Post(API + "/api/login", data, ContentType: "application/json");
            String token = hc.FindCookie(new Uri(API), "token");
            return token;
        }

        public List<RemoteSearchedMap> SearchMaps(String token, String username, string v) {
            byte[] data = hc.Get(API + "/search/" + v, token: token, username: username);
            String str = Encoding.UTF8.GetString(data);
            RemoteParse.CheckLoggedIn(str, username);
            return RemoteParse.GetRemoteSearchMaps(str);
        }
        
        public String GetMapThumbnail(String token, String username, string v) {
            byte[] data = hc.Get(API + "/api/search_result_popup/" + v, token: token, username: username);
            String str = Encoding.UTF8.GetString(data);
            JsonValue val = JsonValue.Parse(str);
            if (val != null) {
                if (val.IsObject()) {
                    return val.AsObject().GetRawString("minimap");
                }
            }
            return null;
        }
        
        public String GetMapMainData(String token, String username, string remoteID) {
            byte[] data = hc.Get(API + "/map/" + remoteID, token: token, username: username);
            String str = Encoding.UTF8.GetString(data);
            RemoteParse.CheckLoggedIn(str, username);
            return RemoteParse.GetRemoteMapMain(str);
        }
        
        public byte[] GetMapMainCHK(String token, String username, string chkHash) {
            byte[] data = hc.Get(API + "/api/chk/" + chkHash, token: token, username: username);
            return data;
        }

        public List<RemoteAsset> GetRemoteAssets() {
            List<RemoteAsset> res = new List<RemoteAsset>();
            byte[] data = hc.Get(API_RION + "?version=1");
            String str = Encoding.UTF8.GetString(data);
            JsonValue val = JsonValue.Parse(str);
            if (val != null) {
                if (val.IsArray()) {
                    foreach(JsonValue v in val.AsArray().Values) {
                        if (v.IsObject()) {
                            JsonObject obj = v.AsObject();
                            string name = obj.GetRawString("name");
                            string file = obj.GetRawString("file");
                            int? size = obj.GetRawInt("size");
                            int? type = obj.GetRawInt("type");
                            if (name != null && file != null && size.HasValue && type.HasValue) {
                                RemoteAsset ra = new RemoteAsset(name, file, size.Value, type.Value);
                                if (ra.IsValid()) {
                                    res.Add(ra);
                                }
                            }
                        }
                    }
                }
            }
            return res;
        }

        public Stream GetRemoteAsset(RemoteAsset ra) {
            return hc.GetStream(API_RION_FILES + ra.Path);
        }
    }
}
