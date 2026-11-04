using Iced.Intel;

namespace InternalsViewer.Query.FullTrace.InstructionHistory.Listing.Helpers;

internal static class RegisterHelper
{
    public const int ArgumentRegisterCount = 4;

    public static int GetArgumentSlot(Register register) => register.GetFullRegister() switch
    {
        Register.RCX => 0,
        Register.RDX => 1,
        Register.R8 => 2,
        Register.R9 => 3,
        _ => -1
    };

    public static bool IsRead(OpAccess access)
        => access is OpAccess.Read or OpAccess.CondRead or OpAccess.ReadWrite or OpAccess.ReadCondWrite;

    public static bool IsWrite(OpAccess access)
        => access is OpAccess.Write or OpAccess.CondWrite or OpAccess.ReadWrite or OpAccess.ReadCondWrite;

    public static bool IsSameRegister(in Instruction instruction, int first, int second)
        => instruction.OpCount > second
           && instruction.GetOpKind(first) == OpKind.Register
           && instruction.GetOpKind(second) == OpKind.Register
           && instruction.GetOpRegister(first) == instruction.GetOpRegister(second);

    public static bool IsZeroingIdiom(in Instruction instruction) => instruction.Mnemonic switch
    {
        Mnemonic.Xor or Mnemonic.Sub or Mnemonic.Pxor or Mnemonic.Xorps or Mnemonic.Xorpd => IsSameRegister(instruction, 0, 1),
        Mnemonic.Vpxor or Mnemonic.Vxorps or Mnemonic.Vxorpd => IsSameRegister(instruction, 1, 2),
        _ => false
    };
}
