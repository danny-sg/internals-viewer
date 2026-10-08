#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>

#define DBG_ASSERT(cond) ((void)0)
#define DBG_ASSERT_MSG(cond, ...) ((void)0)

#include <TTD/IReplayEngine.h>
#include <TTD/IReplayEngineRegisters.h>

#include <algorithm>
#include <memory>
#include <string>
#include <unordered_map>
#include <unordered_set>
#include <vector>

#include "TimeTravelBridge.h"

using namespace TTD;
using namespace TTD::Replay;

namespace
{
    enum ReadResult : int32_t
    {
        Success              = 0,
        LibraryNotLoaded     = 1,
        EntryPointNotFound   = 2,
        EngineNotCreated     = 3,
        TraceNotOpened       = 4,
        CursorNotCreated     = 5,
        ReplayFailed         = 6,
        Cancelled            = 7
    };

    using CreateReplayEngineFunction = uint32_t(__cdecl*)(IReplayEngine*&, GUID const&);

    using CallReturnFunction = void(__fastcall*)(uintptr_t, GuestAddress, GuestAddress, IThreadView const*);

    using GapFunction = bool(__fastcall*)(uintptr_t, GapKind, GapEventType, IThreadView const*);

    constexpr size_t ValuesPerCall = 33;

    constexpr size_t SequenceValue = 0;

    constexpr size_t ThreadValue = 1;

    constexpr size_t IntegerValues = 2;

    constexpr size_t FloatingValues = 10;

    constexpr size_t EntryPointeeValues = 14;

    constexpr size_t ReturnPointeeValues = 22;

    constexpr size_t ReturnValue = 30;

    constexpr size_t FloatingReturnValue = 31;

    constexpr size_t NodeValue = 32;

    constexpr size_t ChunkCalls = 16384;

    constexpr size_t ForcedChunkCalls = ChunkCalls * 4;

    constexpr size_t IntegerSlots = 8;

    constexpr size_t RegisterSlots = 4;

    constexpr uint64_t StackArgumentOffset = 0x28;

    constexpr uint64_t ReturnedFlag = 1ull << 32;

    constexpr uint64_t StackReadFlag = 1ull << 33;

    constexpr int EntryPointeeShift = 40;

    constexpr int ReturnPointeeShift = 48;

    constexpr uint64_t LowestPointer = 0x10000;

    constexpr uint64_t HighestPointer = 0x0000800000000000ull;

    struct NodeKey
    {
        int32_t  Parent;
        uint64_t Address;
        uint64_t Instance;

        bool operator==(NodeKey const& other) const noexcept
        {
            return Parent == other.Parent && Address == other.Address && Instance == other.Instance;
        }
    };

    struct NodeKeyHash
    {
        size_t operator()(NodeKey const& key) const noexcept
        {
            auto const mixed = (key.Address * 0x9E3779B97F4A7C15ull) ^ (key.Instance * 0xC2B2AE3D27D4EB4Full);

            return std::hash<uint64_t>{}(mixed ^ static_cast<uint32_t>(key.Parent));
        }
    };

    struct FunctionKey
    {
        uint64_t Address;
        uint64_t Instance;

        bool operator==(FunctionKey const& other) const noexcept
        {
            return Address == other.Address && Instance == other.Instance;
        }
    };

    struct FunctionKeyHash
    {
        size_t operator()(FunctionKey const& key) const noexcept
        {
            return std::hash<uint64_t>{}((key.Address * 0x9E3779B97F4A7C15ull) ^ (key.Instance * 0xC2B2AE3D27D4EB4Full));
        }
    };

    struct LoggedFunction
    {
        uint64_t              Address = 0;
        uint64_t              Instance = 0;
        std::vector<uint64_t> Values;
        int64_t               InFlight = 0;
        uint32_t              Chunk = 0;
    };

    struct StackFrame
    {
        uint64_t        StackPointer;
        int32_t         Node;
        uint64_t        ReturnAddress;
        LoggedFunction* Function;
        int64_t         Call;
        uint32_t        Chunk;
    };

