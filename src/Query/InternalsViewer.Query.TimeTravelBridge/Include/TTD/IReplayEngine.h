// Copyright (c) Microsoft Corporation.
//
// This file describes the interface exposed by TTD's Replay Engine
//
// Concepts:
//
// Trace file: Contains all, or portions, of the execution of a single process between two points of time.
//  - This includes:
//      - CPU instructions executed.
//      - Approximate contents of the process' memory.
//      - Relevant events, like exceptions, debugger console output, DLL load/unload, thread creation/termination.
// Timeline: It's the list of all of the CPU instructions executed between the begin and end points of time
//  recorded in the trace. All other events recorded are each attached to a single instruction execution.
// Breakpoints: User-specified points of interest in the recorded process.
//  - Specified on:
//      - Specific events, like exceptions.
//      - Specific code locations.
//      - Memory location watchpoints, for reading or for writing.
//  - Breakpoints could have filters attached, like a particular thread, to reduce noise.
//  - Breakpoints are specified generally (this code location or that memory location) but,
//    when applied to the timeline, they implicitly generate a list of all positions where the breakpoint "hits".
//
// API elements:
//
// Position: 128-bit number that identifies a single instruction executed in the trace's timeline.
//  - Positions are monotonically increasing, and so can be compared numerically.
//    Positions have numerical gaps. Positions obtained from a RNG might not be valid.
//    The IReplayEngine interface can identify invalid positions, and provide valid positions from invalid ones.
// IReplayEngine: This is the main replay engine interface.
//  - It can load a trace file, and provide global information about it.
//  - Examples of things you should be able to query from it:
//      - PEB pointer.
//      - List of threads througout the trace's timeline. (Note: OS thread IDs may get re-used by more than one thread)
//      - List of modules loaded/unloaded throughout the timeline.
//      - Other events of interest, like exceptions and message markers.
//      - We plan to offer some fine-grained event information, like a list of all function returns
//        with the returned value (EAX). Either as iterable events, or through specific query APIs, or both.
//      - Useful timeline queries, Possible examples:
//          - Position before/after a given one.
//          - Position XX% through the trace.
//          - Position of the next context switch after a given position.
//          - Valid position that's closest to any given, possibly invalid, position.
//          - Count of instructions executed globally, or on a specific thread, between two given positions.
//  - It also allows you to create cursors to move through the timeline.
// ICursor: Provides access to a single focus position in the timeline, called the current position.
//  - It allows the user to query information contextual to the current position:
//      - Identify the current thread.
//      - List the active threads, including the current positions on those threads.
//      - Get CPU registers of any active thread.
//      - Get contents of memory.
//      - List the modules currently loaded & unloaded.
//  - It tells the replay engine that the user is interested in the current position in the trace,
//    allowing it to cache data to optimize the contextual queries around this point.
//  - It can easily be moved to new positions, by executing forward or backward, or by jumping to any given position.
//  - When execution is initiated on a cursor, a set of breakpoints may be considered "active". In that case,
//    execution will stop at the first breakpoint hit in the direction of execution (forwards or backwards),
//    and the position of that hit becomes the new current position of the cursor.
//  - It can be efficiently cloned, to allow speculative/exploratory executions from the current position.
//  - It also allows the engine to more quickly create new cursors, or move existing ones,
//    to the immediate vicinity of the current position, especially forward of it.
//
// Lingering API questions:
//
// Breakpoints:
//  - The main question is... should breakpoints be specified globally through IReplayEngine? Or should they be
//    local to a single cursor? Or maybe both?
//  - If specified globally, should the cursor be able to locally turn on/off, any specific global breakpoint?
// Indexing: how much happens under the hood in the engine, vs. how much is exposed through the API to the indexer.

#pragma once

#include "IdnaBasicTypes.h"
#include <guiddef.h>
#include <cwchar>
#include <algorithm>
#include <string>
#include <tuple>

#if _MSVC_LANG >= 202002L
#include <span>
#endif

// This file includes inline functions, which want to include assertions.
// We specify the following assertion macros: DBG_ASSERT and DBG_ASSERT_MSG.
// DBG_ASSERT takes just a condition. DBG_ASSERT_MSG an additional message string, and arguments.
// The user must define these macros to fit their application's environment.
#pragma push_macro("DBG_ASSERT")
#pragma push_macro("DBG_ASSERT_MSG")

#ifndef DBG_ASSERT
#pragma message("The user of this file needs to define DBG_ASSERT(cond) in whichever manner they prefer.")
#define DBG_ASSERT(cond) do {} while(false)
#endif
#ifndef DBG_ASSERT_MSG
#define DBG_ASSERT_MSG(cond, ...) DBG_ASSERT(cond)
#endif

// Defined in IReplayEngineRegisters.h. Forward-declared here to minimize header dependencies.
// Use #include <IReplayEngineRegisters.h> if needed.
typedef struct _CROSS_PLATFORM_CONTEXT CROSS_PLATFORM_CONTEXT;
typedef struct _AVX_EXTENDED_CONTEXT   AVX_EXTENDED_CONTEXT;

namespace TTD
{

class ErrorReporting;

// Callback used by the engine to report errors encountered
using ClientErrorReportCallback [[deprecated]] = void(__stdcall*)(_In_opt_z_ wchar_t const* pMessage);

namespace Replay
{

// TODO: We can't exactly use UUIDs on Linux, presumably, so figure out a different way.

// Non-owning interface pointers.
// Note that these interfaces don't allow initialization or destruction, only operation of the class,
// which helps clarify ownership semantics for code that only needs to operate on an engine provided to it.
class __declspec(uuid("{B1D2E6AB-9052-4B72-999E-A88BA868F899}")) ICursorView;
class __declspec(uuid("{4D3420A5-37EF-4114-AE91-63D0378C84A9}")) IReplayEngineView;
class __declspec(uuid("{2DBF3602-669F-490A-962C-749D91C3A1A4}")) ITraceListView;

// Owning interface pointers.
// These allow destruction of the class.
class ICursor;
class IReplayEngine;
class ITraceList;

// Debugging/testing interfaces, defined elsewhere.
class __declspec(uuid("{A1529168-71AB-4BC5-9FE0-184F2520088B}")) ICursorInternals;
class __declspec(uuid("{05900C1F-547A-40B2-A66D-4B9639618556}")) IEngineInternals;

using TTD::SystemInfo; // For backcompat. This used to be defined here.

// Structure containing all registers.
union RegisterContext
{
    // Note: empty for now. Will populate with the appropriate fields.
    struct {} X86;
    struct {} X64;
    struct {} Arm;
    struct {} Arm64;

    // For source backcompat, fix the correct size and alignment.
    uint64_t Data[2672 / sizeof(uint64_t)];

    // For source backcompat, allow this to be converted to the Windows debugger's register structure.
    operator CROSS_PLATFORM_CONTEXT() const noexcept;
};

// Structure containing extended registers (like extra SIMD registers).
union ExtendedRegisterContext
{
    // Note: empty for now. Will populate with the appropriate fields.
    struct {} X86;
    struct {} X64;
    struct {} Arm;
    struct {} Arm64;

    // For source backcompat, fix the correct size and alignment.
    uint64_t Data[8832 / sizeof(uint64_t)];

    // For source backcompat, allow this to be converted to the Windows debugger's register structure.
    operator AVX_EXTENDED_CONTEXT() const noexcept;
};

// Represents an individual module used during the trace file's timeline.
// Modules are identified by their size, checksum and timestamp values, and also by their base address.
// The same module, but loaded in two different addresses, is represented by two distinct 'Module' objects.
struct Module
{
    wchar_t const* pName;       // The name is for informational purposes only. It's not involved in comparison operators.
    size_t         NameLength;
    GuestAddress   Address;
    uint64_t       Size;
    uint32_t       Checksum;
    uint32_t       Timestamp;

    friend constexpr auto AsComparisonTuple(Module const& m) { return std::tie(m.Address, m.Timestamp, m.Checksum, m.Size); }

    friend constexpr bool operator==(Module const& a, Module const& b) { return AsComparisonTuple(a) == AsComparisonTuple(b); }
    friend constexpr bool operator!=(Module const& a, Module const& b) { return AsComparisonTuple(a) != AsComparisonTuple(b); }
    friend constexpr bool operator< (Module const& a, Module const& b) { return AsComparisonTuple(a) <  AsComparisonTuple(b); }
    friend constexpr bool operator> (Module const& a, Module const& b) { return AsComparisonTuple(a) >  AsComparisonTuple(b); }
    friend constexpr bool operator<=(Module const& a, Module const& b) { return AsComparisonTuple(a) <= AsComparisonTuple(b); }
    friend constexpr bool operator>=(Module const& a, Module const& b) { return AsComparisonTuple(a) >= AsComparisonTuple(b); }
};

inline size_t ModuleToString(
    _In_                 Module   const& module,
    _Out_writes_z_(size) wchar_t* const  pBuff,
    _In_                 size_t   const  size)
{
    return _snwprintf_s(pBuff, size, _TRUNCATE, L"Module %ls at address 0X%llX with size %llu", module.pName, module.Address, module.Size);
}

// Represents an individual instance (load/unload lifetime) of a module.
// A module could be loaded/unloaded on the same address multiple times during the timeline,
// resulting in multiple of these instances.
// Note that we use the module pointers in the comparison operators.
// When the ModuleInstance is obtained from the engine, the engine guarantees that
// the resulting pointer comparisons are equivalent to comparing the module objects themselves.
struct ModuleInstance
{
    Module const* pModule;
    SequenceId    LoadTime;
    SequenceId    UnloadTime;

    friend constexpr auto AsComparisonTuple(ModuleInstance const& m) { return std::tie(m.LoadTime, m.pModule); }

    friend constexpr bool operator==(ModuleInstance const& a, ModuleInstance const& b) { return AsComparisonTuple(a) == AsComparisonTuple(b); }
    friend constexpr bool operator!=(ModuleInstance const& a, ModuleInstance const& b) { return AsComparisonTuple(a) != AsComparisonTuple(b); }
    friend constexpr bool operator< (ModuleInstance const& a, ModuleInstance const& b) { return AsComparisonTuple(a) <  AsComparisonTuple(b); }
    friend constexpr bool operator> (ModuleInstance const& a, ModuleInstance const& b) { return AsComparisonTuple(a) >  AsComparisonTuple(b); }
    friend constexpr bool operator<=(ModuleInstance const& a, ModuleInstance const& b) { return AsComparisonTuple(a) <= AsComparisonTuple(b); }
    friend constexpr bool operator>=(ModuleInstance const& a, ModuleInstance const& b) { return AsComparisonTuple(a) >= AsComparisonTuple(b); }
};

// Uniquely identifies each one of the threads found in the trace file.
enum class UniqueThreadId : uint32_t
{
    Invalid = 0,
    Min     = 1,
    Max     = uint32_t(-1),
};

// Expresses how much debugging validation should be done within the replay engine.
enum class DebugModeType
{
    // No debugging.
    None,

    // Currently, no debugging.
    Medium,

    // Report all validation errors encountered in the trace, insted of just the first in each segment.
    High,
};

enum class [[deprecated]] DebugEvent
{
    Read,
    Write,
    SingleInstruction,
    InstructionBytes,
    GeneralExecutionState,
    Message,
    ValidationDerailment,
    MissingDataDerailment,
    IncorrectPacketDerailment,
    CountedPacketOutOfOrder,
    EmulatorDerailment
};

#pragma warning(push)
#pragma warning(disable: 4996) // '': was declared deprecated
[[deprecated]] inline char const* GetDebugEventName(_In_ DebugEvent const event)
{
    switch (event)
    {
    case DebugEvent::Read                     : return "Read";
    case DebugEvent::Write                    : return "Write";
    case DebugEvent::SingleInstruction        : return "SingleInstruction";
    case DebugEvent::InstructionBytes         : return "InstructionBytes";
    case DebugEvent::GeneralExecutionState    : return "GeneralExecutionState";
    case DebugEvent::Message                  : return "Message";
    case DebugEvent::ValidationDerailment     : return "ValidationDerailment";
    case DebugEvent::MissingDataDerailment    : return "MissingDataDerailment";
    case DebugEvent::IncorrectPacketDerailment: return "IncorrectPacketDerailment";
    case DebugEvent::CountedPacketOutOfOrder  : return "CountedPacketOutOfOrder";
    case DebugEvent::EmulatorDerailment       : return "EmulatorDerailment";
    default                                   : return "<Unknown>";
    }
}

struct [[deprecated]] LoggingInformation
{
    SequenceId       SequenceId;
    GuestAddress     Address;
    uint64_t         GuestContextHash;
    InstructionCount InstructionCount;
    ThreadId         ThreadId;
    UniqueThreadId   UniqueThreadId;

