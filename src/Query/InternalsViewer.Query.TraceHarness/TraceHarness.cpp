#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#include <softpub.h>
#include <wintrust.h>
#include <psapi.h>
#include <shellapi.h>

#include <cstdio>
#include <string>
#include <utility>
#include <vector>

#pragma comment(lib, "wintrust.lib")
#pragma comment(lib, "crypt32.lib")
#pragma comment(lib, "shell32.lib")
#pragma comment(lib, "advapi32.lib")

namespace
{
    constexpr DWORD ServiceTimeout = 120000;

    constexpr DWORD StopTimeout = 600000;

    constexpr DWORD PollInterval = 250;

    struct Options
    {
        std::wstring RecorderDirectory;
        std::wstring OutputDirectory;
        std::wstring Id;
        DWORD ProcessId = 0;
        std::vector<std::wstring> Modules;
    };

    class Handle
    {
    public:
        explicit Handle(HANDLE value = nullptr) : value_(value) {}

        ~Handle()
        {
            if (IsValid())
            {
                CloseHandle(value_);
            }
        }

        Handle(Handle const&) = delete;

        Handle& operator=(Handle const&) = delete;

        Handle(Handle&& other) noexcept : value_(std::exchange(other.value_, nullptr)) {}

        HANDLE Get() const { return value_; }

        bool IsValid() const { return value_ != nullptr && value_ != INVALID_HANDLE_VALUE; }

    private:
        HANDLE value_;
    };

    class ServiceHandle
    {
    public:
        explicit ServiceHandle(SC_HANDLE value = nullptr) : value_(value) {}

        ~ServiceHandle()
        {
            if (value_ != nullptr)
            {
                CloseServiceHandle(value_);
            }
        }

        ServiceHandle(ServiceHandle const&) = delete;

        ServiceHandle& operator=(ServiceHandle const&) = delete;

        SC_HANDLE Get() const { return value_; }

    private:
        SC_HANDLE value_;
    };

    std::wstring LogPath;

    void Log(std::wstring const& message)
    {
        FILE* file = nullptr;

        if (_wfopen_s(&file, LogPath.c_str(), L"a, ccs=UTF-8") == 0 && file != nullptr)
        {
            fwprintf(file, L"%s\n", message.c_str());

            fclose(file);
        }
    }

    std::wstring LastError()
    {
        auto const error = GetLastError();

        wchar_t* text = nullptr;

        FormatMessageW(FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_FROM_SYSTEM | FORMAT_MESSAGE_IGNORE_INSERTS,
                       nullptr,
                       error,
                       0,
                       reinterpret_cast<wchar_t*>(&text),
                       0,
                       nullptr);

        std::wstring message = text != nullptr ? text : L"";

        LocalFree(text);

        while (!message.empty() && (message.back() == L'\r' || message.back() == L'\n'))
        {
            message.pop_back();
        }

        return message + L" (" + std::to_wstring(error) + L")";
    }

    bool ParseOptions(Options& options)
    {
        int count = 0;

        auto* arguments = CommandLineToArgvW(GetCommandLineW(), &count);

        if (arguments == nullptr)
        {
            return false;
        }

        for (int index = 1; index + 1 < count; index += 2)
        {
            std::wstring const name = arguments[index];

            std::wstring const value = arguments[index + 1];

            if (name == L"--ttd")
            {
                options.RecorderDirectory = value;
            }
            else if (name == L"--out")
            {
                options.OutputDirectory = value;
            }
            else if (name == L"--id")
            {
                options.Id = value;
            }
            else if (name == L"--pid")
            {
                options.ProcessId = wcstoul(value.c_str(), nullptr, 10);
            }
            else if (name == L"--module")
            {
                options.Modules.push_back(value);
            }
        }

        LocalFree(arguments);

        return !options.RecorderDirectory.empty()
               && !options.OutputDirectory.empty()
               && !options.Id.empty()
               && options.ProcessId != 0;
    }

    std::wstring EventName(Options const& options, wchar_t const* purpose)
    {
        return std::wstring(L"Local\\InternalsViewer.TimeTravel.") + purpose + L"." + options.Id;
    }

    Handle OpenSignal(Options const& options, wchar_t const* purpose)
    {
        Handle handle(OpenEventW(EVENT_MODIFY_STATE | SYNCHRONIZE, FALSE, EventName(options, purpose).c_str()));

        if (!handle.IsValid())
        {
            Log(std::wstring(L"The ") + purpose + L" event could not be opened: " + LastError());
        }

        return handle;
    }

