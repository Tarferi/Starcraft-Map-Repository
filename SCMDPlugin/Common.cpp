#include "Common.h"
#include <Windows.h>

void Error(const char* message) {
	MessageBoxA(NULL, message, "Starcraft Map Repository", MB_ICONERROR);
}

void Info(const char* message) {
	MessageBoxA(NULL, message, "Starcraft Map Repository", MB_ICONERROR);
}