using System;
using System.Collections.Generic;

namespace GUILib.data {

    class LocalMap {

    }

    class LocalMapRepository {

        private Model model;

        public LocalMapRepository() {
            model = Model.Create();
        }


        public IEnumerable<LocalMap> GetMaps(String filer, int offset, int count) {
            return null;
        }

    }
}
