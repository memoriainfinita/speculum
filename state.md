# Speculum — project state

## Current state
**Working and in use.** Built, tested (clean compile, no exceptions on startup, real click
tests against the window) and used daily by its owner.

Code review of 2026-08-28: fixed an indefinite hang while reading process output, the
missing warning when scrcpy is not installed, and the assumption that the phone's WiFi
interface is called `wlan0`. Also fixed that child processes inherited the launcher's
working directory, which left the adb server holding the folder of the `.exe` and blocking
any attempt to move or rename it. See TODO and Design decisions.

Interface translated to English on 2026-08-28, source code included. Repo still to be made
public.

Review environment: scrcpy **4.1**, installed with `winget install Genymobile.scrcpy` on
2026-08-28 (it was not on the machine), and a <test-phone> (<codename>, Android 15) over
USB.

Verified against that real scrcpy 4.1, not against documentation: the 20 flags added do
exist, and the 30 long and 12 short flags the app already used are still valid — none has
been renamed or withdrawn.

## What it is
A Windows desktop tool that wraps [scrcpy](https://github.com/Genymobile/scrcpy) (Android
mirroring and control over USB/WiFi) in a graphical interface, so there is no need to use
the terminal or to remember command line flags.

It exists because scrcpy was installed with `winget` (package `Genymobile.scrcpy`) and,
while it works perfectly from the command line, there is no comfortable way to launch it,
stop it, record, or connect over WiFi without typing commands every time.

The name: `speculum` is Latin for mirror, which is literally what the tool does. It follows
the <workspace> convention of naming projects with a real Latin noun.

## File layout (this folder)
- `Speculum.cs` — C# source (WinForms). A single file, with no external dependencies
  beyond .NET Framework (which already ships with Windows).
- `Speculum.exe` — the finished program, ready to run. Double click and that is it. Not
  versioned: it is generated from the `.cs` and published attached to each release.
- `README.md`, `LICENSE` (GPLv3), `.gitignore` and `state.md` (this file).
- `speculum.ico` — the app icon: an antique oval mirror, four sizes.
- `tools/make-icon.ps1` — generates the `.ico`. Requires Windows PowerShell 5.1.
- `docs/` — screenshots of the four tabs and `icon.png`, used by the README.
- `.archive/` — outside the repo: earlier versions of the source and the two console menus
  (`scrcpy-menu.bat`, `scrcpy-menu-completo.bat`) that preceded the graphical interface.
- `Recordings/` — created by the app next to the `.exe` when recording. Outside the repo.

## How to build the .exe from the .cs
Needs neither Visual Studio nor the .NET SDK. It uses the C# compiler that already ships
with Windows (`csc.exe`, part of .NET Framework):

```
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:winexe /win32icon:speculum.ico /out:Speculum.exe /reference:System.Windows.Forms.dll /reference:System.Drawing.dll Speculum.cs
```

(PowerShell's `Add-Type -OutputType WindowsApplication` does **not** work for this in
PowerShell 7/pwsh — only in classic Windows PowerShell 5.1. Hence `csc.exe` directly.)

Note for scripted builds: run it from PowerShell, not from Git Bash. Git Bash rewrites
`/nologo` into a Windows path and the compiler fails with `CS2001: source file
'C:/Program Files/Git/nologo' could not be found`.

## What the interface does
- **Basic tab**: launch mode (normal / fullscreen / borderless for OBS / read-only),
  USB/WiFi selector, automatic WiFi pairing button, show touches, keep screen awake, record
  the session with an automatic file name (date/time) in a `Recordings` folder next to the
  `.exe`.
- **Advanced tab**: checkboxes and dropdowns for practically every scrcpy option, grouped
  by category (Video, Audio, Control, Other) — with no flag to type by hand. Every free
  text field has a visible example inside it (e.g. "8M") and a tooltip with the full
  explanation on hover. A free text field at the end covers any flag not represented in the
  interface.
- **Management**: start/stop scrcpy, restart or stop the adb server separately, see the
  status of connected devices, informational tools (version, list cameras, encoders,
  displays, installed apps).

