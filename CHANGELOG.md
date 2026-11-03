# Changelog

What changes from one release to the next, newest first. Add each user-visible change under **Unreleased** as it is
made; on release, the section is renamed to the version and becomes the GitHub release notes.

## [Unreleased]

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

[Unreleased]: https://github.com/neilbgr/VcvPatchTools/compare/v1.1.1...HEAD
[1.1.1]: https://github.com/neilbgr/VcvPatchTools/compare/v1.1.0...v1.1.1
[1.1.0]: https://github.com/neilbgr/VcvPatchTools/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/neilbgr/VcvPatchTools/releases/tag/v1.0.0
