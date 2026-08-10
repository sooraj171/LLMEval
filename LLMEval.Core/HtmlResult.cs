using System.Net;
using System.Text;

namespace LLMEval;

/// <summary>
/// Builds STAF-style HTML suite reports (same visual language as STAF.Playwright <c>HtmlResult</c>):
/// blue header bar, yellow Copperplate title, cyan result rows, green/red pass/fail.
/// </summary>
public static class HtmlResult
{
    /// <summary>Renders a complete <c>report.html</c> document for a suite run.</summary>
    public static string Write(SuiteRunResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var sb = new StringBuilder(capacity: 4096);

        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html>");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta http-equiv=\"Content-Language\" content=\"en-us\" />");
        sb.AppendLine("<meta charset=\"utf-8\" />");
        sb.AppendLine("<title>STAF.LLMEval</title>");
        sb.AppendLine("<style>");
        sb.AppendLine(".result:hover { background-color: #FFF8C6; font-weight: bold; }");
        sb.AppendLine(".headBk { background-color: #2962FF; }");
        sb.AppendLine("table { border-collapse: collapse; }");
        sb.AppendLine("pre { margin: 0; white-space: pre-wrap; font-family: Verdana, sans-serif; font-size: 11px; }");
        sb.AppendLine("</style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("<blockquote>");
        sb.AppendLine("<table border=\"2\" bordercolor=\"#000000\" id=\"table1\" width=\"100%\" bordercolorlight=\"#000000\">");

        // Title
        sb.AppendLine("<tr>");
        sb.AppendLine("<td COLSPAN=\"9\" class=\"headBk\">");
        sb.AppendLine("<p align=\"center\"><font color=\"yellow\" size=\"4\" face=\"Copperplate Gothic Bold\">&nbsp; Automation Script - STAF.LLMEval</font></p>");
        sb.AppendLine("</td>");
        sb.AppendLine("</tr>");

        // Start time
        sb.AppendLine("<tr>");
        sb.AppendLine("<td COLSPAN=\"9\" class=\"headBk\">");
        sb.AppendLine($"<p align=\"justify\"><b><font color=\"white\" size=\"2\" face=\"Verdana\">&nbsp;START TIME:&nbsp;&nbsp;{Encode(FormatStafTime(result.StartedAt))}&nbsp;</font></b></p>");
        sb.AppendLine("</td>");
        sb.AppendLine("</tr>");

        // Summary
        sb.AppendLine("<tr>");
        sb.AppendLine("<td COLSPAN=\"9\" class=\"headBk\">");
        var summary = $"Pass rate: {result.PassRate:P1} ({result.Passed}/{result.Total})";
        if (result.TotalUsage?.TotalTokens != null)
        {
            summary += $" | Tokens: {result.TotalUsage.TotalTokens} (prompt {result.TotalUsage.PromptTokens}, completion {result.TotalUsage.CompletionTokens})";
        }
        if (result.TotalUsage?.EstimatedCostUsd != null)
        {
            summary += $" | Est. cost: ${result.TotalUsage.EstimatedCostUsd:0.######}";
        }
        sb.AppendLine($"<p align=\"left\"><font color=\"#E0E0E0\" size=\"2\" face=\"Verdana\">&nbsp;{Encode(summary)}</font></p>");
        sb.AppendLine("</td>");
        sb.AppendLine("</tr>");

        // Column headers (Playwright mapping + eval columns)
        sb.AppendLine("<tr bgcolor=\"#448AFF\">");
        AppendHeaderCell(sb, "Module Name");      // Case Id
        AppendHeaderCell(sb, "Description");      // Question
        AppendHeaderCell(sb, "Actual Result");    // As Expected / Not As Expected
        AppendHeaderCell(sb, "Execution Status"); // PASS / FAIL
        AppendHeaderCell(sb, "Score");
        AppendHeaderCell(sb, "Metric");
        AppendHeaderCell(sb, "Expected");
        AppendHeaderCell(sb, "Actual");
        AppendHeaderCell(sb, "Details");
        sb.AppendLine("</tr>");

        foreach (var c in result.Cases)
        {
            sb.AppendLine("<tr class=\"result\" bgcolor=\"#80D8FF\">");
            AppendBodyCell(sb, c.Id);
            AppendBodyCellPre(sb, c.Question);
            AppendBodyCell(sb, c.Passed ? "As Expected" : "Not As Expected");
            AppendStatusCell(sb, c.Passed);
            AppendBodyCell(sb, c.Score.ToString("0.###"));
            AppendBodyCell(sb, c.MetricName);
            AppendBodyCellPre(sb, c.Expected);
            AppendBodyCellPre(sb, c.Actual);
            AppendBodyCellPre(sb, c.Details);
            sb.AppendLine("</tr>");
        }

        // End time
        sb.AppendLine("<tr>");
        sb.AppendLine("<td class=\"headBk\" COLSPAN=\"9\">");
        sb.AppendLine($"<p align=\"justify\"><b><font color=\"white\" size=\"2\" face=\"Verdana\">&nbsp;END TIME :&nbsp;&nbsp;{Encode(FormatStafTime(result.CompletedAt))}&nbsp;</font></b></p>");
        sb.AppendLine("</td>");
        sb.AppendLine("</tr>");

        sb.AppendLine("</table>");
        sb.AppendLine("</blockquote>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");
        return sb.ToString();
    }

    private static void AppendHeaderCell(StringBuilder sb, string label)
    {
        sb.AppendLine("<td>");
        sb.AppendLine($"<p align=\"center\"><b><font color=\"white\" face=\"Arial Narrow\" size=\"2\">{Encode(label)}</font></b></p>");
        sb.AppendLine("</td>");
    }

    private static void AppendBodyCell(StringBuilder sb, string? text)
    {
        sb.AppendLine("<td>");
        sb.AppendLine($"<p align=\"center\"><font face=\"Verdana\" size=\"2\">{Encode(text)}</font></p>");
        sb.AppendLine("</td>");
    }

    private static void AppendBodyCellPre(StringBuilder sb, string? text)
    {
        sb.AppendLine("<td>");
        sb.AppendLine($"<pre>{Encode(text)}</pre>");
        sb.AppendLine("</td>");
    }

    private static void AppendStatusCell(StringBuilder sb, bool passed)
    {
        var color = passed ? "#008000" : "#FF0000";
        var label = passed ? "PASS" : "FAIL";
        sb.AppendLine("<td>");
        sb.AppendLine($"<p align=\"center\"><b><font face=\"Verdana\" size=\"2\" color=\"{color}\">{label}</font></b></p>");
        sb.AppendLine("</td>");
    }

    /// <summary>Matches STAF.Playwright HtmlResult time formatting.</summary>
    private static string FormatStafTime(DateTimeOffset value) =>
        value.ToLocalTime().ToString("MM / dd / yyyy T hh : mm : ss");

    private static string Encode(string? value) =>
        WebUtility.HtmlEncode(value ?? string.Empty);
}
