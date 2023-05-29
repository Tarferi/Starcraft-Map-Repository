using GUILib.db;
using System;

namespace GUILib.data {
    class Model {

        private static Model instance = null;

        private MapDB db;

        private RemoteClient client;

        private bool valid = false;

        private Model() {
            db = MapDB.Create();
            if (db == null) {
                return;
            }
            client = new RemoteClient();
            valid = true;
        }

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

        public Config GetConfig() {
            return db.GetConfig();
        }

        public Path GetPath(String purpose) {
            return db.GetPath(purpose);
        }

        public RemoteClient GetRemoteClient() {
            return client;
        }
    }
}
