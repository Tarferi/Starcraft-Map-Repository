#include "json.h"
#include <string>
#include <stdlib.h>
#include <vector>
#include <map>

String& String::operator=(const char* text) {
	Clear();
	Append(text);
	return *this;
}

String::String() {
	static_assert(sizeof(buff) >= sizeof(std::string), "Invalid buffer size");
	memset(buff, 0, sizeof(buff));
	std::string* str = new(buff) std::string();
	
}

String::~String() {
	std::string* str = (std::string*)buff;
	str->~basic_string();
	memset(buff, 0, sizeof(buff));
}

void String::Clear() {
	std::string* str = (std::string*)buff;
	str->clear();
}

void String::Import(String& another) {
	Clear();
	std::string* remote = (std::string*)another.buff;
	Append(remote->c_str());
}

void String::Append(const char* text) {
	std::string* str = (std::string*)buff;
	str->append(text);
}

void String::Append(char text) {
	char x[2] = { text,0 };
	Append(x);
}

bool String::Contains(const char* substr) {
	const char* cstr = GetRaw();
	return strstr(cstr, substr) != nullptr;
}

const char* String::GetRaw() {
	std::string* str = (std::string*)buff;
	return str->c_str();
}

JsonTokenizer::JsonTokenizer(char* input, unsigned int length) {
	this->input = input;
	this->length = length;
}

bool JsonTokenizer::IsAtTheEnd() {
	return position == length;
}

void JsonTokenizer::SkipWhitespace() {
	while (!IsAtTheEnd()) {
		char c = input[position];
		if (c == ' ' || c == '\r' || c == '\n' || c == '\t') {
			position++;
			continue;
		} else {
			break;
		}
	}
}

int JsonTokenizer::FromHex(char c) {
	if (c >= 'a' && c <= 'f') {
		return (c - 'a') + 10;
	} else if (c >= '0' && c <= '9') {
		return c - '0';
	} else {
		JsonValue::OnJsonError("Invalid hex char: " + c);
		return -1;
	}
}

bool JsonTokenizer::ParseString(String& sb) {
	position++;
	while (!IsAtTheEnd()) {
		char strChar = input[position];
		if (strChar == '"') {
			position++;
			return true;
		} else if (strChar == '\\') {
			position++;
			if (IsAtTheEnd()) {
				JsonValue::OnJsonError("Invalid string escape");
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
					JsonValue::OnJsonError("Invalid string escape");
					return false;
				}
				int c1 = FromHex(input[position]);
				position++;
				if (IsAtTheEnd()) {
					JsonValue::OnJsonError("Invalid string escape");
					return false;
				}
				int c2 = FromHex(input[position]);
				position++;
				if (IsAtTheEnd()) {
					JsonValue::OnJsonError("Invalid string escape");
					return false;
				}
				int c3 = FromHex(input[position]);
				position++;
				if (IsAtTheEnd()) {
					JsonValue::OnJsonError("Invalid string escape");
					return false;
				}
				int c4 = FromHex(input[position]);
				int cx = (c1 << 24) + (c2 << 16) + (c3 << 8) + c4;
				//String cstr = Char.ConvertFromUtf32(cx);
				// Let's hope we won't need this
				sb.Append(c1);
				sb.Append(c2);
				sb.Append(c3);
				sb.Append(c4);
			} else {
				JsonValue::OnJsonError("Invalid string escape");
				return false;
			}
		} else {
			sb.Append(strChar);
			position++;
		}
	}
	JsonValue::OnJsonError("Failed to parse string");
	return false;
}

bool JsonTokenizer::ParseNumber(String& sb) {
	int state = 0;
	while (true) {
		char c = IsAtTheEnd() ? '\0' : input[position];

		switch (c) {
		case '+':
			if (state == 7) {
				state = 8;
				break;
			} else {
				JsonValue::OnJsonError("Numeric format error");
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
				JsonValue::OnJsonError("Numeric format error");
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
				JsonValue::OnJsonError("Numeric format error");
				return false;
			}

		case '0':
			if (state == 0 || state == 1) {
				state = 4;
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
				JsonValue::OnJsonError("Numeric format error");
				return false;
			}

		case '.':
			if (state == 2 || state == 3 || state == 4) {
				state = 5;
				break;
			} else {
				JsonValue::OnJsonError("Numeric format error");
				return false;
			}


		case 'e':
		case 'E':
			if (state == 2 || state == 3 || state == 4 || state == 6) {
				state = 7;
				break;
			} else {
				JsonValue::OnJsonError("Numeric format error");
				return false;
			}


		default:
			if (state == 2 || state == 3 || state == 4 || state == 6 || state == 9) {
				return true;
			} else {
				JsonValue::OnJsonError("Numeric format error");
				return false;
			}
		}
		sb.Append(c);
		position++;
	}
}

