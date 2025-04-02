#pragma once

#include <Windows.h>
#include <detours.h>

using namespace System;

namespace CLRDetourWrapper {
	public ref class DetourWrapper
	{
		// TODO: Add your methods for this class here.
    public:
        DetourWrapper(System::Threading::AsyncLocal<System::String^>^ asyncLocal);
        static System::Threading::AsyncLocal<int>^ asyncLocalInt;
        static System::Threading::AsyncLocal<System::String^>^ asyncLocalString;
	};
}