    struct ModuleEntry
    {
        std::wstring Name;
        uint64_t     Address;
        uint64_t     Size;
    };

    struct Trace
    {
        std::unique_ptr<IReplayEngine, Deleter<IReplayEngine>> Engine;
        std::vector<ModuleEntry>                               Modules;
    };

    struct CallTree
    {
        std::vector<CallNode>                               Nodes;
        std::unordered_map<NodeKey, int32_t, NodeKeyHash>   Index;
        std::unordered_map<uint32_t, std::vector<StackFrame>> Stacks;
        std::unordered_set<uint32_t>                        Threads;
        std::unordered_set<uint64_t>                        InstanceMethods;
        std::vector<CallActivity>                           Activity;
        std::vector<int32_t>                                LastActivity;
        std::unordered_map<FunctionKey, LoggedFunction, FunctionKeyHash> Log;
        CallChunkCallback                                   LogCalls = nullptr;
        std::vector<uint64_t>                               Columns;
        uint64_t                                            FirstSequence = 0;
        uint64_t                                            LastSequence = 0;
        int32_t                                             Slices = 0;

        int32_t Child(int32_t parent, uint64_t address, uint64_t instance)
        {
            auto [entry, added] = Index.try_emplace(NodeKey{ parent, address, instance },
                                                    static_cast<int32_t>(Nodes.size()));

            if (added)
            {
                Nodes.push_back(CallNode{ parent, 0, address, instance, 0 });

                LastActivity.push_back(-1);
            }

            return entry->second;
        }

        void RecordCall(int32_t node, uint64_t sequence)
        {
            if (Slices <= 0)
            {
                return;
            }

            auto const span = LastSequence > FirstSequence ? LastSequence - FirstSequence + 1 : 1;

            auto const offset = sequence > FirstSequence ? sequence - FirstSequence : 0;

            auto const slice = static_cast<int32_t>(std::min<uint64_t>(offset * Slices / span, Slices - 1));

            auto& last = LastActivity[node];

            if (last >= 0 && Activity[last].Slice == slice)
            {
                Activity[last].Calls++;

                return;
            }

            last = static_cast<int32_t>(Activity.size());

            Activity.push_back(CallActivity{ node, slice, 1 });
        }
    };

    struct ReplayState
    {
        ICursor*          Cursor;
        ProgressCallback  Progress;
        volatile int32_t* Cancel;
        uint64_t          FirstSequence;
        uint64_t          LastSequence;
        int32_t           LastPercent;
    };

    bool IsRecorded(std::unordered_set<uint32_t> const& threads, uint32_t threadId)
    {
        return threads.empty() || threads.contains(threadId);
    }

    bool ReadQword(IThreadView const& thread, uint64_t address, uint64_t& value)
    {
        value = 0;

        if (address == 0)
        {
            return false;
        }

        auto const buffer = thread.QueryMemoryBuffer(static_cast<GuestAddress>(address), BufferView{ &value, sizeof(value) });

        return buffer.Memory.Size == sizeof(value);
    }

    uint64_t InstanceOf(CallTree const& tree, uint64_t target, IThreadView const& thread)
    {
        if (!tree.InstanceMethods.contains(target))
        {
            return 0;
        }

        CROSS_PLATFORM_CONTEXT const context = thread.GetCrossPlatformContext();

        return context.Amd64Context.Rcx;
    }

    bool IsPointer(uint64_t value)
    {
        return value >= LowestPointer && value < HighestPointer;
    }

    uint64_t ReadPointees(IThreadView const& thread, uint64_t* values, size_t target, int shift)
    {
        uint64_t flags = 0;

        auto* pointees = values + target;

        for (size_t slot = 0; slot < IntegerSlots; slot++)
        {
            auto const address = values[IntegerValues + slot];

            if ((slot >= RegisterSlots && (values[ThreadValue] & StackReadFlag) == 0) || !IsPointer(address))
            {
                continue;
            }

            if (ReadQword(thread, address, pointees[slot]))
            {
                flags |= 1ull << (shift + slot);
            }
        }

        return flags;
    }

