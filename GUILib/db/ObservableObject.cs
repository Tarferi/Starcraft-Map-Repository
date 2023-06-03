using GUILib.libs.json;
using GUILib.ui.utils;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Windows.Threading;

namespace GUILib.db {

    public class OT<T> {

        public readonly Func<JsonValue, T> Getter;
        public readonly Func<T, JsonValue> Converter;

        public readonly T DefaultValue;

        public OT(T defaultValue, Func<JsonValue, T> getter, Func<T, JsonValue> converter) {
            this.DefaultValue = defaultValue;
            this.Getter = getter;
            this.Converter = converter;
        }
    }

    public abstract class RefObj<T> {

        private int refs = 0;
        List<Action<T>> disposers = new List<Action<T>>();

        public void IncRef() {
            refs++;
        }

        public void DecRef() {
            refs--;
            if (refs == 0) {
                foreach(Action<T> disposer in disposers) {
                    T self = (T)((object)this);
                    disposer(self);
                }
            }
        }

        public void AddDisposeListener(Action<T> obj) {
            disposers.Add(obj);
        }
    }

    public abstract class ObservableObject<T> : RefObj<T> {

        JsonObject values = new JsonObject();

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

        private X GET<X> (String name, OT<X> defaultValue, out bool none) {
            return GET<X>(name, defaultValue, values, out none);
        }
        
        private X GET<X> (String name, OT<X> defaultValue, JsonObject source, out bool none) {
            JsonValue val = source.GetValue(name);
            if (val != null) {
                none = false;
                return defaultValue.Getter(val);
            }
            none = true;
            return defaultValue.DefaultValue;
        }

        protected X GET<X>(OT<X> defaultValue, [CallerMemberName] String key=null) {
            bool none = false;
            return GET<X>(key, defaultValue, out none);
        }
        
        protected X GET<X>(OT<X> defaultValue, JsonObject source, [CallerMemberName] String key=null) {
            bool none = false;
            return GET<X>(key, defaultValue, source, out none);
        }

        private void SET<X>(String name, X value, OT<X> defaultValue) {
            SET<X>(name, value, defaultValue, values);
        }
        
        private void SET<X>(String name, X value, OT<X> defaultValue, JsonObject source) {
            bool none = false;
            X originalValue = GET<X>(name, defaultValue, source, out none);
            if (!EqualityComparer<X>.Default.Equals(originalValue, value) || none) {
                object me = this;
                source.Put(name, defaultValue.Converter(value));
                NotifyAlways(name);
            }
        }

        // Template specific

        protected void SET(int value, [CallerMemberName] String key = null) {
            SET<int>(key, value, INT, values);
        }
        
        protected void SET(String value, [CallerMemberName] String key = null) {
            SET<String>(key, value, STRING, values);
        }
        
        protected void SET(int value, JsonObject source, [CallerMemberName] String key = null) {
            SET<int>(key, value, INT, source);
        }
        
        protected void SET(String value, JsonObject source, [CallerMemberName] String key = null) {
            SET<String>(key, value, STRING, source);
        }
        
        protected string ToJsonString(JsonObject source) {
            return source.ToJson();
        }

        protected JsonObject FromJsonString(string str) {
            if (str!=null && str != "") {
                JsonValue val = JsonValue.Parse(str);
                if (val != null) {
                    if (val.IsObject()) {
                        return val.AsObject();
                    }
                }
            }
            return null;
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

        protected static OT<int> INT = new OT<int>(0, (e)=>e.AsNumber().IntValue, (x)=>new JsonNumber(x));
        protected static OT<string> STRING = new OT<string>(null, (e)=>e.AsString().Value, (x)=>new JsonString(x));
    }
}
