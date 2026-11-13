# Milestone log

One entry per milestone: what was built, what was measured, what didn't go to plan. Newest at the bottom.

## Milestone 0: scaffolding (8 October 2026)

**Built:** `Helpers.slnx` with Core, Speech, Ai, Windows and App projects, an xUnit test project, shared build settings in `Directory.Build.props`, and a GitHub Actions workflow that builds and tests on every push.

**Not to plan:**

- The Avalonia template installed Avalonia 12, not 11. Same licence, brief updated.
- The .NET 10 SDK writes solutions as `.slnx`, so the file is `Helpers.slnx`.
- winget hung on this PC while refreshing its index, so the SDK was installed with Microsoft's direct installer.
- The checkout lives in Dropbox. `.git` and every `bin` and `obj` folder are marked Dropbox-ignored instead of moving the folder.

## Milestone 1: engine spike (8 October 2026)

**Built:** `tools/EngineSpike`, a console program that downloads the Kokoro model package once, loads it in sherpa-onnx, speaks a 325-character paragraph with two British voices, streams audio to the speakers as the engine produces it, saves WAV files, and prints the numbers below.

**Model:** `kokoro-multi-lang-v1_0` from the sherpa-onnx releases, 350 MB download, full precision. It is the only sherpa-onnx package with the British voices. No int8 build of it exists; the only int8 Kokoro package is v1.1, which is Chinese-focused with three English voices.

**Machine:** Dave's work PC, Windows 11, CPU only, 4 engine threads.

| Measure | Result | Brief target |
|---|---|---|
| Model load | 850 to 900 ms | not set |
| Time to first audio, first ever run | 855 ms | under 1 s |
| Time to first audio, warm | 130 to 230 ms | under 1 s |
| Synthesis speed | 6 to 8 times faster than real time (RTF 0.12 to 0.16) | not set |
| Memory after load | about 450 MB working set | under 400 MB |
| Memory after three paragraphs | about 700 MB working set, 860 MB private | under 400 MB |

**Voice quality:** Dave's call. The WAV files are in `out/engine-spike/` and the spike can be re-run with any of the eight British voices: `dotnet run --project tools/EngineSpike -- --runs bf_lily:1.0,bm_daniel:1.0`.

**Not to plan:**

- **Memory is over target and grows with use.** The engine's working set rises from 450 MB to 700 MB over three paragraphs, with playback switched off, so the growth is in onnxruntime, not the spike. Things to try at milestone 3: synthesise one sentence at a time rather than a whole paragraph (the arena grows with the longest input), look for an onnxruntime arena setting sherpa-onnx exposes, quantise the model to int8 ourselves, and rely on the planned "unload when idle" setting.
- **No int8 British model** exists ready-made, so the brief's "prefer int8" can't be followed yet.
- **NAudio 3 renamed things.** `WaveOutEvent` is now `WaveOut`, and the buffer size is set in the constructor. Worth knowing before milestone 3.
- **The spike targets `net10.0-windows`** because NAudio's playback classes only ship for Windows frameworks. The real `IAudioOutput` for Windows will need the same.

## Milestone 2: text pipeline (8 October 2026)

**Built:** the reading pipeline in `Helpers.Core.Text`, with 80-plus unit tests. Captured text goes in; a list of segments comes out, each with the text to show, the text to speak, and a pause to leave afterwards.

- `MarkdownDetector` decides whether text is Markdown (AI chat) or plain (email, documents).
- `PlainTextSegmenter` ports the prototype rules: bullets stripped, blank lines dropped, one segment per line.
- `MarkdownSegmenter` uses Markdig: headings and bold-only lines pause longer, bold and inline code keep their words, code blocks become "code block, 12 lines", lists go one item at a time with numbers spoken, tables get a caption then one row per segment as "column: value; column: value".
- `SentenceSplitter` splits on full stops, question and exclamation marks but not after Dr., e.g., et al., Fig. 2, initials, list numbers, decimals, version numbers or file names, and chunks anything over 400 characters at a clause boundary.
- `SpokenTextRules` turns web addresses into "link", email addresses into "email address", and file paths into "file name.ext", all switchable.
- `PronunciationDictionary` does whole-word replacement with optional case sensitivity and ships a starter list.
- `ReadingPipeline` strings them together.

**Test data:** two real Claude Code replies from this project, saved under `tests/Helpers.Tests/Fixtures`. The acceptance checks from the brief run against them: no stars, hashes, backticks or pipes are ever spoken, code is skipped with a count, tables read row by row, nothing handed to the engine is over the limit.

**Tool:** `tools/ReadingDump` prints the pipeline's output for any file, so a reply can be checked by eye before it is heard.

**Not to plan:**

- Markdig is at version 1.4, not the 0.x series the docs online mostly describe. The API was the same.
- Two things only showed up when reading real output rather than unit tests: a quoted abbreviation such as "e.g." wrongly ended a sentence, and table rows joined with commas were hard to follow. Both fixed and now covered by tests.
- Bold-only lines were added as headings. The brief didn't ask for it, but every Claude Code reply uses them that way.

## Milestone 3: player and tray (started 8 October 2026)

**Step 1, the overlay spike: passed.** An Avalonia 12 window can be clicked without taking focus. `OverlayWindow` in the app project is borderless, topmost, off the taskbar, translucent with acrylic blur, and refuses activation two ways: the Win32 no-activate and tool-window styles are added once the window opens, and a message hook answers `WM_MOUSEACTIVATE` with `MA_NOACTIVATE`. The test: Notepad in front, two simulated clicks on a button inside the overlay, Notepad still in front, the button's counter at 2. Every overlay surface (pill, player, toasts, AI card) will inherit from this class.

**Also in step 1:** Lexend is now the app font, bundled under `src/Helpers.App/Assets/Fonts` with its licence, replacing the template's Inter package. The app has no main window: it starts in the tray with a menu, and `ShutdownMode` is explicit.

**Not to plan so far:**

- Avalonia 12 renamed `SystemDecorations` to `WindowDecorations`. Expect more renames like it; the online docs mostly describe 11.
- `Helpers.App` and `Helpers.Windows` now target `net10.0-windows` rather than plain `net10.0`, because NAudio's playback classes only ship for Windows frameworks and a plain project can't reference a Windows one. The Mac build will need its own target later; Core, Speech and Ai stay platform-neutral.

