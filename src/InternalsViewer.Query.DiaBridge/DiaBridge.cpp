#include "pch.h"
#include "DiaBridge.h"

#include <filesystem>
#include <memory>
#include <string>
#include <windows.h>

#include <dia2.h>
#include <wrl/client.h>

#include <DbgHelp.h>

#pragma comment(lib, "Dbghelp.lib")

using Microsoft::WRL::ComPtr;

// Source/Session are COM objects whose code lives inside msdia140.dll (DiaModule). They must be released before
// FreeLibrary(DiaModule) runs, otherwise Release() ends up calling into memory that's already been unloaded. Members
// are destroyed after the destructor body, so the body has to release them explicitly rather than leaving it to
// ComPtr. The same rule applies to any local ComPtr<> obtained from this module, which is why OpenPdb declares its
// locals after the handle they belong to - scope exit then destroys them in that order.
struct DiaHandle
{
    HMODULE DiaModule = nullptr;
    bool ComInitialized = false;

    ComPtr<IDiaDataSource> Source;
    ComPtr<IDiaSession> Session;

    ~DiaHandle()
    {
        Session.Reset();
        Source.Reset();

        if (DiaModule)
        {
            FreeLibrary(DiaModule);
        }

        if (ComInitialized)
        {
            CoUninitialize();
        }
    }
};

// Holds a live symbol enumeration and the prefix to filter on. The enumerator is bound to its session, so it must be
// released (EndEnumSymbols) before the session is closed.
struct EnumHandle
{
    ComPtr<IDiaEnumSymbols> Symbols;
    std::wstring Prefix;
};

// Owns a BSTR returned from DIA - get_name and friends hand ownership to the caller.
class Bstr
{
  public:
    Bstr() = default;

    Bstr(const Bstr &) = delete;

    Bstr &operator=(const Bstr &) = delete;

    ~Bstr()
    {
        SysFreeString(Value);
    }

    BSTR *GetAddressOf()
    {
        return &Value;
    }

    const wchar_t *Get() const
    {
        return Value;
    }

    explicit operator bool() const
    {
        return Value != nullptr;
    }

  private:
    BSTR Value = nullptr;
};

using DllGetClassObjectFn = HRESULT(__stdcall *)(REFCLSID, REFIID, LPVOID *);

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
    // Every failure below returns through the handle's destructor, which unwinds whatever was acquired so far.
    auto handle = std::make_unique<DiaHandle>();

    // CoInitializeEx can return RPC_E_CHANGED_MODE if this thread already has a different COM apartment (e.g. an STA UI
    // thread). In that case we must NOT call CoUninitialize, since we never actually acquired a reference to release.
    handle->ComInitialized = SUCCEEDED(CoInitializeEx(nullptr, COINIT_MULTITHREADED));

    // Load msdia140.dll directly from beside this DLL rather than going through normal COM activation, so it doesn't
    // need to be regsvr32'd/registered on the target machine.
    auto diaPath = GetCurrentModuleFolder() + L"\\msdia140.dll";

    handle->DiaModule = LoadLibraryW(diaPath.c_str());

    if (!handle->DiaModule)
    {
        return nullptr;
    }

    auto dllGetClassObject =
        reinterpret_cast<DllGetClassObjectFn>(GetProcAddress(handle->DiaModule, "DllGetClassObject"));

    if (!dllGetClassObject)
    {
        return nullptr;
    }

    // Declared after the handle so scope exit releases it before the module unloads - see the DiaHandle comment.
    ComPtr<IClassFactory> factory;

    auto hr =
        dllGetClassObject(__uuidof(DiaSource), IID_IClassFactory, reinterpret_cast<void **>(factory.GetAddressOf()));

    if (FAILED(hr))
    {
        return nullptr;
    }

    hr = factory->CreateInstance(nullptr, __uuidof(IDiaDataSource),
                                 reinterpret_cast<void **>(handle->Source.GetAddressOf()));

    if (FAILED(hr))
    {
        return nullptr;
    }

    hr = handle->Source->loadDataFromPdb(pdbPath);

    if (FAILED(hr))
    {
        return nullptr;
    }

    hr = handle->Source->openSession(handle->Session.GetAddressOf());

    if (FAILED(hr))
    {
        return nullptr;
    }

    return handle.release();
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

    Bstr name;

    hr = symbol->get_name(name.GetAddressOf());

    if (FAILED(hr) || !name)
    {
        DWORD symTag = 0;

        symbol->get_symTag(&symTag);

        swprintf_s(buffer, bufferLength, L"TAG=%lu NO_NAME", symTag);

        return false;
    }

    auto friendly = DemangleName(name.Get());

    swprintf_s(buffer, bufferLength, L"%s+0x%lX", friendly.c_str(), displacement);

    return true;
}

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

    auto enumerator = std::make_unique<EnumHandle>();

    // Public symbols cover the exported/public functions found in the symbol-server PDBs (which are public PDBs).
    if (FAILED(global->findChildren(SymTagPublicSymbol, nullptr, nsNone, enumerator->Symbols.GetAddressOf())) ||
        !enumerator->Symbols)
    {
        return nullptr;
    }

    enumerator->Prefix = prefix ? prefix : L"";

    return enumerator.release();
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

        Bstr name;

        std::wstring friendly;

        if (SUCCEEDED(symbol->get_name(name.GetAddressOf())) && name)
        {
            friendly = DemangleName(name.Get());
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
    delete static_cast<EnumHandle *>(enumeratorHandle);
}

void ClosePdb(void *sessionHandle)
{
    delete static_cast<DiaHandle *>(sessionHandle);
}