## Design decisions (why it is built this way)
- **WinForms + csc.exe instead of a `.bat`**: the user asked for something "more
  comfortable" than a console menu — no black window flashing, with selectable controls
  instead of having to remember flag syntax.
- **Advanced tab with real controls (not just free text)**: the first version had a single
  text box for "advanced flags". The user explicitly asked for it to be selectable like the
  rest, without having to memorise options — hence the checkboxes/dropdowns by category.
- **Examples + tooltips on the text fields**: it was still unclear what format each field
  expected (bitrate, crop, etc.). A cue banner was added (the native Windows placeholder
  via `EM_SETCUEBANNER`) plus a `ToolTip` with the long explanation.
- **"Stop ADB" button separate from "Restart ADB"**: the user asked whether there was a way
  to stop adb without restarting it. Important: adb restarts itself as soon as any action
  touches it again (standard behaviour of the tool, not fixable from the app).
- **"Low-latency WiFi" preset**: it fills in the bitrate/size/fps fields on the Advanced tab
  instead of applying hidden flags, so that there are no duplicates and no invisible magic —
  everything sent to scrcpy is visible and editable in the fields.
- **Working directory of the child processes set to `%TEMP%`**: without `WorkingDirectory`,
  the child inherits the launcher's cwd, and the adb server keeps an open handle on that
  folder for as long as it lives. In a portable app that pins the folder of the `.exe`
  itself: the user cannot move it, rename it or eject the USB stick, with no hint as to
  why. `WorkingDirectory = Path.GetTempPath()` is set on both `ProcessStartInfo`. No path
  in the app depends on the cwd: `Recordings` is resolved from
  `AppDomain.CurrentDomain.BaseDirectory`.
- **The adb server is only stopped on exit if the app started it**: adb is a single
  machine-wide daemon and may be in use by Android Studio, another terminal or another
  launcher window, so killing it unconditionally would be shutting down something that is
  not ours. `adbWasAlreadyRunning` is computed in the form's `Load`, before
  `RefreshStatus()` (which queries adb and would therefore start it), and `FormClosing`
  runs `kill-server` only if the app started it and no scrcpy is left open. Verified on
  both branches: with adb stopped to begin with, it is stopped on close; with adb already
  running, it survives.
- **Icon generated by script, not drawn by hand**: `tools/make-icon.ps1` builds the `.ico`
  with `System.Drawing`, with no external dependencies, just as the `.exe` is built with
  `csc.exe` alone. The icon stays reproducible and editable instead of being an opaque
  binary. The script needs Windows PowerShell 5.1: `System.Drawing` is not in pwsh 7.
- **A different drawing per size inside the `.ico`**: the beading of the frame and the
  gradients only show at 48 and 256 px; at 32 only the gradients remain; at 16 only the
  silhouette, with the oval fattened to eat the margins and no inner outline, which at that
  size eats the ring from both sides. An `.ico` is a container of images, not one scaled
  image, and that is what keeps the ornament from turning into grey mud in the taskbar.
  The 16, 32 and 48 entries as bitmaps and the 256 one as PNG (Vista format): the 256x256
  PNG entry was checked to survive intact inside the `.exe`.
- **The icon is embedded with `/win32icon` and the window extracts it from the `.exe`
  itself** (`Icon.ExtractAssociatedIcon`), rather than embedding it a second time as a
  resource. A single source for Explorer, the taskbar and the title bar.
- **The whole source in English, not only the visible strings**: the interface, the
  identifiers and the comments. The repo is meant to go public under GPLv3, and someone
  arriving at it reads the code, not just the window. Leaving Spanish method names with
  English strings would have been the worst of both.
- **The old `Grabaciones` folder is renamed on startup, not abandoned**: the app is
  portable and the folder lives next to the `.exe`, so an installation that predates the
  translation already has recorded sessions in it. `MigrateLegacyRecordingsFolder()`
  renames it to `Recordings` at `Load` if `Recordings` does not exist yet, and says so in
  the log; if the rename fails it says where the old sessions are instead of losing them
  silently.

