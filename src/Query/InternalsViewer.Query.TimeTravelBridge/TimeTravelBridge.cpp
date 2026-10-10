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

    constexpr size_t ValuesPerCall = 8;

    constexpr size_t SequenceValue = 0;

    constexpr size_t ThreadValue = 1;

    constexpr size_t IntegerValues = 2;

    constexpr size_t ReturnValue = 6;

    constexpr size_t NodeValue = 7;

    constexpr size_t ChunkCalls = 16384;

    constexpr size_t IdleChunkCalls = 256;

    constexpr size_t IdleFlushInterval = 262144;

    constexpr size_t ForcedChunkCalls = ChunkCalls * 4;

    constexpr uint64_t ReturnedFlag = 1ull << 32;

    constexpr size_t SpanChunk = 65536;

    constexpr uint32_t SpanReturned = 1;

    constexpr uint32_t SpanStartUnknown = 2;

    constexpr uint32_t NoCall = UINT32_MAX;

    constexpr int32_t ExcludedNode = -2;

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
        uint32_t              Logged = 0;
    };

    struct StackFrame
    {
        uint64_t        StackPointer;
        int32_t         Node;
        uint64_t        ReturnAddress;
        LoggedFunction* Function;
        int64_t         Call;
        uint32_t        Chunk;
        Position        Start;
        uint64_t        StartInstructions;
        uint32_t        LoggedCall;
        uint32_t        Flags;
    };

    struct ThreadClock
    {
        Position Last;
        uint64_t Instructions = 0;
    };

    struct LoggedCall
    {
        LoggedFunction* Function;
        int64_t         Call;
        uint32_t        Index;
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
        std::unordered_set<uint64_t>                        Excluded;
        std::unordered_set<uint64_t>                        Markers;
        std::unordered_map<FunctionKey, LoggedFunction, FunctionKeyHash> Log;
        size_t                                              LoggedSinceFlush = 0;
        CallChunkCallback                                   LogCalls = nullptr;
        std::vector<uint64_t>                               Columns;
        CallSpanCallback                                    LogSpans = nullptr;
        std::vector<CallSpan>                               Spans;
        std::unordered_map<uint32_t, ThreadClock>           Clocks;

        int32_t Child(int32_t parent, uint64_t address, uint64_t instance)
        {
            auto [entry, added] = Index.try_emplace(NodeKey{ parent, address, instance },
                                                    static_cast<int32_t>(Nodes.size()));

            if (added)
            {
                Nodes.push_back(CallNode{ parent, 0, address, instance, 0 });
            }

            return entry->second;
        }

        ThreadClock const& Advance(uint32_t thread, Position const& position)
        {
            auto [entry, added] = Clocks.try_emplace(thread);

            auto& clock = entry->second;

            if (!added)
            {
                if (position.Sequence == clock.Last.Sequence)
                {
                    if (position.Steps > clock.Last.Steps)
                    {
                        clock.Instructions += static_cast<uint64_t>(position.Steps - clock.Last.Steps);
                    }
                }
                else if (position.Sequence > clock.Last.Sequence)
                {
                    clock.Instructions += static_cast<uint64_t>(position.Steps);
                }
            }

            clock.Last = position;

            return clock;
        }

        void RecordSpan(StackFrame const& frame, uint32_t thread, ThreadClock const& end, uint32_t flags)
        {
            if (LogSpans == nullptr || frame.Node == ExcludedNode)
            {
                return;
            }

            Spans.push_back(CallSpan{ static_cast<uint64_t>(frame.Start.Sequence),
                                      static_cast<uint64_t>(frame.Start.Steps),
                                      static_cast<uint64_t>(end.Last.Sequence),
                                      static_cast<uint64_t>(end.Last.Steps),
                                      frame.StartInstructions,
                                      end.Instructions,
                                      frame.Node,
                                      thread,
                                      frame.LoggedCall,
                                      frame.Flags | flags });

            if (Spans.size() >= SpanChunk)
            {
                FlushSpans();
            }
        }

        void FlushSpans()
        {
            if (LogSpans == nullptr || Spans.empty())
            {
                return;
            }

            LogSpans(Spans.data(), static_cast<int32_t>(Spans.size()));

            Spans.clear();
        }
    };

    struct ThreadProgress
    {
        uint32_t Thread;
        uint64_t FirstSequence;
        uint64_t LastSequence;
        int32_t  LastPercent;
    };

    struct ReplayState
    {
        ICursor*                    Cursor;
        ProgressCallback            Progress;
        volatile int32_t*           Cancel;
        std::vector<ThreadProgress> Threads;
    };

    int32_t NearestNode(std::vector<StackFrame> const& stack)
    {
        for (auto frame = stack.rbegin(); frame != stack.rend(); ++frame)
        {
            if (frame->Node != ExcludedNode)
            {
                return frame->Node;
            }
        }

        return -1;
    }

    bool InsideMarker(CallTree const& tree, std::vector<StackFrame> const& stack)
    {
        return !stack.empty() && stack.back().Node >= 0 && tree.Markers.contains(tree.Nodes[stack.back().Node].Address);
    }

    bool IsRecorded(std::unordered_set<uint32_t> const& threads, uint32_t threadId)
    {
        return threads.empty() || threads.contains(threadId);
    }

    LoggedCall LogCall(CallTree&            tree,
                       AMD64_CONTEXT const& registers,
                       uint64_t             sequence,
                       uint32_t             threadId,
                       uint64_t             target,
                       uint64_t             instance,
                       int32_t              node)
    {
        if (tree.LogCalls == nullptr)
        {
            return { nullptr, -1, NoCall };
        }

        auto& function = tree.Log[FunctionKey{ target, instance }];

        function.Address = target;
        function.Instance = instance;

        auto const call = function.Values.size() / ValuesPerCall;

        function.Values.resize((call + 1) * ValuesPerCall);

        auto* values = function.Values.data() + call * ValuesPerCall;

        values[SequenceValue] = sequence;
        values[ThreadValue] = threadId;

        values[IntegerValues + 0] = registers.Rcx;
        values[IntegerValues + 1] = registers.Rdx;
        values[IntegerValues + 2] = registers.R8;
        values[IntegerValues + 3] = registers.R9;

        values[NodeValue] = static_cast<uint64_t>(node);

        function.InFlight++;

        tree.LoggedSinceFlush++;

        return { &function, static_cast<int64_t>(call), function.Logged++ };
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

    void FlushIdle(CallTree& tree)
    {
        tree.LoggedSinceFlush = 0;

        for (auto& [key, function] : tree.Log)
        {
            if (function.InFlight != 0)
            {
                continue;
            }

            if (function.Values.size() >= IdleChunkCalls * ValuesPerCall)
            {
                Flush(tree, function);
            }

            if (function.Values.empty())
            {
                std::vector<uint64_t>().swap(function.Values);
            }
        }
    }

    void CompleteCall(StackFrame const& frame, IThreadView const& thread)
    {
        auto* values = frame.Function->Values.data() + frame.Call * ValuesPerCall;

        CROSS_PLATFORM_CONTEXT const context = thread.GetCrossPlatformContext();

        values[ReturnValue] = context.Amd64Context.Rax;

        values[ThreadValue] |= ReturnedFlag;
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
                uint64_t                 returnTarget,
                uint32_t                 threadId,
                ThreadClock const&       clock)
    {
        while (!stack.empty() && stack.back().StackPointer <= stackPointer)
        {
            auto const frame = stack.back();

            stack.pop_back();

            auto const returned = thread != nullptr && frame.ReturnAddress == returnTarget;

            tree.RecordSpan(frame, threadId, clock, returned ? SpanReturned : 0);

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

        auto const position = thread->GetPosition();

        auto const& clock = tree.Advance(threadId, position);

        if (fallThroughAddress != GuestAddress{})
        {
            Unwind(tree, stack, stackPointer, nullptr, 0, threadId, clock);

            auto const marker = tree.Markers.contains(target);

            if (!marker && !stack.empty() && stack.back().Node == ExcludedNode)
            {
                return;
            }

            auto const returnAddress = static_cast<uint64_t>(fallThroughAddress);

            if (!marker && (tree.Excluded.contains(target) || InsideMarker(tree, stack)))
            {
                stack.push_back(StackFrame{ stackPointer,
                                            ExcludedNode,
                                            returnAddress,
                                            nullptr,
                                            -1,
                                            0,
                                            position,
                                            clock.Instructions,
                                            NoCall,
                                            0 });

                return;
            }

            auto const parent = NearestNode(stack);

            CROSS_PLATFORM_CONTEXT const context = thread->GetCrossPlatformContext();

            auto const& registers = context.Amd64Context;

            auto const instance = tree.InstanceMethods.contains(target) ? static_cast<uint64_t>(registers.Rcx) : 0;

            auto const node = tree.Child(parent, target, instance);

            tree.Nodes[node].Calls++;

            auto const logged = LogCall(tree,
                                        registers,
                                        static_cast<uint64_t>(position.Sequence),
                                        threadId,
                                        target,
                                        instance,
                                        node);

            stack.push_back(StackFrame{ stackPointer,
                                        node,
                                        returnAddress,
                                        logged.Function,
                                        logged.Call,
                                        logged.Function ? logged.Function->Chunk : 0,
                                        position,
                                        clock.Instructions,
                                        logged.Index,
                                        0 });

            if (tree.LoggedSinceFlush >= IdleFlushInterval)
            {
                FlushIdle(tree);
            }

            return;
        }

        Unwind(tree, stack, stackPointer + sizeof(uint64_t), thread, target, threadId, clock);

        if (stack.empty())
        {
            stack.push_back(StackFrame{ stackPointer + 2 * sizeof(uint64_t),
                                        tree.Child(-1, target, 0),
                                        0,
                                        nullptr,
                                        -1,
                                        0,
                                        position,
                                        clock.Instructions,
                                        NoCall,
                                        SpanStartUnknown });
        }
    }

    bool __fastcall OnGap(uintptr_t context, GapKind kind, GapEventType event, IThreadView const* thread)
    {
        auto& tree = *reinterpret_cast<CallTree*>(context);

        auto const threadId = static_cast<uint32_t>(thread->GetThreadInfo().Id);

        if (!IsRecorded(tree.Threads, threadId))
        {
            return false;
        }

        auto& stack = tree.Stacks[threadId];

        if (kind == GapKind::Large || event == GapEventType::StopEmulation)
        {
            auto const& clock = tree.Advance(threadId, thread->GetPosition());

            Unwind(tree, stack, UINT64_MAX, nullptr, 0, threadId, clock);

            return false;
        }

        if (kind != GapKind::Unrecorded || stack.empty())
        {
            return false;
        }

        auto const& frame = stack.back();

        auto const programCounter = static_cast<uint64_t>(thread->GetProgramCounter());

        if (frame.ReturnAddress == 0
            || programCounter != frame.ReturnAddress
            || static_cast<uint64_t>(thread->GetStackPointer()) < frame.StackPointer)
        {
            return false;
        }

        auto const& clock = tree.Advance(threadId, thread->GetPosition());

        Unwind(tree, stack, frame.StackPointer, thread, programCounter, threadId, clock);

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

        if (state.Progress == nullptr)
        {
            return;
        }

        auto const sequence = static_cast<uint64_t>(position.Sequence);

        for (auto& thread : state.Threads)
        {
            auto const percent = sequence >= thread.LastSequence
                                 ? 100
                                 : sequence <= thread.FirstSequence
                                   ? 0
                                   : static_cast<int32_t>((sequence - thread.FirstSequence) * 100
                                                          / (thread.LastSequence - thread.FirstSequence));

            if (percent != thread.LastPercent)
            {
                thread.LastPercent = percent;

                state.Progress(thread.Thread, percent);
            }
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

        ReplayState state{ cursor.get(), progress, cancel, {} };

        if (threadFilter.empty())
        {
            state.Threads.push_back(ThreadProgress{ 0,
                                                    static_cast<uint64_t>(engine.GetFirstPosition().Sequence),
                                                    static_cast<uint64_t>(engine.GetLastPosition().Sequence),
                                                    -1 });
        }

        auto const* threads = engine.GetThreadList();

        for (size_t index = 0; index < engine.GetThreadCount() && !threadFilter.empty(); index++)
        {
            auto const& thread = threads[index];

            if (threadFilter.contains(static_cast<uint32_t>(thread.Id)))
            {
                state.Threads.push_back(ThreadProgress{ static_cast<uint32_t>(thread.Id),
                                                        static_cast<uint64_t>(thread.ActiveTime.Min.Sequence),
                                                        static_cast<uint64_t>(thread.ActiveTime.Max.Sequence),
                                                        -1 });
            }
        }

        std::sort(state.Threads.begin(),
                  state.Threads.end(),
                  [](ThreadProgress const& left, ThreadProgress const& right) { return left.FirstSequence < right.FirstSequence; });

        cursor->SetEventMask(EventMask::Gap);
        cursor->SetGapKindMask(GapKindMask::Unrecorded | GapKindMask::Large);
        cursor->SetGapEventMask(GapEventMask::All);
        cursor->SetExceptionMask(ExceptionMask::None);
        cursor->SetCallReturnCallback(onCallReturn, context);
        cursor->SetGapEventCallback(onGap, context);
        cursor->SetReplayProgressCallback(OnProgress, reinterpret_cast<uintptr_t>(&state));
        cursor->SetReplayFlags(ReplayFlags::ReplaySegmentsSequentially | ReplayFlags::ReplayAllSegmentsWithoutFiltering);
        cursor->SetPosition(engine.GetFirstPosition());

        auto const status = ReplayToEnd(*cursor);

        if (status != Success || progress == nullptr)
        {
            return status;
        }

        for (auto const& thread : state.Threads)
        {
            if (thread.LastPercent != 100)
            {
                progress(thread.Thread, 100);
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
                         const uint64_t*       excludedFunctions,
                         int32_t               excludedFunctionCount,
                         const uint64_t*       markerFunctions,
                         int32_t               markerFunctionCount,
                         CallChunkCallback     logCalls,
                         CallSpanCallback      logSpans,
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

        for (int32_t index = 0; index < excludedFunctionCount; index++)
        {
            result->Excluded.insert(excludedFunctions[index]);
        }

        for (int32_t index = 0; index < markerFunctionCount; index++)
        {
            result->Markers.insert(markerFunctions[index]);
        }

        result->LogCalls = logCalls;

        result->LogSpans = logSpans;

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

        for (auto& [threadId, stack] : result->Stacks)
        {
            auto const last = result->Clocks.find(threadId);

            if (last == result->Clocks.end())
            {
                continue;
            }

            for (auto frame = stack.rbegin(); frame != stack.rend(); ++frame)
            {
                result->RecordSpan(*frame, threadId, last->second, 0);
            }
        }

        result->FlushSpans();

        result->Spans = {};
        result->Clocks.clear();

        result->Stacks.clear();
        result->Index.clear();

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

    void CloseCallTree(void* tree)
    {
        delete static_cast<CallTree*>(tree);
    }

    void CloseTrace(void* trace)
    {
        delete static_cast<Trace*>(trace);
    }
}
