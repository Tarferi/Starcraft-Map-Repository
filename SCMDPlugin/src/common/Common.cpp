#include "Common.h"
#include <stdio.h>
#include <Windows.h>
#include <Shlobj.h>

void Error(const char* message) {
	MessageBoxA(NULL, message, "Starcraft Map Repository", MB_ICONERROR);
}

void Info(const char* message) {
	MessageBoxA(NULL, message, "Starcraft Map Repository", MB_ICONERROR);
}

bool WriteFile(const char* path, uint8* data, uint32 dataLength) {
	FILE* f = nullptr;
	if (!fopen_s(&f, path, "wb")) {
		uint32 written = 0;
		while (written != dataLength) {
			int32 writtenNow = (int32)fwrite(&(data[written]), 1, dataLength - written, f);
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

#pragma push_macro("DeleteFile")
#ifdef DeleteFile
#undef DeleteFile
#endif
bool DeleteFile(const char* path) {
	return DeleteFileA(path);
}
#pragma pop_macro("DeleteFile")

#pragma push_macro("CreateDirectory")
#ifdef CreateDirectory
#undef CreateDirectory
#endif
bool CreateDirectory(const char* path) {
	return CreateDirectoryA(path, NULL);
}
#pragma pop_macro("CreateDirectory")

#pragma push_macro("LoadLibrary")
#ifdef LoadLibrary
#undef LoadLibrary
#endif
Library LoadLibrary(const char* path) {
	return LoadLibraryA(path);
}
#pragma pop_macro("LoadLibrary")

void FreeLibrary(Library lib) {
	HMODULE modl = (HMODULE)lib;
	FreeLibrary(modl);
}

void* GetProcAddress(Library lib, const char* fun) {
	HMODULE modl = (HMODULE)lib;
	return GetProcAddress(modl, (LPCSTR)fun);
}

char path[MAX_PATH];

char* GetCurrentPath() {
	HMODULE hm = NULL;

	if (GetModuleHandleEx(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT, (LPCWSTR)&DirectoryExists, &hm) == 0) {
		Error("Failed to detect current directory");
		return nullptr;
	}
	if (GetModuleFileNameA(hm, path, sizeof(path)) == 0) {
		Error("Failed to detect current directory");
		return nullptr;
	}

	int len = (int)strlen(path);
	for (int i = len - 1, cnt = 2; i >= 0; i--) {
		if (path[i] == '\\' || path[i] == '/') {
			path[i] = 0;
			cnt--;
			if (cnt == 0) {
				break;
			}
		}
	}

	return path;
}

bool OpenMap(void* hwnd_wnd, const char* map) {
	HWND mainWindow = (HWND)hwnd_wnd;

	POINT point;
	point.x = 5;
	point.y = 5;

	HGLOBAL hMem = GlobalAlloc(GHND, sizeof(DROPFILES) + strlen(map) + 2);

	DROPFILES* dfiles = (DROPFILES*)GlobalLock(hMem);
	if (!dfiles) {
		GlobalFree(hMem);
		return false;
	}

	dfiles->pFiles = sizeof(DROPFILES);
	dfiles->pt = point;
	dfiles->fNC = TRUE;
	dfiles->fWide = FALSE;
	memcpy(&dfiles[1], map, strlen(map));
	GlobalUnlock(hMem);

	if (!PostMessage(mainWindow, WM_DROPFILES, (WPARAM)hMem, 0)) {
		GlobalFree(hMem);
		return false;
	}

	return true;
}