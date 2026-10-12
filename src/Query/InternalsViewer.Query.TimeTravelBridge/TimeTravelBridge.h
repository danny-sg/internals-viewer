#pragma once

#include <cstdint>

extern "C"
{
    struct CallNode
    {
        int32_t  Parent;
        uint32_t Reserved;
        uint64_t Address;
        uint64_t Instance;
        uint64_t Calls;
    };

    struct CallSpan
    {
        uint64_t StartSequence;
        uint64_t StartSteps;
        uint64_t EndSequence;
        uint64_t EndSteps;
        uint64_t StartInstructions;
        uint64_t EndInstructions;
        int32_t  Node;
        uint32_t Thread;
        uint32_t Call;
        uint32_t Flags;
    };

    constexpr uint32_t MaximumHops = 3;

    struct PointerRead
    {
        uint64_t Function;
        int32_t  Register;
        uint32_t AtReturn;
        uint32_t Offset;
        uint32_t Hops;
        uint32_t Path[MaximumHops];
    };

    struct PointerValue
    {
        uint64_t Function;
        uint64_t Instance;
        uint64_t Value;
        uint32_t Call;
        int32_t  Register;
        uint32_t Offset;
        uint32_t AtReturn;
    };

    typedef void(__stdcall* ProgressCallback)(uint32_t thread, int32_t percent);

    typedef void(__stdcall* CallChunkCallback)(uint64_t address, uint64_t instance, const uint64_t* columns, int32_t calls);

    typedef void(__stdcall* CallSpanCallback)(const CallSpan* spans, int32_t count);

    struct MarkerValue
    {
        uint64_t Function;
        uint64_t Buffer;
        uint64_t Sequence;
        uint64_t Steps;
        uint64_t Instructions;
        int32_t  Node;
        uint32_t Thread;
    };

    typedef void(__stdcall* PointerValueCallback)(const PointerValue* values, int32_t count);

    typedef void(__stdcall* MarkerCallback)(const MarkerValue* markers, int32_t count);

    __declspec(dllexport) int32_t OpenTrace(const wchar_t* replayLibraryPath, const wchar_t* tracePath, void** trace);

    __declspec(dllexport) int32_t GetModuleCount(void* trace);

    __declspec(dllexport) bool GetModule(void*     trace,
                                         int32_t   index,
                                         wchar_t*  name,
                                         int32_t   nameLength,
                                         uint64_t* address,
                                         uint64_t* size);

    __declspec(dllexport) int32_t ReadCallTree(void*                 trace,
                                               const uint32_t*       threadIds,
                                               int32_t               threadCount,
                                               const uint64_t*       instanceMethods,
                                               int32_t               instanceMethodCount,
                                               const uint64_t*       excludedFunctions,
                                               int32_t               excludedFunctionCount,
                                               const PointerRead*    pointerReads,
                                               int32_t               pointerReadCount,
                                               const uint64_t*       publishFunctions,
                                               int32_t               publishFunctionCount,
                                               const uint64_t*       reserveFunctions,
                                               int32_t               reserveFunctionCount,
                                               CallChunkCallback     logCalls,
                                               CallSpanCallback      logSpans,
                                               PointerValueCallback  logPointerValues,
                                               MarkerCallback        logMarkers,
                                               ProgressCallback      progress,
                                               volatile int32_t*     cancel,
                                               void**                tree);

    __declspec(dllexport) int32_t GetCallNodeCount(void* tree);

    __declspec(dllexport) void GetCallNodes(void* tree, CallNode* nodes, int32_t count);

    __declspec(dllexport) void CloseCallTree(void* tree);

    __declspec(dllexport) void CloseTrace(void* trace);
}
