using System;
using System.Collections.Generic;
using System.Text;

namespace GUILib.libs.json {
    
    enum JsonToken {
        BeginArray,
        EndArray,
        BeginObject,
        EndObject,
        Delim,
        Separator,
        Number,
        String,
        True,
        False,
        Null,
        EndOfInput
    }

    class JsonTokenizer {

        private char[] input;

        private int position = 0;

        public JsonTokenizer(String input) {
            this.input = input.ToCharArray();
        }

        private void SkipWhitespace() {
            while (!IsAtTheEnd()) {
                char c = input[position];
                if(c==' ' || c=='\r' || c=='\n' || c == '\t') {
                    position++;
                    continue;
                } else {
                    break;
                }
            }
        }

        private bool IsAtTheEnd() {
            return position == input.Length;
        }

        private int FromHex(char c) {
            if(c>='a' && c <= 'f') {
                return (c - 'a') + 10;
            } else if(c>='0' && c <= '9') {
                return c - '0';
            } else {
                JsonValue.OnJsonError("Invalid hex char: " + c);
                return -1;
            }
        }

        private bool ParseString(StringBuilder sb) {
            position++;
            while (!IsAtTheEnd()) {
                char strChar = input[position];
                if (strChar == '"') {
                    position++;
                    return true;
                } else if (strChar == '\\') {
                    position++;
                    if (IsAtTheEnd()) {
                        JsonValue.OnJsonError("Invalid string escape");
                        return false;
                    }
                    strChar = input[position];
                    if (strChar == '"') {
                        sb.Append("\"");
                        position++;
                    } else if (strChar == '\\') {
                        sb.Append("\\");
                        position++;
                    } else if (strChar == '/') {
                        sb.Append("/");
                        position++;
                    } else if (strChar == 'b') {
                        sb.Append("\b");
                        position++;
                    } else if (strChar == 'f') {
                        sb.Append("\f");
                        position++;
                    } else if (strChar == 'n') {
                        sb.Append("\n");
                        position++;
                    } else if (strChar == 'r') {
                        sb.Append("\r");
                        position++;
                    } else if (strChar == 't') {
                        sb.Append("\t");
                        position++;
                    } else if (strChar == 'u') {
                        position++;
                        if (IsAtTheEnd()) {
                            JsonValue.OnJsonError("Invalid string escape");
                            return false;
                        }
                        int c1 = FromHex(input[position]);
                        position++;
                        if (IsAtTheEnd()) {
                            JsonValue.OnJsonError("Invalid string escape");
                            return false;
                        }
                        int c2 = FromHex(input[position]);
                        position++;
                        if (IsAtTheEnd()) {
                            JsonValue.OnJsonError("Invalid string escape");
                            return false;
                        }
                        int c3 = FromHex(input[position]);
                        position++;
                        if (IsAtTheEnd()) {
                            JsonValue.OnJsonError("Invalid string escape");
                            return false;
                        }
                        int c4 = FromHex(input[position]);
                        int cx = (c1 << 24) + (c2 << 16) + (c3 << 8) + c4;
                        String cstr = Char.ConvertFromUtf32(cx);
                        sb.Append(cstr);
                    } else {
                        JsonValue.OnJsonError("Invalid string escape");
                        return false;
                    }
                } else {
                    sb.Append(strChar);
                    position++;
                }
            }
            JsonValue.OnJsonError("Failed to parse string");
            return false;
        }

