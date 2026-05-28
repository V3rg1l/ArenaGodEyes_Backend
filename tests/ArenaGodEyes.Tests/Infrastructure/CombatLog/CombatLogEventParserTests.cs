using ArenaGodEyes.Infrastructure.CombatLog;

namespace ArenaGodEyes.Tests.Infrastructure.CombatLog;

public sealed class CombatLogEventParserTests
{
    [Fact]
    public void TryParse_ReturnsArenaStartEventAndFields()
    {
        const string rawLine = "5/27/2026 21:15:10.123  ARENA_MATCH_START,0,980,15,3v3,1";

        var parsed = CombatLogEventParser.TryParse(rawLine);

        Assert.NotNull(parsed);
        Assert.True(parsed!.IsTimestampParsed);
        Assert.Equal("ARENA_MATCH_START", parsed.EventName);
        Assert.Equal("980", parsed.Fields[2]);
        Assert.Equal("3v3", parsed.Fields[4]);
        Assert.Equal("1", parsed.Fields[5]);
    }

    [Fact]
    public void TryParse_PreservesQuotedFieldsWithoutSplittingInnerCommas()
    {
        const string rawLine =
            "5/27/2026 21:16:42.000  SPELL_CAST_SUCCESS,Player-1,\"Mage, Test\",0x511,0x0,Creature-0,\"Training Dummy\",0xa48,0x0,116,\"Frostbolt, Rank 1\"";

        var parsed = CombatLogEventParser.TryParse(rawLine);

        Assert.NotNull(parsed);
        Assert.Equal("SPELL_CAST_SUCCESS", parsed!.EventName);
        Assert.Equal("\"Mage, Test\"", parsed.Fields[2]);
        Assert.Equal("\"Frostbolt, Rank 1\"", parsed.Fields[10]);
    }
}
