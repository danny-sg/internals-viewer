// Copyright (c) Microsoft Corporation.
#pragma once

#if !defined(__cplusplus)
#error TTD is a C++ API. It cannot be used from plain C.
#endif

#include "TypeUtilities.h"
#include "TTDCommonTypes.h"

#include <stdint.h>
#include <stddef.h>

#include <type_traits>

#include <sal.h>
#include <intrin.h>

#if _M_IX86 || _M_X64
#include <immintrin.h>
#endif // _M_IX86 || _M_X64

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

// Some useful macros to conditionally compile depending on the compiler and the target architecture.

#define TTD_IS_INTEL_CPU     (_M_IX86 || (_M_X64 && !_M_ARM64EC))
#define TTD_IS_ARM64_CPU     (_M_ARM64 || _M_ARM64EC)
#define TTD_IS_MSVC_COMPILER (_WIN32 && !__clang__)

namespace TTD
{

// Types defined to represent SIMD types for any CPU.
// Using these works around the differences of SSE/AVX on Intel/AMD CPUs vs. NEON on ARM CPUs.
// It also allows access to single elements, which compilers like Clang don't even offer, in a portable manner.
// Whenever possible, conversion and assignment operators to/from the compiler's native SIMD types are provided.
// This conditional support for the compiler's native SIMD types technically causes a benign form of ODR violation.
// It is benign because the types' alignment and size remain the same throughout and any member conditionally offered
// is defined exactly the same or not at all.

union alignas(8) simd64_t
{
    uint8_t  u8 [8];
    uint16_t u16[4];
    uint32_t u32[2];
    uint64_t u64[1];
    int8_t   s8 [8];
    int16_t  s16[4];
    int32_t  s32[2];
    int64_t  s64[1];

    operator uint64_t() const { return u64[0]; }
    operator int64_t () const { return s64[0]; }
    simd64_t& operator= (uint64_t x) { u64[0] = x; return *this; }
    simd64_t& operator= (int64_t  x) { s64[0] = x; return *this; }

#if TTD_IS_INTEL_CPU && (TTD_IS_MSVC_COMPILER || defined(__MMX__))
    __m64 value;

    operator __m64 const&() const { return value; } // Reference because otherwise the MSVC compiler incorrectly complains about the lack of EMMS.
    simd64_t& operator= (__m64 x) { value = x; return *this; }
#elif TTD_IS_ARM64_CPU
    __n64 value;

    operator __n64() const { return value; }
    simd64_t& operator= (__n64 x) { value = x; return *this; }
#endif
};

union alignas(16) simd128_t
{
    uint8_t  u8 [16];
    uint16_t u16[ 8];
    uint32_t u32[ 4];
    uint64_t u64[ 2];
    int8_t   s8 [16];
    int16_t  s16[ 8];
    int32_t  s32[ 4];
    int64_t  s64[ 2];
    float    f32[ 4];
    double   f64[ 2];

#if TTD_IS_INTEL_CPU && (TTD_IS_MSVC_COMPILER || defined(__SSE2__))
    __m128i value;

    simd128_t& operator= (simd128_t const& x) = default;

    operator __m128i() const { return value; }
    simd128_t& operator= (__m128i x) { value = x; return *this; }
#if !__clang__
    operator __m128 () const { return _mm_castsi128_ps(value); }
    operator __m128d() const { return _mm_castsi128_pd(value); }
    simd128_t& operator= (__m128  x) { value = _mm_castps_si128(x); return *this; }
    simd128_t& operator= (__m128d x) { value = _mm_castpd_si128(x); return *this; }
#endif // !__clang__
#elif TTD_IS_ARM64_CPU
    __n128 value;

    operator __n128() const { return value; }
    simd128_t& operator= (__n128 x) { value = x; return *this; }
#endif
};

union alignas(32) simd256_t
{
    uint8_t   u8  [32];
    uint16_t  u16 [16];
    uint32_t  u32 [ 8];
    uint64_t  u64 [ 4];
    int8_t    s8  [32];
    int16_t   s16 [16];
    int32_t   s32 [ 8];
    int64_t   s64 [ 4];
    simd128_t m128[ 2];
    float     f32 [ 8];
    double    f64 [ 4];

#if TTD_IS_INTEL_CPU && (TTD_IS_MSVC_COMPILER || defined(__AVX__))
    __m256i value;

