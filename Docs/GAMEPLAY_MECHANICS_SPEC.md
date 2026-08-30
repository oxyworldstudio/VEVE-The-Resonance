# VEVE: The Resonance — Detailed Gameplay Mechanics Specification
**Tranche B — Core Loops, Progression Systems & Interaction Rules**

Anchors: every rule below references a real API already implemented in
`VEVE.Runtime` (ballistics, physiology, stamina, movement, weapon physics,
customization, operators, legacy, gear, agents, campaign, save) or declares it.

---

## 1. Game Pillars → Systems Map

| Pillar | Runtime owner(s) |
|---|---|
| Hyper-realistic lethality | `Ballistics` / `TerminalBallistics`, `Damageable`, `GearLoadout` (NIJ), `Physiology` |
| Human-factor movement | `PlayerController`, `MovementSimulation`, `CharacterMassModel`, `StaminaSystem`, `GroundContactProbe` |
| Persistent identity & stakes | `CampaignState` (permadeath modes), `OperatorProfile/Traits`, `OperatorLegacySystem`, `PersonalizationStateStore` |
| Tactical depth over twitch | `TacticalLayoutEvaluator`, `SquadManager`/`CoordinationProtocol`, `AdvancedSoundPropagation`, `AudioOcclusion` |
| Authentic gunsmithing | `IconicWeaponCatalog` (18 weapons), `AttachmentCompatibilityMatrix`, `ZeroingSystem`/`RangeCard`, `WeaponCustomizationManager`, `WeaponInstanceIdentity` |

### 1.1 Core Loop (Macro) — 30–90 min per session

```
CAMPAIGN BRIEF (intel + faction state + gear/munitions stock)
   → PERSONALIZATION WORKSPACE (5 tabs: Operator / Weapon build / Gear /
     Finishes / Zeroing; constraints enforced per brief)
   → INSERT (rotary/walk-in; last 200m silent — see §4 movement)
   → ENGAGE  (encounter loops §2; each contact is information)
   → DEGRADE (ammo, magazine reserves, plate hits, stamina, comms noise)
   → DECIDE (continue / redirect / abort — extraction costs intel)
   → EXTRACT (cost paid: time, heat, ammo expenditure)
   → ASSESS (scoring: lethals precision, collateral, crew, intel)
   → INVEST (progression spend, legacy for KIA, gear restock, armory)
   → CAMPAIGN STATE mutated (terrain control, enemy alert posture)
   → back to BRIEF.
```

### 1.2 Micro Loop (in-contact, 3–30 s)

1. **Cue** — noise or motion event from AI audio/vision systems; player HUD only shows what the operator could physically perceive (diegetics §7).
2. **Localize** — sound-direction arc + crew callout (`AgentBridge` BehaviorOp `Callout`); AI does the same (`HearingSystem`-style).
3. **Decide** — engage / move-to-cover / suppress / flank. Every option has a cost table (§2.3).
4. **Act** — aim stability from physiology (`Physiology.AimStabilityFactor`) × traits × optic handling multipliers × recoil dynamics. 
5. **Resolve & read** — ballistic terminal result (penetration/ricochet/stop) + audio (supersonic crack, impact report) → the world state mutates and is *read again* in the next cycle.

### 1.3 Session-length meta loop

`Intel points` (earned) spend on: extra magazines (+4 mags), better plate tier (III→IV is the single biggest lethality change), optic unlock, one extra operator roster slot, mission route insertion choice. **Money never buys skill — gear buys options.**

---

## 2. Encounter & Combat Rules (specific numbers)

### 2.1 Engagement bands (per mission biome from `BiomeProfiles`)

| Band | Meters | Weapons that shine | Rules |
|---|---|---|---|
| Close | 0–25 | MP7, P90, M870, M1911 | Ricochet risk high (`CalculateRicochet` thresholds); overpressure indoors (AI detection −20% but player hearing +40% disorient 0.6s) |
| Mid | 25–100 | Carbine (5.56), AK (5.45/7.62×39) | Cover degrades at ≥4 hits from carbines (Destructible integrity −0.12/hit) |
| Distant | 100–400 | SCAR-H (7.62 NATO), M110, Mk14 | First-round hit needs zeroed optic; `RangeCard` holdover live |
| Extreme | 400–1200 | M82A1, bolt snipers | `ZeroingSystem.ComputeHoldoverMoa` applies; wind drift from `AdvancedBallistics` |

### 2.2 Detection math (both ways)

