# Helpers: build brief 2.0

Working title: **Helpers**. Now called **Unmuddle** (decided 11 October 2026); this brief keeps the working title it was written with. A small tray app that helps people read, write and think things through when working with AI assistants such as Claude, Claude Code and ChatGPT: for anyone who finds a wall of text or a careful request hard work. Windows 10/11 first; macOS is a stated goal (see below).

It does three jobs:

1. **Hear it.** Select text in any app and a small Read button appears. Click it and a natural offline voice reads the text. It understands the Markdown that AI chats produce.
2. **Write it.** A Compose window with spelling as you type, a button to hear your draft read back, and a Tidy button that fixes grammar and puts your thoughts in order without changing what you mean. One click sends the result to the chat.
3. **Shape it.** Turn rough notes or a client's request into a clear instruction for the AI. Turn a long AI reply into a short summary or plain language for someone else.

Everything runs on the user's own machine. Nothing leaves it unless the user switches on a clearly labelled cloud feature and supplies their own API key.

This brief replaces brief 1.0, which covered reading only. The changes are listed at the end.

`prototype/ReadAloud.ahk` (AutoHotkey v2) is a working prototype of the reading part. Use it as the reference for clipboard capture, clipboard restore and plain-text clean-up. Don't port AutoHotkey itself.

## Who it is for

People who are strong systems thinkers and creatives but are not natural typers, find long replies hard to read, and find it hard to put thoughts in order on the page. Many of them are the go-between for clients or a team and an AI chat. Dave is the first user and the reviewer.

## Working agreement

- Build milestone by milestone, in the order below. At the end of each one, stop and show Dave what works, with a short note of anything that didn't go to plan.
- Ask before changing any decision in the next section, and before adding dependencies beyond those listed.
- Unit-test everything in `Helpers.Core`.
- Never commit API keys. Never put user text in logs.
- UK English in all UI text, comments and docs.
- Keep the principles: free for everyone, no tiers, offline first, no telemetry.

## Decisions already made

| Area | Decision |
|---|---|
| Licence | GPL-3.0 (already in the repo). All dependencies below are compatible. |
| Runtime | C# on .NET 10 (LTS). Windows first, x64. macOS after v0.2. `Helpers.Core`, `Helpers.Speech` and `Helpers.Ai` must have no Windows dependencies, so the Mac version is a new shell, not a rewrite. |
| UI | Avalonia 12 (MIT) with its Fluent theme and the Inter UI font the template ships, following the OS light/dark setting and accent colour. Decided 7 October 2026 so one UI runs on Windows and macOS. There is no main window: the app lives in the tray and shows small overlays. See Surfaces. |
| Speech | sherpa-onnx (NuGet `org.k2fsa.sherpa.onnx`, Apache-2.0) running Kokoro (Apache-2.0 weights). Fully offline. Runs on Windows and macOS. |
| Default voice | bm_george (speaker 26), chosen by Dave on 8 October 2026 after the milestone 1 listen. Any British Kokoro voice can be picked in Settings. Kokoro v1.0 includes bf_emma, bf_isabella, bf_alice, bf_lily, bm_george, bm_lewis, bm_daniel and bm_fable. The package is `kokoro-multi-lang-v1_0`; speaker IDs 20 to 27 are the British voices (bf_alice 20, bf_emma 21, bf_isabella 22, bf_lily 23, bm_daniel 24, bm_fable 25, bm_george 26, bm_lewis 27). No int8 build of it exists as of October 2026. |
| Audio | NAudio (MIT) on Windows, behind an `IAudioOutput` interface in Core. |
| Reading the selection | UI Automation first (UIA3 via FlaUI.UIA3, MIT, or direct COM interop). Clipboard as fallback. Both behind `ISelectionSource`. |
| Markdown | Markdig (BSD-2) in Core to parse Markdown into spoken text. No regex-only Markdown handling. |
| Spelling | On Windows, the Windows spell-check engine through its `ISpellChecker` COM API, with the underlines drawn by our own Avalonia adorner. Language en-GB. User dictionary supported. Free, offline, nothing to ship. On Mac, `NSSpellChecker` behind the same interface. |
| Grammar and rewriting | A language model behind one `IAssistant` interface. Local provider first, cloud provider second. See AI helpers. |
| How AI help is shown | As notes pinned to spots in the user's own text: a question or a minimal fix each, accepted one at a time. Never a silent rewrite, because rewrites lose the writer's tone and can quietly drop a point. Tidy is spelling and grammar only, with every change marked. Decided by Dave on 9 October 2026. |
| The editing loop | Select text anywhere, press Edit on the pill, work on it in Compose, press Put it back to paste it over the original. Decided by Dave on 9 October 2026. |
| Word tools | Meanings and synonyms from Open English WordNet (CC BY 4.0, downloaded once, 10 MB). Syllables from the TeX British English hyphenation patterns (MIT, bundled). Pronunciation from the British word list that ships inside the voice package, shown as IPA and as a plain respelling. Google and Wiktionary open in the browser. Approved by Dave on 9 October 2026. |
| Updates | Opt-in, asked on the first-run screen and switchable on the Privacy page. On, the app fetches the latest GitHub release's `version.json` once a day and offers a newer version in a toast; Update now downloads the installer, checks its SHA-256, runs it silently and restarts. Off, the app never contacts anything on its own. Chosen by Dave on 9 October 2026. |
| How the app is described | A general tool that helps people read, write and think things through. Public text leads on what it does, never on a condition or a diagnosis, and never makes a claim that it diagnoses, treats or compensates for a medical condition, so it stays clearly a reading and writing tool rather than a medical device. Decided by Dave on 9 October 2026. |
| Licences and privacy in the app | A Privacy and licences page in Settings lists every component with its licence and whether it uses the network, and says in plain words what leaves the PC. The list lives in code (`ThirdPartyNotices`) and in `docs/THIRD-PARTY-NOTICES.md`; the review is `docs/PRIVACY.md`. Asked for by Dave on 9 October 2026. |
| Local model | LLamaSharp (MIT) with the CPU backend. Default model: Qwen3-4B instruct, Q4_K_M GGUF (Apache-2.0), run with thinking off. Fallback for low-memory PCs: Qwen3-1.7B. Confirm the exact model at milestone 9 by testing on Dave's laptop. |
| Cloud model | Optional, bring-your-own-key. First provider is Claude through the official Anthropic C# SDK (NuGet `Anthropic`). Default model ID `claude-haiku-4-5` because it is the cheapest and these are simple tasks. The model ID is a setting; `claude-sonnet-5-5` gives better rewrites at higher cost. |
| Dictation | Use the operating system's own voice typing (Win+H on Windows, the dictation key on Mac). It works in the Compose window already. Nothing to build in v1. |
| Readable fonts | Bundle Lexend and Atkinson Hyperlegible (both SIL Open Font Licence). System font stays the default. |
| Privacy | Offline by default. Cloud features are opt-in, per feature, and clearly labelled. No telemetry. |
| Tests | xUnit, the `dotnet new xunit` default, in `Helpers.Tests`. |

