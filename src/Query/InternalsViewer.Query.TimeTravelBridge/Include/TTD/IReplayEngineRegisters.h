// Copyright (c) Microsoft Corporation.
#pragma once

#include <cstdint>

// Defining INCLUDE_IREPLAYENGINE_REGISTERS allows the contents of this file to be put under a namespace,
// so it can be validated against the debugger definitions.
#if !defined(INCLUDE_IREPLAYENGINE_REGISTERS)

#include "IReplayEngine.h"

#endif // !defined(INCLUDE_IREPLAYENGINE_REGISTERS)


// These definitions come from ntdbg.h. To make it easier to include this header in mixed environments,
// only provide the defintions if ntdbg.h has *not* been included. To force the use of the types below:
//     #define INCLUDE_IREPLAYENGINE_REGISTERS.
#if defined(INCLUDE_IREPLAYENGINE_REGISTERS) || !defined(_NTDBG_)

#pragma warning(push)
#pragma warning(disable: 4201) // nonstandard extension used: nameless struct/union

struct M128BIT {
    uint64_t Low;
    int64_t  High;
};

struct M256BIT {
    M128BIT Low;
    M128BIT High;
};

struct M512BIT {
    M256BIT Low;
    M256BIT High;
};


#define MAXIMUM_SUPPORTED_EXTENSION     512
#define X86_CONTEXT_ALIGN               4

//
// Define the size of FP registers in the FXSAVE format
//
#define X86_SIZE_OF_FX_REGISTERS        128

struct X86_FXSAVE_FORMAT {
    uint16_t  ControlWord;
    uint16_t  StatusWord;
    uint16_t  TagWord;
    uint16_t  ErrorOpcode;
    uint32_t  ErrorOffset;
    uint32_t  ErrorSelector;
    uint32_t  DataOffset;
    uint32_t  DataSelector;
    uint32_t  MXCsr;
    uint32_t  Reserved2;
    uint8_t   RegisterArea[X86_SIZE_OF_FX_REGISTERS];
    uint8_t   Reserved3[X86_SIZE_OF_FX_REGISTERS];
    uint8_t   Reserved4[224];
};

//  Define the size of the 80387 save area, which is in the context frame.
#define X86_SIZE_OF_80387_REGISTERS      80
struct X86_FLOATING_SAVE_AREA {
    uint32_t   ControlWord;
    uint32_t   StatusWord;
    uint32_t   TagWord;
    uint32_t   ErrorOffset;
    uint32_t   ErrorSelector;
    uint32_t   DataOffset;
    uint32_t   DataSelector;
    uint8_t    RegisterArea[X86_SIZE_OF_80387_REGISTERS];
    uint32_t   Cr0NpxState;
};

#define VDMCONTEXT_i386    0x00010000

#define VDMCONTEXT_CONTROL         (VDMCONTEXT_i386 | 0x00000001L) // SS:SP, CS:IP, FLAGS, BP
#define VDMCONTEXT_INTEGER         (VDMCONTEXT_i386 | 0x00000002L) // AX, BX, CX, DX, SI, DI
#define VDMCONTEXT_SEGMENTS        (VDMCONTEXT_i386 | 0x00000004L) // DS, ES, FS, GS
#define VDMCONTEXT_FLOATING_POINT  (VDMCONTEXT_i386 | 0x00000008L) // 387 state
#define VDMCONTEXT_DEBUG_REGISTERS (VDMCONTEXT_i386 | 0x00000010L) // DB 0-3,6,7
#define VDMCONTEXT_EXTENDED_REGISTERS  (VDMCONTEXT_i386 | 0x00000020L) // cpu specific extensions

struct X86_NT5_CONTEXT {

