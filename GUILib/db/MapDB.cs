using Community.CsharpSqlite.SQLiteClient;
using GUILib.data;
using GUILib.ui.utils;
using System;
using System.Collections.Generic;
using System.Data.Common;

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
            object val = values[name];
            return (int)val;
        }

    }

    class SQLite {

        private SqliteConnection conn;

        public SQLite(String fileName) {
            //conn = new SqliteConnection("Data Source=" + fileName + ";Version = 3;New = True;Compress = True;");
            conn = new SqliteConnection(string.Format("Version=3,uri=file:{0}", fileName));
            try {
                conn.Open();
            } catch (Exception e) {
                Debugger.Log(e);
                conn = null;
            }
        }

        private DbCommand Prepare(String sql, params object[] replacements) {
            DbCommand cmd = conn.CreateCommand();
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

                SqliteParameter[] pars = new SqliteParameter[replacements.Length];
                for (int i = 0; i < replacements.Length; i++) {
                    pars[i] = new SqliteParameter("@SqlParam_" + i, replacements[i]);
                }
                cmd.Parameters.AddRange(pars);

                sql = p;
            }
            return cmd;
        }

        protected bool Execute(String sql, params object[] replacements) {
            using (DbCommand cmd = Prepare(sql, replacements)) {
                try {
                    cmd.ExecuteNonQuery();
                } catch (Exception e) {
                    Debugger.Log(e);
                    return false;
                }
                return true;
            }
        }
 
        protected List<Row> Select(String sql, params object[] replacements) {
            using (DbCommand cmd = Prepare(sql, replacements)) {
                List<Row> res = new List<Row>();
                try {
                    using (DbDataReader reader = cmd.ExecuteReader()) {
                        while (reader.Read()) {
                            Dictionary<string, object> values = new Dictionary<string, object>();
                            for (int i = 0; i < reader.FieldCount; i++) {
                                values[reader.GetName(i)] = reader.GetValue(i);
                            }
                            Row row = new Row(values);
                            res.Add(row);
                        }
                    }
                } catch (Exception e) {
                    Debugger.Log(e);
                    return null;
                }
                return res;
            }
        }
        
        public virtual bool IsValid() {
            return conn != null;
        }
    
    }

    class MapDB : SQLite {

        bool valid = false;

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


            bRet &= Execute("CREATE TABLE IF NOT EXISTS remote_maps (" +
                                "ID INTEGER PRIMARY KEY AUTOINCREMENT," +
                                "remote_id TEXT," +
                                "name TEXT, " +
                                "thumbnail TEXT, " +
                                "other_data TEXT" +
                                ")");
            
            bRet &= Execute("CREATE TABLE IF NOT EXISTS asset_manager (" +
                                "ID INTEGER PRIMARY KEY AUTOINCREMENT," +
                                "inputs TEXT," +
                                "output TEXT " +
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
                Path p = new Path(ID, path, purpose);
                p.Watch(save);
                return p;
            }
        }

        public Config GetConfig() {
            List<Row> rows = Select("SELECT * FROM Config");
            if (rows.Count == 0) {
                Execute("INSERT INTO Config (username,password,API) VALUES (?,?,?)", "", "", "");
                rows = Select("SELECT * FROM Config");
            }

            if (rows.Count == 0) {
                ErrorMessage.Show("Failed to read config from database");
                return null;
            } else {
                int ID = rows[0].GetInt("ID");
                String username = rows[0].GetString("username");
                String password = rows[0].GetString("password");
                String api = rows[0].GetString("API");
                Config cfg = new Config(ID, username, password, api);
                cfg.Watch(save);
                return cfg;
            }
        }
        
        public AssetManager GetAssetManager() {
            List<Row> rows = Select("SELECT * FROM asset_manager");
            if (rows.Count == 0) {
                Execute("INSERT INTO asset_manager (inputs,output) VALUES (?,?)", "", "");
                rows = Select("SELECT * FROM asset_manager");
            }

            if (rows.Count == 0) {
                ErrorMessage.Show("Failed to read asset manager from database");
                return null;
            } else {
                int ID = rows[0].GetInt("ID");
                String inputs = rows[0].GetString("inputs");
                String output = rows[0].GetString("output");
                AssetManager assetManager = new AssetManager(ID, inputs, output);
                assetManager.Watch(save);
                return assetManager;
            }
        }

        public RemoteMap GetMap(String remoteID) {
            List<Row> rows = Select("SELECT * FROM remote_maps WHERE remote_id = ?", remoteID);
            if (rows.Count == 0) {
                Execute("INSERT INTO remote_maps (remote_id,name,thumbnail,other_data) VALUES (?,?,?,?)", remoteID, "", "", "{}");
                rows = Select("SELECT * FROM remote_maps WHERE remote_id = ?", remoteID);
            }

            if (rows.Count == 0) {
                ErrorMessage.Show("Failed to read remote map from database");
                return null;
            } else {
                int ID = rows[0].GetInt("ID");
                String remote_id = rows[0].GetString("remote_id");
                String name = rows[0].GetString("name");
                String thumbnail = rows[0].GetString("thumbnail");
                String otherData= rows[0].GetString("other_data");
                RemoteMap p = new RemoteMap(ID, remote_id, name, thumbnail, otherData);
                p.Watch(save);
                return p;
            }
        }

        private void save(Path path) {
            Execute("UPDATE Path SET path=?, purpose=? WHERE ID = ?", path.Value, path.Purpose, path.ID);
        }
        
        private void save(Config cfg) {
            Execute("UPDATE Config SET username=?, password=?, API=? WHERE ID = ?", cfg.Username, cfg.Password, cfg.API, cfg.ID);
        }
          
        private void save(RemoteMap map) {
            Execute("UPDATE remote_maps SET remote_id=?, name=?, thumbnail=?, other_data=? WHERE ID = ?", map.RemoteID, map.Name, map.Thumbnail, map.OtherData, map.ID);
        }
        
        private void save(AssetManager assetManager) {
            Execute("UPDATE asset_manager SET inputs=?, output=? WHERE ID = ?", assetManager.Inputs, assetManager.Output, assetManager.ID);
        }

    }
}
