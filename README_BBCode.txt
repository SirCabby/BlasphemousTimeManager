[size=6][b]Blasphemous Time Manager[/b][/size]

A lightweight mod that lets you [b]see, freeze, and set[/b] your total in-game play time — the value normally only visible on the save-slot screen.

[size=5][b]Features[/b][/size]
[list]
[*][b]Live overlay[/b] of your play time ([i]HH:MM:SS[/i]) in-game.
[*][b]Freeze[/b] the timer so your play time stops counting.
[*][b]Set[/b] your play time to any value you type — the change is permanent once you save.
[/list]

[size=5][b]Requirements[/b][/size]
[list]
[*]Blasphemous
[*]BepInEx 5.4.x (x64 / Mono) — installed in Step 1 below.
[/list]

[size=5][b]Step 1 — Install BepInEx (the mod loader)[/b][/size]
[list=1]
[*]Open the [url=https://github.com/BepInEx/BepInEx/releases]BepInEx releases page[/url] and download the latest [b]BepInEx 5.4.x[/b] for [b]x64 Windows[/b] — the file is named like [i]BepInEx_win_x64_5.4.x.zip[/i]. [b]Do not[/b] use the 6.x (BepInEx 6 / IL2CPP) builds.
[*]Find your game folder: in Steam, right-click [b]Blasphemous[/b] → [b]Manage[/b] → [b]Browse local files[/b]. It looks like:
[code]C:\Program Files (x86)\Steam\steamapps\common\Blasphemous[/code]
[*]Extract the contents of the zip [b]directly into that folder[/b] (next to [i]Blasphemous.exe[/i]). You should now see a [i]BepInEx[/i] folder, plus [i]winhttp.dll[/i] and [i]doorstop_config.ini[/i].
[*]Launch the game once, then quit. This first run creates the [i]BepInEx\plugins[/i] and [i]BepInEx\config[/i] folders.
[/list]

[size=5][b]Step 2 — Install this mod[/b][/size]
[list=1]
[*]Drop [b]BlasTimeManager.dll[/b] into the plugins folder:
[code]...\Blasphemous\BepInEx\plugins\[/code]
[*]Launch the game and load a save. You'll see [b]Play time  HH:MM:SS[/b] in the top-left corner.
[/list]

[size=5][b]Controls[/b][/size]
[list]
[*][b]F7[/b] — show / hide the overlay.
[*][b]F8[/b] — freeze / unfreeze the timer (overlay turns cyan and shows [FROZEN]).
[*][b]F9[/b] — open the set-time box. Type [i]HH:MM:SS[/i] (e.g. [i]02:30:00[/i]) or plain seconds (e.g. [i]9000[/i]). [b]Enter[/b] = apply, [b]Esc[/b] = cancel.
[/list]
[i]After setting a value, rest at a Prie Dieu (which saves) to make it permanent.[/i]

[size=5][b]Changing the keybinds[/b][/size]
Edit this file (created after the first run with the mod installed) in any text editor:
[code]...\Blasphemous\BepInEx\config\local.blasphemous.timemanager.cfg[/code]

[size=5][b]Uninstall[/b][/size]
Delete [b]BlasTimeManager.dll[/b] from [i]BepInEx\plugins[/i]. To remove the loader as well, delete the BepInEx files you added in Step 1.

[size=5][b]Troubleshooting[/b][/size]
[list]
[*][b]No overlay?[/b] It only appears while you're actually in a level — not on the main menu or during loading. Press [b]F7[/b] to make sure it's enabled.
[*][b]Nothing happens at all?[/b] Double-check you used BepInEx [b]5.4.x x64 (Mono)[/b], extracted it into the game folder (not a subfolder), and ran the game once before adding the DLL.
[/list]