    simd256_t& operator= (simd256_t const& x) = default;

    operator __m256i() const { return value; }
    simd256_t& operator= (__m256i x) { value = x; return *this; }
#if !__clang__
    operator __m256 () const { return _mm256_castsi256_ps(value); }
    operator __m256d() const { return _mm256_castsi256_pd(value); }
    simd256_t& operator= (__m256  x) { value = _mm256_castps_si256(x); return *this; }
    simd256_t& operator= (__m256d x) { value = _mm256_castpd_si256(x); return *this; }
#endif // !__clang__
#elif TTD_IS_ARM64_CPU
    __n128x2 value;

    operator __n128x2() const { return value; }
    simd256_t& operator= (__n128x2 x) { value = x; return *this; }
#endif
};

union alignas(64) simd512_t
{
    uint8_t   u8  [64];
    uint16_t  u16 [32];
    uint32_t  u32 [16];
    uint64_t  u64 [ 8];
    int8_t    s8  [64];
    int16_t   s16 [32];
    int32_t   s32 [16];
    int64_t   s64 [ 8];
    simd128_t m128[ 4];
    simd256_t m256[ 2];
    float     f32 [16];
    double    f64 [ 8];

#if TTD_IS_INTEL_CPU && (TTD_IS_MSVC_COMPILER || defined(__AVX512F__))
    __m512i value;

    simd512_t& operator= (simd512_t const& x) = default;

    operator __m512i() const { return value; }
    simd512_t& operator= (__m512i x) { value = x; return *this; }
#if !__clang__
    operator __m512 () const { return _mm512_castsi512_ps(value); }
    operator __m512d() const { return _mm512_castsi512_pd(value); }
    simd512_t& operator= (__m512  x) { value = _mm512_castps_si512(x); return *this; }
    simd512_t& operator= (__m512d x) { value = _mm512_castpd_si512(x); return *this; }
#endif // !__clang__
#elif TTD_IS_ARM64_CPU
    __n128x4 value;