**Step 2, the player (8 October 2026, evening).** Reading works end to end: text goes through the pipeline, `ReadingSession` synthesises one sentence ahead and plays through NAudio, the player shows the current sentence and expands into the reading view with click-to-jump, toasts stack above the tray, and the tray menu has Read clipboard, Watch clipboard, Pause, Stop, Show player, Settings, Calm look, Memory in use and Exit. Settings persist in `%APPDATA%\Helpers\settings.json`. Neon and Calm are resource dictionaries swapped at runtime.

**Dave's first test:** every voice, play, stop, replay and speed all fine. He asked for volume, an output device choice, a settings cog, and a settings area, and reported that the player could grow off the edge of his monitor and that toasts could straddle two monitors.

**Step 3, from that feedback.** A first Settings window, pulled forward from milestone 6: voice with preview, speed, volume, output device, vibe, light or dark, hide delay, watch clipboard. A cog on the player and a Settings item in the tray open it. Overlays now open on the monitor the mouse is on and nudge themselves back inside that screen whenever their size changes; the reading view's height scales to the screen. A microphone choice waits for dictation.

**Not to plan in steps 2 and 3:**

- Acrylic blur applies to the whole window rectangle on Windows, so the transparent shadow margin became a frosted box. Overlays now use plain transparency with a near-solid card instead; the glow fades properly, the blur-through is gone.
- Play on a finished reading did nothing. Now it restarts; clicking a sentence after the end starts from there.
- NAudio 3 again: `DesiredLatency` is gone; use `BufferMilliseconds` and `NumberOfBuffers`. Avalonia 12: `NativeMenuItemToggleType` is now `MenuItemToggleType`.
- A build interrupted by a file lock left a truncated DLL in `obj`, which then failed every later build with "Image is too small" from the app-host step. `dotnet clean` on the project fixes it.
- Output devices come from the old WinMM API, whose names are cut at 31 characters. WASAPI gives full names and device-change events; worth switching when device hot-plugging is handled.

**Still to do in milestone 3:** the drifting flecks, the speed slider in the gradient rather than the default blue, the memory figure in the log once measured in the real app, and an app icon once the product has a name.

**Step 4, from Dave's second round (8 October 2026, evening).** Overlays now clamp to the screen that holds their top-left corner, so growing across a monitor edge pulls them back instead of pushing them over. The reading view is resizable by a grip in its corner and remembers its size. A Size setting scales every overlay from 60% to 125%, default 90%. The Custom vibe takes two or three hex colours, with six named palettes to start from. The border gradient and the sentence highlight drift while the voice is speaking, with a switch to stop it. The compact bar no longer cuts sentences off: it renders the sentence word by word, marks the word being spoken, and scrolls so that word's line is visible. Kokoro gives no word timings, so the word is estimated from the clip's length and the words' lengths, with extra weight for punctuation.

**Not done from that round:** the tray menu is a native Windows menu and can't take the vibe styling; a styled quick menu comes with the Read button in milestone 4, along with the hotkey and send-to-reader Dave asked for.

**Step 5, from Dave's third round.** In the reading view the sentence box now sits on its own full-width line under the controls, so narrowing the window can't push it under the icons. The open/close button shows which state it is in. The word being spoken is marked on the current line of the reading view as well as in the bar. Whether the player was left open is remembered. The Custom vibe's colours are now picked with colour wheels (Avalonia's own ColorPicker package, same licence) and apply as you pick; the Apply button is gone. A switch adds the optional third colour.

**Step 6, closing the milestone.** Flecks: glowing dots in the gradient colours plus lime, placed in the band of ground around the card and wandering slowly; a Flecks slider in Settings sets how many, Calm has none, and Windows' "animation effects off" setting freezes them along with the gradient drift. Sliders in every vibe take the vibe's colours. A multi-size app icon built from the speaker mark is embedded in the exe and used for the tray; it is name-agnostic, so the product name can come later.

**Milestone 3 measured:** about 750 MB working set while reading a long reply, the same as the engine spike, so reading sentence by sentence did not shrink the engine's arena. Idle CPU is effectively zero: the only timer that runs while idle is the 40 ms fleck drift, and only while the player is visible.

**Milestone 3 done, 8 October 2026.** Not in the brief but built on Dave's requests along the way: volume, output device, a first Settings window, UI scaling, a resizable reading view, custom gradient colours with colour wheels, gradient motion, word-level highlighting. Deferred to milestone 4 and 5: the hotkey, selection capture, the Read button, and a styled quick menu in place of the native tray menu.

## Milestone 4: selection capture and the hotkey (started 8 October 2026)

**Built:** `SelectionCapture` in `Helpers.Windows`, behind `ISelectionSource` in Core. UI Automation first through FlaUI, time-boxed to 150 ms; then the clipboard: every copyable format is saved, the clipboard is cleared, Ctrl+Insert is sent, the app waits up to 350 ms for text, and only then, and never to a terminal, VS Code or Cursor, does it try Ctrl+C for up to a second. The saved clipboard is always put back. Windows running as administrator are detected and refused with a hint. Password fields are refused.

**The hotkey:** a hidden message-only window on its own thread (`MessageWindow`) registers a global shortcut, default Ctrl+Alt+Space, on by default because Dave asked for it. Pressed while reading, it stops; otherwise it reads the selection. Settings has a key-capture box, an on/off switch, and a message if Windows refuses the combination because another app owns it. Verified from a second process: Windows refuses to register Ctrl+Alt+Space while the app runs, so the app holds it.

**Also in Core:** `HotkeyGesture` parses and formats shortcut text, with tests.

**Dave's test, 8 October 2026, evening:** the shortcut works in Teams, VS Code, Chrome and Outlook. Three things came out of the testing and were fixed the same evening: a teardown race when a new read replaced a running one; the shortcut now reads a new selection even mid-read and stops only when nothing is selected; and Watch clipboard, which read every copy, now only offers a Read button in a fading toast. Also added from the testing: years, money, percentages, clock times and ordinals spoken naturally (`NumberSpeech`), an unload-when-idle setting for the voice's memory, messages pinned to the main monitor by default, and placement that settles twice because Dave's monitors have different scaling. Word, PDFs, Notepad, Explorer and Windows Terminal are still to be ticked off the checklist.

**Milestone 4 done, 8 October 2026.**

## Milestone 5: the Read button and the quick menu (started 8 October 2026)