    std::pair<LoggedFunction*, int64_t> LogCall(CallTree&          tree,
                                                IThreadView const& thread,
                                                uint32_t           threadId,
                                                uint64_t           target,
                                                uint64_t           instance,
                                                int32_t            node,
                                                uint64_t           stackPointer,
                                                uint64_t           returnAddress)
    {
        if (tree.LogCalls == nullptr)
        {
            return { nullptr, -1 };
        }

        auto& function = tree.Log[FunctionKey{ target, instance }];

        function.Address = target;
        function.Instance = instance;

        auto const call = function.Values.size() / ValuesPerCall;

        function.Values.resize((call + 1) * ValuesPerCall);

        auto* values = function.Values.data() + call * ValuesPerCall;

        CROSS_PLATFORM_CONTEXT const context = thread.GetCrossPlatformContext();

        auto const& registers = context.Amd64Context;

        values[SequenceValue] = static_cast<uint64_t>(thread.GetPosition().Sequence);

        values[IntegerValues + 0] = registers.Rcx;
        values[IntegerValues + 1] = registers.Rdx;
        values[IntegerValues + 2] = registers.R8;
        values[IntegerValues + 3] = registers.R9;

        values[FloatingValues + 0] = registers.Xmm0.Low;
        values[FloatingValues + 1] = registers.Xmm1.Low;
        values[FloatingValues + 2] = registers.Xmm2.Low;
        values[FloatingValues + 3] = registers.Xmm3.Low;

        uint64_t top = 0;

        auto const entry = ReadQword(thread, stackPointer, top) && top == returnAddress
                           ? stackPointer
                           : stackPointer - sizeof(uint64_t);

        auto stackRead = true;

        for (size_t slot = RegisterSlots; slot < IntegerSlots; slot++)
        {
            auto const address = entry + StackArgumentOffset + (slot - RegisterSlots) * sizeof(uint64_t);

            stackRead = ReadQword(thread, address, values[IntegerValues + slot]) && stackRead;
        }

        values[ThreadValue] = threadId | (stackRead ? StackReadFlag : 0);

        values[ThreadValue] |= ReadPointees(thread, values, EntryPointeeValues, EntryPointeeShift);

        values[NodeValue] = static_cast<uint64_t>(node);

        function.InFlight++;

        return { &function, static_cast<int64_t>(call) };
    }

    void Flush(CallTree& tree, LoggedFunction& function)
    {
        auto const calls = function.Values.size() / ValuesPerCall;

        if (calls == 0)
        {
            return;
        }

        tree.Columns.resize(calls * ValuesPerCall);

        for (size_t call = 0; call < calls; call++)
        {
            for (size_t column = 0; column < ValuesPerCall; column++)
            {
                tree.Columns[column * calls + call] = function.Values[call * ValuesPerCall + column];
            }
        }

        tree.LogCalls(function.Address, function.Instance, tree.Columns.data(), static_cast<int32_t>(calls));

        function.Values.clear();
        function.InFlight = 0;
        function.Chunk++;
    }

    void CompleteCall(StackFrame const& frame, IThreadView const& thread)
    {
        auto* values = frame.Function->Values.data() + frame.Call * ValuesPerCall;

        CROSS_PLATFORM_CONTEXT const context = thread.GetCrossPlatformContext();

        values[ReturnValue] = context.Amd64Context.Rax;

        values[FloatingReturnValue] = context.Amd64Context.Xmm0.Low;

        values[ThreadValue] |= ReturnedFlag | ReadPointees(thread, values, ReturnPointeeValues, ReturnPointeeShift);
    }

