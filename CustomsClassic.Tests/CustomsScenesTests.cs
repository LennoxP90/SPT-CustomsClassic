using CustomsClassic.Shared;
using Xunit;

public class CustomsScenesTests
{
    const string D = "Assets/Content/Locations/Custom/";
    const string X = D + "Custom_Expansion_Temp/";

    // The 29 keys of 4.1's bigmap.ScenesPreset, in shipped order.
    static readonly string[] Shipped =
    {
        D + "custom_Scripts.unity", D + "custom_Terrain.unity", D + "custom_mazuto.unity", D + "custom_Garage.unity",
        D + "custom_Tamozhnya.unity", D + "custom_TrailerPark.unity", D + "custom_factoryStorageZone.unity",
        D + "custom_Obshezhitie.unity", D + "custom_Obshezhitie_1_indoor.unity", D + "custom_Obshezhitie_2_indoor.unity",
        D + "custom_AZS.unity", D + "custom_city.unity", D + "custom_multiScene.unity", D + "custom_Road.unity",
        X + "custom_Abandoned_Lab.unity", X + "custom_Abandoned_Plant.unity", X + "Custom_ChemicalFactory.unity",
        D + "custom_AZS_old.unity", X + "Custom_Construction_Factory.unity", X + "custom_Pump_Station.unity",
        X + "Custom_RepairBox.unity", X + "Custom_Expansion.unity", D + "custom_background.unity", D + "custom_Light.unity",
        D + "custom_DesignStuff.unity", D + "custom_DesignMain.unity", D + "custom_AI.unity", D + "Custom_Sound.unity",
        D + "custom_Culling.unity",
    };

    static readonly string[] Bundle = { "Assets/customsclassic_out/customsclassic_map.unity" };

    [Fact]
    public void RecognisesCustomsByItsScriptsScene()
    {
        Assert.True(CustomsScenes.IsCustomsPreset(Shipped));
        Assert.False(CustomsScenes.IsCustomsPreset(new[] { "Assets/Content/Locations/Factory/Factory.unity" }));
    }

    [Fact]
    public void ClassicKeepsFourSystemScenesAndLoadsTheBundleAfterScripts()
    {
        var classic = CustomsScenes.ClassicPaths(Shipped, Bundle);
        Assert.Equal(new[]
        {
            D + "custom_Scripts.unity", Bundle[0], D + "custom_DesignMain.unity", D + "custom_AI.unity", D + "Custom_Sound.unity",
        }, classic);
    }

    [Fact]
    public void ModernPropsLampsAndDoorsAreDropped()
    {
        var classic = CustomsScenes.ClassicPaths(Shipped, Bundle);
        Assert.DoesNotContain(classic, p => p.Contains("custom_DesignStuff") || p.Contains("custom_Light"));
    }

    [Fact]
    public void GeometryExpansionAndCullingAreDropped()
    {
        var classic = CustomsScenes.ClassicPaths(Shipped, Bundle);
        Assert.DoesNotContain(classic, p => p.Contains("custom_Terrain") || p.Contains("Expansion_Temp") || p.Contains("custom_Culling"));
    }

    [Fact]
    public void BundleScenesLeadWhenScriptsIsMissing()
    {
        var classic = CustomsScenes.ClassicPaths(new[] { D + "custom_AI.unity" }, Bundle);
        Assert.Equal(new[] { Bundle[0], D + "custom_AI.unity" }, classic);
    }

    [Fact]
    public void ClassicScenesAreRecognisedByPrefix()
    {
        Assert.True(CustomsScenes.IsClassicScene("customsclassic_map"));
        Assert.False(CustomsScenes.IsClassicScene("custom_Garage"));
    }

    [Theory]
    [InlineData("backport", MapVariant.Classic)]
    [InlineData("original", MapVariant.Original)]
    [InlineData("unmanaged", MapVariant.Original)]
    [InlineData(null, MapVariant.Original)]
    public void MapVariantsWordsTranslate(string theirs, string ours)
        => Assert.Equal(ours, VariantVocabulary.FromMapVariants(theirs));

    [Fact]
    public void OurClassicIsTheirBackport()
        => Assert.Equal("backport", VariantVocabulary.ToMapVariants(MapVariant.Classic));
}
