# Developing Unmuddle

Everything needed to pick the project up on a fresh PC and carry on: setting up, building, running, checking each part, releasing, and the traps found along the way. [CONTRIBUTING.md](../CONTRIBUTING.md) has the rules; [BRIEF.md](BRIEF.md) has the decisions; [STATUS.md](STATUS.md) says where things stand; [MILESTONES.md](MILESTONES.md) is the full build log.

The app was called Helpers until 11 October 2026. Users see Unmuddle everywhere, and the program is `Unmuddle.exe`. The code keeps the working title in its solution, projects and namespaces (`Helpers.slnx`, `Helpers.Core` and so on), and `HelpersInfo` holds the name, the tagline and the repository address.

## Setting up a new PC

1. Install [Git](https://git-scm.com/download/win), the [.NET 10 SDK](https://dotnet.microsoft.com/download), and [VS Code](https://code.visualstudio.com/) with the C# Dev Kit extension. Use the direct installers; `winget` has hung on one PC while refreshing its index.
2. Clone somewhere that isn't synced by Dropbox, OneDrive or Google Drive, for example `C:\Code\unmuddle`:

   ```
   git clone https://github.com/drhumphrey/unmuddle.git
   cd unmuddle
   ```

3. Make your commits use your private GitHub address, not a work one:

   ```
   git config user.name "Your Name"
   git config user.email "92411358+drhumphrey@users.noreply.github.com"
   ```

4. Build, test and run:

   ```
   dotnet build Helpers.slnx
   dotnet test Helpers.slnx
   dotnet run --project src/Helpers.App
   ```

5. The first run downloads the voice (350 MB). The local AI model (2.5 GB) and the dictionary (10 MB) download when first used. They live under `%LOCALAPPDATA%\Unmuddle\models` and survive rebuilds.
6. Optional: the [GitHub CLI](https://cli.github.com/) (`gh auth login`) for releases and pull requests from the terminal; [Inno Setup 6](https://jrsoftware.org/isdl.php) to build the installer locally.

The Windows spell checker needs Windows' English (United Kingdom) language features. Without them the spelling tests do nothing and Compose says spelling is off.

## Where things live

**In the repository**

| Folder | What's in it |
|---|---|
| `src/Helpers.Core` | Everything platform-free: text clean-up, Markdown, sentences, numbers said naturally, pronunciations, spelling logic, the AI prompts and notes parser, the word tools, updates, settings. Every part is unit-tested. |
| `src/Helpers.Speech` | The Kokoro voice through sherpa-onnx, and its download. |
| `src/Helpers.Ai` | The local model through LLamaSharp, Claude through the Anthropic SDK, and the local model catalogue and download. |
| `src/Helpers.Windows` | Windows-only parts: selection capture (UI Automation, then the clipboard), the mouse and keyboard hooks, the hotkey window, pasting into other windows, the spell checker, Credential Manager, start-up registration. |
| `src/Helpers.App` | The Avalonia app. `Overlays/` never take focus (Read button, player, toasts, quick menu). `Windows/` are normal windows built on `ShellWindow` (Compose, Settings, Welcome, word tools, updates). `Services/` holds the controllers. `Themes/` holds the vibes and the shared control styles. |
| `tests/Helpers.Tests` | Unit tests for Core. `Fixtures/` holds real AI replies used as reading tests. |
| `tests/Helpers.Windows.Tests` | Tests against the real Windows spell checker. They do nothing on a PC without UK English. |
| `tools/EngineSpike` | Reads a paragraph with two voices and prints timings and memory. |
| `tools/ReadingDump` | Prints what the reader would say for a file. Handy when a reply reads oddly. |
| `tools/screenshots` | Scripts that capture the app's own windows for the README. See its README. |
| `installer/Helpers.iss` | The Inno Setup script. |
| `.github/workflows` | Build on every push, CodeQL scanning, and the release on a `v*` tag. |

**On the PC, outside the repository**

| What | Where |
|---|---|
| Settings, including the unsent Compose draft | `%APPDATA%\Unmuddle\settings.json` |
| The user's own spelling words | `%APPDATA%\Unmuddle\dictionary.txt` |
| Voice, local AI model, dictionary data | `%LOCALAPPDATA%\Unmuddle\models` (`kokoro-multi-lang-v1_0`, `llm`, `wordnet`) |
| Claude API key | Windows Credential Manager, generic credential `Unmuddle/anthropic-api-key` |
| Start with Windows | `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, value `Unmuddle` |
| Installed app | `%LOCALAPPDATA%\Programs\Unmuddle` (where the installer puts it) |

Copies from the Helpers days used the same places under the name Helpers. On every start, before anything reads its settings, `Program.Main` moves them across: both folders (only if the new one doesn't exist yet), the saved Claude key (the old one is removed once the new one is saved), and the start-up entry. The installer keeps the old AppId, so it replaces a Helpers install, and it removes the old program folder and Start menu folder.

## Developer switches

Pass these to `Unmuddle.exe`, or after `--` with `dotnet run --project src/Helpers.App --`. They let you check a part without clicking through the app.

| Switch | What it does |
|---|---|
| `--read-file <path>` | Reads a file aloud |
| `--read-clipboard` | Reads whatever text is on the clipboard |
| `--expanded` | Opens the player's reading view |
| `--menu` | Shows the styled tray menu |
| `--pill` | Shows the Read button at the mouse |
| `--compose` | Opens Compose |
| `--compose-check`, `--compose-tidy` | Opens Compose and runs Check my thinking or Tidy on the saved draft |
| `--lookup <word>` | Opens the word tools on a word |
| `--dictionary` | Downloads the dictionary data |
| `--settings [page]` | Opens Settings, optionally on a page: `Voice`, `Reading`, `"Read button"`, `Look`, `Player`, `AI`, `Start-up`, `"Privacy and licences"` |
| `--first-run` | Shows the welcome screen |
| `--ai-test <in> <out>` | Downloads the local model if needed, runs Check my thinking and Tidy on a file, and writes the answers, timings and memory use to another file. Uses its own temporary settings. |

Only one copy of the app runs at a time (`SingleInstance`). A second copy tells the first, which shows a message, and closes straight away, switches and all. Stop the running copy, the installed one included, before starting a development copy. Version 0.2.0 and earlier don't know the rule, so a newer copy will start beside one of those and they will fight over the shortcuts.

## Checking each part by hand

| Part | How to check |
|---|---|
| Reading | Select text in Outlook, Chrome, VS Code and Teams; press Ctrl+Alt+H; the clipboard must be unchanged afterwards. |
| Read button | Drag-select, double-click and triple-click; it must not appear for window drags, file drags, scrollbars or the Snipping Tool, nor for opening a file with a double-click or dragging a box around files. |
| Compose | Type three misspellings; right-click one and fix it; Read back; Ctrl+Alt+C from VS Code's chat box, then Send to chat: the text lands, nothing is submitted, the clipboard is unchanged. |
| Edit and Put it back | Select your own text anywhere, Edit, change it, Put it back: the original selection is replaced. |
| AI helper | `--ai-test` on a draft, then the buttons in Compose. With a Claude key, the first cloud press must show what will be sent. |
| Word tools | `--lookup necessary`: syllables, respelling, IPA, meanings and synonym chips. |
| Updates | Needs a published release. Switch the check on, press Check now. |

The full acceptance list, app by app, is in [BRIEF.md](BRIEF.md).

## Releasing

1. Set the version in `Directory.Build.props` (`<Version>`), write the notes in `docs/releases/v<version>.md`, commit and push. The Release workflow refuses a tag that doesn't match the version there.
2. Tag and push the tag:

   ```
   git tag v0.2.0
   git push origin v0.2.0
   ```

3. The Release workflow tests, publishes a self-contained win-x64 build, drops debug symbols and other platforms' native libraries, zips it, builds the installer with Inno Setup, writes `version.json` with the SHA-256 of both files, records a build attestation for all three (anyone can check one with `gh attestation verify <file> --repo drhumphrey/unmuddle`), and attaches them to a GitHub release with the notes from `docs/releases`. About ten minutes.
4. Copies with the update check switched on see the new version within a day.

To build the same thing locally:

```
dotnet publish src/Helpers.App --configuration Release --runtime win-x64 --self-contained true -p:PublishSingleFile=false --output out/publish/win-x64
```

Then delete `out/publish/win-x64/*.pdb`. With Inno Setup installed: `iscc installer\Helpers.iss /DVersion=0.2.0 /DPublishDir=..\out\publish\win-x64`.

**When a downloaded model changes**, its checksum in the code must change too, or the download is refused: the voice in `src/Helpers.Speech/KokoroEngine.cs`, the local models in `src/Helpers.Ai/LocalModels.cs`, the dictionary in `src/Helpers.Core/Words/WordNet.cs`. Take the SHA-256 from the publisher (GitHub release assets show a digest; Hugging Face shows it on the file page) and check it against your own download.

**Trying an update without a release.** The quiet install and restart can be tried on one PC: publish as above, build two installers with different `/DVersion` values, install the first with `/VERYSILENT /MERGETASKS="!startup"`, start it, then run `cmd.exe` with the arguments `UpdateCheck.HandOverArguments` gives for the second installer and the installed exe, and close the app. The uninstall entry should then show the second version, and the app should start again within half a minute. Remove it with `unins000.exe /VERYSILENT` in the install folder. Stop the development copy first, because the uninstaller ends every `Unmuddle.exe`.

## Dependabot and code scanning

- Dependabot proposes updates once a month, minor and patch versions grouped into one pull request per ecosystem; major versions come one at a time.
- Library updates need the app run, not just the tests: start it, read something, open Compose, run `--ai-test`, then merge. Change the library's version in `ThirdPartyNotices` and `docs/THIRD-PARTY-NOTICES.md` too: the Privacy and licences page shows it.
- Test-tool updates need the build log checked: it must still say 261 and 3 tests ran, because a test runner that finds no tests still passes.
- CodeQL results are under the repository's Security tab. `.github/codeql/codeql-config.yml` leaves out three checks that flag the design rather than problems: calls into Windows (two checks) and every `Path.Combine`.
- Dependabot alerts, secret scanning with push protection, and private vulnerability reporting are on. Push protection refuses a push that contains a key.

## Traps found along the way

**Builds**

- "Image is too small" from `CreateAppHost`: a build was interrupted by a file lock and left a truncated file in `obj`. Run `dotnet clean src/Helpers.App` and build again.
- Builds fail on locked files while the app is running. Stop it first.
- A freshly published folder can be briefly locked by antivirus. Retry after a few seconds. A shell sitting inside the folder also stops it being deleted.
- LLamaSharp's CPU backend copies native libraries for every platform; the app project leaves out all but the target runtime's. The package writes its paths with doubled separators (`runtimes\\win-x64\...`), so the match is a loose pattern.
- `AVLN3001` warnings from Avalonia's XAML compiler are harmless.
- Smart App Control, if it's on, blocks unsigned files it doesn't trust, freshly built ones and NuGet build tools included. On the second PC it blocked a test DLL (`dotnet test` then says "No test is available"), Avalonia 12.1.4's source generator (the build then fails with every named control missing, CS0103) and the app's own DLL (the app doesn't start). The test host's `--diag` log says "An Application Control policy has blocked this file", and the Code Integrity event log names each file. It has no exclusions; on a development PC, turn it off. The second PC's was turned off on 11 October 2026.
- On Windows, don't type bare `python`: the Microsoft Store alias can hang.

**Avalonia 12**

- Renames from 11: `SystemDecorations` is `WindowDecorations`; `NativeMenuItemToggleType` is `MenuItemToggleType`.
- No non-activating windows built in: add `WS_EX_NOACTIVATE`, `WS_EX_TOOLWINDOW`, `WS_EX_TOPMOST` once the handle exists, and answer `WM_MOUSEACTIVATE` with `MA_NOACTIVATE` (see `OverlayWindow`).
- Acrylic blur covers the whole window rectangle, so a transparent shadow margin turns into a frosted box. Overlays use plain transparency.
- A `MenuFlyout` creates its presenter before raising `Opening`; items added in `Opening` never show, and the menu opens as an empty sliver. Build the items before it opens (Compose does it on the right-button press).
- A `MenuFlyout` named in XAML gets no code-behind field, because it isn't in the visual tree.
- `TextBox` moves the caret on a right-click release, and raises `TextChanged` after the fact.
- The `ScrollViewer` inside a `TextBox` gets its template a layout pass later than the box, so look for the `TextPresenter` until it's there.
- A `TextBlock` inside a horizontal `StackPanel` never wraps. Use a `Grid` with `Auto,*` columns.
- Fluent styles the `accent` button class itself, in Windows blue, and wins over app styles. The app's main button class is `main`.
- With monitors at different scaling, a window's size is only right after it lands; overlays place themselves twice.

**Windows**

- NAudio 3 renamed `WaveOutEvent` to `WaveOut`, removed `DesiredLatency`, and needs a `-windows` target framework.
- Output device names from WinMM are cut at 31 characters. WASAPI would give full names.
- Low-level hooks are removed silently if the callback is slow. The hooks only note the event; all work happens elsewhere.
- `cmd /c` strips the first and last quote of a line with more than two, so `"installer" /args & start "" "app"` falls apart ("The filename, directory name, or volume label syntax is incorrect"). Use `/s /c` with the whole line in one more pair of quotes, as `UpdateCheck.HandOverArguments` does.
- Ctrl+C in a terminal means "interrupt". Selection capture sends Ctrl+Insert first and never Ctrl+C to a terminal; pasting into a terminal uses Shift+Insert.
- The clipboard must always be put back. `ClipboardSnapshot` saves every copyable format first.
- The window in front can be the lock screen, Start or search. Those are never a target for sending text.
- Windows' Snipping Tool overlay looks exactly like a text selection to the mouse hook. It's excluded in code.
- So does opening a file with a double-click, or dragging a box around files. The Read button looks at the mouse pointer (`CursorShape`): the ordinary arrow when the button went down, and no I-beam when it came up, means no text was selected. Windows' standard pointers are shared handles, so this asks the other app nothing. An app that draws its own pointers is given the benefit of the doubt.
- Ctrl+Alt+Space, the first default for reading, is also the Claude desktop app's shortcut. Windows gives a shortcut to whichever app asks first and doesn't say who has it.
- Electron apps (VS Code, Teams, Claude desktop) give a full UI Automation tree only when they think assistive technology is running. Don't rely on it there; the clipboard route covers them.

**Installer**

- Inno Setup's install-mode dialog offered "Install for all users". That registers the app machine-wide while the files still go in one user's folder, and the update check only recognises a per-user install. The dialog is gone; every install is per-user.
- The uninstaller ends every `Unmuddle.exe` by name, including a development copy run from `bin`.

**AI**

- Qwen3 thinks before answering unless told not to. The local provider adds `/no_think` and an empty `<think>` block.
- The local model sometimes mis-copies the words it quotes. Notes are pinned with exact, loose and edit-distance matching in turn.
- The Anthropic SDK reads `ANTHROPIC_BASE_URL` and `ANTHROPIC_API_KEY` from the environment. The app pins the address and always passes the key from Credential Manager.
- With the voice and the 4B model loaded, the app uses about 4.7 GB. The model unloads after 10 idle minutes by default.

**PowerShell, for scripts**

- `Start-Process -ArgumentList` splits arguments containing spaces unless each has its own inner quotes.
- `$null` passed to a `string` parameter of a native call arrives as an empty string. Pass `[IntPtr]::Zero` where the API wants NULL.
- Screen coordinates are only right in a DPI-aware process. Call `SetProcessDpiAwarenessContext(-4)` first.

## Working with an AI coding assistant on this repo

`CLAUDE.md` holds the working agreement an assistant reads first. The rules that matter most on a real PC:

- Never send synthetic mouse clicks or key presses to the desktop. To test a menu, post a message to the app's own window only (see `tools/screenshots/post-right-click.ps1`).
- Never screenshot anything but the app's own windows. The scripts in `tools/screenshots` find the window by its exact title and refuse anything else.
- Back up `settings.json` before planting test data, mute the voice, and put the settings back afterwards.
