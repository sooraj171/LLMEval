using System.Text.RegularExpressions;

namespace LLMEval
{
    /// <summary>Splits an AI response into individual factual statements (sentences or bullet points) for grounding checks.</summary>
    public static class ResponseStatementSplitter
    {
        private static readonly Regex BulletOrNumberStart = new Regex(
            @"^\s*[\-\*•]\s+|\s*\d+[\.\)]\s+",
            RegexOptions.Compiled | RegexOptions.Multiline);

        /// <summary>Maximum character length for the combined reference text passed to the judge (avoids token overflows).</summary>
        public const int MaxReferenceLength = 12_000;

        /// <summary>Maximum character length for the full AI response when splitting (very long responses are truncated for stability).</summary>
        public const int MaxResponseLength = 8_000;

        /// <summary>Splits the AI response into statements: by sentence boundaries (. ! ?) and by bullet/numbered lines. Empty and very short fragments are omitted.</summary>
        public static IReadOnlyList<string> SplitIntoStatements(string aiResponse)
        {
            if (string.IsNullOrWhiteSpace(aiResponse))
                return Array.Empty<string>();

            var text = aiResponse.Length > MaxResponseLength
                ? aiResponse.Substring(0, MaxResponseLength) + "..."
                : aiResponse;

            var raw = new List<string>();

            // Split by bullet or numbered lines first (keep each line as a statement)
            var lines = text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed)) continue;
                if (BulletOrNumberStart.IsMatch(trimmed))
                {
                    raw.Add(BulletOrNumberStart.Replace(trimmed, " ").Trim());
                    continue;
                }
                // Split remaining by sentence boundaries
                var sentences = Regex.Split(trimmed, @"(?<=[.!?])\s+");
                foreach (var s in sentences)
                {
                    var t = s.Trim();
                    if (t.Length >= 3) raw.Add(t);
                }
            }

            // If we didn't get any bullet-style splits, treat whole text as sentences
            if (raw.Count == 0)
            {
                var sentences = Regex.Split(text, @"(?<=[.!?])\s+");
                foreach (var s in sentences)
                {
                    var t = s.Trim();
                    if (t.Length >= 3) raw.Add(t);
                }
            }

            return raw.Distinct().ToList();
        }
    }
}