## Relevant debugging context (in case it comes back)
- There was a WiFi connection problem that was **not scrcpy's**: Tailscale had installed a
  route capturing the traffic towards the local network (`<lan-subnet>`) and sending it
  through the VPN tunnel instead of out over direct WiFi. If WiFi pairing fails with a
  timeout even though the phone and the PC are on the same network, check `Get-NetRoute` in
  case Tailscale (or another VPN) is hijacking that subnet.
- A weak WiFi signal (RSSI below -75/-80 dBm) causes audio/video dropouts even with the
  connection established — that is not a bug, it is radio physics. The 2.4GHz network
  usually reaches further than the 5GHz one at the same distance.
- The scrcpy install has no shim in `%LOCALAPPDATA%\Microsoft\WinGet\Links`: winget adds
  the package folder itself to the user PATH. A shell whose environment block predates the
  install therefore does not see `scrcpy.exe`, and the app correctly reports "NOT
  INSTALLED". Before concluding anything from that message in a script, refresh the PATH
  from `[Environment]::GetEnvironmentVariable('PATH','User')`.

## TODO

Found in the code review of 2026-08-28, before publishing the repo.

- [x] **Deadlock in `RunCommandSync`.** It read the whole of `StandardOutput.ReadToEnd()`
  and then `StandardError.ReadToEnd()`, calling `WaitForExit(timeoutMs)` only at the end.
  With the child writing to both streams, it filled the pipe buffer without draining it
  (~4 KB) and blocked. Worse than estimated: the block happens *before* the `WaitForExit`,
  so the timeout never got applied and the hang was **indefinite**, not 15 s. Reproduced
  with a process writing 4000 lines to each stream: the old version hung with no recovery;
  the new one finishes in under 2 s with all 8000 lines intact. Fixed with
  `BeginOutputReadLine`/`BeginErrorReadLine` and two `ManualResetEvent`. Done 2026-08-28.

  A nuance checked afterwards with the phone connected: **`--list-apps` does not trigger
  the hang**, despite being the likeliest looking candidate. With 222 lines of output the
  old version finished without trouble; scrcpy does not saturate both pipes at once. The
  bug is real, but it takes a process that writes a lot to both streams simultaneously.

  The wait on the `ManualResetEvent` was measured in case it added latency: it does not.
  End of stream arrives at 1741 ms, before `WaitForExit` returns, and the total matches the
  duration of the process. An earlier comparison showing 6 s against 1.8 s was misleading:
  it measured the first run of scrcpy, which uploads the 733 KB `scrcpy-server` to the
  phone and processes the apps, against a second, already warm one.
- [x] **No dependency check on startup.** Added `FindInPath()` and `CheckDependencies()`,
  called from `RefreshStatus()` and `Launch()`. Without scrcpy, the status bar shows
  "scrcpy: NOT INSTALLED" and the log says what is missing plus the
  `winget install Genymobile.scrcpy`, instead of Windows' "the system cannot find the file
  specified". Verified on a machine without scrcpy. Done 2026-08-28.
- [x] **`ConnectWifi` assumed the `wlan0` interface.** Extracted into `GetPhoneIp()`: it
  tries `wlan0` first and, failing that, goes through the rest of the interfaces. Done
  2026-08-28.

  The first version filtered by interface name (`rmnet`, `dummy`, `tun`) and **failed when
  tested against the real phone**: a <test-phone> with WiFi off has no `wlan0`, and
  its data interfaces are `ccmni0`/`ccmni1` — that is what they are called on MediaTek
  modems, while `rmnet` is the Qualcomm name. It returned `<cgnat-ip>`, an address in
  the carrier CGNAT range (100.64.0.0/10) unreachable from the LAN: exactly the mistake the
  filter was meant to prevent.

  Fixed with `IsLocalNetworkIp()`, which requires the IP to be in a private range
  (10/8, 192.168/16, 172.16/12) instead of trusting the interface name, which changes with
  the modem vendor. If there is no valid one, it lists the rejected ones with their
  interface and says to turn WiFi on. Verified against the phone: 12 IP classification
  cases plus the real run, which now returns `null` with the warning.
