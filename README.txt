DEAD SHIP (RimWorld 1.6)

INSTALL
1. Put the DeadShip folder inside your RimWorld Mods folder (Steam: ...\steamapps\common\RimWorld\Mods).
2. Build the code (below), start RimWorld and enable "Dead Ship" in the Mods menu, restart.
3. New colony -> choose the scenario "Dead Ship: Crash Site".
If something breaks, press ~ for the dev log (or open Player.log) and send the red errors.

BUILDING THE CODE (once, and again whenever a .cs file changes)
- Double-click Source\build.bat. It uses the C# compiler built into Windows and writes Assemblies\DeadShip.dll plus build.log.
- If Combat Extended is in your Steam Workshop folder it prints "Building with Combat Extended support" and builds the CE code in.
- Do this BEFORE starting RimWorld. The old Source\Mineable_SealedSection.cs is an empty stub and can be deleted.

COMPATIBILITY
- Needs no DLC and not Anomaly. Works with or without Royalty, Ideology and Biotech.
- Combat Extended: supported. The puker's acid spit, the lurker's barbs, the exploder's blast and the plasma cutter are real CE projectiles or explosions, necromorphs get CE melee tools, armour and stats, and the plasma cutter and pulse rifle have magazines and their own ammo (Patches\CE_*.xml, applied only when CE is loaded). Without CE they use built-in projectile pictures, hit rolls and vanilla stats.

WHAT IS IN IT
THE MARKER (in pieces)
- Sealed ship section: hull wreckage that cannot be mined. Colonists must attack it to break it open. It drops a Marker fragment, and 35% of the time a lurker was inside.
- Marker fragment: a small item. It does nothing alone and is never used up.
- Resonance altar (Power menu): holds up to four fragments. Each one adds 4,000 W, deepens colonist fear, spreads it further and draws bigger raids. Eject them and it stops.
- Marker study bench (Production menu) and the Marker Studies research tab: the bench only works with a fragment loaded. The tab reads left to right in four rows:
    Weapons: Plasma Cutting (needs Gunsmithing) -> Pulse Rifle.
    Power: Marker Assembly -> Resonance Tuning -> Harmonic Focusing -> Carrier Wave Amplification.
    Damping: Containment -> Signal Attenuation -> Neural Shielding -> Cognitive Isolation.
    Memory Suppression (needs Drug Production) sits under Neural Shielding, which needs it.
    Knowledge: Necromorph Anatomy -> Weak Point Mapping (a hit from a colonist with any weapon goes for a limb or weak point 15% then 30% more often).
    Detection: Signal Triangulation (wreck sites are 3-10 tiles away instead of 6-18) -> Early Warning (a stirring body takes 60% longer to rise).
    Gear: Hollow Points (hollow-point ammo: a soft-cased pulse round for the rifle, and a burn cartridge for the plasma cutter, made for lighter materials. Both are much harder against flesh and weak against armour; Gunsmithing only), Hardened Plasma Cells (a harder-hitting, armour-piercing second cell for the cutter) and Pulse Penetrators (an armour-piercing second round for the pulse rifle), side by side, each needing only Gunsmithing. The extra ammo is Combat Extended only.
    Engineering Suit (Machining): a rig and a helmet made at the machining table. Good armour, and each piece you wear cuts the Marker's sanity drain by 20% (40% for both).
    Defences: Bulkhead Doors (a powered door with 450 HP; build it from containment alloy to seal a Marker room).
- Resonance chain: every fragment gives 25% / 50% / 100% more power (x2 at the end), but each step also makes the dread about 8% worse.
- Damping chain: each step cuts the dread, its reach and the raid growth (x0.85, x0.8, x0.75). It can never be silenced: dread never falls below 40% of normal.
- Marker scaffold (Power menu, needs Marker Assembly): load sixteen fragments one at a time. It works from the first: every fragment adds 4,000 W (more with Resonance research), a little more dread everywhere on the map (plus a mood hit that steps up at 1, 4, 8, 12 and 16 fragments), a wider fear radius (+10% per fragment) and bigger raids (+5% per fragment). The sixteenth turns it into the full Marker: 64,000 W and no off switch.
- Seeing necromorphs: a colonist with a necromorph in sight (24 cells) gets a mood penalty that grows with their number: -3 for one, -6 for 2-3, -10 for 4-7, -14 for 8 or more (a brute counts as three).
- Fear: a mood penalty that grows nearer an active altar or Marker (radii 6 / 12 / 20 / 30 cells), plus a colony-wide dread that steps up with the total fragments loaded (1, 4, 8, 12, 16).
- Containment alloy (Containment research): smelted at a fueled or electric smithy from 20 steel + 1 component for 20 alloy. Build the walls and doors of a closed room entirely from it (any wall or door, alloy as the material) and a Marker inside counts as about 35% of its fragments for dread range and strength. One wall or door of another material, or a room open to the map edge, breaks it.
- Memory suppressant (Memory Suppression research, made at a drug lab from 1 industrial medicine + 2 chemfuel): lasts about a day, cuts the Marker's nearby fear by one step and the colony dread by two, at a small Consciousness and Manipulation cost.

