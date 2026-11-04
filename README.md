# GameEQ

Click what you want to hear and get an EQ for any headset, applied with one click.

**Website:** https://hrgacro.github.io/game-eq/
**Windows Helper:** [Download GameEQHelper.exe](https://github.com/Hrgacro/game-eq/releases/latest/download/GameEQHelper.exe)

## What it does
Pick a **game** (CS2, Valorant, PUBG, Fortnite, Apex, CoD, LoL, Rocket League, EA FC…), click **goals** (Footsteps, Gunfight clarity, Richer sound, Less harsh, Voice clarity, Bass boost, Night mode) and choose your **headset** (top 10 pro headsets plus generic options). Then:

- **Apply to my headset**: one click with GameEQ Helper (Windows)
- or type the 10-band table into your headset app (G HUB, Synapse, NGENUITY, SteelSeries GG, iCUE)
- or download a file for [Equalizer APO](https://sourceforge.net/projects/equalizerapo/)

It works at the Windows audio level and never touches game files, so it's safe with VAC, Vanguard and FACEIT.

## GameEQ Helper (Windows)
A small tray app (green bars icon next to the clock).

1. Download and run `GameEQHelper.exe`. It isn't code-signed yet, so Windows may say *"Windows protected your PC"*. Click **More info → Run anyway**.
2. It needs Equalizer APO (free, open source). If it's missing, the Helper opens the download page. Install it, tick your headset, restart the PC, and run the Helper again.
3. Allow the **one-time** admin prompt.
4. Now **Apply to my headset** on the website works instantly. Right-click the tray icon for quick presets, "Start with Windows", or **Restore original sound & uninstall**.

### Exactly what the Helper changes
- Creates `gameeq.txt` in the Equalizer APO config folder and lets your user account edit that one file.
- Adds one line, `Include: gameeq.txt`, to Equalizer APO's `config.txt`.
- Per-user registry: the `gameeq://` link handler, its own settings (`HKCU\Software\GameEQ`), and optionally "Start with Windows".

Nothing else. *Restore original sound & uninstall* removes all of it. Links from the website are checked and limited (each band ±12 dB, preamp can only lower volume).

## Project layout
- `index.html`: the whole website (GitHub Pages)
- `helper/`: the Windows Helper (C#, .NET Framework 4.8, already built into Windows 10/11)
- `.github/workflows/build-helper.yml`: GitHub builds the .exe and publishes a new Release automatically whenever `helper/` changes

## Sound test
Each game has a short scene with sounds generated in the browser (Web Audio): footsteps walking around you in 3D (HRTF), reloads, gunshots, the headshot sound, bomb beeps, vehicles, building, ball hits, crowd and so on. A radar shows where each sound comes from. A **Gunfight** scene (shooters) has an enemy sneaking up from behind during a firefight, to test footsteps masked by gunshots, with an optional compression toggle (like Windows Loudness Equalization). **EQ on / EQ off** switches between the EQ and the original sound at matched loudness. **Use your own clip** plays a local recording of your own gameplay through the EQ; nothing is uploaded. The sounds are generated, not taken from any game, so there are no copyrighted game files in the repo.

## Pictures
The game and headset pictures are simple illustrations drawn in code. They aren't official logos, cover art or product photos, which belong to the game studios and brands. To use your **own photos** (for example, ones you take of the headsets at your venue), put them in `images/headsets/` and add `photo:"images/headsets/<key>.jpg"` to that headset in `HEADSETS`.

## Add or tweak presets
All presets are plain numbers at the top of the `<script>` in `index.html`:
- `GAMES`: the base curve per game; `GAME_META`: genre, picture, what to listen for, and recommended goals
- `GOALS`: added on top when clicked
- `HEADSETS`: a measured correction per headset model (see below), plus generic balanced, bright and bassy options.

Each curve is 10 numbers in dB for 31, 62, 125, 250, 500, 1k, 2k, 4k, 8k and 16k Hz. The tray quick presets are in `helper/Preset.cs`. Pull requests with better tunings are welcome.

## Where the headset corrections come from
Each headset correction is the 10-band result from [AutoEq](https://github.com/jaakkopasanen/AutoEq) (MIT). AutoEq calculates it from lab measurements by **oratory1990**, **RTINGS** and **Crinacle**, aiming at the Harman reference target.
- When two labs measured the same model, their results are averaged (Cloud II, Cloud Alpha, G PRO X).
- 31 Hz and 16 kHz are capped at ±3 dB, because those bands depend mostly on fit and seal. Every other band is capped at ±6 dB.
- Users can choose Full, Half or Off correction.
- HyperX Cloud Alpha 2 Wireless: SoundGuys' B&K 5128 measurement, run through AutoEq's own algorithm with the same 5128 target AutoEq uses for RTINGS data (checked against the Arctis Nova 7, where it matches AutoEq's official result within 0.4 dB).
- The Razer BlackShark V3 Pro has no lab measurement in AutoEq yet, so it gets no correction until one is published.

Measurements assume the headset's own software EQ is flat and virtual surround is off.

## License
MIT