    bool IsMicrosoftSigned(HANDLE file, std::wstring const& path)
    {
        WINTRUST_FILE_INFO fileInfo{};
        fileInfo.cbStruct = sizeof(fileInfo);
        fileInfo.pcwszFilePath = path.c_str();
        fileInfo.hFile = file;

        WINTRUST_DATA data{};
        data.cbStruct = sizeof(data);
        data.dwUIChoice = WTD_UI_NONE;
        data.fdwRevocationChecks = WTD_REVOKE_NONE;
        data.dwUnionChoice = WTD_CHOICE_FILE;
        data.pFile = &fileInfo;
        data.dwStateAction = WTD_STATEACTION_VERIFY;
        data.dwProvFlags = WTD_CACHE_ONLY_URL_RETRIEVAL;

        GUID action = WINTRUST_ACTION_GENERIC_VERIFY_V2;

        auto const status = WinVerifyTrust(static_cast<HWND>(INVALID_HANDLE_VALUE), &action, &data);

        auto microsoft = false;

        if (status == ERROR_SUCCESS)
        {
            auto* provider = WTHelperProvDataFromStateData(data.hWVTStateData);

            auto* signer = provider != nullptr ? WTHelperGetProvSignerFromChain(provider, 0, FALSE, 0) : nullptr;

            if (signer != nullptr && signer->csCertChain > 0 && signer->pasCertChain[0].pCert != nullptr)
            {
                wchar_t organisation[256]{};

                CertGetNameStringW(signer->pasCertChain[0].pCert,
                                   CERT_NAME_ATTR_TYPE,
                                   0,
                                   const_cast<char*>(szOID_ORGANIZATION_NAME),
                                   organisation,
                                   ARRAYSIZE(organisation));

                microsoft = wcscmp(organisation, L"Microsoft Corporation") == 0;
            }
        }
        else
        {
            wchar_t code[16]{};

            swprintf_s(code, L"0x%08lX", static_cast<unsigned long>(status));

            Log(path + L" failed signature verification (" + code + L")");
        }

        data.dwStateAction = WTD_STATEACTION_CLOSE;

        WinVerifyTrust(static_cast<HWND>(INVALID_HANDLE_VALUE), &action, &data);

        return microsoft;
    }

    bool HasBinaryExtension(std::wstring const& name)
    {
        auto const dot = name.find_last_of(L'.');

        if (dot == std::wstring::npos)
        {
            return false;
        }

        auto const extension = name.substr(dot);

        return _wcsicmp(extension.c_str(), L".exe") == 0 || _wcsicmp(extension.c_str(), L".dll") == 0;
    }

    bool VerifyRecorder(std::wstring const& directory, std::vector<Handle>& held)
    {
        Handle folder(CreateFileW(directory.c_str(),
                                  FILE_LIST_DIRECTORY,
                                  FILE_SHARE_READ | FILE_SHARE_WRITE,
                                  nullptr,
                                  OPEN_EXISTING,
                                  FILE_FLAG_BACKUP_SEMANTICS,
                                  nullptr));

        if (!folder.IsValid())
        {
            Log(L"The TTD directory " + directory + L" could not be opened: " + LastError());

            return false;
        }

        held.push_back(std::move(folder));

        WIN32_FIND_DATAW found{};

        auto const search = FindFirstFileW((directory + L"\\*").c_str(), &found);

        if (search == INVALID_HANDLE_VALUE)
        {
            Log(L"The TTD directory " + directory + L" could not be listed: " + LastError());

            return false;
        }

        auto recorderFound = false;

        do
        {
            std::wstring const name = found.cFileName;

            if ((found.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) != 0 || !HasBinaryExtension(name))
            {
                continue;
            }

            auto const path = directory + L"\\" + name;

            Handle file(CreateFileW(path.c_str(), GENERIC_READ, FILE_SHARE_READ, nullptr, OPEN_EXISTING, 0, nullptr));

            if (!file.IsValid())
            {
                Log(L"TTD file " + path + L" could not be opened: " + LastError());

                FindClose(search);

                return false;
            }

            if (!IsMicrosoftSigned(file.Get(), path))
            {
                Log(L"TTD file " + path + L" is not signed by Microsoft. The recording was not started");

                FindClose(search);

                return false;
            }

            recorderFound |= _wcsicmp(name.c_str(), L"TTD.exe") == 0;

            held.push_back(std::move(file));
        }
        while (FindNextFileW(search, &found));

        FindClose(search);

        if (!recorderFound)
        {
            Log(L"TTD.exe was not found in " + directory);
        }

        return recorderFound;
    }

