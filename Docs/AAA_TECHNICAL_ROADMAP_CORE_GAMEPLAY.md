# VEVE: THE RESONANCE — Technical Roadmap: Concepts → Core Gameplay
**AAA ultra-realism program - architecture, systems, budgets, gates**
*Companion to `Docs/VEVE_STAKEHOLDER_GDD_PITCH.md`, `Docs/GEAMEPLAY_MECHANICS_SPEC.md`, Docs/PROJECT_STATUS.md, Docs/AUTOMATED_QA_SYSTEM.md (`2877ebf`, 411 green tests).*

---

## 0. Where we are (measured, not promised)

| Area | In-tree today |
|---|---|
| Ballistics | 6-DOF external + chamber/internal model + NIJ/VPAM armor multi-hit, ricochet, optics recompute zeroing (published bore geometry), grenade blast through same mitigation chain |
| Human factors | TTA acquisition curves, stamina, physiology (blood/HR/treatments), radio discipline model, diegetic reticle/optics |
| Simulation determinism | fixed-point integer terrain lattice byte-identical cross-platform, journal + mirror reorder, telemetry + reconciliation, offline = host = client path |
| AI | HTN-lite + behavior trees + squad coordination protocols + posture/escalation |
| Content pipeline | code catalog ↔ designer Resources assets (15 ops / 5 biomes, 53 gear/weapon/optic assets, payload codec) |
| QA system | in-engine multi-agent code review (3 specialist agents + orchestrator gate) + 411 EditMode |

**Interpretation:** the *simulation contract* exists. This roadmap takes it from vertical slice to a shipped-quality core gameplay architecture for AAA fidelity, naming exactly how each system is implemented (ECS/Burst/HDRP) so that "more realistic than Arma/Escape series" is a measurable engineering property, not a mood.

---

## 1. Fidelity targets (each is measurable, gates own them)

| Metric | Industry reference | VEVE target | How it is measured |
|---|---|---|---|
| Bullet model | Tarkov / SQ realism band | every round: G1/G7 aero drag (Mach curve table) + yaw/cp growth + bar erosion + muzzle device pressure curve tables | `BallisticValidationTests` against published manufacturer / proof-gun data; error budget ±3% velocity, ±0.3 MOA on paper |
| Armor behavior | real NIJ/VPAM V50/V0 | multi-hit per plate panel, obliquity curve, backface deformation, spall cone; stops depend on round class not "armor points" | already modeled in `GearProtectionStandard`; add SPALL event + trauma wound types (gate on new rules) |
| Destruction | Arma Reforcer terrain, not baked | per-material yield tables + fracture graph (FEM-static, runtime-breakable), debris physical bodies with mass-surface + velocity impact damage | new `DestructionKernel` (see 4.3) |
| Material look | photoreal | spectral-accurate PBR + microfacet aniso (cloth/metal hair), wetness dynamic, procedural detail normals; material physics values tied to render response (friction/IR/gloss) | `SurfaceArtRules` + HDRP Shader Graphs, measured vs. real reflectance data |
| Light | RT | RTPTI (hardware RT or baked radiance-probe grid with displacement GI), spectral sun 2° disk, night with star + moon sky physically | HDRP RT; on low tier: baked + SSAO + SSIL; WebGL path URP |
| AI | Squad + comms realism | GDC (goal-directed coordination) with explicit negotiation + morale states, sensor bands, radio protocol; no omniscience (already enforced) | `TacticalValidation` + headless sim 10k ops |
| Determinism | netcode standard | every authoritative sim tick is pure + journal-reproducible; physics of projectiles/gear/humans run server-authoritative already | Interpolation buffer + journal replay fuzz (new) |
| Frame budget | AAA 60 | 1080p60 mid-GPU, console 4K@60 (120 mode), render scale + AI LODs | Unity profiler CI gates |

---

## 2. Core Architecture (3 pillars; everything attaches to these)