    operator __n128x4() const { return value; }
    simd512_t& operator= (__n128x4 x) { value = x; return *this; }
#endif
};

// For convenience, we define our own macro to define operators for flags types implemented as enums.
#define TTD_DEFINE_ENUM_FLAG_OPERATORS(ENUMTYPE) \
    constexpr ENUMTYPE operator |(ENUMTYPE a, ENUMTYPE b) { return static_cast<ENUMTYPE>( static_cast<std::underlying_type_t<ENUMTYPE>>(a) | static_cast<std::underlying_type_t<ENUMTYPE>>(b)); } \
    constexpr ENUMTYPE operator &(ENUMTYPE a, ENUMTYPE b) { return static_cast<ENUMTYPE>( static_cast<std::underlying_type_t<ENUMTYPE>>(a) & static_cast<std::underlying_type_t<ENUMTYPE>>(b)); } \
    constexpr ENUMTYPE operator ^(ENUMTYPE a, ENUMTYPE b) { return static_cast<ENUMTYPE>( static_cast<std::underlying_type_t<ENUMTYPE>>(a) ^ static_cast<std::underlying_type_t<ENUMTYPE>>(b)); } \
    constexpr ENUMTYPE operator ~(ENUMTYPE a)             { return static_cast<ENUMTYPE>(~static_cast<std::underlying_type_t<ENUMTYPE>>(a)); } \
    constexpr bool     operator !(ENUMTYPE a)             { return static_cast<std::underlying_type_t<ENUMTYPE>>(a) == static_cast<std::underlying_type_t<ENUMTYPE>>(0); } \
    inline ENUMTYPE& operator |=(ENUMTYPE& a, ENUMTYPE b) { return a = a | b; } \
    inline ENUMTYPE& operator &=(ENUMTYPE& a, ENUMTYPE b) { return a = a & b; } \
    inline ENUMTYPE& operator ^=(ENUMTYPE& a, ENUMTYPE b) { return a = a ^ b; } \

// This is the type of addresses into the guest process, used to access guest code and data.
// Note that it's 64 bits, even when the guest is only 32 bits. In that case, the high 32 bits _should_ be zero.
enum class GuestAddress : uint64_t
{
    Null = 0,
    Min  = 0,
    Max  = UINT64_MAX,
};

// Offset functions.
__forceinline constexpr GuestAddress  operator+ (        GuestAddress  addr, int64_t value) { return static_cast<GuestAddress>(static_cast<uint64_t>(addr) + value); }
__forceinline constexpr GuestAddress  operator- (        GuestAddress  addr, int64_t value) { return static_cast<GuestAddress>(static_cast<uint64_t>(addr) - value); }
__forceinline           GuestAddress& operator+=(_Inout_ GuestAddress& addr, int64_t value) { return addr = static_cast<GuestAddress>(static_cast<uint64_t>(addr) + value); }
__forceinline           GuestAddress& operator-=(_Inout_ GuestAddress& addr, int64_t value) { return addr = static_cast<GuestAddress>(static_cast<uint64_t>(addr) - value); }

// Signed distance function.
__forceinline constexpr int64_t operator-(GuestAddress addr1, GuestAddress addr2) { return static_cast<int64_t>(addr1) - static_cast<int64_t>(addr2); }

// Bit twiddling.
__forceinline constexpr uint64_t     operator~(GuestAddress addr) { return ~static_cast<uint64_t>(addr); }
__forceinline constexpr GuestAddress operator&(GuestAddress addr1, uint64_t num2) { return static_cast<GuestAddress>(static_cast<uint64_t>(addr1) & num2); }
__forceinline constexpr GuestAddress operator|(GuestAddress addr1, uint64_t num2) { return static_cast<GuestAddress>(static_cast<uint64_t>(addr1) | num2); }
__forceinline constexpr GuestAddress operator^(GuestAddress addr1, uint64_t num2) { return static_cast<GuestAddress>(static_cast<uint64_t>(addr1) ^ num2); }

__forceinline constexpr GuestAddress operator&(GuestAddress addr1, GuestAddress addr2) { return addr1 & static_cast<uint64_t>(addr2); }
__forceinline constexpr GuestAddress operator|(GuestAddress addr1, GuestAddress addr2) { return addr1 | static_cast<uint64_t>(addr2); }
__forceinline constexpr GuestAddress operator^(GuestAddress addr1, GuestAddress addr2) { return addr1 ^ static_cast<uint64_t>(addr2); }

// Modulo (for alignment checks)
__forceinline constexpr uint64_t     operator%(GuestAddress addr1, uint64_t num2) { return static_cast<uint64_t>(addr1) % num2; }

// When recording, it's convenient to be able to cast between native guest pointers and GuestAddress.
// Note that this only makes sense when running in the context of the guest.
// In particular, 64-bit guest pointers in a 32-bit host cannot be represented as host pointers.
// That situation should be rare, however, as only x64 is used as the universal replay host.
__forceinline GuestAddress PtrToGuestAddress(void const volatile* ptr) { return static_cast<GuestAddress>(reinterpret_cast<uintptr_t>(ptr)); }

template < typename T = void const > __forceinline T* GuestAddressToPtr(GuestAddress addr) { return reinterpret_cast<T*>(static_cast<uintptr_t>(addr)); }

struct GuestAddressRange
{
    GuestAddress Min = GuestAddress::Null;
    GuestAddress Max = GuestAddress::Null;
};

constexpr bool operator==(GuestAddressRange const& a, GuestAddressRange const& b) { return a.Min == b.Min && a.Max == b.Max; }
constexpr bool operator!=(GuestAddressRange const& a, GuestAddressRange const& b) { return a.Min != b.Min || a.Max != b.Max; }

constexpr bool AddressInClosedRange(GuestAddressRange const& range, GuestAddress const address)
{
    return address >= range.Min && address <= range.Max;
}

constexpr bool AddressInHalfOpenRange(GuestAddressRange const& range, GuestAddress const address)
{
    return address >= range.Min && address < range.Max;
}

enum class SelectRegisters : uint8_t
{
    None              = 0,
    BasicRegisters    = 1,
    ExtendedRegisters = 2,
    AllRegisters      = 3
};

// Identifies the host or guest processor architecture.
enum class ProcessorArchitecture : uint8_t
{
    Invalid = 0,
    x86     = 1,
    x64     = 2,
    ARM32   = 3,
    Arm64   = 4, // Note: on the arm64 build, the build system define a ARM64 macro that would conflict with the constant we are defining here
};


constexpr char const* GetProcessorArchitectureName(ProcessorArchitecture architecture)
{
    switch (architecture)
    {
        case ProcessorArchitecture::Invalid: return "Invalid";
        case ProcessorArchitecture::x86    : return "x86";
        case ProcessorArchitecture::x64    : return "x64";
        case ProcessorArchitecture::ARM32  : return "ARM32";
        case ProcessorArchitecture::Arm64  : return "ARM64";
        default                            : return "Unknown";
    }
}


#if _M_IX86
constexpr ProcessorArchitecture c_nativeProcessorArchitecture = ProcessorArchitecture::x86;
#elif _M_X64 && !_M_ARM64EC
constexpr ProcessorArchitecture c_nativeProcessorArchitecture = ProcessorArchitecture::x64;
#elif _M_ARM64 || _M_ARM64EC // Note: at least for now we ignore the differences between classic/pure ARM64 and ARM64EC
constexpr ProcessorArchitecture c_nativeProcessorArchitecture = ProcessorArchitecture::Arm64;
#else
#error Unsupported host platform
#endif

constexpr GuestAddress SanitizeAddressForUserMode(ProcessorArchitecture const architecture, GuestAddress const address)
{
    switch (architecture)
    {
        case ProcessorArchitecture::x86:
        case ProcessorArchitecture::ARM32:
            return address & 0x0000'0000'FFFF'FFFFllu;

        case ProcessorArchitecture::x64:
        case ProcessorArchitecture::Arm64:
            return address & 0x0000'FFFF'FFFF'FFFFllu;

        default:
            return address;
    }
}

enum class MessageSeverity : uint8_t
{
    Debug   = 0,
    Info    = 1,
    Warning = 2,
    Error   = 3,
    Fatal   = 4,
};

// Loading/injection definitions.

enum class InjectMode : uint8_t
{
    LoaderForRecording         = 0,
    LoaderForCombinedRecording = 1,
    LoaderForEmulator          = 2,
    EmulatorForRecording       = 3,
    EmulatorOnly               = 4,
};

constexpr wchar_t const* GetInjectModeName(InjectMode const injectMode) noexcept
{
    switch (injectMode)
    {
        case InjectMode::LoaderForRecording        : return L"LoaderForRecording";
        case InjectMode::LoaderForCombinedRecording: return L"LoaderForCombinedRecording";
        case InjectMode::LoaderForEmulator         : return L"LoaderForEmulator";
        case InjectMode::EmulatorForRecording      : return L"EmulatorForRecording";
        case InjectMode::EmulatorOnly              : return L"EmulatorOnly";

        default:
            return L"<unknown InjectMode>";
    }
}

constexpr size_t MaxInjectModeNameLength = sizeof(L"LoaderForCombinedRecording") / sizeof(wchar_t) - 1;

enum class FindValidDataLineMode : uint32_t
{
    IncludePreviouslyReturnedDataLines,
    ExcludePreviouslyReturnedDataLines
};

// Indicates the CPU support required to replay a trace.
// The recorder emulator uses this to decide when to record fallbacks into the trace file.
enum ReplayCpuSupport : uint8_t
{
    // Default CPU support.
    // This just requires basic commonly-available support in the replay CPU.
    Default               = 0x00,

