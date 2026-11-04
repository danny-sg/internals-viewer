namespace InternalsViewer.Query.FullTrace.Stubs.Enums;

internal enum StubOperationType : byte
{
    /// <summary>
    /// MOV r64, [mem]
    /// </summary>
    Load,

    /// <summary>
    /// LEA r64, [base+disp] / ADD r64, imm / SUB r64, imm
    /// </summary>
    Add,

    /// <summary>
    /// MOV r64, r64
    /// </summary>
    Copy,

    /// <summary>
    /// JMP qword ptr [mem]
    /// </summary>
    JumpMemory,

    /// <summary>
    /// JMP r64
    /// </summary>
    JumpRegister,

    /// <summary>
    /// JMP rel8 / JMP rel32
    /// </summary>
    JumpDirect
}
