# GameEQ: project notes

Owner: Tonči (FACEIT nickname wKD). He doesn't code; Claude writes and fixes the code,
he tests and reports bugs. Explain things in plain words, no jargon without a short explanation.

## What it is
GameEQ: pick a game, goals (Footsteps, Gunfight clarity, Less harsh…) and your headset, and get an EQ.
- `index.html`: the whole website (GitHub Pages): game/headset dropdowns with drawn pictures,
  live EQ graph, sound test (generated per-game scenes, Gunfight scene, EQ on/off level-matched,
  compression toggle, play-your-own-clip), "Apply to my headset" button (gameeq:// link).
- `helper/`: GameEQ Helper, a Windows tray app (C#, .NET Framework 4.8). Writes `gameeq.txt` into
  Equalizer APO's config folder and adds `Include: gameeq.txt` to config.txt (admin asked once).
  Handles gameeq:// links from the website. Tray quick presets live in `helper/Preset.cs`.
- `.github/workflows/build-helper.yml`: builds GameEQHelper.exe on GitHub and publishes a Release
  on every change in helper/.

## Data sources
- Headset corrections: AutoEq 10-band FixedBandEQ results (oratory1990, RTINGS, Crinacle).
  Averaged when two labs measured the same model; 31 Hz / 16 kHz capped ±3 dB, others ±6 dB.
- Cloud Alpha 2 Wireless: SoundGuys B&K 5128 data run through AutoEq's algorithm with the RTINGS 5128 target
  (method checked on Arctis Nova 7: within 0.4 dB of AutoEq's official result).
- BlackShark V3 Pro: no measurement yet, so no correction.
- Pictures are original drawings, no game art or product photos (copyright). Own photos can go in images/headsets/.
- Sound test sounds are generated in the browser, not game files.

## Status (4 Nov 2026)
- Repo renamed from cs2-pro-coach to game-eq (4 Nov 2026). Tonči asked to remove the old coach project completely:
  main history was restarted with a fresh first commit. Don't bring the coach code back.
- Public repo, website live at https://hrgacro.github.io/game-eq/ (GitHub Pages, main / root), 4 Nov 2026.
- Helper 1.1 (4 Nov 2026): installs Equalizer APO itself (downloads official installer), detects default playback device
  (Core Audio) and checks APO is active on it (FxProperties CLSID -> EqualizerAPO.dll), opens Device Selector to fix,
  watches device changes, auto-switches presets per game (process names in GameWatcher.cs), per-game presets saved
  in HKCU\Software\GameEQ\Games from website Apply (&g=game) or tray picks during a game.
- First real-PC test (1.0.2): Helper correctly sent Tonči to install Equalizer APO (not installed yet).
  Tonči chose: keep Equalizer APO as the engine, hidden behind the Helper (no own audio driver).
- 5 Nov 2026: Tonči tested on his PC; works but overall sound was much quieter (preamp ~-10 dB). Fix (Helper 1.1.1 + site):
  preamp = -max boost (no extra 1 dB), boosts capped at +6 dB (MAX_BOOST in index.html), site tells user to raise volume.
- 5 Nov 2026, still too quiet. Real cause found: Equalizer APO's default config.txt has demo lines
  ("Preamp: -6 dB" + "Include: example.txt"); preamps add up, so the first test was ~-17 dB at 2-4 kHz.
  Helper 1.1.2 comments those two lines out (tag "# (turned off by GameEQ) ", backup config.txt.before-gameeq,
  restored on uninstall) and warns about other active config lines that stack. EQ range made gentler:
  boosts max +4 dB, cuts max -6 dB (MAX_BOOST / MAX_CUT in index.html), so GameEQ's own drop is at most -4 dB.
- 8 Nov 2026: Tonči: "everything is very quiet, that's not the point". Root issue: with preamp = -max boost, EQ keeps
  footsteps at the same level and makes everything else quieter. Helper 1.2.0: Louder mode (default ON, tray toggle):
  preamp 0, so footsteps get up to +4 dB louder (small crackle risk in loudest moments; fix = lower in-game volume).
  Tray + "Check / fix my headset" show volume reduction (GameEQ vs other Equalizer APO settings). Current preset is
  re-written on start (Apo.ReadCurrent) so the mode applies without re-applying from the site.
- Be upfront with Tonči about limits: EQ can't make sound louder than the headset's max volume.
- Not yet tested with players. Plan: 5–10 players at the venue, EQ on vs off in real CS2.
- Tonči's own headset: HyperX Cloud Alpha 2 Wireless.

## Environment notes for Claude sessions
- Release tag = helper-v + <Version> in helper/GameEQHelper.csproj. Bump the version when the Helper changes.
- No .NET SDK in the cloud workspace; the Helper only compiles on GitHub Actions (windows-latest).
- Website can be tested with Playwright (Chromium is preinstalled).