### 2.1 Simulation-first split ("SimCore")
All gameplay state lives in **pure C# jobs-friendly structs** (`VeveSim`), zero `MonoBehaviour`, zero `UnityEngine.Object` references. Unity becomes:
- **presentation layer** (MonoBehaviours, HDRP/URP renderers, HUD, reticle, audio listeners),
- **io adapter** (input → SimCommands, journal commands),
- **host/transport** (NGO RPCs or loopback).

The existing deterministic loop (journal → `InterpolationBuffer` + `PredictionReconciler`) generalizes: **every sim module is a reducer.**
```
SimWorld { entities: NativeArray<SimEntity>, systems[], journalInbox }
systems: BallisticsSystem, ArmorMitigationSystem, HumanFactorsSystem,
         PerceptionSystem (sensor bands), DecisionSystem (HTN/utility),
         CoordinationSystem (callouts/negotiation), SquadMoveSystem
tick(dt): fixed 120Hz sim, 60Hz logic, presentation at screen rate
```
Rationale: AAA realism fails when physics is bolted onto animation; physics/medical/logistics being the model makes every visual an honest projection. Tests already match (`PredictionReconciler`, `InterpolationBuffer`, gear, ballistics).

### 2.2 Data as assets ("SimData")
Numeric truth from published tables in a binary, versioned asset format (we already do payload codec assets for designers). SimData packs:
- aero/BC tables + powder/pressure curves, ammo specs, armor/plate specs (NIJ), anatomy wound tables, material acoustic/reflectance data, prop/geometry sets, biome weather.
Versioned hash in the journal + sim world seed — **sim content is auditable and replayable** (and moddable later).

### 2.3 GPU-heavy is optional and tierable
Tier A: HDRP + hardware ray tracing (PC/PS5/XSX); Tier B: URP baked GI probes + SSR + procedural normals (low-end PC, WebGL vertical demo); all tiers share SimCore, so a WebGL demo has the physics of a console.

---

## 3. Engine topology — what to change in Unity now

```
VEVE/
  Sim/                     (ECS/DOTS when migrated, plain System first)
    Sim/Entities/*.cs      data: Bullet, ActorState, AnatomyState, SquadState, PerceptionBand
    Sim/Systems/*.cs        fixed-step pure, Burst
  Content/              (already: catalogs + payload + asset sources - extend SimData)
  Presentation/         (MonoBehaviours: renderers, HUD, reticle, radio panel, avatar binding)
  Net/                  (already: journal/reconciler/interp, NGO adapter - expand to transport-agnostic)
  CodeReview/           (orchestrator + specialist agents - new: SimDataRule, DeterminismRule)
  Runtime legacy        → migrate/unwrap (PlayerController is now SimHuman + a thin MonoDriver)
```
**Migration strategy (strangler, no big-bang):** the existing MonoBehaviour sim pieces (Physiology, CombatState, MovementSimulation, GrenadeProjectile manual integrate, EnemyAwareness TTA) become *reference implementations*; each is ported into a Sim system and kept running **in parallel** on identical inputs until output is bit-equal (new test `ParityHarness`), only then swapped — this keeps the 411 tests as the safety net and is how AAA rewrites are paid for.

---