    void EnableDebugPrivilege()
    {
        HANDLE rawToken = nullptr;

        if (!OpenProcessToken(GetCurrentProcess(), TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, &rawToken))
        {
            return;
        }

        Handle token(rawToken);

        TOKEN_PRIVILEGES privileges{};
        privileges.PrivilegeCount = 1;
        privileges.Privileges[0].Attributes = SE_PRIVILEGE_ENABLED;

        if (LookupPrivilegeValueW(nullptr, SE_DEBUG_NAME, &privileges.Privileges[0].Luid))
        {
            AdjustTokenPrivileges(token.Get(), FALSE, &privileges, 0, nullptr, nullptr);
        }
    }

    bool IsRecorderLoaded(DWORD processId, bool& loaded)
    {
        Handle process(OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, FALSE, processId));

        if (!process.IsValid())
        {
            Log(L"SQL Server process " + std::to_wstring(processId) + L" could not be opened: " + LastError());

            return false;
        }

        std::vector<HMODULE> modules(1024);

        DWORD needed = 0;

        while (true)
        {
            auto const size = static_cast<DWORD>(modules.size() * sizeof(HMODULE));

            if (!EnumProcessModulesEx(process.Get(), modules.data(), size, &needed, LIST_MODULES_ALL))
            {
                Log(L"The modules of process " + std::to_wstring(processId) + L" could not be listed: " + LastError());

                return false;
            }

            if (needed <= size)
            {
                break;
            }

            modules.resize(needed / sizeof(HMODULE));
        }

        loaded = false;

        for (size_t index = 0; index < needed / sizeof(HMODULE); index++)
        {
            wchar_t name[MAX_PATH]{};

            if (GetModuleBaseNameW(process.Get(), modules[index], name, MAX_PATH) > 0 && _wcsnicmp(name, L"TTD", 3) == 0)
            {
                loaded = true;

                break;
            }
        }

