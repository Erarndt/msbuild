#include "pch.h"

#include "DetourWrapper.h"
#include <PathCch.h>


static DWORD(WINAPI* TrueGetCurrentDirectoryW)(_In_ DWORD nBufferLength, _Out_writes_to_opt_(nBufferLength, return +1) LPWSTR lpBuffer) = GetCurrentDirectoryW;

static BOOL(WINAPI * TrueSetCurrentDirectoryW)(_In_ LPCWSTR lpPathName) = SetCurrentDirectoryW;

static DWORD(WINAPI * TrueGetFullPathNameW)(_In_ LPCWSTR lpFileName, _In_ DWORD nBufferLength, _Out_writes_to_opt_(nBufferLength, return +1) LPWSTR lpBuffer, _Outptr_opt_ LPWSTR* lpFilePart) = GetFullPathNameW;

static BOOL(WINAPI * TrueCreateProcessW)(_In_opt_ LPCWSTR lpApplicationName,
    _Inout_opt_ LPWSTR lpCommandLine,
    _In_opt_ LPSECURITY_ATTRIBUTES lpProcessAttributes,
    _In_opt_ LPSECURITY_ATTRIBUTES lpThreadAttributes,
    _In_ BOOL bInheritHandles,
    _In_ DWORD dwCreationFlags,
    _In_opt_ LPVOID lpEnvironment,
    _In_opt_ LPCWSTR lpCurrentDirectory,
    _In_ LPSTARTUPINFOW lpStartupInfo,
    _Out_ LPPROCESS_INFORMATION lpProcessInformation) = CreateProcessW;

