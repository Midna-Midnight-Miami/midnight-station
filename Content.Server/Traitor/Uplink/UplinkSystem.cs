
using Content.Server.Store.Systems;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Implants;
using Content.Shared.Inventory;
using Content.Shared.Mind;
using Content.Shared.PDA;
using Content.Shared.Store;
using Content.Shared.Store.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.Traitor.Uplink;

// goobstation - heavily edited. fuck newstore
// do not touch unless you want to shoot yourself in the leg
public sealed class UplinkSystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventorySystem = default!;
    [Dependency] private readonly SharedHandsSystem _handsSystem = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly StoreSystem _store = default!;
    [Dependency] private readonly SharedSubdermalImplantSystem _subdermalImplant = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;

    [ValidatePrototypeId<CurrencyPrototype>]
    public const string TelecrystalCurrencyPrototype = "Telecrystal";
    private const string FallbackUplinkImplant = "UplinkImplant";
    private const string FallbackUplinkCatalog = "UplinkUplinkImplanter";


        /// <summary>
        /// Adds an uplink to the target
        /// </summary>
        /// <param name="user">The person who is getting the uplink</param>
        /// <param name="balance">The amount of currency on the uplink. If null, will just use the amount specified in the preset.</param>
        /// <param name="currencyProtoId">Id of the currency the store uses. If null, uses Telecrystal</param>
        /// <param name="storePreset">If set to a value, will clear the original preset and replace it with this one.</param>
        /// <param name="uplinkEntity">The entity that will actually have the uplink functionality. Defaults to the PDA if null.</param>
        /// <returns>Whether or not the uplink was added successfully</returns>
        public bool AddUplink(EntityUid user, FixedPoint2? balance, EntProtoId? currencyProtoId, EntProtoId? storePreset, EntityUid? uplinkEntity = null)
        {
            // Try to find target item
            if (uplinkEntity == null)
            {
                uplinkEntity = FindUplinkTarget(user);
                if (uplinkEntity == null)
                    return false;
            }

            if (storePreset != null && HasComp<StoreComponent>(uplinkEntity.Value))
            {
                EnsureComp<StoreComponent>(uplinkEntity.Value, out var uplinkPdaStore);

                var ent = Spawn(storePreset);
                if (!TryComp<StoreComponent>(ent, out var comp))
                    return false;

                uplinkPdaStore.Categories = comp.Categories;
                uplinkPdaStore.CurrencyWhitelist = comp.CurrencyWhitelist;
                uplinkPdaStore.Name = comp.Name;
                uplinkPdaStore.RefundAllowed = comp.RefundAllowed;

                QueueDel(ent);
            }

            EnsureComp<UplinkComponent>(uplinkEntity.Value);
            var store = EnsureComp<StoreComponent>(uplinkEntity.Value); // so this is why every pda has StorePresetUplink
            store.AccountOwner = user;
            store.Balance.Clear();
            if (balance != null)
            {
                store.Balance.Clear();
                _store.TryAddCurrency(new Dictionary<string, FixedPoint2> { { currencyProtoId ?? TelecrystalCurrencyPrototype, balance.Value } }, uplinkEntity.Value, store);
            }

            // TODO add BUI. Currently can't be done outside of yaml -_-

            return true;
        }

        public bool AddUplink(EntityUid user, FixedPoint2? balance, EntityUid? uplinkEntity = null)
        {
            return AddUplink(user, balance, null, null, uplinkEntity);
        }

    /// <summary>
    /// Configure TC for the uplink
    /// </summary>
    private void SetUplink(EntityUid user, EntityUid uplink, FixedPoint2 balance)
    {
        if (!_mind.TryGetMind(user, out var mind, out _))
            return;

        var store = EnsureComp<StoreComponent>(uplink);

        store.AccountOwner = mind;

        store.Balance.Clear();
        var bal = new Dictionary<string, FixedPoint2> { { TelecrystalCurrencyPrototype, balance } };
        _store.TryAddCurrency(bal, uplink, store);
    }

    /// <summary>
    /// Implant an uplink as a fallback measure if the user had no PDA
    /// </summary>
    /// <param name="user">The user to implant the uplink in</param>
    /// <param name="balance">The TC balance for the uplink</param>
    /// <param name="storePreset">The store catalog to use for the uplink</param>
    /// <returns>Whether the implant was successfully added</returns>
    public bool ImplantUplink(EntityUid user, FixedPoint2 balance, EntProtoId storePreset)
    {
        var implantProto = new string(FallbackUplinkImplant);

        var implant = _subdermalImplant.AddImplant(user, implantProto);

        if (!HasComp<StoreComponent>(implant))
            return false;

        if (HasComp<StoreComponent>(implant.Value))
        {
            EnsureComp<StoreComponent>(implant.Value, out var implantStore);

            var ent = Spawn(storePreset);
            if (!TryComp<StoreComponent>(ent, out var comp))
                return false;

            implantStore.Categories = comp.Categories;
            implantStore.CurrencyWhitelist = comp.CurrencyWhitelist;
            implantStore.Name = comp.Name;
            implantStore.RefundAllowed = comp.RefundAllowed;

            QueueDel(ent);
        }

        SetUplink(user, implant.Value, balance);
        return true;
    }

    /// <summary>
    /// Implant an uplink as a fallback measure if the traitor had no PDA
    /// </summary>
    private bool ImplantUplink(EntityUid user, FixedPoint2 balance)
    {
        var implantProto = new string(FallbackUplinkImplant);

        if (!_proto.TryIndex<ListingPrototype>(FallbackUplinkCatalog, out var catalog))
            return false;

        if (!catalog.Cost.TryGetValue(TelecrystalCurrencyPrototype, out var cost))
            return false;

        if (balance < cost) // Can't use Math functions on FixedPoint2
            balance = 0;
        else
            balance = balance - cost;

        var implant = _subdermalImplant.AddImplant(user, implantProto);

        if (!HasComp<StoreComponent>(implant))
            return false;

        SetUplink(user, implant.Value, balance);
        return true;
    }

        /// <summary>
        /// Finds the entity that can hold an uplink for a user.
        /// Usually this is a pda in their pda slot, but can also be in their hands. (but not pockets or inside bag, etc.)
        /// </summary>
        public EntityUid? FindUplinkTarget(EntityUid user)
        {
            // Try to find PDA in inventory
            if (_inventorySystem.TryGetContainerSlotEnumerator(user, out var containerSlotEnumerator))
            {
                while (containerSlotEnumerator.MoveNext(out var pdaUid))
                {
                    if (!pdaUid.ContainedEntity.HasValue) continue;

                    if (HasComp<PdaComponent>(pdaUid.ContainedEntity.Value) || HasComp<StoreComponent>(pdaUid.ContainedEntity.Value))
                        return pdaUid.ContainedEntity.Value;
                }
            }

            // Also check hands
            foreach (var item in _handsSystem.EnumerateHeld(user))
            {
                if (HasComp<PdaComponent>(item) || HasComp<StoreComponent>(item))
                    return item;
            }

            return null;
        }
    }