## 4. System-by-system technical plan
### 4.1 Advanced physics (Sim/Systems - new + promoted)
| Feature | Implementation | Fidelity |
|---|---|---|
| Internal ballistics | powder burn table per ammo (Webeler approximation per grain lot); pressure curve + muzzle device backpressure multipliers; velocity → BC → windage | proof-factory data |
| Bullet flight | already 6D: drag via Cd(Mach) G7 tables, Magnus (spin drift), yaw of repose growth; add transonic tumble energy loss model | measured |
| Ricochet | angle of incidence vs material: plastic vs elastic threshold curve, energy-retained + deflected yaw + debris | paper/plate data |
| Penetration/armor | existing NIJ/VPAM + trauma: add **spall cone** behind plate (velocity + coverage → secondary wound), glancing deflection | wound ballistics |
| Fragment/heat | SPH-lite fluid for splash + smoke plumes (compute shader, tier A only, 2ms budget on sim thread) | look |
| Vehicles | tire friction ellipse (Pacejka coefficients per tire class), weight transfer with load cargo mass | driving feel |
| Debris | see 4.3 | |
Structure:
```csharp
[BurstCompile] partial struct BallisticsSystem : ISystem  // entities, tables from SimData
// legacy Ballistics/ZeroingSystem stay as managed reference impls; ParityHarness gates migration
```
### 4.2 Complex human model (Physiology → anatomy Sim system)
- multi-layer per zone: skin / fat / muscle / bone / organ; hit energy → wound channel; blood loss (arterial vs venous fraction), pneumothorax mechanics, shock index; already present as scalars: promote to struct; treatments consume sim-time with medic skill curves (SkillFromXp already).
- consciousness: SpO2/pressure thresholds (present) + visual/auditory narrowing (presentation effect, sim state `PerfusionQuality`).
- **Rule:** never a health bar. UI never sees HP; only derived sim state (this is the anti-Tarkov-clones differentiator).
### 4.3 Destructible high-fidelity environment interaction
- author: structural graph (nodes = load-bearing, joints material yield) per building module (content pack);
- runtime: hit accumulation (ballistics) + blast (grenade energy sphere falloff already) + breaching charge; graph solver re-evaluates load paths on break; collapse → debris cluster (fixed cap per cell for budget, pooled, physical);
- **debris damage:** mass × velocity² vs material coverage via 4.1 ricochet/frag tables; doors/walls existing systems are the seed; prop scatter grounding uses terrain FNV (existing).
- persistent world state: journal commands already deterministic → destruction is **part of the mission session state** (netcode + SP both true).
### 4.4 Photoreal rendering pipeline (Presentation + HDRP)
- HDRP (tier A) branch as decided in RENDERPIPELINE_MIGRATION playbook (PipelineCompat already); material response **from SimData**: friction/temperature/IR/wetness → albedo/roughness/normal detail; SurfaceStyleDriver (existing) becomes the thin writer of the same values shaders read.
- geometry: GPU-driven cluster (HDRP) for debris/props; Nanite-equivalent not on Unity — mitigation: virtualized normal/mask material layers + impostor pipeline (existing LODController) + HLOD sub-scenes for console;
- lighting: spectral physically-based atmosphere (we have astronomical sun/moon model), star field, cloud volumetrics with sun scattering; night: star/moon irradiance + NVG (white phosphor emulation physically from luminance + noise model),
- post: ACES, lens model (we already have real optic data - FOV, eye relief, aberration) → **reticle is optically accurate** (C1/C3: existing), scope bloom/flares from aperture;
- budget: 4ms GPU post + 3ms ray (tier A), SSAO/SSIL tier B.
### 4.5 Complex AI behaviors (Sim systems)
- perception sensors, multi-band, per-agent: vision (cone with material reflectance/FOV/occlusion + **scope glint W3** already), hearing (audio bands + noise propagation + existing occlusion model with absorption/trauma), vibration/scent minor; memory = belief set, not truth (present);
- decision: per-agent HTN (existing `AgentMind`) with **utility** scoring as tiebreak + **CoordinationSystem**: explicit negotiation tokens over the journal (existing comms model): bounding (cover alternation), flank, suppress (suppressing = perception-blocking + morale events (existing)), casualty evacuation (existing legacy system + morale FSM);
- morale/radio discipline (SquadMorale) drives callout frequency (cadence rules present) which in turn drives hearing by enemies — closed loop;
- headless mode (`sim only`) runs in CI: `TacticalValidation` fuzz: N squads × M ops, verify no state blow-up / monotonic behavior (new test target);
- anti-cheat: sim is deterministic server-side (journal authority already); AI never trusts a client-reported position (client only reports **own** shots).
### 4.6 Audio as a sim system
Sound propagation is currently a sim model (occlusion/absorption/trauma), promote to SimData: HRTF spatialization + reverb (existing zones) driven off same geometry; diegetic radio (VoiceKit) is *presentation over journal events* (existing) — enemies hear via same bands → parity across audio and sim.

---

## 5. Implementation plan (phases w/ entry/exit gates)