Prerequisites on the dev machine: the .NET 10 SDK (`winget install Microsoft.DotNet.SDK.10`, or the installer from dot.net if winget hangs) and the Avalonia templates (`dotnet new install Avalonia.Templates`). Both installed on Dave's PC on 7 October 2026. VS Code with the C# Dev Kit is enough. Inno Setup is needed for the packaging milestone.

## Open questions for Dave

These are decided below so work can start. Overrule any of them.

- **Product name.** Resolved 11 October 2026: **Unmuddle**, "making sense of what you read and write", chosen by Dave. "Helpers" stays as the solution, project and namespace name in the code.
- **Publish v0.1 early.** The plan releases the reading half on its own as v0.1 before Compose exists. I think that is right: it's useful on day one.
- **Five AI actions.** Check my thinking, Tidy, Make a request, Summarise and Explain simply. All are prompt templates behind the same plumbing, so each extra one costs little. Cut any you don't want. Dave's steer on 9 October 2026: the help that matters most to a writer who finds writing hard is not spelling and grammar, which inline checkers already do, but a reader saying "I don't follow this bit" or "something is missing here", without rewriting.
- **Dropbox.** Resolved 8 October 2026: the checkout stays in Dropbox, but `.git` and every `bin` and `obj` folder are marked as Dropbox-ignored, so Dropbox never touches git internals or build output. See Gotchas for the command to run when a project is added.

## User experience

### Surfaces

There is no main window. The app lives in the tray and shows a small surface only when it has something to do. Every surface shares one visual language (see Look and feel).

| Surface | When it appears | Takes focus? | Goes away |
|---|---|---|---|
| **Read pill** | After a mouse selection: Read, Edit, and Paste when the clipboard holds text | Never | After 3 s, or on any click, key or scroll elsewhere |
| **Player** | When reading starts | Never | A few seconds after reading ends (setting), or Stop |
| **Reading view** | Player expanded | Never | Collapse, or with the player |
| **Toast** | Short confirmations and errors: "Copied", "Sent to Visual Studio Code", "Couldn't grab the text from this app", "Loading voice…", "Downloading model, 43%" | Never | 3 s for confirmations. Errors stay until clicked. Progress stays until done. |
| **AI result card** | After Summarise or Explain simply from the pill or player | Never | Dismiss, or "Use this" |
| **Compose** | From the tray, hotkey or pill | Yes, it has a text box | Close, Send to chat, or Put it back |
| **Settings** | From the tray | Yes | Close |
| **First run** | First launch only: pick a voice, hear it, done | Yes | Done |

