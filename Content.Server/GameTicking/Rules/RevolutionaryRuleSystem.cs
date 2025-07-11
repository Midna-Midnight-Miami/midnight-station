using Content.Server.Administration.Logs;
using Content.Server.Antag;
using Content.Server.EUI;
using Content.Server.Flash;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Mind;
using Content.Server.Popups;
using Content.Server.Revolutionary;
using Content.Server.Revolutionary.Components;
using Content.Server.Roles;
using Content.Server.RoundEnd;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Systems;
using Content.Shared.Database;
using Content.Shared.GameTicking.Components;
using Content.Shared.Humanoid;
using Content.Shared.IdentityManagement;
using Content.Shared.Mind.Components;
using Content.Shared.Mindshield.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Content.Shared.Popups;
using Content.Shared.Revolutionary.Components;
using Content.Shared.Stunnable;
using Content.Shared.Zombies;
using Content.Shared.Heretic;
using Content.Goobstation.Common.Changeling;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Content.Shared.Cuffs.Components;
using Content.Shared.Revolutionary;
using Content.Server.Communications;
using System.Linq;
using Content.Goobstation.Shared.Revolutionary;
using Content.Server.Chat.Systems;

using Content.Server.PDA.Ringer;
using Content.Server.Traitor.Uplink;
using Content.Goobstation.Shared.Changeling;
using Content.Shared.Heretic;

namespace Content.Server.GameTicking.Rules;

/// <summary>
/// Where all the main stuff for Revolutionaries happens (Assigning Head Revs, Command on station, and checking for the game to end.)
/// </summary>
// Heavily edited by goobstation. If you want to upstream something think twice
public sealed class RevolutionaryRuleSystem : GameRuleSystem<RevolutionaryRuleComponent>
{
    [Dependency] private readonly IAdminLogManager _adminLogManager = default!;
    [Dependency] private readonly AntagSelectionSystem _antag = default!;
    [Dependency] private readonly EmergencyShuttleSystem _emergencyShuttle = default!;
    [Dependency] private readonly EuiManager _euiMan = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly NpcFactionSystem _npcFaction = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly RoleSystem _role = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly RoundEndSystem _roundEnd = default!;
    [Dependency] private readonly StationSystem _stationSystem = default!;
    [Dependency] private readonly SharedRevolutionarySystem _revolutionarySystem = default!;
    [Dependency] private readonly ChatSystem _chatSystem = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly UplinkSystem _uplink = default!;


    //Used in OnPostFlash, no reference to the rule component is available
    public readonly ProtoId<NpcFactionPrototype> RevolutionaryNpcFaction = "Revolutionary";
    public readonly ProtoId<NpcFactionPrototype> RevPrototypeId = "Rev";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CommandStaffComponent, MobStateChangedEvent>(OnCommandMobStateChanged);

        SubscribeLocalEvent<HeadRevolutionaryComponent, AfterFlashedEvent>(OnPostFlash);
        SubscribeLocalEvent<CommunicationConsoleCallShuttleAttemptEvent>(OnTryCallEvac); // goob edit
        SubscribeLocalEvent<HeadRevolutionaryComponent, MobStateChangedEvent>(OnHeadRevMobStateChanged);
        SubscribeLocalEvent<HeadRevolutionaryComponent, DeclareOpenRevoltEvent>(OnHeadRevDeclareOpenRevolt); //Funky Station