- [x] **`LICENSE`** GPLv3 added, the same one as acta and memoria. Done 2026-08-28.
- [x] **Git repository initialised** (2026-08-28). Branch `main`, initial commit `48e73a6`.
  Versioned: `Speculum.cs`, `README.md`, `LICENSE`, `state.md` and `.gitignore`. Outside
  version control: `.archive/` (earlier versions of the source and the two `.bat` menus,
  moved there once superseded by the graphical interface), `Speculum.exe` and the
  recordings folder. The README warns that the binary is not in the repo and has to be
  downloaded from the releases or built.
- [x] **Renamed the `scrcpy-menus` folder to `scrcpy-launcher`.** Done 2026-08-28.
  What blocked it was `adb.exe`: the server stays alive as a daemon and holds a handle on
  the directory it was launched from, and it outlives the session that started it. It is
  released with `adb kill-server`. Claude Code also opens handles on the folder while it
  works in it, and those are only released when the session closes.
- [x] **Repo published on GitHub** on 2026-08-28: `memoriainfinita/speculum`, private for
  now. Branch `main` with `origin` configured.
- [x] **Local folder renamed to `speculum`.** Done: the folder is now
  `<workdir>\speculum`. It had been blocked by the handles of the Claude Code session
  that made the change; with that session closed it went through. Git was unaffected.
- [x] **Release `v1.0.0`** published on 2026-08-28 with `Speculum.exe` attached (84,480
  bytes, SHA256 `2CA1290A...0238BCF`). The README already pointed at the releases; now they
  exist.
- [x] **App translated to English.** Done 2026-08-28. The whole source: visible strings
  (labels, tabs, tooltips, log messages), identifiers and comments. `Refrescar()` became
  `RefreshStatus()`, not `Refresh()`: `Form.Refresh()` already exists and a private method
  with that name would hide it. README and this file translated as well; the interface has
  no accented characters left to fix, so the note about writing new text without accents no
  longer applies.

  Two overlaps appeared because the English labels have different widths than the Spanish
  ones: "Height:" ran into its text box on the Window tab, and "Facing:" into its dropdown
  on the Camera tab. They were caught by instantiating the real form and testing every pair
  of sibling controls for intersecting bounds — worth keeping as the check for any future
  label change, since neither was visible in the source. Tab pages have to be given
  `tabControl.DisplayRectangle.Size` first: until the tab is shown they keep their 200x100
  design size and every measurement inside them is meaningless.

  Verified: clean compile; 30 automated checks (no overlaps, nothing clipped, every tools
  dropdown entry still matching a `case` in `RunTool`, `BuildFlags` emitting the same flags
  as before the translation, the incompatible pairs still blocked, no Spanish left in any
  visible string); and the four tabs captured to PNG and looked at. The screenshots in
  `docs/` were regenerated in English and renamed (`tab-basic`, `tab-advanced`,
  `tab-window`, `tab-camera`).
- [ ] **Decide when to make the repo public.** Before that, note that this `state.md`
  contains LAN IPs (<phone-ip>/<pc-ip>) and the phone model.
- [ ] **Improve the interface.** Scope still to be defined.
- [x] **Flag coverage audit.** Compared the scrcpy man page (master) against `BuildFlags()`:
  108 documented flags, 42 covered by the interface (39%). The remaining 66, by area:
  camera 9, window 10, connection/adb 8, keyboard/mouse 7, displays 6, codecs 5, buffers 4,
  recording 2, V4L2 2 (Linux only), other 13. Done 2026-08-28.
