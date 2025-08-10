# EntityFix for CounterStrikeSharp
Fixes game_player_equip, game_ui, IgniteLifeTime, point_viewcontrol, trigger_gravity. Idea taken from [cs2fixes](https://github.com/Source2ZE/CS2Fixes)

## Features:
1. Fixes entity game_player_equip. The plugin processes outputs: Use, TriggerForActivatedPlayer, TriggerForAllPlayers
2. Adds a game_ui analogue. [From cs2fixes](https://github.com/Source2ZE/CS2Fixes/pull/216) + Use, Reload and Look
3. Handles entity burning with IgniteLifeTime
4. You can customize particle, time and burning damage -> `config.json`
5. Adds a point_viewcontrol analog from cs:go [Cs2Fixes Wiki] (https://github.com/Source2ZE/CS2Fixes/wiki/Custom-Mapping-Features#point_viewcontrol-entity-implementation)
6. Adds fix for TestActivator null activator crash [cs2Fixes commit] (https://github.com/Source2ZE/CS2Fixes/commit/eadd9ebfbad5ea8694a33ad4c46d53ee422babfe)
7. Fixes trigger_gravity. After the update 2025-07-29 gravity is not applied (Need configure with CS2-ParseGravity and install CS2-HammerIDFix) [cs2Fixes commit] (https://github.com/Source2ZE/CS2Fixes/commit/1d58cf96ab486f9736906e377a9b5d57537d1882)

## Required packages:
1. [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp/) (Min version: 330)

## Admin's commands
Admin Command | Privilege | Description
--- | --- | ---
`css_entityfix_reload` | `@css/root` | Reload config file of EntityFix

## Installation:
1. Compile or copy CS2-EntityFix to `counterstrikesharp/plugins/CS2-EntityFix` folger
2. Copy CS2-EntityFix.json to `counterstrikesharp/gamedata` folger
3. Copy and configure config.json to `counterstrikesharp/plugins/CS2-EntityFix` folger
4. Install [CS2-HammerIDFix] (https://github.com/darkerz7/CS2-HammerIDFix) or analog
5. Create map configs using CS2-ParseGravity
6. Restart server