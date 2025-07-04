using Robust.Shared.GameStates;
using Content.Shared.StatusIcon;
using Robust.Shared.Prototypes;

namespace Content.Shared.Revolutionary.Components;

/// <summary>
/// Component used for marking a Head Rev for conversion and winning/losing.
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(SharedRevolutionarySystem))]
public sealed partial class HeadRevolutionaryComponent : Component
{
    /// <summary>
    /// The status icon corresponding to the head revolutionary.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public ProtoId<FactionIconPrototype> StatusIcon { get; set; } = "HeadRevolutionaryFaction";

    public readonly List<ProtoId<EntityPrototype>> BaseHeadRevActions = new()
    {
        "ActionDeclareOpenRevolt",
    };

    /// <summary>
    /// How long the stun will last after the user is converted.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan StunTime = TimeSpan.FromSeconds(3);

    public override bool SessionSpecific => true;

    //Goobstation
    /// <summary>
    /// If head rev's convert ability is not disabled by mindshield
    /// </summary>
    [DataField]
    public bool ConvertAbilityEnabled = true;

    //Funky Station
    /// <summary>
    /// If head rev's convert ability distributes the ability to see other Revs.
    /// </summary>
    [DataField]
    public bool ConvertGivesRevVision = false;
}