**Built:** low-level mouse and keyboard hooks on the hotkey's message thread (`InputHooks`); the callbacks only read the event and raise it, and the keyboard hook carries no key codes, only "a key was pressed". A platform-neutral `SelectionGestureDetector` in Core turns the events into drag-select, double-click and triple-click, with tests. `ReadButtonService` applies the brief's rules before showing the pill: not when the press was on a frame (title bar, border, scrollbar, checked with a 50 ms hit-test), not when the release is over a different window (a window or file drag), not over our own windows, not in excluded apps (password managers and remote desktop to start with), and not while paused from the tray. The pill is an overlay capsule with one Read button that uses the same capture as the shortcut; it hides after three seconds or on any click, key or scroll elsewhere. Settings gained a Read button section: on/off, delay, excluded apps.

**The quick menu:** a left-click on the tray icon opens a styled menu in the vibe's colours with the same items as the native menu, plus ticks for the toggles. It closes on a choice, a click anywhere else, a key press, or after ten seconds. The native right-click menu stays.

## Milestone 6: settings (8 October 2026, late evening)

**Built, with Dave away and testing tomorrow:** the Settings window is now tabbed: Voice, Reading, Read button, Look, Player, Start-up. New in it: the pronunciation dictionary editor (add a word and how to say it, match case optional, remove), readable-text options for the reading view and the sentence bar (Lexend, Atkinson Hyperlegible or the Windows font; size 14 to 32; line spacing; a cream or grey tint), and the Markdown reading options from the brief (code blocks, file paths, tables, web and email addresses, numbers). Start with Windows uses the per-user Run key and shows in Settings, the tray and the quick menu. Atkinson Hyperlegible is bundled with its licence, as the brief said.

**Not to plan:** nothing yet; Dave hasn't seen it. The readable-text settings don't reach Compose because Compose doesn't exist yet; the resources are in place for it.

## Milestone 7: packaging (9 October 2026, small hours)

**Built:** version 0.1.0 in `Directory.Build.props`; a self-contained win-x64 publish (`dotnet publish`, not single-file, so the native libraries sit beside the exe as the brief suggested); an Inno Setup script for a per-user install under `%LOCALAPPDATA%\Programs\Helpers` with no admin rights, a Start menu entry, an optional start-with-Windows task, and an uninstaller that leaves the voice model and settings alone; a Release workflow that runs on a `v*` tag, tests, publishes, zips, builds the installer on GitHub's Windows runner (which ships Inno Setup) and attaches both files to a GitHub release; a first-run screen; install notes in the README.

**Measured:** the publish is 162 MB on disk and 65 MB zipped. Run from the publish folder, the app starts, holds its hotkey, and sits at about 560 MB working set with the voice loaded.

**Not to plan:**

- The first publish was 262 MB because SkiaSharp and HarfBuzz ship 100 MB of native debug symbols. They are deleted after publish in CI and excluded from the installer.
- About 25 MB of the remaining size is Windows Forms, pulled in by FlaUI. Replacing FlaUI with direct UI Automation COM calls would remove it. Not worth it for v0.1.
- The installer is not built locally because Inno Setup isn't installed on this PC and winget hangs here. CI builds it. A local `iscc` run is one command if it's ever installed.
- Nothing is code-signed. SmartScreen will warn on first run; the README says so.
- The v0.1.0 tag is Dave's call after testing milestones 5 and 6. Pushing it is what makes the release.

## Milestone 8: Compose (9 October 2026, small hours)

**Spike first, as the brief said.** The Windows spell checker through its COM API, declared by hand in `Helpers.Windows` with no extra package. On this PC the English (UK) checker is present, finds "recieve" and "tomorow", and suggests "receive" and "tomorrow". Three tests in a new `Helpers.Windows.Tests` project talk to the real checker and pass in well under a second; they do nothing on a machine without the UK English language pack, so CI stays green there.

**Built:** `ISpellChecker` in Core, with `DraftSpelling` (the checker's errors, minus the user's own words, minus the word still being typed), `SpellingErrors` (pure helpers that keep the underlines in place between checks, with tests) and `UserDictionary` (one word per line in `%APPDATA%\Helpers\dictionary.txt`). `ITargetWindow` and `KnownApps` in Core; `TargetWindow` in Windows: remember the window, bring it to the front, Ctrl+V or Shift+Insert for terminals, the clipboard saved and put back, never Enter. The Compose window: the target in the header with "Use this window", the editor at reading size with wavy red underlines drawn by `SpellingUnderlines` over the text box, a right-click menu with suggestions, "Add to dictionary", "Ignore for now" and the usual edit items, then Read back, Copy and Send to chat, and a status line with the spelling state and "Draft saved". Tidy and Make a request are greyed with a hint until milestone 9. Drafts save two seconds after the last edit and on hide, and come back when Compose reopens. Window placement is remembered. A second global shortcut, Ctrl+Alt+C, opens Compose sending to the window that had focus; it is on by default like the first and lives in Settings under Shortcuts. Compose is in the tray menu and the quick menu. `--compose` is a dev switch.

**Not to plan:**

- The brief said to check the paragraph around the caret. The whole draft is checked instead, on a 350 ms pause after each keystroke or caret move: one COM call, well under a millisecond for a few paragraphs, and simpler to keep right. Worth revisiting only if someone pastes a book into Compose.
- "Add to dictionary" writes to the app's own file, as the brief said. Those words are also handed to the checker as session ignores, so Windows' shared dictionary, which Edge and Mail use, is never changed by this app.
- A `MenuFlyout` named in XAML gets no code-behind field because it is not in the visual tree, so the right-click menu is built in code.
- Dave's PC was locked while this was built, so the Compose window was first seen on a screen the next morning. The layout was right first time; the underlines were not drawn at all. The text box's scroll viewer gets its template a layout pass after the box itself, so the underline control looked for the text presenter too early and never again. It now looks until it finds it. A second small one: the box raises its text-changed event after the fact, so loading the saved draft counted as an edit and saved it straight back; loads are now recognised by content.
- The window screenshots used to check this came out as the wrong part of the screen until the capture script was made DPI-aware; on Dave's mixed-DPI monitors an unaware process gets virtualised coordinates.
- An automated review flagged the third-party release action in the Release workflow as unpinned. It is replaced by GitHub's own `gh release create`, so the workflow has no third-party actions beyond checkout and setup-dotnet.

**Dave to check, from the brief's acceptance list:** type a sentence with three misspellings and red underlines appear; a right-click fixes one; Read back works; Send to chat pastes into the VS Code chat box without submitting; the clipboard is unchanged afterwards. Also worth a look: "Use this window", Ctrl+Alt+C from another app, the draft surviving a close, and what the Sending to line says for Chrome, Teams and Outlook.

