#include "Common.h"
#include "Interface.h"
#include <stdio.h>
#include <Windows.h>

#define INCLUDE_BINARY

#ifdef INCLUDE_BINARY
#include "bin.h"
#endif

bool WriteFile(const char* path, uint8* data, uint32 dataLength) {
	FILE* f = nullptr;
	if (!fopen_s(&f, path, "wb")) {
		uint32 written = 0;
		while (written != dataLength) {
			int32 writtenNow = fwrite(&(data[written]), 1, dataLength - written, f);
			if (writtenNow <= 0) {
				fclose(f);
				return false;
			} else {
				written += writtenNow;
			}
		}
		fclose(f);
		return true;
	}
	return false;
}

bool DirectoryExists(const char* path) {
	DWORD dwAttrib = GetFileAttributesA(path);

	return (dwAttrib != INVALID_FILE_ATTRIBUTES &&
		(dwAttrib & FILE_ATTRIBUTE_DIRECTORY));
}

bool FileExists(const char* path) {
	DWORD dwAttrib = GetFileAttributesA(path);

	return (dwAttrib != INVALID_FILE_ATTRIBUTES &&
		!(dwAttrib & FILE_ATTRIBUTE_DIRECTORY));
}

bool CreateDirectory0(const char* path) {
	return CreateDirectoryA(path, NULL);
}

struct InterfaceData {
	HMODULE library;
};

#ifdef INCLUDE_BINARY
#define WORK_DIR "./Map Repository"
#define WORK_DIR_LIB WORK_DIR "/data1.db"
#endif

Interface::Interface() {
	static_assert(sizeof(struct InterfaceData) <= sizeof(buffer), "Buffer too small");
	struct InterfaceData* data = reinterpret_cast<struct InterfaceData*>(buffer);
	memset(data, 0, sizeof(struct InterfaceData));
#ifdef INCLUDE_BINARY
	if (!DirectoryExists(WORK_DIR)) {
		CreateDirectory0(WORK_DIR);
	}
	if (!DirectoryExists(WORK_DIR)) {
		Error("Failed to create working directory");
		return;
	}

	if (FileExists(WORK_DIR_LIB)) {
		DeleteFileA(WORK_DIR_LIB);
		WriteFile(WORK_DIR_LIB, (uint8*)guilib, guilib_size);
	} else if (!WriteFile(WORK_DIR_LIB, (uint8*)guilib, guilib_size)) {
		Error("Failed to write main library");
		return;
	}

	data->library = LoadLibraryA(WORK_DIR_LIB);
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