    uint32_t               ContextFlags;
    uint32_t               Dr0;
    uint32_t               Dr1;
    uint32_t               Dr2;
    uint32_t               Dr3;
    uint32_t               Dr6;
    uint32_t               Dr7;
    X86_FLOATING_SAVE_AREA FloatSave;
    uint32_t               SegGs;
    uint32_t               SegFs;
    uint32_t               SegEs;
    uint32_t               SegDs;
    uint32_t               Edi;
    uint32_t               Esi;
    uint32_t               Ebx;
    uint32_t               Edx;
    uint32_t               Ecx;
    uint32_t               Eax;
    uint32_t               Ebp;
    uint32_t               Eip;
    uint32_t               SegCs;              // MUST BE SANITIZED
    uint32_t               EFlags;             // MUST BE SANITIZED
    uint32_t               Esp;
    uint32_t               SegSs;
    union {
        uint8_t   ExtendedRegisters[MAXIMUM_SUPPORTED_EXTENSION];
        X86_FXSAVE_FORMAT FxSave;
    };

};


typedef M128BIT  M128BIT;


struct AMD64_XMM_SAVE_AREA32 {
    uint16_t ControlWord;
    uint16_t StatusWord;
    uint8_t  TagWord;
    uint8_t  Reserved1;
    uint16_t ErrorOpcode;
    uint32_t ErrorOffset;
    uint16_t ErrorSelector;
    uint16_t Reserved2;
    uint32_t DataOffset;
    uint16_t DataSelector;
    uint16_t Reserved3;
    uint32_t MxCsr;
    uint32_t MxCsr_Mask;
    M128BIT  FloatRegisters[8];
    M128BIT  XmmRegisters[16];
    uint8_t  Reserved4[96];
};

#define AMD64_CONTEXT_AMD64             0x00100000L
#define AMD64_CONTEXT_CONTROL           (AMD64_CONTEXT_AMD64 | 0x00000001L)
#define AMD64_CONTEXT_INTEGER           (AMD64_CONTEXT_AMD64 | 0x00000002L)
#define AMD64_CONTEXT_SEGMENTS          (AMD64_CONTEXT_AMD64 | 0x00000004L)
#define AMD64_CONTEXT_FLOATING_POINT    (AMD64_CONTEXT_AMD64 | 0x00000008L)
#define AMD64_CONTEXT_DEBUG_REGISTERS   (AMD64_CONTEXT_AMD64 | 0x00000010L)

struct AMD64_CONTEXT {

    //
    // Register parameter home addresses.
    //

    uint64_t P1Home;
    uint64_t P2Home;
    uint64_t P3Home;
    uint64_t P4Home;
    uint64_t P5Home;
    uint64_t P6Home;

    //
    // Control flags.
    //

    uint32_t ContextFlags;
    uint32_t MxCsr;

    //
    // Segment Registers and processor flags.
    //

    uint16_t SegCs;
    uint16_t SegDs;
    uint16_t SegEs;
    uint16_t SegFs;
    uint16_t SegGs;
    uint16_t SegSs;
    uint32_t EFlags;

    //
    // Debug registers
    //

    uint64_t Dr0;
    uint64_t Dr1;
    uint64_t Dr2;
    uint64_t Dr3;
    uint64_t Dr6;
    uint64_t Dr7;

    //
    // Integer registers.
    //

    uint64_t Rax;
    uint64_t Rcx;
    uint64_t Rdx;
    uint64_t Rbx;
    uint64_t Rsp;
    uint64_t Rbp;
    uint64_t Rsi;
    uint64_t Rdi;
    uint64_t R8;
    uint64_t R9;
    uint64_t R10;
    uint64_t R11;
    uint64_t R12;
    uint64_t R13;
    uint64_t R14;
    uint64_t R15;

    //
    // Program counter.
    //

    uint64_t Rip;

    //
    // Floating point state.
    //

    union {
        AMD64_XMM_SAVE_AREA32 FltSave;
        struct {
            M128BIT Header[2];
            M128BIT Legacy[8];
            M128BIT Xmm0;
            M128BIT Xmm1;
            M128BIT Xmm2;
            M128BIT Xmm3;
            M128BIT Xmm4;
            M128BIT Xmm5;
            M128BIT Xmm6;
            M128BIT Xmm7;
            M128BIT Xmm8;
            M128BIT Xmm9;
            M128BIT Xmm10;
            M128BIT Xmm11;
            M128BIT Xmm12;
            M128BIT Xmm13;
            M128BIT Xmm14;
            M128BIT Xmm15;
        };
    };

