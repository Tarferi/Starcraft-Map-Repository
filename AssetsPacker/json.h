#pragma once

class JsonString;
class JsonObject;
class JsonNull;
class JsonBoolean;
class JsonArray;
class JsonNumber;

namespace JsonTokenT {

	enum class JsonTokenE {
	T_BeginArray,
	T_EndArray,
	T_BeginObject,
	T_EndObject,
	T_Delim,
	T_Separator,
	T_Number,
	T_String,
	T_True,
	T_False,
	T_Null,
	T_EndOfInput,
	T_INVALID
	};
}

typedef JsonTokenT::JsonTokenE JsonToken;

class String {

	String(const String&) = delete;
	
	String& operator=(const String&) = delete;

public:

	String& operator=(const char* text);
	
	String();

	~String();

	void Clear();

	void Import(String& another);

	void Append(const char* text);
	
	void Append(char text);

	bool Contains(const char* substr);

	const char* GetRaw();

private:
	unsigned char buff[64];

};

class JsonTokenizer {

public:
	
	JsonTokenizer(char* input, unsigned int length);

    bool GetNext(JsonToken& token, String& contents);

private:

	bool IsAtTheEnd();

	void SkipWhitespace();

	int FromHex(char c);

    bool ParseString(String& sb);

    bool ParseNumber(String& sb);

	bool GetCurrent(JsonToken& token, String& contents);

	bool UpdateCurrent();


	char* input = nullptr;
	unsigned int position = 0;
	unsigned int length = 0;
	JsonToken lastToken = JsonToken::T_INVALID;
	String lastContents;

	bool hasError = false;
};

class JsonValue {

	JsonValue(const JsonValue&) = delete;
	JsonValue(const JsonValue&&) = delete;
	JsonValue& operator=(const JsonValue&) = delete;

public:

    static void OnJsonError(const char* error);

    static JsonValue* Parse(char* jsn, unsigned int length);

    JsonValue();

    virtual ~JsonValue();

public:

	virtual bool IsBoolean();

	JsonBoolean* AsBoolean();
		
	virtual bool IsNull();

	JsonNull* AsNull();

	virtual bool IsNumber();

	JsonNumber* AsNumber();

	virtual bool IsString();

	JsonString* AsString();

	virtual bool IsObject();

	JsonObject* AsObject();

	virtual bool IsArray();

	JsonArray* AsArray();

    virtual bool IsValid();

private:

	static JsonArray* ParseArray(JsonTokenizer& tokens);

	static JsonObject* ParseObject(JsonTokenizer& tokens);

	static JsonValue* Parse(JsonTokenizer& tokens);

    static JsonValue* Parse(JsonTokenizer& tokens, JsonToken& token, String& contents);

};

class JsonBoolean : public JsonValue {
public:

	JsonBoolean(bool value);

	virtual ~JsonBoolean();

	bool GetValue();

	virtual bool IsBoolean() override;

private:
	bool value = false;
};

class JsonNull : public JsonValue {

public:

	JsonNull();

	virtual ~JsonNull();

	virtual bool IsNull() override;

};

class JsonNumber : public JsonValue  {

	friend class JsonValue;

public:

	JsonNumber(int value);
	
	JsonNumber(double value);
	
	int IntValue();

	double RealValue();

	virtual ~JsonNumber();

	virtual bool IsNumber() override;

private:

	static JsonNumber* FromString(String& value);

	int iValue = 0;
	bool isIValue = false;

	double dValue = 0;
	bool isDValue = false;

	String original;
	bool hasOriginal = true;
};

class JsonString : public JsonValue  {

	friend class JsonValue;

private:

	JsonString(String& contents);

public:

	JsonString(const char* contents);

	virtual ~JsonString();
	
	const char* GetString();

	virtual bool IsString() override;

private:

	String contents;
	
};

class JsonArray : public JsonValue  {

public:

	JsonArray();

	virtual ~JsonArray();

	void Add(JsonValue* value);

	unsigned int GetSize();

	JsonValue* GetValueAt(unsigned int idx);

	virtual bool IsArray() override;

private:

	char buff[64];
};

class JsonObject : public JsonValue {

public:
	
	JsonObject();

	virtual ~JsonObject();

	unsigned int GetSize();

	JsonValue* GetValueAt(unsigned int idx);

	JsonValue* GetValue(const char* key);

	JsonString* GetString(const char* key);

	JsonBoolean* GetBoolean(const char* key);

	JsonNull* GetNull(const char* key);

	JsonNumber* GetNumber(const char* key);

	JsonArray* GetArray(const char* key);

	JsonObject* GetObject(const char* key);

	const char* GetRawString(const char* key);

	bool GetRawBoolean(const char* key, bool& error);

	int GetRawInt(const char* key, bool& error);

	double GetRawReal(const char* key, bool& error);

	void Put(const char* key, int value);

	void Put(const char* key, double value);

	void PutNull(const char* key);

	void Put(const char* key, bool value);

	void Put(const char* key, const char* value);

	void Put(const char* key, JsonValue* value);

	virtual bool IsObject() override;

private:

	char buff[64];
};