    // This requires no special support in the replay CPU.
    // Adequate for traces that will be replayed on a completely different CPU architecture,
    // like an Intel trace on ARM64.
    MostConservative      = 0x01,

    // This allows recording assuming that the replay CPU will be similar and of equal or greater capability.
    MostAggressive        = 0x02,

    // This allows recording assuming that the replay CPU supports AVX2
    IntelAvx2Required     = 0x03,

    // This allows recording assuming that the replay CPU supports AVX
    IntelAvxRequired      = 0x04,

    Min                   = 0,
    Max                   = 0x04,
};

constexpr char const* GetReplayCpuSupportName(ReplayCpuSupport const support)
{
    switch (support)
    {
        case ReplayCpuSupport::Default          : return "Default";
        case ReplayCpuSupport::MostConservative : return "MostConservative";
        case ReplayCpuSupport::MostAggressive   : return "MostAggressive";
        case ReplayCpuSupport::IntelAvx2Required: return "IntelAvx2Required";
        case ReplayCpuSupport::IntelAvxRequired : return "IntelAvxRequired";
        default                                 : return "<unknown ReplayCpuSupport>";
    }
}

enum class ErrorCheckingLevel : uint8_t
{
    Off      = 0,
    Minimum  = 1,
    Debug    = 2,
    Paranoid = 3
};

#ifdef DBG
constexpr ErrorCheckingLevel DefaultErrorCheckingLevel = ErrorCheckingLevel::Minimum;
#else
constexpr ErrorCheckingLevel DefaultErrorCheckingLevel = ErrorCheckingLevel::Off;
#endif

inline uint64_t GetErrorCheckingLevelInterval(ErrorCheckingLevel level)
{
    switch (level)
    {
    case ErrorCheckingLevel::Minimum  : return                    1000u;
    case ErrorCheckingLevel::Debug    : return                      10u;
    case ErrorCheckingLevel::Paranoid : return                       1u;
    default                           : return 0x7FFF'FFFF'FFFF'FFFFllu;
    }
}

inline char const* GetErrorCheckingLevelName(ErrorCheckingLevel level)
{
    switch (level)
    {
    case ErrorCheckingLevel::Off     : return "Off";
    case ErrorCheckingLevel::Minimum : return "Minimum";
    case ErrorCheckingLevel::Debug   : return "Debug";
    case ErrorCheckingLevel::Paranoid: return "Paranoid";
    default                          : return "Unknown";
    }
}

enum class SequenceId : uint64_t
{
    // Sequence IDs are ordered, so provide explicit valid min/max.
    Min     = 0,
    Max     = UINT64_MAX - 1,