    void const*  pData;
    size_t       DataSize;
};

// Callback used by the engine to report details about replay
// When/What details are reported is based on the debug mode set for the engine
using ClientLoggingCallback [[deprecated]] = void(__stdcall*)(
    _In_       LoggingInformation const&,
    _In_       DebugEvent,
    _In_opt_z_ wchar_t const*
    );
#pragma warning(pop)

// Values representing the different recording types
enum class RecordingType : uint32_t
{
    Invalid   = 0,
    Full      = 1,
    Selective = 2,
    Chunk     = 3,
};

inline char const* GetRecordingTypeName(_In_ RecordingType const type)
{
    switch (type)
    {
    case RecordingType::Invalid   : return "Invalid";
    case RecordingType::Full      : return "Full";
    case RecordingType::Selective : return "Selective";
    case RecordingType::Chunk     : return "Chunk";
    default                       : return "<Unknown>";
    }
}

// Steps are atomic advances in the execution of a thread.
// Most steps are single instructions executed, but there are other events that don't execute instructions,
// like an exception being thrown, or a context update when the thread is suspended.
enum class StepCount : uint64_t
{
    Zero    = 0,
    Min     = 0,
    Max     = uint64_t(-2),

    Invalid = uint64_t(-1),
};

constexpr StepCount ToStepCount(_In_ InstructionCount const count) { return static_cast<StepCount>(static_cast<uint64_t>(count)); }

constexpr StepCount operator+(_In_ StepCount a, _In_ uint64_t  b) { return static_cast<StepCount>(static_cast<uint64_t>(a) + b); }
constexpr StepCount operator-(_In_ StepCount a, _In_ uint64_t  b) { return static_cast<StepCount>(static_cast<uint64_t>(a) - b); }
constexpr StepCount operator/(_In_ StepCount a, _In_ uint64_t  b) { return static_cast<StepCount>(static_cast<uint64_t>(a) / b); }
constexpr uint64_t  operator%(_In_ StepCount a, _In_ uint64_t  b) { return                        static_cast<uint64_t>(a) % b ; }
constexpr StepCount operator+(_In_ StepCount a, _In_ StepCount b) { return a + static_cast<uint64_t>(b); }
constexpr StepCount operator-(_In_ StepCount a, _In_ StepCount b) { return a - static_cast<uint64_t>(b); }

constexpr bool operator==(_In_ StepCount a, _In_ uint64_t b) { return static_cast<uint64_t>(a) == b; }
constexpr bool operator!=(_In_ StepCount a, _In_ uint64_t b) { return static_cast<uint64_t>(a) != b; }
constexpr bool operator< (_In_ StepCount a, _In_ uint64_t b) { return static_cast<uint64_t>(a) <  b; }
constexpr bool operator> (_In_ StepCount a, _In_ uint64_t b) { return static_cast<uint64_t>(a) >  b; }
constexpr bool operator<=(_In_ StepCount a, _In_ uint64_t b) { return static_cast<uint64_t>(a) <= b; }
constexpr bool operator>=(_In_ StepCount a, _In_ uint64_t b) { return static_cast<uint64_t>(a) >= b; }

inline StepCount& operator+=(_Inout_ StepCount& a, _In_ int64_t   b) { return a = static_cast<StepCount>(static_cast<uint64_t>(a) + b); }
inline StepCount& operator-=(_Inout_ StepCount& a, _In_ int64_t   b) { return a = static_cast<StepCount>(static_cast<uint64_t>(a) - b); }
inline StepCount& operator+=(_Inout_ StepCount& a, _In_ StepCount b) { return a += static_cast<uint64_t>(b); }
inline StepCount& operator-=(_Inout_ StepCount& a, _In_ StepCount b) { return a -= static_cast<uint64_t>(b); }

// Instructions are steps, but steps are not necessarily instructions, so the two safely mix in a limited set of ways.

constexpr StepCount  operator+ (_In_    StepCount  a, _In_ InstructionCount b) { return a + static_cast<uint64_t>(b); }
constexpr StepCount  operator- (_In_    StepCount  a, _In_ InstructionCount b) { return a - static_cast<uint64_t>(b); }
constexpr StepCount& operator+=(_Inout_ StepCount& a, _In_ InstructionCount b) { return a = a + static_cast<uint64_t>(b); }
constexpr StepCount& operator-=(_Inout_ StepCount& a, _In_ InstructionCount b) { return a = a - static_cast<uint64_t>(b); }

constexpr StepCount  operator+ (_In_ InstructionCount a, _In_ StepCount b) { return b + static_cast<uint64_t>(a); }

constexpr bool operator==(_In_ StepCount a, _In_ InstructionCount b) { return static_cast<uint64_t>(a) == static_cast<uint64_t>(b); }
constexpr bool operator!=(_In_ StepCount a, _In_ InstructionCount b) { return static_cast<uint64_t>(a) != static_cast<uint64_t>(b); }
constexpr bool operator< (_In_ StepCount a, _In_ InstructionCount b) { return static_cast<uint64_t>(a) <  static_cast<uint64_t>(b); }
constexpr bool operator> (_In_ StepCount a, _In_ InstructionCount b) { return static_cast<uint64_t>(a) >  static_cast<uint64_t>(b); }
constexpr bool operator<=(_In_ StepCount a, _In_ InstructionCount b) { return static_cast<uint64_t>(a) <= static_cast<uint64_t>(b); }
constexpr bool operator>=(_In_ StepCount a, _In_ InstructionCount b) { return static_cast<uint64_t>(a) >= static_cast<uint64_t>(b); }

constexpr bool operator==(_In_ InstructionCount a, _In_ StepCount b) { return static_cast<uint64_t>(a) == static_cast<uint64_t>(b); }
constexpr bool operator!=(_In_ InstructionCount a, _In_ StepCount b) { return static_cast<uint64_t>(a) != static_cast<uint64_t>(b); }
constexpr bool operator< (_In_ InstructionCount a, _In_ StepCount b) { return static_cast<uint64_t>(a) <  static_cast<uint64_t>(b); }
constexpr bool operator> (_In_ InstructionCount a, _In_ StepCount b) { return static_cast<uint64_t>(a) >  static_cast<uint64_t>(b); }
constexpr bool operator<=(_In_ InstructionCount a, _In_ StepCount b) { return static_cast<uint64_t>(a) <= static_cast<uint64_t>(b); }
constexpr bool operator>=(_In_ InstructionCount a, _In_ StepCount b) { return static_cast<uint64_t>(a) >= static_cast<uint64_t>(b); }

struct Position
{
                 constexpr Position() = default;
    /*implicit*/ constexpr Position(_In_ SequenceId const sequence)                             : Sequence(sequence)               {}
                 constexpr Position(_In_ SequenceId const sequence, _In_ StepCount const steps) : Sequence(sequence), Steps(steps) {}

    SequenceId Sequence = SequenceId::Invalid;
    StepCount  Steps    = StepCount::Zero;

    constexpr bool IsValid() const { return Sequence != SequenceId::Invalid; }

    inline Position& operator+=(int64_t   increment) { Steps += increment; return *this; }
    inline Position& operator+=(StepCount increment) { Steps += increment; return *this; }

    static Position const Invalid;
    static Position const Min;
    static Position const Max;

    friend constexpr auto AsComparisonTuple(Position const& p) { return std::tie(p.Sequence, p.Steps); }

    friend constexpr bool operator==(Position const& a, Position const& b) { return AsComparisonTuple(a) == AsComparisonTuple(b); }
    friend constexpr bool operator!=(Position const& a, Position const& b) { return AsComparisonTuple(a) != AsComparisonTuple(b); }
    friend constexpr bool operator< (Position const& a, Position const& b) { return AsComparisonTuple(a) <  AsComparisonTuple(b); }
    friend constexpr bool operator> (Position const& a, Position const& b) { return AsComparisonTuple(a) >  AsComparisonTuple(b); }
    friend constexpr bool operator<=(Position const& a, Position const& b) { return AsComparisonTuple(a) <= AsComparisonTuple(b); }
    friend constexpr bool operator>=(Position const& a, Position const& b) { return AsComparisonTuple(a) >= AsComparisonTuple(b); }
};

__declspec(selectany) Position const Position::Invalid = { SequenceId::Invalid, StepCount::Zero };
__declspec(selectany) Position const Position::Min     = { SequenceId::Min    , StepCount::Min  };
__declspec(selectany) Position const Position::Max     = { SequenceId::Max    , StepCount::Max  };

constexpr Position operator+(_In_ Position const& position, uint64_t increment)
{
    if (position.Steps + increment < position.Steps)
    {
        return { position.Sequence + 1, position.Steps + increment };
    }
    else
    {
        return { position.Sequence, position.Steps + increment };
    }
}

constexpr Position operator-(_In_ Position const& position, uint64_t increment)
{
    if (position.Steps - increment > position.Steps)
    {
        return { position.Sequence - 1, position.Steps - increment };
    }
    else
    {
        return { position.Sequence, position.Steps - increment };
    }
}

constexpr Position operator+(_In_ Position const& position, StepCount increment) { return position + static_cast<uint64_t>(increment); }
constexpr Position operator-(_In_ Position const& position, StepCount increment) { return position + static_cast<uint64_t>(increment); }

inline size_t PositionToString(_In_ Position const& position, _Out_writes_z_(size) wchar_t* pBuff, _In_ size_t size)
{
    if (position == Position::Invalid)
    {
        return _snwprintf_s(pBuff, size, _TRUNCATE, L"Invalid Position");
    }
    if (position == Position::Min)
    {
        return _snwprintf_s(pBuff, size, _TRUNCATE, L"Min Position");
    }
    if (position == Position::Max)
    {
        return _snwprintf_s(pBuff, size, _TRUNCATE, L"Max Position");
    }
    return _snwprintf_s(pBuff, size, _TRUNCATE, L"%llX:%llX", position.Sequence, position.Steps);
}

// A position range is a closed range, both min and max are valid positions on the trace
struct PositionRange
{
    constexpr PositionRange() = default;
    constexpr PositionRange(_In_ Position const minimum, _In_ Position const maximum) : Min(minimum), Max(maximum) {}

    constexpr bool IsValid() const { return Min.IsValid() && Max.IsValid(); }

    Position Min = Position::Invalid;
    Position Max = Position::Invalid;

    static PositionRange const Invalid;

    friend constexpr auto AsComparisonTuple(PositionRange const& r) { return std::tie(r.Min, r.Max); }

    friend constexpr bool operator==(PositionRange const& a, PositionRange const& b) { return AsComparisonTuple(a) == AsComparisonTuple(b); }
    friend constexpr bool operator!=(PositionRange const& a, PositionRange const& b) { return AsComparisonTuple(a) != AsComparisonTuple(b); }
    friend constexpr bool operator< (PositionRange const& a, PositionRange const& b) { return AsComparisonTuple(a) <  AsComparisonTuple(b); }
    friend constexpr bool operator> (PositionRange const& a, PositionRange const& b) { return AsComparisonTuple(a) >  AsComparisonTuple(b); }
    friend constexpr bool operator<=(PositionRange const& a, PositionRange const& b) { return AsComparisonTuple(a) <= AsComparisonTuple(b); }
    friend constexpr bool operator>=(PositionRange const& a, PositionRange const& b) { return AsComparisonTuple(a) >= AsComparisonTuple(b); }
};

__declspec(selectany) PositionRange const PositionRange::Invalid = { Position::Invalid, Position::Invalid };

constexpr bool PositionInClosedRange(_In_ PositionRange const& range, _In_ Position const& position)
{
    return position >= range.Min && position <= range.Max;
}

constexpr bool PositionInHalfOpenRange(_In_ PositionRange const& range, _In_ Position const& position)
{
    return position >= range.Min && position < range.Max;
}

inline size_t PositionRangeToString(
    _In_                 PositionRange const& range,
    _Out_writes_z_(size) wchar_t*      const  pBuff,
    _In_                 size_t        const  size)
{
    return _snwprintf_s(pBuff, size, _TRUNCATE, L"[%llX:%llX, %llX:%llX]",
        range.Min.Sequence,
        range.Min.Steps,
        range.Max.Sequence,
        range.Max.Steps
    );
}

enum class GapKind : uint8_t
{
    NoGap        , // There was a potential gap, but turned out not to be a gap (as in fallbacks).
    ContextSwitch, // No code was skipped in this thread, but potentially arbitrary amount of code run on other threads.
    Unrecorded   , // Some code on this thread was skipped, like a recorder internal pause, or a syscall.
    Large        , // Large gap between islands. The stack buffer may have been unrolled arbitrarily.
};

constexpr char const* GetGapKindName(_In_ GapKind const kind)
{
    switch (kind)
    {
    case GapKind::NoGap        : return "NoGap";
    case GapKind::ContextSwitch: return "ContextSwitch";
    case GapKind::Unrecorded   : return "Unrecorded";
    case GapKind::Large        : return "Large";
    default                    : return "<Unknown GapKind>";
    }
}

enum class GapKindMask : uint32_t;

constexpr GapKindMask ConvertGapKindToMask(_In_ GapKind const kind)
{
    DBG_ASSERT_MSG(static_cast<uint8_t>(kind) < sizeof(GapKindMask) * 8, "Ensure that all GapKinds fit in GapKindMask");
    return static_cast<GapKindMask>(1u << static_cast<uint8_t>(kind));
}

enum class GapKindMask : uint32_t
{
    NoGap         = static_cast<uint32_t>(ConvertGapKindToMask(GapKind::NoGap        )),
    ContextSwitch = static_cast<uint32_t>(ConvertGapKindToMask(GapKind::ContextSwitch)),
    Unrecorded    = static_cast<uint32_t>(ConvertGapKindToMask(GapKind::Unrecorded   )),
    Large         = static_cast<uint32_t>(ConvertGapKindToMask(GapKind::Large        )),

