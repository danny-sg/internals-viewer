namespace InternalsViewer.Query.Plans;

/// <summary>
/// Builds Operator Events from the Execution Plan and Engine Events
/// </summary>
/// <remarks>
/// Operator Events show the operators on the timeline. They have to be sourced from a combination of the Execution
/// Plan and the events, specifically I/O events.
///
/// The Execution Plan has detail about the operators, but not their start/end times. The Engine Events have start/end
/// times, but the resolution of the times are ms rather an us that the timeline works in.
///
/// Timings are built bottom up.
///
/// Logic is:
///
/// 1. I/O
/// 
/// I/O events determine the start and end of an I/O operator. I/O events are linked to operators via Plan Handle +
/// Node Id. Start Time is the first access of a table/index. End Time should be Start Time + Duration of the operator,
/// but also checked against the last I/O access.
///
/// 2. Blocking/Streaming Operators
///
/// Operators are either blocking, or streaming/non-blocking.
///
/// Blocking  - Input must be consumed before output is produced
/// Streaming - Output is produced immediately
///
/// Some operators have two phases, a blocking phase and a streaming phase, for example Hash Joins will block during
/// the build phase, and stream during the probe phase.1.
/// </remarks>
internal class OperatorEventBuilder
{

}