- **AI sees player**: `PerceptionCycle` view cone (120°/50m default, LOD-scaled) with modifiers:
  prone ×0.6 detect, crouch ×0.75, sprint ×1.25, optic scope glint (sun elevation>35° AND scope>4× AND visible cone: +15% per 4×) [W3].
- **Player sees AI**: no HUD assistance; enemy visibility governed by same cones + environment contrast via `WeatherSystem` (rain −18% detection, fog hard-clamps at fog visibility).
- **Time to acquire (TTA)**: AI 0.8–1.5 s scaled by `WeaponProficiencySystem.SkillFromXp` at spawn (default Novice/Competent): TTA = 1.5 − 0.7×skill.
- **Reaction time**: fixed per `AgentRole` in `CoordinationProtocol` (leader 0.45 s, standard 0.65, support 0.75, marksman 0.9 s for scope work).

### 2.3 Decision costs in-contact (ties to agent plan quality)

```
Engage (shoot)     : reveals position (TacticalSound), ammo wear/fouling,
                     +accuracy vs static targets, −vs moving cover.
Move-to-cover      : costs 0.6–1.1 s per hop; `CharacterController.Move`
                     path + `GroundContactProbe`; exposed during transit.
Suppress           : burst ≥8 rounds within 30° of target's sightline;
                     AI `SuppressiveFire` — target head-dips, LOS −70%.
Flank              : 2-man teams via `SquadManager.FlankBehavior`;
                     requires 1 team suppressing (mutual dependency).
Retreat / bounding : `CombatStanceRules` + `Retreat` op; breaks LOS chain.
Grenade            : `Destructible` splash + `AdvancedSoundPropagation`
                     (bloom on hearing); AI hears pin-pull noise (loudness 8
                     at 3m) — they react within TTA by scattering or rushing.
```

### 2.4 Damage pipeline (every hit, deterministic and testable)

1. `Ballistics.ResolveImpact(energy, material, thickness)` → residual energy after cover/armor.
2. Armor: `GearLoadout.GetDamageMultiplierFor(zone)` from equipped plates (coverage & NIJ level vs threat type; NIJ IIIA vs M855 AP — stops? no, trauma-only).
3. Terminal: `ProjectileBallistics.ResolveTerminalBallistics(residual, mass, caliber, type)` → wound cavity.
4. `Damageable.ApplyDamage(energy × damageMult, zone)` → wound, bleedingRate, bone fracture probability (`CalculateBoneDamage`).
5. **Physiology tick**: blood loss (`Physiology`, liters), consciousness (`UnconsciousnessThreshold` from RealismConfig 0.3×), shock index — player feels it before UI labels it.
6. Death → §3 consequences.

---

## 3. Progression Systems (multi-layer, persistent)

### 3.1 XP & Rank (account level — `ProgressionManager`)

- XP curve: `ProgressionCalculator.CalculateXPForLevel` (current exponential) — keep, but clamp per-mission award: XP from engagement accuracy (`kda`, `missionAccuracy`), time, stealth bonus, intel objectives.
- Rank (1–40) = account breadth: unlocks slots: optic tier (Rank×), gear tier (NIJ), roster slot, mission difficulty multiplier.

### 3.2 Operator proficiency (per weapon family — `WeaponProficiencySystem`)

Family = platform line: `M4/M16`, `AK`, `SCAR`, `HK`, `Sniper`, `SMG`, `Lmg`, `Shotgun`, `Pistol`.
- XP from shots/missions → `AddXpToFamily`. `SkillFromXp(1000 XP ≈ level 50)` (curve in place: 50 skill → ~500 XP; 100 → ~2000 XP diminishing).
- Effects (verified monotonic in tests): recoil mult `0.58`@100 skill vs `1.0`@0; spread similar; plus reload speed + up 35%, ADS settle −25% above 75 (to wire §3.2b).
- **Trait unlock gates** (`UnlockableTraits(level)`): traits are earned narrative + mechanical identity (14 traits implemented) → tied to operator level, not account level.

### 3.2b Operator-level mastery (session identity)

Each `OperatorProfile` has `serviceYears`, default skill (specialty-derived `DefaultProficiencySkill`), and legacy bonuses (`ComputeLegacyBonus`: KIA mentors seed successor XP 0–1200 + one free trait slot above 365 days). Mentorship: skill floor = mentor skill × 0.4 for matching specialty weapon families.

### 3.3 Loadout unlocks (the progression spine)

