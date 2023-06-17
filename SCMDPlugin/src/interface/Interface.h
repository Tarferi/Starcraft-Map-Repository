#pragma once
#include "../common/Common.h"

enum InterfaceEvents {
	ButtonClick=0
};

enum InterfaceObjects {
	RepositoryButtons=0
};

enum class InterfaceEventTypes {
	OpenMap=0
};

typedef struct InterfaceEvent {
	InterfaceEventTypes type;

	char* target = nullptr;

} InterfaceEvent;

class Interface {


	static_assert(sizeof(uint32) == 4, "Invalid data type for ID");

	typedef uint32(__stdcall *UIAction)(uint32 action, uint32 source, uint32 code, uint32 param, uint32 param2);

public:

	typedef void(__stdcall *PollEvent0)(uint32 type, int* eventID, int* param1, int* param2, int* param3, int* param4);
	
	friend void ExportPollerFun(PollEvent0* ptr);

public:

	Interface();

	~Interface();

	void OnBtnRepositoriesClicked();

	static InterfaceEvent* PollEvent(PollEvent0 PollEventFun);

	static void Dispose(PollEvent0 PollEventFun, InterfaceEvent* evt);

private:

	uint32 remote = 0;

	UIAction UIActionFun = nullptr;
	PollEvent0 PollEventFun = nullptr;

	unsigned char buffer[64];

};