    //
    // Vector registers.
    //

    M128BIT VectorRegister[26];
    uint64_t VectorControl;

    //
    // Special debug control registers.
    //

    uint64_t DebugControl;
    uint64_t LastBranchToRip;
    uint64_t LastBranchFromRip;
    uint64_t LastExceptionToRip;
    uint64_t LastExceptionFromRip;
};

#define AMD64_CONTEXT_ALIGN     16


#define ARM_MAX_BREAKPOINTS     8
#define ARM_MAX_WATCHPOINTS     1

#define ARM_CONTEXT_ARM                 0x00200000L
#define ARM_CONTEXT_CONTROL             (ARM_CONTEXT_ARM | 0x00000001L)
#define ARM_CONTEXT_INTEGER             (ARM_CONTEXT_ARM | 0x00000002L)
#define ARM_CONTEXT_FLOATING_POINT      (ARM_CONTEXT_ARM | 0x00000004L)
#define ARM_CONTEXT_DEBUG_REGISTERS     (ARM_CONTEXT_ARM | 0x00000008L)

struct ARM_CONTEXT {
    //
    // The flags values within this flag control the contents of
    // a CONTEXT record.
    //
    // If the context record is used as an input parameter, then
    // for each portion of the context record controlled by a flag
    // whose value is set, it is assumed that that portion of the
    // context record contains valid context. If the context record
    // is being used to modify a thread's context, then only that
    // portion of the threads context will be modified.
    //
    // If the context record is used as an IN OUT parameter to capture
    // the context of a thread, then only those portions of the thread's
    // context corresponding to set flags will be returned.
    //
    // The context record is never used as an OUT only parameter.
    //

    uint32_t ContextFlags;

    //
    // Debug registers
    //

    //
    // This section is specified/returned if the ContextFlags word contains
    // the flag CONTEXT_INTEGER.
    //
    uint32_t R0;
    uint32_t R1;
    uint32_t R2;
    uint32_t R3;
    uint32_t R4;
    uint32_t R5;
    uint32_t R6;
    uint32_t R7;
    uint32_t R8;
    uint32_t R9;
    uint32_t R10;
    uint32_t R11;
    uint32_t R12;

    //
    // This section is specified/returned if the ContextFlags word contains
    // the flag CONTEXT_CONTROL.
    //
    uint32_t Sp;
    uint32_t Lr;
    uint32_t Pc;
    uint32_t Cpsr;

    //
    // This section is specified/returned if the ContextFlags word contains
    // the flag CONTEXT_FLOATING_POINT.
    //
    uint32_t Fpscr;
    uint32_t Padding;
    union {
        M128BIT Q[16];
        uint64_t D[32];
        uint32_t S[32];
    } DUMMYUNIONNAME;

    //
    // This section is specified/returned if the ContextFlags word contains
    // the flag CONTEXT_DEBUG_REGISTERS.
    //
    uint32_t Bvr[ARM_MAX_BREAKPOINTS];
    uint32_t Bcr[ARM_MAX_BREAKPOINTS];
    uint32_t Wvr[ARM_MAX_WATCHPOINTS];
    uint32_t Wcr[ARM_MAX_WATCHPOINTS];

    uint32_t Padding2[2];

};

#define ARM_CONTEXT_ALIGN   8

#define ARM64_MAX_BREAKPOINTS     8
#define ARM64_MAX_WATCHPOINTS     2

#define ARM64_CONTEXT_ARM64   0x00400000L
#define ARM64_CONTEXT_CONTROL (ARM64_CONTEXT_ARM64 | 0x1L)
#define ARM64_CONTEXT_INTEGER (ARM64_CONTEXT_ARM64 | 0x2L)
#define ARM64_CONTEXT_FLOATING_POINT  (ARM64_CONTEXT_ARM64 | 0x4L)
#define ARM64_CONTEXT_DEBUG_REGISTERS (ARM64_CONTEXT_ARM64 | 0x8L)