    Invalid = UINT64_MAX,
};

// Offset functions.
__forceinline SequenceId  operator+ (        SequenceId  addr, int64_t value) { return static_cast<SequenceId>(static_cast<uint64_t>(addr) + value); }
__forceinline SequenceId  operator- (        SequenceId  addr, int64_t value) { return static_cast<SequenceId>(static_cast<uint64_t>(addr) - value); }
__forceinline SequenceId& operator+=(_Inout_ SequenceId& addr, int64_t value) { return addr = static_cast<SequenceId>(static_cast<uint64_t>(addr) + value); }
__forceinline SequenceId& operator-=(_Inout_ SequenceId& addr, int64_t value) { return addr = static_cast<SequenceId>(static_cast<uint64_t>(addr) - value); }

// Signed distance function.
__forceinline int64_t operator-(SequenceId addr1, SequenceId addr2) { return static_cast<int64_t>(addr1) - static_cast<int64_t>(addr2); }

// A convenient way to reference a particular record client from a list of clients participating in a recording.
// Each client is identified in a reliable manner only by a GUID, this ID is volatile and shouldn't be serialized.
enum class RecordClientId : uint32_t
{
    Min     = 0,
    Max     = UINT32_MAX - 1,
    Invalid = UINT32_MAX,
};

template < bool TIsConst >
struct TBufferView
{
    using PtrType = typename std::conditional<TIsConst, void const*, void*>::type;
    template < typename T > using PointerToT = typename std::conditional<TIsConst, T const*, T*>::type;

