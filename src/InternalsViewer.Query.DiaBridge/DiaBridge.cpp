#include "pch.h"
#include "DiaBridge.h"

#include <filesystem>
#include <string>
#include <windows.h>

#include <dia2.h>
#include <wrl/client.h>

#include <DbgHelp.h>

#pragma comment(lib, "Dbghelp.lib")

using Microsoft::WRL::ComPtr;

// Source/Session are COM objects whose code lives inside msdia140.dll (DiaModule).
// They must be released (Reset()) before FreeLibrary(DiaModule) runs, otherwise
// Release() ends up calling into memory that's already been unloaded. The same
// rule applies to any local ComPtr<> obtained from this module in OpenPdb below.
struct DiaHandle
{
    HMODULE DiaModule = nullptr;
    bool ComInitialized = false;

    ComPtr<IDiaDataSource> Source;
    ComPtr<IDiaSession> Session;
};

typedef HRESULT(__stdcall *DllGetClassObjectFn)(REFCLSID, REFIID, LPVOID *);

static std::wstring GetCurrentModuleFolder()
{
    HMODULE module = nullptr;

    GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS, reinterpret_cast<LPCWSTR>(&GetCurrentModuleFolder),
                       &module);

    wchar_t path[MAX_PATH] = {};

    GetModuleFileNameW(module, path, MAX_PATH);

    std::filesystem::path p(path);

    return p.parent_path().wstring();
}

static std::wstring DemangleName(const wchar_t *name)
{
    if (!name)
    {
        return L"";
    }

    wchar_t buffer[4096] = {};

    if (UnDecorateSymbolNameW(name, buffer, _countof(buffer), UNDNAME_NAME_ONLY))
    {
        return buffer;
    }

    return name;
}

void *OpenPdb(const wchar_t *pdbPath)
{
    // CoInitializeEx can return RPC_E_CHANGED_MODE if this thread already has a
    // different COM apartment (e.g. an STA UI thread). In that case we must NOT
    // call CoUninitialize, since we never actually acquired a reference to release.
    HRESULT comHr = CoInitializeEx(nullptr, COINIT_MULTITHREADED);
    bool comInitialized = SUCCEEDED(comHr);

    auto folder = GetCurrentModuleFolder();

    // Load msdia140.dll directly from beside this DLL rather than going through normal
    // COM activation, so it doesn't need to be regsvr32'd/registered on the target machine.
    auto diaPath = folder + L"\\msdia140.dll";

    HMODULE hDia = LoadLibraryW(diaPath.c_str());

    if (!hDia)
    {
        if (comInitialized)
        {
            CoUninitialize();
        }
        return nullptr;
    }

    auto dllGetClassObject = reinterpret_cast<DllGetClassObjectFn>(GetProcAddress(hDia, "DllGetClassObject"));

    if (!dllGetClassObject)
    {
        FreeLibrary(hDia);
        if (comInitialized)
        {
            CoUninitialize();
        }
        return nullptr;
    }

    ComPtr<IClassFactory> factory;

    HRESULT hr =
        dllGetClassObject(__uuidof(DiaSource), IID_IClassFactory, reinterpret_cast<void **>(factory.GetAddressOf()));

    if (FAILED(hr))
    {
        factory.Reset();
        FreeLibrary(hDia);
        if (comInitialized)
        {
            CoUninitialize();
        }
        return nullptr;
    }

    ComPtr<IDiaDataSource> source;

    hr = factory->CreateInstance(nullptr, __uuidof(IDiaDataSource), reinterpret_cast<void **>(source.GetAddressOf()));

    if (FAILED(hr))
    {
        source.Reset();
        factory.Reset();
        FreeLibrary(hDia);
        if (comInitialized)
        {
            CoUninitialize();
        }
        return nullptr;
    }

    hr = source->loadDataFromPdb(pdbPath);

    if (FAILED(hr))
    {
        source.Reset();
        factory.Reset();
        FreeLibrary(hDia);
        if (comInitialized)
        {
            CoUninitialize();
        }
        return nullptr;
    }

    ComPtr<IDiaSession> session;

    hr = source->openSession(session.GetAddressOf());

    if (FAILED(hr))
    {
        session.Reset();
        source.Reset();
        factory.Reset();
        FreeLibrary(hDia);
        if (comInitialized)
        {
            CoUninitialize();
        }
        return nullptr;
    }

    factory.Reset();

    auto handle = new DiaHandle();

    handle->DiaModule = hDia;
    handle->ComInitialized = comInitialized;
    handle->Source = source;
    handle->Session = session;

    return handle;
}

