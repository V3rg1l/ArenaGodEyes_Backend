using System.Globalization;
using System.Text;

namespace ArenaGodEyes.Infrastructure.CombatLog;

public sealed record CombatLogEventLine(
    string RawLine,
    DateTimeOffset? Timestamp,
    bool IsTimestampParsed,
    string EventName,
    IReadOnlyList<string> Fields);

public static class CombatLogEventParser
{
    private static readonly string[] TimestampSeparators = ["  "];

    public static CombatLogEventLine? TryParse(string rawLine)
    {
        if (string.IsNullOrWhiteSpace(rawLine))
        {
            return null;
        }

        var parts = rawLine.Split(TimestampSeparators, 2, StringSplitOptions.None);
        if (parts.Length != 2)
        {
            return new CombatLogEventLine(
                rawLine,
                null,
                false,
                "UNKNOWN",
                [rawLine]);
        }

        var timestamp = TryParseTimestamp(parts[0]);
        var fields = SplitCsv(parts[1]);
        var eventName = fields.Count > 0 ? fields[0] : "UNKNOWN";

        return new CombatLogEventLine(
            rawLine,
            timestamp,
            timestamp is not null,
            eventName,
            fields);
    }

    private static DateTimeOffset? TryParseTimestamp(string timestampText)
    {
        return DateTimeOffset.TryParse(
            timestampText,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeLocal,
            out var parsed)
            ? parsed
            : null;
    }

    private static IReadOnlyList<string> SplitCsv(string input)
    {
        var values = new List<string>();
        var builder = new StringBuilder();
        var inQuotes = false;
        var bracketDepth = 0;
        var parenthesisDepth = 0;

        foreach (var character in input)
        {
            switch (character)
            {
                case '"':
                    inQuotes = !inQuotes;
                    builder.Append(character);
                    break;
                case '[' when !inQuotes:
                    bracketDepth++;
                    builder.Append(character);
                    break;
                case ']' when !inQuotes && bracketDepth > 0:
                    bracketDepth--;
                    builder.Append(character);
                    break;
                case '(' when !inQuotes:
                    parenthesisDepth++;
                    builder.Append(character);
                    break;
                case ')' when !inQuotes && parenthesisDepth > 0:
                    parenthesisDepth--;
                    builder.Append(character);
                    break;
                case ',' when !inQuotes && bracketDepth == 0 && parenthesisDepth == 0:
                    values.Add(builder.ToString());
                    builder.Clear();
                    break;
                default:
                    builder.Append(character);
                    break;
            }
        }

        values.Add(builder.ToString());
        return values;
    }
}