    void Release(CallTree& tree, StackFrame const& frame, IThreadView const* thread, uint64_t returnTarget)
    {
        if (frame.Call < 0 || frame.Chunk != frame.Function->Chunk)
        {
            return;
        }

        if (thread != nullptr && frame.ReturnAddress == returnTarget)
        {
            CompleteCall(frame, *thread);
        }

        auto& function = *frame.Function;

        function.InFlight--;

        auto const size = function.Values.size();

        if (size >= ChunkCalls * ValuesPerCall && (function.InFlight == 0 || size >= ForcedChunkCalls * ValuesPerCall))
        {
            Flush(tree, function);
        }
    }

    void Unwind(CallTree&                tree,
                std::vector<StackFrame>& stack,
                uint64_t                 stackPointer,
                IThreadView const*       thread,
                uint64_t                 returnTarget)
    {
        while (!stack.empty() && stack.back().StackPointer <= stackPointer)
        {
            auto const frame = stack.back();

            stack.pop_back();

            Release(tree, frame, thread, returnTarget);
        }
    }

    void __fastcall OnCallReturn(uintptr_t          context,
                                 GuestAddress       instructionAddress,
                                 GuestAddress       fallThroughAddress,
                                 IThreadView const* thread)
    {
        auto& tree = *reinterpret_cast<CallTree*>(context);

        auto const threadId = static_cast<uint32_t>(thread->GetThreadInfo().Id);

        if (!IsRecorded(tree.Threads, threadId))
        {
            return;
        }

        auto& stack = tree.Stacks[threadId];

        auto const target = static_cast<uint64_t>(instructionAddress);

        auto const stackPointer = static_cast<uint64_t>(thread->GetStackPointer());

        if (fallThroughAddress != GuestAddress{})
        {
            Unwind(tree, stack, stackPointer, nullptr, 0);

            auto const parent = stack.empty() ? -1 : stack.back().Node;

            auto const instance = InstanceOf(tree, target, *thread);

            auto const node = tree.Child(parent, target, instance);

            tree.Nodes[node].Calls++;

            tree.RecordCall(node, static_cast<uint64_t>(thread->GetPosition().Sequence));

            auto const returnAddress = static_cast<uint64_t>(fallThroughAddress);

            auto const [function, call] = LogCall(tree, *thread, threadId, target, instance, node, stackPointer, returnAddress);

            stack.push_back(StackFrame{ stackPointer, node, returnAddress, function, call, function ? function->Chunk : 0 });

            return;
        }

        Unwind(tree, stack, stackPointer + sizeof(uint64_t), thread, target);

        if (stack.empty())
        {
            stack.push_back(StackFrame{ stackPointer + 2 * sizeof(uint64_t), tree.Child(-1, target, 0), 0, nullptr, -1, 0 });
        }
    }

    bool __fastcall OnGap(uintptr_t context, GapKind kind, GapEventType event, IThreadView const* thread)
    {
        auto& tree = *reinterpret_cast<CallTree*>(context);

        auto const threadId = static_cast<uint32_t>(thread->GetThreadInfo().Id);

        if ((kind == GapKind::Large || event == GapEventType::StopEmulation) && IsRecorded(tree.Threads, threadId))
        {
            auto& stack = tree.Stacks[threadId];

            Unwind(tree, stack, UINT64_MAX, nullptr, 0);
        }

        return false;
    }

    void __stdcall OnProgress(uintptr_t context, Position const& position)
    {
        auto& state = *reinterpret_cast<ReplayState*>(context);

        if (state.Cancel != nullptr && *state.Cancel != 0)
        {
            state.Cursor->InterruptReplay();

            return;
        }

        if (state.Progress == nullptr || state.LastSequence <= state.FirstSequence)
        {
            return;
        }

        auto const sequence = static_cast<uint64_t>(position.Sequence);

        auto const done = sequence > state.FirstSequence ? sequence - state.FirstSequence : 0;

        auto const percent = static_cast<int32_t>(done * 100 / (state.LastSequence - state.FirstSequence));

        if (percent != state.LastPercent)
        {
            state.LastPercent = percent;

            state.Progress(percent);
        }
    }