NECROMORPHS
- The roster unlocks by days since the first fragment was loaded: slasher, pregnant and swarmer from day 0; pack, lurker and puker from day 10; exploder and leaper from day 15; brute from day 30. Raids, outbreaks and wrecks only use what is unlocked (the sealed ship sections' lurker is a one-off and ignores it).
- Hidden, permanently hostile faction with raids that only fire while an active altar or Marker is on the map. Raid size grows with fragments loaded (+5% each, reduced by Damping research). Raid mix: slasher 10, pack 8, lurker 6, exploder 5, leaper 5, puker 4, pregnant 3, brute 2. Raiders assault at once (no staging or siege).
- Slasher: bladed arms; dies once enough limbs are cut away.
- Puker: spits acid at range (eats armour, leaves a cleanable puddle), 
- Lurker: barb volleys and tentacle melee; ambushes from cover; dies when all 3 tentacles are cut off.
- Exploder: slow (3.0), walks at colonists and explodes (a Combat Extended explosion under CE).
- Leaper: two arms and a tail, no legs. Leaps 2.5-5.5 cells onto a target (needs its tail, 7 second cooldown) and pins them, clawing and biting while the victim is held. Fast and hits hard. Dies when 3 of arms, tail and head are lost.
- Pregnant: slow and tough, carries a brood sac. When it dies, by any cause, a swarm bursts out. Dies if its head or sac is destroyed, or both arms.
- Pack: a child-sized necromorph, fast (6.2) and light, clawing at close range. Dies when 2 of its arms and head are lost.
- Brute: huge (400 HP torso), slow (3.4), heavy armour and hard fists and spikes. Its only soft spots are two glands on its back (and the head): destroy both glands or the head to kill it. Rare.
- Swarm: one pawn made of many small bodies, weak but fast, with a single "swarm" body part.
- Slashers, lurkers and pukers finish off downed colonists.
- No necromorph feels pain or bleeds. Limbs are weak, and with no legs a necromorph crawls on its arms and keeps fighting.
- Necromorphs always know where the colonists are, even through walls. Exploders and brutes break walls and doors (only those) to reach them. An exploder runs up to the wall or door and blows itself up, but only picks walls that are part of a larger structure (joined to 12 or more wall and door cells), so a lone decoy wall is ignored. Every other necromorph never attacks buildings: it walks to the nearest reachable colonist, or as near as it can get, so a sealed base keeps them outside (but not the vents).
- A creature does one attack at a time: it will not spit or fire while in melee.

OUTBREAKS (the dead rise)
- Dead bodies near an active Marker or altar, inside its dread range, will rise as necromorphs after 3,000-8,000 ticks, with a warning message first. Containment shrinks the range a lot but does not stop it. Children become Packs, infants and most animals Lurkers, adults Slashers, Pukers, Exploders or Leapers. Bodies of necromorphs, dessicated bodies and big animals do not rise.
- If the last active Marker or altar is removed, every necromorph on the map dissolves.

SANITY AND THE MARKER'S CALL
- Sanity is a new need. Nearby necromorphs, the Marker's dread and (slightly) the colony-wide dread drain it. It recovers with sleep, recreation and time away from dread, and drains more slowly asleep or on suppressants.
- Low sanity gives mood penalties (mind fraying, seeing things, the signal is in my head) and at the bottom can cause psychotic wandering, berserk, or the Marker's call: the colonist walks to the nearest Marker and cuts at their own limbs until hurt or stopped.
- Attunement: spending time near a Marker (and not afraid of it) gives the Attuned buff, from a faint colony-wide +2% work, +5% research up to +30% work and +70% research at the consuming stage. A faint version applies colony-wide whenever any fragment is loaded. The cost is sanity and a rising raid risk.

WEAPONS
- Plasma cutter (machining table, needs the Plasma Cutting research): short-range cut plus burn, aims for limbs more often so two shots take a limb off. Under CE it has a 20-round magazine and plasma cells (also crafted at the machining table).
- Pulse rifle (Pulse Rifle research): military grade, built to punch through spacer armour. Weak per shot against necromorphs but good against human raiders. Single, burst and full-auto modes with a 30-round magazine and its own pulse rounds under CE (crafted at the machining table).

VENTILATION (Ventilation research; Temperature menu)
- Air duct: a buried duct, 1 steel. Ducts, vents and intake grilles that touch (up/down/left/right) form one network to an air handler.
- Air handler (2x2, powered): holds every vented room at its target temperature (Cooler / Warmer buttons, 1 degree a click, 5-35C) and uses more power the harder it works.
- Air vent: a floor grate (necromorphs that crawl out of it breach it; below half health it stops working until repaired, but raiders can still use it). A room with a vent on the network is vented: +5% work speed, +2 mood ("conditioned air") and a little faster sanity recovery.
- Air intake grille: must be outdoors (not roofed). With one on the network, vented rooms get fresh air instead: +8% work speed and +5 mood.
- Air additives (Air Additives -> Cognitive Aerosols -> Performance Aerosols research, side by side with Ventilation on the tab): the air handler gets an Additive button that opens a drop-down list of the chemicals you have researched. One at a time, pumped into every vented room, costs 240 W more, and colonists in those rooms get the effect while they stay (they get it the moment they step into the room and lose it the moment they leave). Wakeful air: needs 40% less sleep. Calming air: sanity recovers faster. Nootropic air: +15% Consciousness. Neuromotor air: +15% Manipulation. Sociable air: better social skill. Adrenal air: +15% work speed. Euphoric air: +8 mood.
- The price: when a necromorph raid fires and the network has an intake grille and a vent, some of the raiders (1 + fragments/4, or 3 + fragments/4 with an intake open to the sky, at most half the raid, never a brute) don't charge the base. They run to the nearest air intake, crawl in, and come out of a random vent inside the base. A necromorph that can't reach any colonist because they are walled in does the same. They have to reach the intake first, so guard or wall in your intakes. The vent they use is breached (it stops working until repaired).

WRECK SIGNALS (fragment supply)
- Once a Marker study bench exists, a signal can appear and put a ship wreck on the world map 6-18 tiles away for 14-20 days. Send a caravan and use "Enter wreck" (the button on the caravan; right-click is unreliable).
- The wreck is a sealed 3x2 ship section with one outer door. Rooms hold sleeping necromorphs (any raid type, a pregnant in the core room) which wake together per room. The core room has 3-4 fragments, other rooms a 50% chance of one. Wrecks are rare; a new signal needs the last to be gone.

CRASH SITE SCENARIO
- Three colonists by drop pod, supplies, two plasma cutters, and three sealed ship sections (one near the start, two across the map), so three fragments.

KNOWN GAPS
- Spit, barb, blast, melee, armour, limb health and cutter numbers are first guesses to tune by play.
- Acid puddle uses a recoloured vanilla texture. The study bench and Marker stage art are first versions.
- The exploder, raid tactics, study bench, Marker scaffold, weapons, Pack, Brute, outbreaks, sanity and the research chains are new and lightly tested.
- Wreck balance and the east/west facing of some necromorph sprites are lightly tested.
- Not done yet: a venting system, more Marker Studies projects, other weapons.

Dead zone: necromorphs are drawn to an active altar or Marker but cannot stand within 6 cells of it (DeadZone.Radius in Source/DeadZone.cs). With nothing else to hunt they gather at the edge of the zone; a colonist inside the zone is waited for at its edge, and any necromorph that ends up inside is driven out. Ranged necromorphs can still shoot in.

Debug: with dev mode on, the debug actions menu has a 'Dead Ship' tab with Clock +5/+10/+30 days and Clock reset, for testing the necromorph unlock days.

Start scenarios: 'Dead Ship: Crash Site' (3 survivors by drop pod, steel, medicine, 2 cutters, 3 sealed sections) and 'Dead Ship: Salvage Crew' (3 engineers arriving on foot, each with an engineering rig and helmet, a plasma cutter and a pulse rifle, 400 steel and 14 components, but only 3 medicine and 12 meals; one sealed section beside the crew and two further out). Under Combat Extended, craft ammo once its research is done.

Unitology: a preset ideoligion (Collectivist, Transhumanist, Proselytizer), shown on the ideoligion screen at game start when Ideology is installed. It is built only from the base game's memes for now, with no Marker-specific precepts or rituals yet. Without Ideology it does not load.

Salvage Crew now starts with every colonist already wearing the engineering rig and helmet (ScenPart_DressStartingPawns). Under Combat Extended it also starts with 120 plasma cells and 180 pulse rounds.

Psychic traits: the base game's psychically deaf and psychically hypersensitive traits now matter. Deaf colonists are not affected by the Marker at all (no dread or strain thoughts, no Marker sanity drain, no attunement, no Marker's call; necromorphs still frighten them). Hypersensitive colonists get double the Marker's effects: double mood penalties, double sanity drain, and a second copy of the attunement so its work and research gifts are doubled too. Other psychic traits are unchanged.

The Marker's embrace: a colonist whose sanity stays below 10% for about two days in total (time above 10% wears the count down at a third of the speed; breaks do not reset it) is claimed by the Marker. A warning message comes at one day. They then walk to the nearest active Marker and die there, and it cannot wear off while a Marker is active. Psychically deaf colonists are immune. Hypersensitive colonists reach it sooner because their sanity drains twice as fast. Dev tip: the sanity bar can be set to 0 in the dev tools, and the count is saved with the game.
