# EntityFix for CounterStrikeSharp
Fixes game_player_equip, game_ui, IgniteLifeTime. Idea taken from [cs2fixes](https://github.com/Source2ZE/CS2Fixes)

## Features:
1. Fixes entity game_player_equip. The plugin processes outputs: Use, TriggerForActivatedPlayer, TriggerForAllPlayers
2. Adds a game_ui analogue. [From cs2fixes](https://github.com/Source2ZE/CS2Fixes/pull/216) + Use, Reload and Look
3. Handles entity burning with IgniteLifeTime
4. You can customize particle, time and burning damage -> `string g_IgnitePath`, `float g_VelocityIgnite`, `int g_DamageIgnite` and recompile plugin

## Installation:
1. Compile or copy CS2-EntityFix to `counterstrikesharp/plugins/CS2-EntityFix` folger
2. Copy CS2-EntityFix.json to `counterstrikesharp/gamedata` folger
3. Restart server