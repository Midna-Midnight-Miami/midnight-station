using Content.Shared.Damage;
using Content.Shared.Examine;
using Content.Shared.Inventory;
using Content.Shared.Silicons.Borgs;
using Content.Shared.Verbs;
using Robust.Shared.GameStates;
using Robust.Shared.Utility;
using Content.Shared.Clothing.Components;

// Shitmed Change
using System.Linq;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;

namespace Content.Shared.Armor;

/// <summary>
///     This handles logic relating to <see cref="ArmorComponent" />
/// </summary>
public abstract class SharedArmorSystem : EntitySystem
{
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;

    /// <inheritdoc />
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ArmorComponent, InventoryRelayedEvent<DamageModifyEvent>>(OnRelayDamageModify);
        SubscribeLocalEvent<ArmorComponent, InventoryRelayedEvent<CoefficientQueryEvent>>(OnCoefficientQuery);
        SubscribeLocalEvent<ArmorComponent, BorgModuleRelayedEvent<DamageModifyEvent>>(OnBorgDamageModify);
        SubscribeLocalEvent<ArmorComponent, GetVerbsEvent<ExamineVerb>>(OnArmorVerbExamine);
    }

    private void OnDamageModify(EntityUid uid, ArmorComponent component, DamageModifyEvent args)
    {
        if (args.TargetPart == null)
            return;

        var (partType, _) = _body.ConvertTargetBodyPart(args.TargetPart);

        if (component.ArmorCoverage.Contains(partType))
            args.Damage = DamageSpecifier.ApplyModifierSet(args.Damage,
            DamageSpecifier.PenetrateArmor(component.Modifiers, args.ArmorPenetration));
    }

    /// <summary>
    /// Get the total Damage reduction value of all equipment caught by the relay.
    /// </summary>
    /// <param name="ent">The item that's being relayed to</param>
    /// <param name="args">The event, contains the running count of armor percentage as a coefficient</param>
    private void OnCoefficientQuery(Entity<ArmorComponent> ent, ref InventoryRelayedEvent<CoefficientQueryEvent> args)
    {
        foreach (var armorCoefficient in ent.Comp.Modifiers.Coefficients)
        {
            args.Args.DamageModifiers.Coefficients[armorCoefficient.Key] = args.Args.DamageModifiers.Coefficients.TryGetValue(armorCoefficient.Key, out var coefficient) ? coefficient * armorCoefficient.Value : armorCoefficient.Value;
        }
    }

    private void OnRelayDamageModify(EntityUid uid, ArmorComponent component, InventoryRelayedEvent<DamageModifyEvent> args)
    {
        if (args.Args.TargetPart == null)
            return;

        var (partType, _) = _body.ConvertTargetBodyPart(args.Args.TargetPart);

        if (component.ArmorCoverage.Contains(partType))
            args.Args.Damage = DamageSpecifier.ApplyModifierSet(args.Args.Damage,
            DamageSpecifier.PenetrateArmor(component.Modifiers, args.Args.ArmorPenetration));
    }

    private void OnBorgDamageModify(EntityUid uid, ArmorComponent component,
        ref BorgModuleRelayedEvent<DamageModifyEvent> args)
    {
        args.Args.Damage = DamageSpecifier.ApplyModifierSet(args.Args.Damage,
            DamageSpecifier.PenetrateArmor(component.Modifiers, args.Args.ArmorPenetration)); // Goob edit
    }