| Phase | Duration | Scope | Entry | Exit |
|---|---|---|---|---|
| **F0 (now)** | 1 sprint | SimData codec v1 (binary pack + version hash), ParityHarness (old→new output equality), CI jobs | 411 green | ParityHarness ≥97% parity for ballistics+armor |
| **F1 Physics core** | 2-3 sprints | BallisticsSystem/Spall/Tumble in Sim; internal pressure table; Grenade blast → world graph; debris pool | F0 | physics sim at fixed 120Hz <6ms for 256 bullets; every migration passes parity |
| **F2 humans** | 2 sprints | anatomy wound map, treatments sim-time, medic proficiency link; consciousness effect wiring | F1 | clinical review notes vs real trauma tables; test suite covers wound math |
| **F3 destruction** | 3 sprints | structural graph solver + collapse chunks + world state in journal; doors/glass/vegetation | F2 | destruction deterministic and replayable (netcode path) |
| **F4 render photoreal** | parallel with F3 | HDRP branch (tier A), Shader Graphs per SimData, NVG/optic optics, volumetrics | F1 | frame/perf budget; URP/WebGL path intact |
| **F5 AI coordination** | 2 sprints | CoordinationSystem + GDC tokens over journal, perception bands, headless CI fuzz | F1 | 10k ops fuzz stable; human eval pass |
| **F6 integration + netcode** | 2 sprints | live-host timeline (server-authoritative sim), interpolation on remote (foundation present), lobby+reconcile telemetry on screen | F1,F5 | playtest co-op full ops; telemetry honest |
| **F7 polish + content** | 2+ | mission per biome 5+/biome (content pipeline now), audio passes, cinematics | all | vertical slice at showcase |

---

## 6. Code review system extensions (already in-tree): add three more specialist agents
- `CR-DAT-01 SimDataRule`: table asset has version hash + no magic float literal in Sim systems (data vs code separation enforcement);
- `CR-DET-01 DeterminismRule`: no `DateTime`/`Random`/`Environment.Tick` without journal seed in Sim/; static scan of `UnityEngine.Random` outside Presentation;
- `CR-PERF-01 BudgetRule`: attribute `[SimBudget]` presence on systems, CI compares measured ms against declared budget.
Orchestrator policy: **Error blocks merge** — the same way C4/C14-18 caught their bugs.

## 7. Team & pipeline
- Sim engineers 3 (physics/AI/deterministic), presentation 3 (render/audio/UI), tools 2 (content pipeline/CI/review), design 2 (data tables/ops/balance) + QA harness (automation of all 4). Deterministic sim makes the whole team's iteration reproducible (journal replay = regression tests).
- CI: `EditMode tests → review gate (RunReviewForGate) → sim headless replay fuzz → build tiers (URP-WebGL vertical, HDRP-PC)`.

## 8. Risks (and how the plan answers the current realistic shooters)
1. **Sim vs render coupling creeping back** → the strangler/ParityHarness + review agents physically block it in CI (data rule).
2. **HDRP console-only** branch cost → keep URP/WebGL vertical as a *proof* of fidelity claims, never sim-forked.
3. **Destruction cost** → structural solver runs **per event not per frame**; debris caps are engine budget, not content constraint.
4. **AI cheating accusation** → every AI belief flows through the same sensor + journal math, no privileged truth, headless replay proves it.

## 9. Acceptance — what proves "beyond" the leaders
1. A hit on armor **stops** or **reduces** by real data (never HP), and the victim's UI is a *physiology projection*, verifiable frame-by-frame.
2. A room collapses **because** of blast paths (graph), and the next session replays it identical from the journal (netcode + singleplayer one model).
3. AI retreats **because morale and comms degraded** — and its callouts can be heard and misled (audio sim), headless fuzz verified.
4. A night op: scope glint, moon irradiance, starfield, real optic FOV and eyebox — not a night-vision filter.
5. Zero regression drift: 411 → N tests, review gates run as code, every "more realistic" claim is an **assertion that fails someone's PR**.

---
*The roadmap is executable today from F0: SimData packer + ParityHarness. The current W-slice (lobby→sim→journal→reconcile→debrief) is already the shape the plan scales — this document names how far that shape goes and with which discipline.*
