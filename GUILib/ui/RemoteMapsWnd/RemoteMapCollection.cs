using GUILib.data;
using GUILib.db;
using GUILib.ui.utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GUILib.ui.RemoteMapsWnd {
    class RemoteMapCollection {

        private IEnumerable<RemoteSearchedMap> maps;

        private Model model;

        public RemoteMapCollection() {
            model = Model.Create();
        }

        public void Reset(IEnumerable<RemoteSearchedMap> maps) {
            this.maps = maps;
        }

        public int Count { get { return maps==null ? 0 : maps.Count(); } }

        public void QueryPage(int offset, int size, Action<IEnumerable<RemoteMap>> callback) {
            
            // Number of total items to be returned
            int queryTotal = 0;
            if (offset < Count) {
                int available = Count - offset;
                if (size > available) {
                    queryTotal = available;
                } else {
                    queryTotal = size;
                }
            }

            if(queryTotal == 0) {
                callback(new List<RemoteMap>());
                return;
            }

            List<RemoteMap> result = new List<RemoteMap>();
            List<RemoteMap> unresolved = new List<RemoteMap>();
            for(int i  = offset; i < offset + queryTotal; i++) {
                RemoteSearchedMap rm = maps.ElementAt(i);

                RemoteMap m = model.GetMap(rm.ID);
                if(m.Name != rm.Name) {
                    m.Name = rm.RawName;
                }
                if (m.Thumbnail == null || m.Thumbnail == "") {
                    unresolved.Add(m);
                } else if(m.Title == null) {
                    unresolved.Add(m);
                }
                result.Add(m);
            }

            if (unresolved.Count == 0) {
                callback(result);
                return;
            }

            new AsyncJob(() => {
                foreach (RemoteMap map in unresolved) {
                    if (map.Thumbnail == null || map.Thumbnail == "") {
                        String thumb = model.GetMapThumbnail(map.RemoteID);
                        if (thumb != "" && thumb != null) {
                            map.Thumbnail = thumb;
                        } else {
                            return false;
                        }
                    }
                    if(map.Title == null) {
                        // Scrap main page
                        String otherData = model.GetMapMainData(map.RemoteID);
                        if(otherData==null || otherData == "") {
                            return false;
                        } else {
                            map.OtherData = otherData;
                            String abc = map.Title;
                        }
                    }
                }
                return true;
            }, (res) => {
                if(res is true) {
                    callback(result);
                } else {
                    ErrorMessage.Show("Failed to resolve some thumbnails");
                    callback(null);
                }
            }).Run();
        }

    }
}
