## Rev Head

roles-antag-rev-head-name = Head Revolutionary
roles-antag-rev-head-objective = Your mission is to take over the station by converting people to your cause and killing all Command staff on station.

head-rev-role-greeting =
    You are a Head Revolutionary, a Cybersun mind control specialist.
    Convert, kill, or exile all Command personnel from this station.
    Try to keep the station in one piece. You want to rule over more than ashes, right?

    You are trained to brainwash personnel using a standard flash.
    Beware, this won't work on mindshielded personnel, or those with eye protection.

    Viva la revolución!

head-rev-briefing =
    Use flashes to convert people to your cause.
    Convert, kill or exile command personnel.
    You have an uplink granted by your handlers at Cybersun.
    Your uplink code is: {$code}

head-rev-briefing-implant =
    Use flashes to convert people to your cause.
    Convert, kill or exile command personnel.
    You have an uplink granted by your handlers at Cybersun.

head-rev-break-mindshield = The Mindshield neutralized hypnotic powers, but its functionality has been compromised!

## Rev

roles-antag-rev-name = Revolutionary
roles-antag-rev-objective = Your objective is to ensure the safety and follow the orders of the Head Revolutionaries as well as getting rid or converting of all Command staff on station.

rev-break-control = {$name} has remembered their true allegiance!

rev-role-greeting =
    You are a Revolutionary.
    You are tasked with taking over the station and protecting the Head Revolutionaries.
    Get rid of all of or convert the Command staff.
    Viva la revolución!

rev-briefing = Help your head revolutionaries convert or get rid of every head to take over the station.

## General

rev-title = Revolutionaries
rev-description = Revolutionaries are among us.

rev-not-enough-ready-players = Not enough players readied up for the game. There were {$readyPlayersCount} players readied up out of {$minimumPlayers} needed. Can't start a Revolution.
rev-no-one-ready = No players readied up! Can't start a Revolution.
rev-no-heads = There were no Head Revolutionaries to be selected. Can't start a Revolution.

rev-won = The Head Revs survived and successfully seized control of the station.

rev-lost = Command survived and neutralized revolutionary cells.

rev-stalemate = All of the Head Revs and Command died. It's a draw.

rev-reverse-stalemate = Both Command and Head Revs survived.

rev-headrev-count = {$initialCount ->
    [one] There was one Head Revolutionary:
    *[other] There were {$initialCount} Head Revolutionaries:
}

rev-headrev-name-user = [color=#5e9cff]{$name}[/color] ([color=gray]{$username}[/color]) converted {$count} {$count ->
    [one] person
    *[other] people
}

rev-headrev-name = [color=#5e9cff]{$name}[/color] converted {$count} {$count ->
    [one] person
    *[other] people
}

## Deconverted window

rev-deconverted-title = Deconverted!
rev-deconverted-text =
    As the last headrev was neutralized, the revolution is over.

    You are no longer a revolutionary, so be nice.
rev-deconverted-confirm = Confirm
rev-headrev-must-return = The Revolution is leaderless. We must return to the station within a minute!
rev-headrev-returned = A Head Revolutionary has returned to the station, the Revolution continues!
rev-headrev-abandoned = You have disgraced the revolution by abandoning your station. The Revolution is over.
