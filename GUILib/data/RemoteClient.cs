using System;
using System.Collections.Generic;
using System.Net;
using Newtonsoft.Json;
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

        public byte[] Get(String endpoint, String token = null, String username = null) {
            using (WebClient client = new WebClientEx(GetCookieContainer(endpoint, token, username))) {
                SetupHeaders(client.Headers);
                byte[] data = client.DownloadData(endpoint);
                return data;
            }
        }

        public byte[] Post(String endpoint, byte[] data, String token = null,  String username = null, string ContentType=null) {
            container = new CookieContainer();
            using (WebClient client = new WebClientEx(GetCookieContainer(endpoint, token, username))) {
                SetupHeaders(client.Headers, ContentType);
                byte[] cdata = client.UploadData(endpoint, "Post", data);
                return cdata;
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

    }

    class RemoteClient {

        HTTPClient hc;
        static readonly String API = "https://scmscx.com";

        public RemoteClient() {
            hc = HTTPClient.GetInstance("RemoteClient");
        }

        public bool TokenValid(String token,String iusername, out String username) {
            username = null;
            byte[] data = hc.Get(API, token: token, username: iusername);
            String str = Encoding.UTF8.GetString(data);
            username = RemoteParse.GetUsername(str);
            return username != null;
        }

        public String GetToken(String username, String password) {
            
            String data = JsonConvert.SerializeObject(new { username = username, password = password });
            hc.Post(API + "/api/login", data, ContentType: "application/json");
            String token = hc.FindCookie(new Uri(API), "token");
            return token;
        }

        

    }
}
