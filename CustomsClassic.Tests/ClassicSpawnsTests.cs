using CustomsClassic.Shared;
using Xunit;

public class ClassicSpawnsTests
{
    static SourceSpawn At(double x, int side) => new SourceSpawn { Name = "SpawnPoint (1)", Pos = new[] { x, 1.0, 0.0 }, Side = side };

    [Theory]
    [InlineData(-322.6, 1, true, false, ClassicSpawns.West)]   // source Usec point, trailer park
    [InlineData(-187.1, 2, true, false, ClassicSpawns.West)]   // source Bear point
    [InlineData(572.2, 4, true, true, ClassicSpawns.East)]     // eastern scav point doubles as an eastern PMC spawn
    [InlineData(6.2, 4, false, true, "")]                      // western scav point stays scav only
    public void Roles(double x, int side, bool pmc, bool savage, string infiltration)
    {
        var role = ClassicSpawns.RoleOf(At(x, side));
        Assert.Equal(pmc, role.Pmc);
        Assert.Equal(savage, role.Savage);
        Assert.Equal(infiltration, role.Infiltration);
    }

    [Fact]
    public void PlayerIsRemovedAndBotKept()
        => Assert.Equal(new[] { "Bot" }, ClassicSpawns.WithoutPlayer(new[] { "Player", "Bot" }));

    [Fact]
    public void PlayerOnlyPointIsDropped()
        => Assert.Null(ClassicSpawns.WithoutPlayer(new[] { "Player" }));

    [Fact]
    public void IdsAreStableAndReadable()
        => Assert.Equal("customsclassic-pmc-spawnpoint1", ClassicSpawns.IdFor(At(0, 1), true));
}