    None = 0,
    All  =  NoGap         |
            ContextSwitch |
            Unrecorded    |
            Large         ,
};

TTD_DEFINE_ENUM_FLAG_OPERATORS(GapKindMask)

enum GapEventType : uint8_t
{
    SyntheticSequence        , // is just inserting a sequence to end the current fragment.
    CodeCacheFlush           , // needs to flush its code cache to pick up modified code.
    PreAtomicOperation       , // is about to do an atomic operation.
    PotentialAtomicCollision , // just finished an atomic operation. TODO: We currently don't use this, but we should, to provide faithful ordering of atomics.
    EtwEvent                 , // is recording an ETW event.
    DebugBreak               , // encountered, and skipped, a hardcoded breakpoint.
    FastFail                 , // encountered a fastfail. The process is about to end.
    KernelCall               , // called into kernel.
    SyntheticFallback        , // encountered a synthetic fallback.
    ExceptionDispatch        , // jumped to dispatch an exception.
    UnknownInstruction       , // encountered an instruction that the emulator couldn't handle.
    ThreadSuspended          , // was suspended.
    SListRollback            , // experienced the A-B-A exception in SList's pop function, and was properly rolled back.
    SyncPoint                , // was stopped in the debugger.
    PauseEmulation           , // paused emulation, sort of like a syscall but without accruing an instruction. For instance, to skip a function.
    StopEmulation            , // stopped emulation to end an island. For instance, end of a selective recording monitored function.
    Throttled                , // stopped emulation because of a throttle.
};

constexpr char const* GetGapEventTypeName(_In_ GapEventType const kind)
{
    switch (kind)
    {
    case GapEventType::SyntheticSequence       : return "SyntheticSequence";
    case GapEventType::CodeCacheFlush          : return "CodeCacheFlush";
    case GapEventType::PreAtomicOperation      : return "PreAtomicOperation";
    case GapEventType::PotentialAtomicCollision: return "PotentialAtomicCollision";
    case GapEventType::EtwEvent                : return "EtwEvent";
    case GapEventType::DebugBreak              : return "DebugBreak";
    case GapEventType::FastFail                : return "FastFail";
    case GapEventType::KernelCall              : return "KernelCall";
    case GapEventType::SyntheticFallback       : return "SyntheticFallback";
    case GapEventType::ExceptionDispatch       : return "ExceptionDispatch";
    case GapEventType::UnknownInstruction      : return "UnknownInstruction";
    case GapEventType::ThreadSuspended         : return "ThreadSuspended";
    case GapEventType::SListRollback           : return "SListRollback";
    case GapEventType::SyncPoint               : return "SyncPoint";
    case GapEventType::PauseEmulation          : return "PauseEmulation";
    case GapEventType::StopEmulation           : return "StopEmulation";
    case GapEventType::Throttled               : return "Throttled";
    default                                    : return "<Unknown GapEventType>";
    }
}

enum class GapEventMask : uint32_t;

constexpr GapEventMask ConvertGapEventTypeToMask(_In_ GapEventType const kind)
{
    DBG_ASSERT_MSG(static_cast<uint8_t>(kind) < sizeof(GapEventMask) * 8, "Ensure that all GapEventTypes fit in GapEventMask");
    return static_cast<GapEventMask>(1u << static_cast<uint8_t>(kind));
}

enum class GapEventMask : uint32_t
{
    SyntheticSequence        = static_cast<uint32_t>(ConvertGapEventTypeToMask(GapEventType::SyntheticSequence       )),
    CodeCacheFlush           = static_cast<uint32_t>(ConvertGapEventTypeToMask(GapEventType::CodeCacheFlush          )),
    PreAtomicOperation       = static_cast<uint32_t>(ConvertGapEventTypeToMask(GapEventType::PreAtomicOperation      )),
    PotentialAtomicCollision = static_cast<uint32_t>(ConvertGapEventTypeToMask(GapEventType::PotentialAtomicCollision)),
    EtwEvent                 = static_cast<uint32_t>(ConvertGapEventTypeToMask(GapEventType::EtwEvent                )),
    DebugBreak               = static_cast<uint32_t>(ConvertGapEventTypeToMask(GapEventType::DebugBreak              )),
    FastFail                 = static_cast<uint32_t>(ConvertGapEventTypeToMask(GapEventType::FastFail                )),
    KernelCall               = static_cast<uint32_t>(ConvertGapEventTypeToMask(GapEventType::KernelCall              )),
    SyntheticFallback        = static_cast<uint32_t>(ConvertGapEventTypeToMask(GapEventType::SyntheticFallback       )),
    ExceptionDispatch        = static_cast<uint32_t>(ConvertGapEventTypeToMask(GapEventType::ExceptionDispatch       )),
    UnknownInstruction       = static_cast<uint32_t>(ConvertGapEventTypeToMask(GapEventType::UnknownInstruction      )),
    ThreadSuspended          = static_cast<uint32_t>(ConvertGapEventTypeToMask(GapEventType::ThreadSuspended         )),
    SListRollback            = static_cast<uint32_t>(ConvertGapEventTypeToMask(GapEventType::SListRollback           )),
    SyncPoint                = static_cast<uint32_t>(ConvertGapEventTypeToMask(GapEventType::SyncPoint               )),
    PauseEmulation           = static_cast<uint32_t>(ConvertGapEventTypeToMask(GapEventType::PauseEmulation          )),
    StopEmulation            = static_cast<uint32_t>(ConvertGapEventTypeToMask(GapEventType::StopEmulation           )),
    Throttled                = static_cast<uint32_t>(ConvertGapEventTypeToMask(GapEventType::Throttled               )),

    None = 0,
    All  =  SyntheticSequence        |
            CodeCacheFlush           |
            PreAtomicOperation       |
            PotentialAtomicCollision |
            EtwEvent                 |
            DebugBreak               |
            FastFail                 |
            KernelCall               |
            SyntheticFallback        |
            ExceptionDispatch        |
            UnknownInstruction       |
            ThreadSuspended          |
            SListRollback            |
            SyncPoint                |
            PauseEmulation           |
            StopEmulation            |
            Throttled                ,
};

TTD_DEFINE_ENUM_FLAG_OPERATORS(GapEventMask)

struct GapData
{
    GapKind      Kind;
    GapEventType Event;
};

// Event types are reasons why replay might be stopped.
enum class EventType : uint8_t
{
    // Data-dependent events. Some data related to the event is specified somewhere by the client.
    MemoryWatchpoint  , // Memory watchpoints are triggered by a memory access like read, write, execute, code fetch...
    PositionWatchpoint, // Position watchpoints are triggered for execution within a range of time positions.

    // Execution events, encountered as replay proceeds.
    Exception   , // An exception matching the ExceptionMask was encountered. TODO: Eliminate this, the caller can use the event list and then set a limit Position instead.
    Gap         , // Execution was interrupted for some reason, given as a GapKind and GapEventType.
    Thread      , // Beginning or end of the current thread (depending on direction of execution).

    // Non-maskable data-dependent events.
    StepCount   , // Stopped after the required number of steps were executed, without hitting any other event.
    Position    , // Stopped when the specified position was reached, without hitting any other event.

    // Non-maskable execution events.
    Process     , // Beginning or end of the current process (depending on direction of execution).
    Interrupted , // The user called InterruptReplay(), so replay stopped wherever it did.
    Error       , // Something went wrong.

    // This must be the last enumerator; it is not a valid EventType; it is
    // simply used to automatically count the number of validly usable
    // enumerators.
    Count,

    Invalid = UINT8_MAX,

    // Old enumerations that are being discontinued.
    Watchpoint   [[deprecated]] = MemoryWatchpoint,
    ThreadSwitch [[deprecated]] = Invalid,
    Fragment     [[deprecated]] = Invalid,
    Segment      [[deprecated]] = Invalid,
};

constexpr uint32_t const EventTypeCount = static_cast<uint32_t>(EventType::Count);

constexpr char const* GetEventTypeName(_In_ EventType const type)
{
    switch (type)
    {
    case EventType::MemoryWatchpoint  : return "MemoryWatchpoint";
    case EventType::PositionWatchpoint: return "PositionWatchpoint";
    case EventType::Exception         : return "Exception";
    case EventType::Gap               : return "Gap";
    case EventType::Thread            : return "Thread";
    case EventType::StepCount         : return "StepCount";
    case EventType::Position          : return "Position";
    case EventType::Process           : return "Process";
    case EventType::Interrupted       : return "Interrupted";
    case EventType::Error             : return "Error";
    default                           : return "<Unknown EventType>";
    }
}

enum class EventMask : uint32_t;

constexpr EventMask ConvertEventTypeToMask(_In_ EventType const type)
{
    DBG_ASSERT_MSG(static_cast<uint8_t>(type) < sizeof(EventMask) * 8, "Ensure that all EventTypes fit in EventMask");
    return static_cast<EventMask>(1u << static_cast<uint8_t>(type));
}

enum class EventMask : uint32_t
{
    MemoryWatchpoint   = static_cast<uint32_t>(ConvertEventTypeToMask(EventType::MemoryWatchpoint  )),
    PositionWatchpoint = static_cast<uint32_t>(ConvertEventTypeToMask(EventType::PositionWatchpoint)),
    Exception          = static_cast<uint32_t>(ConvertEventTypeToMask(EventType::Exception         )),
    Gap                = static_cast<uint32_t>(ConvertEventTypeToMask(EventType::Gap               )),
    Thread             = static_cast<uint32_t>(ConvertEventTypeToMask(EventType::Thread            )),

    None = 0,
    All  =  MemoryWatchpoint   |
            PositionWatchpoint |
            Exception          |
            Gap                |
            Thread             ,

    // Old flags that are being discontinued.
    ThreadSwitch [[deprecated]] = None,
    Fragment     [[deprecated]] = None,
    Segment      [[deprecated]] = None,
};

TTD_DEFINE_ENUM_FLAG_OPERATORS(EventMask)

// Watchpoints are data accesses.
// Read/Write/Execute have their common meanings, same as the hardware breakpoints exposed out of CPUs.
//  Read/Write are triggered as the instruction performs the memory operation.
//  Execute is triggered just before the given instruction starts.
// CodeFetch watchpoints are triggered when the replay engine observes a piece of memory being used for code.
//  Its main purpose is to *aggregate* hits in order to provide a fast view of code coverage.
//  It has some caveats:
//  - The size of the piece of memory reported is implementation-dependent and subject to change.
//  - The actual hits returned may be different from run to run. Only the aggregate is guaranteed to be the same.
//  - It's possible to get multiple hits for the same piece of memory.
//  - It's possible to get only one hit for a piece of memory that's executed many times.
// Overwrite is triggered just before a write or a data mismatch happens, and it provides the value
//  that is being overwritten. It is only active when paired with one of these two.
// DataMismatch is triggered when the replay engine receives data that doesn't match what had previously
//  been seen in the running thread, including:
//  - Data written outside of recording, for instance by a kernel call.
//  - Data that was written by another thread.
//  - Data in shared memory pages that was written by another process.
//  Not all mismatched data is reported. For efficiency, the replay engine splits threads into manageable "segments",
//  and only mismatches from within a segment are reported.
// NewData is triggered when the replay engine receives data that it didn't previously had.
//  Such data may be a cross-segment mismatch or cross-segment redundant, or new to the thread being replayed.
//  It's the client code's responsibility to figure out which of the three it was, if needed.
// RedundantData is triggered when the replay engine receives data that it already had.
enum class DataAccessType : uint8_t
{
    Read          = 0,
    Write         = 1,
    Execute       = 2,
    CodeFetch     = 3,
    Overwrite     = 4,
    DataMismatch  = 5,
    NewData       = 6,
    RedundantData = 7,
};

constexpr char const* GetDataAccessTypeName(_In_ DataAccessType const type)
{
    switch (type)
    {
        case DataAccessType::Read         : return "Read";
        case DataAccessType::Write        : return "Write";
        case DataAccessType::Execute      : return "Execute";
        case DataAccessType::CodeFetch    : return "CodeFetch";
        case DataAccessType::Overwrite    : return "Overwrite";
        case DataAccessType::DataMismatch : return "DataMismatch";
        case DataAccessType::NewData      : return "NewData";
        case DataAccessType::RedundantData: return "RedundantData";
        default                           : return "<Unknown DataAccessType>";
    }
}

constexpr wchar_t const* GetDataAccessTypeNameW(_In_ DataAccessType const type)
{
    switch (type)
    {
        case DataAccessType::Read         : return L"Read";
        case DataAccessType::Write        : return L"Write";
        case DataAccessType::Execute      : return L"Execute";
        case DataAccessType::CodeFetch    : return L"CodeFetch";
        case DataAccessType::Overwrite    : return L"Overwrite";
        case DataAccessType::DataMismatch : return L"DataMismatch";
        case DataAccessType::NewData      : return L"NewData";
        case DataAccessType::RedundantData: return L"RedundantData";
        default                           : return L"<Unknown DataAccessType>";
    }
}

constexpr bool IsDataAccessBeforeInstruction(DataAccessType type)
{
    return type == DataAccessType::Execute || type == DataAccessType::CodeFetch;
}

enum class DataAccessMask : uint8_t;

constexpr DataAccessMask ConvertDataAccessTypeToMask(_In_ DataAccessType const type)
{
    DBG_ASSERT_MSG(static_cast<uint8_t>(type) < sizeof(DataAccessMask) * 8, "Ensure that all DataAccessTypes fit in DataAccessMask");
    return static_cast<DataAccessMask>(1u << static_cast<uint8_t>(type));
}

enum class DataAccessMask : uint8_t
{
    Read          = static_cast<uint8_t>(ConvertDataAccessTypeToMask(DataAccessType::Read         )),
    Write         = static_cast<uint8_t>(ConvertDataAccessTypeToMask(DataAccessType::Write        )),
    Execute       = static_cast<uint8_t>(ConvertDataAccessTypeToMask(DataAccessType::Execute      )),
    CodeFetch     = static_cast<uint8_t>(ConvertDataAccessTypeToMask(DataAccessType::CodeFetch    )),
    Overwrite     = static_cast<uint8_t>(ConvertDataAccessTypeToMask(DataAccessType::Overwrite    )),
    DataMismatch  = static_cast<uint8_t>(ConvertDataAccessTypeToMask(DataAccessType::DataMismatch )),
    NewData       = static_cast<uint8_t>(ConvertDataAccessTypeToMask(DataAccessType::NewData      )),
    RedundantData = static_cast<uint8_t>(ConvertDataAccessTypeToMask(DataAccessType::RedundantData)),