**Dave's first look, 9 October 2026, midday.** Two things: the normal windows looked basic next to the player (plain buttons, text sitting high in them), and text ran off the edge of the Welcome and Settings windows. Also "spelling suggestions didn't work", not yet pinned down.

- **Shell windows.** Compose, Settings and Welcome now share one window template, `Window.shell` in `Themes/Controls.axaml`, drawn like the player: no system chrome, a transparent band for the glow, the vibe's card, gradient border and shadow, a slim title row with the app mark that drags, minimise and close marks, and a resize grip when the window can be resized. The content sits in a `LayoutTransformControl`, so the Size setting now scales these windows and their fonts too. `ShellWindow` in `Windows/` is the base class; the overlays stay as they were because they must never take focus.
- **Buttons.** One app-wide style for text buttons: a quiet fill with the vibe's gradient outline, text centred both ways, hover and pressed states from the vibe. The main action on a screen (Done, Send to chat) takes the gradient fill. The class is `main`, not `accent`: Fluent styles `accent` itself in the Windows accent blue and its nested theme styles won over ours. Choice chips on the Welcome screen follow the same look. The overlays' own button classes are untouched.
- **Text off the edge.** Every "switch and sentence" row in Settings was a horizontal StackPanel, which gives its text infinite width, so the sentence never wrapped. Those rows are two-column grids now and the labels wrap; the Welcome screen's "Hear it" row likewise. The device name "Realtek HD Audio 2nd output (Re" is a different thing: the old WinMM API cuts names at 31 characters, noted under milestone 3.
- **The editing loop (Dave's design, 9 October 2026, late afternoon).** Dave described how he would actually use this: select his own half-written reply anywhere, bring it into Compose, fix it with the spelling, a read back and the coming AI notes, then put it back where it came from. So the pill now has three buttons: Read, Edit, and Paste when the clipboard holds text. Edit grabs the selection the same way Read does, opens Compose on it, remembers the source window, and the main button reads "Put it back"; it pastes over the original selection. Paste on the pill pastes the clipboard over the selection without touching the clipboard. An unsent draft that Edit replaces is one Ctrl+Z away, because the load goes through the editor's own undo. Written into the brief along with Dave's steer for milestone 9: the AI help that matters is a reader's notes ("I don't follow this bit", "something is missing here"), pinned to the text as questions or minimal fixes, never a rewrite; Tidy shrinks to spelling and grammar with every change marked. `--pill` shows the pill at the mouse for a look.
- **Snipping Tool.** Dave noticed the app "seems to affect the Snipping Tool". Dragging out a snip is a press, a drag and a release inside one full-screen window, which is exactly the shape of a text selection, so the Read button appeared over the snip. Windows' capture overlays (`ScreenClippingHost`, `SnippingTool`, `ScreenSketch`) are now excluded in code, on top of the user's own exclusion list, so an existing settings file gets the fix too. Waiting on Dave to say whether that was the whole of it.
- **Suggestions.** Dave's next message said right-click did nothing at all. Posting a right-click to the Compose window (a message to our own window only, nothing else on his PC touched) showed the menu does open, as a 4 by 48 pixel sliver with nothing in it. A headless Avalonia spike reproduced it: a flyout creates its presenter before it raises `Opening`, and items added in `Opening` never reach that presenter, so a menu built there is empty. Built on the press instead, before the release opens it, every item shows at full size. The menu is now a fresh flyout built on the right-button press (and on the Menu key or Shift+F10), so a cached presenter can never show stale items either. Two other changes from the same look: Avalonia's text box moves the caret to a right-click, and the checker left the word under the caret alone as "still being typed", so the underline could vanish a third of a second after the right-click; now a word is only left alone while the caret is where typing put it. And the menu no longer relies on the underline list: if nothing is listed under the pointer it asks the checker about that word directly (`SpellingErrors.WordAt`, with tests).

## Milestone 9: AI helpers (started 9 October 2026, evening)

Dave asked for the AI set-up the same evening ("so I can clean up whatever's in the composer along our instructions"), so this started straight after the editing loop, with both providers the brief decided on.

**Built, step 1:**

- Core: `IAssistant` (one method: system prompt, user message, streamed partials, token counts back), the five actions and their default prompts (`PromptTemplates`, with the draft fenced in the user message so instructions inside it are read as text), `WritingNote` and `NotesParser` (the model's JSON answer, however wrapped, turned into notes pinned to spans of the draft: exact match, then case-insensitive, then ignoring spacing and curly quotes, then a bounded edit-distance search for spans the model mis-copied by a letter; a note that still can't be pinned is kept, unanchored), `AiSettings` (provider, model, limits, prompt overrides, this month's token counts), `CloudCost` (tokens to dollars from Anthropic's published prices), `ISecretStore`. Tests for all of it, with one of Dave's own messages as the fixture.
- Windows: `CredentialStore`, the key in Credential Manager under "Helpers/anthropic-api-key".
- `Helpers.Ai`: `AnthropicAssistant` on the official `Anthropic` package (12.55), streaming, usage counted; `LlamaAssistant` on LLamaSharp 0.27 with the CPU backend, ChatML prompt with Qwen3's thinking switched off, stateless one-shot inference, unload on demand; `LocalModels` (Qwen3-4B Q4_K_M, 2.5 GB, and Qwen3-1.7B Q8_0, 1.8 GB, from Qwen's own Hugging Face repos, SHA-256 checked, one download at a time).
- App: `AssistantService` (builds the provider from settings, caches answers for the session, records cloud spend, unloads the local model after idle minutes); the Compose notes panel beside the draft (notes as cards with kind, the quoted span, the note and an Apply button when there is a fix; clicking a card selects its spot; Apply goes through the editor's undo and shifts the other notes; rewrite actions stream into the panel with Use this, Copy and Read it); a cloud mark on the buttons when text would leave the PC and a first-use confirmation that says exactly what will be sent; an AI page in Settings (provider, key, model, this month's cost, local model download with progress, unload minutes, and the five prompts editable with Reset).

**Measured on Dave's PC** with Qwen3-4B on the CPU: Check my thinking on a 900-character draft took 16 s and gave four sensible notes, all pinned; Tidy took 10 s and gave six fixes, one mis-copied by a letter (now caught by the fuzzy match). Working set with the voice and the model both loaded: 4.8 GB, well over the brief's 3 GB guess, so the unload-when-idle default of 10 minutes matters. The publish grew from 162 MB to 267 MB with the CPU backend's native libraries.

