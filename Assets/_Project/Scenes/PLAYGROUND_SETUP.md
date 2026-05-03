# Lost Breadcrumbs Playground Setup

1. Open Unity Editor (2022.3+).
2. Open any scene (for example `Assets/Scenes/SampleScene.unity`).
3. Run `LostBreadcrumbs/Setup/Build Full Playground` from the top menu.
4. Confirm hierarchy root `Scene_Root` is created.
5. Press Play.

Debug controls:
- `F3`: Toggle debug overlay
- `TAB`: Cycle tracked enemy in overlay
- Overlay now shows telemetry (`Learn Phase`, score, weights) and per-enemy learning weights
- Runtime HUD is now available in play mode (HP/Stamina/Objective/Cooldowns + top danger alert banner)
- Runtime HUD right-top now shows live event feed (save/load, stage generate, death, ability, objective)
- Event reactions are now connected: screen flash + camera impulse by event type (death/stage/objective/ability/save-load)
- Event audio is now connected: per-event dummy tone feedback with volume rules (mute toggle supported)
- AudioManager Inspector supports `Assigned SFX (Optional)` slots; when clips are assigned, clip playback is prioritized over dummy tones
- AudioManager now includes per-event rules: priority, burst limiter attenuation curve, per-event cooldown, and mixer group override
- Runtime combat ducking is now enabled (enemy threat-driven): music/ambience levels are ducked automatically via sources or mixer exposed params
- Dummy loop audio runtime now auto-generates/plays fallback BGM+Ambience loops when clips are missing (quick audible verification)
- `WASD` or arrow keys: Move player dummy
- `LeftShift` / `RightShift`: Sprint (consumes stamina, increases movement noise)
- `Space`: Emit Echo noise
- `F`: Toggle flashlight + small noise
- `Q`: Cast Echo Pulse (nearby stun + large noise, cooldown)
- `E`: Deploy Decoy (periodic lure noise, cooldown)
- `R`: Deploy Smoke Screen (temporarily blocks enemy vision, cooldown)
- `F5`: Save checkpoint (run save)
- `F9`: Load checkpoint
- `F10`: Start new run (clear checkpoint)
- `F11`: Run regression checklist (map stage 1/3/5, pressure scaling, death reset)
- `F4`: Toggle event audio mute
- Menu presets: `LostBreadcrumbs/Audio/Apply Preset/...` (Balanced/Intense Combat/Chill Exploration)
- Dummy loop toggle: `LostBreadcrumbs/Audio/Dummy Loops/Force Disable` or `Allow Fallback`
- `1/2/3/4`: Select run loadout (Balanced/Pathfinder/EchoSpecialist/ShadowRunner)
- `F8`: Unlock loadout selection (debug)
- Loadout applies runtime modifiers to movement, vision, pulse, decoy, and smoke
- Menu loadout presets: `LostBreadcrumbs/Gameplay/Loadout/Select/...` and `Unlock Selection`
- `F6`: Cycle map tuning preset (`Compact -> Standard -> Expansive`)
- `F7`: Regenerate current stage with active preset
- Menu map presets: `LostBreadcrumbs/Gameplay/Map Preset/...`
- Regression menu: `LostBreadcrumbs/Gameplay/Regression/Run Runtime Checklist` (Play Mode only)
- Overlay map panel now shows Map Kind C/R/F/H/Risk distribution for density tuning
- Overlay now shows camera clamp/fog adaptive metrics (Camera Bounds, Fog Reveal Radius, Fog Texture)
- Overlay now shows stage pressure metrics (Pressure Total, Enemy Pressure Multipliers, Cooldown Economy P/D/S)
- Overlay now shows regression checklist state/result (PASS/FAIL/Running)
- MapSystem now auto-builds boundary wall segments in TilemapRoot/Walls (with collider)
- MapSystem now auto-builds room archetype occluders in TilemapRoot/Occluders (Cover_* hierarchy + collider)
- MapSystem now auto-builds room interaction hooks in TilemapRoot/Archetypes (Hook_* hierarchy + proximity trigger)
- MapSystem now adds `HookTensionProbe_Stage_*` dummy under generated hooks for inspector validation
- Overlay now shows hook diagnostics (Map Hooks, Hook Tuning Mult C/L/R/CD, Hooks Inside/Triggered/Warning)
- Overlay now shows threat readability diagnostics (Near/Stage/Final pressure, preset bias, camera base size)
- Overlay now shows camera/fog runtime multipliers (Camera LA/Sm/LAS, Fog R/S/F/Refog)
- Camera follow is now clamped to generated world bounds (prevents showing empty outside space)
- Fog now adaptively tunes reveal radius/softness and texture resolution by map world size
- Enemy hearing now applies wall occlusion by noise type (Footstep/Echo/Item/Decoy)
- Entering archetype hook zones emits contextual `ItemUse` noise, increasing investigate pressure around cover routes
- StagePressureDirector now scales enemy spawn pressure (count/risk/seeker/near-start) and ability cooldown economy by stage + telemetry
- ThreatReadabilityDirector now scales camera/fog/enemy-perception by nearby threat + stage pressure + map preset bias
- RegressionChecklistRunner now provides one-key runtime sanity checks (F11) for map/pressure/death-reset regressions
- Center debug floor cells are optional now: `MapSystem.createFloorSpriteRenderer` (default OFF)
- Loadout data source: `Assets/_Project/ScriptableObjects/Balance/SO_RunLoadoutCatalog.asset`