    None      = 0,
    ReadWrite = Read | Write,
    All       = Read | Write | Execute | CodeFetch | Overwrite | DataMismatch | NewData | RedundantData,
};

TTD_DEFINE_ENUM_FLAG_OPERATORS(DataAccessMask)

struct MemoryWatchpointData
{
    GuestAddress   Address;
    uint64_t       Size;
    DataAccessMask AccessMask;
    UniqueThreadId ThreadId = UniqueThreadId::Invalid;

    friend constexpr auto AsComparisonTuple(MemoryWatchpointData const& w) { return std::tie(w.Address, w.Size, w.AccessMask, w.ThreadId); }

    friend constexpr bool operator==(MemoryWatchpointData const& a, MemoryWatchpointData const& b) { return AsComparisonTuple(a) == AsComparisonTuple(b); }
    friend constexpr bool operator!=(MemoryWatchpointData const& a, MemoryWatchpointData const& b) { return AsComparisonTuple(a) != AsComparisonTuple(b); }
    friend constexpr bool operator< (MemoryWatchpointData const& a, MemoryWatchpointData const& b) { return AsComparisonTuple(a) <  AsComparisonTuple(b); }
    friend constexpr bool operator> (MemoryWatchpointData const& a, MemoryWatchpointData const& b) { return AsComparisonTuple(a) >  AsComparisonTuple(b); }
    friend constexpr bool operator<=(MemoryWatchpointData const& a, MemoryWatchpointData const& b) { return AsComparisonTuple(a) <= AsComparisonTuple(b); }
    friend constexpr bool operator>=(MemoryWatchpointData const& a, MemoryWatchpointData const& b) { return AsComparisonTuple(a) >= AsComparisonTuple(b); }
};

struct PositionWatchpointData
{
    PositionRange  Positions;
    UniqueThreadId ThreadId = UniqueThreadId::Invalid;

    friend constexpr auto AsComparisonTuple(PositionWatchpointData const& w) { return std::tie(w.Positions, w.ThreadId); }

    friend constexpr bool operator==(PositionWatchpointData const& a, PositionWatchpointData const& b) { return AsComparisonTuple(a) == AsComparisonTuple(b); }
    friend constexpr bool operator!=(PositionWatchpointData const& a, PositionWatchpointData const& b) { return AsComparisonTuple(a) != AsComparisonTuple(b); }
    friend constexpr bool operator< (PositionWatchpointData const& a, PositionWatchpointData const& b) { return AsComparisonTuple(a) <  AsComparisonTuple(b); }
    friend constexpr bool operator> (PositionWatchpointData const& a, PositionWatchpointData const& b) { return AsComparisonTuple(a) >  AsComparisonTuple(b); }
    friend constexpr bool operator<=(PositionWatchpointData const& a, PositionWatchpointData const& b) { return AsComparisonTuple(a) <= AsComparisonTuple(b); }
    friend constexpr bool operator>=(PositionWatchpointData const& a, PositionWatchpointData const& b) { return AsComparisonTuple(a) >= AsComparisonTuple(b); }
};

// Exception types are groupings of exceptions that can be enabled/disabled independently per-group.
enum class ExceptionType : uint8_t
{
    Hardware  ,
    Software  ,
    CPlusPlus ,
    DebugPrint,
};

constexpr wchar_t const* GetExceptionTypeName(_In_ ExceptionType const type)
{
    switch (type)
    {
    case ExceptionType::Hardware:   return L"Hardware";
    case ExceptionType::Software:   return L"Software";
    case ExceptionType::CPlusPlus:  return L"CPlusPlus";
    case ExceptionType::DebugPrint: return L"DebugPrint";
    default: return L"<Unknown ExceptionType>";
    }
}

enum class ExceptionMask : uint32_t;

constexpr ExceptionMask ConvertExceptionTypeToMask(_In_ ExceptionType const type)
{
    DBG_ASSERT_MSG(static_cast<uint8_t>(type) < sizeof(ExceptionMask) * 8, "Ensure that all ExceptionTypes fit in ExceptionMask");
    return static_cast<ExceptionMask>(1u << static_cast<uint8_t>(type));
}

enum class ExceptionMask : uint32_t
{
    Hardware   = static_cast<uint32_t>(ConvertExceptionTypeToMask(ExceptionType::Hardware  )),
    Software   = static_cast<uint32_t>(ConvertExceptionTypeToMask(ExceptionType::Software  )),
    CPlusPlus  = static_cast<uint32_t>(ConvertExceptionTypeToMask(ExceptionType::CPlusPlus )),
    DebugPrint = static_cast<uint32_t>(ConvertExceptionTypeToMask(ExceptionType::DebugPrint)),

    None = 0,
    All  =  Hardware  |
            Software  |
            CPlusPlus |
            DebugPrint,
};

TTD_DEFINE_ENUM_FLAG_OPERATORS(ExceptionMask)

// Additional flags that modify how replay will proceed.
enum ReplayFlags : uint32_t
{
    // Causes the Cursor to replay only the thread that is the "current" thread
    // when replay began.  Breakpoints, watchpoints, and events on other threads
    // will not be hit.  Other threads will only be replayed to ensure that the
    // Cursor is in a consistent state when it returns after having replayed the
    // current thread.
    ReplayOnlyCurrentThread           = 0x0000'0001u,

    // Causes the Cursor to replay the entire trace, starting from its current
    // position, and ending at the first breakpoint, watchpoint, or other
    // unmasked event that it hits.  The default behavior is for the Cursor to
    // only replay the parts of the trace that might possibly hit an event.
    // This option is primarily useful for debugging, to verify that the entire
    // trace is replayable.
    ReplayAllSegmentsWithoutFiltering = 0x0000'0002u,


    // Causes the Cursor to replay one segment at a time, sequentially.  (The
    // default behavior is for the Cursor to replay multiple segments in parallel,
    // attempting to maximize usage of available CPU resources.)
    ReplaySegmentsSequentially        = 0x0000'0004u,

    None    = 0,
    Default = 0,
    All = ReplayOnlyCurrentThread
        | ReplayAllSegmentsWithoutFiltering
        | ReplaySegmentsSequentially,
};

TTD_DEFINE_ENUM_FLAG_OPERATORS(ReplayFlags)

// Metadata used to describe each one of the threads found in the trace file.
// [LifetimeStartSequence..ExitSequence] is the portion of the timeline where the thread is considered active.
// The active lifetime of a thread is the closest approximation to when the thread was present during record.
// [FirstPosition..LastPosition] is the portion of the timeline that contains instructions executed by the thread.
// LifetimeStartSequence <= FirstPosition < LastPosition <= ExitSequence
struct ThreadInfo
{
    UniqueThreadId UniqueId;
    ThreadId       Id;
    PositionRange  Lifetime;
    PositionRange  ActiveTime;

    /*implicit*/ constexpr operator UniqueThreadId() const { return UniqueId; }

    friend constexpr bool operator==(ThreadInfo const& a, ThreadInfo const& b)
    {
        if (a.UniqueId != b.UniqueId)
        {
            return false;
        }
        DBG_ASSERT(a.Id         == b.Id);
        DBG_ASSERT(a.Lifetime   == b.Lifetime);
        DBG_ASSERT(a.ActiveTime == b.ActiveTime);
        return true;
    }

    friend constexpr bool operator< (ThreadInfo const& a, ThreadInfo const& b) { return a.UniqueId < b.UniqueId; }
    friend constexpr bool operator> (ThreadInfo const& a, ThreadInfo const& b) { return  (b <  a); }
    friend constexpr bool operator<=(ThreadInfo const& a, ThreadInfo const& b) { return !(b <  a); }
    friend constexpr bool operator>=(ThreadInfo const& a, ThreadInfo const& b) { return !(a <  b); }
    friend constexpr bool operator!=(ThreadInfo const& a, ThreadInfo const& b) { return !(a == b); }
};

// An active thread info represents a particular thread's info at a particular time in its execution.
// LastValidPosition could be either the immediately preceding position, or the last position in the previous fragment,
// or Position::Min, if CurrentPosition is at the beginning of the thread's lifetime.
struct ActiveThreadInfo
{
    ThreadInfo const* pThread;
    // TODO: Turn this into a position range, right now LastValidPosition is exclusive, not inclusive, so it's not just a naming change
    Position          CurrentPosition;
    Position          LastValidPosition;

    constexpr ThreadInfo const* operator->() const { return pThread; }

    friend constexpr auto AsComparisonTuple(ActiveThreadInfo const& t) { return std::tie(t->UniqueId, t.CurrentPosition); }

    friend constexpr bool operator==(ActiveThreadInfo const& a, ActiveThreadInfo const& b)
    {
        if (AsComparisonTuple(a) != AsComparisonTuple(b))
        {
            return false;
        }
        DBG_ASSERT(a.LastValidPosition == b.LastValidPosition);
        return true;
    }
    friend constexpr bool operator!=(ActiveThreadInfo const& a, ActiveThreadInfo const& b) { return !(a == b); }

    friend constexpr bool operator< (ActiveThreadInfo const& a, ActiveThreadInfo const& b) { return AsComparisonTuple(a) <  AsComparisonTuple(b); }
    friend constexpr bool operator> (ActiveThreadInfo const& a, ActiveThreadInfo const& b) { return AsComparisonTuple(a) >  AsComparisonTuple(b); }
    friend constexpr bool operator<=(ActiveThreadInfo const& a, ActiveThreadInfo const& b) { return AsComparisonTuple(a) <= AsComparisonTuple(b); }
    friend constexpr bool operator>=(ActiveThreadInfo const& a, ActiveThreadInfo const& b) { return AsComparisonTuple(a) >= AsComparisonTuple(b); }
};

struct ExceptionEvent
{
    Position          Position;
    ThreadInfo const* pThreadInfo;
    ExceptionType     Type;
    uint32_t          Code;
    uint32_t          Flags;
    GuestAddress      RecordAddress;
    GuestAddress      ProgramCounter;
    uint32_t          ParameterCount;
    uint64_t          Parameters[15];
};

inline size_t ExceptionEventToString(
    _In_                 ExceptionEvent const& exception,
    _Out_writes_z_(size) wchar_t*       const  pBuff,
    _In_                 size_t         const  size)
{
    return _snwprintf_s(pBuff, size, _TRUNCATE, L"Exception 0x%08X of type %s at PC: 0X%llX",
        exception.Code,
        GetExceptionTypeName(exception.Type),
        exception.ProgramCounter);
}

struct ThreadCreatedEvent
{
    ThreadCreatedEvent() = default;
    ThreadCreatedEvent(_In_ Position const& position, _In_ ThreadInfo const* pThreadInfo)
        : Position(position)
        , pThreadInfo(pThreadInfo)
    {}

    Position          Position;
    ThreadInfo const* pThreadInfo;
};

inline size_t ThreadCreatedEventToString(_In_ ThreadCreatedEvent const& event, _Out_writes_z_(size) wchar_t* pBuff, _In_ size_t size)
{
    return _snwprintf_s(pBuff, size, _TRUNCATE, L"Thread UID: %3u TID: 0x%04X created at %llX:%llX",
        event.pThreadInfo->UniqueId,
        event.pThreadInfo->Id,
        event.Position.Sequence,
        event.Position.Steps
    );
}

struct ThreadTerminatedEvent
{
    ThreadTerminatedEvent() = default;
    ThreadTerminatedEvent(_In_ Position const& position, _In_ ThreadInfo const* pThreadInfo)
        : Position(position)
        , pThreadInfo(pThreadInfo)
    {}

