# Boosters

`ToolBase` displays the HUD slot; `GameController` and `BoosterFlow` execute gameplay actions.
`Resources/BoosterUnlockConfig.asset` is the editable ScriptableObject list of keys, milestone levels, titles, descriptions and icons (`BoosterDefinition`).
`BoosterUnlockConfig` reads that list and caches immutable lookup values; runtime progress never mutates the config asset.
`BoosterUnlockProgress` stores confirmed introductions and their single free use through `GamePrefs`.
`GameLevelPresentationFlow` opens `FeatureUnlockDialog`, then runs `BoosterFlyPresenter` and `BoosterTutorialGuide`.
`Resources/BoosterPresentation.prefab` owns the FlightIcon and Tutorial UI; enable the relevant child to preview it in Prefab Mode.
`BoosterPresentationUI` instantiates that prefab and selects the active presentation part; references are serialized in the prefab.
`BoosterTutorialRegistry` chooses Undo, Shuffle, Path or default interactive steps.
Introduction = popup confirmation and flight; free use = a successful inventory transaction, not a button tap.
Extra Slot = `InventoryKeys.TOOL_PATH`; Undo prerequisite = a valid move recorded in `GameData.UndoData`.
