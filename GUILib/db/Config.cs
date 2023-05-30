using System;

namespace GUILib.db {

    public class Config : ObservableObject<Config> {

        public int ID { get => GET(INT); set => SET(value); }
        public string Username { get => GET(STRING); set => SET(value); }
        public string Password { get => GET(STRING); set => SET(value); }
        public string API { get => GET(STRING); set => SET(value); }

        public Config(int ID, String username, String password, String API) {
            this.ID = ID;
            this.Username = username;
            this.Password = password;
            this.API = API;
        }
    }
}