    Position          Position;
    ThreadInfo const* pThreadInfo;
};

inline size_t ThreadTerminatedEventToString(_In_ ThreadTerminatedEvent const& event, _Out_writes_z_(size) wchar_t* pBuff, _In_ size_t size)
{
    return _snwprintf_s(pBuff, size, _TRUNCATE, L"Thread UID: %3u TID: 0x%04X terminated at %llX:%llX",
        event.pThreadInfo->UniqueId,
        event.pThreadInfo->Id,
        event.Position.Sequence,
        event.Position.Steps
    );
}

inline wchar_t const* GetModuleBaseName(
    _In_z_ wchar_t const* const pModuleFullName,
    _In_ size_t const pModuleNameLength
) noexcept
{
    static constexpr wchar_t const delimiters[] = { L':', L'/', L'\\' };

    wchar_t const* const first = pModuleFullName;
    wchar_t const* const last = first + pModuleNameLength;

    // Return first if not found, which is pModuleFullName
    return std::find_first_of(
        std::make_reverse_iterator(last),
        std::make_reverse_iterator(first),
        std::begin(delimiters),
        std::end(delimiters)).base();
}

// Represents a single module loaded event
struct ModuleLoadedEvent
{
    Position      Position;
    Module const* pModule;
};

inline size_t ModuleLoadedEventToString(
    _In_                 ModuleLoadedEvent const& moduleEvent,
    _Out_writes_z_(size) wchar_t*          const  pBuff,
    _In_                 size_t            const  size)
{
    // Note that written doesn't contain the count of the null terminator
    auto const written = _snwprintf_s(pBuff, size, _TRUNCATE, L"Module %ls Loaded at position: ", GetModuleBaseName(moduleEvent.pModule->pName, moduleEvent.pModule->NameLength));
    if (written <= 0)
    {
        return 0u; // An error occured
    }

    if (static_cast<size_t>(written) == size + 1u)
    {
        // The buffer is full already
        return written;
    }
    return written + PositionToString(moduleEvent.Position, pBuff + written, size - written);
}

// Represents a single module unloaded event
struct ModuleUnloadedEvent
{
    Position      Position;
    Module const* pModule;
};

inline size_t ModuleUnloadedEventToString(
    _In_                 ModuleUnloadedEvent const& moduleEvent,
    _Out_writes_z_(size) wchar_t*            const  pBuff,
    _In_                 size_t              const  size)
{
    // Note that written doesn't contain the count of the null terminator
    auto const written = _snwprintf_s(pBuff, size, _TRUNCATE, L"Module %ls Unloaded at position: ", GetModuleBaseName(moduleEvent.pModule->pName, moduleEvent.pModule->NameLength));
    if (written <= 0)
    {
        return 0u; // An error occured
    }

    if (static_cast<size_t>(written) == size + 1u)
    {
        // The buffer is full already
        return written;
    }
    return written + PositionToString(moduleEvent.Position, pBuff + written, size - written);
}

// Represents information about a recording client.
// Besides the client's ID, the information is opaque to TTD.
struct RecordClient
{
    RecordClientId  Id;
    GUID            ClientGuid;
    PositionRange   Lifetime;
    ConstBufferView OpenUserData;
    ConstBufferView CloseUserData;

    friend constexpr bool operator==(RecordClient const& a, RecordClient const& b) { return a.Id == b.Id; }
    friend constexpr bool operator!=(RecordClient const& a, RecordClient const& b) { return a.Id != b.Id; }
    friend constexpr bool operator< (RecordClient const& a, RecordClient const& b) { return a.Id <  b.Id; }
    friend constexpr bool operator> (RecordClient const& a, RecordClient const& b) { return a.Id >  b.Id; }
    friend constexpr bool operator<=(RecordClient const& a, RecordClient const& b) { return a.Id <= b.Id; }
    friend constexpr bool operator>=(RecordClient const& a, RecordClient const& b) { return a.Id >= b.Id; }
};

inline size_t RecordClientToString(
                         RecordClient const& client,
    _Out_writes_z_(size) wchar_t*     const  pBuff,
                         size_t       const  size
)
{
    // Note that written doesn't contain the count of the null terminator
    auto const written = _snwprintf_s(pBuff, size, _TRUNCATE, L"Record client %u with GUID {%08lX-%04X-%04X-%02X%02X-%02X%02X%02X%02X%02X%02X} and lifetime: ",
        client.Id,
        client.ClientGuid.Data1,
        client.ClientGuid.Data2,
        client.ClientGuid.Data3,
        client.ClientGuid.Data4[0],
        client.ClientGuid.Data4[1],
        client.ClientGuid.Data4[2],
        client.ClientGuid.Data4[3],
        client.ClientGuid.Data4[4],
        client.ClientGuid.Data4[5],
        client.ClientGuid.Data4[6],
        client.ClientGuid.Data4[7]
    );
    if (written <= 0)
    {
        return 0u; // An error occured
    }

    if (static_cast<size_t>(written) == size + 1u)
    {
        // The buffer is full already
        return written;
    }
    return written + PositionRangeToString(client.Lifetime, pBuff + written, size - written);
}

// Represents events entered by a recording client, they are opaque to TTD.
struct CustomEvent
{
    Position            Position;
    ThreadInfo const*   pThreadInfo;
    RecordClient const* pRecordClient;
    ConstBufferView     UserData;

    friend constexpr bool operator==(CustomEvent const& a, CustomEvent const& b)
    {
        if (a.Position != b.Position)
        {
            return false;
        }
        DBG_ASSERT(*a.pThreadInfo   == *b.pThreadInfo  );
        DBG_ASSERT(*a.pRecordClient == *b.pRecordClient);
        return true;
    }
    friend constexpr bool operator< (CustomEvent const& a, CustomEvent const& b) { return a.Position <  b.Position; }
    friend constexpr bool operator> (CustomEvent const& a, CustomEvent const& b) { return a.Position >  b.Position; }
    friend constexpr bool operator<=(CustomEvent const& a, CustomEvent const& b) { return a.Position <= b.Position; }
    friend constexpr bool operator>=(CustomEvent const& a, CustomEvent const& b) { return a.Position >= b.Position; }
    friend constexpr bool operator!=(CustomEvent const& a, CustomEvent const& b) { return !(a == b); }
};

inline size_t CustomEventToString(
                         CustomEvent const& event,
    _Out_writes_z_(size) wchar_t*    const  pBuff,
                         size_t      const  size
)
{
    // Note that written doesn't contain the count of the null terminator
    auto const written = _snwprintf_s(pBuff, size, _TRUNCATE, L"Custom event from client %u on thread %3u (0x%04X) at position: ",
        event.pRecordClient->Id,
        event.pThreadInfo->UniqueId,
        event.pThreadInfo->Id
    );
    if (written <= 0)
    {
        return 0u; // An error occured
    }

    if (static_cast<size_t>(written) == size + 1u)
    {
        // The buffer is full already
        return written;
    }
    return written + PositionToString(event.Position, pBuff + written, size - written);
}

// Represents a logically-connected collection of islands of execution as defined by a recording client.
// The purpose given to each activity by the recording client is opaque to TTD.
struct Activity
{
    RecordClient const* pRecordClient;
    ActivityId          Id;
    PositionRange       Lifetime;       // Minimal range that includes all the islands in this activity.

    friend constexpr auto AsComparisonTuple(Activity const& a) { return std::tie(*a.pRecordClient, a.Id); }

    friend constexpr bool operator==(Activity const& a, Activity const& b) { return AsComparisonTuple(a) == AsComparisonTuple(b); }
    friend constexpr bool operator!=(Activity const& a, Activity const& b) { return AsComparisonTuple(a) != AsComparisonTuple(b); }
    friend constexpr bool operator< (Activity const& a, Activity const& b) { return AsComparisonTuple(a) <  AsComparisonTuple(b); }
    friend constexpr bool operator> (Activity const& a, Activity const& b) { return AsComparisonTuple(a) >  AsComparisonTuple(b); }
    friend constexpr bool operator<=(Activity const& a, Activity const& b) { return AsComparisonTuple(a) <= AsComparisonTuple(b); }
    friend constexpr bool operator>=(Activity const& a, Activity const& b) { return AsComparisonTuple(a) >= AsComparisonTuple(b); }
};

inline size_t ActivityToString(
                         Activity const& activity,
    _Out_writes_z_(size) wchar_t* const  pBuff,
                         size_t   const  size
)
{
    // Note that written doesn't contain the count of the null terminator
    auto const written = _snwprintf_s(pBuff, size, _TRUNCATE, L"Activity %u from client %u with lifetime: ",
        activity.Id,
        activity.pRecordClient->Id
    );
    if (written <= 0)
    {
        return 0u; // An error occured
    }

    if (static_cast<size_t>(written) == size + 1u)
    {
        // The buffer is full already
        return written;
    }
    return written + PositionRangeToString(activity.Lifetime, pBuff + written, size - written);
}

// Represents a single sequence of execution in one thread,
// as requested by a recording client and belonging to some activity.
struct Island
{
    PositionRange     Lifetime;
    ThreadInfo const* pThreadInfo;
    Activity const*   pActivity;
    ConstBufferView   UserData;

    friend constexpr bool operator< (Island const& a, Island const& b) { return a.Lifetime.Min <  b.Lifetime.Min; }
    friend constexpr bool operator> (Island const& a, Island const& b) { return a.Lifetime.Min >  b.Lifetime.Min; }
    friend constexpr bool operator<=(Island const& a, Island const& b) { return a.Lifetime.Min <= b.Lifetime.Min; }
    friend constexpr bool operator>=(Island const& a, Island const& b) { return a.Lifetime.Min >= b.Lifetime.Min; }
};

inline size_t IslandToString(
                         Island   const& island,
    _Out_writes_z_(size) wchar_t* const  pBuff,
                         size_t   const  size
)
{
    // Note that written doesn't contain the count of the null terminator
    auto const written = _snwprintf_s(pBuff, size, _TRUNCATE, L"Island on activity %u from client %u on thread %3u (0x%04X) with lifetime: ",
        island.pActivity->Id,
        island.pActivity->pRecordClient->Id,
        island.pThreadInfo->UniqueId,
        island.pThreadInfo->Id
    );
    if (written <= 0)
    {
        return 0u; // An error occured
    }

    if (static_cast<size_t>(written) == size + 1u)
    {
        // The buffer is full already
        return written;
    }
    return written + PositionRangeToString(island.Lifetime, pBuff + written, size - written);
}

enum class IndexStatus : uint32_t
{
    IndexFileLoaded,
    IndexFileNotPresent,
    IndexFileUnloadable,
};

constexpr wchar_t const* GetIndexStatusName(IndexStatus const status) noexcept
{
    switch (status)
    {
    case IndexStatus::IndexFileLoaded    : return L"Loaded";
    case IndexStatus::IndexFileNotPresent: return L"NotPresent";
    case IndexStatus::IndexFileUnloadable: return L"Unloadable";
    default                              : return L"<unknown status>";
    }
}

enum class IndexBuildFlags : uint32_t
{
    None                              = 0x00,

    // If an index file exists for this trace but that index file is unloadable,
    // attempt to delete the existing index file first.  (The default behavior
    // is not to delete a pre-existing index file, even if it is unloadable,
    // to ensure that file deletion is an explicit action.)
    DeleteExistingUnloadableIndexFile = 0x01,

    // Create the index file with "delete on close" semantics so that it only exists
    // for the life of the debugger session
    TemporaryIndexFile                = 0x02,

    // Create a self-contained index that doesn't require the trace file.
    MakeSelfContained                 = 0x04,

    All = DeleteExistingUnloadableIndexFile
        | TemporaryIndexFile
        | MakeSelfContained
};

TTD_DEFINE_ENUM_FLAG_OPERATORS(IndexBuildFlags)

struct IndexBuildProgressType
{
    uint32_t KeyframeCount;
    uint32_t KeyframesProcessed;
};

using IndexBuildProgressCallback = void(__stdcall*)(
    _In_opt_ void const*                   pCallerContext,
    _In_     IndexBuildProgressType const* pProgressData
);

struct IndexTreeStats
{
    // The size in bytes of each page, and the total number of pages present in
    // the index tree.  (The page count may not equal the sum of the specific
    // page type counts below, due to bookkeeping overhead.)
    uint64_t m_pageSize;
    uint64_t m_pageCount;

    // Inner page statistics
    uint64_t m_innerPageCount;
    uint64_t m_innerPageEntryCount;
    uint64_t m_innerPageEntryCapacity;
    uint64_t m_innerPageEntrySize;

    // Leaf page statistics
    uint64_t m_leafPageCount;
    uint64_t m_leafPageEntryCount;
    uint64_t m_leafPageEntryCapacity;
    uint64_t m_leafPageEntrySize;