DWORD WINAPI GetCurrentDirectoryDetour(DWORD nBufferLength, LPWSTR lpBuffer)
{
    System::String^ localString = CLRDetourWrapper::DetourWrapper::asyncLocalString->Value;
    if (localString == nullptr) {
        return TrueGetCurrentDirectoryW(nBufferLength, lpBuffer);
    }

    if (nBufferLength == 0 || lpBuffer == NULL)
    {
        return localString->Length + 1;
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
    System::String^ localString = CLRDetourWrapper::DetourWrapper::asyncLocalString->Value;

    if (localString == nullptr || lpFileName == NULL) {
        return TrueGetFullPathNameW(lpFileName, nBufferLength, lpBuffer, lpFilePart);
    }

    int inputStringLength = 0;
    for (inputStringLength = 0; lpFileName[inputStringLength] != '\0'; ++inputStringLength) {

    }

    if (inputStringLength >= 3 && lpFileName[1] == L':')
    {
        return TrueGetFullPathNameW(lpFileName, nBufferLength, lpBuffer, lpFilePart);
    }

    if (inputStringLength >= 3 && (lpFileName[0] == '\\' && lpFileName[1] == '\\' && lpFileName[2] == '.'))
    {
        return TrueGetFullPathNameW(lpFileName, nBufferLength, lpBuffer, lpFilePart);
    }

    int fileNameCount = 0;
    for (int j = 0; lpFileName[j] != '\0'; ++j)
    {
        ++fileNameCount;
    }

    int totalCount = localString->Length + fileNameCount + 1;

    if (nBufferLength == 0 || lpBuffer == NULL)
    {
        WCHAR* pathOut = new WCHAR[totalCount];
        int copyIndex = 0;
        for (int index = 0; index < localString->Length; ++index)
        {
            pathOut[copyIndex] = localString[index];
            ++copyIndex;
        }

        for (int index = 0; index < fileNameCount; ++index)
        {
            pathOut[copyIndex] = lpFileName[index];
            ++copyIndex;
        }

        pathOut[copyIndex] = '\0';

        int writeIndex = 0;
        for (int readIndex = 0; readIndex < totalCount; ++readIndex, ++writeIndex)
        {
            if (pathOut[readIndex] == '.' && pathOut[readIndex + 1] == '.')
            {
                writeIndex -= 2;
                while (pathOut[writeIndex] != '\\')
                {
                    --writeIndex;
                }
                writeIndex;
                readIndex += 2;
            }
            else if (pathOut[readIndex] == '\0')
            {
                pathOut[writeIndex] = pathOut[readIndex];
                break;
            }

            pathOut[writeIndex] = pathOut[readIndex];
        }

        int strlen = 0;
        while (pathOut[strlen] != '\0')
        {
            ++strlen;
        }
        delete[] pathOut;

        return strlen;
    }
    else
    {
        {
            WCHAR* pathOut = new WCHAR[totalCount];
            int copyIndex = 0;
            for (int index = 0; index < localString->Length; ++index)
            {
                pathOut[copyIndex] = localString[index];
                ++copyIndex;
            }

            for (int index = 0; index < fileNameCount; ++index)
            {
                pathOut[copyIndex] = lpFileName[index];
                ++copyIndex;
            }

            pathOut[copyIndex] = '\0';

            int writeIndex = 0;
            for (int readIndex = 0; readIndex < totalCount; ++readIndex, ++writeIndex)
            {
                if (pathOut[readIndex] == '.' && pathOut[readIndex + 1] == '.')
                {
                    writeIndex -= 2;
                    while (pathOut[writeIndex] != '\\')
                    {
                        --writeIndex;
                    }
                    writeIndex;
                    readIndex += 2;
                }
                else if (pathOut[readIndex] == '\0')
                {
                    pathOut[writeIndex] = pathOut[readIndex];
                    break;
                }

                pathOut[writeIndex] = pathOut[readIndex];
            }

            int strlen = 0;
            while (pathOut[strlen] != '\0')
            {
                lpBuffer[strlen] = pathOut[strlen];
                ++strlen;
            }

            lpBuffer[strlen] = '\0';
            delete[] pathOut;

            return strlen;
        }
    }
}

BOOL WINAPI SetCurrentDirectoryDetour(_In_ LPCWSTR lpPathName)
{
    System::String^ newCurrentDir = gcnew System::String(lpPathName);
    bool rooted = System::IO::Path::IsPathRooted(newCurrentDir);
    if (rooted)
    {
        CLRDetourWrapper::DetourWrapper::asyncLocalString->Value = newCurrentDir;

        return true;
    }
    else
    {
        if (CLRDetourWrapper::DetourWrapper::asyncLocalString->Value == nullptr)
        {
            return false;
        }

        System::String^ testString = System::IO::Path::Combine(CLRDetourWrapper::DetourWrapper::asyncLocalString->Value, newCurrentDir);
        if (System::IO::File::Exists(testString))
        {
            CLRDetourWrapper::DetourWrapper::asyncLocalString->Value = testString;
            return true;
        }
    }

    return false;
}

BOOL WINAPI CreateProcessDetour(_In_opt_ LPCWSTR lpApplicationName,
    _Inout_opt_ LPWSTR lpCommandLine,
    _In_opt_ LPSECURITY_ATTRIBUTES lpProcessAttributes,
    _In_opt_ LPSECURITY_ATTRIBUTES lpThreadAttributes,
    _In_ BOOL bInheritHandles,
    _In_ DWORD dwCreationFlags,
    _In_opt_ LPVOID lpEnvironment,
    _In_opt_ LPCWSTR lpCurrentDirectory,
    _In_ LPSTARTUPINFOW lpStartupInfo,
    _Out_ LPPROCESS_INFORMATION lpProcessInformation)
{
    System::String^ localString = CLRDetourWrapper::DetourWrapper::asyncLocalString->Value;
    if (localString == nullptr) {
        return TrueCreateProcessW(lpApplicationName, lpCommandLine, lpProcessAttributes, lpThreadAttributes, bInheritHandles, dwCreationFlags, lpEnvironment, lpCurrentDirectory, lpStartupInfo, lpProcessInformation);
    }

    int len = lpCurrentDirectory == 0 ? 0 : lstrlenW(lpCurrentDirectory);
    if (!len) {
        return TrueCreateProcessW(lpApplicationName, lpCommandLine, lpProcessAttributes, lpThreadAttributes, bInheritHandles, dwCreationFlags, lpEnvironment, lpCurrentDirectory, lpStartupInfo, lpProcessInformation);
    }

    int newDirLength = localString->Length + 1;
    WCHAR* newCurrentDir = new WCHAR[newDirLength];
    for (int i = 0; i < newDirLength - 1; ++i) {
        newCurrentDir[i] = localString[i];
    }

    newCurrentDir[newDirLength - 1] = '\0';

    BOOL result = TrueCreateProcessW(lpApplicationName, lpCommandLine, lpProcessAttributes, lpThreadAttributes, bInheritHandles, dwCreationFlags, lpEnvironment, newCurrentDir, lpStartupInfo, lpProcessInformation);

    delete[] newCurrentDir;

    return result;
}

CLRDetourWrapper::DetourWrapper::DetourWrapper(System::Threading::AsyncLocal<System::String^>^ asyncLocal) {
    asyncLocalString = asyncLocal;

    DetourRestoreAfterWith();

    DetourTransactionBegin();
    DetourUpdateThread(GetCurrentThread());
    DetourAttach(&(PVOID&)TrueGetCurrentDirectoryW, GetCurrentDirectoryDetour);
    DetourAttach(&(PVOID&)TrueSetCurrentDirectoryW, SetCurrentDirectoryDetour);
    DetourAttach(&(PVOID&)TrueGetFullPathNameW, GetFullPathNameDetour);
    DetourAttach(&(PVOID&)TrueCreateProcessW, CreateProcessDetour);
    DetourTransactionCommit();
}
