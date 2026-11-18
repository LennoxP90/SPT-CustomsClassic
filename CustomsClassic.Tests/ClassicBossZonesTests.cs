using CustomsClassic.Shared;
using Xunit;

public class ClassicBossZonesTests
{
    static readonly string[] Open = { "ZoneDormitory", "ZoneGasStation", "ZoneCustoms", "ZoneSnipeFactory" };

    [Fact]
    public void NoRuleKeepsTheShippedZonesThatAreStillOpen() =>
        Assert.Equal(new[] { "ZoneDormitory", "ZoneGasStation" }, ClassicBossZones.Resolve(null, new[] { "ZoneDormitory", "ZoneGasStation", "ZoneScavBase" }, Open));

    [Fact]
    public void ARuleReplacesTheShippedZones() =>
        Assert.Equal(new[] { "ZoneCustoms" }, ClassicBossZones.Resolve(new[] { "ZoneCustoms" }, new[] { "ZoneScavBase" }, Open));

    [Fact]
    public void AClosedZoneInARuleIsSkipped() =>
        Assert.Empty(ClassicBossZones.Resolve(new[] { "ZoneScavBase" }, new[] { "ZoneScavBase" }, Open));

    [Fact]
    public void EverywhereIsEveryOpenZoneButTheSniperOnes() =>
        Assert.Equal(new[] { "ZoneCustoms", "ZoneDormitory", "ZoneGasStation" }, ClassicBossZones.Resolve(new[] { "*" }, new[] { "ZoneScavBase" }, Open));

    [Fact]
    public void ABossKeepsItsZoneWhenNoPlayerIsThere() =>
        Assert.Equal("ZoneGasStation", ClassicBossZones.AwayFromPlayers("ZoneGasStation", "ZoneDormitory,ZoneGasStation", z => false, 0));

    [Fact]
    public void ABossMovesToItsOtherZoneWhenAPlayerStartsInTheOneItDrew() =>
        Assert.Equal("ZoneDormitory", ClassicBossZones.AwayFromPlayers("ZoneGasStation", "ZoneDormitory,ZoneGasStation", z => z == "ZoneGasStation", 7));

    [Fact]
    public void ABossStaysOutWhenPlayersAreInEveryZoneItHas() =>
        Assert.Null(ClassicBossZones.AwayFromPlayers("ZoneGasStation", "ZoneDormitory,ZoneGasStation", z => true, 3));

    [Theory]
    [InlineData(22, true)]
    [InlineData(3, true)]
    [InlineData(6, false)]
    [InlineData(12, false)]
    [InlineData(21, false)]
    public void NightRunsFromTenToSix(int hour, bool night) => Assert.Equal(night, ClassicBossZones.IsNight(hour));
}
