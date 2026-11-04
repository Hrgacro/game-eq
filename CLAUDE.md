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
- Repo renamed from cs2-pro-coach to game-eq (done 4 Nov 2026); the old coach code is kept in branch `archive/cs2-pro-coach`.
- Helper compiles on GitHub Actions (first run 4 Nov 2026 succeeded, Release helper-v1.0.1, 25 KB exe). Not yet run on a real PC.
- Not yet tested with players. Plan: 5–10 players at the venue, EQ on vs off in real CS2.
- Tonči's own headset: HyperX Cloud Alpha 2 Wireless.

## Environment notes for Claude sessions
- No .NET SDK in the cloud workspace; the Helper only compiles on GitHub Actions (windows-latest).
- Website can be tested with Playwright (Chromium is preinstalled).
