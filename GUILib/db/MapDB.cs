using GUILib.ui.utils;
using System;
using System.Collections.Generic;
using System.Data.SQLite;

namespace GUILib.db {

    class Row {

        private Dictionary<String, object> values;

        public Row(Dictionary<String, object> values) {
            this.values = values;
        }
       
        public String GetString(String name) {
            return values[name] as String;
        }

        public int GetInt(String name) {
            long val = (long)values[name];
            return (int)val;
        }

    }

    class SQLite {

        private SQLiteConnection conn;
        public SQLite(String fileName) {
            conn = new SQLiteConnection("Data Source=Map Repository/" + fileName + ";Version = 3;New = True;Compress = True;");
            try {
                conn.Open();
            } catch (Exception) {
                conn = null;
            }
        }

        private SQLiteCommand Prepare(String sql, params object[] replacements) {
            SQLiteCommand cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            if (replacements.Length > 0) {
                String[] sqlParts = sql.Split('?');
                String p = "";
                for(int i = 0; i < sqlParts.Length; i++) {
                    p += sqlParts[i];
                    if (i > 0 && i + 1 < sqlParts.Length) {
                        p += "@SqlParam_" + (i - 1);
                    }
                }

                SQLiteParameter[] pars = new SQLiteParameter[replacements.Length];
                for (int i = 0; i < replacements.Length; i++) {
                    pars[i] = new SQLiteParameter("@SqlParam_" + i, replacements[i]);
                }
                cmd.Parameters.AddRange(pars);

                sql = p;
            }
            return cmd;
        }

        protected bool Execute(String sql, params object[] replacements) {
            using (SQLiteCommand cmd = Prepare(sql, replacements)) {
                try {
                    cmd.ExecuteNonQuery();
                } catch (Exception) {
                    return false;
                }
                return true;
            }
        }
        protected List<Row> Select(String sql, params object[] replacements) {
            using (SQLiteCommand cmd = Prepare(sql, replacements)) {
                List<Row> res = new List<Row>();
                try {
                    using (SQLiteDataReader reader = cmd.ExecuteReader()) {
                        while (reader.Read()) {
                            Dictionary<string, object> values = new Dictionary<string, object>();
                            for (int i = 0; i < reader.FieldCount; i++) {
                                values[reader.GetName(i)] = reader.GetValue(i);
                            }
                            Row row = new Row(values);
                            res.Add(row);
                        }
                    }
                } catch (Exception) {
                    return null;
                }
                return res;
            }
        }
        public virtual bool IsValid() {
            return conn != null;
        }
    }

    class Path {

        private int id;
        private String path;
        private String purpose;

        private Action<Path> saveFun;

        public Path(int ID, String path, String purpose, Action<Path> saveFun) {
            this.id = ID;
            this.path = path;
            this.purpose = purpose;
            this.saveFun = saveFun;
        }
        
        public int ID { get => id; }

        public string Purpose { get => purpose ; set { purpose = value; update(); } }
        public string Value { get => path; set { path = value; update(); } }

        private void update() {
            saveFun(this);
        }
    }

    class Config {

        private int id;
        private String username;
        private String password;
        private String API0;

        private Action<Config> saveFun;

        public Config(int ID, String username, String password, String API, Action<Config> saveFun) {
            this.id = ID;
            this.username = username;
            this.password = password;
            this.API0 = API;
            this.saveFun = saveFun;
        }

        public int ID { get => id; }
        public string Username { get => username; set { username = value; update(); } }
        public string Password { get => password; set { password = value; update(); } }
        public string API { get => API0; set { API0 = value; update(); } }

        private void update() {
            saveFun(this);
        }
    }

    class MapDB : SQLite {

        bool valid = false;

        private Config cfg= null;
        private Dictionary<String, Path> paths = new Dictionary<string, Path>();

        private bool CreateTables() {
            bool bRet = true;

            bRet &= Execute("CREATE TABLE IF NOT EXISTS config (" +
                                "ID INTEGER PRIMARY KEY AUTOINCREMENT," +
                                "username TEXT," +
                                "password TEXT," +
                                "API TEXT" +
                                ")");
            
            bRet &= Execute("CREATE TABLE IF NOT EXISTS path (" +
                                "ID INTEGER PRIMARY KEY AUTOINCREMENT," +
                                "path TEXT," +
                                "purpose TEXT" +
                                ")");

            return bRet;
        }

        private MapDB() : base("maps.db") {
            if (!base.IsValid()) {
                return;
            }
            if (!CreateTables()) {
                return;
            }
            valid = true;
        }

        public static MapDB Create() {
            MapDB db = new MapDB();
            if(db.IsValid()) {
                return db;
            }
            return null;
        }

        public override bool IsValid() {
            return base.IsValid() && valid;
        }

        public Path GetPath(String purpose) {
            if(!paths.ContainsKey(purpose)) {
                Action<Path> saver = (Path path) => {
                    Execute("UPDATE Path SET path=?, purpose=? WHERE ID = ?", path.Value, path.Purpose, path.ID);
                };

                List<Row> rows = Select("SELECT * FROM Path WHERE purpose = ?", purpose);
                if (rows.Count == 0) {
                    Execute("INSERT INTO Path (path,purpose) VALUES (?,?)", "", purpose);
                    rows = Select("SELECT * FROM Path WHERE purpose = ?", purpose);
                }

                if (rows.Count == 0) {
                    ErrorMessage.Show("Failed to read path from database");
                    return null;
                } else {
                    int ID = rows[0].GetInt("ID");
                    String path = rows[0].GetString("path");
                    Path p = new Path(ID, path, purpose, saver);
                    paths[purpose] = p;
                }
            }
            return paths[purpose];
        }

        public Config GetConfig() {
            if (cfg == null) {
                Action<Config> saver = (Config cfg) => {
                    Execute("UPDATE Config SET username=?, password=?, API=? WHERE ID = ?", cfg.Username, cfg.Password, cfg.API, cfg.ID);
                };

                List<Row> rows = Select("SELECT * FROM Config");
                if (rows.Count == 0) {
                    Execute("INSERT INTO Config (username,password,API) VALUES (?,?,?)", "", "", "");
                    rows = Select("SELECT * FROM Config");
                }

                if(rows.Count == 0) {
                    ErrorMessage.Show("Failed to read config from database");
                    return null;
                } else {
                    int ID = rows[0].GetInt("ID");
                    String username = rows[0].GetString("username");
                    String password = rows[0].GetString("password");
                    String api = rows[0].GetString("API");
                    cfg = new Config(ID, username, password, api, saver);
                }
            }
            return cfg;
        }

    }
}