**Not to plan:**

- Tidy on the 4B model changed "Ok" to "Okay" despite being told to keep informality. The prompt says it twice now; the notes view makes it harmless either way, since nothing applies without a click.
- The model mis-copies a word now and then ("ofern" for "oftern"). `SpanAnchor` now ends with a bounded Levenshtein search, so those fixes still land.
- The first-use cloud confirmation lives inside the notes panel rather than a dialog, so nothing steals focus.
- Dave's settings were switched to "On this PC" after the model downloaded, so the buttons are live for his test; Claude needs his key pasted on the AI page.

**Dave to check:** Check my thinking and Tidy on something he has actually written; Apply on a fix and Ctrl+Z after it; Make a request; the cloud path once a key is in; whether 16 seconds feels acceptable or whether Claude should be the default on this PC.

**Step 2, the same evening: word tools, and a privacy and licences page.** Dave's asks at 18:52 and 19:19: "dictionary meaning, Google lookup and thesaurus would be useful, how to say and phonetic breakdown on a word, all the tools basically", and "in the settings we should have a licence and reference to other libraries used... being clear that local AI usage should keep everything local but linking to an AI API could make it leak."

- **Word tools.** Right-click any word in Compose and the first item is "Look up". A small shell window shows the word in syllables (re · gis · tra · tion), how to say it as a plain respelling with the stressed syllable in capitals (re-jis-TRAY-shuhn) and in IPA, Hear it and Slowly through the voice, Google and Wiktionary in the browser, the meanings by part of speech with an example, and the other words for it as chips that replace the word in Compose through the editor's undo. Syllables come from Liang's algorithm over the TeX British English patterns (MIT, Wujastyk and Toal), bundled. Sounds come from the British pronunciation list that already ships inside the voice package: 192,000 words from misaki, with its one-letter vowels expanded to IPA. Meanings and synonyms come from Open English WordNet 2025 (CC BY 4.0), read straight from its database files; it downloads once, 9.6 MB, SHA-256 checked, on the first "Download the dictionary". All three are in Core with tests; the WordNet test writes a two-line database in the real format, offsets and all.
- **Privacy and licences page** in Settings: what the app is and its licence, what leaves the PC in plain words, Anthropic's privacy policy a click away, and every component with its version, licence, what it is for, whether it uses the network, and a Website button. The list lives once, in `ThirdPartyNotices` in Core, and is mirrored in `docs/THIRD-PARTY-NOTICES.md`. The longer review is `docs/PRIVACY.md`.
- **Security review, and two fixes from it.** Every dependency was checked for network use: only the Anthropic SDK has any, and only when a cloud-marked button is pressed. The SDK would honour an `ANTHROPIC_BASE_URL` environment variable, which could redirect a draft and a key elsewhere, so the app now pins the address. The voice package download had no checksum; GitHub publishes one for the release asset and it is now built in and checked, as the model and dictionary downloads already were. Licences: MIT, BSD-2, Apache-2.0, OFL, CC BY 4.0 and espeak-ng's GPL-3 data all sit happily inside a GPL-3 program.

**Not to plan:**

- The patterns give hyphenation points, not true syllables: "in·form·a·tion", not "in·for·ma·tion". For seeing a long word in pieces that is still the point, and it is what every typesetter has used since 1983.
- The respelling's syllable split is a simple rule (a cluster of consonants between vowels leaves its first with the earlier syllable), so it says re-jis-TRAY-shuhn where a dictionary might say rej-i-STRAY-shun. Readable either way.
- WordNet has no entry for many names and new words; Google and Wiktionary are the fallback, in the browser, and the window says so.

