#include "NativeInterop.h"
#include <Windows.h>

//#define SINGLE_WINDOW

#ifdef SINGLE_WINDOW

typedef HWND(*CreateUserInterfaceFunc)();
typedef void(*DisplayUserInterfaceFunc)(void);
typedef void(*DestroyUserInterfaceFunc)(void);

struct NativeInteropData {
	WNDCLASSEX HostWindowClass; 
	MSG loop_message; 
	HWND cpphwin_hwnd;
	HWND wpf_hwnd; 
	HINSTANCE hInstance;
	CreateUserInterfaceFunc CreateUserInterface;
	DisplayUserInterfaceFunc DisplayUserInterface;
	DestroyUserInterfaceFunc DestroyUserInterface;
	HMODULE dotNetGUILibrary;
	RECT hwin_rect;
	bool isHWindowRunning = false;
};

struct NativeInteropData StaticData;

LRESULT CALLBACK HostWindowProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam) {
	switch (msg) {
	case WM_CLOSE:
		StaticData.DestroyUserInterface(); //// Destroy WPF Control before Destorying Host Window
		DestroyWindow(hwnd);
		break;
	case WM_DESTROY:
		StaticData.isHWindowRunning = false;
		break;
	case WM_SIZE: //// Resize WPF Control on Host Window Resizing
		if (StaticData.wpf_hwnd != nullptr) {
			GetClientRect(StaticData.cpphwin_hwnd, &StaticData.hwin_rect);
			MoveWindow(StaticData.wpf_hwnd, 0, 0, StaticData.hwin_rect.right - StaticData.hwin_rect.left, StaticData.hwin_rect.bottom - StaticData.hwin_rect.top, TRUE);
		}
		break;
	default:
		return DefWindowProc(hwnd, msg, wParam, lParam);
	}
	return 0;
}

NativeInterop::NativeInterop() {
	static_assert(sizeof(struct NativeInteropData) <= sizeof(buffer), "Buffer too small");
	//struct NativeInteropData* data = reinterpret_cast<struct NativeInteropData*>(buffer);
	struct NativeInteropData* data = &StaticData;
	memset(data, 0, sizeof(struct NativeInteropData));
	data->hInstance = GetModuleHandle(NULL);
}

