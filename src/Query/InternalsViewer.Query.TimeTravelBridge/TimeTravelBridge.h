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

    typedef void(__stdcall* ProgressCallback)(uint32_t thread, int32_t percent);

    typedef void(__stdcall* CallChunkCallback)(uint64_t address, uint64_t instance, const uint64_t* columns, int32_t calls);

    typedef void(__stdcall* CallEndCallback)(uint64_t address,
                                             uint64_t instance,
                                             uint32_t call,
                                             uint64_t endSequence,
                                             uint64_t endSteps,
                                             uint64_t returnValue,
                                             uint32_t returned);

    typedef void(__stdcall* CallSpanCallback)(const CallSpan* spans, int32_t count);

    struct HookFunction
    {
        uint64_t Address;
        uint32_t Events;
        uint32_t Hook;
    };

    struct HookFrame
    {
        uint64_t Id;
        uint64_t StackPointer;
        uint64_t Instance;
        int32_t  Node;
        uint32_t Reserved;
    };

    struct HookEvent
    {
        uint64_t         Function;
        uint64_t         Instance;
        uint64_t         Id;
        uint64_t         Arguments[4];
        uint64_t         ReturnValue;
        uint64_t         StackPointer;
        uint64_t         ReturnAddress;
        uint64_t         Sequence;
        uint64_t         Steps;
        uint64_t         Instructions;
        int32_t          Node;
        uint32_t         Call;
        uint32_t         Thread;
        uint32_t         Type;
        uint32_t         Hook;
        uint32_t         Reserved;
    };

    typedef void(__stdcall* HookCallback)(const HookEvent* hookEvent, const void* view, void* session);

    typedef int32_t(__stdcall* StubCallback)(const void* view, uint64_t address, uint64_t* registers, uint64_t* target);

    __declspec(dllexport) int32_t OpenTrace(const wchar_t* replayLibraryPath, const wchar_t* tracePath, void** trace);

    __declspec(dllexport) int32_t GetModuleCount(void* trace);

    __declspec(dllexport) bool GetModule(void*     trace,
                                         int32_t   index,
                                         wchar_t*  name,
                                         int32_t   nameLength,
                                         uint64_t* address,
                                         uint64_t* size);

    __declspec(dllexport) int32_t ReadCallTree(void*                  trace,
                                               const uint32_t*        threadIds,
                                               int32_t                threadCount,
                                               const uint64_t*        instanceMethods,
                                               int32_t                instanceMethodCount,
                                               const uint64_t*        excludedFunctions,
                                               int32_t                excludedFunctionCount,
                                               const HookFunction*    hookFunctions,
                                               int32_t                hookFunctionCount,
                                               CallChunkCallback      logCalls,
                                               CallEndCallback        logCallEnds,
                                               CallSpanCallback       logSpans,
                                               HookCallback           onHook,
                                               StubCallback           resolveStub,
                                               ProgressCallback       progress,
                                               volatile int32_t*      cancel,
                                               void**                 tree);

    __declspec(dllexport) uint64_t ReadTraceMemory(const void* view, uint64_t address, void* destination, uint64_t size);

    __declspec(dllexport) bool WatchFrame(void* session, uint32_t thread, uint64_t frame);

    __declspec(dllexport) void WatchStack(void* session, uint32_t thread, uint64_t stackPointer);

    __declspec(dllexport) uint32_t GetHookFrames(void* session, uint32_t thread, HookFrame* frames, uint32_t capacity);

    __declspec(dllexport) int32_t GetCallNodeCount(void* tree);

    __declspec(dllexport) void GetCallNodes(void* tree, CallNode* nodes, int32_t count);

    __declspec(dllexport) void CloseCallTree(void* tree);

    __declspec(dllexport) void CloseTrace(void* trace);
}