| Node | Requirement | Unlocks |
|---|---|---|
| Optics tier 1 | none | red dot |
| Optics tier 2 | rank 8 | LPVO 1-6× |
| Optics tier 3 | rank 16 + scope family skill 50 | HSMR 5-25× |
| Armor plate IIIA | rank 1 | standard |
| Plate III | rank 10 + intel 2 | stops 7.62×39, fails M855 AP (visible in damage log) |
| Plate IV | rank 18, 1 KIA legacy | stops AP multi-hit (2) |
| Suppressor | family skill 30 | stealth +10% detect penalty |
| Heavy round swap | family skill 60 | 7.62×39 in AK; recoil +90% → recoil feel difference |
| 12th roster slot | 3 KIA legacies | succession pipeline |

### 3.4 Campaign/world state (from CampaignSystem + BiomeProfiles)

- 2–3 routes per region; mission success lowers enemy posture there, failure raises it and **spills** (next mission inserts into higher-alert biome = more patrols, `PatrolSystem` density).
- `FactionControlState` per `EnvironmentContextProfile`: contested / friendly / hostile determines ambient civilians (CivilianAgent) & intel-gathering difficulty (dialogue success chance via `SpeechRecognition`-style checks).
- KIA operators → memorial + legacy; roster of dead operators *visible* in UI roster screen. That is emotional progression.

---

## 4. Movement & Interaction Rules (the tactile layer)

### 4.1 Stamina economy (already wired in PlayerController/MovementSimulation)

- Walk: regen; crouch walk: regen ×0.55; sprint: drain via `UseStamina`; combat sprinting (firing while moving) −60 % accuracy & +100% noise: **sprint = repositioning, not attack**.
- Load effect via `MovementSimulation` `TotalMassKg` from `Inventory` + `CharacterMassModel`: > 28 kg → sprint max speed −18%, jump disabled — player must *choose* plates vs mobility (real tradeoff that gear customizes — §1.3 options loop).

### 4.2 Stances (no crouch-jumping, no sliding — tactical realism)

Prone = +50% stability, no weapon raise under 8 m/s. Crouch transitions 0.35 s. Stuck to ground via `GroundedStickVelocity` sentinel from gravity fix.

### 4.3 Interaction rule table (door/cover/supplies)

| Object | Interaction | Rules |
|---|---|---|
| Locked door | kick / tool / breaching charge | Kick: stamina −12, sound loudness 45 (AI `Investigating`); tools 6s quiet; C4 0.4s + spall |
| Cover | destructible | `Destructible` integrity per hit; cover becomes *ammo-depleting resource*; player learns "don't trust the pillar" |
| Weapon | malfunction FSM | fouling+wear ≥ threshold → `Malfunctioned`; `R` clears or `FieldMedic`-style gunsmith repair at camp; suppressors increase fouling rate +35% |
| Medkit | self/other `MedicalTreatment` | 12–60 s depending on wound type (tourniquet quick; stitching slow); interrupted if damaged |
| Optics | boresight | `ZeroingSystem` battle-zero wizard via PersonalizationWorkspace; `RangeCard` persists on weapon via `WeaponInstanceIdentity.zeroClicks*` |
| Magazines | partial reload | tactical (keep loaded mag in inventory, −0.5 skill) vs full reload (3s+) |

---

## 5. Death = economy, not failure (permadeath modes in CampaignSystem)

| Mode | Operator | Campaign | Save |
|---|---|---|---|
| `Test` | instant respawn | no state change | no writes |
| `Assisted` | wounded-return (−20% skill 2 missions) | progress | save |
| `Realistic` | KIA → `OperatorLegacySystem.CommissionSuccessor` | mission re-run with *new* operator identity | save |
| `Immersive` | like Realistic, no HUD | + one-life account mode | save only on extract |

The **successor inherits mentorship** (§3.2b) — so KIA has a visible gameplay consequence *and* narrative weight.

---

## 6. AI Behavior Rules (complements Agentic layer)

### 6.1 Senses (perception LOD from AgentLOD)

All `PerceptionCycle` raycasts gated by tier: T0 every 0.1 s, T3 = statistical estimate only (group-level threat model). Hearing via **spatial audio cues already computed** by `AdvancedSoundPropagation`: any `TacticalSound.NoiseProduced` event inside AI cone of hearing → `lastKnownEnemyPosition`.

### 6.2 Decision (HTN-lite) plan types (already implemented in `LocalHeuristicCognition`)

