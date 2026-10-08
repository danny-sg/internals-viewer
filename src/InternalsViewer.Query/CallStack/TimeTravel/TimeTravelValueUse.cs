namespace InternalsViewer.Query.CallStack.TimeTravel;

public sealed record TimeTravelValueUse(ulong Address,
                                        ulong Instance,
                                        string Location,
                                        int Calls,
                                        TimeTravelValueMatch First,
                                        TimeTravelValueMatch Last);