    PtrType BaseAddress;
    size_t  Size;

    __forceinline TBufferView() : BaseAddress(nullptr), Size(0) {}

    __forceinline TBufferView(
        _Notnull_ PtrType baseAddress,
                  size_t  size
    )
        : BaseAddress(baseAddress)
        , Size       (size)
    {
        DBG_ASSERT(IsValid());
    }

    __forceinline TBufferView(
        _Pre_ptrdiff_cap_(endAddress) _Notnull_ PtrType     baseAddress,
                                      _Notnull_ void const* endAddress
    )
        : BaseAddress(baseAddress)
        , Size       (PointerDiffInBytes(endAddress, baseAddress))
    {
        DBG_ASSERT(endAddress >= baseAddress);
        DBG_ASSERT(IsValid());
    }

    __forceinline bool IsValid() const { return (BaseAddress != nullptr && Size > 0) || (BaseAddress == nullptr && Size == 0); }
    __forceinline bool IsNull () const { return BaseAddress == nullptr; }

    __forceinline void Reset() { BaseAddress = nullptr; Size = 0; }

    template < typename T >
    PointerToT<T>
    __fastcall
    AsPointerTo() const
    {
        DBG_ASSERT(IsValid());
        return static_cast<PointerToT<T>>(BaseAddress);
    }

    template < typename T >
    PointerToT<T>
    __fastcall
    EndAsPointerTo() const
    {
        DBG_ASSERT(IsValid());
        return static_cast<PointerToT<T>>(GetEndAddress());
    }

    friend __forceinline bool operator==(TBufferView const& b1, TBufferView const& b2) { DBG_ASSERT(b1.IsValid()); DBG_ASSERT(b2.IsValid()); return b1.BaseAddress == b2.BaseAddress && b1.Size == b2.Size; }
    friend __forceinline bool operator< (TBufferView const& b1, TBufferView const& b2) { DBG_ASSERT(b1.IsValid()); DBG_ASSERT(b2.IsValid()); return b1.BaseAddress <  b2.BaseAddress || (b1.BaseAddress == b2.BaseAddress && b1.Size <  b2.Size); }
    friend __forceinline bool operator<=(TBufferView const& b1, TBufferView const& b2) { DBG_ASSERT(b1.IsValid()); DBG_ASSERT(b2.IsValid()); return b1.BaseAddress <  b2.BaseAddress || (b1.BaseAddress == b2.BaseAddress && b1.Size <= b2.Size); }

    // Operators defined in terms of the ones above.
    friend __forceinline bool operator!=(TBufferView const& b1, TBufferView const& b2) { DBG_ASSERT(b1.IsValid()); DBG_ASSERT(b2.IsValid()); return !operator==(b1, b2); }
    friend __forceinline bool operator> (TBufferView const& b1, TBufferView const& b2) { DBG_ASSERT(b1.IsValid()); DBG_ASSERT(b2.IsValid()); return operator< (b2, b1); }
    friend __forceinline bool operator>=(TBufferView const& b1, TBufferView const& b2) { DBG_ASSERT(b1.IsValid()); DBG_ASSERT(b2.IsValid()); return operator<=(b2, b1); }

    // Shrinking the buffer by adding to the base address.
    __forceinline TBufferView& operator+=(size_t const incrementInBytes)
    {
        DBG_ASSERT(IsValid());
        DBG_ASSERT(incrementInBytes <= Size);
        BaseAddress = AsPointerTo<uint8_t>() + incrementInBytes;
        Size       -= incrementInBytes;
        return *this;
    }

    friend __forceinline TBufferView operator+(TBufferView const& buffer, size_t const incrementInBytes)
    {
        auto result = buffer;
        result += incrementInBytes;
        return result;
    }

    __forceinline PtrType GetEndAddress() const { DBG_ASSERT(IsValid()); return reinterpret_cast<PtrType>(reinterpret_cast<uintptr_t>(BaseAddress) + Size); }