typedef union _ARM64_NEON128 {
    struct {
        uint64_t Low;
        int64_t High;
    } DUMMYSTRUCTNAME;
    double D[2];
    float S[4];
    uint16_t H[8];
    uint8_t B[16];
} ARM64_NEON128, *PARM64_NEON128;

typedef struct _ARM64_CONTEXT {

    //
    // Control flags.
    //

    /* +0x000 */ uint32_t ContextFlags;

    //
    // Integer registers
    //

    /* +0x004 */ uint32_t Cpsr;       // NZVF + DAIF + CurrentEL + SPSel
    /* +0x008 */ uint64_t X[29];
    /* +0x0f0 */ uint64_t Fp;
    /* +0x0f8 */ uint64_t Lr;
    /* +0x100 */ uint64_t Sp;
    /* +0x108 */ uint64_t Pc;

    //
    // Floating Point/NEON Registers
    //

    /* +0x110 */ ARM64_NEON128 V[32];
    /* +0x310 */ uint32_t Fpsr;
    /* +0x314 */ uint32_t Fpcr;

    //
    // Debug registers
    //

    /* +0x318 */ uint32_t Bcr[ARM64_MAX_BREAKPOINTS];
    /* +0x338 */ uint64_t Bvr[ARM64_MAX_BREAKPOINTS];
    /* +0x378 */ uint32_t Wcr[ARM64_MAX_WATCHPOINTS];
    /* +0x380 */ uint64_t Wvr[ARM64_MAX_WATCHPOINTS];
    /* +0x390 */

} ARM64_CONTEXT, *PARM64_CONTEXT;

#define ARM64_CONTEXT_ALIGN  16




struct VECTOR_128BIT_REGISTERS {
    M128BIT Ymm0;
    M128BIT Ymm1;
    M128BIT Ymm2;
    M128BIT Ymm3;
    M128BIT Ymm4;
    M128BIT Ymm5;
    M128BIT Ymm6;
    M128BIT Ymm7;
    M128BIT Ymm8;
    M128BIT Ymm9;
    M128BIT Ymm10;
    M128BIT Ymm11;
    M128BIT Ymm12;
    M128BIT Ymm13;
    M128BIT Ymm14;
    M128BIT Ymm15;
};

//
//	Number of AVX512 registers on x86 bits system
//

#define	NUMBER_AVX512_REGISTERS_X86	8

//
//	Number of AVX512 registers on AMD64 bits system
//

#define	NUMBER_AVX512_REGISTERS_AMD64	32

typedef struct _VECTOR_256BIT_REGISTERS {
    M256BIT Zmm0;
    M256BIT Zmm1;
    M256BIT Zmm2;
    M256BIT Zmm3;
    M256BIT Zmm4;
    M256BIT Zmm5;
    M256BIT Zmm6;
    M256BIT Zmm7;
    M256BIT Zmm8;
    M256BIT Zmm9;
    M256BIT Zmm10;
    M256BIT Zmm11;
    M256BIT Zmm12;
    M256BIT Zmm13;
    M256BIT Zmm14;
    M256BIT Zmm15;
} VECTOR_256BIT_REGISTERS, *PVECTOR_256BIT_REGISTERS;

//
//  This structure VECTOR_512BIT_REGISTERS cover the full 512-bit of the 16 registers ZMM16-ZMM31
//  The ZMM16-ZMM31 registers values are populated from the AVX Hi16_ZMM state buffer.
//

typedef struct _VECTOR_512BIT_REGISTERS {
    M512BIT Zmm16;
    M512BIT Zmm17;
    M512BIT Zmm18;
    M512BIT Zmm19;
    M512BIT Zmm20;
    M512BIT Zmm21;
    M512BIT Zmm22;
    M512BIT Zmm23;
    M512BIT Zmm24;
    M512BIT Zmm25;
    M512BIT Zmm26;
    M512BIT Zmm27;
    M512BIT Zmm28;
    M512BIT Zmm29;
    M512BIT Zmm30;
    M512BIT Zmm31;
} VECTOR_512BIT_REGISTERS, *PVECTOR_512BIT_REGISTERS;

