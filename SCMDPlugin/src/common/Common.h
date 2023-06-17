#pragma once
#include "types.h"

void Error(const char* message);

void Info(const char* message);

bool WriteFile(const char* path, uint8* data, uint32 dataLength);

bool DirectoryExists(const char* path);

bool FileExists(const char* path);

bool DeleteFile(const char* path);

bool CreateDirectory(const char* path);

using Library = void*;

Library LoadLibrary(const char* path);

void FreeLibrary(Library lib);

void* GetProcAddress(Library lib, const char* fun);