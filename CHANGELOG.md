# Changelog

What changes from one release to the next, newest first. Add each user-visible change under **Unreleased** as it is
made; on release, the section is renamed to the version and becomes the GitHub release notes.

## [Unreleased]

### Added
- A module patched into itself (an output back into its own FM or feedback input) shows its cable, a tight loop
  from its lowest output just under its box into its lowest input, crossing nothing; such modules count as feedback
  loops (folded boxes hint at them, `inspect` lists them).
- The Info tab lists the patch's feedback loops.
- The Info tab groups modules of the same kind on one row, with their count; a click on it lists the instances.
- The Info tab links to one VCV Library page with all the patch's modules, as Rack does for missing modules, to add
  them to your account.
- A **Cardinal compatibility** option, on by default, in the Info tab and the Diagram tab's Options: turned off, the
  Info tab's Cardinal column and the module sheet's Cardinal note go away, for Rack-only users. Remembered by the browser.

### Changed
- Feedback cables are drawn dash-dot (scope links keep their plain dashes) instead of carrying a "↺ feedback" tag; the
  standalone SVG legend explains it.
- Renamed from VCV Patch Tools to **Modular Patch Tools** (VCV is a trademark of VCV): the repository is now
  github.com/neilbgr/ModularPatchTools, the web app neilbgr.github.io/ModularPatchTools (the old address redirects,
  and what the browser remembered is kept), and the command line `modpatch` instead of `vcvpatch`.

### Fixed
- A module that hears an oscillator and goes back into its FM, sync or CV input is a feedback loop, drawn as such,
  instead of an insert tucked under the oscillator. Inserts still need to return into an input that takes audio.
- In a loop, the cable going back into a modulation input (a mixer's level CV, an oscillator's FM…) is the one drawn
  as feedback, so the signal path keeps reading left to right.
- The audio interface the sound leaves through stays in the last column, even when a loop could pull it back.
- An insert that also feeds another module (a reverb in a filter's insert feeding a detector) no longer has its
  send arrow landing on its own output: the send and the return take the top slot of their side.

## [1.3.1] - 2026-11-04

### Fixed
- Modules tagged with one of Rack's other names for a tag ("Drums", "Percussion", "Clock", "S&H", "EQ"…) take the
  role of that tag instead of a guessed controller; unknown modules named like drums ("Kick", "Snare", "Perc"…) are
  guessed as voices.

### Changed
- Web app: the notes under the diagram fold to one line ("⚠ 3 notes about this patch"), opened by a click, so the
  diagram takes the whole page. The Info tab still lists them in full.

## [1.3.0] - 2026-11-03

### Added
- The top bar shows the version; its menu gives the release date, the build and links to the source, the release notes
  and the command-line tool.

### Fixed
- Modules tab: the editor's buttons follow the dark theme.

### Changed
- Web app: scopes and displays show as boxes next to what they watch by default, instead of hidden. A scope view saved
  in the browser before this change gives way to the new default once.

## [1.2.0] - 2026-11-03

### Added
- Aluminium Fader, Faders2, Faders4 and Buttons: named ports, the Performance role, and listed as bundled with the
  neilbgr edition of Cardinal.
- The ports of Aluminium's Fader, Faders2, Faders4, Buttons and PadX take the label typed on their panel, as the module
  itself names them ("Fader 2" becomes "Cutoff").
- A port tab for a stereo pair reads "L/R send", "L/R Ch 2"… instead of "2 × audio".
- Every module of the VCV Library is known by name and tags, closed-source ones included (NYSTHI, FLAG, Vult…), so its
  role and function are no longer guessed. Its ports are still shown by number.
- Port names for VCV Fundamental's newer modules (Process, Push, Logic, Compare, Gates…) and HoyerHoppes.
- Venom Bay Input / Bay Output / Bay Norm: the wireless links are drawn as the direct cables they stand for, so the
  signal path no longer stops at a bay.
- Info tab: a link to report published modules without port names as a pre-filled GitHub issue.
- Modules tab: describe the modules the catalog doesn't know (your own plugin, one not published yet), with their name,
  role, tags and port names, or import them from the plugin's sources (files or a .zip of the repository), read in the
  browser. Descriptions are kept in the browser, and can be exported and imported as a JSON file; a commented template
  holds an entry for each unknown module of the patch.
- CLI: `--catalog <file>` adds your own module descriptions to `info` and `diagram`; `catalog scan --src <plugin>` writes
  such a file from a plugin's sources.

### Fixed
- Limiters, compressors and low-pass gates (Bogaudio LMTR…) are modifiers, not guessed controllers.
- Modules of a private or not yet published plugin no longer link to a VCV Library page that doesn't exist, and their
  diagnostic says so instead of pointing at the catalog files.
- Diagnostics give one line per module and no longer ask to edit `catalog/roles.json`.
- VCV Fundamental modules use the names of the current release ("ADSR EG"): the copy Cardinal bundles is older.
- A mixer fed by a single voice whose mix goes back into that voice is part of it, not of the bus: a switch picking
  either the oscillators mixed together or one of them at a time (Moog Matriarch style) keeps both paths in the voice.
- A docked expander's outputs (Venom's Mix Aux Send) no longer leave from the same spot as its base's outputs with the
  same port numbers: the send has its own exit, and a send/return loop has its own tabs on its host. The loop is drawn
  square in its own channels, like every other cable, instead of curved.