    void ReadModules(IReplayEngine& engine, Trace& trace)
    {
        auto const count = engine.GetModuleInstanceCount();

        auto const* instances = engine.GetModuleInstanceList();

        std::unordered_set<uint64_t> seen;

        for (size_t index = 0; index < count; index++)
        {
            auto const* module = instances[index].pModule;

            if (module == nullptr || !seen.insert(static_cast<uint64_t>(module->Address)).second)
            {
                continue;
            }

            trace.Modules.push_back(ModuleEntry{ std::wstring(module->pName, module->NameLength),
                                                static_cast<uint64_t>(module->Address),
                                                module->Size });
        }
    }

    ReadResult ReplayToEnd(ICursor& cursor)
    {
        while (true)
        {
            auto const result = cursor.ReplayForward();

            switch (result.StopReason)
            {
                case EventType::Error:
                {
                    return ReplayFailed;
                }

                case EventType::Interrupted:
                {
                    return Cancelled;
                }

                case EventType::Position:
                case EventType::StepCount:
                case EventType::Process:
                {
                    return Success;
                }

                default:
                {
                    if (result.StepsExecuted == StepCount::Zero)
                    {
                        return Success;
                    }

                    break;
                }
            }
        }
    }

    ReadResult ReplayTrace(IReplayEngine&                      engine,
                           std::unordered_set<uint32_t> const& threadFilter,
                           CallReturnFunction                  onCallReturn,
                           GapFunction                         onGap,
                           uintptr_t                           context,
                           ProgressCallback                    progress,
                           volatile int32_t*                   cancel)
    {
        std::unique_ptr<ICursor, Deleter<ICursor>> cursor{ engine.NewCursor() };

        if (!cursor)
        {
            return CursorNotCreated;
        }

        ReplayState state
        {
            cursor.get(),
            progress,
            cancel,
            static_cast<uint64_t>(engine.GetFirstPosition().Sequence),
            static_cast<uint64_t>(engine.GetLastPosition().Sequence),
            -1
        };

        auto flags = ReplayFlags::ReplaySegmentsSequentially | ReplayFlags::ReplayAllSegmentsWithoutFiltering;

        cursor->SetEventMask(EventMask::Gap);
        cursor->SetGapKindMask(GapKindMask::Unrecorded | GapKindMask::Large);
        cursor->SetGapEventMask(GapEventMask::All);
        cursor->SetExceptionMask(ExceptionMask::None);
        cursor->SetCallReturnCallback(onCallReturn, context);
        cursor->SetGapEventCallback(onGap, context);
        cursor->SetReplayProgressCallback(OnProgress, reinterpret_cast<uintptr_t>(&state));

        if (threadFilter.empty())
        {
            cursor->SetReplayFlags(flags);
            cursor->SetPosition(engine.GetFirstPosition());

            return ReplayToEnd(*cursor);
        }

        cursor->SetReplayFlags(flags | ReplayFlags::ReplayOnlyCurrentThread);

        auto const* threads = engine.GetThreadList();

        for (size_t index = 0; index < engine.GetThreadCount(); index++)
        {
            if (!threadFilter.contains(static_cast<uint32_t>(threads[index].Id)))
            {
                continue;
            }

            cursor->SetPositionOnThread(threads[index].UniqueId, threads[index].Lifetime.Min);

            if (auto const status = ReplayToEnd(*cursor); status != Success)
            {
                return status;
            }
        }

        return Success;
    }
}

