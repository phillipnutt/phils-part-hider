# Changelog

## Unreleased

- Simpler code inside, no change in what the mod does. Several things were
  each decided in more than one place; now each has one rule: when the parts
  list catches up with the hidden list, when the window redraws, which hidden
  parts are still installed, how buttons are colored, and what a middle-click
  over the window does. Window buttons now act straight away instead of
  waiting a frame.

## 1.0.0 (2026-11-06)

First release.

- Middle-click a part icon in the VAB/SPH parts list to hide it.
- Hidden parts window, in the look of Phil's Iterative Parts and laid out like
  the stock parts list: the VAB's categories down the left with a count on each,
  the stock part icons in a grid, sort by Name, Mod, Recent, Mass, Cost or Size,
  search, and the pointed-at part's details in the bottom bar.
- Click a part to unhide it, with Undo. Unhide all asks first. Parts whose mod
  is gone are listed by name, with a button to forget them.
- One hidden list for every save, in `GameData/PhilsPartHider/PluginData`.