        return true;
    }

    std::wstring ServiceOf(SC_HANDLE manager, DWORD processId)
    {
        DWORD needed = 0;
        DWORD count = 0;
        DWORD resume = 0;

        EnumServicesStatusExW(manager, SC_ENUM_PROCESS_INFO, SERVICE_WIN32, SERVICE_ACTIVE, nullptr, 0, &needed, &count, &resume, nullptr);

        std::vector<BYTE> buffer(needed);

        resume = 0;

        if (!EnumServicesStatusExW(manager,
                                   SC_ENUM_PROCESS_INFO,
                                   SERVICE_WIN32,
                                   SERVICE_ACTIVE,
                                   buffer.data(),
                                   static_cast<DWORD>(buffer.size()),
                                   &needed,
                                   &count,
                                   &resume,
                                   nullptr))
        {
            Log(L"Services could not be listed: " + LastError());

            return {};
        }

        auto const* services = reinterpret_cast<ENUM_SERVICE_STATUS_PROCESSW const*>(buffer.data());

        for (DWORD index = 0; index < count; index++)
        {
            if (services[index].ServiceStatusProcess.dwProcessId == processId)
            {
                return services[index].lpServiceName;
            }
        }

        Log(L"No service runs in process " + std::to_wstring(processId));

        return {};
    }

    bool WaitForState(SC_HANDLE service, DWORD state, SERVICE_STATUS_PROCESS& status)
    {
        auto const started = GetTickCount64();

        while (true)
        {
            DWORD needed = 0;

            if (!QueryServiceStatusEx(service, SC_STATUS_PROCESS_INFO, reinterpret_cast<BYTE*>(&status), sizeof(status), &needed))
            {
                return false;
            }

            if (status.dwCurrentState == state)
            {
                return true;
            }

            if (GetTickCount64() - started > ServiceTimeout)
            {
                return false;
            }

            Sleep(PollInterval);
        }
    }

    bool StopNamedService(SC_HANDLE manager, std::wstring const& name)
    {
        ServiceHandle service(OpenServiceW(manager, name.c_str(), SERVICE_STOP | SERVICE_QUERY_STATUS));

        SERVICE_STATUS_PROCESS status{};

        SERVICE_STATUS stopStatus{};

        if (service.Get() == nullptr
            || (!ControlService(service.Get(), SERVICE_CONTROL_STOP, &stopStatus) && GetLastError() != ERROR_SERVICE_NOT_ACTIVE)
            || !WaitForState(service.Get(), SERVICE_STOPPED, status))
        {
            Log(L"Service " + name + L" could not be stopped: " + LastError());

            return false;
        }

        return true;
    }

    bool StartNamedService(SC_HANDLE manager, std::wstring const& name, SERVICE_STATUS_PROCESS& status)
    {
        ServiceHandle service(OpenServiceW(manager, name.c_str(), SERVICE_START | SERVICE_QUERY_STATUS));

        if (service.Get() == nullptr
            || !StartServiceW(service.Get(), 0, nullptr)
            || !WaitForState(service.Get(), SERVICE_RUNNING, status))
        {
            Log(L"Service " + name + L" could not be started: " + LastError());

            return false;
        }

        return true;
    }

    std::vector<std::wstring> RunningDependents(SC_HANDLE manager, std::wstring const& name)
    {
        std::vector<std::wstring> names;

        ServiceHandle service(OpenServiceW(manager, name.c_str(), SERVICE_ENUMERATE_DEPENDENTS));

        if (service.Get() == nullptr)
        {
            return names;
        }

        DWORD needed = 0;
        DWORD count = 0;

        EnumDependentServicesW(service.Get(), SERVICE_ACTIVE, nullptr, 0, &needed, &count);

        if (needed == 0)
        {
            return names;
        }

        std::vector<BYTE> buffer(needed);

        if (EnumDependentServicesW(service.Get(),
                                   SERVICE_ACTIVE,
                                   reinterpret_cast<ENUM_SERVICE_STATUSW*>(buffer.data()),
                                   static_cast<DWORD>(buffer.size()),
                                   &needed,
                                   &count))
        {
            auto const* dependents = reinterpret_cast<ENUM_SERVICE_STATUSW const*>(buffer.data());

            for (DWORD index = 0; index < count; index++)
            {
                names.emplace_back(dependents[index].lpServiceName);
            }
        }

        return names;
    }

    DWORD RestartService(DWORD processId)
    {
        ServiceHandle manager(OpenSCManagerW(nullptr, nullptr, SC_MANAGER_CONNECT | SC_MANAGER_ENUMERATE_SERVICE));

        if (manager.Get() == nullptr)
        {
            Log(L"The service manager could not be opened: " + LastError());

            return 0;
        }

        auto const name = ServiceOf(manager.Get(), processId);

        if (name.empty())
        {
            return 0;
        }

        auto const dependents = RunningDependents(manager.Get(), name);

        for (auto const& dependent : dependents)
        {
            if (!StopNamedService(manager.Get(), dependent))
            {
                return 0;
            }
        }

        SERVICE_STATUS_PROCESS status{};

        if (!StopNamedService(manager.Get(), name) || !StartNamedService(manager.Get(), name, status))
        {
            return 0;
        }

        for (auto dependent = dependents.rbegin(); dependent != dependents.rend(); ++dependent)
        {
            SERVICE_STATUS_PROCESS dependentStatus{};

            StartNamedService(manager.Get(), *dependent, dependentStatus);
        }

        Log(L"Service " + name + L" restarted to clear an earlier recording, now process " + std::to_wstring(status.dwProcessId));

        return status.dwProcessId;
    }

    bool WriteProcess(std::wstring const& directory, DWORD processId, bool restarted)
    {
        auto const text = std::to_string(processId) + (restarted ? " 1" : " 0");

        Handle file(CreateFileW((directory + L"\\process.txt").c_str(),
                                GENERIC_WRITE,
                                FILE_SHARE_READ,
                                nullptr,
                                CREATE_ALWAYS,
                                FILE_ATTRIBUTE_NORMAL,
                                nullptr));

        DWORD written = 0;

        return file.IsValid() && WriteFile(file.Get(), text.data(), static_cast<DWORD>(text.size()), &written, nullptr);
    }

    Handle OpenOutput(std::wstring const& path)
    {
        SECURITY_ATTRIBUTES attributes{ sizeof(attributes), nullptr, TRUE };

        return Handle(CreateFileW(path.c_str(),
                                  FILE_APPEND_DATA,
                                  FILE_SHARE_READ | FILE_SHARE_WRITE,
                                  &attributes,
                                  OPEN_ALWAYS,
                                  FILE_ATTRIBUTE_NORMAL,
                                  nullptr));
    }

    Handle Run(std::wstring const& executable,
               std::wstring const& arguments,
               std::wstring const& outputPath,
               std::wstring const& errorPath)
    {
        auto output = OpenOutput(outputPath);

        auto error = OpenOutput(errorPath);

        STARTUPINFOW startup{};
        startup.cb = sizeof(startup);
        startup.dwFlags = STARTF_USESTDHANDLES;
        startup.hStdOutput = output.Get();
        startup.hStdError = error.Get();

        PROCESS_INFORMATION process{};

        auto commandLine = L"\"" + executable + L"\" " + arguments;

        if (!CreateProcessW(executable.c_str(),
                            commandLine.data(),
                            nullptr,
                            nullptr,
                            TRUE,
                            CREATE_NO_WINDOW,
                            nullptr,
                            nullptr,
                            &startup,
                            &process))
        {
            Log(L"TTD could not be started: " + LastError());

            return Handle();
        }

        CloseHandle(process.hThread);

        return Handle(process.hProcess);
    }

    std::wstring AttachArguments(Options const& options, DWORD processId)
    {
        std::wstring arguments = L"-noUI -out \"" + options.OutputDirectory + L"\\" + options.Id + L".run\"";

        for (auto const& module : options.Modules)
        {
            arguments += L" -module " + module;
        }

        return arguments + L" -onInitCompleteEvent " + EventName(options, L"Ready") + L" -attach " + std::to_wstring(processId);
    }

    int Record(Options const& options)
    {
        auto prepared = OpenSignal(options, L"Prepared");
        auto attach = OpenSignal(options, L"Attach");
        auto ready = OpenSignal(options, L"Ready");
        auto stop = OpenSignal(options, L"Stop");

        if (!prepared.IsValid() || !attach.IsValid() || !ready.IsValid() || !stop.IsValid())
        {
            return 1;
        }

        std::vector<Handle> held;

        if (!VerifyRecorder(options.RecorderDirectory, held))
        {
            return 1;
        }

        EnableDebugPrivilege();

        auto processId = options.ProcessId;

        auto loaded = false;

        if (!IsRecorderLoaded(processId, loaded))
        {
            return 1;
        }

        if (loaded)
        {
            processId = RestartService(processId);

            if (processId == 0)
            {
                return 1;
            }
        }

        if (!WriteProcess(options.OutputDirectory, processId, loaded))
        {
            Log(L"The process file could not be written: " + LastError());

            return 1;
        }

        SetEvent(prepared.Get());

        HANDLE const start[] = { attach.Get(), stop.Get() };

        if (WaitForMultipleObjects(2, start, FALSE, INFINITE) != WAIT_OBJECT_0)
        {
            return 0;
        }

        auto const executable = options.RecorderDirectory + L"\\TTD.exe";

        auto recorder = Run(executable,
                            AttachArguments(options, processId),
                            options.OutputDirectory + L"\\ttd.log",
                            options.OutputDirectory + L"\\ttd.error.log");

        if (!recorder.IsValid())
        {
            return 1;
        }

        HANDLE const recording[] = { stop.Get(), recorder.Get() };

        if (WaitForMultipleObjects(2, recording, FALSE, INFINITE) == WAIT_OBJECT_0 + 1)
        {
            if (WaitForSingleObject(ready.Get(), 0) != WAIT_OBJECT_0)
            {
                Log(L"TTD exited before the recording started");

                return 1;
            }

            WaitForSingleObject(stop.Get(), INFINITE);
        }

        auto stopper = Run(executable,
                           L"-stop " + std::to_wstring(processId),
                           options.OutputDirectory + L"\\ttd.stop.log",
                           options.OutputDirectory + L"\\ttd.stop.log");

        if (stopper.IsValid())
        {
            WaitForSingleObject(stopper.Get(), StopTimeout);
        }

        WaitForSingleObject(recorder.Get(), StopTimeout);

        return 0;
    }
}

int APIENTRY wWinMain(_In_ HINSTANCE, _In_opt_ HINSTANCE, _In_ LPWSTR, _In_ int)
{
    Options options;

    if (!ParseOptions(options))
    {
        return 2;
    }

    LogPath = options.OutputDirectory + L"\\recorder.log";

    return Record(options);
}