//
//  AVX YMM registers
//

struct AVX_YMM_REGISTERS {
    VECTOR_128BIT_REGISTERS LowPart;
    VECTOR_128BIT_REGISTERS HighPart;
};

//
//  AVX ZMM registers
//

typedef struct _AVX_ZMM_256_REGISTERS {
    AVX_YMM_REGISTERS LowPart;
    VECTOR_256BIT_REGISTERS HighPart;
} AVX_ZMM_256_REGISTERS, *PAVX_ZMM_256_REGISTERS;

typedef struct _AVX_ZMM_REGISTERS {
    AVX_ZMM_256_REGISTERS LowPart;
    VECTOR_512BIT_REGISTERS HighPart;
} AVX_ZMM_REGISTERS, *PAVX_ZMM_REGISTERS;

typedef struct _OPMASK_REGISTERS {
    uint64_t k0;
    uint64_t k1;
    uint64_t k2;
    uint64_t k3;
    uint64_t k4;
    uint64_t k5;
    uint64_t k6;
    uint64_t k7;
} OPMASK_REGISTERS, *POPMASK_REGISTERS;

typedef struct _AVX_512_K_REGISTERS {
    OPMASK_REGISTERS OpMask;
} AVX_512_K_REGISTERS, *PAVX_512_K_REGISTERS;

constexpr size_t c_avxExtraReserved = (16 * 32) + (32 * 64) + (32 * 128) + (8 * 8);

 typedef struct _CROSS_PLATFORM_CONTEXT {
    union {
        X86_NT5_CONTEXT   X86Nt5Context;
        AMD64_CONTEXT     Amd64Context;
        ARM_CONTEXT       ArmContext;
        ARM64_CONTEXT     Arm64Context;
        uint8_t           ContextPadding[2672];
    };
} CROSS_PLATFORM_CONTEXT;


typedef struct _AVX_EXTENDED_CONTEXT {
    union {
        AVX_YMM_REGISTERS YmmRegisters;
        AVX_ZMM_REGISTERS ZmmRegisters;
    };
    AVX_512_K_REGISTERS Avx512Registers;
    uint8_t ReservedAVXEx[c_avxExtraReserved];

} AVX_EXTENDED_CONTEXT, *PAVX_EXTENDED_CONTEXT;

#pragma warning(pop)

#endif  // defined(INCLUDE_IREPLAYENGINE_REGISTERS) || !defined(_NTDBG_)

#if !defined(INCLUDE_IREPLAYENGINE_REGISTERS)

static_assert(sizeof (TTD::Replay::RegisterContext) <= sizeof (CROSS_PLATFORM_CONTEXT), "TTD::Replay::RegisterContext must be smaller than CROSS_PLATFORM_CONTEXT");
static_assert(alignof(TTD::Replay::RegisterContext) <= alignof(CROSS_PLATFORM_CONTEXT), "TTD::Replay::RegisterContext must align less strictly than CROSS_PLATFORM_CONTEXT");

static_assert(sizeof (TTD::Replay::ExtendedRegisterContext) <= sizeof (AVX_EXTENDED_CONTEXT), "TTD::Replay::ExtendedRegisterContext must be smaller than AVX_EXTENDED_CONTEXT");
static_assert(alignof(TTD::Replay::ExtendedRegisterContext) <= alignof(AVX_EXTENDED_CONTEXT), "TTD::Replay::ExtendedRegisterContext must align less strictly than AVX_EXTENDED_CONTEXT");

inline TTD::Replay::RegisterContext::operator CROSS_PLATFORM_CONTEXT() const noexcept
{
    CROSS_PLATFORM_CONTEXT result{};
    *reinterpret_cast<RegisterContext*>(&result) = *this;
    return result;
}

inline TTD::Replay::ExtendedRegisterContext::operator AVX_EXTENDED_CONTEXT() const noexcept
{
    AVX_EXTENDED_CONTEXT result{};
    *reinterpret_cast<ExtendedRegisterContext*>(&result) = *this;
    return result;
}

#endif // !defined(INCLUDE_IREPLAYENGINE_REGISTERS)