Rules for every surface:

- Appears on the monitor where the cursor is. The pill and the AI result card appear near the cursor; toasts appear above the tray; the player remembers where it was left.
- Esc dismisses whichever surface is on top.
- A toast may carry one action button: "Copy instead", "Open Settings", "Retry". Never more than three toasts on screen; older ones collapse.
- Toasts are the only place errors appear. No modal error dialogs, ever.
- Overlays never steal focus from the app being read or written in. Only Compose, Settings and First run are normal windows.

### Look and feel

The aim is a modern, attractive app that feels native on Windows 11 and on Mac, and is calm to use.

- **One visual language.** Rounded corners, a soft shadow, a translucent background where the OS supports it (Mica or acrylic on Windows 11, vibrancy on Mac) with a solid fallback. The OS accent colour marks the one primary action on each surface.
- **Vibes.** The shapes never change; the skin does. Neon is the default: gradient edges, a soft glow and flecks of bright colour, in the spirit of an RGB gaming keyboard. Custom lets the user pick the gradient. Calm is the quiet glass look with one flat OS accent, one click away in the tray menu for screen sharing. The gradient touches chrome only; reading text stays plain and high contrast in every vibe. Details and rules in `DESIGN.md`.
- **Light and dark** follow the OS by default, with an override in Settings, because Neon wants dark.
- **Type is large by default.** Readable-text settings (font, size, spacing, tint) apply to every surface that shows the user's text, not only the reading view.
- **Big targets.** Nothing clickable is smaller than 32 px, because the pill is clicked mid-selection and the player is clicked without looking.
- **Motion is short and quiet.** Fades of 150 ms or less, no bounces. Honour the OS reduced-motion setting.
- **Icons** from Fluent UI System Icons (MIT). Looks native on Windows 11 and fine on Mac.
- **No sounds** by default. A reading app shouldn't ding.
- **Design doc.** Mockups of each surface, both themes, go in `docs/DESIGN.md` before milestone 3 starts.

### Read button (the main trigger)

- **When it appears:** after a mouse text selection (drag-select, double-click or triple-click). It's a small pill near the cursor with three buttons at most, because it is clicked mid-selection: **Read** with a speaker icon; **Edit**, which opens Compose with the selected text and remembers the window it came from; and **Paste**, shown only when the clipboard holds text, which pastes over the selection. Summarise lives in the player and in Compose, not on the pill. (Changed 9 October 2026 from a single Read button, for the editing loop.)
- **Focus:** it must never steal focus. Use a non-activating window.
- **When it must not appear:**
  - window drags, file drags and scrollbar drags
  - clicks on our own windows
  - apps on the excluded list
- **When it hides:** after about 3 seconds, or on any click elsewhere, key press or scroll.
- **Clicking "Read"** captures the selection and starts reading.
- **Keyboard selections** (Shift+arrows) don't trigger the pill. The optional hotkey covers those.

### Player

- **Window:** a small floating bar. It stays on top, is draggable, remembers its position and can be minimised to the tray.
- **Controls:**
  - play/pause and stop
  - back/forward one sentence
  - speed slider: 0.5x to 2.0x in 0.1 steps, default 1.0x
  - voice picker
  - expand/collapse
- **Compact mode** shows the current sentence, highlighted as it's read.
- **Expanded mode (reading view)** shows the whole cleaned text in a readable font, the current sentence highlighted, and lets you click any sentence to jump there. This is the view for long AI replies.
- **When it shows:** it appears when reading starts. It hides a few seconds after reading ends; this is a setting.
- **Focus:** clicking its buttons must not take focus from the app being read.

### Compose window

A plain window for writing to the AI. Opened from the tray, the hotkey, or the pill.

- **Target.** When Compose opens it remembers the window that had focus, and shows it in the title bar: "Sending to: Visual Studio Code". The user can change the target by clicking another window and pressing "Use this window".
- **Editor.** A multi-line text box with spelling as you type (red underline, right-click suggestions, "Add to dictionary"). Readable-text settings apply. The operating system's voice typing works here with nothing extra.
- **Buttons.**
  - **Read back.** Reads the draft with the same voice and player.
  - **Check my thinking.** The reader's notes: where the logic jumps, what is missing, which word is unclear. Questions and minimal fixes pinned to the text, never a rewrite. Needs an AI helper (see below). Greyed out with a hint if no model is set up.
  - **Tidy.** Spelling and grammar only, every change marked. Keeps the words and the tone. Needs an AI helper.
  - **Make a request.** Rewrites the draft as a clear instruction to a coding assistant. Needs an AI helper.
  - **Send to chat.** Pastes the text into the target window. Never presses Enter. The user presses Enter themselves.
  - **Put it back.** The same button when Compose was opened by Edit on the pill: pastes the text over the original selection in the window it came from.
  - **Copy.** Puts the text on the clipboard and says so.
