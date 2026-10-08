// Copyright (c) Microsoft Corporation.
#pragma once

#include <stdint.h>
#include <stddef.h>

#include <type_traits>

#include <sal.h>

template < typename TResult, typename TArgument >
__forceinline
TResult __fastcall SafeNumericCastOrZero(TArgument argument)
{
    auto const result = static_cast<TResult>(argument);
    if (argument != static_cast<TArgument>(result))
    {
        return static_cast<TResult>(0);
    }
    else
    {
        return result;
    }
}

template < typename TArgument, typename TLimit >
__forceinline
TArgument __fastcall ValidateLimitOrZero(TArgument argument, TLimit limit)
{
    if (argument > limit)
    {
        return static_cast<TArgument>(0);
    }
    else
    {
        return argument;
    }
}

// Like the standard "offsetof", but this one returns the offset immediately following the given field.
// It can be used to determine whether it's safe to read the field.
#define offsetafter(type, field) (reinterpret_cast<size_t>(&static_cast<type*>(nullptr)->field + 1))

// Pointer math utilities.
__forceinline
ptrdiff_t __fastcall PointerDiffInBytes(_In_opt_ void const* ptr1, _In_opt_ void const* ptr2)
{
    return static_cast<ptrdiff_t>(reinterpret_cast<intptr_t>(ptr1) - reinterpret_cast<intptr_t>(ptr2));
}

// Note: cast of 'offset' if it is signed will may sign-extend the value. This is intended.
// Example: V is int32_t, and uintptr_t is uint64_t.
// We could implement this separately for signed and unsigned offsets,
// but 2's complement behavior ensures correctness when casting to unsigned.
template < typename T, typename V >
__forceinline
T* __fastcall PointerOffsetInBytes(_In_ T* ptr, V offset)
{
    return reinterpret_cast<T*>(reinterpret_cast<uintptr_t>(ptr) + static_cast<uintptr_t>(offset));
}

// This slightly tricky version of the above function can be called as PointerOffsetInBytes<Type>(...).
// It performs the same function as reinterpret_cast<Type*>(PointerOffsetInBytes(...)).
template < typename T, typename V >
__forceinline
T* __fastcall PointerOffsetInBytes(
    _In_ typename std::conditional<std::is_const<T>::value, void const, void>::type* ptr,
         V                                                                           offset
)
{
    return reinterpret_cast<T*>(reinterpret_cast<uintptr_t>(ptr) + static_cast<uintptr_t>(offset));
}
