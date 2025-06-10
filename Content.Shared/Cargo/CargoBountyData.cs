// SPDX-FileCopyrightText: 2023 DrSmugleaf <DrSmugleaf@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 Leon Friedrich <60421075+ElectroJr@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 metalgearsloth <31366439+metalgearsloth@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 Nemanja <98561806+EmoGarbage404@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
//
// SPDX-License-Identifier: MIT

using Robust.Shared.Serialization;
using Content.Shared.Cargo.Prototypes;

namespace Content.Shared.Cargo;

/// <summary>
/// A data structure for storing currently available bounties.
/// </summary>
[DataDefinition, NetSerializable, Serializable]
public readonly partial record struct CargoBountyData
{
    /// <summary>
    /// A unique id used to identify the bounty
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// The prototype containing information about the bounty.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField(required: true)]
    public ProtoId<CargoBountyPrototype> Bounty { get; init; } = string.Empty;

    public CargoBountyData(CargoBountyPrototype bounty, int uniqueIdentifier)
    {
        Bounty = bounty.ID;
        Id = $"{bounty.IdPrefix}{uniqueIdentifier:D3}";
    }

    /// <summary>
    /// Used for creating bounties via the old system with pre-defined bounties
    /// </summary>
    /// <param name="uniqueIdentifier">Some number to be used as an ID with IdPrefix</param>
    /// <param name="prototype">The prototype of the bounty to be created</param>
    public CargoBountyData(int uniqueIdentifier, CargoBountyPrototype prototype)
    {
        Id = $"{IdPrefix}{uniqueIdentifier:D3}";
        Description = prototype.Description;
        IdPrefix = prototype.IdPrefix;
        Reward = prototype.Reward;
        var items = new List<CargoBountyItemData>();
        foreach (var entry in prototype.Entries)
        {
            CargoBountyItemData newItem = entry switch
            {
                CargoObjectBountyItemEntry itemEntry => new CargoObjectBountyItemData(itemEntry),
                CargoReagentBountyItemEntry itemEntry => new CargoReagentBountyItemData(itemEntry),
                _ => throw new NotImplementedException($"Unknown type: {entry.GetType().Name}"),
            };
            items.Add(newItem);
        }
        Entries = items;
    }

    public CargoBountyData()
    {

    }
}
