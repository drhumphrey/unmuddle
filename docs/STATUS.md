# Status

Where the project stands, what's been checked, what hasn't, and what comes next. Written 10 October 2026, when work moved from the first PC to a personal one. Update it as things change; the detail of how each part was built is in [MILESTONES.md](MILESTONES.md).

## In one line

The app is now called **Unmuddle**, "making sense of what you read and write" (11 October 2026; it was Helpers). Version 0.2.0, the first as Unmuddle, is released on GitHub (11 October 2026), after 0.1.0 as Helpers (10 October 2026). It works on both PCs: reading aloud, the Read button, Compose, the AI helpers on the PC and on Claude, the word tools, settings, the installer and an opt-in update check. Some parts haven't been tried by a user yet.

## Built

| Milestone | What | State |
|---|---|---|
| 0 to 2 | Solution, voice spike, text pipeline | Done |
| 3 | Player, reading view, tray, toasts, vibes | Done, used daily |
| 4 | Selection capture and Ctrl+Alt+Space | Done, checked in Teams, VS Code, Chrome and Outlook |
| 5 | Read button and styled quick menu | Done |
| 6 | Tabbed Settings, pronunciations, readable text, Start with Windows | Done |
| 7 | Packaging: installer, release workflow, welcome screen | Done. v0.1.0 released as Helpers, v0.2.0 as Unmuddle, each checked from the published release; from 0.2.0, every release file has a build attestation |
| 8 | Compose: spelling, Read back, Send to chat, drafts, Edit and Put it back, Paste on the Read button | Done |
| 9 | AI helpers: Check my thinking, Tidy, Make a request; on this PC (Qwen3 4B) and Claude | Done for those three actions |
| 10 | Claude provider, key storage, cost shown, first-use confirmation | Built early, as part of 9 |
| Extras | Word tools, Privacy and licences page, opt-in update check, code scanning, Dependabot, a how-to guide with pictures ([GUIDE.md](GUIDE.md)) | Done |

267 Core tests and 3 Windows tests pass, on both PCs. Every push builds and is scanned on GitHub.

## Tried by the maintainer

- Voices, play, pause, stop, replay, speed, volume, output device.
- The shortcut in Teams, VS Code, Chrome and Outlook.
- Watch clipboard's offer to read.
- Messages on the main monitor with three monitors at different scaling.
- Compose's look, spelling underlines and the right-click menu, after the fixes.
- The welcome screen and the Settings pages, after the fixes for text running off the edge.
- A second PC: v0.1.0 installed and ran; the development copy builds, passes every test, and runs from the build folder.

## Fixed in 0.2.1

From Dave's use of 0.2.0, 13 November 2026. Version 0.2.1 carries them. Detail in [MILESTONES.md](MILESTONES.md).

- Only one copy runs at a time. Starting it again shows a message from the copy already running.
- The read shortcut's default is Ctrl+Alt+H. A copy still on Ctrl+Alt+Space moves across by itself when another app has that one.
- The Read button no longer appears when opening, dragging or boxing files. Built and unit-tested; Dave still to try it in daily use.

## Changed in 0.2.2

- The Read button waits to be asked: Ctrl+Alt+D shows it at the mouse. Appearing after every selection is a setting, off by default. A copy from before is told once. Built, and the shortcut seen to register; Dave still to try the button itself.

## Changed in 0.2.3

- Updates has its own page in Settings, out of Privacy and licences. The guide's pictures of the Settings pages need retaking: the Read button, Updates and Privacy pages have all changed.

## Changed in 0.2.5

- Closing the player stops the voice. Built; Dave still to try it.
- A play mark on the tray icon, and in the Unmuddle menu, while the voice is reading. The icon was drawn and looked at; the tray and the menu themselves are Dave's to check.

## Not tried yet

- **Send to chat** and **Put it back** into a real chat box, and that the clipboard is unchanged afterwards.
- **Edit** and **Paste** on the Read button.
- **Use this window**, and Ctrl+Alt+C picking up the right window.
- **Check my thinking** and **Tidy** on real writing, in daily use.
- **Claude** in the cloud. No key has been entered yet.
- **Word tools** in daily use.
- **The update check** against a real release. The quiet install and restart were tried with two local installers on the second PC; the check and the download need 0.2.0 installed and a newer release published.
- **Start with Windows** after a real sign-in, from the installed location.
- From the acceptance list: Word, PDFs in a browser and Acrobat, Notepad, Explorer renaming, Windows Terminal, the Claude desktop app.

## Known issues and limits

- The local AI takes 10 to 20 seconds per action and uses about 4.7 GB of memory with the voice while loaded. It unloads after 10 idle minutes.
- The local model sometimes mis-quotes a word in its notes, and once changed "Ok" to "Okay" despite being told to keep informal words.
- Output device names are cut at 31 characters.
- Syllables come from hyphenation patterns, so a few split oddly ("in·form·a·tion").
- The app isn't code-signed, so Windows SmartScreen warns on first run.
- In 0.1.0, Update now can't start the installer, so it closes the app without updating. Fixed in 0.2.0.
- The v0.1.0 installer offers to install for all users. A copy installed that way never offers Update now. Fixed for the next release, which installs per-user only.
- Code scanning lists 49 style notes, mostly broad catches and LINQ suggestions: a tidy-up list, not bugs.
- On a PC with Smart App Control on, Windows may block the unsigned app outright rather than warn. Code signing would help. On the second PC it blocked development builds until it was turned off ([DEVELOPING.md](DEVELOPING.md)).
- The local AI is refused on processors without AVX2, though the library ships a fallback build that might work on older ones. Worth testing before relaxing the check.
- FlaUI brings in about 25 MB of Windows Forms. Calling UI Automation directly would remove it.
- Kokoro has no text normaliser. Numbers, dates and money are handled by rules in `NumberSpeech`; each new oddity heard becomes a rule with a test.

## Not built yet

- **Summarise** and **Explain simply**: the instructions are written and editable in Settings, but there are no buttons. The brief puts them in the player and Compose, with a small result card beside the player.
- The brief's three-way **AI result card** overlay for the player.
- A **microphone choice**. Dictation uses Windows' own voice typing (Win+H), which already works in Compose.
- **macOS** (milestone 11).

## Next, in order

1. **Promotion:** a short demo animation in the README, then a few posts.
2. **Copies of Helpers 0.1.0** (the first PC, if it's still there) can't update themselves: install 0.2.0 by hand from the [Releases page](https://github.com/drhumphrey/unmuddle/releases).
3. **Try the update check for real** when the next version comes: install 0.2.0 first (the second PC runs the development copy only), then let it update.
4. **Try everything under "Not tried yet"**, fix what turns up, and log it.
5. **Code signing.** Unsigned for now, with build attestations instead. Apply to SignPath once Unmuddle has visible use ([CODE-SIGNING.md](CODE-SIGNING.md)).
6. **Summarise and Explain simply** buttons, and the player's result card.
7. **A browser extension** for Chrome and Edge, to put Read and Edit on the right-click menu.
8. **macOS.**

## Decisions still open

- How to promote it, and where.

Decided 11 October 2026: the name is Unmuddle, with "making sense of what you read and write" as its line. Ship unsigned for now, with a build attestation on every release file, and come back to SignPath once Unmuddle has visible use.
