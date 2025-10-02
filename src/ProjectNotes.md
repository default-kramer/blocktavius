# Structure
Blocktavius.Core should contain general-purpose code that could apply to DQB or Minecraft or any similar voxel game.
This project should have no dependencies.

Blocktavius.DQB2 is intended for DQB2-specific code.
This project should depend on Blocktavius.Core only.

Blocktavius.AppDQB2 is a WPF GUI that will allow the user to manipulate a DQB2 stage.
Large-scale terraforming is the primary goal, but other functions will be desired.
This GUI uses the "Eye of Rubiss" as a companion app to quickly show previews of edited stages,
which allows much faster iteration than having to save the file, load it in DQB2, and look around.

# Code
## Naming
Ranges that are [inclusive, inclusive] should use "Min" and "Max" names.
Ranges that are [inclusive, exclusive) should use "Start" and "End" names.
Also consider using "Length" or "Count" instead, which might improve clarity.
