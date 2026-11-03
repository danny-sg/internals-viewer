namespace InternalsViewer.Query.Helpers;

public static class CountText
{
    public static string GetCalls(long count) => GetText(count, "Call", "Calls");

    public static string GetText(long count, string singular, string plural) => $"{count:N0} {(count == 1 ? singular : plural)}";
}