        SubscribeLocalEvent<RevolutionaryRuleComponent, AfterAntagEntitySelectedEvent>(AfterEntitySelected); // Funky Station
        SubscribeLocalEvent<RevolutionaryRoleComponent, GetBriefingEvent>(OnGetBriefing);

    }

    protected override void Started(EntityUid uid, RevolutionaryRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);
        component.CommandCheck = _timing.CurTime + component.TimerWait;
    }

    private void AfterEntitySelected(Entity<RevolutionaryRuleComponent> ent, ref AfterAntagEntitySelectedEvent args)
    {
        if (HasComp<HeadRevolutionaryComponent>(args.EntityUid))
        {
             MakeHeadRevolutionary(args.EntityUid, ent);
        }  
    }

    /// <summary>
    /// (Funky Station) Adds a revolutionary uplink to HRevs. Makes midround HRevs less awkward,
    /// now that they aren't dropping their fucking kit in the middle of security.
    /// </summary>
    /// <returns>true if uplink was successfully added.</returns>
    private bool MakeHeadRevolutionary(EntityUid traitor, RevolutionaryRuleComponent component)
    {
        //Sync Open Revolt state effects to new Head Rev
        if (component.OpenRevoltDeclared && TryComp<HeadRevolutionaryComponent>(traitor, out var headRevComp))
            _revolutionarySystem.ToggleConvertGivesVision((traitor, headRevComp), true);
    
        //Add Rev Uplink
        if (!_mind.TryGetMind(traitor, out var mindId, out var mind))
            return false;

        var pda = _uplink.FindUplinkTarget(traitor);
        EntityUid? uplinkEntity = null;
        
        if (pda != null && _uplink.AddUplink(traitor, 50, "Telecrystal", component.UplinkStoreId, pda))
        {
            uplinkEntity = pda;
        }
        else
        {
            // Fallback to implant with 5 TC cost (45 TC remaining)
            if (!_uplink.ImplantUplink(traitor, 45, component.UplinkStoreId))
                return false;
            // For implants, we don't have a ringer code, so we'll skip that part
            _antag.SendBriefing(traitor, Loc.GetString("head-rev-role-greeting"), Color.Red, null);
        
            if (_role.MindHasRole<RevolutionaryRoleComponent>(mindId, out var revRoleComp))
                AddComp(revRoleComp.Value, new RoleBriefingComponent { Briefing = Loc.GetString("head-rev-briefing-implant") }, overwrite: true);
            
            return true;
        }

        // Only get ringer code if we successfully added uplink to PDA
        if (uplinkEntity.HasValue)
        {
            var code = EnsureComp<RingerUplinkComponent>(uplinkEntity.Value).Code;
            _antag.SendBriefing(traitor, Loc.GetString("head-rev-role-greeting"), Color.Red, null);
    
            if (_role.MindHasRole<RevolutionaryRoleComponent>(mindId, out var revRoleComp))
                AddComp(revRoleComp.Value, new RoleBriefingComponent { Briefing = Loc.GetString("head-rev-briefing", ("code", string.Join("-", code).Replace("sharp", "#"))) }, overwrite: true);
        }

    return true;
}

    protected override void ActiveTick(EntityUid uid, RevolutionaryRuleComponent component, GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, component, gameRule, frameTime);

        if (component.CommandCheck <= _timing.CurTime)
        {
            component.CommandCheck = _timing.CurTime + component.TimerWait;

            // goob edit
            if (CheckCommandLose())
            {
                if (!component.HasRevAnnouncementPlayed)
                {
                    _chatSystem.DispatchGlobalAnnouncement(
                        Loc.GetString("revolutionaries-win-announcement"),
                        Loc.GetString("revolutionaries-win-sender"),
                        colorOverride: Color.Gold);

                    component.HasRevAnnouncementPlayed = true;
                }

                foreach (var ms in EntityQuery<MindShieldComponent, MobStateComponent>())
                {
                    var entity = ms.Item1.Owner;

                    // assign eotrs
                    if (HasComp<RevolutionEnemyComponent>(entity))
                        continue;
                    var revenemy = EnsureComp<RevolutionEnemyComponent>(entity);
                    _antag.SendBriefing(entity, Loc.GetString("rev-eotr-gain"), Color.Red, revenemy.RevStartSound);
                }
            }

            if (CheckRevsLose() && !component.HasAnnouncementPlayed)
            {
                _chatSystem.DispatchGlobalAnnouncement(
                    Loc.GetString("revolutionaries-lose-announcement"),
                    Loc.GetString("revolutionaries-sender-cc"),
                    colorOverride: Color.Gold);

                component.HasAnnouncementPlayed = true;
            }

            if (component.OpenRevoltAnnouncementPending)
            {
                //Build string for announcement
                string headRevNameList = "";

                var headRevs = AllEntityQuery<HeadRevolutionaryComponent, MobStateComponent>();
                while (headRevs.MoveNext(out var headRev, out var headRevComp, out _))
                {
                    if (!TryComp<MetaDataComponent>(headRev, out var headRevData))
                        continue;
                    if (headRevNameList.Length > 0)
                        headRevNameList += ", ";
                    headRevNameList += headRevData.EntityName;
                }

                _chatSystem.DispatchGlobalAnnouncement(
                        Loc.GetString("revolutionaries-open-revolt-announcement", ("nameList", headRevNameList)),
                        Loc.GetString("revolutionaries-sender-cc"),
                        colorOverride: Color.Red);
                
                component.OpenRevoltAnnouncementPending = false;
            }
        }
    }

    protected override void AppendRoundEndText(EntityUid uid,
        RevolutionaryRuleComponent component,
        GameRuleComponent gameRule,
        ref RoundEndTextAppendEvent args)
    {
        base.AppendRoundEndText(uid, component, gameRule, ref args);

        var revsLost = CheckRevsLose();
        var commandLost = CheckCommandLose();
        // This is (revsLost, commandsLost) concatted together
        // (moony wrote this comment idk what it means)
        var index = (commandLost ? 1 : 0) | (revsLost ? 2 : 0);
        args.AddLine(Loc.GetString(Outcomes[index]));

        var sessionData = _antag.GetAntagIdentifiers(uid);
        args.AddLine(Loc.GetString("rev-headrev-count", ("initialCount", sessionData.Count)));
        foreach (var (mind, data, name) in sessionData)
        {
            _role.MindHasRole<RevolutionaryRoleComponent>(mind, out var role);
            var count = CompOrNull<RevolutionaryRoleComponent>(role)?.ConvertedCount ?? 0;

            args.AddLine(Loc.GetString("rev-headrev-name-user",
                ("name", name),
                ("username", data.UserName),
                ("count", count)));

            // TODO: someone suggested listing all alive? revs maybe implement at some point
        }
    }

    private void OnGetBriefing(EntityUid uid, RevolutionaryRoleComponent comp, ref GetBriefingEvent args)
    {
        var ent = args.Mind.Comp.OwnedEntity;
        var head = HasComp<HeadRevolutionaryComponent>(ent);

        if (!head)
        {
            args.Append(Loc.GetString("rev-briefing"));
        }
    }

    /// <summary>
    /// Called when a Head Rev uses a flash in melee to convert somebody else.
    /// </summary>
    private void OnPostFlash(EntityUid uid, HeadRevolutionaryComponent comp, ref AfterFlashedEvent ev)
    {
        // GoobStation - check if headRev's ability enabled
        if (!comp.ConvertAbilityEnabled)
            return;

        var alwaysConvertible = HasComp<AlwaysRevolutionaryConvertibleComponent>(ev.Target);

        if (!_mind.TryGetMind(ev.Target, out var mindId, out var mind) && !alwaysConvertible)
            return;

        if (HasComp<RevolutionaryComponent>(ev.Target) ||
            HasComp<MindShieldComponent>(ev.Target) ||
            !HasComp<HumanoidAppearanceComponent>(ev.Target) &&
            !alwaysConvertible ||
            !_mobState.IsAlive(ev.Target) ||
            HasComp<ZombieComponent>(ev.Target) ||
            HasComp<HereticComponent>(ev.Target) ||
            HasComp<ChangelingComponent>(ev.Target) // goob edit - no more ling or heretic revs
            || HasComp<CommandStaffComponent>(ev.Target)) // goob edit - rev no command flashing
        {
            return;
        }

        if (HasComp<RevolutionEnemyComponent>(ev.Target))
            RemComp<RevolutionEnemyComponent>(ev.Target);

        _npcFaction.AddFaction(ev.Target, RevolutionaryNpcFaction);
        var revComp = EnsureComp<RevolutionaryComponent>(ev.Target);

        if (comp.ConvertGivesRevVision)
            EnsureComp<ShowRevolutionaryIconsComponent>(ev.Target);

        _popup.PopupEntity(Loc.GetString("flash-component-user-head-rev",
            ("victim", Identity.Entity(ev.Target, EntityManager))), ev.Target);

        if (ev.User != null)
        {
            _adminLogManager.Add(LogType.Mind,
                LogImpact.Medium,
                $"{ToPrettyString(ev.User.Value)} converted {ToPrettyString(ev.Target)} into a Revolutionary");

            if (_mind.TryGetMind(ev.User.Value, out var revMindId, out _))
            {
                if (_role.MindHasRole<RevolutionaryRoleComponent>(revMindId, out var role))
                    role.Value.Comp2.ConvertedCount++;
            }
        }

        if (mindId == default || !_role.MindHasRole<RevolutionaryRoleComponent>(mindId))
        {
            _role.MindAddRole(mindId, "MindRoleRevolutionary");
        }

        if (mind?.Session != null)
            _antag.SendBriefing(mind.Session, Loc.GetString("rev-role-greeting"), Color.Red, revComp.RevStartSound);

        var message = Loc.GetString("flash-component-user-head-rev",
            ("victim", Identity.Entity(ev.Target, EntityManager)));
    
        _popup.PopupEntity(message, ev.Target);
  


        // Goobstation - Check lose if command was converted
        if (!TryComp<CommandStaffComponent>(ev.Target, out var commandComp))
            return;

        commandComp.Enabled = false;
        CheckCommandLose();
    }

    //~~TODO: Enemies of the revolution~~
    // goob edit: too bad wizden goob did it first :trollface:
    private void OnCommandMobStateChanged(EntityUid uid, CommandStaffComponent comp, MobStateChangedEvent ev)
    {
        if (ev.NewMobState == MobState.Dead || ev.NewMobState == MobState.Invalid)
            CheckCommandLose();
    }

    /// <summary>
    /// Checks if all of command is dead and if so will remove all sec and command jobs if there were any left.
    /// </summary>
    private bool CheckCommandLose()
    {
        var commandList = new List<EntityUid>();

        var heads = AllEntityQuery<CommandStaffComponent>();
        while (heads.MoveNext(out var id, out var commandComp)) // GoobStation - commandComp
        {
            // GoobStation - If mindshield was removed from head and he got converted - he won't count as command
            if (commandComp.Enabled)
                commandList.Add(id);
        }

        return IsGroupDetainedOrDead(commandList, true, true, true);
    }

    private void OnHeadRevMobStateChanged(EntityUid uid, HeadRevolutionaryComponent comp, MobStateChangedEvent ev)
    {
        if (ev.NewMobState == MobState.Dead || ev.NewMobState == MobState.Invalid)
            if (CheckRevsLose())
                DeconvertAllRevs();
    }

    /// <summary>
    /// Funky Station - yeah
    /// </summary>
    private void DeconvertAllRevs()
    {
        var stunTime = TimeSpan.FromSeconds(4);
        var rev = AllEntityQuery<RevolutionaryComponent, MindContainerComponent>();

        while (rev.MoveNext(out var uid, out _, out var mc))
        {
            if (HasComp<HeadRevolutionaryComponent>(uid))
                continue;

            _npcFaction.RemoveFaction(uid, RevolutionaryNpcFaction);
            _stun.TryParalyze(uid, stunTime, true); // todo: use gamerule
            RemCompDeferred<RevolutionaryComponent>(uid);
            RemCompDeferred<ShowRevolutionaryIconsComponent>(uid);
            _popup.PopupEntity(Loc.GetString("rev-break-control", ("name", Identity.Entity(uid, EntityManager))), uid);
            _adminLogManager.Add(LogType.Mind, LogImpact.Medium, $"{ToPrettyString(uid)} was deconverted due to all Head Revolutionaries dying.");

            // Goobstation - check if command staff was deconverted
            if (TryComp<CommandStaffComponent>(uid, out var commandComp))
                commandComp.Enabled = true;

            if (!_mind.TryGetMind(uid, out var mindId, out _, mc))
                continue;

            // remove their antag role
            _role.MindTryRemoveRole<RevolutionaryRoleComponent>(mindId);

            // make it very obvious to the rev they've been deconverted since
            // they may not see the popup due to antag and/or new player tunnel vision
            if (_mind.TryGetSession(mindId, out var session))
                _euiMan.OpenEui(new DeconvertedEui(), session);
        }
    }

    /// <summary>
    /// Checks if all the Head Revs are dead and if so will deconvert all regular revs.
    /// </summary>
    private bool CheckRevsLose()
    {
        var stunTime = TimeSpan.FromSeconds(4);
        var headRevList = new List<EntityUid>();

        var headRevs = AllEntityQuery<HeadRevolutionaryComponent, MobStateComponent>();
        while (headRevs.MoveNext(out var uid, out var headRevComp, out _)) // GoobStation - headRevComp
        {
            // GoobStation - Checking if headrev ability is enabled to count them
            if (headRevComp.ConvertAbilityEnabled)
                headRevList.Add(uid);
        }

        // If no Head Revs are alive all normal Revs will lose their Rev status and rejoin Nanotrasen
        // Cuffing Head Revs is not enough - they must be killed.
        if (IsGroupDetainedOrDead(headRevList, false, false, false))
        {
            var rev = AllEntityQuery<RevolutionaryComponent, MindContainerComponent>();
            while (rev.MoveNext(out var uid, out _, out var mc))
            {
                if (HasComp<HeadRevolutionaryComponent>(uid))
                    continue;

                _npcFaction.RemoveFaction(uid, RevolutionaryNpcFaction);
                _stun.TryParalyze(uid, stunTime, true);
                RemCompDeferred<RevolutionaryComponent>(uid);
                _popup.PopupEntity(Loc.GetString("rev-break-control", ("name", Identity.Entity(uid, EntityManager))), uid);
                _adminLogManager.Add(LogType.Mind, LogImpact.Medium, $"{ToPrettyString(uid)} was deconverted due to all Head Revolutionaries dying.");

                // Goobstation - check if command staff was deconverted
                if (TryComp<CommandStaffComponent>(uid, out var commandComp))
                    commandComp.Enabled = true;

                if (!_mind.TryGetMind(uid, out var mindId, out _, mc))
                    continue;

                // remove their antag role
                _role.MindTryRemoveRole<RevolutionaryRoleComponent>(mindId);

                // make it very obvious to the rev they've been deconverted since
                // they may not see the popup due to antag and/or new player tunnel vision
                if (_mind.TryGetSession(mindId, out var session))
                    _euiMan.OpenEui(new DeconvertedEui(), session);
            }
            return true;
        }

        return false;
    }

    // goob edit - no shuttle call until internal affairs are figured out
    private void OnTryCallEvac(ref CommunicationConsoleCallShuttleAttemptEvent ev)
    {
        var revs = EntityQuery<RevolutionaryComponent, MobStateComponent>();
        var revenemies = EntityQuery<RevolutionEnemyComponent, MobStateComponent>();
        var minds = EntityQuery<MindContainerComponent>();

        var revsNormalized = revs.Count() / (minds.Count() - revs.Count());
        var enemiesNormalized = revenemies.Count() / (minds.Count() - revenemies.Count());

        // calling evac will result in an error if:
        // - command is gone & there are more than 35% of enemies
        // - or if there are more than 35% of revolutionaries
        // hardcoded values because idk why not
        // regards
        if (CheckCommandLose() && enemiesNormalized >= .35f
        || revsNormalized >= .35f)
        {
            ev.Cancelled = true;
            ev.Reason = Loc.GetString("shuttle-call-error");
            return;
        }
    }

    /// <summary>
    /// Will take a group of entities and check if these entities are alive, dead or cuffed.
    /// </summary>
    /// <param name="list">The list of the entities</param>
    /// <param name="checkOffStation">Bool for if you want to check if someone is in space and consider them missing in action. (Won't check when emergency shuttle arrives just in case)</param>
    /// <param name="countCuffed">Bool for if you don't want to count cuffed entities.</param>
    /// <param name="countRevolutionaries">Bool for if you want to count revolutionaries.</param>
    /// <returns></returns>
    private bool IsGroupDetainedOrDead(List<EntityUid> list, bool checkOffStation, bool countCuffed, bool countRevolutionaries)
    {
        var gone = 0;

        foreach (var entity in list)
        {
            if (TryComp<CuffableComponent>(entity, out var cuffed) && cuffed.CuffedHandCount > 0 && countCuffed)
            {
                gone++;
                continue;
            }

            if (TryComp<MobStateComponent>(entity, out var state))
            {
                if (state.CurrentState == MobState.Dead || state.CurrentState == MobState.Invalid)
                {
                    gone++;
                    continue;
                }

                if (checkOffStation && _stationSystem.GetOwningStation(entity) == null && !_emergencyShuttle.EmergencyShuttleArrived)
                {
                    gone++;
                    continue;
                }
            }
            //If they don't have the MobStateComponent they might as well be dead.
            else
            {
                gone++;
                continue;
            }

            if ((HasComp<RevolutionaryComponent>(entity) || HasComp<HeadRevolutionaryComponent>(entity)) && countRevolutionaries)
            {
                gone++;
                continue;
            }
        }

        return gone == list.Count || list.Count == 0;
    }

    /// <summary>
    /// Declares a state of Open Revolt. This allows all Revolutionaries to see each other, at the cost of announcing openly the names of the Head Revolutionaries
    /// </summary>
    private void DeclareOpenRevolt()
    {
        var query = QueryActiveRules();
        while (query.MoveNext(out var uid, out _, out var revolutionaryRule, out _))
        {
            if (revolutionaryRule.OpenRevoltDeclared)
                return;

            revolutionaryRule.OpenRevoltDeclared = true;
            //Queue announcement
            revolutionaryRule.OpenRevoltAnnouncementPending = true;
        }

        var headRevs = AllEntityQuery<HeadRevolutionaryComponent, MobStateComponent>();
        while (headRevs.MoveNext(out var uid, out var headRevComp, out _))
        {
            _revolutionarySystem.ToggleConvertGivesVision((uid, headRevComp), true);
        }

        //Make All Revs see each other's Rev status
        var rev = AllEntityQuery<RevolutionaryComponent, MindContainerComponent>();
        while (rev.MoveNext(out var uid, out _, out var mc))
        {
            EnsureComp<ShowRevolutionaryIconsComponent>(uid);
            _popup.PopupEntity(Loc.GetString("revolutionaries-open-revolt-rev-popup"), uid, uid, PopupType.LargeCaution);
        }
    }

    private void OnHeadRevDeclareOpenRevolt(EntityUid uid, HeadRevolutionaryComponent comp, DeclareOpenRevoltEvent args)
    {
        DeclareOpenRevolt();
        args.Handled = true;
    }

    private static readonly string[] Outcomes =
    {
        // revs survived and heads survived... how
        "rev-reverse-stalemate",
        // revs won and heads died
        "rev-won",
        // revs lost and heads survived
        "rev-lost",
        // revs lost and heads died
        "rev-stalemate"
    };
}