using GUILib.ui.utils;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Windows.Threading;

namespace GUILib.db {
    public abstract class ObservableObject<T> {

        Dictionary<string, object> values = new Dictionary<string, object>();

        private List<Pair<Dispatcher, Action<T>>> watchers = new List<Pair<Dispatcher, Action<T>>>();

        public void Watch(Action<T> callback) {
            foreach(Pair<Dispatcher,Action<T>> watcher in watchers) {
                if(watcher.right == callback) { 
                    return; 
                }
            }
            watchers.Add(new Pair<Dispatcher, Action<T>>(Dispatcher.CurrentDispatcher, callback));
                
        }

        public void Unwatch(Action<T> callback) {
            Pair<Dispatcher, Action<T>> toRemove = null;
            foreach (Pair<Dispatcher, Action<T>> watcher in watchers) {
                if (watcher.right == callback) {
                    toRemove = watcher;
                    break;
                }
            }
            if (toRemove != null) {
                watchers.Remove(toRemove);
            }
        }

        private X GET<X> (String name, X defaultValue, out bool none) {
            return GET<X>(name, defaultValue, values, out none);
        }
        
        private X GET<X> (String name, X defaultValue, IDictionary<string, object> source, out bool none) {
            object value = null;
            if (source.TryGetValue(name, out value)) {
                none = false;
                return (X)value;
            } else {
                none = true;
                return defaultValue;
            }
        }

        protected X GET<X>(X defaultValue, [CallerMemberName] String key=null) {
            bool none = false;
            return GET<X>(key, defaultValue, out none);
        }

        protected X GET<X>(X defaultValue, IDictionary<string, object> soruce, [CallerMemberName] String key=null) {
            bool none = false;
            return GET<X>(key, defaultValue, soruce, out none);
        }

        private void SET<X>(String name, X value) {
            SET<X>(name, value, values);
        }
        
        private void SET<X>(String name, X value, IDictionary<string, object> soruce) {
            bool none = false;
            X originalValue = GET<X>(name, value, soruce, out none);
            if (!EqualityComparer<X>.Default.Equals(originalValue, value) || none) {
                object me = this;
                soruce[name] = value;
                NotifyAlways(name);
            }
        }

        protected void SET<X>(X value, [CallerMemberName] String key = null) {
            SET<X>(key, value);
        }
        
        protected void SET<X>(X value, IDictionary<string, object> source, [CallerMemberName] String key = null) {
            SET<X>(key, value, source);
        }
        
        protected string ToJsonString(IDictionary<string, object> source) {
            return Json.JsonParser.ToJson(source);
        }

        protected IDictionary<string, object> FromJsonString(string str) {
            if (str!=null && str != "") {
                return Json.JsonParser.FromJson(str);
            }
            return new Dictionary<string, object>();
        }

        protected void NotifyAlways([CallerMemberName] String key = null) {
            object me = this;
            T self = (T)me;
            foreach (Pair<Dispatcher, Action<T>> listener in watchers) {
                Dispatcher d = listener.Left;
                if (d == Dispatcher.CurrentDispatcher) {
                    listener.right(self);
                } else {
                    d.Invoke(() => listener.right(self));
                }
            }
        }

        protected String FormatFileSize(int bytes) {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = (double) bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1) {
                order++;
                len = len / 1024;
            }
            return String.Format("{0:0.##} {1}", len, sizes[order]);
        }

        protected String FormatFileSize(String bytes) {
            int test = 0;
            if(Int32.TryParse(bytes, out test)) {
                return FormatFileSize(test);
            }
            return "<Invalid size>";
        }

        protected int INT = 0;
        protected string STRING = null;
    }
}
