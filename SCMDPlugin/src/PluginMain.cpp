// Basic interface I/O for plugin.
#include "base/SCMDPlugin.h"
#include "common/Common.h"
#include "common/resource.h"
#include "interface/Interface.h"

#include <stdio.h>
#include <CommCtrl.h>

#pragma comment(lib, "Comctl32.lib")

const char* PluginName = "Starcraft Map Repository"; // Plugin menu name

template<typename callable>
void ForEachChild(HWND parent, callable cb) {
	struct tmp {
		callable* cb;
	} tmpI{ &cb};
	EnumChildWindows(parent, [](HWND wnd, LPARAM lp) -> BOOL {
		struct tmp* tmpI = reinterpret_cast<struct tmp*>(lp);
		(*tmpI->cb)(wnd);
		return TRUE;
	}, (LPARAM)&tmpI);
}

static HWND FindNthChildOfClass(HWND wnd, const char* className, int childIdx) {
	int idx = 0;
	HWND c = NULL;
	ForEachChild(wnd, [&](HWND child) {
		char buffer[256];
		RealGetWindowClassA(child, buffer, sizeof(buffer) - 1);
		if (!strcmp(buffer, className)) {
			if (childIdx == idx) {
				c = child;
			}
		}
		idx++;
	});
	return c;
}

bool GetChildrenRect(HWND parent, RECT* rr) {
	RECT r;
	bool hasLeft = false;
	bool hasRight = false;
	bool hasTop = false;
	bool hasBottom = false;
	r.left = -1;
	r.right = -1;
	r.top = -1;
	r.bottom = -1;
	ForEachChild(parent, [&](HWND child) {
		RECT cr;
		GetWindowRect(child, &cr);
		if (cr.left < r.left || !hasLeft) {
			r.left = cr.left;
			hasLeft = true;
		}
		if (cr.top < r.top || !hasTop) {
			r.top = cr.top;
			hasTop = true;
		}
		if (cr.right > r.right || !hasRight) {
			r.right = cr.right;
			hasRight = true;
		}
		if (cr.bottom > r.bottom || !hasBottom) {
			r.bottom = cr.bottom;
			hasBottom = true;
		}
	});
	if (!hasLeft || !hasTop || !hasRight || !hasBottom) {
		return false;
	}
	rr->left = r.left;
	rr->top = r.top;
	rr->right = r.right;
	rr->bottom = r.bottom;
	return true;
}

#define IDM_OPEN_REPO 13105

struct GlobalDataT {
	HHOOK wndHook;
	HWND btnRepositories;
	HWND mainWindow;
	Interface* ifc;
} GlobalData;

void BtnOpenRepositoryClicked() {
	GlobalData.ifc->OnBtnRepositoriesClicked();
}

LRESULT CALLBACK wndHook(int nCode, WPARAM wParam, LPARAM lParam) {
	if (nCode >= 0) {
		CWPSTRUCT* cs = reinterpret_cast<CWPSTRUCT*>(lParam);
		switch (cs->message) {
		case WM_SETCURSOR:
		case WM_NCHITTEST:
			break;

		case WM_COMMAND:
		{
			if (cs->wParam == IDM_OPEN_REPO && cs->hwnd == GlobalData.mainWindow) {
				BtnOpenRepositoryClicked();
			}
			break;
		}
		}
	}
	return CallNextHookEx(GlobalData.wndHook, nCode, wParam, lParam);
};

int GetMenuIdx(HMENU menu, const char* text) {
	char tmp[1024];
	int cnt = GetMenuItemCount(menu);
	sprintf_s(tmp, "Found %d menu items", cnt);
	Info(tmp);
	for (int i = 0; i < cnt; i++) {
		sprintf_s(tmp, "Reading menu item %d for %d", i, (int)(menu));
		Info(tmp);
		int chars = GetMenuStringA(menu, i, tmp, sizeof(tmp) - 1, MF_BYPOSITION);
		if (chars > 0) {
			if (!strcmp((LPSTR)chars, text)) {
				return i;
			}
		}
	}
	return -1;
}

// This function is called when the plugin is being initialized.
void Initialize(HWND hMainWindow, HINSTANCE hPluginInstance) {
	/*
	Info("Part 1");
	HMENU menu = GetMenu(hMainWindow);
	Info("Part 2");
	if (!menu) {
		Error("Menu not found");
		return;
	}
	int idxHelp = GetMenuIdx(menu, "Help");
	if (idxHelp < 0) {
		Error("Menu item \"Help\" not found");
		return;
	}
	Info("Part 3");
	*/

	HWND rebar = FindNthChildOfClass(hMainWindow, "ReBarWindow32", 0);
	if (!rebar) {
		return;
	}
	HWND fileOps = FindNthChildOfClass(hMainWindow, "ToolbarWindow32", 0);

	RECT r;
	if (!GetChildrenRect(rebar, &r)) {
		return;
	}

	int width = 300;
	/*
	int x = r.right + 10;
	int y = r.top;
	int w = width;
	int h = r.bottom - r.top;
	*/

	int x = 900;
	int y = 1;
	int w = 34;
	int h = 26;

	HINSTANCE hInstance = (HINSTANCE)GetWindowLongPtr(hMainWindow, GWLP_HINSTANCE);
	HWND tb = CreateWindowEx(0, WC_BUTTON, L" ", WS_CHILD | WS_VISIBLE | BS_FLAT | BS_BITMAP, x, y, w, h, rebar, (HMENU)IDM_OPEN_REPO, hInstance, NULL);
	if (!tb) {
		return;
	}

	HMODULE plugInst = GetModuleHandle(NULL);

	HICON icon = NULL;
	LoadIconWithScaleDown(hPluginInstance, MAKEINTRESOURCE(IDI_ICON1), 24, 24, &icon);
	if (icon) {
		SendMessage(tb, BM_SETIMAGE, IMAGE_ICON, (LPARAM)icon);
	}
	
	SetParent(tb, rebar);
	MoveWindow(tb, x, y, w, h, true);
	ShowWindow(tb, SW_SHOW);
	
	GlobalData.btnRepositories = tb;
	GlobalData.mainWindow = hMainWindow;
	GlobalData.wndHook = SetWindowsHookEx(WH_CALLWNDPROC, wndHook, plugInst, GetCurrentThreadId());
	GlobalData.ifc = new Interface();

}

// This function is called when the DLL is unloaded.
void Finalize() {

}

// This code is run when the menu is pressed.
BOOL WINAPI RunPlugin(	TEngineData*	EngineData,		//	Struct containing engine data
						DWORD CurSection,				//	Section plugin is being run for (Currently either triggers or mission briefing)
						CChunkData*	Triggers,			//	Pointer to trigger datachunk
						CChunkData*	MissionBriefing,	//	Pointer to mission briefing datachunk
						CChunkData*	SwitchRenaming,		//	Pointer to switch renaming datachunk
						CChunkData*	UnitProperties,		//	Pointer to unit properties datachunk
						CChunkData*	UnitPropUsage	)	//	Pointer to unit property usage datachunk
{
	// If any of required chunks are not present, then just return.
	if ((Triggers == NULL) || (MissionBriefing == NULL) || (SwitchRenaming == NULL) || (UnitProperties == NULL) || (UnitPropUsage == NULL)) {
		return FALSE; // Plugin process failed.
	}

	return TRUE;
}