    uint64_t m_maximumLeafDepth;
    uint64_t m_sumOfLeafDepths;
};

struct IndexFileStats
{
    IndexTreeStats m_globalMemoryIndexStats;
    IndexTreeStats m_segmentMemoryIndexStats;

    // These are, respectively, the total number of times that we had to map a
    // page into memory (e.g. via a call to MapViewOfFile) and the total number
    // of times that we accessed any page.  These together give a good indication
    // of the efficiency of the in-memory index cache.
    uint64_t m_mapPageCallCount;
    uint64_t m_lockPageCallCount;
};

// Deleter class to allow std::unique_ptr and std::shared_ptr to manage ownership of the engine's objects.
template < typename Interface >
struct Deleter
{
    void operator()(_Inout_ Interface* const pObject) const
    {
        pObject->Destroy();
    }
};

// Allows the user to select how memory queries will be conducted.
// No matter which policy is used, results shall be consistent as long as the current position (and thread) don't change.
enum class QueryMemoryPolicy : uint32_t
{
    // Default allows the engine to select a policy from the remaining options,
    // depending of the circumstances of the recorded trace.
    Default,

    // Quick query that concentrates on the current position and current thread, possibly ignoring some of the memory
    // observed by other threads, along with memory observed by the current thread in the past or future.
    // Only memory that is quick to find during thread-local execution is considered.
    // Note: This is the policy used when querying memory from the IThreadView interface as provided to a callback.
    ThreadLocal,

    // Only return memory that has a high confidence of being correct and is efficient to find.
    GloballyConservative,

    // Look harder to try and return memory even if it has a lower confidence of being correct.
    GloballyAggressive,

    // Look hard inside of the entire current fragment, including in the future, for the memory value.
    InFragmentAggressive,
};

constexpr wchar_t const* GetQueryMemoryPolicyName(_In_ QueryMemoryPolicy const type)
{
    switch (type)
    {
    case QueryMemoryPolicy::Default             : return L"Default";
    case QueryMemoryPolicy::ThreadLocal         : return L"ThreadLocal";
    case QueryMemoryPolicy::GloballyConservative: return L"GloballyConservative";
    case QueryMemoryPolicy::GloballyAggressive  : return L"GloballyAggressive";
    case QueryMemoryPolicy::InFragmentAggressive: return L"InFragmentAggressive";
    default: return L"<Unknown QueryMemoryPolicy>";
    }
}

// A range of guest memory that was recorded at the same position in the trace.
struct MemoryRange
{
    GuestAddress    Address;
    ConstBufferView Memory;
    SequenceId      Sequence;
};

// A range of guest memory possibly collected from multiple positions in the trace.
struct MemoryBuffer
{
    GuestAddress    Address;
    ConstBufferView Memory;
};

// A range of guest memory possibly collected from multiple positions in the trace,
// including the list of sub-ranges each taken from the same position.
struct MemoryBufferWithRanges
{
    GuestAddress    Address;
    ConstBufferView Memory;
    size_t          RangeCount;
};

class IThreadView
{
public:
    // Context queries on a single thread.
    virtual ThreadInfo const&       GetThreadInfo          () const noexcept = 0;
    virtual GuestAddress            GetTebAddress          () const noexcept = 0;
    virtual Position const&         GetPosition            () const noexcept = 0;
    virtual Position                GetPreviousPosition    () const noexcept = 0;
    virtual GuestAddress            GetProgramCounter      () const noexcept = 0;
    virtual GuestAddress            GetStackPointer        () const noexcept = 0;
    virtual GuestAddress            GetFramePointer        () const noexcept = 0;
    virtual uint64_t                GetBasicReturnValue    () const noexcept = 0;
    virtual RegisterContext         GetCrossPlatformContext() const noexcept = 0;
    virtual ExtendedRegisterContext GetAvxExtendedContext  () const noexcept = 0;

    // Memory queries using the default policy.
    // Note that querying memory from execution callbacks is guaranteed to use thread local queries.
    // In order to use the more interesting policies, the user must create a new cursor at the desired position,
    // or force a return from execution on the current cursor, so that it can be used.

    // Returns a single range of contiguous memory bytes stored internally,
    // taken from a single position in the trace, where the buffer is valid until the next memory query on this cursor.
    virtual MemoryRange QueryMemoryRange(_In_ GuestAddress) const noexcept = 0;

    // Fills as much of the provided buffer as possible, and returns the resulting valid buffer.
    virtual MemoryBuffer QueryMemoryBuffer(_In_ GuestAddress, _In_ BufferView) const noexcept = 0;

    // Fills as much of the provided buffer as possible, and returns he resulting valid buffer,
    // along with a list of the corresponding ranges.
    virtual MemoryBufferWithRanges QueryMemoryBufferWithRanges(
        _In_                    GuestAddress,
        _In_                    BufferView,
        _In_range_(1, SIZE_MAX) size_t       maxRanges,
        _Out_writes_(maxRanges) MemoryRange*
    ) const noexcept = 0;

protected:
    virtual ~IThreadView() = default;
};

// This is the main replay engine cursor interface to use for navigating through the trace file.
// Many cursors can be created for a single engine object.
// A cursor allows context-sensitive queries. The context is a specific time position and thread.
class ICursorView
{
public:
    // Memory queries.

    // Returns a single range of contiguous memory bytes stored internally,
    // taken from a single position in the trace, where the buffer is valid until the next memory query on this cursor.
    virtual MemoryRange QueryMemoryRange(
        _In_ GuestAddress,
        _In_ QueryMemoryPolicy = QueryMemoryPolicy::Default
    ) const noexcept = 0;

    // Fills as much of the provided buffer as possible, and returns the resulting valid buffer.
    virtual MemoryBuffer QueryMemoryBuffer(
        _In_ GuestAddress,
        _In_ BufferView,
        _In_ QueryMemoryPolicy = QueryMemoryPolicy::Default
    ) const noexcept = 0;

    // Fills as much of the provided buffer as possible, and returns he resulting valid buffer,
    // along with a list of the corresponding ranges.
    virtual MemoryBufferWithRanges QueryMemoryBufferWithRanges(
        _In_                    GuestAddress,
        _In_                    BufferView,
        _In_range_(1, SIZE_MAX) size_t            maxRanges,
        _Out_writes_(maxRanges) MemoryRange*,
        _In_                    QueryMemoryPolicy = QueryMemoryPolicy::Default
    ) const noexcept = 0;

    virtual void              SetDefaultMemoryPolicy(QueryMemoryPolicy)       noexcept = 0;
    virtual QueryMemoryPolicy GetDefaultMemoryPolicy()                  const noexcept = 0;

    // General queries.
    virtual void* __fastcall UnsafeGetReplayEngine(_In_ GUID const& engineGuid) const noexcept = 0;

    template < typename Interface = IReplayEngineView > Interface* GetReplayEngine() const noexcept { return static_cast<Interface*>(UnsafeGetReplayEngine(__uuidof(Interface))); }

    // Unsafe exported functions. In order to use them, they require the appropriate cast.
    virtual void*       UnsafeAsInterface(GUID const&)       noexcept = 0;
    virtual void const* UnsafeAsInterface(GUID const&) const noexcept = 0;

    template < typename Interface > auto AsInterface()       noexcept { return static_cast<Interface*      >(UnsafeAsInterface(__uuidof(Interface))); }
    template < typename Interface > auto AsInterface() const noexcept { return static_cast<Interface const*>(UnsafeAsInterface(__uuidof(Interface))); }

    // Context queries on any live thread.
    virtual ThreadInfo const&       GetThreadInfo          (_In_ ThreadId = ThreadId::Invalid) const noexcept = 0;
    virtual GuestAddress            GetTebAddress          (_In_ ThreadId = ThreadId::Invalid) const noexcept = 0;
    virtual Position const&         GetPosition            (_In_ ThreadId = ThreadId::Invalid) const noexcept = 0;
    virtual Position const&         GetPreviousPosition    (_In_ ThreadId = ThreadId::Invalid) const noexcept = 0;
    virtual GuestAddress            GetProgramCounter      (_In_ ThreadId = ThreadId::Invalid) const noexcept = 0;
    virtual GuestAddress            GetStackPointer        (_In_ ThreadId = ThreadId::Invalid) const noexcept = 0;
    virtual GuestAddress            GetFramePointer        (_In_ ThreadId = ThreadId::Invalid) const noexcept = 0;
    virtual uint64_t                GetBasicReturnValue    (_In_ ThreadId = ThreadId::Invalid) const noexcept = 0;
    virtual RegisterContext         GetCrossPlatformContext(_In_ ThreadId = ThreadId::Invalid) const noexcept = 0;
    virtual ExtendedRegisterContext GetAvxExtendedContext  (_In_ ThreadId = ThreadId::Invalid) const noexcept = 0;

    virtual size_t                GetModuleCount() const noexcept = 0;
    virtual ModuleInstance const* GetModuleList () const noexcept = 0;

    virtual size_t                  GetThreadCount() const noexcept = 0;
    virtual ActiveThreadInfo const* GetThreadList () const noexcept = 0;

    virtual void      SetEventMask(_In_ EventMask) noexcept = 0;
    virtual EventMask GetEventMask()         const noexcept = 0;

    virtual void        SetGapKindMask(_In_ GapKindMask) noexcept = 0;
    virtual GapKindMask GetGapKindMask()           const noexcept = 0;

    virtual void         SetGapEventMask(_In_ GapEventMask) noexcept = 0;
    virtual GapEventMask GetGapEventMask()            const noexcept = 0;

    virtual void          SetExceptionMask(_In_ ExceptionMask) noexcept = 0;
    virtual ExceptionMask GetExceptionMask()             const noexcept = 0;

    virtual void        SetReplayFlags(_In_ ReplayFlags) noexcept = 0;
    virtual ReplayFlags GetReplayFlags()           const noexcept = 0;

    virtual bool AddMemoryWatchpoint   (_In_ MemoryWatchpointData const&) noexcept = 0;
    virtual bool RemoveMemoryWatchpoint(_In_ MemoryWatchpointData const&) noexcept = 0;

    virtual bool AddPositionWatchpoint   (_In_ PositionWatchpointData const&) noexcept = 0;
    virtual bool RemovePositionWatchpoint(_In_ PositionWatchpointData const&) noexcept = 0;

    // Reverts to Position::Invalid, good as new.
    virtual void Clear() noexcept = 0;

    // Moves to the closest valid position to the given position, "rounding up" if necessary.
    virtual void SetPosition(_In_ Position const&) noexcept = 0;

    // Moves to the closest valid position, within the given thread, to the given position, "rounding up" if necessary.
    virtual void SetPositionOnThread(_In_ UniqueThreadId, _In_ Position const& = Position::Min) noexcept = 0;

    struct MemoryWatchpointResult
    {
        GuestAddress   Address;
        uint64_t       Size;
        DataAccessType AccessType;
    };

    // Allows the caller to observe and reject (result == false) or accept (result != false) a particular watchpoint hit.
    // ICursorView is not to be used. Most calls might be unsafe when performed from within the callback.
    // IThreadView allows the callback to safely perform limited thread-local queries for registers, memory and state.
    typedef bool __fastcall MemoryWatchpointCallback(_In_ uintptr_t context, _In_ MemoryWatchpointResult const&, _In_ IThreadView const*);

    // Allows the caller to observe and reject (result == false) or accept (result != false) a particular watchpoint hit.
    // ICursorView is not to be used. Most calls might be unsafe when performed from within the callback.
    // IThreadView allows the callback to safely perform limited thread-local queries for registers, memory and state.
    typedef bool __fastcall PositionWatchpointCallback(_In_ uintptr_t context, _In_ Position const&, _In_ IThreadView const*);

    // Allows the caller to observe and reject (result == false) or accept (result != false) a particular gap event stop.
    // ICursorView is not to be used. Most calls might be unsafe when performed from within the callback.
    // IThreadView allows the callback to safely perform limited thread-local queries for registers, memory and state.
    typedef bool __fastcall GapEventCallback(_In_ uintptr_t context, GapKind, GapEventType, _In_ IThreadView const*);

    // Reports to the caller how far replay has completed.
    // During replay, this callback is called synchronously, from the thread which called the ReplayXXX() function.
    // If it doesn't return immediately, the replay scheduler may starve, throttling the replay back.
    // Note: In some situations, this may be desirable, to avoid excessive memory consumption,
    // or to allow a slower consumer of data generated during replay to act as the natural bottleneck.
    // ICursorView is not to be used. Most calls might be unsafe when performed from within the callback.
    typedef void __stdcall ReplayProgressCallback(_In_ uintptr_t context, _In_ Position const& position);

    // Allows the caller to identify when there's a potential break in thread continuity during replay.
    // In between calls to this callback on a particular thread, all other execution callbacks (like WatchpointCallback)
    // are guaranteed to refer to the same thread from the trace and be consecutive in the timeline within that thread.
    // Together with the ReplayProgressCallback, this allows efficient funneling of data out of the replay.
    // ICursorView is not to be used. Most calls might be unsafe when performed from within the callback.
    typedef void __fastcall ThreadContinuityBreakCallback(_In_ uintptr_t context);

