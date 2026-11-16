using System.Linq;
using CustomsClassic.Shared;
using Xunit;

public class ClassicExitsTests
{
    [Fact]
    public void FourPmcAndFourScavExits()
    {
        Assert.Equal(4, ClassicExits.All.Count(e => !e.Scav));
        Assert.Equal(4, ClassicExits.All.Count(e => e.Scav));
    }

    [Fact]
    public void EverySourceExitHasAPmcAndAScavName()
    {
        foreach (var spot in ClassicExits.All.GroupBy(e => (e.X, e.Z)))
        {
            Assert.Contains(spot, e => !e.Scav);
            Assert.Contains(spot, e => e.Scav);
        }
    }

    [Fact]
    public void NamesMatchCaseInsensitively()
    {
        Assert.True(ClassicExits.IsClassic("old gas station"));
        Assert.False(ClassicExits.IsClassic("ZB-1012"));
        Assert.True(ClassicExits.IsClassic("Crossroads"));
        Assert.Null(ClassicExits.Find(null));
    }
}