- **AI results** never replace the draft silently. Notes appear beside the draft, each pinned to a spot in the text; clicking a note highlights its spot. A note with a fix has "Apply"; a note that is a question is answered by editing. Tidy's changes are marked in place with "Apply", "Apply all" and "Keep mine". Every applied change goes through the editor's own undo, so Ctrl+Z reverses it.
- **The editing loop.** Select your own half-written text anywhere, press Edit on the pill, fix it in Compose with the spelling, the notes and Read back, then Put it back. Decided 9 October 2026.
- **Word tools.** Right-click any word, "Look up". A small window: the word in syllables, how to say it (a plain respelling with the stressed syllable in capitals, and IPA), Hear it and Slowly, the meanings with an example, other words for it as chips that replace the word, and Google and Wiktionary in the browser. Added 9 October 2026 at Dave's request.
- **Drafts** auto-save to settings every few seconds and come back when Compose reopens.

### Readable text

Applies to the reading view and the Compose editor. Lives in Settings.

- font: system default, Lexend or Atkinson Hyperlegible
- text size
- line spacing
- background tint: none, cream or grey
- highlight colour for the current sentence

### Tray icon

The menu has:

- Read clipboard
- Watch clipboard (toggle: when on, any new text copied is read straight away; handy with the Copy button on an AI reply)
- Pause/Resume
- Stop
- Show player
- Compose
- Settings
- Pause the Read button for 1 hour
- Calm look (toggle)
- Start with Windows
- Exit

### Hotkeys

Off by default and configurable. Suggested defaults:

- Ctrl+Alt+H: read the current selection, or stop if already reading. (Ctrl+Alt+Space until 13 November 2026; changed at Dave's word because other apps claim it, the Claude desktop app among them.)
- Ctrl+Alt+C: open Compose, targeting the current window.

Note that Ctrl+Alt is AltGr on many European keyboards, so these must be easy to change.

### Settings window

- voice, with a preview button
- default speed
- Read button on/off and its delay
- excluded apps (by process name)
- hotkeys on/off and the keys themselves
- pronunciation dictionary editor
- readable text (above)
- look: vibe (Neon, Custom, Calm), gradient colours or follow the OS accent, glow, flecks, theme override
- Markdown reading options (below)
- AI helpers: provider choice, local model download and status, cloud key and model, cost so far, prompt templates
- Start with Windows

## Architecture

One solution, `Helpers.slnx` (the .NET 10 solution format), with these projects:

| Project | Responsibility |
|---|---|
| `Helpers.App` | The UI: tray, pill, player, Compose and Settings windows, composition root. |
| `Helpers.Core` | Text clean-up (plain and Markdown), sentence splitter, pronunciation dictionary, settings model, prompt templates, diff for AI results, cost estimates. Interfaces: `ISpeechEngine`, `IAudioOutput`, `IAssistant`, `ISelectionSource`, `ITargetWindow`, `ISpellChecker`. No platform dependencies. |
| `Helpers.Windows` | Mouse hook, UIA capture, clipboard capture and restore, paste-to-target, non-activating window helpers, Windows spell checker, start-with-Windows registration. A future `Helpers.Mac` implements the same interfaces. |
| `Helpers.Speech` | sherpa-onnx Kokoro engine and the sentence-ahead synthesis queue. Platform-neutral. |
| `Helpers.Ai` | `IAssistant` implementations: LLamaSharp local provider, Anthropic cloud provider. Model download with progress and SHA-256 check. Platform-neutral. |
| `Helpers.Tests` | Unit tests for Core. |

Threading:

- **Mouse hook:** the low-level mouse hook (`WH_MOUSE_LL`) runs on its own thread with a message loop. The callback only queues the event and returns immediately.
- **Speech worker:** one worker owns the sherpa-onnx `OfflineTts` instance. Create it once and reuse it; never call it from more than one thread.
- **Look-ahead:** synthesis runs one or two sentences ahead of playback, so audio starts fast and never gaps between sentences.
- **AI worker:** one worker owns the LLamaSharp model. Requests queue. Output streams token by token into the result view so it feels alive.

## Component details

### Selection capture

1. **UIA first.** Get the focused element. If it supports TextPattern, read `GetSelection()`.
   - Never read password fields (`IsPassword`).
   - Time-box UIA calls to about 150 ms. Chromium and Electron build their accessibility tree lazily, so the first query in Chrome, Edge, Teams, VS Code or new Outlook can be slow or empty. Expect the clipboard route to be the common path in VS Code and in chat panels.
2. **Clipboard fallback.**
   - Save the clipboard (every format you can), then clear it.
   - Send **Ctrl+Insert**, not Ctrl+C. Ctrl+Insert copies in Win32, Office, Chromium, Electron and terminals, and never means "interrupt". In VS Code's built-in terminal and in Claude Code, a stray Ctrl+C can kill a running task.
   - Wait up to 300 ms for text. If nothing arrives and the window is not a terminal-class window (Windows Terminal, console windows, VS Code, Cursor), send Ctrl+C and wait up to 1 s more.
   - Restore the original clipboard. The user's clipboard must never end up changed.
3. **Elevated windows.** When we're not elevated, don't attempt capture from admin windows (Windows blocks it anyway). Show a short hint instead.

### Text clean-up (Core, unit-tested)

**Plain text rules**, ported from the prototype:

- URLs become "link"
- bullet characters are stripped
- blank lines are collapsed
- a line break with no punctuation before it becomes a pause (". ")

**Added rules:**

- Email addresses read as "email address" (a setting).
- File paths such as `src/Helpers.Core/Splitter.cs` read as "file Splitter dot c s" (a setting: full path, file name only, or "file").

**Markdown mode.** Detect Markdown (fences, headings, list markers, inline backticks, bold stars) and parse it with Markdig. Then speak it like this:

- headings: read the text, then a longer pause
- bold and italic: read the text only
- inline code: read the text only, with the pronunciation dictionary applied
- code blocks: say "code block, 12 lines" and skip the contents. Setting: skip, read first line, or read all.
- lists: a short pause before each item; numbered lists say the number
- tables: say "table, 3 columns, 5 rows" and read each row as "column name: value". Setting to skip tables.
- links: read the link text; say "link" only if there is no text
- horizontal rules and HTML: skip
- block quotes: read normally

**Pronunciation dictionary.** User-editable, whole-word matching, optional case-sensitivity. Ship a starter list for things AI chats say a lot: `npm`, `JSON`, `async`, `regex`, `UIA`, `GGUF`, `EVAR → ee-var`. Store it in settings.

**Sentence splitter.** It must cope with abbreviations ("Dr.", "et al.", "e.g.", "Fig. 2", decimals, version numbers like "v2.0", file names). Cap chunks at about 400 characters for the engine.

### Speech engine

- **Loading:** load the model in the background at app start. If a read is requested before it's ready, show "Loading voice…" in the player.
- **Speed:** use the engine's own speed parameter, not audio time-stretching.
- **Preview:** the voice preview speaks a fixed sample sentence.
- **Model files:** they live in `%LOCALAPPDATA%\Helpers\models\`. Either the first run downloads them (with progress and a SHA-256 check) or the installer ships them; decide at the packaging milestone. Make the path configurable for offline installs.
- **Memory:** add an "Unload voice when idle for N minutes" setting.

### Playback

- **Queue:** a streaming queue behind `IAudioOutput` with pause/resume, skip back/forward a sentence, and stop. NAudio on Windows.
- **Output device:** use the system default output device, and cope with device changes (headphones plugged in mid-read) without crashing.

### Compose: spell check

- Behind `ISpellChecker` in Core so the Mac version can use the Mac's own checker.
- On Windows: call the Windows `ISpellChecker` COM API from `Helpers.Windows`, check the paragraph around the caret on a short debounce, and draw red underlines with an Avalonia adorner over the `TextBox`. Right-click shows the suggestions and "Add to dictionary". Spike this first; it is the one piece of Compose with any technical doubt.
- Windows needs the English (United Kingdom) language features installed for en-GB spelling. Detect the case where no checker is available and show a one-line hint with the Settings page to open.
- The user dictionary is a file in `%APPDATA%\Helpers\`. "Add to dictionary" writes to it. The pronunciation dictionary and the spelling dictionary are separate things.

### Compose: send to chat

- Remember the target window handle when Compose opens.
- On Send: save the clipboard, put the text on it, activate the target window, send Ctrl+V (Shift+Insert for terminal-class windows), wait about 500 ms, restore the clipboard.
- If the target window has gone, say so and offer Copy instead.
- Never send Enter.

### AI helpers

The principle: keep the voice local because it's free, private and fast. Use a language model only for what rules can't do: grammar, reordering, summarising, rephrasing.

**Interface.** `IAssistant` in Core has one method: run a named action on some text and stream the result. Core holds the five prompt templates, editable in Settings and resettable to defaults.

**The principle for help with writing** (Dave, 9 October 2026): spelling and grammar fixes do not help with the thing that actually trips most people who find writing hard, which is the order of thought and the steps that got skipped because they were obvious to the writer. A full rewrite "fixes" that by replacing the writer's voice, which is worse. So the help is a reader's notes: "I don't quite follow this bit", "something is missing between these two sentences", "which 'it' is this?", each pinned to the spot, each a question or the smallest possible fix, accepted one at a time.

**The five actions.**

| Action | Prompt intent | Where it appears |
|---|---|---|
| Check my thinking | Read as a careful colleague. Return notes, each pinned to a quoted span: where the logic jumps, where context the reader needs is missing, where a word or reference is unclear, where a word is missing. Each note is one short question or one minimal fix. Never rewrite. Never add facts. Output as a list of {span, kind, note, fix?}. | Compose |
| Tidy | Fix spelling and grammar only. Keep every word that isn't wrong, every fact, and the user's voice. Don't reorder, don't add, don't remove. Output as a list of {span, fix}. | Compose |
| Make a request | Rewrite notes as a clear instruction to a coding assistant. Keep every fact. Don't invent requirements. Use numbered steps if there are several asks. Mark anything unclear with [check]. | Compose |
| Summarise | Give the main points in up to five short bullets, then one line saying what the reader needs to do, if anything. | Player, Compose |
| Explain simply | Rewrite for a non-technical reader. Short sentences. No jargon. Keep it accurate. | Player, Compose |

**Test material.** A handful of Dave's own messages from this project's chat, with his permission given on 9 October 2026, as fixtures for the prompt work. They are good examples: a missing word here, a step skipped there, exactly what a spell checker cannot see and a reader can.

**Local provider (LLamaSharp).**

- Loads the GGUF model lazily on first use and unloads after N idle minutes (setting).
- Run Qwen3 with thinking off (`/no_think` in the system prompt or the chat-template flag), or Tidy will take forever.
- Context window 4,096 tokens is enough. Cap input at about 3,000 words and say so above that.
- Model download from Hugging Face on first use, with progress, SHA-256 check and a cancel button. Model path configurable.
- The CPU backend needs AVX2. Detect its absence and say so rather than crash.

**Cloud provider (Anthropic).**

- Use the official Anthropic C# SDK. Send only the action's system prompt and the text. No history. Stream the reply.
- `max_tokens` about 1.5x the input tokens, minimum 256.
- Cap input at 4,000 characters by default (configurable) and warn above it.
- Cache results by a hash of (action, text, model) for the session.
- Record the `usage` token counts the API returns and show a running monthly cost estimate in Settings.
- API key stored with Windows Credential Manager or DPAPI (Keychain on Mac), never in plain-text settings.
- Transparency: a cloud icon on every button that would send text off the machine, and a first-use confirmation that says exactly what will be sent.
- Leave room for an OpenAI-compatible provider (which also covers Ollama and LM Studio) behind the same interface. Not v1.

**Showing results.** Every AI result appears beside the original with changes marked (word-level diff from Core). Buttons: Use this, Keep mine, Read it. Nothing is replaced silently.

## Privacy and security

- No telemetry. No selected text, drafts, AI results or audio in logs or crash reports.
- Settings and drafts live in `%APPDATA%\Helpers\settings.json`. Secrets live in Credential Manager.
- Never read password fields.
- Skip excluded apps. The default list is common password managers (1Password, Bitwarden, KeePass) and `mstsc.exe`.
- Cloud calls happen only when the user clicks a button marked with the cloud icon, over HTTPS, with no extra metadata.
- The local model never touches the network after download.

## Performance targets

Measure and report these at the milestone where each first applies, then adjust:

- **Idle CPU:** about 0%.
- **Memory with the voice loaded:** aim for under 400 MB. Measured at milestone 1: 450 MB after load, 700 MB after three paragraphs. See `MILESTONES.md` for the things to try.
- **Memory with the local language model loaded:** report it. Expect about 3 GB for the 4B model. Unload when idle.
- **Time to first audio:** under 1 s from clicking Read, on a typical work laptop, for a normal sentence.
- **Pill delay:** the pill appears within 200 ms of mouse-up.
- **Tidy on a 150-word paragraph, local model, CPU only:** first words within 3 s, finished within 20 s on a typical work laptop. If slower, default to the 1.7B model.

## Packaging

- **Build:** `dotnet publish`, self-contained, win-x64.
- **Native libraries:** make sure sherpa-onnx's and llama.cpp's native DLLs load correctly in a single-file publish (check `IncludeNativeLibrariesForSelfExtract`), or ship them next to the exe.
- **Outputs:** a zip, and an Inno Setup installer that installs per user and needs no admin rights.
- **Models are never in the installer or the repo.** The voice is small enough to consider bundling; decide at milestone 7. The language model is always a download.
- **Start with Windows:** use the HKCU Run key.
- **Signing:** optional. Note in the README that unsigned builds trigger a SmartScreen prompt on first run.
- **CI:** a GitHub Actions workflow on `windows-latest` that builds and runs the tests on every push, and attaches the zip to a release when a tag is pushed.

## Milestones

Demo each one before starting the next.

0. **Scaffolding.** Install the .NET 10 SDK and the Avalonia templates. Create the solution and empty projects. CI builds and runs an empty test. Sort out the Dropbox question.
1. **Engine spike.** A console app that reads a paragraph aloud with Kokoro via sherpa-onnx, using two British voices at 1.0x and 1.5x. Report model load time, time to first audio and memory use. This is the voice-quality check: if the voices aren't good enough, stop here.
2. **Text pipeline.** Plain-text clean-up, Markdown mode, pronunciation dictionary and sentence splitter in Core, with tests. Test input includes real Claude Code replies.
3. **Player and tray.** Starts with a spike: a non-activating, translucent Avalonia overlay that never takes focus. Then read clipboard text with the full player, sentence highlighting, the expanded reading view, toasts and Watch clipboard. The design doc is done before this starts.
4. **Selection capture.** UIA plus the clipboard fallback, tested against the checklist below.
5. **Read button.** Mouse hook, show/hide rules, non-activating window, multiple monitors and DPI.
6. **Settings.** Pronunciation dictionary, readable text, Start with Windows, excluded apps, hotkeys.
7. **Packaging.** Installer, model download, CI release. **Ship v0.1: reading only.**
8. **Compose.** Spell check, read back, send to chat, drafts. No AI yet. Added 9 October 2026: the editing loop, Edit and Paste on the pill and Put it back in Compose.
9. **AI helpers, local.** `IAssistant`, the LLamaSharp provider, model download, the five actions, the notes view beside the draft with Apply per note. Check my thinking is the one to get right; Tidy is the easy one. Confirm the default model on Dave's laptop.
10. **AI helpers, cloud.** The Anthropic provider, key storage, transparency and cost tracking. **Ship v0.2.**
11. **macOS.** See "macOS version" below. Only after v0.2 and only if Dave still wants it.

## Acceptance checklist

**Reading.** For each app: select text with the mouse, click Read, hear it, and confirm the clipboard is unchanged afterwards.

- Word
- Outlook (classic and new)
- Teams (new)
- Chrome and Edge, including claude.ai and chat.openai.com
- a PDF in Edge and in Acrobat Reader
- Notepad
- VS Code: the editor, the built-in terminal, and the Claude Code chat panel. Confirm Ctrl+Insert copies in the terminal and that no Ctrl+C is ever sent there.
- Claude desktop app
- File Explorer: renaming a file must not trigger the pill
- Windows Terminal: no Ctrl+C is sent when nothing's selected

**Markdown.** Copy a real Claude Code reply containing headings, a list, a code block and a table. Read it. Code is skipped with a count, the table is read row by row, no stars or hashes are spoken.

**Reading view.** Expand the player on a long reply. Click a sentence in the middle. Reading jumps there.

**Compose.** Type a sentence with three misspellings. Red underlines appear. Right-click fixes one. Read back works. Send to chat pastes into the VS Code chat box without submitting, and the clipboard is unchanged afterwards.

**AI, local.** With no network, Tidy a messy paragraph. Changes are marked. Keep mine restores the original. Make a request turns three lines of notes into numbered steps.

**AI, cloud.** With a key set, the cloud icon is visible, the first-use confirmation appears once, and the cost figure in Settings moves.

**Also:**

- No pill when dragging a window by its title bar, dragging files or resizing windows.
- Double-clicking a word shows the pill, and clicking elsewhere removes it.
- If the clipboard holds an image or rich text before a read, it's still intact afterwards.
- With two monitors at different scaling, the pill appears next to the cursor on both.
- The laptop sleeping or headphones being plugged in mid-read causes no crash.
- Selecting text in a password field reads nothing.

## Gotchas

- **Non-activating windows in Avalonia:** there is no built-in flag. Get the Win32 handle with `TryGetPlatformHandle()` once the window is created, set `WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TOPMOST`, set `ShowActivated = false`, and answer `WM_MOUSEACTIVATE` with `MA_NOACTIVATE` through a window-procedure hook. Prove this works for the pill and the player as the first task of milestone 3, before building anything on it.
- **Avalonia tray icon:** use Avalonia's own `TrayIcon`. It works on Windows and macOS.
- **Avalonia transparency:** set `TransparencyLevelHint` to a list (Mica, acrylic, then transparent, then none) so Windows 10 and Mac fall back cleanly. Test both themes on both.
- **Low-level hooks:** Windows silently removes them if the callback is slow. Never do UIA or clipboard work inside the callback.
- **UIPI:** a non-elevated app can't read from or send keys to elevated windows.
- **Clipboard restore:** some formats are delayed-rendered or app-private and can't be restored. Restore what you can (text, Unicode text, RTF, HTML, images, file lists) and test with Office.
- **DPI:** declare per-monitor v2 DPI awareness in the app manifest.
- **Electron accessibility:** VS Code, Teams and the Claude desktop app expose a full UIA tree only when they detect assistive technology. Don't rely on UIA there. VS Code users can set `editor.accessibilitySupport` to `on` to help, but we must work without it.
- **Windows spell check:** needs the en-GB language features installed in Windows. Without them the checker reports no errors at all, which looks like perfect spelling. Detect it and say so.
- **LLamaSharp backends:** reference exactly one backend package (`LLamaSharp.Backend.Cpu`). Mixing backends causes native load failures.
- **Qwen3 thinking:** on by default and makes short tasks slow. Turn it off.
- **Synced folders:** keep the checkout out of Dropbox and OneDrive. The first checkout lived in Dropbox, with `.git`, `bin`, `obj` and `out` excluded through an NTFS stream Dropbox honours (`Set-Content -Path <folder> -Stream com.dropbox.ignored -Value 1`). From October 2026 the project is cloned from GitHub into an ordinary folder instead.
- **More:** `docs/DEVELOPING.md` lists every trap found while building, from Avalonia quirks to PowerShell argument quoting.

## macOS version (milestone 11, after v0.2)

What carries over unchanged: `Helpers.Core`, `Helpers.Speech`, `Helpers.Ai` and the Avalonia UI in `Helpers.App`. sherpa-onnx, Kokoro, Markdig and LLamaSharp all run on macOS, including Apple Silicon.

What needs a Mac implementation (`Helpers.Mac`):

- **Selection capture:** the Accessibility API (`AXUIElement`), which needs the user to grant Accessibility permission in System Settings. Clipboard fallback sends Cmd+C.
- **Mouse events:** a `CGEventTap`, which also needs Accessibility permission.
- **Tray:** a menu bar item (`NSStatusItem`).
- **Audio:** an `IAudioOutput` on Core Audio, or a cross-platform library such as PortAudio.
- **Spell check:** `NSSpellChecker`.
- **Send to chat:** Cmd+V to the target app.
- **Secrets:** Keychain.
- **Packaging:** a signed and notarised `.app` in a `.dmg`. Apple Developer membership is a yearly cost; without it the app shows a scary warning on first open.

## Later (not in v1)

- offline dictation with Whisper (Whisper.net, MIT) for better punctuation than the system's voice typing
- an OpenAI-compatible cloud or local provider (covers Ollama and LM Studio)
- reading text in images via the Windows OCR API
- "Save as MP3"
- per-app voice and speed
- an ARM64 Windows build
- a cloud premium voice
- language translation
- Linux (Avalonia makes it plausible; nobody has asked)

## What changed from brief 1.0

- **Scope widened** from a read-aloud app to a three-job reading and writing companion: hear it, write it, shape it.
- **macOS added as a stated goal** after v0.2, with the Mac work listed.
- **UI toolkit changed from WPF to Avalonia** (decided 7 October 2026) so one UI serves Windows and Mac. Spell check moves from WPF's built-in to the Windows `ISpellChecker` API with our own underlines.
- **Surfaces and Look and feel sections added.** No main window; a small set of non-activating overlays with one visual language; errors only ever appear as toasts; a design doc with mockups comes before milestone 3.
- **Markdown mode added** to text clean-up, using Markdig, because AI chat output is Markdown and reading it raw is painful.
- **Expanded reading view** added to the player, with click-to-jump.
- **Compose window added**, with system spell check, read back, send to chat and drafts.
- **AI helpers added** behind one interface: four actions, local model first, cloud second. Results are always shown beside the original, never swapped in silently.
- **Language translation removed** from v1. In 1.0 "Translate" meant English to another language. What Dave needs is "turn this into a clear request", which is now Make a request.
- **Clipboard fallback now sends Ctrl+Insert first**, and never sends Ctrl+C to VS Code or terminals, because a stray Ctrl+C can kill a running Claude Code task.
- **Model ID corrected.** 1.0 named a Haiku model that doesn't exist. The current one is `claude-haiku-4-5`. The official Anthropic C# SDK exists, so that open question is closed.
- **Readable fonts and text settings added** (Lexend, Atkinson Hyperlegible, size, spacing, tint).
- **Watch clipboard** added to the tray.
- **Projects renamed** from `ReadAloud.*` to `Helpers.*`, plus a new `Helpers.Ai` project and platform-neutral interfaces for audio and spelling.
- **Milestones reordered** with a scaffolding step first, a text-pipeline milestone with real test data, a v0.1 release of the reading half before Compose starts, and macOS last.
- **Dropbox warning** added to the open questions.