    // Allows the caller to monitor execution of fallbacks, and inspect the local context at the point of execution.
    // When synthetic == true, it means that the instruction was emulated at record time, but added to the file as a fallback,
    // to allow the use of replay engines that don't know how to emulate it, like some x64 instructions that don't exist in x86.
    // The address and size parameters indicate where the bytes of the instruction can be found (via a memory query).
    // ICursorView is not to be used. Most calls might be unsafe when performed from within the callback.
    // IThreadView allows the callback to safely perform limited thread-local queries for registers, memory and state.
    typedef void __fastcall FallbackCallback(_In_ uintptr_t context, _In_ bool synthetic, _In_ GuestAddress instructionAddress, _In_ size_t instructionSize, _In_ IThreadView const*);

    // Callback on every call / iretq / return instruction
    // call: guestInstructionAddress = calling to; guestFallThroughInstructionAddress = address of function after the call
    // ret: guestInstructionAddress = returning to; guestFallThroughInstructionAddress = nullptr
    // Note: the current position points to the call or ret instruction
    // ICursorView is not to be used. Most calls might be unsafe when performed from within the callback.
    // IThreadView allows the callback to safely perform limited thread-local queries for registers, memory and state.
    typedef void __fastcall CallReturnCallback(_In_ uintptr_t context, _In_ GuestAddress guestInstructionAddress, _In_ GuestAddress guestFallThroughInstructionAddress, _In_ IThreadView const*);

    // Callback on every jump instruction to an indirect target
    // targetAddress: address that is being jumped to
    typedef void __fastcall IndirectJumpCallback(_In_ uintptr_t context, _In_ GuestAddress targetAddress, _In_ IThreadView const*);

    // Callback whenever register changes are performed outside of execution.
    // If pNewData is called with nullptr, it means the new data is all zero.
    // ICursorView is not to be used. Most calls might be unsafe when performed from within the callback.
    // IThreadView allows the callback to safely perform limited thread-local queries for registers, memory and state.
    typedef void __fastcall RegisterChangedCallback(uintptr_t context, uint8_t regId, _In_reads_bytes_(dataSizeInBytes) void const* pOldData, _In_reads_bytes_opt_(dataSizeInBytes) void const* pNewData, size_t dataSizeInBytes, _In_ IThreadView const*);

    virtual void SetMemoryWatchpointCallback     (_In_opt_ MemoryWatchpointCallback*     , _In_ uintptr_t context) noexcept = 0;
    virtual void SetPositionWatchpointCallback   (_In_opt_ PositionWatchpointCallback*   , _In_ uintptr_t context) noexcept = 0;
    virtual void SetGapEventCallback             (_In_opt_ GapEventCallback*             , _In_ uintptr_t context) noexcept = 0;
    virtual void SetThreadContinuityBreakCallback(_In_opt_ ThreadContinuityBreakCallback*, _In_ uintptr_t context) noexcept = 0;
    virtual void SetReplayProgressCallback       (_In_opt_ ReplayProgressCallback*       , _In_ uintptr_t context) noexcept = 0;
    virtual void SetFallbackCallback             (_In_opt_ FallbackCallback*             , _In_ uintptr_t context) noexcept = 0;
    virtual void SetCallReturnCallback           (_In_opt_ CallReturnCallback*           , _In_ uintptr_t context) noexcept = 0;
    virtual void SetIndirectJumpCallback         (_In_opt_ IndirectJumpCallback*         , _In_ uintptr_t context) noexcept = 0;
    virtual void SetRegisterChangedCallback      (_In_opt_ RegisterChangedCallback*      , _In_ uintptr_t context) noexcept = 0;

    struct ReplayResult
    {
        ReplayResult() {}

        EventType        StopReason{};
        StepCount        StepsExecuted{};
        InstructionCount InstructionsExecuted{};
        union
        {
            MemoryWatchpointResult   MemoryWatchpoint{};
            Position                 PositionWatchpoint;
            GapData                  GapEvent;
            ExceptionEvent const*    pException;

            [[deprecated]] MemoryWatchpointResult Watchpoint;
        };
    };

    // Execute up to the limit position, but no more than the given number of steps.
    virtual ReplayResult ReplayForward (_In_ Position limit, _In_ StepCount = StepCount::Max) noexcept = 0;
    virtual ReplayResult ReplayBackward(_In_ Position limit, _In_ StepCount = StepCount::Max) noexcept = 0;

    // Handy overloads with no position limit.
    ReplayResult ReplayForward (_In_ StepCount stepCount = StepCount::Max) { return ReplayForward (Position::Max, stepCount); }
    ReplayResult ReplayBackward(_In_ StepCount stepCount = StepCount::Max) { return ReplayBackward(Position::Min, stepCount); }

    // This function can be called at any time from any thread.
    // If this cursor is in the middle of a replay operation, it'll finish as quickly as possible and the cursor
    // left at some position between the position where replay started and the first event that would've hit.
    virtual void InterruptReplay() noexcept = 0;

    virtual ICursorInternals*       GetInternals()       noexcept = 0;
    virtual ICursorInternals const* GetInternals() const noexcept = 0;

    enum class InternalDataId : uint32_t { Invalid = 0 };

    // Retrieve an internal blob of data, intended to be used for internal diagnostics and telemetry.
    // The definition of the blobs of data available are not publicly defined.
    virtual size_t GetInternalData(InternalDataId, _Out_writes_bytes_(bufferSizeInBytes) void* pBuffer, size_t bufferSizeInBytes) const noexcept = 0;

    // Convenience adapter to retrieve an internal blob of data of a specific type.
    template < typename T > T GetInternalData() const noexcept { T result{}; GetInternalData(T::InternalDataId, &result, sizeof(result)); return result; }

    // Convenient templated adapters useful to set callbacks from lambdas or other function objects.
    // Just be careful to ensure that the object or lambda capture outlives the calls to ReplayXXX()

    template < typename Func > void SetMemoryWatchpointCallback     (Func& func) noexcept;
    template < typename Func > void SetPositionWatchpointCallback   (Func& func) noexcept;
    template < typename Func > void SetGapEventCallback             (Func& func) noexcept;
    template < typename Func > void SetThreadContinuityBreakCallback(Func& func) noexcept;
    template < typename Func > void SetReplayProgressCallback       (Func& func) noexcept;
    template < typename Func > void SetFallbackCallback             (Func& func) noexcept;
    template < typename Func > void SetCallReturnCallback           (Func& func) noexcept;
    template < typename Func > void SetIndirectJumpCallback         (Func& func) noexcept;
    template < typename Func > void SetRegisterChangedCallback      (Func& func) noexcept;

    // For source backcompat, define old names as deprecated.

    using WatchpointResult [[deprecated]] = MemoryWatchpointResult;

    [[deprecated]] bool AddWatchpoint   (_In_ MemoryWatchpointData const& data) noexcept { return AddMemoryWatchpoint   (data); }
    [[deprecated]] bool RemoveWatchpoint(_In_ MemoryWatchpointData const& data) noexcept { return RemoveMemoryWatchpoint(data); }

    [[deprecated]] void SetWatchpointCallback(_In_opt_ MemoryWatchpointCallback* callback, _In_ uintptr_t context) noexcept { return SetMemoryWatchpointCallback(callback, context); }
    template < typename Func > void SetWatchpointCallback(Func& func) noexcept { return SetMemoryWatchpointCallback(func); }

protected:
    virtual ~ICursorView() = default;
};

template < typename Func >
void ICursorView::SetMemoryWatchpointCallback(Func& func) noexcept
{
    SetMemoryWatchpointCallback([](uintptr_t context, MemoryWatchpointResult const& watchpointResult, IThreadView const* pThreadView) -> bool
        {
            return (*reinterpret_cast<Func*>(context))(watchpointResult, pThreadView);
        },
        reinterpret_cast<uintptr_t>(&func)
    );
}

template < typename Func >
void ICursorView::SetPositionWatchpointCallback(Func& func) noexcept
{
    SetPositionWatchpointCallback([](uintptr_t context, Position const& watchpointPosition, IThreadView const* pThreadView) -> bool
        {
            return (*reinterpret_cast<Func*>(context))(watchpointPosition, pThreadView);
        },
        reinterpret_cast<uintptr_t>(&func)
    );
}

template < typename Func >
void ICursorView::SetGapEventCallback(Func& func) noexcept
{
    SetGapEventCallback([](uintptr_t context, GapKind kind, GapEventType event, IThreadView const* pThreadView) -> bool
        {
            return (*reinterpret_cast<Func*>(context))(kind, event, pThreadView);
        },
        reinterpret_cast<uintptr_t>(&func)
    );
}

template < typename Func >
void ICursorView::SetThreadContinuityBreakCallback(Func& func) noexcept
{
    SetThreadContinuityBreakCallback([](uintptr_t context)
        {
            return (*reinterpret_cast<Func*>(context))();
        },
        reinterpret_cast<uintptr_t>(&func)
    );
}

template < typename Func >
void ICursorView::SetReplayProgressCallback(Func& func) noexcept
{
    SetReplayProgressCallback([](uintptr_t context, _In_ Position const& position)
        {
            return (*reinterpret_cast<Func*>(context))(position);
        },
        reinterpret_cast<uintptr_t>(&func)
    );
}

template < typename Func >
void ICursorView::SetFallbackCallback(Func& func) noexcept
{
    SetFallbackCallback([](uintptr_t context, _In_ bool synthetic, _In_ GuestAddress instructionAddress, _In_ size_t instructionSize, _In_ IThreadView const* pThreadView)
        {
            return (*reinterpret_cast<Func*>(context))(synthetic, instructionAddress, instructionSize, pThreadView);
        },
        reinterpret_cast<uintptr_t>(&func)
    );
}

template < typename Func >
void ICursorView::SetCallReturnCallback(Func& func) noexcept
{
    SetCallReturnCallback([](uintptr_t context, _In_ GuestAddress guestInstructionAddress, _In_ GuestAddress guestFallThroughInstructionAddress, _In_ IThreadView const* pThreadView)
        {
            return (*reinterpret_cast<Func*>(context))(guestInstructionAddress, guestFallThroughInstructionAddress, pThreadView);
        },
        reinterpret_cast<uintptr_t>(&func)
    );
}

template < typename Func >
void ICursorView::SetIndirectJumpCallback(Func& func) noexcept
{
    SetIndirectJumpCallback([](uintptr_t context, GuestAddress targetAddress, _In_ IThreadView const* pThreadView)
        {
            return (*reinterpret_cast<Func*>(context))(targetAddress, pThreadView);
        },
        reinterpret_cast<uintptr_t>(&func)
    );
}

template < typename Func >
void ICursorView::SetRegisterChangedCallback(Func& func) noexcept
{
    SetRegisterChangedCallback([](uintptr_t context, uint8_t regId, _In_reads_bytes_(dataSizeInBytes) void const* pOldData, _In_reads_bytes_opt_(dataSizeInBytes) void const* pNewData, size_t dataSizeInBytes, _In_ IThreadView const* pThreadView)
        {
            return (*reinterpret_cast<Func*>(context))(regId, pOldData, pNewData, dataSizeInBytes, pThreadView);
        },
        reinterpret_cast<uintptr_t>(&func)
    );
}

#if _MSVC_LANG >= 202002L

// ICursorView convenience functions

inline constexpr auto ActiveThreads(ICursorView const* pCursorView) noexcept
{
    return std::span(pCursorView->GetThreadList(), pCursorView->GetThreadCount());
}

inline constexpr auto ModuleInstances(ICursorView const* pCursorView) noexcept
{
    return std::span(pCursorView->GetModuleList(), pCursorView->GetModuleCount());
}

#endif // _MSVC_LANG >= 202002L

// Helper functions for writing code that works with ICursorView or IThreadView
// TODO: Look at converging ICursorView / IThreadView interfaces to make interop easier

inline MemoryBuffer QueryMemoryBuffer(
    _In_ ICursorView const* pCursor,
    _In_ GuestAddress       address,
    _In_ BufferView         bufferView)
{
    return pCursor->QueryMemoryBuffer(address, bufferView);
}

inline MemoryBuffer QueryMemoryBuffer(
    _In_ IThreadView const* pThreadView,
    _In_ GuestAddress       address,
    _In_ BufferView         bufferView)
{
    return pThreadView->QueryMemoryBuffer(address, bufferView);
}

inline UniqueThreadId GetUniqueThreadId(_In_ ICursorView const* pCursor)
{
    return pCursor->GetThreadInfo().UniqueId;
}

inline UniqueThreadId GetUniqueThreadId(_In_ IThreadView const* pThreadView)
{
    return pThreadView->GetThreadInfo().UniqueId;
}

// This is the main replay engine interface to use for a fully-loaded replay engine.
// Provides for global queries on the trace file, and creation of cursors.
class IReplayEngineView
{
public:
    // Unsafe exported functions. In order to use them, they require the appropriate cast.
    virtual void*       UnsafeAsInterface(GUID const&)       noexcept = 0;
    virtual void const* UnsafeAsInterface(GUID const&) const noexcept = 0;