**Step 3, the same evening: the opt-in update check.** Dave asked how updates should work and chose opt-in, "specially as we're doing the free approach". The release workflow now writes a `version.json` next to the installer and the zip, with the version, a note, the release page, and the SHA-256 of each file. In the app: `UpdateCheck` in Core (parse, compare versions, once-a-day rule; tests), `UpdateService` (asks GitHub 45 seconds after start-up and at most once a day, only when the switch is on; a toast offers the new version; the What's new window has Update now, the download page, Skip this version and Not now), the first-run screen's third question, and an Updates section on the Privacy page with the switch, Check now and the last check time. Update now downloads the installer, checks the checksum, hands over to a tiny command that runs the installer silently and starts the new version, and quits. Zip copies get the download page instead; the app knows which it is from the installer's uninstall key. Until the repo is public, GitHub answers 404 and the check says there is nothing yet.

**Step 4, getting ready to go public (9 October 2026, late evening).** Dave plans to make the repository public. Audit first: no keys anywhere in the history, no work references in any tracked file, no screenshots or local files tracked. Then the tidy: a README written for people arriving cold (what it is, who it's for, screenshots of every surface, install steps, what downloads and when, privacy in plain words, status, building), `CONTRIBUTING.md` with the project's rules and the writing rules for on-screen text, `SECURITY.md` pointing to GitHub's private reporting, a short code of conduct based on the Contributor Covenant, issue forms for bugs and ideas that warn against pasting private text, and a pull request checklist. Screenshots were taken of the app's own windows with made-up text, the voice muted, and Dave's settings put back afterwards; their black margins were made transparent so they sit well on light and dark pages.

**Found while taking them:** Dave's PC was locked, so the window in front was the Windows lock screen, and Compose offered to send to "LockApp". The lock screen, Start, search and the touch keyboard are now never a target (`KnownApps.IsSystemShell`, with tests). Also: the AI page showed the model folder with the Windows user name in it, now `%LOCALAPPDATA%\…`; and its Download button stayed visible after the model was there, now hidden.

**Left for Dave to decide before going public:** the personal note in `CLAUDE.md`, four early commits authored with a work email address, and whether to keep the history or start the public repository from a single commit.

**Step 5, how the app describes itself (9 October 2026, night).** Dave's call: the tools help far more people than one group, and the app must never read as a medical aid. Every public mention of a condition is gone from the README, the contributing guide, the issue forms, the brief's framing, the test fixtures and the two AI prompts; the prompts now say what to expect of the writing ("may misspell words, leave words out, or jump between ideas") instead of naming a condition. The brief records the decision in its table. CONTRIBUTING gains a rule: a tool, not a treatment. Two notices added as the licence suggests: "comes with no warranty" on the Privacy page and in the README, and "AI notes can be wrong, you decide" beside the notes and on the AI page. The README also says plainly it is a general reading and writing tool, not a medical device. Licence unchanged: GPL v3 or later.

**Security housekeeping for a public repository:** GitHub's CodeQL code scanning on every push, pull request and weekly, covering the C# and the workflow files, and Dependabot for library and action updates, grouped monthly.

**Smaller install.** LLamaSharp's CPU backend copies its native libraries for every platform into the output: Linux, Mac and ARM as well as Windows x64, about 80 MB the Windows install never uses. The app project now leaves them out of a publish for one runtime. The match had to allow for the package writing its paths with doubled separators (`runtimes\win-x64\...`); the first attempt dropped the Windows libraries too, caught because the local AI test failed to find them. Measured: the publish went from 268 MB to 191 MB; the local AI and the voice both load from it.

## Handover to a personal PC (10 October 2026, just after midnight)

Dave is moving the project off the first PC, where it lived in a Dropbox folder, onto his own computer, cloned from GitHub. Everything that had lived only in the chat or in the assistant's notes is now in the repository:

- `docs/STATUS.md`: what's built, what the maintainer has tried, what nobody has tried yet, known issues, what isn't built, the next steps in order, and the open decisions.
- `docs/DEVELOPING.md`: setting up a new PC, where every file lives in the repository and on the PC, all the developer switches, how to check each part by hand, the release steps, how to update a model's checksum, and every trap found while building, grouped by builds, Avalonia, Windows, AI and PowerShell.
- `tools/screenshots`: the scripts used for the README pictures, now one command (`capture.ps1`) that backs up the settings, plants a made-up draft, mutes the voice, captures each window by its exact title, puts the settings back byte for byte, and writes cleaned images. Run once end to end on this PC: settings identical afterwards. Its first run missed the Read button, which hides after three seconds; it now starts looking straight away.
- `CLAUDE.md` now points to the status and the handbook first, and carries the rules for an assistant on a real PC: no synthetic input, screenshots of the app's own windows only, settings backed up and put back.
- The brief's Dropbox note is replaced with "keep the checkout out of synced folders".

On the first PC the app was moved out of the project folder into `%LOCALAPPDATA%\Programs\Helpers`, where the installer would put it, and the Start with Windows entry now points there, so the project folder can be deleted without breaking the app.

## v0.1.0 released (10 October 2026, 00:10)

Tagged and released at Dave's word. Before the tag: the release workflow was given written release notes (`docs/releases/v0.1.0.md`) and a step to make sure Inno Setup was there. The Release run passed first time: installer 54 MB, zip 82 MB, `version.json` with both checksums, all on the Releases page, not marked pre-release so the update check's `releases/latest` address finds it.

**Checked from this PC:** `version.json` fetched from the exact address the app uses parses and points at the files. The installer was downloaded, matched its published SHA-256, installed silently over the hand-copied app in `%LOCALAPPDATA%\Programs\Helpers`, registered with Windows as "Helpers 0.1.0", added the Start menu entry, kept the Start with Windows entry, and started with the voice loaded.

**Not to plan:** an automated security review of the workflow commit flagged two things. Tag and version values were pasted into script text, so a crafted tag name could inject commands; only maintainers can push tags, but every value now reaches the scripts through environment variables, and the tag must look like a version. And the Inno Setup fallback would have fetched the newest Inno Setup from Chocolatey's public feed; GitHub's Windows images already include Inno Setup 6.7.1, so the fallback is gone and the release stops with a clear message if it's ever missing.

## Second PC (10 October 2026, small hours)

**Set up:** cloned to `C:\Code\helpers`. The first clone went into a Google Drive folder, where Drive's syncing made Git report five unchanged images as modified and held files open during the move, so the checkout now sits outside synced folders as the handbook says. Installed with winget, which worked on this PC: the .NET 10 SDK (10.0.401), Inno Setup 6.7.3 and the GitHub CLI; in VS Code, the C# Dev Kit and the Avalonia extension; and the Avalonia templates. The solution builds with no errors and all 264 tests pass. The development copy runs from the build folder at about 600 MB with the voice loaded, using the settings, voice, local model and dictionary already on the PC.

**Found:** v0.1.0 had been installed here in Inno Setup's all-users mode, which the installer's opening dialog allowed. That registers the app machine-wide while the files still go to `%LOCALAPPDATA%\Programs\Helpers` inside one user's profile. The app knows it was installed from the per-user uninstall entry only, so this copy would have offered the download page but never Update now. All users never made sense with the files in one profile, so the installer no longer asks: `PrivilegesRequiredOverridesAllowed=dialog` is gone, and every install is per-user, as the script's header always said. The quiet update passes no install-mode switch, so it is unaffected. The app was not taught to recognise machine-wide installs: the installer had been downloaded twice, by the release check and on this PC, and that copy is gone. The script change is not built locally; the next release builds it.

**Not to plan:**

- Dave chose to run the development copy only and removed v0.1.0. The uninstaller took its files, the Start menu entry and the Start with Windows entry, and left the settings and downloads alone, as designed. The second PC has no installed copy to try the update check on.
- The uninstaller ends every `Helpers.App.exe` by name, so it would also close a development copy started from `bin`. The development copy was started after it.

## Security pass (10 October 2026, early morning)

**GitHub settings.** Private vulnerability reporting, which SECURITY.md tells people to use, was off, as were the Dependabot alerts `dependabot.yml` assumes, and secret scanning. All three are on now, secret scanning with push protection, so a push containing a key is refused.

**Code scanning** had 265 open alerts, one of them about security: the build workflow had no `permissions` block. It now asks for read access only, like the scanning workflow; the repository's default was already read-only. 211 came from three checks that flag the design rather than problems in it: every declaration of and call to a Windows function in `Helpers.Windows` (152), and every `Path.Combine` (59). They are left out in `.github/codeql/codeql-config.yml`. The `Path.Combine` calls that touch downloads were looked at first: the dictionary zip and the voice archive are checked against built-in checksums before they are unpacked, and Windows' tar, run without `-P`, refuses paths outside the folder. Four were small and are fixed: `GesturePoint.DistanceTo` squared whole numbers, which could overflow on a gap of 46,000 pixels, and now works in floating point; `ReadingSession.Voice` and `Speed` were written under the lock but read without it, and now read under it like `CurrentIndex` (events are raised outside the lock, so it can't deadlock). That leaves 49 style notes, mostly broad catches and LINQ suggestions: a tidy-up list.

**Dependabot.** All five pull requests were green, and each build log showed all 264 tests ran, which matters for test-tool updates: a runner that finds no tests still passes. The test tools are merged from the command line, so each pull request shows as merged: coverlet.collector 10.1.0 (#3), Microsoft.NET.Test.Sdk 18.10.1 (#4) and xunit.runner.visualstudio 4.0.1 (#5). #3 and #4 change neighbouring lines, so that merge was resolved by hand. Avalonia 12.1.4 (#2) and actions/checkout 7 with actions/setup-dotnet 6 (#1) go into Dave's development copy to try first, as the handbook asks for library updates.

**Not to plan:** Smart App Control is on on the second PC, and it began blocking freshly built and newly downloaded files: the Windows test DLL first ("An Application Control policy has blocked this file", in the test host's `--diag` log and the Code Integrity event log), then the Core test DLL, Avalonia 12.1.4's source generator, so #2 can't build there, and finally the app's own DLL, so the development copy couldn't start. Not the updates: GitHub ran all 264 tests on the merged result, #2 built there, and code scanning went from 265 open alerts to the 49 style notes. Smart App Control is Dave's call.

**Then (11 October 2026).** Dave turned Smart App Control off. Everything builds on the second PC and all 264 tests pass there. Dependabot had replaced #2 with #6, Avalonia 12.1.4 alone, since the test runner was already newer. Dave tried the development copy on #6 and #1 and was happy, so both are merged and #2 is closed. Dependabot is caught up.

## The update path, tried (11 October 2026)

**Found:** the update check had never been tried end to end, and its last step couldn't have worked. Update now hands over to `cmd.exe` to run the installer once the app has gone, then start the app again. That line had six quotes, and with more than two `cmd /c` strips the first and the last, so the line fell apart; tried with harmless commands, cmd said "The filename, directory name, or volume label syntax is incorrect". In 0.1.0, Update now closes the app and installs nothing. Also, the What's new window added its button handlers again each time it was shown, so a second toast could start two downloads.

**Fixed:** `UpdateCheck.HandOverArguments` in Core builds the line under `/s`, with the whole of it in one more pair of quotes. It refuses a path with a quote or a percent sign, which cmd would misread; the app then offers the download page. Tests for both. The window is wired once, and a second Update now while a download runs is ignored.

**Version control:** 0.1.1 is set in `Directory.Build.props`, and the release workflow now refuses a tag that doesn't match it, so a build from source always says which version it is. The notes are in `docs/releases/v0.1.1.md`, and tell 0.1.0 users to install by hand.

**Tried on the second PC:** a Release publish, and two installers built from it with Inno Setup, labelled 0.1.1 and 0.1.2. 0.1.1 installed quietly for the user only: a per-user uninstall entry, nothing machine-wide, and no start-up entry with the task left out, which confirms yesterday's installer fix. With 0.1.1 running, the exact hand-over line ran the 0.1.2 installer while the app closed. Windows recorded 0.1.2, and the app started again 23 seconds after the hand-over. The test copy was uninstalled afterwards; settings unchanged, downloads kept.

**Not tried:** the check and the download against a real GitHub release. That needs 0.1.1 installed and a newer version published.

## A how-to guide, and code signing looked into (11 October 2026)

Dave: "so far everything works well", and asked for more of a summary and a how-to guide with pictures, and for code signing.

**The guide** is `docs/GUIDE.md`, linked from the top of the README. It starts with a table of what you want and what to do, then takes each part in turn: getting started, hearing text, the player, Compose, Edit and Put it back, the AI helpers, the word tools, every Settings page, updates, and what to do when something isn't working. It has 15 pictures. The capture script now also takes the small player, the menu and every Settings page. It plants the app's default lists of pronunciations and left-out apps instead of the user's own, and shows an ordinary volume on the Voice page.

**Found while taking them:**

- The Start-up page showed the settings file's full path, Windows user name included. It now shows `%APPDATA%\Helpers\settings.json`, as the AI page already did for the model folder.
- The Privacy and licences page still said Avalonia 12.1.3. It says 12.1.4 now, in the notices file too, and the handbook says to change both with each library update.
- The new Compose picture named the app that happened to be in front, and the local model hadn't finished after a minute, so the README keeps its Compose picture. The script now warns to check that line.
- The menu came out hidden until it was captured at once. Tidy is left out of the default run, because the local model may not finish in time.

**Code signing,** researched: no free route is open yet. SignPath's free plan wants visible use first, and has turned down projects with far more downloads; Microsoft's service takes individuals only in the US and Canada; a Certum certificate works now but signs by hand. Two catches for any route: Inno Setup's temporary installer file needs signing too, and about 70 of the libraries Helpers ships are unsigned, which Smart App Control may block whatever the installer carries. The options, sources and plan are in `docs/CODE-SIGNING.md`; the choice is Dave's.

**Dave's call, the same day:** unsigned for now, and leave it at that. Instead, the release workflow now records a build attestation for the installer, the zip and `version.json` with GitHub's `actions/attest` (v4): a record, signed through GitHub, that each file was built from this repository by its release workflow, which anyone can check with `gh attestation verify`. The 0.1.1 notes say how. It runs for the first time with the 0.1.1 release; if it fails, the release stops before anything is published.

## Renamed Unmuddle (11 October 2026)

Dave chose the name after looking at a few. Hark was the favourite until a search found a well-funded AI assistant already called that. "Making Sense" is an everyday phrase, and a software company, so it became the line instead: **Unmuddle, making sense of what you read and write**. "Helpers" stays as the code's name for its solution, projects and namespaces.

**What users see:** every window title, the tray, the messages, the welcome screen, the About line, the installer, the Start menu, and the program itself, now `Unmuddle.exe`. `HelpersInfo` holds the name, the line and the repository address; the links, the update address and the downloads' user agent come from it. The version is 0.2.0, which takes in the update fix that was going to be 0.1.1, and the 0.1.1 notes became the 0.2.0 notes.

**Moving existing copies across:** before anything reads its settings, `Program.Main` moves `%APPDATA%\Helpers` and `%LOCALAPPDATA%\Helpers` to the Unmuddle names, only when the new folder doesn't exist and never as a merge. It also moves the saved Claude key from `Helpers/anthropic-api-key` to `Unmuddle/anthropic-api-key`, removing the old one only once the new one is saved, and swaps the start-up entry. `DataFolderMigration` in Core has tests. The installer keeps the old AppId, so it replaces a Helpers install rather than sitting beside it; it installs to `Programs\Unmuddle`, closes a running Helpers first, and removes the old program and Start menu folders.

**Tried on the second PC:**

- The development copy's first start as Unmuddle moved Dave's real settings (byte for byte), his dictionary and 2.73 GB of downloads. The voice and AI settings carried on.
- Helpers 0.1.0, downloaded from its release and checked against its checksum, was installed quietly and not started. The locally built Unmuddle 0.2.0 installer then upgraded it: one entry in Apps, now "Unmuddle 0.2.0", the old program and Start menu folders gone, nothing added to start-up. The release build ran. It was uninstalled afterwards, with the data kept.
- 275 tests pass. The pictures that showed the old name were retaken.

**Not to plan:**

- The program's name is the assembly name, and every `avares://` resource address uses it, so they all moved to `avares://Unmuddle/`.
- The first scripted replacements in the docs and the capture script failed on the backslashes in paths, and were redone from a file.
- The look on the second PC was Calm by the time of the rename (it was Custom earlier that night), so the retaken pictures came out blue beside the others. The capture script now sets the same Custom look for every picture.

Afterwards the local checkout moved from `C:\Code\helpers` to `C:\Code\unmuddle`, to match; it rebuilt and passed every test from there.

## v0.2.0 released (11 October 2026)

Tagged at Dave's word, the first release as Unmuddle. The Release run passed first time, including the new attestation step: installer 51.9 MB, zip 77.8 MB, and `version.json`, marked as the latest release.

**Checked from the second PC:** the app's update address returns 0.2.0, and so does the old Helpers address through GitHub's redirect, so a 0.1.0 copy with the check switched on will be offered it. The installer downloaded from the address in `version.json` matched its checksum. `gh attestation verify` passed: SLSA provenance from `.github/workflows/release.yml` at `refs/tags/v0.2.0` in `drhumphrey/unmuddle`. The repository's description and topics now say what Unmuddle is.

**Not done, on purpose:** 0.2.0 isn't installed on the second PC, which runs the development copy; two copies would fight over the shortcuts. Install it there when the next version is ready to try the update for real.

## Three fixes from daily use (13 November 2026)

Dave, after a month with 0.2.0 installed: the app can be started more than once, the "Couldn't claim the shortcut" message is a nuisance, and the Read button turns up too often, when opening or moving files.

**One copy at a time.** `SingleInstance` in `Helpers.Windows` holds a named event for the sign-in session. `Program.Main` claims it before anything else. A second copy finds it taken, signals it and closes; the first shows "Unmuddle is already running. Its icon is by the clock." Two tests, against real named events.

**The read shortcut.** Ctrl+Alt+Space was taken on Dave's PC with Unmuddle closed. Windows doesn't say by whom; the Claude desktop app was running and uses those keys. The default is now Ctrl+Alt+H, for "hear it". Ctrl+Alt+R was the first thought and was taken on the same PC, as were several other R combinations. A copy whose saved shortcut is still Ctrl+Alt+Space tries it first at start-up; if Windows refuses, it saves Ctrl+Alt+H instead and says so in a message that fades, with Open Settings. If neither is free, the old error shows as before. A shortcut the user chose is never changed. Compose stays on Ctrl+Alt+C: the second message in Dave's first picture came from a second copy of the app, which can't happen now.

**The Read button.** A double-click or a short drag counted as a selection whatever was under the mouse, so opening a file, dragging one inside a window, or dragging a box around several all brought the button up. It now looks at the mouse pointer when the button goes down and when it comes up (`CursorShape` in `Helpers.Windows`, the rule in `PointerShapes` in Core, with tests). The ordinary arrow, hand or resize pointer at the press, and no I-beam at the release, means no text. A pointer the app drew itself is allowed as before, so nothing that worked should stop.

**Tried on Dave's PC:** 282 Core tests and 5 Windows tests pass. The new build was started twice with 0.2.0 running: the second copy closed within four seconds, the first stayed, the saved shortcut became Ctrl+Alt+H and Windows showed it as held. The settings file was backed up first and put back byte for byte.

**Not tried:** the Read button change with a real mouse, in Explorer and in the apps on the acceptance list. Nothing sends synthetic clicks on this PC, so that is Dave's to try.

**Not to plan:**

- The checkout is in a Google Drive folder again, and Git reported 15 unchanged images as modified, as on the second PC in October. Builds were sent outside it with `--artifacts-path`. It should move out of Drive.
- Selecting text that starts and ends on a link shows the hand pointer at both ends, so the Read button won't appear there. The shortcut still reads it.

## The Read button waits to be asked (13 November 2026)

Dave, the same evening, with 0.2.1 installed: the button still appears near whatever is selected, and most of the time he doesn't want it. He selects to copy and paste far more often than to read, and in Illustrator, PowerPoint or Blender selecting is constant. He suggested a shortcut, or two keys and a right-click. Asked, he chose a keyboard shortcut only, and "only when asked" as the default for everyone.

**Built:**

- `ReadButtonAfterSelection` in settings, off by default, replaces `ReadButtonEnabled`. The old name is no longer read, so every existing copy moves to the new way on update.
- A third global shortcut, Ctrl+Alt+J by default (`ReadButtonHotkey`), shows the button at the mouse for six seconds. Read, Edit and Paste then act on the window in front. Ctrl+Alt+B and Ctrl+Alt+M were taken on Dave's PC; J was free, and isn't an AltGr character on UK, German, Spanish or Polish keyboards.
- The button hides on any key press, and the shortcut's own keys are still down, and repeating, when it appears. Key presses are ignored until 350 ms pass without one, so holding the shortcut can't hide what it just showed. The keyboard hook still carries no key codes.
- A copy from before the change is told once, in a message that stays 20 seconds (`ToastService.Notice`), with Open Settings going to the Read button page. A new copy is told on the welcome screen, whose switch now reads "Show a Read button every time I select text" and starts off.
- Settings, Read button page: "Show when I ask" with its shortcut box first, then "Show after every selection".

**Tried on Dave's PC:** 282 Core and 5 Windows tests pass. With the installed copy stopped, the new build started, held Ctrl+Alt+J, and saved the new settings with the told flag set. Settings were backed up and put back byte for byte, and the installed copy restarted.

**Also learned:** the update from 0.2.0 to 0.2.1 worked on Dave's PC through Check now and Update now, the first time the whole path has run against a real release.

**Not tried:** pressing Ctrl+Alt+J and using the button. No synthetic key presses on this PC, so that is Dave's to try.