Core gameplay loop (current build):
- Stage pressure escalates over run progression and behavior score (more enemies/seeker bias, tighter ability cooldown economy)
- Collect `Breadcrumb_*` pickups
- Exit portal unlocks when all breadcrumbs are collected
- Enter unlocked exit to generate next stage automatically
- Enemies are auto-spawned from generated map cells each stage
- Enemy contact deals damage; HP reaches zero -> current stage regenerates (death reset applies)
- Player can use `Echo Pulse` as an emergency tool with risk/reward
- Sprint drains stamina and recovers over time (exhausted state briefly disables sprint)
- Collect `StaminaPickup_*` to recover stamina instantly (small pickup noise risk)
- Stamina pickups use weighted/rare spawn rules by stage and map cell kind (not guaranteed every floor)
- Player can deploy `Decoy` to redirect enemy investigation routes (except reduced effect on Seeker)
- Player can deploy `Smoke Screen` to cut line of sight and disengage chase
- While inside Smoke Screen: emitted noise is dampened (footstep/echo/active item noise)
- Enemy profiles now have different `Item Noise Response` sensitivity for pickup/item-use noises
- Player behavior telemetry now drives enemy learning phase (`Early/Mid/Late`) with adaptive search/chase pressure
- Threat alert color/state changes in real-time based on enemy state/suspicion/distance (`?�정/주의/경계/?�험/추격 �?)
- Run save now stores checkpoint stage/player state and meta progress (runs/best stage/deaths/breadcrumb totals)
- Save data now also stores selected loadout + unlocked loadout IDs and restores them on load/start
- Hideout cells now spawn `SafeHaven_*` zones that conceal player from enemy sensing
- New `Seeker` profile can partially detect concealed players (counter enemy type)
- `Seeker` profile has very low decoy response, so decoy lure works less against it
- `Seeker` partially penetrates smoke vision, so smoke disengage is less effective against Seeker
- While inside Safe Haven: periodic HP recovery + reduced emitted noise
- On death: flashlight OFF, fog reset, pulse cooldown reset, decoy reset, smoke reset, sprint state+stamina reset, short respawn invulnerability
- New run loadout system supports distinct stealth styles with risk/reward stat tradeoffs

Sequential map generation:
- Select `Scene_Root/GameRoot/Systems/MapSystem`
- Context menu `Generate Current Stage`
- Context menu `Generate Next Stage` (stage length/pressure increases)
- Room/Hideout/Fork/Corridor cells now expand into multi-cell footprints for larger playable spaces
- Player dummy auto-spawns on generated Start cell

Fog of war:
- `TilemapRoot/FogMask` has `FogOfWarSystem`
- Circle reveal follows player
- Flashlight cone opens additional reveal area
- Fog bounds auto-fit generated map size

Map tuning notes:
- `SO_SequentialMapConfig` now includes Spatial Expansion settings (room/hideout/fork/corridor expansion chance/radius)
- Increase `maxTotalExpansionCells` or `expansionMaxRadius` for larger spaces; reduce `corridorExpansionChance` for tighter routes

Loadout authoring notes:
- Setup menu: `LostBreadcrumbs/Setup/Create Default Run Loadout Catalog`
- Catalog asset drives per-loadout tuning/unlock-default flags (foundation for meta unlock/save)

Audio hierarchy notes:
- `Scene_Root/GameRoot/Managers/AudioCombatDuckingDirector` computes threat-based ducking intensity
- `Scene_Root/GameRoot/Managers/AudioDummyLoopRuntime` auto-generates/plays dummy loops for BGM/Ambience fallback
- `Scene_Root/GameRoot/Runtime/AudioEmitters/BGM_Dummy` and `Ambience_Dummy` are auto-created for immediate verification

Authoring test helpers:
- Select `AuthoringRoot/TestMarkers/NoiseButton`
- Run context action `Emit Test Noise`































