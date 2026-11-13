# Privacy and security

What the app touches, what it keeps, what it sends, and a look at every component it uses. Written 9 October 2026 after Dave asked for the licences to be listed in Settings and for a check that local AI stays local. The same text, shortened, is on the Privacy and licences page in Settings.

## The short version

- Nothing phones home. No telemetry, no crash reports. No update checks unless you switch them on.
- **The update check is opt-in.** The first-run screen asks; the Updates page in Settings has the switch. On, the app asks GitHub for the latest release's version file once a day, a while after it starts. GitHub sees your IP address and the app's version in the request, nothing else. A new version is offered in a message, never installed by itself. "Update now" downloads the installer, checks it against the checksum in the version file, and runs it quietly. Decided by Dave on 9 October 2026, opt-in "specially as we're doing the free approach".
- The voice, the local AI model and the dictionary are each downloaded once, over HTTPS, when you first use that feature, and checked against a checksum built into the app. After that they never touch the network.
- **Local AI keeps everything local.** The model runs on your processor through llama.cpp. The model file cannot make network connections; nothing in the app sends your text anywhere while "On this PC" is chosen.
- **Claude in the cloud sends your text to Anthropic.** Only when you have chosen Claude in Settings, pasted your own key, and pressed a button that carries the cloud mark. What goes: your draft and the instructions for that button. What does not: your name, other text, history, anything from other apps. The app pins the address to api.anthropic.com so an environment variable cannot redirect it. Anthropic's handling of what you send is governed by their API terms and privacy policy, not by this app; read them before pasting a key.
- Your API key lives in Windows Credential Manager, encrypted by Windows with your sign-in, never in the settings file.

## What the app reads from other apps

- **The selection**, only when you press Read or Edit on the pill, or the shortcut. First through Windows UI Automation; if that gives nothing, by sending a copy shortcut and reading the clipboard, after which everything that was on the clipboard is put back.
- **The clipboard**, when Watch clipboard is on, to offer a Read button for new text. Off by default. The offer fades; nothing is read until you press it.
- **The mouse**, through a low-level hook, to notice a drag-select, a double-click or a triple-click: positions and buttons only.
- **The keyboard**, through a low-level hook, only to notice that a key was pressed so the Read button can hide. The hook carries no key codes. It cannot see what you type.
- **Never** password fields, which UI Automation marks. Never apps on the excluded list, which starts with password managers and remote desktop.
- **Never** windows running as administrator, which Windows blocks anyway.

## What the app keeps on this PC

| What | Where |
|---|---|
| Settings, including the unsent Compose draft and your month's token counts | `%APPDATA%\Unmuddle\settings.json` |
| Your dictionary words | `%APPDATA%\Unmuddle\dictionary.txt` |
| The voice (about 350 MB), the local AI model (about 2.5 GB) and the dictionary data (about 10 MB) | `%LOCALAPPDATA%\Unmuddle\models` |
| The Claude API key | Windows Credential Manager, under "Unmuddle/anthropic-api-key" |

Nothing else. No logs that contain your text.

## Every component, and whether it uses the network

| Component | Licence | Network |
|---|---|---|
| .NET runtime 10 | MIT | No |
| Avalonia 12 (UI, Fluent theme, colour picker) | MIT | No |
| sherpa-onnx 1.13 (voice engine; includes ONNX Runtime, MIT, and espeak-ng phoneme data, GPL-3.0) | Apache-2.0 | No. The voice package is downloaded once from its GitHub release. |
| Kokoro voice v1.0 (hexgrad), with misaki's British pronunciation list | Apache-2.0 | No |
| NAudio 3.1 | MIT | No |
| Markdig 1.4 | BSD-2-Clause | No |
| FlaUI 5.0 (UI Automation) | MIT | No |
| LLamaSharp 0.27 and llama.cpp | MIT | No. The model is downloaded once from Hugging Face, SHA-256 checked. |
| Qwen3 4B and 1.7B (GGUF) | Apache-2.0 | No |
| Anthropic C# SDK 12.55 | MIT | **Yes, to api.anthropic.com, only on a cloud-marked button.** |
| Lexend, Atkinson Hyperlegible fonts | SIL OFL 1.1 | No |
| Open English WordNet 2025 | CC BY 4.0 | No. Downloaded once from its GitHub release, SHA-256 checked. |
| British English hyphenation patterns (Wujastyk and Toal) | MIT | No. Bundled. |

Also used, as part of Windows: the spell checker, Credential Manager, UI Automation, the clipboard.

## Licence compatibility

The app is GPL-3.0-or-later. MIT, BSD-2-Clause and Apache-2.0 code can be combined into a GPL-3 program. The SIL Open Font Licence covers the fonts, which are bundled unchanged with their licence files. CC BY 4.0 is one-way compatible with GPL-3 for adapted material, and the WordNet data is used unchanged with attribution. espeak-ng's data is GPL-3 itself. Nothing here conflicts.

## Things reviewed and changed

- The Anthropic SDK reads `ANTHROPIC_BASE_URL` from the environment if set. The app now sets the base URL itself, so a planted environment variable cannot redirect a draft and a key elsewhere.
- The SDK would also read `ANTHROPIC_API_KEY` from the environment. The app always passes the key from Credential Manager and never builds a client without one.
- The voice package download had no checksum. One is now built in and checked, like the model and dictionary downloads.
- Downloads go to a `.part` file and are moved into place only after the checksum matches, so a broken download leaves nothing behind.
- The local model is loaded from a file path the app computes; it never runs anything from the download beyond reading weights.

## Things to keep an eye on

- Anthropic's API terms can change. The Settings page links to them rather than quoting them.
- Hugging Face and GitHub are trusted for the one-off downloads because the checksums are fixed in the app; a changed file is refused, not used.
- The update check is the one network call the app makes on its own, and only when opted in. It fetches one small JSON file from GitHub and nothing more. The installer it can download is checked against the checksum in that file; once installers are code-signed, the signature should be checked too.
