#pragma once

#include "../common/Common.h"

typedef struct {
	int32 code;
	
	int32 param2;
	int32 param3;
	int32 param4;
} NetCommMessage;

class NetComm {

protected:
	
	NetComm();

public:

	virtual ~NetComm();

	virtual bool Valid();

	void RunConnector(const char* exePath);

	virtual void OnMessage(NetCommMessage* message) = 0;

	bool SendMessage(NetCommMessage* message);

private:

	bool valid = false;

};

