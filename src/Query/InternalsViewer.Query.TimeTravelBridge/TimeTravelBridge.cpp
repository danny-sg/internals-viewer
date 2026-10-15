#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>

#define DBG_ASSERT(cond) ((void)0)
#define DBG_ASSERT_MSG(cond, ...) ((void)0)

#include <TTD/IReplayEngine.h>
#include <TTD/IReplayEngineRegisters.h>

#include <algorithm>
#include <array>
#include <cstring>
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

    constexpr size_t ValuesPerCall = 10;

    constexpr size_t SequenceValue = 0;

    constexpr size_t ThreadValue = 1;

    constexpr size_t IntegerValues = 2;

    constexpr size_t ReturnValue = 6;

    constexpr size_t NodeValue = 7;

    constexpr size_t IdValue = 8;

    constexpr size_t EndValue = 9;

    constexpr uint32_t AllocatedOperation = 0;

    constexpr uint32_t ConstructedOperation = 1;

    constexpr uint32_t VisitedOperation = 2;

    constexpr uint32_t NoOperation = UINT32_MAX;

    constexpr uint64_t SmallestAddress = 0x10000;

    constexpr size_t MemoryMapChunk = 65536;

    constexpr int32_t ArgumentRegisters = 4;

    constexpr int32_t ArgumentSlots = 16;

    constexpr size_t StackArgumentCount = ArgumentSlots - ArgumentRegisters;

    constexpr int32_t ReturnRegister = 16;

    constexpr size_t PointerValueChunk = 65536;

    constexpr uint32_t ReadPointerMode = 0;

    constexpr uint32_t ReadValueMode = 1;

    constexpr uint32_t ReadTextMode = 2;

    constexpr uint32_t ReadWideTextMode = 3;

    constexpr uint32_t ReadBytesMode = 4;

    constexpr size_t MaximumTextBytes = 128;

    constexpr uint64_t PageSize = 0x1000;

    constexpr uint64_t BindingCountOffset = 0x62;

    constexpr uint64_t BindingArrayOffset = 0x70;

    constexpr size_t BindingSize = 0x50;

    constexpr size_t BindingRecordOffset = 0x00;

    constexpr size_t BindingValueOffset = 0x28;

    constexpr size_t BindingStatusOffset = 0x30;

    constexpr size_t BindingFixedLengthOffset = 0x3C;

    constexpr size_t BindingFlagsOffset = 0x40;

    constexpr size_t BindingInfoOffset = 0x48;

    constexpr uint64_t BindingInfoLengthOffset = 0x18;

    constexpr uint64_t BindingInfoFlagsOffset = 0x2A;

    constexpr uint32_t ByReferenceBinding = 0x800;

    constexpr uint32_t LengthBinding = 0x100;

    constexpr uint8_t AlternateBufferBinding = 0x10;

    constexpr uint16_t MaximumBindings = 1024;

    constexpr uint32_t BindingRowFormat = 0;

    constexpr uint32_t ValueRowFormat = 1;

    constexpr uint64_t RowValueSize = 0x20;

    constexpr uint64_t RowValueStatusOffset = 0x00;

    constexpr uint64_t RowValueDataOffset = 0x08;

    constexpr uint64_t RowValueLengthOffset = 0x10;

    constexpr uint64_t MaximumRowBufferBytes = 0x10000;

    constexpr uint64_t MaximumValueBytes = 8000;

    constexpr uint8_t ByReferenceColumn = 1;

    constexpr uint8_t LengthColumn = 2;

    constexpr uint8_t TruncatedColumn = 4;

    constexpr uint8_t UnreadableColumn = 8;

    constexpr uint8_t PageReferenceColumn = 16;

    constexpr uint8_t UnreadableBindingColumn = 32;

    constexpr uint8_t AlternateBufferColumn = 64;

    constexpr uint8_t UnreadableSlotColumn = 128;

    constexpr uint64_t DataPageSize = 0x2000;

    constexpr uint64_t PageHeaderSize = 96;

    constexpr uint64_t PageIdOffset = 0x20;

    constexpr size_t PageIdSize = 6;

    constexpr size_t PageIdFileOffset = 4;

    constexpr uint64_t LsnOffset = 0x28;

    constexpr size_t LsnSize = 10;

    constexpr uint8_t PageHeaderVersion = 1;

    constexpr size_t PageReferenceSize = sizeof(uint16_t) + PageIdSize + LsnSize;

    constexpr size_t RowBufferChunkRows = 4096;

    constexpr size_t RowBufferChunkBytes = 1 << 22;

    constexpr uint64_t BatchIdsOffset = 0x08;

    constexpr uint64_t BatchVectorsOffset = 0x08;

    constexpr uint64_t BatchPureOffset = 0x10;

    constexpr uint64_t BatchVectorCountOffset = 0x1C;

    constexpr uint64_t BatchCapacityOffset = 0x24;

    constexpr uint64_t BatchRowCountOffset = 0x28;

    constexpr uint64_t BatchSelectionOffset = 0x38;

    constexpr uint64_t BatchVectorEntrySize = 0x58;

    constexpr uint16_t MaximumBatchVectors = 1024;

    constexpr int32_t MaximumBatchSelection = 0x10000;

    constexpr uint64_t LargestInlineTag = 0xF;

    constexpr uint64_t DeepLengthOffset = 2;

    constexpr uint32_t UnreadableBatch = 1;

    constexpr uint32_t UnreadableSelection = 2;

    constexpr uint32_t ImplicitSelection = 4;

    constexpr uint16_t PureVector = 1;

    constexpr uint16_t UnreadableVector = 2;

    constexpr uint16_t MissingVector = 4;

    constexpr uint16_t UnreadableSlots = 8;

    constexpr size_t BatchChunkBatches = 1024;

    constexpr size_t BatchChunkSlots = 1 << 20;

    constexpr size_t BatchChunkBytes = 1 << 22;

    constexpr size_t PublishedEventChunk = 65536;

    constexpr uint8_t EventPublisherFunction = 1;

    constexpr uint8_t EventWriterFunction = 2;

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

    constexpr int InstanceRegisterShift = 60;

    constexpr uint64_t InstanceAddressMask = (1ull << InstanceRegisterShift) - 1;

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
        std::array<uint64_t, StackArgumentCount> StackArguments;
        uint64_t        Id;
    };

    uint64_t OwnerOf(std::vector<StackFrame> const& stack, uint64_t address, uint64_t stackPointer)
    {
        if (address < stackPointer)
        {
            return 0;
        }

        for (auto frame = stack.rbegin(); frame != stack.rend(); ++frame)
        {
            if (frame->StackPointer > address)
            {
                return frame->Id;
            }
        }

        return 0;
    }

    bool IsActive(std::vector<StackFrame> const& stack, uint64_t call)
    {
        for (auto frame = stack.rbegin(); frame != stack.rend(); ++frame)
        {
            if (frame->Id == call)
            {
                return true;
            }
        }

        return false;
    }

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

    struct PendingPublish
    {
        uint64_t Publisher;
        uint64_t StackPointer;
        Position Start;
        uint64_t Instructions;
        int32_t  Node;
        uint64_t SessionBuffer;
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

    struct ColumnBinding
    {
        int32_t  RecordOffset;
        uint32_t Flags;
        uint16_t FixedLength;
        uint8_t  Unreadable;
        uint64_t ValueOffset;
        uint64_t StatusOffset;
        uint64_t LengthOffset;
    };

    struct RowBufferStart
    {
        uint64_t               Iterator = 0;
        RowBufferLayout const* Layout = nullptr;
        uint64_t               Buffer = 0;
        uint64_t               Bindings = 0;
        uint32_t               Count = 0;
    };

    struct BatchStart
    {
        uint64_t Iterator = 0;
        uint64_t Data = 0;
        uint64_t Consumer = 0;
    };

    struct BindingSet
    {
        uint16_t                   Count = 0;
        uint64_t                   FirstValueOffset = 0;
        uint32_t                   FirstFlags = 0;
        std::vector<ColumnBinding> Columns;
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

    template <typename T>
    T ReadField(uint8_t const* source, size_t offset)
    {
        T value{};

        std::memcpy(&value, source + offset, sizeof(T));

        return value;
    }

    bool IsInBuffer(uint64_t offset, uint64_t size)
    {
        return offset < MaximumRowBufferBytes && size <= MaximumRowBufferBytes - offset;
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
        std::vector<uint64_t>                               Columns;
        CallSpanCallback                                    LogSpans = nullptr;
        std::vector<CallSpan>                               Spans;
        std::unordered_map<uint32_t, ThreadClock>           Clocks;
        std::unordered_map<uint64_t, std::vector<PointerRead>> Reads;
        PointerValueCallback                                LogPointerValues = nullptr;
        std::vector<PointerValue>                           PointerValues;
        std::unordered_map<uint64_t, uint8_t>               PublishedEventFunctions;
        std::unordered_map<uint32_t, std::vector<PendingPublish>> Publishing;
        size_t                                              PendingPublishes = 0;
        PublishedEventCallback                              LogPublishedEvents = nullptr;
        std::vector<PublishedEvent>                         PublishedEvents;
        uint64_t                                            CallCount = 0;
        std::unordered_map<uint64_t, uint32_t>              MemoryMapFunctions;
        MemoryMapCallback                                   LogMemoryMap = nullptr;
        std::vector<MemoryMapEntry>                         MemoryMap;
        std::unordered_map<uint64_t, uint32_t>              LatestEntries;
        std::unordered_map<uint64_t, std::vector<uint32_t>> OwnedEntries;
        std::unordered_map<uint64_t, int32_t>               RowBufferReads;
        std::unordered_map<uint64_t, RowBufferLayout>       RowBufferLayouts;
        RowBufferCallback                                   LogRowBuffers = nullptr;
        std::vector<RowBufferRow>                           RowBufferRows;
        std::vector<RowBufferColumn>                        RowBufferColumns;
        std::vector<uint8_t>                                RowBufferData;
        std::unordered_map<uint64_t, BindingSet>            BindingSets;
        std::unordered_map<uint64_t, RowBufferStart>        RowBufferStarts;
        std::unordered_set<uint64_t>                        BatchFunctions;
        BatchCallback                                       LogBatches = nullptr;
        std::unordered_map<uint64_t, BatchStart>            BatchStarts;
        std::vector<CapturedBatch>                          Batches;
        std::vector<CapturedVector>                         BatchVectors;
        std::vector<uint16_t>                               BatchSelection;
        std::vector<uint64_t>                               BatchSlots;
        std::vector<CapturedDeepValue>                      BatchDeepValues;
        std::vector<uint8_t>                                BatchData;
        std::vector<uint16_t>                               BatchVectorIds;
        std::vector<uint64_t>                               BatchBlock;

        uint64_t NextCall()
        {
            return ++CallCount;
        }

        int32_t RowBufferRegisterOf(uint64_t function) const
        {
            if (LogRowBuffers == nullptr || RowBufferReads.empty())
            {
                return -1;
            }

            auto const found = RowBufferReads.find(function);

            return found == RowBufferReads.end() ? -1 : found->second;
        }

        void BeginRowBuffer(IThreadView const& thread, uint64_t id, uint64_t iterator)
        {
            if (iterator < SmallestAddress)
            {
                return;
            }

            RowBufferStart start{ iterator };

            if (!ReadLayout(thread, start))
            {
                return;
            }

            RowBufferStarts[id] = start;
        }

        bool ReadLayout(IThreadView const& thread, RowBufferStart& start) const
        {
            uint64_t vtable = 0;

            if (!ReadValue(thread, start.Iterator, vtable))
            {
                return true;
            }

            auto const layout = RowBufferLayouts.find(vtable);

            if (layout == RowBufferLayouts.end())
            {
                return false;
            }

            start.Layout = &layout->second;

            uint64_t pointer = 0;

            if (ReadValue(thread, start.Iterator + start.Layout->BufferOffset, pointer))
            {
                start.Buffer = pointer;
            }

            if (start.Layout->Format == ValueRowFormat)
            {
                uint32_t count = 0;

                if (ReadValue(thread, start.Iterator + start.Layout->CountOffset, count))
                {
                    start.Count = count;
                }
            }
            else if (ReadValue(thread, start.Iterator + start.Layout->BindingsOffset, pointer))
            {
                start.Bindings = pointer;
            }

            return true;
        }

        void EndRowBuffer(uint64_t id)
        {
            if (!RowBufferStarts.empty())
            {
                RowBufferStarts.erase(id);
            }
        }

        void ReadRowBuffer(IThreadView const& thread, LoggedFunction const& function, uint32_t call, uint64_t id)
        {
            auto const found = RowBufferStarts.find(id);

            if (found == RowBufferStarts.end())
            {
                return;
            }

            auto start = found->second;

            RowBufferStarts.erase(found);

            if (start.Layout == nullptr && (!ReadLayout(thread, start) || start.Layout == nullptr))
            {
                return;
            }

            auto const isValueRow = start.Layout->Format == ValueRowFormat;

            if (start.Buffer == 0 || (isValueRow ? start.Count == 0 : start.Bindings == 0))
            {
                ReadLayout(thread, start);
            }

            if (isValueRow)
            {
                ReadValueRow(thread, function, call, start);

                return;
            }

            auto const iterator = start.Iterator;

            auto const buffer = start.Buffer;

            auto const bindings = start.Bindings;

            if (buffer < SmallestAddress || bindings < SmallestAddress)
            {
                return;
            }

            auto const* set = ReadBindings(thread, bindings);

            if (set == nullptr)
            {
                return;
            }

            auto const first = static_cast<uint32_t>(RowBufferColumns.size());

            for (auto const& binding : set->Columns)
            {
                RowBufferColumns.push_back(ReadColumn(thread, binding, buffer));
            }

            RowBufferRows.push_back(RowBufferRow{ function.Address,
                                                  function.Instance,
                                                  iterator,
                                                  buffer,
                                                  CallCount,
                                                  call,
                                                  first,
                                                  static_cast<uint32_t>(set->Columns.size()),
                                                  BindingRowFormat });

            FlushRowBuffersWhenFull();
        }

        void ReadValueRow(IThreadView const& thread, LoggedFunction const& function, uint32_t call, RowBufferStart const& start)
        {
            if (start.Buffer < SmallestAddress || start.Count == 0 || start.Count > MaximumBindings)
            {
                return;
            }

            auto const first = static_cast<uint32_t>(RowBufferColumns.size());

            for (uint32_t index = 0; index < start.Count; index++)
            {
                RowBufferColumns.push_back(ReadValueColumn(thread, start.Buffer + index * RowValueSize));
            }

            RowBufferRows.push_back(RowBufferRow{ function.Address,
                                                  function.Instance,
                                                  start.Iterator,
                                                  start.Buffer,
                                                  CallCount,
                                                  call,
                                                  first,
                                                  start.Count,
                                                  ValueRowFormat });

            FlushRowBuffersWhenFull();
        }

        RowBufferColumn ReadValueColumn(IThreadView const& thread, uint64_t address)
        {
            RowBufferColumn column{ static_cast<uint32_t>(RowBufferData.size()), 0, 0, -1, 0, 0, 0, 0 };

            if (!ReadValue(thread, address + RowValueStatusOffset, column.Status))
            {
                column.Flags = UnreadableColumn | UnreadableSlotColumn;

                return column;
            }

            if (column.Status != 0)
            {
                return column;
            }

            uint64_t value = 0;

            uint64_t length = 0;

            if (!ReadValue(thread, address + RowValueDataOffset, value) || !ReadValue(thread, address + RowValueLengthOffset, length))
            {
                column.Flags = UnreadableColumn | UnreadableSlotColumn;

                return column;
            }

            if (length == 0)
            {
                auto const* bytes = reinterpret_cast<uint8_t const*>(&value);

                RowBufferData.insert(RowBufferData.end(), bytes, bytes + sizeof(value));

                column.Length = sizeof(value);

                column.Captured = sizeof(value);

                return column;
            }

            column.Flags = ByReferenceColumn | LengthColumn;

            column.Length = static_cast<uint32_t>((std::min)(length, uint64_t{ UINT32_MAX }));

            return ReadValueBytes(thread, value, length, column);
        }

        void FlushRowBuffersWhenFull()
        {
            if (RowBufferRows.size() >= RowBufferChunkRows || RowBufferData.size() >= RowBufferChunkBytes)
            {
                FlushRowBuffers();
            }
        }

        BindingSet const* ReadBindings(IThreadView const& thread, uint64_t bindings)
        {
            uint16_t count = 0;

            uint64_t array = 0;

            if (!ReadValue(thread, bindings + BindingCountOffset, count)
                || !ReadValue(thread, bindings + BindingArrayOffset, array)
                || count == 0
                || count > MaximumBindings
                || array < SmallestAddress)
            {
                return nullptr;
            }

            uint64_t firstValueOffset = 0;

            uint32_t firstFlags = 0;

            if (!ReadValue(thread, array + BindingValueOffset, firstValueOffset)
                || !ReadValue(thread, array + BindingFlagsOffset, firstFlags))
            {
                return nullptr;
            }

            auto& set = BindingSets[array];

            if (set.Count == count && set.FirstValueOffset == firstValueOffset && set.FirstFlags == firstFlags)
            {
                return &set;
            }

            set.Count = count;

            set.FirstValueOffset = firstValueOffset;

            set.FirstFlags = firstFlags;

            set.Columns.clear();

            for (size_t index = 0; index < count; index++)
            {
                set.Columns.push_back(ReadBinding(thread, array + index * BindingSize));
            }

            return &set;
        }

        static ColumnBinding ReadBinding(IThreadView const& thread, uint64_t address)
        {
            ColumnBinding binding{ -1, 0, 0, 0, 0, 0, 0 };

            uint64_t info = 0;

            uint8_t infoFlags = 0;

            if (!ReadValue(thread, address + BindingRecordOffset, binding.RecordOffset))
            {
                binding.RecordOffset = -1;
            }

            if (!ReadValue(thread, address + BindingFlagsOffset, binding.Flags)
                || !ReadValue(thread, address + BindingFixedLengthOffset, binding.FixedLength)
                || !ReadValue(thread, address + BindingValueOffset, binding.ValueOffset)
                || !ReadValue(thread, address + BindingStatusOffset, binding.StatusOffset)
                || !ReadValue(thread, address + BindingInfoOffset, info)
                || info < SmallestAddress
                || !ReadValue(thread, info + BindingInfoLengthOffset, binding.LengthOffset)
                || !ReadValue(thread, info + BindingInfoFlagsOffset, infoFlags))
            {
                binding.Unreadable = UnreadableBindingColumn;
            }
            else if ((infoFlags & AlternateBufferBinding) != 0)
            {
                binding.Unreadable = AlternateBufferColumn;
            }

            return binding;
        }

        RowBufferColumn ReadColumn(IThreadView const& thread, ColumnBinding const& binding, uint64_t buffer)
        {
            RowBufferColumn column{ static_cast<uint32_t>(RowBufferData.size()),
                                    0,
                                    0,
                                    binding.RecordOffset,
                                    binding.Flags,
                                    binding.FixedLength,
                                    0,
                                    0 };

            if (binding.Unreadable != 0)
            {
                column.Flags = UnreadableColumn | binding.Unreadable;

                return column;
            }

            if (!IsInBuffer(binding.StatusOffset, sizeof(column.Status))
                || !IsInBuffer(binding.ValueOffset, sizeof(uint64_t))
                || !ReadValue(thread, buffer + binding.StatusOffset, column.Status))
            {
                column.Flags = UnreadableColumn | UnreadableSlotColumn;

                return column;
            }

            if (column.Status != 0)
            {
                return column;
            }

            uint64_t length = binding.FixedLength;

            if ((binding.Flags & LengthBinding) != 0)
            {
                if (!IsInBuffer(binding.LengthOffset, sizeof(length)) || !ReadValue(thread, buffer + binding.LengthOffset, length))
                {
                    column.Flags = UnreadableColumn | UnreadableSlotColumn;

                    return column;
                }

                column.Flags |= LengthColumn;
            }

            column.Length = static_cast<uint32_t>((std::min)(length, uint64_t{ UINT32_MAX }));

            if ((binding.Flags & ByReferenceBinding) == 0)
            {
                auto const stored = binding.FixedLength > 0 ? (std::min)(length, uint64_t{ binding.FixedLength }) : length;

                return ReadValueBytes(thread, buffer + binding.ValueOffset, stored, column);
            }

            column.Flags |= ByReferenceColumn;

            uint64_t pointer = 0;

            if (!ReadValue(thread, buffer + binding.ValueOffset, pointer))
            {
                column.Flags |= UnreadableColumn | UnreadableSlotColumn;

                return column;
            }

            if (ReadPageReference(thread, pointer, length, column))
            {
                return column;
            }

            return ReadValueBytes(thread, pointer, length, column);
        }

        RowBufferColumn ReadValueBytes(IThreadView const& thread, uint64_t address, uint64_t length, RowBufferColumn column)
        {
            auto const wanted = static_cast<size_t>((std::min)(length, MaximumValueBytes));

            auto const offset = RowBufferData.size();

            RowBufferData.resize(offset + wanted);

            auto const found = address < SmallestAddress ? 0 : ReadMemory(thread, address, RowBufferData.data() + offset, wanted);

            RowBufferData.resize(offset + found);

            column.Captured = static_cast<uint32_t>(found);

            if (found < wanted)
            {
                column.Flags |= UnreadableColumn;
            }
            else if (column.Captured < column.Length)
            {
                column.Flags |= TruncatedColumn;
            }

            return column;
        }

        bool ReadPageReference(IThreadView const& thread, uint64_t pointer, uint64_t length, RowBufferColumn& column)
        {
            auto const offset = pointer & (DataPageSize - 1);

            if (pointer < SmallestAddress || offset < PageHeaderSize || length > DataPageSize - offset)
            {
                return false;
            }

            auto const page = pointer - offset;

            std::array<uint8_t, PageIdSize> pageId{};

            std::array<uint8_t, LsnSize> lsn{};

            uint8_t version = 0;

            if (!ReadValue(thread, page + PageIdOffset, pageId)
                || !ReadValue(thread, page + LsnOffset, lsn)
                || ReadField<uint16_t>(pageId.data(), PageIdFileOffset) == 0
                || (ReadValue(thread, page, version) && version != PageHeaderVersion))
            {
                return false;
            }

            auto const reference = static_cast<uint16_t>(offset);

            auto const* referenceBytes = reinterpret_cast<uint8_t const*>(&reference);

            RowBufferData.insert(RowBufferData.end(), referenceBytes, referenceBytes + sizeof(reference));

            RowBufferData.insert(RowBufferData.end(), pageId.begin(), pageId.end());

            RowBufferData.insert(RowBufferData.end(), lsn.begin(), lsn.end());

            column.Captured = static_cast<uint32_t>(PageReferenceSize);

            column.Flags |= PageReferenceColumn;

            return true;
        }

        bool IsBatchFunction(uint64_t function) const
        {
            return LogBatches != nullptr && !BatchFunctions.empty() && BatchFunctions.contains(function);
        }

        void BeginBatch(std::vector<StackFrame> const& stack, uint64_t id, uint64_t iterator, uint64_t data)
        {
            if (data < SmallestAddress)
            {
                return;
            }

            BatchStarts[id] = BatchStart{ iterator, data, ConsumerOf(stack, iterator) };
        }

        uint64_t ConsumerOf(std::vector<StackFrame> const& stack, uint64_t iterator) const
        {
            for (auto frame = stack.rbegin(); frame != stack.rend(); ++frame)
            {
                if (frame->Node < 0 || static_cast<size_t>(frame->Node) >= Nodes.size())
                {
                    continue;
                }

                auto const instance = Nodes[frame->Node].Instance;

                if (instance != 0 && instance != iterator)
                {
                    return instance;
                }
            }

            return 0;
        }

        void EndBatch(uint64_t id)
        {
            if (!BatchStarts.empty())
            {
                BatchStarts.erase(id);
            }
        }

        void ReadBatch(IThreadView const& thread, LoggedFunction const& function, uint32_t call, uint64_t id)
        {
            auto const found = BatchStarts.find(id);

            if (found == BatchStarts.end())
            {
                return;
            }

            auto const start = found->second;

            BatchStarts.erase(found);

            uint64_t batch = 0;

            uint64_t ids = 0;

            uint16_t idCount = 0;

            if (!ReadValue(thread, start.Data, batch)
                || !ReadValue(thread, start.Data + BatchIdsOffset, ids)
                || batch < SmallestAddress
                || ids < SmallestAddress
                || !ReadValue(thread, ids, idCount)
                || idCount > MaximumBatchVectors)
            {
                return;
            }

            BatchVectorIds.resize(idCount);

            auto const idBytes = idCount * sizeof(uint16_t);

            if (idCount > 0 && ReadMemory(thread, ids + sizeof(uint16_t), BatchVectorIds.data(), idBytes) != idBytes)
            {
                return;
            }

            CapturedBatch captured{ function.Address,
                                    function.Instance,
                                    start.Iterator,
                                    batch,
                                    start.Consumer,
                                    CallCount,
                                    call,
                                    static_cast<uint32_t>(BatchSelection.size()),
                                    0,
                                    static_cast<uint32_t>(BatchVectors.size()),
                                    0,
                                    0,
                                    0,
                                    0 };

            int32_t capacity = 0;

            if (ReadValue(thread, batch + BatchCapacityOffset, capacity) && capacity > 0 && capacity <= MaximumBatchSelection)
            {
                captured.Capacity = static_cast<uint32_t>(capacity);
            }

            uint64_t vectors = 0;

            uint64_t pure = 0;

            int32_t vectorCount = 0;

            int32_t rowCount = 0;

            uint64_t selection = 0;

            if (!ReadValue(thread, batch + BatchVectorsOffset, vectors)
                || !ReadValue(thread, batch + BatchPureOffset, pure)
                || !ReadValue(thread, batch + BatchVectorCountOffset, vectorCount)
                || !ReadValue(thread, batch + BatchRowCountOffset, rowCount)
                || !ReadValue(thread, batch + BatchSelectionOffset, selection)
                || rowCount < 0
                || rowCount > MaximumBatchSelection
                || vectorCount < 0
                || vectors < SmallestAddress)
            {
                captured.Flags |= UnreadableBatch;

                AddBatch(captured);

                return;
            }

            ReadSelection(thread, selection, rowCount, captured);

            for (auto const vectorId : BatchVectorIds)
            {
                BatchVectors.push_back(ReadVector(thread, vectorId, vectors, pure, vectorCount, captured));
            }

            captured.VectorCount = idCount;

            AddBatch(captured);
        }

        void ReadSelection(IThreadView const& thread, uint64_t selection, int32_t rowCount, CapturedBatch& captured)
        {
            auto const count = static_cast<size_t>(rowCount);

            BatchSelection.resize(captured.FirstSelected + count);

            auto* rows = BatchSelection.data() + captured.FirstSelected;

            if (selection < SmallestAddress)
            {
                for (size_t index = 0; index < count; index++)
                {
                    rows[index] = static_cast<uint16_t>(index);
                }

                captured.Flags |= ImplicitSelection;
            }
            else if (count > 0 && ReadMemory(thread, selection, rows, count * sizeof(uint16_t)) != count * sizeof(uint16_t))
            {
                BatchSelection.resize(captured.FirstSelected);

                captured.Flags |= UnreadableSelection;

                return;
            }

            captured.SelectedCount = static_cast<uint32_t>(count);
        }

        CapturedVector ReadVector(IThreadView const& thread,
                                  uint16_t vectorId,
                                  uint64_t vectors,
                                  uint64_t pure,
                                  int32_t vectorCount,
                                  CapturedBatch const& captured)
        {
            CapturedVector vector{ static_cast<uint32_t>(BatchSlots.size()),
                                   0,
                                   static_cast<uint32_t>(BatchDeepValues.size()),
                                   0,
                                   vectorId,
                                   0,
                                   0 };

            if (vectorId == 0 || vectorId > vectorCount)
            {
                vector.Flags |= MissingVector;

                return vector;
            }

            auto const index = static_cast<uint64_t>(vectorId) - 1;

            uint64_t buffer = 0;

            if (!ReadValue(thread, vectors + index * BatchVectorEntrySize, buffer) || buffer < SmallestAddress)
            {
                vector.Flags |= UnreadableVector;

                return vector;
            }

            uint8_t pureBits = 0;

            if (pure >= SmallestAddress && ReadValue(thread, pure + index / 8, pureBits) && ((pureBits >> (index % 8)) & 1) != 0)
            {
                vector.Flags |= PureVector;

                uint64_t slot = 0;

                if (!ReadValue(thread, buffer, slot))
                {
                    vector.Flags |= UnreadableVector;

                    return vector;
                }

                BatchSlots.push_back(slot);
            }
            else
            {
                ReadSlots(thread, buffer, captured, vector);
            }

            vector.SlotCount = static_cast<uint32_t>(BatchSlots.size()) - vector.FirstSlot;

            for (uint32_t slot = 0; slot < vector.SlotCount; slot++)
            {
                ReadDeepValue(thread, slot, BatchSlots[vector.FirstSlot + slot]);
            }

            vector.DeepValueCount = static_cast<uint32_t>(BatchDeepValues.size()) - vector.FirstDeepValue;

            return vector;
        }

        void ReadSlots(IThreadView const& thread, uint64_t buffer, CapturedBatch const& captured, CapturedVector& vector)
        {
            if (captured.SelectedCount == 0)
            {
                return;
            }

            auto const* selection = BatchSelection.data() + captured.FirstSelected;

            auto const rowCount = static_cast<size_t>(*std::max_element(selection, selection + captured.SelectedCount)) + 1;

            BatchBlock.resize(rowCount);

            auto const readSlots = ReadMemory(thread, buffer, BatchBlock.data(), rowCount * sizeof(uint64_t)) / sizeof(uint64_t);

            for (size_t row = 0; row < rowCount; row++)
            {
                uint64_t slot = 0;

                if (row < readSlots)
                {
                    slot = BatchBlock[row];
                }
                else if (!ReadValue(thread, buffer + row * sizeof(uint64_t), slot))
                {
                    vector.Flags |= UnreadableSlots;
                }

                BatchSlots.push_back(slot);
            }
        }

        void ReadDeepValue(IThreadView const& thread, uint32_t slot, uint64_t value)
        {
            if ((value & 1) == 0 || value <= LargestInlineTag)
            {
                return;
            }

            auto const pointer = value & ~1ull;

            CapturedDeepValue deep{ slot, static_cast<uint32_t>(BatchData.size()), 0, 0 };

            uint16_t length = 0;

            if (pointer >= SmallestAddress && ReadValue(thread, pointer - DeepLengthOffset, length))
            {
                deep.Length = length;

                auto const wanted = static_cast<size_t>((std::min)(uint64_t{ length }, MaximumValueBytes));

                BatchData.resize(deep.Data + wanted);

                deep.Captured = static_cast<uint32_t>(ReadMemory(thread, pointer, BatchData.data() + deep.Data, wanted));

                BatchData.resize(deep.Data + deep.Captured);
            }

            BatchDeepValues.push_back(deep);
        }

        void AddBatch(CapturedBatch const& captured)
        {
            Batches.push_back(captured);

            if (Batches.size() >= BatchChunkBatches || BatchSlots.size() >= BatchChunkSlots || BatchData.size() >= BatchChunkBytes)
            {
                FlushBatches();
            }
        }

        void FlushBatches()
        {
            if (LogBatches == nullptr || Batches.empty())
            {
                return;
            }

            LogBatches(Batches.data(),
                       static_cast<int32_t>(Batches.size()),
                       BatchVectors.data(),
                       static_cast<int32_t>(BatchVectors.size()),
                       BatchSelection.data(),
                       static_cast<int32_t>(BatchSelection.size()),
                       BatchSlots.data(),
                       static_cast<int32_t>(BatchSlots.size()),
                       BatchDeepValues.data(),
                       static_cast<int32_t>(BatchDeepValues.size()),
                       BatchData.data(),
                       static_cast<int32_t>(BatchData.size()));

            Batches.clear();
            BatchVectors.clear();
            BatchSelection.clear();
            BatchSlots.clear();
            BatchDeepValues.clear();
            BatchData.clear();
        }

        void FlushRowBuffers()
        {
            if (LogRowBuffers == nullptr || RowBufferRows.empty())
            {
                return;
            }

            LogRowBuffers(RowBufferRows.data(),
                          static_cast<int32_t>(RowBufferRows.size()),
                          RowBufferColumns.data(),
                          static_cast<int32_t>(RowBufferColumns.size()),
                          RowBufferData.data(),
                          static_cast<int32_t>(RowBufferData.size()));

            RowBufferRows.clear();
            RowBufferColumns.clear();
            RowBufferData.clear();
        }

        uint32_t OperationOf(uint64_t function) const
        {
            auto const found = MemoryMapFunctions.find(function);

            return found == MemoryMapFunctions.end() ? NoOperation : found->second;
        }

        void RecordAddress(uint32_t                       operation,
                           uint64_t                       address,
                           uint64_t                       call,
                           int32_t                        node,
                           uint32_t                       thread,
                           std::vector<StackFrame> const& stack,
                           uint64_t                       stackPointer)
        {
            if (LogMemoryMap == nullptr || address < SmallestAddress)
            {
                return;
            }

            auto const latest = LatestEntries.find(address);

            if (latest != LatestEntries.end() && MemoryMap[latest->second].Until == 0)
            {
                auto& entry = MemoryMap[latest->second];

                if (operation == VisitedOperation && entry.Operation != AllocatedOperation)
                {
                    return;
                }

                if (operation == ConstructedOperation && entry.Operation == ConstructedOperation && IsActive(stack, entry.Call))
                {
                    return;
                }

                if (operation == AllocatedOperation && entry.Operation == AllocatedOperation && entry.Call > call)
                {
                    entry.Call = call;
                    entry.Node = node;

                    return;
                }
            }

            auto const owner = OwnerOf(stack, address, stackPointer);

            auto const index = static_cast<uint32_t>(MemoryMap.size());

            MemoryMap.push_back(MemoryMapEntry{ address, call, owner, 0, node, thread, operation, 0 });

            LatestEntries[address] = index;

            if (owner != 0)
            {
                OwnedEntries[owner].push_back(index);
            }
        }

        void EndScope(uint64_t owner)
        {
            if (owner == 0 || OwnedEntries.empty())
            {
                return;
            }

            auto const found = OwnedEntries.find(owner);

            if (found == OwnedEntries.end())
            {
                return;
            }

            for (auto const index : found->second)
            {
                MemoryMap[index].Until = CallCount;
            }

            OwnedEntries.erase(found);
        }

        void FlushMemoryMap()
        {
            if (LogMemoryMap == nullptr)
            {
                return;
            }

            for (size_t start = 0; start < MemoryMap.size(); start += MemoryMapChunk)
            {
                auto const count = (std::min)(MemoryMapChunk, MemoryMap.size() - start);

                LogMemoryMap(MemoryMap.data() + start, static_cast<int32_t>(count));
            }
        }

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

        std::vector<PointerRead> const* ReadsOf(uint64_t function) const
        {
            if (LogPointerValues == nullptr || Reads.empty())
            {
                return nullptr;
            }

            auto const reads = Reads.find(function);

            return reads == Reads.end() ? nullptr : &reads->second;
        }

        void ReadPointer(IThreadView const&    thread,
                         LoggedFunction const& function,
                         uint32_t              call,
                         PointerRead const&    read,
                         uint64_t              pointer)
        {
            if (pointer == 0)
            {
                return;
            }

            for (uint32_t hop = 0; hop < read.Hops && hop < MaximumHops; hop++)
            {
                uint64_t next = 0;

                auto const link = thread.QueryMemoryBuffer(static_cast<GuestAddress>(pointer + read.Path[hop]),
                                                           BufferView{ &next, sizeof(next) });

                if (link.Memory.Size != sizeof(next) || next == 0)
                {
                    return;
                }

                pointer = next;
            }

            if (read.Mode == ReadTextMode || read.Mode == ReadWideTextMode)
            {
                ReadText(thread, function, call, read, pointer + read.Offset);

                return;
            }

            uint64_t value = 0;

            auto const address = static_cast<GuestAddress>(pointer + read.Offset);

            auto const buffer = thread.QueryMemoryBuffer(address, BufferView{ &value, sizeof(value) });

            auto const known = static_cast<size_t>(buffer.Memory.Size);

            if (read.Mode == ReadBytesMode && known > 0 && known < sizeof(value))
            {
                RecordValue(function, call, read, value & ((1ull << (known * 8)) - 1), static_cast<uint32_t>(known));

                return;
            }

            if (known != sizeof(value))
            {
                return;
            }

            RecordValue(function, call, read, value, read.Mode == ReadBytesMode ? static_cast<uint32_t>(known) : 0);
        }

        void ReadText(IThreadView const& thread, LoggedFunction const& function, uint32_t call, PointerRead const& read, uint64_t address)
        {
            std::array<uint64_t, MaximumTextBytes / sizeof(uint64_t)> words{};

            auto const toPageEnd = PageSize - (address & (PageSize - 1));

            auto length = (std::min)(MaximumTextBytes, static_cast<size_t>(toPageEnd));

            auto buffer = thread.QueryMemoryBuffer(static_cast<GuestAddress>(address), BufferView{ words.data(), length });

            auto bytes = static_cast<size_t>(buffer.Memory.Size);

            if (bytes == length && length < MaximumTextBytes)
            {
                auto const rest = thread.QueryMemoryBuffer(static_cast<GuestAddress>(address + length),
                                                           BufferView{ reinterpret_cast<uint8_t*>(words.data()) + length,
                                                                       MaximumTextBytes - length });

                bytes += static_cast<size_t>(rest.Memory.Size);
            }

            if (bytes == 0)
            {
                return;
            }

            auto const* characters = reinterpret_cast<uint8_t const*>(words.data());

            auto const width = read.Mode == ReadWideTextMode ? size_t{ 2 } : size_t{ 1 };

            auto end = bytes;

            for (size_t index = 0; index + width <= bytes; index += width)
            {
                if (characters[index] == 0 && (width == 1 || characters[index + 1] == 0))
                {
                    end = index + width;

                    break;
                }
            }

            auto const parts = (end + sizeof(uint64_t) - 1) / sizeof(uint64_t);

            for (size_t part = 0; part < parts; part++)
            {
                RecordValue(function, call, read, words[part], static_cast<uint32_t>(part));
            }
        }

        void RecordValue(LoggedFunction const& function, uint32_t call, PointerRead const& read, uint64_t value, uint32_t part = 0)
        {
            PointerValues.push_back(PointerValue{ function.Address,
                                                  function.Instance,
                                                  value,
                                                  call,
                                                  read.Register,
                                                  read.Offset,
                                                  read.IsReadAtReturn,
                                                  read.Mode,
                                                  part });

            if (PointerValues.size() >= PointerValueChunk)
            {
                FlushPointerValues();
            }
        }

        void FlushPointerValues()
        {
            if (LogPointerValues == nullptr || PointerValues.empty())
            {
                return;
            }

            LogPointerValues(PointerValues.data(), static_cast<int32_t>(PointerValues.size()));

            PointerValues.clear();
        }

        void BeginPublish(uint32_t thread, PendingPublish const& publish)
        {
            EndPublishes(thread, publish.StackPointer);

            Publishing[thread].push_back(publish);

            PendingPublishes++;
        }

        void EndPublishes(uint32_t thread, uint64_t stackPointer)
        {
            auto const found = Publishing.find(thread);

            if (found == Publishing.end())
            {
                return;
            }

            auto& publishing = found->second;

            while (!publishing.empty() && publishing.back().StackPointer <= stackPointer)
            {
                publishing.pop_back();

                PendingPublishes--;
            }
        }

        void RecordWrite(uint32_t thread, uint64_t stackPointer, uint64_t buffer)
        {
            EndPublishes(thread, stackPointer);

            auto const found = Publishing.find(thread);

            if (found == Publishing.end() || found->second.empty() || found->second.back().SessionBuffer == buffer)
            {
                return;
            }

            auto& publish = found->second.back();

            publish.SessionBuffer = buffer;

            PublishedEvents.push_back(PublishedEvent{ publish.Publisher,
                                                      buffer,
                                                      static_cast<uint64_t>(publish.Start.Sequence),
                                                      static_cast<uint64_t>(publish.Start.Steps),
                                                      publish.Instructions,
                                                      publish.Node,
                                                      thread });

            if (PublishedEvents.size() >= PublishedEventChunk)
            {
                FlushPublishedEvents();
            }
        }

        void FlushPublishedEvents()
        {
            if (LogPublishedEvents == nullptr || PublishedEvents.empty())
            {
                return;
            }

            LogPublishedEvents(PublishedEvents.data(), static_cast<int32_t>(PublishedEvents.size()));

            PublishedEvents.clear();
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
                       uint64_t             sequence,
                       uint32_t             threadId,
                       uint64_t             target,
                       uint64_t             instance,
                       int32_t              node,
                       uint64_t             id)
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

        values[IdValue] = id;

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

    void CompleteCall(CallTree& tree, StackFrame const& frame, IThreadView const& thread)
    {
        auto* values = frame.Function->Values.data() + frame.Call * ValuesPerCall;

        CROSS_PLATFORM_CONTEXT const context = thread.GetCrossPlatformContext();

        values[ReturnValue] = context.Amd64Context.Rax;

        values[ThreadValue] |= ReturnedFlag;

        if (!tree.RowBufferStarts.empty())
        {
            tree.ReadRowBuffer(thread, *frame.Function, frame.LoggedCall, frame.Id);
        }

        if (!tree.BatchStarts.empty())
        {
            tree.ReadBatch(thread, *frame.Function, frame.LoggedCall, frame.Id);
        }

        auto const* reads = tree.ReadsOf(frame.Function->Address);

        if (reads == nullptr)
        {
            return;
        }

        for (auto const& read : *reads)
        {
            if (read.IsReadAtReturn == 0 || read.Register < 0 || read.Register > ReturnRegister)
            {
                continue;
            }

            auto const pointer = read.Register == ReturnRegister    ? values[ReturnValue]
                                 : read.Register < ArgumentRegisters ? values[IntegerValues + read.Register]
                                                                     : frame.StackArguments[read.Register - ArgumentRegisters];

            tree.ReadPointer(thread, *frame.Function, frame.LoggedCall, read, pointer);
        }
    }

    void LoadStackArguments(IThreadView const&                         thread,
                            uint64_t                                   stackPointer,
                            uint64_t                                   returnAddress,
                            std::array<uint64_t, StackArgumentCount>& stackArguments)
    {
        uint64_t top = 0;

        auto const pushed = thread.QueryMemoryBuffer(static_cast<GuestAddress>(stackPointer), BufferView{ &top, sizeof(top) });

        auto const homeSpace = pushed.Memory.Size == sizeof(top) && top == returnAddress ? stackPointer + sizeof(uint64_t) : stackPointer;

        auto const buffer = thread.QueryMemoryBuffer(static_cast<GuestAddress>(homeSpace + ArgumentRegisters * sizeof(uint64_t)),
                                                     BufferView{ stackArguments.data(), sizeof(stackArguments) });

        auto const read = (std::min)(static_cast<size_t>(buffer.Memory.Size), sizeof(stackArguments)) / sizeof(uint64_t);

        for (auto slot = read; slot < StackArgumentCount; slot++)
        {
            stackArguments[slot] = 0;
        }
    }

    void ReadArguments(CallTree&                                  tree,
                       IThreadView const&                         thread,
                       AMD64_CONTEXT const&                       registers,
                       uint64_t                                   returnAddress,
                       LoggedCall const&                          logged,
                       std::array<uint64_t, StackArgumentCount>& stackArguments)
    {
        auto const* reads = logged.Function == nullptr ? nullptr : tree.ReadsOf(logged.Function->Address);

        if (reads == nullptr)
        {
            return;
        }

        uint64_t const arguments[ArgumentRegisters] = { registers.Rcx, registers.Rdx, registers.R8, registers.R9 };

        auto stackLoaded = false;

        for (auto const& read : *reads)
        {
            if (read.Register < 0 || read.Register >= ArgumentSlots)
            {
                continue;
            }

            if (read.Register >= ArgumentRegisters && !stackLoaded)
            {
                LoadStackArguments(thread, registers.Rsp, returnAddress, stackArguments);

                stackLoaded = true;
            }

            auto const pointer = read.Register < ArgumentRegisters ? arguments[read.Register]
                                                                   : stackArguments[read.Register - ArgumentRegisters];

            if (read.Mode == ReadValueMode)
            {
                tree.RecordValue(*logged.Function, logged.Index, read, pointer);
            }
            else if (read.IsReadAtReturn == 0)
            {
                tree.ReadPointer(thread, *logged.Function, logged.Index, read, pointer);
            }
        }
    }

    void Release(CallTree& tree, StackFrame const& frame, IThreadView const* thread, uint64_t returnTarget)
    {
        if (frame.Call < 0 || frame.Chunk != frame.Function->Chunk)
        {
            return;
        }

        frame.Function->Values[frame.Call * ValuesPerCall + EndValue] = tree.CallCount;

        if (thread != nullptr && frame.ReturnAddress == returnTarget)
        {
            CompleteCall(tree, frame, *thread);
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

            if (returned && frame.Function != nullptr && tree.OperationOf(frame.Function->Address) == AllocatedOperation)
            {
                CROSS_PLATFORM_CONTEXT const context = thread->GetCrossPlatformContext();

                tree.RecordAddress(AllocatedOperation, context.Amd64Context.Rax, frame.Id, frame.Node, threadId, stack, stackPointer);
            }

            tree.RecordSpan(frame, threadId, clock, returned ? SpanReturned : 0);

            Release(tree, frame, thread, returnTarget);

            tree.EndRowBuffer(frame.Id);

            tree.EndBatch(frame.Id);

            tree.EndScope(frame.Id);
        }
    }

    void TrackPublishedEvent(CallTree&                      tree,
                             IThreadView const&             thread,
                             uint32_t                       threadId,
                             uint64_t                       target,
                             uint64_t                       stackPointer,
                             Position const&                position,
                             ThreadClock const&             clock,
                             std::vector<StackFrame> const& stack)
    {
        auto const function = tree.PublishedEventFunctions.find(target);

        if (function == tree.PublishedEventFunctions.end())
        {
            return;
        }

        if (function->second == EventPublisherFunction)
        {
            tree.BeginPublish(threadId, PendingPublish{ target, stackPointer, position, clock.Instructions, NearestNode(stack), 0 });
        }
        else if (tree.PendingPublishes > 0)
        {
            CROSS_PLATFORM_CONTEXT const context = thread.GetCrossPlatformContext();

            tree.RecordWrite(threadId, stackPointer, context.Amd64Context.Rcx);
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

            if (!tree.PublishedEventFunctions.empty())
            {
                TrackPublishedEvent(tree, *thread, threadId, target, stackPointer, position, clock, stack);
            }

            if (!stack.empty() && stack.back().Node == ExcludedNode)
            {
                return;
            }

            auto const returnAddress = static_cast<uint64_t>(fallThroughAddress);

            if (tree.Excluded.contains(target))
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
                                            {},
                                            tree.NextCall() });

                return;
            }

            auto const parent = NearestNode(stack);

            CROSS_PLATFORM_CONTEXT const context = thread->GetCrossPlatformContext();

            auto const& registers = context.Amd64Context;

            auto const method = tree.InstanceMethods.find(target);

            uint64_t const arguments[ArgumentRegisters] = { registers.Rcx, registers.Rdx, registers.R8, registers.R9 };

            auto const instance = method == tree.InstanceMethods.end() ? 0 : arguments[method->second % ArgumentRegisters];

            auto const node = tree.Child(parent, target, instance);

            tree.Nodes[node].Calls++;

            auto const id = tree.NextCall();

            auto const logged = LogCall(tree,
                                        registers,
                                        static_cast<uint64_t>(position.Sequence),
                                        threadId,
                                        target,
                                        instance,
                                        node,
                                        id);

            if (auto const operation = tree.OperationOf(target); operation == ConstructedOperation || operation == VisitedOperation)
            {
                tree.RecordAddress(operation, registers.Rcx, id, node, threadId, stack, stackPointer);
            }

            std::array<uint64_t, StackArgumentCount> stackArguments{};

            ReadArguments(tree, *thread, registers, returnAddress, logged, stackArguments);

            if (auto const instanceRegister = tree.RowBufferRegisterOf(target);
                instanceRegister >= 0 && logged.Function != nullptr)
            {
                tree.BeginRowBuffer(*thread, id, arguments[instanceRegister]);
            }

            if (logged.Function != nullptr && tree.IsBatchFunction(target))
            {
                tree.BeginBatch(stack, id, registers.Rcx, registers.Rdx);
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
                                        stackArguments,
                                        id });

            if (tree.LoggedSinceFlush >= IdleFlushInterval)
            {
                FlushIdle(tree);
            }

            return;
        }

        if (tree.PendingPublishes > 0)
        {
            tree.EndPublishes(threadId, stackPointer + sizeof(uint64_t));
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
                                        SpanStartUnknown,
                                        {},
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

            tree.EndPublishes(threadId, UINT64_MAX);

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

    int32_t ReadCallTree(void*                  trace,
                         const uint32_t*        threadIds,
                         int32_t                threadCount,
                         const uint64_t*        instanceMethods,
                         int32_t                instanceMethodCount,
                         const uint64_t*        excludedFunctions,
                         int32_t                excludedFunctionCount,
                         const PointerRead*     pointerReads,
                         int32_t                pointerReadCount,
                         const uint64_t*        eventPublisherFunctions,
                         int32_t                eventPublisherFunctionCount,
                         const uint64_t*        eventWriterFunctions,
                         int32_t                eventWriterFunctionCount,
                         const MemoryMapFunction* memoryMapFunctions,
                         int32_t                memoryMapFunctionCount,
                         const RowBufferRead*   rowBufferReads,
                         int32_t                rowBufferReadCount,
                         const RowBufferLayout* rowBufferLayouts,
                         int32_t                rowBufferLayoutCount,
                         const uint64_t*        batchFunctions,
                         int32_t                batchFunctionCount,
                         CallChunkCallback      logCalls,
                         CallSpanCallback       logSpans,
                         PointerValueCallback   logPointerValues,
                         PublishedEventCallback logPublishedEvents,
                         MemoryMapCallback      logMemoryMap,
                         RowBufferCallback      logRowBuffers,
                         BatchCallback          logBatches,
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

        for (int32_t index = 0; index < pointerReadCount; index++)
        {
            result->Reads[pointerReads[index].Function].push_back(pointerReads[index]);
        }

        result->LogCalls = logCalls;

        result->LogSpans = logSpans;

        result->LogPointerValues = logPointerValues;

        result->LogPublishedEvents = logPublishedEvents;

        result->LogMemoryMap = logMemoryMap;

        for (int32_t index = 0; index < memoryMapFunctionCount && logMemoryMap != nullptr; index++)
        {
            result->MemoryMapFunctions[memoryMapFunctions[index].Address] = memoryMapFunctions[index].Operation;
        }

        result->LogRowBuffers = logRowBuffers;

        for (int32_t index = 0; index < rowBufferReadCount && logRowBuffers != nullptr; index++)
        {
            auto const& read = rowBufferReads[index];

            if (read.Register >= 0 && read.Register < ArgumentRegisters)
            {
                result->RowBufferReads[read.Function] = read.Register;
            }
        }

        for (int32_t index = 0; index < rowBufferLayoutCount && logRowBuffers != nullptr; index++)
        {
            result->RowBufferLayouts[rowBufferLayouts[index].VTable] = rowBufferLayouts[index];
        }

        result->LogBatches = logBatches;

        for (int32_t index = 0; index < batchFunctionCount && logBatches != nullptr; index++)
        {
            result->BatchFunctions.insert(batchFunctions[index]);
        }

        for (int32_t index = 0; index < eventPublisherFunctionCount && logPublishedEvents != nullptr; index++)
        {
            result->PublishedEventFunctions[eventPublisherFunctions[index]] = EventPublisherFunction;
        }

        for (int32_t index = 0; index < eventWriterFunctionCount && logPublishedEvents != nullptr; index++)
        {
            result->PublishedEventFunctions[eventWriterFunctions[index]] = EventWriterFunction;
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
            }
        }

        result->FlushSpans();

        result->FlushPointerValues();

        result->FlushPublishedEvents();

        result->FlushMemoryMap();

        result->FlushRowBuffers();

        result->FlushBatches();

        result->MemoryMap = {};
        result->LatestEntries.clear();
        result->OwnedEntries.clear();

        result->RowBufferRows = {};
        result->RowBufferColumns = {};
        result->RowBufferData = {};
        result->BindingSets.clear();
        result->RowBufferStarts.clear();

        result->Batches = {};
        result->BatchVectors = {};
        result->BatchSelection = {};
        result->BatchSlots = {};
        result->BatchDeepValues = {};
        result->BatchData = {};
        result->BatchVectorIds = {};
        result->BatchBlock = {};
        result->BatchStarts.clear();

        result->Spans = {};
        result->PointerValues = {};
        result->PublishedEvents = {};
        result->Publishing.clear();
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
