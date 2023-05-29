#pragma once
#include "types.h"

enum InterfaceEvents {
	ButtonClick=0
};

enum InterfaceObjects {
	RepositoryButtons=0
};

class Interface {

	static_assert(sizeof(uint32) == 4, "Invalid data type for ID");

	typedef uint32(__stdcall *UIAction)(uint32 action, uint32 source, uint32 code, uint32 param, uint32 param2);
public:

	Interface();

	~Interface();

	void OnBtnRepositoriesClicked();

private:

	uint32 remote = 0;

	UIAction UIActionFun = nullptr;

	unsigned char buffer[64];

};

