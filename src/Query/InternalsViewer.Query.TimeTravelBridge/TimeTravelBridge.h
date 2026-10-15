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
        uint32_t IsReadAtReturn;
        uint32_t Offset;
        uint32_t Hops;
        uint32_t Path[MaximumHops];
        uint32_t Mode;
    };

    struct PointerValue
    {
        uint64_t Function;
        uint64_t Instance;
        uint64_t Value;
        uint32_t Call;
        int32_t  Register;
        uint32_t Offset;
        uint32_t IsReadAtReturn;
        uint32_t Mode;
        uint32_t Part;
    };

    typedef void(__stdcall* ProgressCallback)(uint32_t thread, int32_t percent);

    typedef void(__stdcall* CallChunkCallback)(uint64_t address, uint64_t instance, const uint64_t* columns, int32_t calls);

    typedef void(__stdcall* CallSpanCallback)(const CallSpan* spans, int32_t count);

    struct PublishedEvent
    {
        uint64_t Publisher;
        uint64_t SessionBuffer;
        uint64_t Sequence;
        uint64_t Steps;
        uint64_t Instructions;
        int32_t  Node;
        uint32_t Thread;
    };

    struct MemoryMapFunction
    {
        uint64_t Address;
        uint32_t Operation;
        uint32_t Reserved;
    };

    struct MemoryMapEntry
    {
        uint64_t Address;
        uint64_t Call;
        uint64_t Owner;
        uint64_t Until;
        int32_t  Node;
        uint32_t Thread;
        uint32_t Operation;
        uint32_t Reserved;
    };

    struct RowBufferRead
    {
        uint64_t Function;
        int32_t  Register;
        uint32_t Reserved;
    };

    struct RowBufferLayout
    {
        uint64_t VTable;
        uint32_t BufferOffset;
        uint32_t BindingsOffset;
        uint32_t CountOffset;
        uint32_t Format;
    };

    struct RowBufferRow
    {
        uint64_t Function;
        uint64_t Instance;
        uint64_t Iterator;
        uint64_t Buffer;
        uint64_t Id;
        uint32_t Call;
        uint32_t FirstColumn;
        uint32_t ColumnCount;
        uint32_t Format;
    };

    struct RowBufferColumn
    {
        uint32_t Data;
        uint32_t Length;
        uint32_t Captured;
        int32_t  RecordOffset;
        uint32_t BindingFlags;
        uint16_t FixedLength;
        uint8_t  Status;
        uint8_t  Flags;
    };

    struct CapturedBatch
    {
        uint64_t Function;
        uint64_t Instance;
        uint64_t Iterator;
        uint64_t Batch;
        uint64_t Consumer;
        uint64_t Id;
        uint32_t Call;
        uint32_t FirstSelected;
        uint32_t SelectedCount;
        uint32_t FirstVector;
        uint32_t VectorCount;
        uint32_t Flags;
        uint32_t Capacity;
        uint32_t Reserved;
    };

    struct CapturedVector
    {
        uint32_t FirstSlot;
        uint32_t SlotCount;
        uint32_t FirstDeepValue;
        uint32_t DeepValueCount;
        uint16_t VectorId;
        uint16_t Flags;
        uint32_t Reserved;
    };

    struct CapturedDeepValue
    {
        uint32_t Slot;
        uint32_t Data;
        uint32_t Length;
        uint32_t Captured;
    };

    typedef void(__stdcall* PointerValueCallback)(const PointerValue* values, int32_t count);

    typedef void(__stdcall* MemoryMapCallback)(const MemoryMapEntry* entries, int32_t count);

    typedef void(__stdcall* RowBufferCallback)(const RowBufferRow*    rows,
                                               int32_t                rowCount,
                                               const RowBufferColumn* columns,
                                               int32_t                columnCount,
                                               const uint8_t*         data,
                                               int32_t                dataLength);

    typedef void(__stdcall* BatchCallback)(const CapturedBatch*     batches,
                                           int32_t                  batchCount,
                                           const CapturedVector*    vectors,
                                           int32_t                  vectorCount,
                                           const uint16_t*          selection,
                                           int32_t                  selectionCount,
                                           const uint64_t*          slots,
                                           int32_t                  slotCount,
                                           const CapturedDeepValue* deepValues,
                                           int32_t                  deepValueCount,
                                           const uint8_t*           data,
                                           int32_t                  dataLength);

    typedef void(__stdcall* PublishedEventCallback)(const PublishedEvent* publishedEvents, int32_t count);

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
                                               void**                 tree);

    __declspec(dllexport) int32_t GetCallNodeCount(void* tree);

    __declspec(dllexport) void GetCallNodes(void* tree, CallNode* nodes, int32_t count);

    __declspec(dllexport) void CloseCallTree(void* tree);

    __declspec(dllexport) void CloseTrace(void* trace);
}