bool JsonTokenizer::GetCurrent(JsonToken& token, String& contents) {
	token = lastToken;
	contents.Import(lastContents);
	return token != JsonToken::T_INVALID;
}

static int cnt = 0;

bool JsonTokenizer::UpdateCurrent() {
	lastToken = JsonToken::T_INVALID;
	lastContents.Clear();
	SkipWhitespace();
	if (IsAtTheEnd()) {
		lastToken = JsonToken::T_EndOfInput;
		return true;
	}

	char first = input[position];
	if (first == '[') {
		lastToken = JsonToken::T_BeginArray;
		lastContents = "[";
		position++;
		return true;
	} else if (first == ']') {
		lastToken = JsonToken::T_EndArray;
		lastContents = "]";
		position++;
		return true;
	} else if (first == '{') {
		lastToken = JsonToken::T_BeginObject;
		lastContents = "{";
		position++;
		return true;
	} else if (first == '}') {
		lastToken = JsonToken::T_EndObject;
		lastContents = "}";
		position++;
		return true;
	} else if (first == '"') {
		// String
		lastToken = JsonToken::T_String;
		if (ParseString(lastContents)) {
			return true;
		}
	} else if (first == '-' || (first >= '0' && first <= '9')) {
		// Number
		lastToken = JsonToken::T_Number;
		if (ParseNumber(lastContents)) {
			return true;
		}
	} else if (first == ',') {
		lastToken = JsonToken::T_Delim;
		lastContents = ",";
		position++;
		return true;
	} else if (first == ':') {
		lastToken = JsonToken::T_Separator;
		lastContents = ":";
		position++;
		return true;
	} else if (first == 't') {
		lastToken = JsonToken::T_True;
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
		lastToken = JsonToken::T_False;
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
		lastToken = JsonToken::T_Null;
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
	JsonValue::OnJsonError("Invalid token");
	return false;
}

bool JsonTokenizer::GetNext(JsonToken& token, String& contents) {
	if (!hasError) {
		if (UpdateCurrent()) {
			if (GetCurrent(token, contents)) {
				if (token != JsonToken::T_INVALID) {
					return true;
				}
			}
		}
	}
	hasError = true;
	token = JsonToken::T_INVALID;
	contents.Clear();
	return false;
}

void JsonValue::OnJsonError(const char* error) {
	// Console.Error(error);
	//throw Exception(error);
	//fprintf(stderr, "Json error: %s\n", error);
	return;
}

JsonValue* JsonValue::Parse(char* str, unsigned int length) {
	JsonTokenizer tokenizer(str, length);
	return Parse(tokenizer);
}

JsonValue::JsonValue() {

}

JsonValue::~JsonValue() {
}

JsonValue* JsonValue::Parse(JsonTokenizer& tokens) {
	JsonToken t = JsonToken::T_INVALID;
	String contents;
	if (tokens.GetNext(t, contents)) {
		return Parse(tokens, t, contents);
	}
	JsonValue::OnJsonError("Failed to parse JSON");
	return nullptr;
}

JsonValue* JsonValue::Parse(JsonTokenizer& tokens, JsonToken& token, String& contents) {
	switch (token) {
		case JsonToken::T_BeginArray:
			return ParseArray(tokens);
		case JsonToken::T_BeginObject:
			return ParseObject(tokens);
		case JsonToken::T_True:
			return new JsonBoolean(true);
		case JsonToken::T_False:
			return new JsonBoolean(false);
		case JsonToken::T_Null:
			return new JsonNull();
		case JsonToken::T_Number:
			return JsonNumber::FromString(contents);
		case JsonToken::T_String:
			return new JsonString(contents);
	}
	JsonValue::OnJsonError("Failed to parse JSON");
	return nullptr;
}

JsonArray* JsonValue::ParseArray(JsonTokenizer& tokens) {
	JsonArray* arr = new JsonArray();

	for (bool first = true; true; first = false) {
		JsonToken t = JsonToken::T_INVALID;
		String contents;

		bool ok = false;
		if (tokens.GetNext(t, contents)) {
			switch (t) {
				case JsonToken::T_EndArray:
				{
					if (first) {
						return arr;
					}
					JsonValue::OnJsonError("Invalid token");
					delete arr;
					return nullptr;
				}
				default:
				{
					JsonValue* value = Parse(tokens, t, contents);
					if (value) {
						arr->Add(value);
						ok = true;
					}
					break;
				}
			}
		}
		if (!ok) {
			JsonValue::OnJsonError("Invalid token");
			delete arr;
			return nullptr;
		}
		ok = false;

		if (tokens.GetNext(t, contents)) {
			switch (t) {
			case JsonToken::T_EndArray:
				return arr;

			case JsonToken::T_Delim:
				ok = true;
				break;
			}
		}
		if (!ok) {
			JsonValue::OnJsonError("Invalid token");
			delete arr;
			return nullptr;
		}
	}
}

JsonObject* JsonValue::ParseObject(JsonTokenizer& tokens) {
	JsonObject* obj = new JsonObject();
	for (bool first = true; true; first = false) {
		JsonToken tokenKey = JsonToken::T_INVALID;
		String contentsKey;
		JsonToken tokenSeparator = JsonToken::T_INVALID;
		String contentsSeparator;
		bool ok = false;
		if (tokens.GetNext(tokenKey, contentsKey)) {
			switch (tokenKey) {
			case JsonToken::T_EndObject:
				if (first) {
					return obj;
				}
				break;

			case JsonToken::T_String:
				ok = true;
				break;
			}
		}

		if (!ok) {
			JsonValue::OnJsonError("Invalid token");
			delete obj;
			return nullptr;
		}

		ok = false;
		if (tokens.GetNext(tokenSeparator, contentsSeparator)) {
			if (tokenSeparator == JsonToken::T_Separator) {
				ok = true;
			}
		}

		if (!ok) {
			JsonValue::OnJsonError("Invalid token");
			delete obj;
			return nullptr;
		}

		ok = false;
		JsonValue* next = Parse(tokens);
		if (next) {
			obj->Put(contentsKey.GetRaw(), next);
		} else {
			JsonValue::OnJsonError("Invalid token");
			delete obj;
			return nullptr;
		}

		if (tokens.GetNext(tokenSeparator, contentsSeparator)) {
			switch (tokenSeparator) {
			case JsonToken::T_Delim:
				ok = true;
				break;

			case JsonToken::T_EndObject:
				return obj;
			}
		}

		if (!ok) {
			JsonValue::OnJsonError("Invalid token");
			delete obj;
			return nullptr;
		}
	}
}

bool JsonValue::IsBoolean() {
	return false;
}

JsonBoolean* JsonValue::AsBoolean() {
	return IsBoolean() ? dynamic_cast<JsonBoolean*>(this) : nullptr;
}

bool JsonValue::IsNull() {
	return false;
}

JsonNull* JsonValue::AsNull() {
	return IsNull() ? dynamic_cast<JsonNull*>(this) : nullptr;
}

bool JsonValue::IsNumber() {
	return false;
}

JsonNumber* JsonValue::AsNumber() {
	return IsNumber() ? dynamic_cast<JsonNumber*>(this) : nullptr;
}

bool JsonValue::IsString() {
	return false;
}

JsonString* JsonValue::AsString() {
	return IsString() ? dynamic_cast<JsonString*>(this) : nullptr;
}

bool JsonValue::IsObject() {
	return false;
}

JsonObject* JsonValue::AsObject() {
	return IsObject() ? dynamic_cast<JsonObject*>(this) : nullptr;
}

bool JsonValue::IsArray() {
	return false;
}

JsonArray* JsonValue::AsArray() {
	return IsArray() ? dynamic_cast<JsonArray*>(this) : nullptr;
}

bool JsonValue::IsValid() {
	return true;
}

JsonBoolean::JsonBoolean(bool value) {
	this->value = value;
}

JsonBoolean::~JsonBoolean() {}

bool JsonBoolean::GetValue() {
	return value;
}

bool JsonBoolean::IsBoolean() {
	return true;
}

JsonNull::JsonNull() {}

JsonNull::~JsonNull() {}

bool JsonNull::IsNull() {
	return true;
}

JsonNumber::JsonNumber(int value) {
	iValue = value;
	isIValue = true;
	isDValue = false;
	hasOriginal = false;
}

JsonNumber::JsonNumber(double value) {
	dValue = value;
	isDValue = true;
	isIValue = false;
	hasOriginal = false;
}

JsonNumber::~JsonNumber() {

}

bool JsonNumber::IsNumber() {
	return true;
}

int JsonNumber::IntValue() {
	if (isIValue) {
		return iValue;
	} else {
		return (int)dValue;
	}
}

double JsonNumber::RealValue() {
	if (isDValue) {
		return dValue;
	} else {
		return (double)iValue;
	}
}

JsonNumber* JsonNumber::FromString(String& value) {
	if (value.Contains("e") || value.Contains("E") || value.Contains(".")) {
		const char* raw = value.GetRaw();
		char* end = nullptr;
		double d = strtod(raw, &end);
		if (end != raw) {
			JsonNumber* num = new JsonNumber(d);
			num->original.Import(value);
			num->hasOriginal = true;
			return num;
		}
		JsonValue::OnJsonError("Failed to parse real");
		return nullptr;
	} else {
		const char* raw = value.GetRaw();
		int d = atoi(raw);
		JsonNumber *num = new JsonNumber(d);
		num->original.Import(value);
		num->hasOriginal = true;
		return num;
	}
}

JsonString::JsonString(String& contents) {
	this->contents.Import(contents);
}

JsonString::JsonString(const char* contents) {
	this->contents = contents;
}

JsonString::~JsonString() {

}

const char* JsonString::GetString() {
	return contents.GetRaw();
}

bool JsonString::IsString() {
	return true;
}

JsonArray::JsonArray() {
	static_assert(sizeof(buff) >= sizeof(std::vector<JsonValue*>), "Buffer too small");
	memset(buff, 0, sizeof(buff));
	std::vector<JsonValue*>* vect = new (buff) std::vector<JsonValue*>();
}

JsonArray::~JsonArray() {
	for (unsigned int i = 0; i < GetSize(); i++) {
		JsonValue* val = GetValueAt(i);
		delete val;
	}
	std::vector<JsonValue*>* vect = (std::vector<JsonValue*>*)buff;
	vect->~vector();
	memset(buff, 0, sizeof(buff));
}

void JsonArray::Add(JsonValue* value) {
	std::vector<JsonValue*>* vect = (std::vector<JsonValue*>*)buff;
	vect->push_back(value);
}

unsigned int JsonArray::GetSize() {
	std::vector<JsonValue*>* vect = (std::vector<JsonValue*>*)buff;
	return (unsigned int)vect->size();
}

JsonValue* JsonArray::GetValueAt(unsigned int idx) {
	std::vector<JsonValue*>* vect = (std::vector<JsonValue*>*)buff;
	return vect->at(idx);
}

bool JsonArray::IsArray() {
	return true;
}

JsonObject::JsonObject() {
	static_assert(sizeof(buff) >= sizeof(std::map<const char*, JsonValue*>), "Buffer too small");
	memset(buff, 0, sizeof(buff));
	std::map<std::string, JsonValue*>* map = new (buff) std::map<std::string, JsonValue*>();
}

JsonObject::~JsonObject() {
	for (unsigned int i = 0; i < GetSize(); i++) {
		JsonValue* val = GetValueAt(i);
		delete val;
	}
	std::map<std::string, JsonValue*>* map = (std::map<std::string, JsonValue*>*)buff;
	map->~map();
	memset(buff, 0, sizeof(buff));
}

unsigned int JsonObject::GetSize() {
	std::map<std::string, JsonValue*>* map = (std::map<std::string, JsonValue*>*)buff;
	return (unsigned int)map->size();
}

JsonValue* JsonObject::GetValueAt(unsigned int idx) {
	std::map<std::string, JsonValue*>* map = (std::map<std::string, JsonValue*>*)buff;
	auto it = map->begin();
	std::advance(it, idx);
	return it->second;
}

JsonValue* JsonObject::GetValue(const char* key) {
	std::map<std::string, JsonValue*>* map = (std::map<std::string, JsonValue*>*)buff;
	std::string search(key);
	auto it = map->find(search);
	if (it == map->end()) {
		return nullptr;
	}
	return it->second;
}

JsonString* JsonObject::GetString(const char* key) {
	JsonValue* val = GetValue(key);
	if (val != nullptr) {
		if (val->IsString()) {
			return val->AsString();
		}
	}
	JsonValue::OnJsonError("Missing key");
	return nullptr;
}

JsonBoolean* JsonObject::GetBoolean(const char* key) {
	JsonValue* val = GetValue(key);
	if (val != nullptr) {
		if (val->IsBoolean()) {
			return val->AsBoolean();
		}
	}
	JsonValue::OnJsonError("Missing key");
	return nullptr;
}

JsonNull* JsonObject::GetNull(const char* key) {
	JsonValue* val = GetValue(key);
	if (val != nullptr) {
		if (val->IsNull()) {
			return val->AsNull();
		}
	}
	JsonValue::OnJsonError("Missing key");
	return nullptr;
}

JsonNumber* JsonObject::GetNumber(const char* key) {
	JsonValue* val = GetValue(key);
	if (val != nullptr) {
		if (val->IsNumber()) {
			return val->AsNumber();
		}
	}
	JsonValue::OnJsonError("Missing key");
	return nullptr;
}

JsonArray* JsonObject::GetArray(const char* key) {
	JsonValue* val = GetValue(key);
	if (val != nullptr) {
		if (val->AsArray()) {
			return val->AsArray();
		}
	}
	JsonValue::OnJsonError("Missing key");
	return nullptr;
}

JsonObject* JsonObject::GetObject(const char* key) {
	JsonValue* val = GetValue(key);
	if (val != nullptr) {
		if (val->IsObject()) {
			return val->AsObject();
		}
	}
	JsonValue::OnJsonError("Missing key");
	return nullptr;
}

const char* JsonObject::GetRawString(const char* key) {
	JsonString* val = GetString(key);
	if (val != nullptr) {
		return val->GetString();
	}
	JsonValue::OnJsonError("Missing key");
	return nullptr;
}

bool JsonObject::GetRawBoolean(const char* key, bool& error) {
	JsonBoolean* val = GetBoolean(key);
	if (val != nullptr) {
		return val->GetValue();
	}
	JsonValue::OnJsonError("Missing key");
	error = true;
	return false;
}

int JsonObject::GetRawInt(const char* key, bool& error) {
	JsonNumber* val = GetNumber(key);
	if (val != nullptr) {
		return val->IntValue();
	}
	JsonValue::OnJsonError("Missing key");
	error = true;
	return 0;
}

double JsonObject::GetRawReal(const char* key, bool& error) {
	JsonNumber* val = GetNumber(key);
	if (val != nullptr) {
		return val->RealValue();
	}
	JsonValue::OnJsonError("Missing key");
	error = true;
	return 0;
}

void JsonObject::Put(const char* key, int value) {
	Put(key, new JsonNumber(value));
}

void JsonObject::Put(const char* key, double value) {
	Put(key, new JsonNumber(value));
}

void JsonObject::PutNull(const char* key) {
	Put(key, new JsonNull());
}

void JsonObject::Put(const char* key, bool value) {
	Put(key, new JsonBoolean(value));
}

void JsonObject::Put(const char* key, const char* value) {
	Put(key, new JsonString(value));
}

void JsonObject::Put(const char* key, JsonValue* value) {
	std::map<std::string, JsonValue*>* map = (std::map<std::string, JsonValue*>*)buff;
	std::string search(key);
	map->insert_or_assign(search, value);
}

bool JsonObject::IsObject() {
	return true;
}