    // This operator just implements regular decay-to-const,
    // same as is automatically done for pointers, but for these BufferView structures.
    // It allows you to pass a BufferView to a function that receives a ConstBufferView.
    __fastcall operator TBufferView<true>() const
    {
        return TBufferView<true>(BaseAddress, Size);
    }
};

using ConstBufferView = TBufferView<true>;
using BufferView      = TBufferView<false>;

enum class ThreadId : uint32_t
{
    Invalid = 0,
    Min     = 1,
    Max     = uint32_t(-1),
};

struct TimingInfo
{
    // GetSystemTime() function timing information.
    uint64_t               SystemTime;
    // GetProcessTimes() function timing information.
    uint64_t               ProcessCreateTime;
    uint64_t               ProcessUserTime;
    uint64_t               ProcessKernelTime;
    // Currently undefined.
    uint64_t               SystemUpTime;
};

struct SystemInfo
{
    uint32_t            MajorVersion;   // Log major version
    uint32_t            MinorVersion;   // Log minor version
    uint32_t            BuildNumber;    // Log build version
    uint32_t            ProcessId;      // System Process Id

    TimingInfo Time;

    struct
    {
        // ProcessorArchitecture, ProcessorLevel and ProcessorRevision are all
        // taken from the SYSTEM_INFO structure obtained by GetSystemInfo( ).
        uint16_t ProcessorArchitecture;
        uint16_t ProcessorLevel;
        uint16_t ProcessorRevision;
        uint8_t  NumberOfProcessors;
        uint8_t  ProductType;

        //
        // MajorVersion, MinorVersion, BuildNumber, PlatformId and
        // CSDVersion are all taken from the OSVERSIONINFO structure
        // returned by GetVersionEx( ).
        //

        uint32_t MajorVersion;
        uint32_t MinorVersion;
        uint32_t BuildNumber;
        uint32_t PlatformId;

        //
        // RVA to a CSDVersion string in the string table.
        //

        uint32_t CSDVersionRva;
        uint16_t SuiteMask;
        uint16_t Reserved2;

        union
        {
            // X86 platforms use CPUID function to obtain processor information.
            struct
            {
                // CPUID Subfunction 0, register EAX (VendorId [0]),
                // EBX (VendorId [1]) and ECX (VendorId [2]).
                uint32_t VendorId [ 3 ];
                // CPUID Subfunction 1, register EAX
                uint32_t VersionInformation;
                // CPUID Subfunction 1, register EDX
                uint32_t FeatureInformation;

                // CPUID, Subfunction 80000001, register EBX. This will only
                // be obtained if the vendor id is "AuthenticAMD".
                uint32_t AMDExtendedCpuFeatures;
            } X86CpuInfo;

            // Non-x86 platforms use processor feature flags.
            struct
            {
                uint64_t ProcessorFeatures[2];
            } OtherCpuInfo;
        } Cpu;
    } System;

    // Name of person that ran the guest process.
    char16_t UserName  [64];
    char16_t SystemName[64];
};

struct ExceptionRecord64 {
    uint32_t ExceptionCode;
    uint32_t ExceptionFlags;
    uint64_t ExceptionRecord;
    uint64_t ExceptionAddress;
    uint32_t NumberParameters;
    uint32_t Padding0;
    uint64_t ExceptionInformation[15];
};

struct EtwEventDescriptor
{
    uint16_t Id;
    uint8_t  Version;
    uint8_t  Channel;
    uint8_t  Level;
    uint8_t  Opcode;
    uint16_t Task;
    uint64_t Keyword;
};

}
// namespace TTD

// Older versions of this file defined these types in the global namespace.
// Such use is still available but it's now deprecated.
// These aliases will be removed in a future version.
using simd64_t  [[deprecated]] = TTD::simd64_t;
using simd128_t [[deprecated]] = TTD::simd128_t;
using simd256_t [[deprecated]] = TTD::simd256_t;
using simd512_t [[deprecated]] = TTD::simd512_t;

#pragma pop_macro("DBG_ASSERT_MSG")
#pragma pop_macro("DBG_ASSERT")