bool ResolveRva(void *sessionHandle, unsigned int rva, wchar_t *buffer, int bufferLength)
{
    if (!sessionHandle)
    {
        swprintf_s(buffer, bufferLength, L"NULL_SESSION");

        return false;
    }

    auto handle = static_cast<DiaHandle *>(sessionHandle);

    ComPtr<IDiaSymbol> symbol;

    LONG displacement = 0;

    HRESULT hr = handle->Session->findSymbolByRVAEx(rva, SymTagNull, symbol.GetAddressOf(), &displacement);

    if (FAILED(hr))
    {
        swprintf_s(buffer, bufferLength, L"HRESULT=0x%08X", static_cast<unsigned>(hr));

        return false;
    }

    if (!symbol)
    {
        swprintf_s(buffer, bufferLength, L"NO_SYMBOL");

        return false;
    }

    BSTR name = nullptr;

    hr = symbol->get_name(&name);

    if (FAILED(hr) || !name)
    {
        DWORD symTag = 0;

        symbol->get_symTag(&symTag);

        swprintf_s(buffer, bufferLength, L"TAG=%lu NO_NAME", symTag);

        return false;
    }

    DWORD symTag = 0;

    symbol->get_symTag(&symTag);

    // Demangle to friendlier name
    auto friendly = DemangleName(name);

    swprintf_s(buffer, bufferLength, L"%s+0x%lX", friendly.c_str(), displacement);

    SysFreeString(name);

    return true;
}

// Holds a live symbol enumeration and the prefix to filter on. The enumerator is bound to its session, so it must be
// released (End) before the session is closed.
struct EnumHandle
{
    ComPtr<IDiaEnumSymbols> Symbols;
    std::wstring Prefix;
};

void *BeginEnumSymbols(void *sessionHandle, const wchar_t *prefix)
{
    if (!sessionHandle)
    {
        return nullptr;
    }

    auto handle = static_cast<DiaHandle *>(sessionHandle);

    ComPtr<IDiaSymbol> global;

    if (FAILED(handle->Session->get_globalScope(global.GetAddressOf())) || !global)
    {
        return nullptr;
    }

    ComPtr<IDiaEnumSymbols> symbols;

    // Public symbols cover the exported/public functions found in the symbol-server PDBs (which are public PDBs).
    if (FAILED(global->findChildren(SymTagPublicSymbol, nullptr, nsNone, symbols.GetAddressOf())) || !symbols)
    {
        return nullptr;
    }

    auto enumerator = new EnumHandle();

    enumerator->Symbols = symbols;
    enumerator->Prefix = prefix ? prefix : L"";

    return enumerator;
}

bool NextSymbol(void *enumeratorHandle, wchar_t *buffer, int bufferLength)
{
    if (!enumeratorHandle)
    {
        return false;
    }

    auto enumerator = static_cast<EnumHandle *>(enumeratorHandle);

    for (;;)
    {
        ComPtr<IDiaSymbol> symbol;

        ULONG fetched = 0;

        if (FAILED(enumerator->Symbols->Next(1, symbol.GetAddressOf(), &fetched)) || fetched != 1)
        {
            return false;
        }

        BSTR name = nullptr;

        std::wstring friendly;

        if (SUCCEEDED(symbol->get_name(&name)) && name)
        {
            friendly = DemangleName(name);

            SysFreeString(name);
        }

        // rfind(prefix, 0) == 0 is a StartsWith test.
        if (!friendly.empty() && (enumerator->Prefix.empty() || friendly.rfind(enumerator->Prefix, 0) == 0))
        {
            swprintf_s(buffer, bufferLength, L"%s", friendly.c_str());

            return true;
        }
    }
}

void EndEnumSymbols(void *enumeratorHandle)
{
    if (!enumeratorHandle)
    {
        return;
    }

    auto enumerator = static_cast<EnumHandle *>(enumeratorHandle);

    enumerator->Symbols.Reset();

    delete enumerator;
}

void ClosePdb(void *sessionHandle)
{
    if (!sessionHandle)
    {
        return;
    }

    auto handle = static_cast<DiaHandle *>(sessionHandle);

    // Must release before FreeLibrary below - see the DiaHandle comment above.
    handle->Session.Reset();
    handle->Source.Reset();

    if (handle->DiaModule)
    {
        FreeLibrary(handle->DiaModule);
    }

    bool comInitialized = handle->ComInitialized;

    delete handle;

    if (comInitialized)
    {
        CoUninitialize();
    }
}