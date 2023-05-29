#include "SCMDPlugin.h"

HWND hMainWindow;
HINSTANCE hMainInstance;
HINSTANCE hPluginInstance;

AllocRam scmd2_malloc;
DeAllocRam scmd2_free; 
ReAllocRam scmd2_realloc;

// User-supplied code.
void Initialize(HWND hMainWindow, HINSTANCE hPluginInstance);
void Finalize();

BOOL APIENTRY DllMain( HANDLE hModule, 
                       DWORD  ul_reason_for_call, 
                       LPVOID lpReserved
					 )
{
	switch(ul_reason_for_call) {
	case DLL_PROCESS_ATTACH:
		hPluginInstance = (HINSTANCE)hModule;
		break;
	case DLL_PROCESS_DETACH:
		Finalize();
		break;
	}
	return true;
}




//	DO NOT EDIT THIS !
DWORD WINAPI GetPluginVersion(void)
{
	return PLUGINVERSION;
}


extern const char* PluginName; // User-defined plugin name


// Menu name specifier.
BOOL WINAPI PluginGetMenuString(DWORD Section, CHAR* MenuString, WORD StringLength)
{
	if(Section == 'GIRT')
	{
		if(StringLength < strlen(PluginName) + 1) return FALSE;
		strcpy_s(MenuString, 128, PluginName);
		return TRUE;
	}
	return FALSE;
}



BOOL WINAPI InitPlugin(	HWND MainWindow, 
						HINSTANCE MainInstance, 
						AllocRam AllocMem, 
						DeAllocRam DeleteMem, 
						ReAllocRam ResizeMem, 
						DWORD* RequestedSections	)	//	DWORD[8]
{
	hMainWindow = MainWindow;
	hMainInstance = MainInstance;

	scmd2_malloc = AllocMem;
	scmd2_free = DeleteMem;
	scmd2_realloc = ResizeMem;

	Initialize(hMainWindow, hPluginInstance);

	// For only trigger editor.
	RequestedSections[0] = 'GIRT';
	return true;
}

