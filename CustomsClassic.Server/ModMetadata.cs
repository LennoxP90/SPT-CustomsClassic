using JetBrains.Annotations;
using SPTarkov.Server.Core.Models.Spt.Mod;

namespace CustomsClassic.Server;

[UsedImplicitly]
public record ModMetadata : IModMetadata
{
    public string ModGuid { get; init; } = "com.lennoxp90.customsclassic";
    public string Name { get; init; } = "CustomsClassic";
    public string Author { get; init; } = "LennoxP90";
    public List<string>? Contributors { get; init; }
    public SemanticVersioning.Version Version { get; init; } = new(typeof(ModMetadata).Assembly.GetName().Version!.ToString(3));

    // The real floor is the referenced SPTarkov.Server.Core version (4.1.0), which ModValidator enforces.
    public SemanticVersioning.Range SptVersion { get; init; } = new("~4.1.0");
    public bool HasPrepatcher { get; init; } = false;
    public List<string>? Incompatibilities { get; init; }

    // Upper bound: this calls MapVariants' extension API, which a 2.0.0 may change.
    public Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; } = new()
    {
        ["com.lennoxp90.mapvariants"] = new SemanticVersioning.Range(">=1.1.0 <2.0.0"),
    };
    public string? Url { get; init; }
    public string License { get; init; } = "MIT";
}
