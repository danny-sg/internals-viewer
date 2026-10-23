#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>

#define DBG_ASSERT(cond) ((void)0)
#define DBG_ASSERT_MSG(cond, ...) ((void)0)

#include <TTD/IReplayEngine.h>
#include <TTD/IReplayEngineRegisters.h>

#include <algorithm>
#include <array>
#include <cstddef>
#include <cstring>
#include <memory>
#include <optional>
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

    constexpr size_t ValuesPerCall = 11;

    constexpr size_t StartSequenceValue = 0;

    constexpr size_t StartStepsValue = 1;

    constexpr size_t ThreadValue = 2;

    constexpr size_t IntegerValues = 3;

    constexpr size_t ReturnValue = 7;

    constexpr size_t NodeValue = 8;

    constexpr size_t EndSequenceValue = 9;

    constexpr size_t EndStepsValue = 10;

    constexpr uint64_t UnknownSequence = UINT64_MAX;

    constexpr int32_t ArgumentRegisters = 4;

    constexpr uint32_t CallHook = 0;

    constexpr uint32_t EntryHook = 1;

    constexpr uint32_t ReturnHook = 2;

    constexpr uint32_t AbandonedHook = 3;

    constexpr uint32_t FrameEndedHook = 4;

    constexpr uint32_t StackUnwoundHook = 5;

    constexpr uint32_t CallEvents = 1;

    constexpr uint32_t EntryEvents = 2;

    constexpr uint32_t ReturnEvents = 4;

    constexpr uint32_t AllHooks = UINT32_MAX;

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

    constexpr int32_t NotStub = 0;

    constexpr int32_t StubResolved = 1;

    constexpr size_t MaximumStubDepth = 4;

    static_assert(offsetof(AMD64_CONTEXT, R15) - offsetof(AMD64_CONTEXT, Rax) == 15 * sizeof(uint64_t));

    constexpr int InstanceRegisterShift = 60;

    constexpr uint64_t InstanceAddressMask = (1ull << InstanceRegisterShift) - 1;

    struct NodeKey
    {
        int32_t  Parent;
        uint64_t FunctionAddress;
        uint64_t InstanceAddress;

        bool operator==(NodeKey const& other) const noexcept
        {
            return Parent == other.Parent && FunctionAddress == other.FunctionAddress && InstanceAddress == other.InstanceAddress;
        }
    };

    struct NodeKeyHash
    {
        size_t operator()(NodeKey const& key) const noexcept
        {
            auto const mixed = (key.FunctionAddress * 0x9E3779B97F4A7C15ull) ^ (key.InstanceAddress * 0xC2B2AE3D27D4EB4Full);

            return std::hash<uint64_t>{}(mixed ^ static_cast<uint32_t>(key.Parent));
        }
    };

    struct FunctionKey
    {
        uint64_t FunctionAddress;
        uint64_t InstanceAddress;

        bool operator==(FunctionKey const& other) const noexcept
        {
            return FunctionAddress == other.FunctionAddress && InstanceAddress == other.InstanceAddress;
        }
    };

    struct FunctionKeyHash
    {
        size_t operator()(FunctionKey const& key) const noexcept
        {
            return std::hash<uint64_t>{}((key.FunctionAddress * 0x9E3779B97F4A7C15ull) ^ (key.InstanceAddress * 0xC2B2AE3D27D4EB4Full));
        }
    };

    struct LoggedFunction
    {
        uint64_t              FunctionAddress = 0;
        uint64_t              InstanceAddress = 0;
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
        uint64_t        Id;
        uint32_t        Events;
        uint32_t        Hook;
        bool            IsWatched;
    };

    struct HookTarget
    {
        uint32_t Events = 0;
        uint32_t Hook = AllHooks;
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

    size_t ReadMemory(IThreadView const& thread, uint64_t address, void* destination, size_t size)
    {
        auto* bytes = static_cast<uint8_t*>(destination);

        size_t read = 0;

        while (read < size)
        {
            auto const buffer = thread.QueryMemoryBuffer(static_cast<GuestAddress>(address + read),
                                                         BufferView{ bytes + read, size - read });

            auto const found = static_cast<size_t>(buffer.Memory.Size);

            if (found == 0)
            {
                break;
            }

            read += found;
        }

        return read;
    }

    template <typename T>
    bool ReadValue(IThreadView const& thread, uint64_t address, T& value)
    {
        return ReadMemory(thread, address, &value, sizeof(T)) == sizeof(T);
    }

    struct CallTree
    {
        std::vector<CallNode>                               Nodes;
        std::unordered_map<NodeKey, int32_t, NodeKeyHash>   Index;
        std::unordered_map<uint32_t, std::vector<StackFrame>> Stacks;
        std::unordered_set<uint32_t>                        Threads;
        std::unordered_map<uint64_t, uint8_t>               InstanceMethods;
        std::unordered_set<uint64_t>                        Excluded;
        std::unordered_map<FunctionKey, LoggedFunction, FunctionKeyHash> Log;
        size_t                                              LoggedSinceFlush = 0;
        CallChunkCallback                                   LogCalls = nullptr;
        CallEndCallback                                     LogCallEnds = nullptr;
        std::vector<uint64_t>                               Columns;
        CallSpanCallback                                    LogSpans = nullptr;
        std::vector<CallSpan>                               Spans;
        std::unordered_map<uint32_t, ThreadClock>           Clocks;
        uint64_t                                            CallCount = 0;
        std::unordered_map<uint64_t, HookTarget>            HookFunctions;
        HookCallback                                        OnHook = nullptr;
        StubCallback                                        ResolveStub = nullptr;
        std::unordered_set<uint64_t>                        NotStubs;
        std::unordered_map<uint32_t, std::vector<uint64_t>> WatchedStacks;
        size_t                                              WatchedStackCount = 0;

        uint64_t NextCall()
        {
            return ++CallCount;
        }

        static AMD64_CONTEXT const& GetRegisters(IThreadView const& thread, std::optional<AMD64_CONTEXT>& registers)
        {
            if (!registers.has_value())
            {
                CROSS_PLATFORM_CONTEXT const context = thread.GetCrossPlatformContext();

                registers = context.Amd64Context;
            }

            return *registers;
        }

        uint64_t ResolveCall(IThreadView const& thread, uint64_t target, std::optional<AMD64_CONTEXT>& registers)
        {
            for (size_t depth = 0; ResolveStub != nullptr && depth < MaximumStubDepth && !NotStubs.contains(target); depth++)
            {
                GetRegisters(thread, registers);

                uint64_t resolved = 0;

                auto const result = ResolveStub(&thread, target, &registers->Rax, &resolved);

                if (result == NotStub)
                {
                    NotStubs.insert(target);
                }

                if (result != StubResolved)
                {
                    break;
                }

                target = resolved;
            }

            return target;
        }

        HookTarget HookTargetOf(uint64_t function) const
        {
            if (OnHook == nullptr || HookFunctions.empty())
            {
                return {};
            }

            auto const found = HookFunctions.find(function);

            return found == HookFunctions.end() ? HookTarget{} : found->second;
        }

        uint32_t GetFrames(uint32_t threadId, HookFrame* frames, uint32_t capacity) const
        {
            auto const found = Stacks.find(threadId);

            if (found == Stacks.end())
            {
                return 0;
            }

            auto const& stack = found->second;

            uint32_t index = 0;

            for (auto frame = stack.rbegin(); frame != stack.rend(); ++frame, ++index)
            {
                if (index >= capacity)
                {
                    continue;
                }

                auto const instanceAddress = frame->Node >= 0 && static_cast<size_t>(frame->Node) < Nodes.size()
                                                 ? Nodes[frame->Node].InstanceAddress
                                                 : 0;

                frames[index] = HookFrame{ frame->Id, frame->StackPointer, instanceAddress, frame->Node, 0 };
            }

            return index;
        }

        void Dispatch(HookEvent& hookEvent, IThreadView const* thread)
        {
            OnHook(&hookEvent, thread, this);
        }

        static void SetPosition(HookEvent& hookEvent, ThreadClock const& clock)
        {
            hookEvent.Sequence = static_cast<uint64_t>(clock.Last.Sequence);
            hookEvent.Steps = static_cast<uint64_t>(clock.Last.Steps);
            hookEvent.Instructions = clock.Instructions;
        }

        void HookCall(IThreadView const&             thread,
                      AMD64_CONTEXT const&           registers,
                      uint64_t                       target,
                      uint64_t                       stackPointer,
                      uint64_t                       returnAddress,
                      ThreadClock const&             clock,
                      uint32_t                       threadId,
                      int32_t                        node,
                      uint32_t                       hook)
        {
            HookEvent hookEvent{};

            hookEvent.FunctionAddress = target;
            hookEvent.Arguments[0] = registers.Rcx;
            hookEvent.Arguments[1] = registers.Rdx;
            hookEvent.Arguments[2] = registers.R8;
            hookEvent.Arguments[3] = registers.R9;
            hookEvent.StackPointer = stackPointer;
            hookEvent.ReturnAddress = returnAddress;
            SetPosition(hookEvent, clock);
            hookEvent.Node = node;
            hookEvent.Call = NoCall;
            hookEvent.Thread = threadId;
            hookEvent.Type = CallHook;
            hookEvent.Hook = hook;

            Dispatch(hookEvent, &thread);
        }

        void HookEntry(IThreadView const&             thread,
                       AMD64_CONTEXT const&           registers,
                       LoggedFunction const&          function,
                       uint64_t                       id,
                       uint32_t                       call,
                       int32_t                        node,
                       uint64_t                       stackPointer,
                       uint64_t                       returnAddress,
                       ThreadClock const&             clock,
                       uint32_t                       threadId,
                       uint32_t                       hook)
        {
            HookEvent hookEvent{};

            hookEvent.FunctionAddress = function.FunctionAddress;
            hookEvent.InstanceAddress = function.InstanceAddress;
            hookEvent.Id = id;
            hookEvent.Arguments[0] = registers.Rcx;
            hookEvent.Arguments[1] = registers.Rdx;
            hookEvent.Arguments[2] = registers.R8;
            hookEvent.Arguments[3] = registers.R9;
            hookEvent.StackPointer = stackPointer;
            hookEvent.ReturnAddress = returnAddress;
            SetPosition(hookEvent, clock);
            hookEvent.Node = node;
            hookEvent.Call = call;
            hookEvent.Thread = threadId;
            hookEvent.Type = EntryHook;
            hookEvent.Hook = hook;

            Dispatch(hookEvent, &thread);
        }

        void HookExit(StackFrame const&              frame,
                      IThreadView const*             thread,
                      uint32_t                       threadId,
                      uint64_t                       stackPointer,
                      ThreadClock const&             clock)
        {
            HookEvent hookEvent{};

            if (thread != nullptr)
            {
                CROSS_PLATFORM_CONTEXT const context = thread->GetCrossPlatformContext();

                hookEvent.ReturnValue = context.Amd64Context.Rax;
            }

            hookEvent.FunctionAddress = frame.Function->FunctionAddress;
            hookEvent.InstanceAddress = frame.Function->InstanceAddress;
            hookEvent.Id = frame.Id;
            hookEvent.StackPointer = stackPointer;
            hookEvent.ReturnAddress = frame.ReturnAddress;
            SetPosition(hookEvent, clock);
            hookEvent.Node = frame.Node;
            hookEvent.Call = frame.LoggedCall;
            hookEvent.Thread = threadId;
            hookEvent.Type = thread != nullptr ? ReturnHook : AbandonedHook;
            hookEvent.Hook = frame.Hook;

            Dispatch(hookEvent, thread);
        }

        void FrameEnded(StackFrame const& frame, IThreadView const* thread, uint32_t threadId, ThreadClock const& clock)
        {
            HookEvent hookEvent{};

            hookEvent.Id = frame.Id;
            hookEvent.StackPointer = frame.StackPointer;
            SetPosition(hookEvent, clock);
            hookEvent.Node = frame.Node;
            hookEvent.Call = frame.LoggedCall;
            hookEvent.Thread = threadId;
            hookEvent.Type = FrameEndedHook;
            hookEvent.Hook = AllHooks;

            Dispatch(hookEvent, thread);
        }

        void StackUnwound(uint32_t threadId, uint64_t stackPointer, IThreadView const* thread, ThreadClock const& clock)
        {
            if (WatchedStackCount == 0)
            {
                return;
            }

            auto const found = WatchedStacks.find(threadId);

            if (found == WatchedStacks.end() || found->second.empty())
            {
                return;
            }

            auto& watches = found->second;

            auto const before = watches.size();

            watches.erase(std::remove_if(watches.begin(), watches.end(), [stackPointer](uint64_t watch) { return watch <= stackPointer; }),
                          watches.end());

            auto const removed = before - watches.size();

            if (removed == 0)
            {
                return;
            }

            WatchedStackCount -= removed;

            HookEvent hookEvent{};

            hookEvent.StackPointer = stackPointer;
            SetPosition(hookEvent, clock);
            hookEvent.Node = -1;
            hookEvent.Call = NoCall;
            hookEvent.Thread = threadId;
            hookEvent.Type = StackUnwoundHook;
            hookEvent.Hook = AllHooks;

            Dispatch(hookEvent, thread);
        }

        bool WatchFrame(uint32_t threadId, uint64_t frameId)
        {
            auto const found = Stacks.find(threadId);

            if (found == Stacks.end())
            {
                return false;
            }

            for (auto frame = found->second.rbegin(); frame != found->second.rend(); ++frame)
            {
                if (frame->Id == frameId)
                {
                    frame->IsWatched = true;

                    return true;
                }
            }

            return false;
        }

        void WatchStack(uint32_t threadId, uint64_t stackPointer)
        {
            WatchedStacks[threadId].push_back(stackPointer);

            WatchedStackCount++;
        }

        int32_t Child(int32_t parent, uint64_t functionAddress, uint64_t instanceAddress)
        {
            auto [entry, added] = Index.try_emplace(NodeKey{ parent, functionAddress, instanceAddress },
                                                    static_cast<int32_t>(Nodes.size()));

            if (added)
            {
                Nodes.push_back(CallNode{ parent, 0, functionAddress, instanceAddress, 0 });
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

    bool IsRecorded(std::unordered_set<uint32_t> const& threads, uint32_t threadId)
    {
        return threads.empty() || threads.contains(threadId);
    }

    LoggedCall LogCall(CallTree&            tree,
                       AMD64_CONTEXT const& registers,
                       Position const&      position,
                       uint32_t             threadId,
                       uint64_t             target,
                       uint64_t             instanceAddress,
                       int32_t              node)
    {
        if (tree.LogCalls == nullptr)
        {
            return { nullptr, -1, NoCall };
        }

        auto& function = tree.Log[FunctionKey{ target, instanceAddress }];

        function.FunctionAddress = target;
        function.InstanceAddress = instanceAddress;

        auto const call = function.Values.size() / ValuesPerCall;

        function.Values.resize((call + 1) * ValuesPerCall);

        auto* values = function.Values.data() + call * ValuesPerCall;

        values[StartSequenceValue] = static_cast<uint64_t>(position.Sequence);
        values[StartStepsValue] = static_cast<uint64_t>(position.Steps);
        values[ThreadValue] = threadId;

        values[IntegerValues + 0] = registers.Rcx;
        values[IntegerValues + 1] = registers.Rdx;
        values[IntegerValues + 2] = registers.R8;
        values[IntegerValues + 3] = registers.R9;

        values[NodeValue] = static_cast<uint64_t>(node);

        values[EndSequenceValue] = UnknownSequence;

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

        tree.LogCalls(function.FunctionAddress, function.InstanceAddress, tree.Columns.data(), static_cast<int32_t>(calls));

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

    void Release(CallTree&          tree,
                 StackFrame const&  frame,
                 IThreadView const* thread,
                 uint64_t           returnTarget,
                 ThreadClock const& clock)
    {
        if (frame.Call < 0)
        {
            return;
        }

        auto const returned = thread != nullptr && frame.ReturnAddress == returnTarget;

        uint64_t returnValue = 0;

        if (returned)
        {
            CROSS_PLATFORM_CONTEXT const context = thread->GetCrossPlatformContext();

            returnValue = context.Amd64Context.Rax;
        }

        if (frame.Chunk != frame.Function->Chunk)
        {
            if (tree.LogCallEnds != nullptr)
            {
                tree.LogCallEnds(frame.Function->FunctionAddress,
                                 frame.Function->InstanceAddress,
                                 frame.LoggedCall,
                                 static_cast<uint64_t>(clock.Last.Sequence),
                                 static_cast<uint64_t>(clock.Last.Steps),
                                 returnValue,
                                 returned ? 1 : 0);
            }

            return;
        }

        auto* values = frame.Function->Values.data() + frame.Call * ValuesPerCall;

        values[EndSequenceValue] = static_cast<uint64_t>(clock.Last.Sequence);

        values[EndStepsValue] = static_cast<uint64_t>(clock.Last.Steps);

        if (returned)
        {
            values[ReturnValue] = returnValue;

            values[ThreadValue] |= ReturnedFlag;
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

            Release(tree, frame, thread, returnTarget, clock);

            if ((frame.Events & ReturnEvents) != 0)
            {
                tree.HookExit(frame, returned ? thread : nullptr, threadId, stackPointer, clock);
            }

            if (frame.IsWatched)
            {
                tree.FrameEnded(frame, thread, threadId, clock);
            }
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

            auto const returnAddress = static_cast<uint64_t>(fallThroughAddress);

            std::optional<AMD64_CONTEXT> callRegisters;

            auto const callee = tree.ResolveCall(*thread, target, callRegisters);

            auto const hooked = tree.HookTargetOf(callee);

            if ((hooked.Events & CallEvents) != 0)
            {
                tree.HookCall(*thread,
                              CallTree::GetRegisters(*thread, callRegisters),
                              callee,
                              stackPointer,
                              returnAddress,
                              clock,
                              threadId,
                              NearestNode(stack),
                              hooked.Hook);
            }

            if (!stack.empty() && stack.back().Node == ExcludedNode)
            {
                return;
            }

            if (tree.Excluded.contains(callee) || tree.Excluded.contains(target))
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
                                            0,
                                            tree.NextCall() });

                return;
            }

            auto const parent = NearestNode(stack);

            auto const& registers = CallTree::GetRegisters(*thread, callRegisters);

            auto const method = tree.InstanceMethods.find(callee);

            uint64_t const arguments[ArgumentRegisters] = { registers.Rcx, registers.Rdx, registers.R8, registers.R9 };

            auto const instanceAddress = method == tree.InstanceMethods.end() ? 0 : arguments[method->second % ArgumentRegisters];

            auto const node = tree.Child(parent, callee, instanceAddress);

            tree.Nodes[node].Calls++;

            auto const id = tree.NextCall();

            auto const logged = LogCall(tree,
                                        registers,
                                        position,
                                        threadId,
                                        callee,
                                        instanceAddress,
                                        node);

            auto const frameEvents = logged.Function != nullptr ? hooked.Events : 0;

            if ((frameEvents & EntryEvents) != 0)
            {
                tree.HookEntry(*thread,
                               registers,
                               *logged.Function,
                               id,
                               logged.Index,
                               node,
                               stackPointer,
                               returnAddress,
                               clock,
                               threadId,
                               hooked.Hook);
            }

            stack.push_back(StackFrame{ stackPointer,
                                        node,
                                        returnAddress,
                                        logged.Function,
                                        logged.Call,
                                        logged.Function ? logged.Function->Chunk : 0,
                                        position,
                                        clock.Instructions,
                                        logged.Index,
                                        0,
                                        id,
                                        frameEvents,
                                        hooked.Hook });

            if (tree.LoggedSinceFlush >= IdleFlushInterval)
            {
                FlushIdle(tree);
            }

            return;
        }

        tree.StackUnwound(threadId, stackPointer + sizeof(uint64_t), thread, clock);

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
                                        SpanStartUnknown,
                                        tree.NextCall() });
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

            tree.StackUnwound(threadId, UINT64_MAX, thread, clock);

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

    constexpr size_t WatchChunkHits = 65536;
    constexpr size_t WatchChunkBytes = 1 << 20;
    constexpr uint64_t MaximumWatchBytes = 4096;
    constexpr uint32_t MaximumMergedWatchBytes = 65536;

    struct WatchSession
    {
        WatchHitCallback      OnHits = nullptr;
        std::vector<WatchHit> Hits;
        std::vector<uint8_t>  Data;

        void Flush()
        {
            if (!Hits.empty())
            {
                OnHits(Hits.data(), static_cast<int32_t>(Hits.size()), Data.data(), static_cast<int32_t>(Data.size()));
            }

            Hits.clear();

            Data.clear();
        }
    };

    bool __fastcall OnWatchpoint(uintptr_t context, ICursorView::MemoryWatchpointResult const& result, IThreadView const* thread)
    {
        auto& session = *reinterpret_cast<WatchSession*>(context);

        auto const address = static_cast<uint64_t>(result.Address);

        auto const programCounter = static_cast<uint64_t>(thread->GetProgramCounter());

        auto const threadId = static_cast<uint32_t>(thread->GetThreadInfo().Id);

        auto const type = static_cast<uint32_t>(result.AccessType);

        auto const size = static_cast<size_t>(std::min(result.Size, MaximumWatchBytes));

        auto const offset = session.Data.size();

        session.Data.resize(offset + size);

        auto const read = ReadMemory(*thread, address, session.Data.data() + offset, size);

        session.Data.resize(offset + read);

        if (!session.Hits.empty())
        {
            auto& last = session.Hits.back();

            if (last.Thread == threadId
                && last.ProgramCounter == programCounter
                && last.Type == type
                && last.Address + last.Size == address
                && last.Offset + last.Size == offset
                && last.Size + read <= MaximumMergedWatchBytes)
            {
                last.Size += static_cast<uint32_t>(read);

                return false;
            }
        }

        auto const& position = thread->GetPosition();

        session.Hits.push_back(WatchHit{ static_cast<uint64_t>(position.Sequence),
                                         static_cast<uint64_t>(position.Steps),
                                         address,
                                         programCounter,
                                         threadId,
                                         static_cast<uint32_t>(read),
                                         static_cast<uint32_t>(offset),
                                         type });

        if (session.Hits.size() >= WatchChunkHits || session.Data.size() >= WatchChunkBytes)
        {
            session.Flush();
        }

        return false;
    }

    ReadResult ReplayWatch(IReplayEngine&    engine,
                           WatchRange const* ranges,
                           int32_t           rangeCount,
                           DataAccessMask    access,
                           Position const&   start,
                           Position const&   end,
                           WatchSession&     session,
                           ProgressCallback  progress,
                           volatile int32_t* cancel,
                           int32_t&          watched)
    {
        std::unique_ptr<ICursor, Deleter<ICursor>> cursor{ engine.NewCursor() };

        if (!cursor)
        {
            return CursorNotCreated;
        }

        ReplayState state{ cursor.get(), progress, cancel, {} };

        state.Threads.push_back(ThreadProgress{ 0,
                                                static_cast<uint64_t>(start.Sequence),
                                                static_cast<uint64_t>(end.Sequence),
                                                -1 });

        for (int32_t index = 0; index < rangeCount; index++)
        {
            if (cursor->AddMemoryWatchpoint(MemoryWatchpointData{ static_cast<GuestAddress>(ranges[index].Address),
                                                                  ranges[index].Size,
                                                                  access }))
            {
                watched++;
            }
        }

        cursor->SetEventMask(EventMask::MemoryWatchpoint);
        cursor->SetExceptionMask(ExceptionMask::None);
        cursor->SetMemoryWatchpointCallback(OnWatchpoint, reinterpret_cast<uintptr_t>(&session));
        cursor->SetReplayProgressCallback(OnProgress, reinterpret_cast<uintptr_t>(&state));
        cursor->SetReplayFlags(ReplayFlags::ReplaySegmentsSequentially | ReplayFlags::ReplayAllSegmentsWithoutFiltering);
        cursor->SetPosition(start);

        while (true)
        {
            auto const result = cursor->ReplayForward(end);

            if (result.StopReason == EventType::Error)
            {
                return ReplayFailed;
            }

            if (result.StopReason == EventType::Interrupted)
            {
                return Cancelled;
            }

            if (result.StopReason == EventType::Position
                || result.StopReason == EventType::StepCount
                || result.StopReason == EventType::Process
                || result.StepsExecuted == StepCount::Zero)
            {
                break;
            }
        }

        session.Flush();

        if (progress != nullptr && state.Threads[0].LastPercent != 100)
        {
            progress(0, 100);
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

    int32_t ReadCallTree(void*                  trace,
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
                         void**                 tree)
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
            result->InstanceMethods[instanceMethods[index] & InstanceAddressMask]
                = static_cast<uint8_t>(instanceMethods[index] >> InstanceRegisterShift);
        }

        for (int32_t index = 0; index < excludedFunctionCount; index++)
        {
            result->Excluded.insert(excludedFunctions[index]);
        }

        result->LogCalls = logCalls;

        result->LogCallEnds = logCallEnds;

        result->LogSpans = logSpans;

        result->OnHook = onHook;

        result->ResolveStub = resolveStub;

        for (int32_t index = 0; index < hookFunctionCount && onHook != nullptr; index++)
        {
            auto& hooked = result->HookFunctions[hookFunctions[index].Address];

            hooked.Events |= hookFunctions[index].Events;

            hooked.Hook = hookFunctions[index].Hook;
        }

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

                Release(*result, *frame, nullptr, 0, last->second);
            }
        }

        result->FlushSpans();

        result->WatchedStacks.clear();

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

    int32_t WatchMemory(void*             trace,
                        const WatchRange* ranges,
                        int32_t           rangeCount,
                        uint32_t          accessMask,
                        uint64_t          startSequence,
                        uint64_t          endSequence,
                        WatchHitCallback  onHits,
                        ProgressCallback  progress,
                        volatile int32_t* cancel,
                        int32_t*          watched)
    {
        *watched = 0;

        auto& engine = *static_cast<Trace*>(trace)->Engine;

        auto const first = engine.GetFirstPosition();

        auto const last = engine.GetLastPosition();

        auto const start = startSequence == 0
                           ? first
                           : Position{ static_cast<SequenceId>(startSequence), StepCount::Zero };

        auto const end = endSequence == 0 || endSequence >= static_cast<uint64_t>(last.Sequence)
                         ? last
                         : Position{ static_cast<SequenceId>(endSequence), StepCount::Zero };

        WatchSession session;

        session.OnHits = onHits;

        return ReplayWatch(engine,
                           ranges,
                           rangeCount,
                           static_cast<DataAccessMask>(accessMask),
                           start,
                           end,
                           session,
                           progress,
                           cancel,
                           *watched);
    }

    uint64_t ReadTraceMemory(const void* view, uint64_t address, void* destination, uint64_t size)
    {
        if (view == nullptr || destination == nullptr || size == 0)
        {
            return 0;
        }

        return ReadMemory(*static_cast<IThreadView const*>(view), address, destination, static_cast<size_t>(size));
    }

    bool WatchFrame(void* session, uint32_t thread, uint64_t frame)
    {
        return session != nullptr && static_cast<CallTree*>(session)->WatchFrame(thread, frame);
    }

    void WatchStack(void* session, uint32_t thread, uint64_t stackPointer)
    {
        if (session != nullptr)
        {
            static_cast<CallTree*>(session)->WatchStack(thread, stackPointer);
        }
    }

    uint32_t GetHookFrames(void* session, uint32_t thread, HookFrame* frames, uint32_t capacity)
    {
        return session == nullptr ? 0 : static_cast<CallTree*>(session)->GetFrames(thread, frames, capacity);
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