extern "C"
{
    int32_t OpenTrace(const wchar_t* replayLibraryPath, const wchar_t* tracePath, void** trace)
    {
        *trace = nullptr;

        auto const library = LoadLibraryExW(replayLibraryPath,
                                            nullptr,
                                            LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR | LOAD_LIBRARY_SEARCH_DEFAULT_DIRS);

        if (library == nullptr)
        {
            return LibraryNotLoaded;
        }

        auto const create = reinterpret_cast<CreateReplayEngineFunction>(GetProcAddress(library, "CreateReplayEngine"));

        if (create == nullptr)
        {
            return EntryPointNotFound;
        }

        IReplayEngine* rawEngine = nullptr;

        if (create(rawEngine, __uuidof(IReplayEngineView)) != 0 || rawEngine == nullptr)
        {
            return EngineNotCreated;
        }

        auto result = std::make_unique<Trace>();

        result->Engine.reset(rawEngine);

        if (!result->Engine->Initialize(tracePath))
        {
            return TraceNotOpened;
        }

        ReadModules(*result->Engine, *result);

        *trace = result.release();

        return Success;
    }

    int32_t GetModuleCount(void* trace)
    {
        return static_cast<int32_t>(static_cast<Trace*>(trace)->Modules.size());
    }

    bool GetModule(void* trace, int32_t index, wchar_t* name, int32_t nameLength, uint64_t* address, uint64_t* size)
    {
        auto const& modules = static_cast<Trace*>(trace)->Modules;

        if (index < 0 || static_cast<size_t>(index) >= modules.size() || nameLength <= 0)
        {
            return false;
        }

        auto const& module = modules[index];

        wcsncpy_s(name, nameLength, module.Name.c_str(), _TRUNCATE);

        *address = module.Address;
        *size = module.Size;

        return true;
    }

    int32_t ReadCallTree(void*                 trace,
                         const uint32_t*       threadIds,
                         int32_t               threadCount,
                         const uint64_t*       instanceMethods,
                         int32_t               instanceMethodCount,
                         int32_t               activitySlices,
                         CallChunkCallback     logCalls,
                         ProgressCallback      progress,
                         volatile int32_t*     cancel,
                         void**                tree)
    {
        *tree = nullptr;

        auto& engine = *static_cast<Trace*>(trace)->Engine;

        auto result = std::make_unique<CallTree>();

        for (int32_t index = 0; index < threadCount; index++)
        {
            result->Threads.insert(threadIds[index]);
        }

        for (int32_t index = 0; index < instanceMethodCount; index++)
        {
            result->InstanceMethods.insert(instanceMethods[index]);
        }

        result->LogCalls = logCalls;

        result->Slices = activitySlices;
        result->FirstSequence = static_cast<uint64_t>(engine.GetFirstPosition().Sequence);
        result->LastSequence = static_cast<uint64_t>(engine.GetLastPosition().Sequence);

        auto const status = ReplayTrace(engine,
                                        result->Threads,
                                        OnCallReturn,
                                        OnGap,
                                        reinterpret_cast<uintptr_t>(result.get()),
                                        progress,
                                        cancel);

        if (status != Success)
        {
            return status;
        }

        result->Stacks.clear();
        result->Index.clear();
        result->LastActivity.clear();

        if (result->LogCalls != nullptr)
        {
            for (auto& [key, function] : result->Log)
            {
                Flush(*result, function);
            }
        }

        result->Log.clear();
        result->Columns = {};

        *tree = result.release();

        return Success;
    }

    int32_t GetCallNodeCount(void* tree)
    {
        return static_cast<int32_t>(static_cast<CallTree*>(tree)->Nodes.size());
    }

    void GetCallNodes(void* tree, CallNode* nodes, int32_t count)
    {
        auto const& source = static_cast<CallTree*>(tree)->Nodes;

        auto const copied = std::min(static_cast<size_t>(count), source.size());

        std::copy_n(source.begin(), copied, nodes);
    }

    int32_t GetCallActivityCount(void* tree)
    {
        return static_cast<int32_t>(static_cast<CallTree*>(tree)->Activity.size());
    }

    void GetCallActivity(void* tree, CallActivity* activity, int32_t count)
    {
        auto const& source = static_cast<CallTree*>(tree)->Activity;

        auto const copied = std::min(static_cast<size_t>(count), source.size());

        std::copy_n(source.begin(), copied, activity);
    }

    void CloseCallTree(void* tree)
    {
        delete static_cast<CallTree*>(tree);
    }

    void CloseTrace(void* trace)
    {
        delete static_cast<Trace*>(trace);
    }
}
