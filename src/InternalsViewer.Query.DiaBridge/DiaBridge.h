#pragma once

// Plain C ABI so this can be P/Invoked from C#: opaque void* handles instead of
// C++ classes/exceptions, since neither survives crossing the managed/native boundary.
extern "C"
{
    __declspec(dllexport) void *OpenPdb(const wchar_t *pdbPath);

    __declspec(dllexport) bool ResolveRva(void *session, unsigned int rva, wchar_t *buffer, int bufferLength);

    // Enumerates the PDB's public symbols whose (demangled) name starts with prefix. Begin returns an opaque
    // enumerator; call NextSymbol repeatedly until it returns false, then End to release it.
    __declspec(dllexport) void *BeginEnumSymbols(void *session, const wchar_t *prefix);

    __declspec(dllexport) bool NextSymbol(void *enumerator, wchar_t *buffer, int bufferLength);

    __declspec(dllexport) void EndEnumSymbols(void *enumerator);

    __declspec(dllexport) void ClosePdb(void *session);
}