        private bool ParseNumber(StringBuilder sb) {
            int state = 0;
            while (true) {
                char c = IsAtTheEnd() ? '\0' : input[position];

                switch (c) {
                    case '+':
                        if (state == 7) {
                            state = 8;
                            break;
                        } else {
                            JsonValue.OnJsonError("Numeric format error");
                            return false;
                        }

                    case '-':
                        if (state == 0) {
                            state = 1;
                            break;
                        } else if (state == 7) {
                            state = 8;
                            break;
                        } else {
                            JsonValue.OnJsonError("Numeric format error");
                            return false;
                        }


                    case '1':
                    case '2':
                    case '3':
                    case '4':
                    case '5':
                    case '6':
                    case '7':
                    case '8':
                    case '9':
                        if (state == 0 || state == 1) {
                            state = 2;
                            break;
                        } else if (state == 2 || state == 3) {
                            state = 3;
                            break;
                        } else if (state == 5 || state == 6) {
                            state = 6;
                            break;
                        } else if (state == 7 || state == 8) {
                            state = 9;
                            break;
                        } else {
                            JsonValue.OnJsonError("Numeric format error");
                            return false;
                        }

                    case '0':
                        if(state == 0 || state == 1) {
                            state = 4;
                            break;
                        } else if(state == 2 || state == 3) {
                            state = 3;
                            break;
                        } else if(state == 5 || state == 6) {
                            state = 6;
                            break;
                        } else if (state == 7 || state == 8) {
                            state = 9;
                            break;
                        } else {
                            JsonValue.OnJsonError("Numeric format error");
                            return false;
                        }

                    case '.':
                        if (state == 2 || state == 3 || state == 4) {
                            state = 5;
                            break;
                        } else {
                            JsonValue.OnJsonError("Numeric format error");
                            return false;
                        }


                    case 'e':
                    case 'E':
                        if (state == 2 || state == 3 || state == 4 || state == 6) {
                            state = 7;
                            break;
                        } else {
                            JsonValue.OnJsonError("Numeric format error");
                            return false;
                        }


                    default:
                        if (state == 2 || state == 3 || state == 4 || state == 6 || state == 9) {
                            return true;
                        } else {
                            JsonValue.OnJsonError("Numeric format error");
                            return false;
                        }
                }
                sb.Append(c);
                position++;
            }
        }

        public bool GetCurrent(out JsonToken? token, out String contents) {
            token = lastToken;
            contents = lastContents;
            return token.HasValue && contents != null;
        }

