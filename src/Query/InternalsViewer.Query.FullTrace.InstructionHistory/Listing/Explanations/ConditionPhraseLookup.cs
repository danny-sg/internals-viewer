using Iced.Intel;

namespace InternalsViewer.Query.FullTrace.InstructionHistory.Listing.Explanations;

internal static class ConditionPhraseLookup
{
    public static string GetPhrase(ConditionCode condition) => condition switch
    {
        ConditionCode.o => "if the last calculation overflowed",
        ConditionCode.no => "if the last calculation did not overflow",
        ConditionCode.b => "if the last comparison was less than, as unsigned numbers",
        ConditionCode.ae => "if the last comparison was greater than or equal, as unsigned numbers",
        ConditionCode.e => "if the last comparison was equal",
        ConditionCode.ne => "if the last comparison was not equal",
        ConditionCode.be => "if the last comparison was less than or equal, as unsigned numbers",
        ConditionCode.a => "if the last comparison was greater than, as unsigned numbers",
        ConditionCode.s => "if the last result was negative",
        ConditionCode.ns => "if the last result was zero or positive",
        ConditionCode.p => "if the parity flag is set, which after a floating-point comparison means a value was not a number",
        ConditionCode.np => "if the parity flag is clear, which after a floating-point comparison means both values were numbers",
        ConditionCode.l => "if the last comparison was less than, as signed numbers",
        ConditionCode.ge => "if the last comparison was greater than or equal, as signed numbers",
        ConditionCode.le => "if the last comparison was less than or equal, as signed numbers",
        ConditionCode.g => "if the last comparison was greater than, as signed numbers",
        _ => "if its condition holds"
    };
}