1. critical health < 25% → Retreat+Callout
2. target + LOS → FireAt (+Flank support if team&distance>50m)
3. no target, known pos → Investigate (+TakeCover)
4. else → Idle/Patrol.

### 6.3 Coordination protocol (SquadManager)

- Leader issues `AssignTeamTasks` + formation (`TeamFormation`); members follow positions ±2 m; on KIA leader → succession already coded.
- **Bounding Overwatch** = `CoordinationProtocol` (Fire-Maneuver) alternating with `Suppress` steps; suppression math = noise-per-squad + cover degradation.

### 6.4 Emergent flavor rules (cheap, high ROI)

- Morale: squad w/ >40% KIA flees (state `Retreating`, `SquadManager`), leaving wounded (interactable by player — moral dilemmas).
- Radio discipline: AI calls out player hits only when in comms range (CommunicationSystem already exists).

---

## 7. HUD / Information Diegesis (UI/UX rules)

**Rule: no info the operator doesn't have physically.**

| Source | Diegetic | HUD |
|---|---|---|
| Ammo | last-round click, visual mag-check (weapon anim state) | none (or minimal) |
| Health | pulse rate audio (from `HeartRate`, via voice `RadioSystem`/HearingSystem), limping anim, screen desaturating | vitals bar exists (AdvancedHUDLayout Vitals) but *off in Immersive* |
| Target | only when optic picture & magnification justify | damage-indicator only (no aim assist, no reticle lock) |
| Squad | voice lines (VoiceKitLibrary barks, stress-tier pitch) | squad pips when within 35 m comms radius |
| Compass | map & compass available; no enemy radar ever | compass strip via AdvancedHUDLayout.Compass |
| Zero | `RangeCard` in personalization UI = pre-mission math; in-mission hold-over = eyeball |

---

## 8. Difficulty & Realism Presets — concrete multipliers

| Parameter | Test | Assisted | Realistic | Immersive |
|---|---|---|---|---|
| AI accuracy skill | ×0.5 | ×0.55 | ×1.0 | ×1.2 |
| AI TTA | ×2 | ×1.7 | ×1.0 | ×0.85 |
| Player damage taken | ×0.7 | ×0.8 | ×1.0 | ×1.0 (+no HUD) |
| Bleedout time | ∞ | ×3 | ×1 | ×1 |
| Stamina regen | ×4 | ×2 | ×1 | ×0.9 |
| Ammo/gear stock | ∞ | +30% | baseline | baseline |
| HUD | full | full | minimal (compass+vitals only) | none |

---

## 9. Mission scoring & rewards

Score = f( lethality-precision = hits/shots (Ballistics data per shot), collateral (civilian damage → intel −1 per event), intel acquired (mission events), time-under-combat efficiency, extraction style).
Intel points = currency (see 1.3). Score tiers: Ghost / Operator / Grunt narrative epilogue variants (dialogue fragments via `DialogueSystem`).

---

## 10. Implementation Backlog (sequenced for the remaining tranches)

1. **B1** (tranche in-flight, pending QA gate): Gear, Optics/Zeroing, Operators, Personalization UI → integration.
2. **B2 — Player feel patch**: `WeaponProficiencySystem` → PlayerController sway/recoil mult; optic handling multipliers into ADS; `RangeCard` hold-over applied to aim ray (W3 seam doc'd).
3. **B3 — Campaign glue**: KIA → legacy hook (`CampaignSystem` calls `OperatorLegacySystem.CommissionSuccessor`); successor loads with mentorship floor; PersonalizationWorkspace bind to `OperatorProfile.CreateDefaultRoster()`.
4. **B4 — Encounter director**: intel points affect next mission's `TacticalLayoutEvaluator` density + alert; morale flee rule in squad manager.
5. **B5 — Door/breach + partial-reload FSM** (cheap, huge tactile ROI; uses existing Destructible/Inventory).
6. **B6 — HUD diegesis toggle** per realism preset (wire UIManager settings).
7. **B7 — Mission scoring + rewards** (mission stats already tracked in CombatState — aggregate + award XP/intel).
8. **B8 — Content**: 2nd biome mission set, 3rd difficulty track, optic library expansion (SFP/FFP already modeled).

Each B-item = own tranche of agents + orchestrator QA gate (Unity compile + full EditMode + scene regen + WebGL), same discipline as A-tranche.

---

### W-numbers legend
W3=scope-glint rule (to be added) — other W rules: [W1] plate multi-hit already in Gear, [W2] malfunction FSM in `Weapon` already.
