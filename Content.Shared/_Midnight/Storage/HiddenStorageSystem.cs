using Content.Shared.Tools.Systems;
using Content.Shared._Midnight.Storage;
using Robust.Shared.Serialization;
using Content.Shared.Interaction;
using Content.Shared.Tools;
using Content.Shared.Containers.ItemSlots;
using Robust.Shared.Audio.Systems;
using Content.Shared.DoAfter;

namespace Content.Shared._Midnight.Storage;

public sealed class HiddenStorageSystem : EntitySystem
{
    [Dependency] private readonly SharedToolSystem _toolSystem = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HiddenStorageComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<HiddenStorageComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<HiddenStorageComponent, ToggleHiddenStorageDoAfterEvent>(OnDoAfterComplete);
    }

    private void OnInit(EntityUid uid, HiddenStorageComponent component, ComponentInit args)
    {
        UpdateSlotState(uid, component);
    }

    private void OnInteractUsing(EntityUid uid, HiddenStorageComponent component, InteractUsingEvent args)
    {
        if (args.Handled || !_toolSystem.HasQuality(args.Used, component.OpeningTool))
            return;

        args.Handled = true;

        var doAfterArgs = new DoAfterArgs(EntityManager, args.User, component.OpenDelay,
            new ToggleHiddenStorageDoAfterEvent(), uid, target: uid)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            BreakOnWeightlessMove = false,
            NeedHand = true
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
    }

    private void OnDoAfterComplete(EntityUid uid, HiddenStorageComponent component, ToggleHiddenStorageDoAfterEvent args)
    {
        if (args.Cancelled)
            return;

        // Toggle state
        component.IsOpen = !component.IsOpen;
        Dirty(uid, component);

        // Play sound at END of DoAfter
        _audio.PlayPredicted(component.IsOpen ? component.OpenSound : component.CloseSound, uid, args.User);
        UpdateSlotState(uid, component);
    }

    private void UpdateSlotState(EntityUid uid, HiddenStorageComponent component)
    {
        if (!TryComp<ItemSlotsComponent>(uid, out var itemSlots))
            return;

        foreach (var (id, _) in itemSlots.Slots)
        {
            // Lock slot when storage is closed, unlock when open
            _itemSlots.SetLock(uid, id, !component.IsOpen, itemSlots);
        }
    }
}

[Serializable, NetSerializable]
public sealed partial class ToggleHiddenStorageDoAfterEvent : SimpleDoAfterEvent
{
}