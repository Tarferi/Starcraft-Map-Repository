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
#define WORK_DIR "./Map Repository"
#define WORK_DIR_LIB WORK_DIR "/data1.db"
#endif

Interface::Interface() {
	static_assert(sizeof(struct InterfaceData) <= sizeof(buffer), "Buffer too small");
	struct InterfaceData* data = reinterpret_cast<struct InterfaceData*>(buffer);
	memset(data, 0, sizeof(struct InterfaceData));
#ifdef INCLUDE_BINARY_LIB
	if (!DirectoryExists(WORK_DIR)) {
		CreateDirectory(WORK_DIR);
	}
	if (!DirectoryExists(WORK_DIR)) {
		Error("Failed to create working directory");
		return;
	}

	if (FileExists(WORK_DIR_LIB)) {
		DeleteFile(WORK_DIR_LIB);
		WriteFile(WORK_DIR_LIB, (uint8*)guilib, guilib_size);
	} else if (!WriteFile(WORK_DIR_LIB, (uint8*)guilib, guilib_size)) {
		Error("Failed to write main library");
		return;
	}

	data->library = LoadLibrary(WORK_DIR_LIB);
	if (data->library) {
		UIActionFun = (UIAction)GetProcAddress(data->library, "UIAction");
		if (!UIActionFun) {
			UIActionFun = nullptr;
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
