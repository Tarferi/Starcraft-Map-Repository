#include "Interface.h"
#include <string>

#define INCLUDE_BINARY_LIB
//#define INCLUDE_BINARY_EXE

#ifdef INCLUDE_BINARY_LIB
#include "../../bin.h"
#endif

#ifdef INCLUDE_BINARY_EXE
#include "../../bin_exe.h"
#endif

struct InterfaceData {
	Library library;
};

#ifdef INCLUDE_BINARY_LIB
#define WORK_DIR0 "Map Repository"
#define WORK_DIR_LIB0 WORK_DIR0 "\\data1.db"
#endif

Interface::Interface() {
	static_assert(sizeof(struct InterfaceData) <= sizeof(buffer), "Buffer too small");
	struct InterfaceData* data = reinterpret_cast<struct InterfaceData*>(buffer);
	memset(data, 0, sizeof(struct InterfaceData));

	char buffer[1024];

	auto LoadWorkDir = [&]() {
		char* path = GetCurrentPath();
		sprintf_s(buffer, "%s\\%s", path, WORK_DIR0);
		return buffer;
	};

	auto LoadWorkDirLib = [&]() {
		char* path = GetCurrentPath();
		sprintf_s(buffer, "%s\\%s", path, WORK_DIR_LIB0);
		return buffer;
	};

	char* WorkDir = nullptr;
	char* WorkDirLib = nullptr;
	
	WorkDir = LoadWorkDir();
	
#ifdef INCLUDE_BINARY_LIB
	if (!DirectoryExists(WorkDir)) {
		CreateDirectory(WorkDir);
	}
	if (!DirectoryExists(WorkDir)) {
		Error("Failed to create working directory");
		return;
	}

	WorkDirLib = LoadWorkDirLib();

	if (FileExists(WorkDirLib)) {
		DeleteFile(WorkDirLib);
		WriteFile(WorkDirLib, (uint8*)guilib, guilib_size);
	} else if (!WriteFile(WorkDirLib, (uint8*)guilib, guilib_size)) {
		Error("Failed to write main library");
		return;
	}

	data->library = LoadLibrary(WorkDirLib);
	if (data->library) {
		UIActionFun = (UIAction)GetProcAddress(data->library, "UIAction");
		if (!UIActionFun) {
			UIActionFun = nullptr;
			FreeLibrary(data->library);
			data->library = nullptr;
			Error("Failed to load main library contents");
			return;
		}

		PollEventFun = (PollEvent0)GetProcAddress(data->library, "PollEvent");
		if (!PollEventFun) {
			UIActionFun = nullptr;
			PollEventFun = nullptr;
			FreeLibrary(data->library);
			data->library = nullptr;
			Error("Failed to load main library contents");
			return;
		}
	} else {
		Error("Failed to load main library");
		return;
	}

#else
//#error TODO: LoadLibrary from debug folders
#endif

	remote = UIActionFun(0, 0, 0, 0, 0); // create
}

Interface::~Interface() {
	struct InterfaceData* data = reinterpret_cast<struct InterfaceData*>(buffer);
	if (data->library) {
		FreeLibrary(data->library);
		data->library = nullptr;
	}
}

void Interface::OnBtnRepositoriesClicked() {
	if (UIActionFun) {
		UIActionFun(1, remote, (uint32)InterfaceEvents::ButtonClick, (uint32)InterfaceObjects::RepositoryButtons, 0);
	} else {
		Error("UI Interface is not available");
	}
}

InterfaceEvent* Interface::PollEvent(PollEvent0 PollEventFun) {
	int evtID = 0;
	int x1 = 0;
	int x2 = 0;
	int x3 = 0;
	int x4 = 0;
	PollEventFun(0, &evtID, &x1, &x2, &x3, &x4);

	InterfaceEvent* evt = new InterfaceEvent();
	evt->type = (InterfaceEventTypes)evtID;
	switch (evt->type) {
	case InterfaceEventTypes::OpenMap:
		evt->target = (char*)x1;
		break;
	}
	return evt;
}

void Interface::Dispose(PollEvent0 PollEventFun, InterfaceEvent* evt) {
	int evtID = 0;
	int x1 = 0;
	int x2 = 0;
	int x3 = 0;
	int x4 = 0;

	evtID = (int)evt->type;
	switch (evt->type) {
	case InterfaceEventTypes::OpenMap:
		x1 = (int)evt->target;
		break;
	}
	PollEventFun(1, &evtID, &x1, &x2, &x3, &x4);
	delete evt;
}