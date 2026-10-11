// Copyright (c) Microsoft Corporation.
//
// This file defines a variety of types used by TTD APIs.

#pragma once

#include <stdint.h>
#include <stddef.h>

#include <type_traits>

// Macro that defines operators for flags types implemented as enums. For convenience.
#define TTD_DEFINE_ENUM_FLAG_OPERATORS(ENUMTYPE) \
    constexpr ENUMTYPE operator |(ENUMTYPE a, ENUMTYPE b) { return static_cast<ENUMTYPE>( static_cast<std::underlying_type_t<ENUMTYPE>>(a) | static_cast<std::underlying_type_t<ENUMTYPE>>(b)); } \
    constexpr ENUMTYPE operator &(ENUMTYPE a, ENUMTYPE b) { return static_cast<ENUMTYPE>( static_cast<std::underlying_type_t<ENUMTYPE>>(a) & static_cast<std::underlying_type_t<ENUMTYPE>>(b)); } \
    constexpr ENUMTYPE operator ^(ENUMTYPE a, ENUMTYPE b) { return static_cast<ENUMTYPE>( static_cast<std::underlying_type_t<ENUMTYPE>>(a) ^ static_cast<std::underlying_type_t<ENUMTYPE>>(b)); } \
    constexpr ENUMTYPE operator ~(ENUMTYPE a)             { return static_cast<ENUMTYPE>(~static_cast<std::underlying_type_t<ENUMTYPE>>(a)); } \
    constexpr bool     operator !(ENUMTYPE a)             { return static_cast<std::underlying_type_t<ENUMTYPE>>(a) == static_cast<std::underlying_type_t<ENUMTYPE>>(0); } \
    inline ENUMTYPE& operator |=(ENUMTYPE& a, ENUMTYPE b) { return a = a | b; } \
    inline ENUMTYPE& operator &=(ENUMTYPE& a, ENUMTYPE b) { return a = a & b; } \
    inline ENUMTYPE& operator ^=(ENUMTYPE& a, ENUMTYPE b) { return a = a ^ b; } \

namespace TTD
{

// Represents a number of instructions, encapsulated as a stronger-typed enum.
// Counting instructions is a core activity of TTD that is used at all levels of recording and replay.
enum class InstructionCount : uint64_t
{
    Zero    = 0,
    Min     = 0,
    Max     = UINT64_MAX - 1,

    Invalid = UINT64_MAX
};

// Convenience arithmetic operators on instruction counts.

constexpr InstructionCount operator+ (InstructionCount a, uint64_t  b) { return static_cast<InstructionCount>(static_cast<uint64_t>(a) + b); }
constexpr InstructionCount operator- (InstructionCount a, uint64_t  b) { return static_cast<InstructionCount>(static_cast<uint64_t>(a) - b); }
constexpr InstructionCount operator* (InstructionCount a, uint64_t  b) { return static_cast<InstructionCount>(static_cast<uint64_t>(a) * b); }
constexpr InstructionCount operator/ (InstructionCount a, uint64_t  b) { return static_cast<InstructionCount>(static_cast<uint64_t>(a) / b); }
constexpr uint64_t         operator% (InstructionCount a, uint64_t  b) { return                               static_cast<uint64_t>(a) % b ; }
constexpr InstructionCount operator+ (InstructionCount a, InstructionCount b) { return a + static_cast<uint64_t>(b); }
constexpr InstructionCount operator- (InstructionCount a, InstructionCount b) { return a - static_cast<uint64_t>(b); }

// Convenience comparison of instruction counts and raw intgrals.

constexpr bool operator==(InstructionCount a, uint64_t b) { return static_cast<uint64_t>(a) == b; }
constexpr bool operator!=(InstructionCount a, uint64_t b) { return static_cast<uint64_t>(a) != b; }
constexpr bool operator< (InstructionCount a, uint64_t b) { return static_cast<uint64_t>(a) <  b; }
constexpr bool operator> (InstructionCount a, uint64_t b) { return static_cast<uint64_t>(a) >  b; }
constexpr bool operator<=(InstructionCount a, uint64_t b) { return static_cast<uint64_t>(a) <= b; }
constexpr bool operator>=(InstructionCount a, uint64_t b) { return static_cast<uint64_t>(a) >= b; }

// Convenience assignment arithmetic operators on instruction counts.

inline InstructionCount& operator+=(_Inout_ InstructionCount& a, int64_t          b) { return a = static_cast<InstructionCount>(static_cast<uint64_t>(a) + b); }
inline InstructionCount& operator-=(_Inout_ InstructionCount& a, int64_t          b) { return a = static_cast<InstructionCount>(static_cast<uint64_t>(a) - b); }
inline InstructionCount& operator+=(_Inout_ InstructionCount& a, InstructionCount b) { return a += static_cast<uint64_t>(b); }
inline InstructionCount& operator-=(_Inout_ InstructionCount& a, InstructionCount b) { return a -= static_cast<uint64_t>(b); }


// Selectable flags for generating custom events.
enum class CustomEventFlags : uint32_t
{
    // Keyframe events force the generation of a keyframe at the current position.
    // This allows the custom event's position to be more acurately ordered with other
    // common events, like observations of memory values.
    // Note that generating keyframes may cost significant performance when recording, so use with care.
    Keyframe = 0x0000'0001,

    None = 0,
    All  = 0xFFFF'FFFF,
};
TTD_DEFINE_ENUM_FLAG_OPERATORS(CustomEventFlags)

// Client-managed value that identifies a particular activity.
// An activity is a grouping of portions of recording (islands) that has some meaningful significance to the client.
// For instance, multiple portions of an algorithm that may run on multiple tasks on a threadpool.
// The client provides this ID when uses Start API[s] to record a portion of a thread.
// The provided Null value is used to signify no/unknown/dontcare,
// and will be used as the activity ID for all recording that is not under control of a client
// (e.g. by just recording an unsuspecting process via TTD.exe).
enum class ActivityId : uint32_t
{
    Null = 0,
    Min  = 1,
    Max  = UINT32_MAX,
};

// Represents the progress towards the throttling limit specified for the current island of recording.
struct ThrottleState
{
    InstructionCount InstructionsExecuted;
    InstructionCount InstructionsRemaining;
};

// Represents all possibly valid states of a thread, with respect to TTD's recording engine.
enum class ThreadRecordingState : uint8_t
{
    NotStarted = 0, // Outside of a Start/Stop recording sequence.
    Recording  = 1, // Inside of a Start/Stop recording sequence and currently being recorded.
    Paused     = 2, // Inside of a Start/Stop recording sequence, but recording has been temporarily paused by calling the API.
    Throttled  = 3, // Inside of a Start/Stop recording sequence, but recording has been halted by the throttling mechanism.
};

// Different types of custom events.
// Thread-local events are recorder as part of the calling thread, if it's currently recording.
// If the calling thread is not currently recording, thread-local events are still recorded, but as global events.
// Global events are not attached to any thread.
enum class CustomEventType : uint8_t
{
    ThreadLocal = 0,
    Global      = 1,
};

// The maximum size, in bytes, of a single piece of user data to be stored in the trace file is 16 KB.
constexpr size_t MaxUserDataSizeInBytes = 16 * 1024;

}
// namespace TTD