    template < typename Interface > auto AsInterface()       noexcept { return static_cast<Interface*      >(UnsafeAsInterface(__uuidof(Interface))); }
    template < typename Interface > auto AsInterface() const noexcept { return static_cast<Interface const*>(UnsafeAsInterface(__uuidof(Interface))); }

    virtual GuestAddress          GetPebAddress   () const noexcept = 0;
    virtual SystemInfo     const& GetSystemInfo   () const noexcept = 0;
    // TODO: Remove these two APIs since we can now get the lifetime
    virtual Position       const& GetFirstPosition() const noexcept = 0;
    virtual Position       const& GetLastPosition () const noexcept = 0;
    virtual PositionRange  const& GetLifetime     () const noexcept = 0;

    virtual RecordingType GetRecordingType() const noexcept = 0;

    virtual ThreadInfo const& GetThreadInfo(_In_ UniqueThreadId) const noexcept = 0;
    virtual size_t            GetThreadCount                     () const noexcept = 0;
    virtual ThreadInfo const* GetThreadList                      () const noexcept = 0;
    virtual size_t const*     GetThreadFirstPositionIndex        () const noexcept = 0;
    virtual size_t const*     GetThreadLastPositionIndex         () const noexcept = 0;
    virtual size_t const*     GetThreadLifetimeFirstPositionIndex() const noexcept = 0;
    virtual size_t const*     GetThreadLifetimeLastPositionIndex () const noexcept = 0;

    virtual size_t                       GetThreadCreatedEventCount   () const noexcept = 0;
    virtual ThreadCreatedEvent const*    GetThreadCreatedEventList    () const noexcept = 0;
    virtual size_t                       GetThreadTerminatedEventCount() const noexcept = 0;
    virtual ThreadTerminatedEvent const* GetThreadTerminatedEventList () const noexcept = 0;

    virtual size_t        GetModuleCount() const noexcept = 0;
    virtual Module const* GetModuleList () const noexcept = 0;

    virtual size_t                GetModuleInstanceCount      () const noexcept = 0;
    virtual ModuleInstance const* GetModuleInstanceList       () const noexcept = 0;
    virtual size_t const*         GetModuleInstanceUnloadIndex() const noexcept = 0;

    virtual size_t                     GetModuleLoadedEventCount  () const noexcept = 0;
    virtual ModuleLoadedEvent   const* GetModuleLoadedEventList   () const noexcept = 0;
    virtual size_t                     GetModuleUnloadedEventCount() const noexcept = 0;
    virtual ModuleUnloadedEvent const* GetModuleUnloadedEventList () const noexcept = 0;

    virtual size_t                GetExceptionEventCount() const noexcept = 0;
    virtual ExceptionEvent const* GetExceptionEventList () const noexcept = 0;

    virtual ExceptionEvent const* GetExceptionAtOrAfterPosition(_In_ Position const&) const noexcept = 0;

    virtual size_t          GetKeyframeCount() const noexcept = 0;
    virtual Position const* GetKeyframeList() const noexcept = 0;

    virtual size_t              GetRecordClientCount() const noexcept = 0;
    virtual RecordClient const* GetRecordClientList () const noexcept = 0;
    virtual RecordClient const& GetRecordClient     (RecordClientId) const noexcept = 0;

    virtual size_t             GetCustomEventCount() const noexcept = 0;
    virtual CustomEvent const* GetCustomEventList () const noexcept = 0;

    virtual size_t          GetActivityCount() const noexcept = 0;
    virtual Activity const* GetActivityList () const noexcept = 0;

    virtual size_t        GetIslandCount() const noexcept = 0;
    virtual Island const* GetIslandList () const noexcept = 0;

    // Create a cursor we can use to move through the trace file, and to query for data contextually.
    virtual ICursor* NewCursor(GUID const& = __uuidof(ICursorView)) noexcept = 0;

    // Builds the global memory index for the trace file, if one has not already
    // been built.  (TODO:  This should be asynchronous and should return a
    // task-like object that provides cancellation and progress reporting.)
    virtual IndexStatus BuildIndex(
        _In_     IndexBuildProgressCallback reportProgress,
        _In_opt_ void const*                pCallerContext,
        _In_     IndexBuildFlags            flags = IndexBuildFlags::None
        ) noexcept = 0;

    virtual IndexStatus GetIndexStatus() const noexcept = 0;

    virtual IndexFileStats GetIndexFileStats() const noexcept = 0;

    virtual void RegisterDebugModeAndLogging(
                 DebugModeType   const debugMode,
        _In_opt_ ErrorReporting* const pErrorReporting
    ) noexcept = 0;

    [[deprecated]] void RegisterErrorAndWarningCallbacks(
        _In_     void const*,
        _In_     void const*,
        _In_     DebugModeType      mode,
        _In_opt_ ErrorReporting*    pErrorReporting
        ) noexcept
    {
        RegisterDebugModeAndLogging(mode, pErrorReporting);
    }

    virtual IEngineInternals*       GetInternals()       noexcept = 0;
    virtual IEngineInternals const* GetInternals() const noexcept = 0;
protected:
    virtual ~IReplayEngineView() = default;
};

// Convenience functions for working with lists of things in replay engine

#if _MSVC_LANG >= 202002L

// IReplayEngineView convenience functions

inline constexpr auto Activities(IReplayEngineView const* pReplayEngineView) noexcept
{
    return std::span(pReplayEngineView->GetActivityList(), pReplayEngineView->GetActivityCount());
}

inline constexpr auto CustomEvents(IReplayEngineView const* pReplayEngineView) noexcept
{
    return std::span(pReplayEngineView->GetCustomEventList(), pReplayEngineView->GetCustomEventCount());
}

inline constexpr auto ExceptionEvents(IReplayEngineView const* pReplayEngineView) noexcept
{
    return std::span(pReplayEngineView->GetExceptionEventList(), pReplayEngineView->GetExceptionEventCount());
}

inline constexpr auto Islands(IReplayEngineView const* pReplayEngineView) noexcept
{
    return std::span(pReplayEngineView->GetIslandList(), pReplayEngineView->GetIslandCount());
}

inline constexpr auto Keyframes(IReplayEngineView const* pReplayEngineView) noexcept
{
    return std::span(pReplayEngineView->GetKeyframeList(), pReplayEngineView->GetKeyframeCount());
}

inline constexpr auto Modules(IReplayEngineView const* pReplayEngineView) noexcept
{
    return std::span(pReplayEngineView->GetModuleList(), pReplayEngineView->GetModuleCount());
}

inline constexpr auto ModuleInstances(IReplayEngineView const* pReplayEngineView) noexcept
{
    return std::span(pReplayEngineView->GetModuleInstanceList(), pReplayEngineView->GetModuleInstanceCount());
}

inline constexpr auto ModuleLoadedEvents(IReplayEngineView const* pReplayEngineView) noexcept
{
    return std::span(pReplayEngineView->GetModuleLoadedEventList(), pReplayEngineView->GetModuleLoadedEventCount());
}

inline constexpr auto ModuleUnloadedEvents(IReplayEngineView const* pReplayEngineView) noexcept
{
    return std::span(pReplayEngineView->GetModuleUnloadedEventList(), pReplayEngineView->GetModuleUnloadedEventCount());
}

inline constexpr auto RecordClients(IReplayEngineView const* pReplayEngineView) noexcept
{
    return std::span(pReplayEngineView->GetRecordClientList(), pReplayEngineView->GetRecordClientCount());
}

inline constexpr auto Threads(IReplayEngineView const* pReplayEngineView) noexcept
{
    return std::span(pReplayEngineView->GetThreadList(), pReplayEngineView->GetThreadCount());
}

inline constexpr auto ThreadCreatedEvents(IReplayEngineView const* pReplayEngineView) noexcept
{
    return std::span(pReplayEngineView->GetThreadCreatedEventList(), pReplayEngineView->GetThreadCreatedEventCount());
}

inline constexpr auto ThreadTerminatedEvents(IReplayEngineView const* pReplayEngineView) noexcept
{
    return std::span(pReplayEngineView->GetThreadTerminatedEventList(), pReplayEngineView->GetThreadTerminatedEventCount());
}

// Convenience functions getting the module name in null-terminated form

inline constexpr auto ModuleName(Module const& module)
{
    return std::wstring(module.pName, module.NameLength);
}   

inline constexpr auto ModuleName(ModuleInstance const& moduleInstance)
{
    return ModuleName(*moduleInstance.pModule);
}   

#endif // _MSVC_LANG >= 202002L

enum class TraceFileType : uint8_t
{
    Trace = 0, // Trace file as recorded by TTD (usually .run).
    Index = 1, // Index file, as generated from a .run file (usually .idx, or .ttd if self-contained).
    Pack  = 2, // File containing multiple self-contained traces (usually .ttd).
};

// Information about where the trace resides: which file(s) and where within the file(s).
// If not nullptr, the companion file for a trace file will be the associated index file.
// The companion file for an index file will be a trace file providing the execution streams.
// TraceIndex indicates which trace within the given file.
// Trace and index files only contain one trace, so in that case the index will be zero.
struct TraceFileInfo
{
    TraceFileType  FileType;
    wchar_t const* pFileName;
    wchar_t const* pCompanionFileName; // May be nullptr.
    uint32_t       TraceIndex;
};

// Information about the contents of the trace, and also its location.
struct TraceInfo
{
    GUID           SessionId;
    GUID           GroupId;
    RecordingType  RecordingType;
    TraceFileInfo  FileInfo;
    // TODO: Add some metadata.
};

// This is the interface to a collection of traces.
// Initially, the list is empty. Call LoadFile one or more times to add files to the list.
// Enumerate the list anytime by calling GetTraceCount and GetTraceInfo.
// Then, OpenTrace can be called to create a replay engine for any individual trace in the list.
class ITraceListView
{
public:
    // Unsafe exported functions. In order to use them, they require the appropriate cast.
    virtual void*       UnsafeAsInterface(GUID const&)       noexcept = 0;
    virtual void const* UnsafeAsInterface(GUID const&) const noexcept = 0;

    template < typename Interface > auto AsInterface()       noexcept { return static_cast<Interface*      >(UnsafeAsInterface(__uuidof(Interface))); }
    template < typename Interface > auto AsInterface() const noexcept { return static_cast<Interface const*>(UnsafeAsInterface(__uuidof(Interface))); }

    // Load a file and add any traces within it to the list.
    // Note that this can be called multiple times to add multiple traces or packs to the list.
    virtual bool LoadFile(_In_z_ wchar_t const* pFileName, _In_opt_z_ wchar_t const* pCompanionName = nullptr) noexcept = 0;

    virtual size_t           GetTraceCount()             const noexcept = 0;
    virtual TraceInfo const& GetTraceInfo (size_t index) const noexcept = 0;

    // Create a cursor we can use to move through the trace file, and to query for data contextually.
    virtual IReplayEngine* OpenTrace(size_t index, GUID const& = __uuidof(IReplayEngineView)) noexcept = 0;

    virtual void RegisterDebugModeAndLogging(
                 DebugModeType   const debugMode,
        _In_opt_ ErrorReporting* const pErrorReporting
    ) noexcept = 0;

    [[deprecated]] void RegisterErrorAndWarningCallbacks(
        _In_     void const*,
        _In_     void const*,
        _In_     DebugModeType      mode,
        _In_opt_ ErrorReporting*    pErrorReporting
        ) noexcept
    {
        RegisterDebugModeAndLogging(mode, pErrorReporting);
    }

protected:
    virtual ~ITraceListView() = default;
};

// Owning cursor interface. It's the caller's responsibility to use it as such so it's destroyed only once.
// Note that, after destroying the engine that backs up the cursor,
// the only operation that is guaranteed to be valid on the cursor is Destroy().
class ICursor : public ICursorView
{
public:
    friend IReplayEngine;
    friend Deleter<ICursor>;

    // Private, but accessible to the Deleter class;
    virtual void Destroy() noexcept = 0;
};

// Owning engine interface. It's the caller's responsibility to use it as such so it's destroyed only once.
class IReplayEngine : public IReplayEngineView
{
public:
    friend Deleter<IReplayEngine>;

    // Private, but accessible to the Deleter class;
    virtual void Destroy() noexcept = 0;

    // Intialize the engine to replay the given file. May be called just once.
    virtual bool Initialize(_In_z_ wchar_t const* pTraceFileName) noexcept = 0;
};

// Owning trace list interface. It's the caller's responsibility to use it as such so it's destroyed only once.
class ITraceList : public ITraceListView
{
public:
    friend Deleter<ITraceList>;

    // Private, but accessible to the Deleter class;
    virtual void Destroy() noexcept = 0;
};

// For source backcompat, define old names as deprecated aliases.
using WatchpointData [[deprecated]] = MemoryWatchpointData;

}} // namespace TTD::Replay

#pragma pop_macro("DBG_ASSERT_MSG")
#pragma pop_macro("DBG_ASSERT")
