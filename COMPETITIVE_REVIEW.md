# audsw — Competitive Review & Positioning Report

_Last updated: 2026-06-29_

## 1. What audsw is today (baseline)

A self-contained native `.exe` tray app that **toggles between exactly two playback
devices** on a global hotkey, with a **custom light/dark themed toast**, a Win11-styled
rounded menu, CLI commands (`list` / `set` / `cycle`), autostart, and — importantly — it
sets **both the default *and* default-communications roles together**. Heading to the
Microsoft Store.

## 2. The competitive field

| Competitor | Model | Reach | What it is |
|---|---|---|---|
| **SoundSwitch** (Belphemur) | Free OSS (GPLv2), donate | **~3,100★**, 20+ languages, on Store | Market leader. Playback **+ recording**, per-app auto-switch rules, profiles, mic mute, toast/banner/sound notifications, streamer-safe. Feature-maximalist. |
| **SoundShift** | **Paid, $2.29**, Store | Press darling (Neowin, Windows Central) | Closest to *our* positioning: minimal, **PowerToys-style Win11 UI**, dark mode, fluid animation, **shortcut-pairs to toggle two**, switches **input+output together**, tidy toast. Can't switch device mid-Teams-call. |
| **EarTrumpet** | Free OSS | Very popular | Different niche: **per-app volume mixer + per-app routing**. No hotkey device toggle. Only changes default role (breaks Teams). |
| **josetr/AudioDeviceSwitcher** | Free OSS, Store | ~48★ | UWP Win10 app. Playback+recording, cycle **N devices** via hotkey, plus an "offline" mode that emits CLI commands. |
| **junderw52/AudioSwitcher** | Free OSS | ~1★, v2.1 (Mar 2026) | **Near-identical twin of audsw**: WinForms single-exe, zero deps, toggle two devices, Ctrl+Alt+S, toast, autostart, about dialog. |
| **MS Store "Audio Device Switcher"** | Free, Store | Generic | Hotkey default-device switch. |
| **Windows 11 native** | Free, built-in | Everyone | Taskbar speaker flyout / Win+A → pick device. No hotkey, no toggle, multi-click. |
| Legacy: davkean/audio-switcher, xenolightning AudioSwitcher (the lib we use), NirCmd, SoundVolumeView, AudioDeviceCmdlets | Free | niche | Scripting / older GUIs. |

## 3. Weighted feature matrix

Weights = importance as an **acquisition driver** for the realistic target user
(someone who switches outputs constantly). Scores 0–5.

| Feature | Weight | audsw | SoundShift | SoundSwitch | junderw52 | Win11 native |
|---|--:|--:|--:|--:|--:|--:|
| One-hotkey toggle, zero setup | 5 | 5 | 4 | 3 | 5 | 0 |
| Sets **default + comms** (Teams/Discord follow) | 5 | **5** | 2 | 4 | 2 | 1 |
| Switch **N devices** (not just 2) | 4 | 1 | 3 | 5 | 1 | 4 |
| **Input/mic** switching too | 4 | 0 | 5 | 5 | 0 | 2 |
| CLI / scriptable (StreamDeck, AHK) | 3 | **5** | 0 | 1 | 0 | 0 |
| Per-app / profile automation | 3 | 0 | 1 | 5 | 0 | 0 |
| Polished Win11 UI + themed toast | 3 | 4 | 5 | 3 | 3 | 3 |
| Tiny single self-contained exe | 2 | 5 | 2 | 2 | 5 | — |
| Price (lower = better) | 2 | 5 | 3 | 5 | 5 | 5 |
| **Weighted total** | | **~118** | **~108** | **~133** | **~85** | **~58** |

(Indicative, not precise — the point is the *shape*.)

## 4. The honest read on "simplicity + custom toast"

We were partly right, but it's not enough on its own:

- **The custom toast is table stakes, not a moat.** Every serious competitor (SoundShift,
  SoundSwitch, junderw52) already ships a themed toast on switch. It's a *retention/polish*
  feature, not a reason anyone downloads. Don't market on it.
- **"Simplicity" is already taken — and easily cloned.** SoundShift owns "minimal +
  gorgeous Win11" *and charges for it with press coverage*. junderw52 is a free,
  functionally-identical twin. Simplicity alone gives no defensible wedge against either.

**What is actually defensible and differentiating in our code today:**

1. **We set BOTH default *and* communications roles together.** This is our single
   strongest, most underused asset. It's a real, repeatedly-complained-about pain point —
   EarTrumpet users literally opened threads begging for it, and SoundShift *fails* at it
   during Teams calls. This is the "headphones↔speakers and my Discord/Teams/Zoom follows
   me" promise. **Lead with this.**
2. **CLI + single self-contained native exe.** SoundShift is GUI-only. This makes us the
   choice for StreamDeck / AutoHotkey / portable-app / scripting power users. Niche but
   loyal, with zero competition at the modern/polished end.

## 5. Recommendation

**Don't fight for the mass casual market** — Windows 11's flyout + SoundSwitch already own
it. **Pick the wedge audience:** power users, gamers, streamers, and remote-meeting-heavy
people who toggle outputs many times a day *and* get burned by apps not following the
switch.

**Positioning line:** _"One hotkey. Your headphones and speakers swap — and Teams, Discord
and Zoom follow. Single tiny exe, scriptable, no setup."_

**On simple vs. expand — take the middle path.** Keep the core UX dead-simple, but close
the two gaps that cost us *target* users (not casuals), because they're cheap given we
already hold a `CoreAudioController`:

- **Add input/mic toggle** (weight 4, we score 0). SoundShift and SoundSwitch both do
  in+out; it's the #1 thing we're missing vs. them.
- **Allow cycling N devices, not just 2** (weight 4, we score 1). Lots of people have
  speakers + headset + HDMI TV. The hardcoded pair is our most visible functional
  limitation.
- _(Optional)_ per-device "jump straight to device X" hotkeys, like SoundShift/SoundSwitch
  — more flexible than toggle-only.

Skip per-app profiles/automation — that's SoundSwitch's territory and chasing it dilutes
our "simple" identity.

**Store strategy:** SoundShift proves people *pay $2.29* for a minimal, pretty switcher. We
can undercut on price *or* match on polish while beating it on the dual-role-switch + CLI.
That's a credible launch story; "simplest + prettiest toast" is not.

## Sources

- SoundSwitch — https://soundswitch.aaflalo.me/
- SoundSwitch GitHub — https://github.com/Belphemur/SoundSwitch
- SoundShift on Store — https://apps.microsoft.com/detail/9mx3m6xs4s81
- Neowin on SoundShift — https://www.neowin.net/news/this-small-windows-app-is-a-great-power-toy-for-switching-audio-devices/
- junderw52/AudioSwitcher — https://github.com/junderw52/AudioSwitcher
- josetr/AudioDeviceSwitcher — https://github.com/josetr/AudioDeviceSwitcher
- EarTrumpet default+comms discussion — https://github.com/File-New-Project/EarTrumpet/discussions/682
- MS Store Audio Device Switcher — https://apps.microsoft.com/detail/9n71nh5h6t7k
- Win11 taskbar switching — https://www.howtogeek.com/739682/how-to-switch-audio-devices-from-windows-11s-taskbar/