        private bool UpdateCurrent() {
            lastToken = null;
            lastContents = null;
            SkipWhitespace();
            if (IsAtTheEnd()) {
                lastToken = JsonToken.EndOfInput;
                lastContents = null;
                return true;
            }

            char first = input[position];
            if (first == '[') {
                lastToken = JsonToken.BeginArray;
                lastContents = "[";
                position++;
                return true;
            } else if (first == ']') {
                lastToken = JsonToken.EndArray;
                lastContents = "]";
                position++;
                return true;
            } else if (first == '{') {
                lastToken = JsonToken.BeginObject;
                lastContents = "{";
                position++;
                return true;
            } else if (first == '}') {
                lastToken = JsonToken.EndObject;
                lastContents = "}";
                position++;
                return true;
            } else if (first == '"') {
                // String
                lastToken = JsonToken.String;
                StringBuilder sb = new StringBuilder();
                if (ParseString(sb)) {
                    lastContents = sb.ToString();
                    return true;
                }
            } else if (first == '-' || (first >= '0' && first <= '9')) {
                // Number
                lastToken = JsonToken.Number;
                StringBuilder sb = new StringBuilder();
                if (ParseNumber(sb)) {
                    lastContents = sb.ToString();
                    return true;
                }
            } else if (first == ',') {
                lastToken = JsonToken.Delim;
                lastContents = ",";
                position++;
                return true;
            } else if (first == ':') {
                lastToken = JsonToken.Separator;
                lastContents = ":";
                position++;
                return true;
            } else if (first == 't') {
                lastToken = JsonToken.True;
                position++;
                if (!IsAtTheEnd()) {
                    if (input[position] == 'r') {
                        position++;
                        if (!IsAtTheEnd()) {
                            if (input[position] == 'u') {
                                position++;
                                if (!IsAtTheEnd()) {
                                    if (input[position] == 'e') {
                                        position++;
                                        lastContents = "true";
                                        return true;
                                    }
                                }
                            }
                        }
                    }
                }
            } else if (first == 'f') {
                lastToken = JsonToken.False;
                if (!IsAtTheEnd()) {
                    if (input[position] == 'a') {
                        position++;
                        if (!IsAtTheEnd()) {
                            if (input[position] == 'l') {
                                position++;
                                if (!IsAtTheEnd()) {
                                    if (input[position] == 's') {
                                        position++;
                                        if (input[position] == 'e') {
                                            position++;
                                            lastContents = "false";
                                            return true;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            } else if (first == 'n') {
                lastToken = JsonToken.Null;
                if (!IsAtTheEnd()) {
                    if (input[position] == 'u') {
                        position++;
                        if (!IsAtTheEnd()) {
                            if (input[position] == 'l') {
                                position++;
                                if (!IsAtTheEnd()) {
                                    if (input[position] == 'l') {
                                        position++;
                                        lastContents = "null";
                                        return true;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            JsonValue.OnJsonError("Invalid token");
            return false;
        }

        public bool GetNext(out JsonToken? token, out String contents) {
            if (!hasError) {
                if (UpdateCurrent()) {
                    if(GetCurrent(out token, out contents)) {
                        if(token.HasValue && contents != null) {
                            return true;
                        }
                    }
                }
            }
            hasError = true;
            token = null;
            contents = null;
            return false;
        }

        private bool hasError = false;
        private JsonToken? lastToken = null;
        private String lastContents = null;
    }

    public abstract class JsonValue {

        public static void OnJsonError(String error) {
            // Console.Error(error);
            //throw Exception(error);
            return;
        }

        public static JsonValue Parse(String jsn) {
            JsonTokenizer tokenizer = new JsonTokenizer(jsn);
            return Parse(tokenizer);
        }

        private static JsonArray ParseArray(JsonTokenizer tokens) {
            JsonArray arr = new JsonArray();

            for(bool first = true; true; first = false) { 
                JsonToken? t = null;
                String contents = null;

                bool ok = false;
                if (tokens.GetNext(out t, out contents)) {
                    switch (t) {
                        case JsonToken.EndArray:
                            if (first) {
                                return arr;
                            }
                            JsonValue.OnJsonError("Invalid token");
                            return null;

                        default:
                            JsonValue value = Parse(tokens, t.Value, contents);
                            if (value != null) {
                                arr.Values.Add(value);
                                ok = true;
                            }
                            break;
                    }
                }
                if (!ok) {
                    JsonValue.OnJsonError("Invalid token");
                    return null;
                }
                ok = false;

                if (tokens.GetNext(out t, out contents)) {
                    switch (t) {
                        case JsonToken.EndArray:
                            return arr;

                        case JsonToken.Delim:
                            ok = true;
                            break;
                    }
                }
                if (!ok) {
                    JsonValue.OnJsonError("Invalid token");
                    return null;
                }
            }
        }
        
        private static JsonObject ParseObject(JsonTokenizer tokens) {
            JsonObject obj = new JsonObject();
            for(bool first = true; true; first = false) { 
                JsonToken? tokenKey = null;
                String contentsKey = null;
                JsonToken? tokenSeparator = null;
                String contentsSeparator = null;
                bool ok = false;
                if (tokens.GetNext(out tokenKey, out contentsKey)) {
                    switch (tokenKey) {
                        case JsonToken.EndObject:
                            if (first) {
                                return obj;
                            }
                            break;

                        case JsonToken.String:
                            ok = true;
                            break;
                    }
                }

                if (!ok) {
                    JsonValue.OnJsonError("Invalid token");
                    return null;
                }

                ok = false;
                if (tokens.GetNext(out tokenSeparator, out contentsSeparator)) {
                    if(tokenSeparator == JsonToken.Separator){
                        ok = true;
                    }
                }

                if (!ok) {
                    JsonValue.OnJsonError("Invalid token");
                    return null;
                }
                
                ok = false;
                JsonValue next = Parse(tokens);
                if (next != null) {
                    obj.Values[contentsKey] = next;
                } else {
                    JsonValue.OnJsonError("Invalid token");
                    return null;
                }

                if (tokens.GetNext(out tokenSeparator, out contentsSeparator)) {
                    switch (tokenSeparator) {
                        case JsonToken.Delim:
                            ok = true;
                            break;

                        case JsonToken.EndObject:
                            return obj;
                    }
                }

                if (!ok) {
                    JsonValue.OnJsonError("Invalid token");
                    return null;
                }
            }
        }

        private static JsonValue Parse(JsonTokenizer tokens) {
            JsonToken? t = null;
            String contents = null;
            if (tokens.GetNext(out t, out contents)) {
                return Parse(tokens, t.Value, contents);
            }
            JsonValue.OnJsonError("Failed to parse JSON");
            return null;
        }

        private static JsonValue Parse(JsonTokenizer tokens, JsonToken token, String contents) {
            switch (token) {
                case JsonToken.BeginArray:
                    JsonArray arr = ParseArray(tokens);
                    return arr;
                case JsonToken.BeginObject:
                    JsonObject obj = ParseObject(tokens);
                    return obj;
                case JsonToken.True:
                    return new JsonBoolean(true);
                case JsonToken.False:
                    return new JsonBoolean(false);
                case JsonToken.Null:
                    return new JsonNull();
                case JsonToken.Number:
                    return JsonNumber.FromString(contents);
                case JsonToken.String:
                    return new JsonString(contents);
            }
            JsonValue.OnJsonError("Failed to parse JSON");
            return null;
        }

        public String ToJson() {
            StringBuilder sb = new StringBuilder();
            ToJson(sb);
            return sb.ToString();
        }

        protected abstract void ToJson(StringBuilder sb);

        public virtual bool IsBoolean() {
            return false;
        }

        public JsonBoolean AsBoolean() {
            return (JsonBoolean)this;
        }

        public virtual bool IsNull() {
            return false;
        }

        public JsonNull AsNull() {
            return (JsonNull)this;
        }

        public virtual bool IsNumber() {
            return false;
        }

        public JsonNumber AsNumber() {
            return (JsonNumber)this;
        }

        public virtual bool IsString() {
            return false;
        }

        public JsonString AsString() {
            return (JsonString)this;
        }
        
        public virtual bool IsObject() {
            return false;
        }

        public JsonObject AsObject() {
            return (JsonObject)this;
        }
         
        public virtual bool IsArray() {
            return false;
        }

        public JsonArray AsArray() {
            return (JsonArray)this;
        }

    }

    public class JsonBoolean : JsonValue {

        public bool Value { get; set; }

        public JsonBoolean(bool value) {
            this.Value = value;
        }

        protected override void ToJson(StringBuilder sb) {
            sb.Append(Value ? "true" : "false");
        }

        public override bool IsBoolean() {
            return true;
        }

    }

    public class JsonNull : JsonValue {

        protected override void ToJson(StringBuilder sb) {
            sb.Append("null");
        }

        public override bool IsNull() {
            return true;
        }

    }

    public class JsonNumber : JsonValue {

        private int iValue = 0;
        private bool isIValue = false;

        private double dValue = 0;
        private bool isDValue = false;

        private String original = null;

        public int IntValue {
            get {
                if (isIValue) {
                    return iValue;
                } else {
                    return (int)dValue;
                }
            }
            set {
                iValue = value;
                isIValue = true;
                isDValue = false;
                original = null;
            }
        }

        public double RealValue {
            get {
                if (isDValue) {
                    return dValue;
                } else {
                    return (double)iValue;
                }
            }
            set {
                dValue = value;
                isDValue = true;
                isIValue = false;
                original = null;
            }
        }

        public JsonNumber(double value) {
            RealValue = value;
        }
        
        public JsonNumber(int value) {
            IntValue = value;
        }
        
        public static JsonNumber FromString(String value) {
            if(value.Contains("e") || value.Contains("E") || value.Contains(".")) {
                double d;
                if(Double.TryParse(value, out d)) {
                    JsonNumber num = new JsonNumber(d);
                    num.original = value;
                    return num;
                }
                JsonValue.OnJsonError("Failed to parse real: " + value);
                return null;
            } else {
                int d;
                if (Int32.TryParse(value, out d)) {
                    JsonNumber num = new JsonNumber(d);
                    num.original = value;
                    return num;
                }
                JsonValue.OnJsonError("Failed to parse integer: " + value);
                return null;
            }
        }

        protected override void ToJson(StringBuilder sb) {
            if (original != null) {
                sb.Append(original);
            } else if (isIValue) {
                sb.Append("" + iValue);
            } else {
                sb.Append("" + dValue);
            }
        }

        public override bool IsNumber() {
            return true;
        }

    }

    public class JsonString : JsonValue {

        public String Value { get; set; }

        public JsonString(String value) {
            this.Value = value;
        }

        private static bool NeedEscape(string src, int i) {
            char c = src[i];
            return c < 32 || c == '"' || c == '\\'
                || (c >= '\uD800' && c <= '\uDBFF' &&
                    (i == src.Length - 1 || src[i + 1] < '\uDC00' || src[i + 1] > '\uDFFF'))
                || (c >= '\uDC00' && c <= '\uDFFF' &&
                    (i == 0 || src[i - 1] < '\uD800' || src[i - 1] > '\uDBFF'))
                || c == '\u2028' || c == '\u2029'
                || (c == '/' && i > 0 && src[i - 1] == '<');
        }

        public static void AppendEscapedString(StringBuilder sb, string src) {
            int start = 0;
            for (int i = 0; i < src.Length; i++)
                if (NeedEscape(src, i)) {
                    sb.Append(src, start, i - start);
                    switch (src[i]) {
                        case '\b': sb.Append("\\b"); break;
                        case '\f': sb.Append("\\f"); break;
                        case '\n': sb.Append("\\n"); break;
                        case '\r': sb.Append("\\r"); break;
                        case '\t': sb.Append("\\t"); break;
                        case '\"': sb.Append("\\\""); break;
                        case '\\': sb.Append("\\\\"); break;
                        case '/': sb.Append("\\/"); break;
                        default:
                            sb.Append("\\u");
                            sb.Append(((int)src[i]).ToString("x04"));
                            break;
                    }
                    start = i + 1;
                }
            sb.Append(src, start, src.Length - start);
        }

        protected override void ToJson(StringBuilder sb) {
            sb.Append("\"");
            AppendEscapedString(sb, Value);
            sb.Append("\"");
        }

        public override bool IsString() {
            return true;
        }

    }

    public class JsonObject : JsonValue {

        public IDictionary<string, JsonValue> Values = new Dictionary<string, JsonValue>();

        protected override void ToJson(StringBuilder sb) {
            sb.Append("{");

            bool first = true;
            foreach(String key in Values.Keys) {
                if (!first) {
                    sb.Append(",");
                }
                first = false;
                JsonValue value = Values[key];
                sb.Append("\"");
                JsonString.AppendEscapedString(sb, key);
                sb.Append("\":");
                String str = value.ToJson();
                sb.Append(str);
            }

            sb.Append("}");
        }

        public override bool IsObject() {
            return true;
        }

        public JsonValue GetValue(String key) {
            JsonValue val = null;
            if (Values.TryGetValue(key, out val)) {
                return val;
            }
            //JsonValue.OnJsonError("Missing key: " + key);
            return null;
        }

        public JsonString GetString(string key) {
            JsonValue val = GetValue(key);
            if(val != null) {
                if (val.IsString()) {
                    return val.AsString();
                }
            }
            JsonValue.OnJsonError("Missing key: " + key);
            return null;
        }

        public JsonBoolean GetBoolean(string key) {
            JsonValue val = GetValue(key);
            if (val != null) {
                if (val.IsBoolean()) {
                    return val.AsBoolean();
                }
            }
            JsonValue.OnJsonError("Missing key: " + key);
            return null;
        }

        public JsonNull GetNull(string key) {
            JsonValue val = GetValue(key);
            if (val != null) {
                if (val.IsNull()) {
                    return val.AsNull();
                }
            }
            JsonValue.OnJsonError("Missing key: " + key);
            return null;
        }

        public JsonNumber GetNumber(string key) {
            JsonValue val = GetValue(key);
            if (val != null) {
                if (val.IsNumber()) {
                    return val.AsNumber();
                }
            }
            JsonValue.OnJsonError("Missing key: " + key);
            return null;
        }
  
        public JsonArray GetArray(string key) {
            JsonValue val = GetValue(key);
            if (val != null) {
                if (val.IsArray()) {
                    return val.AsArray();
                }
            }
            JsonValue.OnJsonError("Missing key: " + key);
            return null;
        }

        public JsonObject GetObject(string key) {
            JsonValue val = GetValue(key);
            if (val != null) {
                if (val.IsObject()) {
                    return val.AsObject();
                }
            }
            JsonValue.OnJsonError("Missing key: " + key);
            return null;
        }

        public String GetRawString(string key) {
            JsonString val = GetString(key);
            if (val != null) {
                return val.Value;
            }
            JsonValue.OnJsonError("Missing key: " + key);
            return null;
        }

        public bool? GetRawBoolean(string key) {
            JsonBoolean val = GetBoolean(key);
            if (val != null) {
                return val.Value;
            }
            JsonValue.OnJsonError("Missing key: " + key);
            return null;
        }
        
        public int? GetRawInt(string key) {
            JsonNumber val = GetNumber(key);
            if (val != null) {
                return val.IntValue;
            }
            JsonValue.OnJsonError("Missing key: " + key);
            return null;
        }
        
        public double? GetRawReal(string key) {
            JsonNumber val = GetNumber(key);
            if (val != null) {
                return val.RealValue;
            }
            JsonValue.OnJsonError("Missing key: " + key);
            return null;
        }

        public void Put(string key, int value) {
            Put(key, new JsonNumber(value));
        }

        public void Put(string key, double value) {
            Put(key, new JsonNumber(value));
        }

        public void PutNull(string key) {
            Put(key, new JsonNull());
        }

        public void Put(string key, bool value) {
            Put(key, new JsonBoolean(value));
        }

        public void Put(string key, string value) {
            Put(key, new JsonString(value));
        }

        public void Put(string key, JsonValue value) {
            Values[key] = value;
        }
    }

    public class JsonArray : JsonValue {

        public IList<JsonValue> Values = new List<JsonValue>();

        protected override void ToJson(StringBuilder sb) {
            sb.Append("{");

            for(int i = 0; i < Values.Count; i++) { 
                if (i >= 0) {
                    sb.Append(",");
                }
                JsonValue value = Values[i];
                String str = value.ToJson();
                sb.Append(str);
            }

            sb.Append("}");
        }

        public override bool IsArray() {
            return true;
        }

    }

}
