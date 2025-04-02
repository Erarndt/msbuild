#include "pch.h"

#include "NetCLRDetourWrapper.h"

static DWORD(WINAPI* TrueGetCurrentDirectoryW)(_In_ DWORD nBufferLength, _Out_writes_to_opt_(nBufferLength, return +1) LPWSTR lpBuffer) = GetCurrentDirectoryW;

static DWORD(WINAPI * TrueGetFullPathNameW)(_In_ LPCWSTR lpFileName, _In_ DWORD nBufferLength, _Out_writes_to_opt_(nBufferLength, return +1) LPWSTR lpBuffer, _Outptr_opt_ LPWSTR* lpFilePart) = GetFullPathNameW;


DWORD WINAPI GetCurrentDirectoryDetour(DWORD nBufferLength, LPWSTR lpBuffer)
{
    System::String^ localString = NetCLRDetourWrapper::DetourWrapper::asyncLocalString->Value;
    if (localString == nullptr) {
        return TrueGetCurrentDirectoryW(nBufferLength, lpBuffer);
    }

    int i;
    for (i = 0; i < localString->Length; ++i) {
        lpBuffer[i] = localString[i];
    }

    lpBuffer[i] = '\0';

    return i;
}

DWORD WINAPI GetFullPathNameDetour(_In_ LPCWSTR lpFileName, _In_ DWORD nBufferLength, _Out_writes_to_opt_(nBufferLength, return +1) LPWSTR lpBuffer, _Outptr_opt_ LPWSTR* lpFilePart)
{
    System::String^ localString = NetCLRDetourWrapper::DetourWrapper::asyncLocalString->Value;
    if (localString == nullptr) {
        return TrueGetFullPathNameW(lpFileName, nBufferLength, lpBuffer, lpFilePart);
    }

    int i;
    for (i = 0; i < localString->Length; ++i) {
        lpBuffer[i] = localString[i];
    }

    for (int j = 0; lpFileName[j] != '\0'; ++j)
    {
        lpBuffer[i] = lpFileName[j];
        ++i;
    }

    lpBuffer[i] = '\0';

    return i;
}

NetCLRDetourWrapper::DetourWrapper::DetourWrapper(System::Threading::AsyncLocal<System::String^>^ asyncLocal) {
    asyncLocalString = asyncLocal;

    DetourRestoreAfterWith();

    DetourTransactionBegin();
    DetourUpdateThread(GetCurrentThread());
    DetourAttach(&(PVOID&)TrueGetCurrentDirectoryW, GetCurrentDirectoryDetour);
    DetourAttach(&(PVOID&)TrueGetFullPathNameW, GetFullPathNameDetour);
    DetourTransactionCommit();
}
