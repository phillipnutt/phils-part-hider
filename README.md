# Phil's Part Hider

> **This mod was written by an AI.** Every line of code and these docs were
> written by Claude, Anthropic's AI, at Phil Nutt's direction. Phil designed it
> and play-tests it, but he isn't a programmer and no human has reviewed the
> code.

A small Kerbal Space Program 1.12 mod for big mod lists: hide the parts you
never use from the VAB and SPH parts list, with one click. It's one of Phil's
mods, alongside [Phil's Iterative Parts](https://github.com/phillipnutt/phils-iterative-parts),
and shares its look.

- **Hide:** middle-click (press the scroll wheel on) any part icon in the parts
  list. It disappears from both editors, in every save.
- **See what's hidden:** the orange eye toolbar button opens the Hidden parts
  window, laid out like the stock parts list: the VAB's categories down the
  left (with how many parts each one hides), and the hidden parts as the same
  3D icons the parts list uses, spinning when you point at them.
- **Sort** by Name, Mod, Recent, Mass, Cost or Size. Click the same sort again to
  reverse it.
- **Search** by part name, mod, maker or category.
- **Bring one back:** click it. The bottom bar offers **Undo** for a few
  seconds. **Unhide all** asks first.
- The bottom bar shows the part you point at: its mod, category, mass, cost and
  size.

Hidden parts are still loaded. Phil's Part Hider only filters the editor's
parts list, so craft and ships that already use a hidden part load and fly as
before, and nothing about your saves changes.

## Install

Copy `GameData/PhilsPartHider` from the release zip into your KSP `GameData`
folder. No other mods needed.

## The list

One file for every save: `GameData/PhilsPartHider/PluginData/HiddenParts.cfg`,
one `part = name` line per hidden part, oldest first. Delete a line (with KSP
closed) to unhide that part. To start over, delete the file. If you remove a mod
whose parts you had hidden, the window lists those names as not installed, with
a button to forget them.

## Building

MSBuild on `PhilsPartHider.csproj`. It expects KSP at the default Steam
location; pass `/p:KspDir="..."` for another install, or `/p:InstallToGame=false`
to build without copying into the game. The toolbar icon is drawn by
`Tools/Draw-Icon.ps1`; everything else in the window is drawn in code.

## License

MIT. See [LICENSE](LICENSE).
