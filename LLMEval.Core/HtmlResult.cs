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
    public static string Write(SuiteRunResult result) => Write(result, history: null);

    /// <summary>
    /// Renders <c>report.html</c>. When <paramref name="history"/> is provided, a pass-rate sparkline
    /// of recent runs is included (opt-in via <see cref="LLMEvalOptions.EnableRunHistory"/>).
    /// </summary>
    public static string Write(SuiteRunResult result, IReadOnlyList<RunHistoryEntry>? history)
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

        if (history is { Count: > 0 })
        {
            sb.AppendLine("<tr>");
            sb.AppendLine("<td COLSPAN=\"9\" class=\"headBk\">");
            sb.AppendLine($"<p align=\"left\"><font color=\"#E0E0E0\" size=\"2\" face=\"Verdana\">&nbsp;Pass-rate trend (last {history.Count} run{(history.Count == 1 ? "" : "s")})</font></p>");
            sb.AppendLine(WriteTrendSvg(history));
            sb.AppendLine("</td>");
            sb.AppendLine("</tr>");
        }

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

    private static string WriteTrendSvg(IReadOnlyList<RunHistoryEntry> history)
    {
        const int width = 640;
        const int height = 88;
        const int padL = 40;
        const int padR = 16;
        const int padT = 8;
        const int padB = 20;
        var plotW = width - padL - padR;
        var plotH = height - padT - padB;
        var n = history.Count;

        string X(int i)
        {
            if (n == 1) return (padL + plotW / 2.0).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            var v = padL + (double)i / (n - 1) * plotW;
            return v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        }

        string Y(double passRate)
        {
            var clamped = Math.Clamp(passRate, 0, 1);
            var v = padT + (1.0 - clamped) * plotH;
            return v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        }

        var points = new StringBuilder();
        for (var i = 0; i < n; i++)
        {
            if (i > 0) points.Append(' ');
            points.Append(X(i)).Append(',').Append(Y(history[i].PassRate));
        }

        var last = history[n - 1];
        var lastLabel = (last.PassRate * 100).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "%";
        var y100 = Y(1);
        var y0 = Y(0);

        return $"""
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {width} {height}" width="100%" height="{height}" role="img" aria-label="Pass-rate trend">
            <rect x="0" y="0" width="{width}" height="{height}" fill="#2962FF"/>
            <line x1="{padL}" y1="{y100}" x2="{width - padR}" y2="{y100}" stroke="#90CAF9" stroke-width="1" stroke-dasharray="4 3"/>
            <line x1="{padL}" y1="{y0}" x2="{width - padR}" y2="{y0}" stroke="#90CAF9" stroke-width="1"/>
            <text x="4" y="{y100}" fill="#E0E0E0" font-size="10" font-family="Verdana">100%</text>
            <text x="8" y="{y0}" fill="#E0E0E0" font-size="10" font-family="Verdana">0%</text>
            <polyline fill="none" stroke="#FFF59D" stroke-width="2" points="{points}"/>
            <circle cx="{X(n - 1)}" cy="{Y(last.PassRate)}" r="3.5" fill="#FFF59D"/>
            <text x="{X(n - 1)}" y="{padT + 10}" fill="#FFF59D" font-size="11" font-family="Verdana" text-anchor="end">{Encode(lastLabel)}</text>
            </svg>
            """;
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