- [x] **High-value flags added.** Two new tabs, 19 controls:
  - *Window and capture*: `--window-x/y/width/height`, `--window-title`, `--no-window`;
    `--record-format`, `--record-orientation`; `--start-app`, `--new-display`,
    `--display-id`.
  - *Camera*: `--camera-id`, `--camera-facing`, `--camera-size`, `--camera-fps`,
    `--camera-ar`, `--camera-zoom`, `--camera-high-speed`, `--camera-torch`.
  - `--list-camera-sizes` added to the tools dropdown.
  - Setting any camera field switches the video source to `camera` on its own and says so
    in the log: otherwise scrcpy ignores those flags silently.
  - `BlockingReason()` blocks the incompatible combinations (`--new-display` with
    `--display-id`, `--camera-id` with `--camera-facing`) explaining which one to drop.
  - `ClearAdvanced()` extended to the new fields; otherwise flags stayed active and
    invisible after pressing "Clear all".
  - Coverage after the change: 62 of 108 flags (57%), measured with the same audit. What is
    left out is fine tuning (codecs, buffers), Linux specific (V4L2), multi-device
    connection, or reachable through the free flags field.
  - Verified with 30 automated checks that instantiate the real form and compare the output
    of `BuildFlags()`, including the validations and a regression of the options that
    already existed. Done 2026-08-28.
- [x] **Test with a real phone.** <test-phone> over USB, scrcpy 4.1. Checked: device
  detection, `--list-apps` (222 lines), `--list-cameras` (3 cameras), `--list-camera-sizes`
  (the new option) and `--list-displays`. Mirror launched from the app with the new flags:
  the window comes up with the given `--window-title` and the position and size from
  `--window-x/y/width/height` are applied. Done 2026-08-28.

  Camera tested end to end: with `--camera-id=0`, `--camera-size=1920x1080` and
  `--camera-fps=30`, and the video source deliberately left at "(default)". The app
  detected the fields, switched the source to `camera` and logged it, and scrcpy showed the
  live image from the back camera. The window comes up landscape (~16:9) as opposed to
  screen mirroring, which is portrait (1080x2400): that confirms the source really changed.
  Sizes this phone accepts, via `--list-camera-sizes`: from 3264x2448 down to 720x480 on
  the back camera, with fps {10, 15, 20, 30}.

  When measuring the window, `GetWindowRect` lies: it includes the invisible DWM border and
  gives a position and a size that are not the visible ones. To check where it really is
  you need `DwmGetWindowAttribute` with `DWMWA_EXTENDED_FRAME_BOUNDS` (9). Besides, scrcpy
  positions the video area, not the window: the title bar adds about 38 px on top. And if
  the requested height does not fit on the screen, the system clips it and adjusts the
  width to the phone's ratio, which looks like the flags being ignored when they are not.

- [x] **WiFi pairing tested end to end.** With the phone on `wlan0` <phone-ip>/24 and the
  PC on <pc-ip>, and with mobile data on at the same time (`ccmni0`,
  <cgnat-ip>): `ConnectWifi()` put the phone into TCP mode, picked the `wlan0` address
  discarding the data one, and `adb connect <phone-ip>:5555` connected. After that, a
  mirror launched with `-e` from the app, with bitrate 2M, size 800 and 30 fps: correct
  image and `--window-title` / `--window-x` applied. Done 2026-08-28.

  This also confirmed live the Tailscale problem already noted above: it installed a
  `<lan-subnet>` route with metric 0 that beat the Ethernet one (metric 256), sending LAN
  traffic through the tunnel. Pairing worked all the same, but latency to the phone was
  154 ms on the first ping. After uninstalling Tailscale only the Ethernet route is left
  and it drops to a steady 3-5 ms.
- [x] **Visual review of the new tabs** (2026-08-28). Camera looked fine. On "Window and
  capture" the "Apps and displays" group was cut off at the bottom: the three groups add up
  to ~332 px and the `TabControl` had a fixed height of 305 px, so the new-display and id
  fields could only be reached by scrolling inside the tab. Making the window bigger did
  not help: the `TabControl` is anchored `Top|Left|Right`, without `Bottom`, and does not
  grow. Fixed by raising the `TabControl` to 380 px, `ClientSize` to 620x780 and
  `MinimumSize` to 640x680, and moving the buttons, tools and log down from `y=355` to
  `y=430`. Verified on screen: it all fits with no scrolling.
