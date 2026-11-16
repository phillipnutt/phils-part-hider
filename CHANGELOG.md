# Changelog

## Unreleased

Fixes:

- Middle-click hides a part only when its parts-list icon is the top thing
  under the mouse: not through a menu or dialog, from an inventory slot, or
  from another mod's window.
- F2 hides the Hidden parts window with the rest of the UI. While hidden, it
  no longer blocks clicks or the camera.
- "Forget them" no longer drops Phil's Iterative Parts parts that belong to
  another save (PIP makes its parts per save, so in other saves they look
  missing).
- The small corner button on a tile now brings the part back like the rest of
  the tile, instead of changing that part's default paint everywhere.
- If the hidden list can't be read, it's kept as
  `HiddenParts.cfg.unreadable-<date>` before a new list is saved over it.
- When the window refills, the old tiles leave the layout at once.

Also:

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