void NativeInterop::RunWindow() {
	struct NativeInteropData* data = reinterpret_cast<struct NativeInteropData*>(buffer);

	const wchar_t cpphwinCN[] = L"CppMAppHostWinClass";
	data->HostWindowClass.cbSize = sizeof(WNDCLASSEX); 
	data->HostWindowClass.lpfnWndProc = HostWindowProc;
	data->HostWindowClass.hCursor = LoadCursor(NULL, IDC_ARROW);
	data->HostWindowClass.cbClsExtra = 0; 
	data->HostWindowClass.style = 0;
	data->HostWindowClass.cbWndExtra = 0;
	data->HostWindowClass.hInstance = data->hInstance;
	//HostWindowClass.hIcon = app_icon; HostWindowClass.hIconSm = app_icon;
	data->HostWindowClass.lpszClassName = cpphwinCN;
	data->HostWindowClass.lpszMenuName = NULL;

	//// Register Window
	if (!RegisterClassEx(&data->HostWindowClass)) {
		return;
	}

	/// Creating Unmanaged Host Window
	data->cpphwin_hwnd = CreateWindowEx(
		WS_EX_CLIENTEDGE,
		cpphwinCN,
		L"",
		WS_THICKFRAME | WS_OVERLAPPEDWINDOW,
		CW_USEDEFAULT, CW_USEDEFAULT, 800, 715,
		NULL, NULL, data->hInstance, NULL);

	/// Check if How Window is valid
	if (data->cpphwin_hwnd == NULL) {
		return;
	}

	/// Making Window Fixed Size
	//if (FIXED_WINDOW) {
	//	::SetWindowLong(data->cpphwin_hwnd, GWL_STYLE, GetWindowLong(cpphwin_hwnd, GWL_STYLE) & ~WS_SIZEBOX);
	//}

	/// Centering Host Window
	RECT window_r; RECT desktop_r;
	GetWindowRect(data->cpphwin_hwnd, &window_r); GetWindowRect(GetDesktopWindow(), &desktop_r);
	int xPos = (desktop_r.right - (window_r.right - window_r.left)) / 2;
	int yPos = (desktop_r.bottom - (window_r.bottom - window_r.top)) / 2;

	/// Set Window Position
	::SetWindowPos(data->cpphwin_hwnd, 0, xPos, yPos, 0, 0, SWP_NOZORDER | SWP_NOSIZE);

	/// Loading dotNet UI Library
	data->dotNetGUILibrary = LoadLibrary(L"GUILib.dll");
	data->CreateUserInterface = (CreateUserInterfaceFunc)GetProcAddress(data->dotNetGUILibrary, "CreateUserInterface");
	data->DisplayUserInterface = (DisplayUserInterfaceFunc)GetProcAddress(data->dotNetGUILibrary, "DisplayUserInterface");
	data->DestroyUserInterface = (DestroyUserInterfaceFunc)GetProcAddress(data->dotNetGUILibrary, "DestroyUserInterface");

	/// Creating .Net GUI
	data->wpf_hwnd = data->CreateUserInterface();
	//	(LZ4_Compress_File_Ptr)&LZ4_Compress_File, (LZ4_Decompress_File_Ptr)&LZ4_Decompress_File);

	/// Set Thread to STA
	CoInitializeEx(NULL, COINIT_APARTMENTTHREADED);


	/// Check if WPF Window is valid
	if (data->wpf_hwnd != nullptr) {

		/// Disable Host Window Updates & Draws
		SendMessage(data->cpphwin_hwnd, WM_SETREDRAW, FALSE, 0);

		/// Disable Host Window Double Buffering
		long dwExStyle = GetWindowLong(data->cpphwin_hwnd, GWL_EXSTYLE);
		dwExStyle &= ~WS_EX_COMPOSITED;
		SetWindowLong(data->cpphwin_hwnd, GWL_EXSTYLE, dwExStyle);

		/// Set WPF Window to a Child Control
		SetWindowLong(data->wpf_hwnd, GWL_STYLE, WS_CHILD);

		/// Get your host client area rect
		GetClientRect(data->cpphwin_hwnd, &data->hwin_rect);

		/// Set WPF Control Order , Size and Position
		MoveWindow(data->wpf_hwnd, 0, 0, data->hwin_rect.right - data->hwin_rect.left, data->hwin_rect.bottom - data->hwin_rect.top, TRUE);
		SetWindowPos(data->wpf_hwnd, HWND_TOP, 0, 0, data->hwin_rect.right - data->hwin_rect.left, data->hwin_rect.bottom - data->hwin_rect.top, SWP_NOMOVE);

		/// Set WPF as A Child to Host Window...
		SetParent(data->wpf_hwnd, data->cpphwin_hwnd);

		/// Skadoosh!
		ShowWindow(data->wpf_hwnd, SW_RESTORE);

		/// Display WPF Control by Reseting its Opacity
		data->DisplayUserInterface();
	}


	/// Display Window
	ShowWindow(data->cpphwin_hwnd, SW_SHOW);
	UpdateWindow(data->cpphwin_hwnd);
	BringWindowToTop(data->cpphwin_hwnd);
	data->isHWindowRunning = true;


	/// Adding Message Loop
	while (GetMessage(&data->loop_message, NULL, 0, 0) > 0 && data->isHWindowRunning) {
		TranslateMessage(&data->loop_message);
		DispatchMessage(&data->loop_message);
	}
	FreeLibrary(data->dotNetGUILibrary);
}


#else

typedef void*(*CreateInterop)();
typedef void(*DestroyInterop)(void*);

struct NativeInteropData {
	HMODULE dotNetGUILibrary;
	CreateInterop CreateInteropFun;
	DestroyInterop DestroyInteropFun;
	void* Interop;
};


NativeInterop::NativeInterop() {
	static_assert(sizeof(struct NativeInteropData) <= sizeof(buffer), "Buffer too small");
	struct NativeInteropData* data = reinterpret_cast<struct NativeInteropData*>(buffer);
	memset(data, 0, sizeof(struct NativeInteropData));
	
	data->dotNetGUILibrary = LoadLibrary(L"GUILib.dll");
	if (data->dotNetGUILibrary) {
		data->CreateInteropFun = (CreateInterop)GetProcAddress(data->dotNetGUILibrary, "CreateInterop");
		data->DestroyInteropFun = (DestroyInterop)GetProcAddress(data->dotNetGUILibrary, "DestroyInterop");
	}
}

void NativeInterop::RunWindow() {
	struct NativeInteropData* data = reinterpret_cast<struct NativeInteropData*>(buffer);
	if (data->CreateInteropFun) {
		data->Interop = data->CreateInteropFun();
	}
	if (data->Interop && data->DestroyInteropFun) {
		data->DestroyInteropFun(data->Interop);
	}
	data->Interop = nullptr;
}

#endif