## [1.1.1] - 2026-10-16

### Fixed
- A single voice going straight to the outputs, with no mixer, keeps its VCA and filter: an envelope passing audio
  through its own VCA (Surge XT EG x VCA) is in the sound path, and only the effects at the end of the chain go to
  "Mix & effects".

## [1.1.0] - 2026-10-16

### Added
- Port catalog: about 2,700 modules (about 1,700 in 1.0.0), with 39 open-source plugins Cardinal doesn't bundle, listed in
  `catalog/extra-sources.txt` at pinned commits (SickoCV, 4ms, FrozenWasteland, Nozoid, TinyTricks, dbRackModules,
  SquinkyLabs, Geodesics…) and fetched by `scripts/fetch-extra-sources.sh`.
- `vcvpatch catalog build` accepts several `--src` folders.
- Surge XT: templated modules (VCOs, effects) and the VCO knob names, read from the Surge engine.
- Source scanner: 4ms MetaModule modules, DHE, Sapphire, mscHack mixers, SquinkyLabs composites, enums of any name,
  ports named in nested loops or with `string::f` (`%d`, `%s`, `%c`), C string escapes.

### Changed
- A MIDI CC learned on a port is typed by the port it drives (gate on a clock input), not by its General MIDI meaning.
- A gate into a router's clock or trigger input reads "steps to the next channel".
- Unnamed ports show as `#1`, `#2`… as in Rack.

## [1.0.0] - 2026-10-14

First release.

- **Diagram**: a patch as a macro block diagram, by role (bands) and signal path (columns), cables typed by what they
  carry, layers, folded overview, intents and diagnostics; exports to interactive HTML, SVG, Graphviz, Mermaid and JSON.
- **Info**: what a patch file is and holds (Rack or Cardinal, format, modules with their role and Cardinal support).
- **Convert**: patches between VCV Rack and Cardinal, with warnings for modules official Cardinal doesn't load.
- Web app (Blazor WebAssembly, runs in the browser) and `vcvpatch` command line for Linux, Windows and macOS.

[Unreleased]: https://github.com/neilbgr/ModularPatchTools/compare/v1.3.1...HEAD
[1.3.1]: https://github.com/neilbgr/ModularPatchTools/compare/v1.3.0...v1.3.1
[1.3.0]: https://github.com/neilbgr/ModularPatchTools/compare/v1.2.0...v1.3.0
[1.2.0]: https://github.com/neilbgr/ModularPatchTools/compare/v1.1.1...v1.2.0
[1.1.1]: https://github.com/neilbgr/ModularPatchTools/compare/v1.1.0...v1.1.1
[1.1.0]: https://github.com/neilbgr/ModularPatchTools/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/neilbgr/ModularPatchTools/releases/tag/v1.0.0