private void OnArmorVerbExamine(EntityUid uid, ArmorComponent component, GetVerbsEvent<ExamineVerb> args)
{
    if (!args.CanInteract || !args.CanAccess || !component.ShowArmorOnExamine)
        return;

    // Midnight Station: Handle chameleon perceived armor
    if (TryComp<ChameleonClothingComponent>(uid, out var chameleon) && 
        chameleon.PerceivedShowArmorOnExamine)
    {
        if (chameleon.PerceivedArmourCoverageHidden && chameleon.PerceivedArmourModifiersHidden)
            return;

        if (chameleon.PerceivedArmorModifiers == null || 
            (chameleon.PerceivedArmorModifiers.Coefficients.Count == 0 && 
             chameleon.PerceivedArmorModifiers.FlatReduction.Count == 0))
            return;

        var chameleonExamine = GetChameleonArmorExamine(chameleon);
        var chameleonEv = new ArmorExamineEvent(chameleonExamine);
        RaiseLocalEvent(uid, ref chameleonEv);

        _examine.AddDetailedExamineVerb(args, component, chameleonExamine,
            Loc.GetString("armor-examinable-verb-text"), 
            "/Textures/Interface/VerbIcons/dot.svg.192dpi.png",
            Loc.GetString("armor-examinable-verb-message"));
        return;
    }

    // Existing non-chameleon armor handling
    if (component is { ArmourCoverageHidden: true, ArmourModifiersHidden: true })
        return;

    if (component.Modifiers.Coefficients.Count == 0 && component.Modifiers.FlatReduction.Count == 0)
        return;

    var examineMarkup = GetArmorExamine(component);
    var ev = new ArmorExamineEvent(examineMarkup);
    RaiseLocalEvent(uid, ref ev);

    _examine.AddDetailedExamineVerb(args, component, examineMarkup,
        Loc.GetString("armor-examinable-verb-text"), "/Textures/Interface/VerbIcons/dot.svg.192dpi.png",
        Loc.GetString("armor-examinable-verb-message"));
}

// Regular armor examine method
private FormattedMessage GetArmorExamine(ArmorComponent component)
{
    var msg = new FormattedMessage();
    msg.AddMarkupOrThrow(Loc.GetString("armor-examine"));

    if (!component.ArmourCoverageHidden)
    {
        foreach (var coveragePart in component.ArmorCoverage.Where(p => p != BodyPartType.Other))
        {
            msg.PushNewline();
            var bodyPartType = Loc.GetString("armor-coverage-type-" + coveragePart.ToString().ToLower());
            msg.AddMarkupOrThrow(Loc.GetString("armor-coverage-value", ("type", bodyPartType)));
        }
    }

    if (!component.ArmourModifiersHidden)
    {
        foreach (var coefficientArmor in component.Modifiers.Coefficients)
        {
            msg.PushNewline();
            var armorType = Loc.GetString("armor-damage-type-" + coefficientArmor.Key.ToLower());
            msg.AddMarkupOrThrow(Loc.GetString("armor-coefficient-value",
                ("type", armorType),
                ("value", MathF.Round((1f - coefficientArmor.Value) * 100, 1))
            ));
        }

        foreach (var flatArmor in component.Modifiers.FlatReduction)
        {
            msg.PushNewline();
            var armorType = Loc.GetString("armor-damage-type-" + flatArmor.Key.ToLower());
            msg.AddMarkupOrThrow(Loc.GetString("armor-reduction-value",
                ("type", armorType),
                ("value", flatArmor.Value)
            ));
        }
    }

    return msg;
}

// Chameleon perceived armor examine method
private FormattedMessage GetChameleonArmorExamine(ChameleonClothingComponent component)
{
    var msg = new FormattedMessage();
    msg.AddMarkupOrThrow(Loc.GetString("armor-examine"));

    if (!component.PerceivedArmourCoverageHidden && component.PerceivedArmorCoverage != null)
    {
        foreach (var coveragePart in component.PerceivedArmorCoverage.Where(p => p != BodyPartType.Other))
        {
            msg.PushNewline();
            var bodyPartType = Loc.GetString("armor-coverage-type-" + coveragePart.ToString().ToLower());
            msg.AddMarkupOrThrow(Loc.GetString("armor-coverage-value", ("type", bodyPartType)));
        }
    }

    if (!component.PerceivedArmourModifiersHidden && component.PerceivedArmorModifiers != null)
    {
        foreach (var coefficientArmor in component.PerceivedArmorModifiers.Coefficients)
        {
            msg.PushNewline();
            var armorType = Loc.GetString("armor-damage-type-" + coefficientArmor.Key.ToLower());
            msg.AddMarkupOrThrow(Loc.GetString("armor-coefficient-value",
                ("type", armorType),
                ("value", MathF.Round((1f - coefficientArmor.Value) * 100, 1))
            ));
        }

        foreach (var flatArmor in component.PerceivedArmorModifiers.FlatReduction)
        {
            msg.PushNewline();
            var armorType = Loc.GetString("armor-damage-type-" + flatArmor.Key.ToLower());
            msg.AddMarkupOrThrow(Loc.GetString("armor-reduction-value",
                ("type", armorType),
                ("value", flatArmor.Value)
            ));
        }
    }

    return msg;
}}