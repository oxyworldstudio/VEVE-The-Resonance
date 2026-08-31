# VEVE: THE RESONANCE
## Confidential Game Design Document & Publisher Pitch — v1.0

---

# PART I — THE PITCH

## 1.0 One-Line Premise
**"Arma Reforcer meets This War of Mine's moral weight: a tactical first-person shooter where every bullet is a physics object, every wound is physiology, every squadmate you lose is gone forever — and the war remembers."**

## 1.1 Elevator Pitch (90 seconds)
The tactical shooter market rewards familiarity. Escape from Tarkov proved players will accept extreme systems depth; Arma proved they accept operational scale; PUBG and Squad proved co-op has mass appetite. None have delivered a AAA-titled, narratively-driven *campaign* where hyper-realism is not an obstacle but the story engine. VEVE: The Resonance does that.

Built on a verified, test-passed simulation core — 6-DOF external ballistics with published-spec optics that change the actual firing solution, NIJ/VPAM-certified armor models with multi-hit degradation, human-factors AI (operators who need time to acquire what they see, who radio contacts with real protocol, whose morale breaks), and permadeath with generational legacy — VEVE turns simulation fidelity into *emotion*. You don't level up a space marine. You commission the younger sibling of the operator whose body you couldn't bring home, and the enemy network that killed him is patrolling harder because of what your team did last operation.

The engineering is done: a single deterministic mission session runs identically offline, as host, and as client replay — proven by automated parity tests — and the content stack (18 iconic weapons, 13 published optics, 22+ certification-grade gear items, 10 authored operations across 5 biomes) is already designer-editable data. We are asking for the funding to turn verified systems into a full campaign's worth of authored experience at production scale.

**Genre:** PvPvE-leaning tactical FPS, single & 2–4 player co-op (competitive PvP explicitly out of scope; the netcode is co-op by design)
**Platforms:** PC (Steam) first; console (PS5/XSX) next; WebGL tech-demo exists and ships with every build
**Engine:** Unity 6 / current LTS, custom deterministic simulation layer — pipeline-ready (URP/HDRP switch documented, no code change)
**Rating target:** M / PEGI 18 (violence, no gore tourism: medical realism is sober, not exploitative)
**Comparable titles (positioning):** Escape from Tarkov (systems), Squad (co-op tactics), Ready or Not (CQB stakes), Arma (operational feel), Spec Ops: The Line (moral descent), Kingdom Come: Deliverance (consequence texture)
**Ask:** Full-production funding for 24–30 months, single-player campaign + 4P co-op vertical slice, content completion and console certification

## 1.2 The One Slide Publishers Remember
> **"Every other shooter asks: 'Can you aim?' VEVE asks: 'Can you be responsible for what your aim is attached to?'**

## 1.3 Market Position & Opportunity
| Fact | Implication |
|---|---|
| The hardcore-tactical segment grew from a $0 niche to a proven AA-to-AAA revenue band (Tarkov franchise, RoN, Squad) | Demand for "more realistic but structured" has been validated by paid user bases — but the top entries are Early Access, service-shaped, or lack a authored narrative campaign |
| No incumbent combines: authored campaign + permadeath legacy + verified physics netcode parity + console-friendly performance budget | VEVE's wedge is *authored realism*: a AAA presentation of hard systems, packaged for players who love Tarkov but are tired of its friction-as-product |
| Community systems-mining (wikis, tier-lists of ammo/armor) is a proven engagement engine in this niche | Our certified-ballistics/gear tables *are* wiki content we generated from manufacturer data — depth players already demand |
| Co-op extraction/tactical is the fastest-growing watch-streaming format after extraction RPGs | Deterministic, replay-verifiable sim is a streaming/anti-cheat asset; late-join & drop-in are architectural, not bolted |
| Early Access is no longer mandatory | Our vertical slice is the actual alpha; systems already gate-tested: pitch is content+presentation, not "please fund risk we haven't solved" |

**Target TAM**: core tactics audience (~6–10M engaged players across comparables) × crossover milsim/enthusiast. Premium pricepoint, no F2P, one premium sequel-adjacent expansion model, cosmetic-only DLC *that players already simulate building in our editor* (see §3.9, finishes).

## 1.4 USPs — The Five-Sentence Version
1. **Bullets are physics objects before they are hitscan metaphors:** published manufacturer data in, deterministic trajectory out; optics and armor change lethality *the way the real gear does*.
2. **Wounds are physiology:** calibers, backface, shock, radio-cadence medevac; your squadmate's voice on the net is the health bar.
3. **Permadeath with memory:** the roster remembers, the successor inherits the mentor's mastery, the enemy posture reflects what you did to their network.
4. **One deterministic simulation** across offline/co-op/client — QA-verifiable at the physics layer; the network code is an extension of the *game's* math, not a second game.
5. **The world itself is the content:** operations are drafts from a designed catalog; patrol densities, callout chains, and radio discipline emerge from rules players read in the manual — and trust, because the physics says so.

---

# PART II — CORE GAMEPLAY (THE 30/300/3000 FRAME)

## 2.1 Loop Pyramid
```
30s:   Observe → Orient → Decide → Act → Read the world's answer
       (a single contact: localize noise, call contact, acquire with skill-gated
        time, aim with optic-corrected holdover, read result: suppressed enemy or flank)

30min: The Operation (drafted from mission catalog)
       Brief → Insert (posture-shaped alert level) → Objectives (clear/destroy/
        recover/intel, extraction style = pacing) → Extract → Debrief on
        *both* the host screen and the client mirror (parity enforced, not themed)

300min: The Campaign
       Operations cycle the world: each success raises region alert posture and
       enemy response (armor likelihood, patrol density) → the next operation
       draft is *consequence-shaped*. KIA operators become lineage: trait slots,
       XP inheritance, skill floors, memorial entries in the roster UI.

3000:   The Player's Relationship with the Simulation
       Skill is re-mapped: the player learns to read holdover marks like a
       real shooter, armor tiers like a procurement officer, radio traffic like
       a squad leader. That vocabulary is the long-tail mastery, self-documented
       into the meta-game that the hardcore community builds wikis and
       tier-lists around.
```

## 2.2 Encounter Grammar
- **Acquisition is a skill curve:** a contact is never "painted" at the enemy; both sides *accumulate time-to-acquire* from human-factors floors. High-skill operators get faster to shoot back — that's the threat; medium-range optics give you the holdover they didn't have at zero.
- **Hearing is estimate, not cheat:** gunfire localizes as a bearing cone with a systematic *range under-estimate* — you converge where the shot felt closer, and the enemy radio is converging too. Silence is cover.
- **Armor is physics, not hit points:** NIJ/VPAM level vs round class vs angle vs prior hits. A plate stops the round; the round still transmits the trauma it has to; a third hit may not stop the same round it stopped cold first time.
- **Morale is communication:** squads that route leave their wounded — you will see it, and the next mission's posture reflects what the surviving network learned. Your own squad routes when you stop calling contacts.
- **The gunsmith is a loadout *design surface*, cosmetic only as flavor:** mounting a scope changes bore geometry → the range card recomputes → your holdover marks are that optic. It's the loop where player agency equals mechanical advantage.

## 2.3 Player Fantasy Layers
| Layer | Fantasy | Systems delivering it |
|---|---|---|
| Individual | "I am capable under fire" | TTA, movement, stamina, weapon handling fidelity |
| Specialist | "I am the gunsmith" | 30+ catalog gunsmith surfaces, published-spec optics, zeroing cards |
| Team leader | "I run an element" | Callouts, radio cadence gates, posture, bounding overwatch |
| Veteran | "My service has a lineage" | permadeath legacy, mentorship, roster memorials |
| Reader/Analyst | "I understand the machine" | everything is wiki-form truth |

## 2.4 The Emotional Arc (Campaign)
**Act I — Duty (Ops 1-4):** competence without context. You're a tool. 
**Act II — Resonance (Ops 5-8):** the war's *network* adapts to your signature (escalation engine); first named losses; the successor is someone you didn't plan to like.
**Act III — Cost (Ops 9-12):** extraction choices carry bodies or intel, not both; the roster memorial becomes the war memorial.
**Endgame:** you've reached a quiet place: the simulation's *economy of consequence* is the meaning. There are no hidden numbers.

---

# PART III — TECHNICAL SPECIFICATION (STAKEHOLDER EDITION)

## 3.1 Proven, Not Promised (verified by current codebase)
| Capability | Status |
|---|---|
| 6-DOF external ballistics: Mach-drag, Coriolis, spin drift, wind, temperature/altitude air density | Implemented, pure-function tested |
| Terminal ballistics: penetration vs material + NIJ armor w/ obliquity, multi-hit retention, BFD/trauma budgets | Implemented, cert-standard data |
| Weapon gunsmithing: per-weapon attachment matrix, rail interfaces, swap times | Implemented, designer-editable catalog |
| Optics: 13 published scopes, bore heights and click values that recompute each firing solution | Implemented, live in ballistics & reticle |
| Per-instance weapon identity: serial, wear, bar-life, zero-state, anti-tamper snapshot, finishes with IR signature | Implemented |
| Physiological sim: blood loss, HR/SpO2, shock index, treatment time gates | Implemented |
| AI: behavior trees, HTN-lite cognition, agent LOD (full→statistical), squad formations, bounding overwatch, comms | Implemented |
| Human-factors AI tuning: TTA skill curve, radio cadence, posture→patrol density | Implemented |
| Diegetic UI: reticle holdover markers, radio chatter, wrist readout, HUD diegesis by mode | Implemented |
| Deterministic mission session (draft→tally→score→XP→escalation) | Implemented |
| Co-op netcode: authoritative journal, mirror with reorder buffer, parity proven by test, per-connection pawns, host/client/scene-rig authority | Implemented |
| Campaign: mission ops catalog (biome-paired), 5 difficulty tracks, death-mode economy | Implemented |
| Tooling: scene builder, content exporter, native test-gate harness, debug dashboard (runtime + editor) | Implemented |
| **Automated quality regime:** 359 EditMode tests green, two-pass native gates per step, single deterministic source of truth | **The culture that makes AAA schedule credible** |

## 3.2 Systems Depth (selective spec extracts)
- **Ballistics:** G7/G1 drag-table interpolation per round; sight-line crossing solver for PBR/battle-zero (10cm/38cm window) with optic bore height (38mm red-dot to 57mm magnum glass); holdover-to-click turret math (MOA/MRAD), scope picture FOV drives reticle px/MOA scale. Supersonic round: crack as separate acoustic event. Suppression as an AI state, from ballistic hits.
- **Armor:** 12+ certified threat levels; angle-dependent obliquity curve; post-strike trauma & backface deformation budgets; multi-hit retention per panel with strike registration; coverage masks per 16 body zones; stopped-round trauma still injures.
- **AI:** LOD tick gating; acoustic estimate vs vision (cone, occlusion); morale FSM (confident→routed latching); callout relay converges squads on degraded positions (radio ≠ omniscience); post-engagement stress→intel scoring feeds camp escalation.
- **Netcode:** commands-as-mission-facts (single journal authority) + presentation-only relay for chatter; mirror = same simulation object; parity test is a build gate, not a feature flag. Late-join replay from zero. Console/PC transport plan documented; the simulation itself is platform-neutral deterministic math.
- **Content pipeline:** code catalogs ↔ designer Resource assets (bi-directional idempotent export, human-readable payload), mission drafts with region/posture awareness.

## 3.3 Performance & Scalability Budget
- 60fps target at 1080p+ (low-spec) through 120fps on capable hardware; AI LOD system (Full→Statistical) bounds NPC cost by camera relevance; agent ticks time-sliced.
- Deterministic sim allows headless server simulation without render thread (dedicated server path already architectural).
- Memory: object pools via existing systems; WebGL demo build exists as pipeline proof, console via render-pipeline playbook.
- Pipeline-ready: URP/HDRP assignment flips shader resolution automatically (family-aware compat layer); HDRP console branch documented; **built-in remains safe default** — every current build green today.

## 3.4 Art Direction (AAA target, present-state honest)
| Vertical | Present | Target |
|---|---|---|
| Environments | procedural/graybox multi-biome with function graph & LOS scoring | high-fidelity modular scenes from the same generator + art pass, biomes as SubScenes |
| Weapons | full catalog/attachment data layer; placeholder mesh | photogrammetry-grade weapon set, first-person rig w/grip-aware IK anchors |
| Characters | procedural reticle/roster/UI layer | mocap + motion-matching locomotion, tactical animation set, damage state visuals |
| UI | diegetic framework complete: reticle, wrist, radio, diegesis by mode | polished art layer on the existing diegetic contracts; never a crutch, never a lie |
| FX | tracer/impact/muzzle systems in; ballistic-into-material logic | high-density VFX + decal systems on URP/HDRP path |
| Audio | acoustic model, radio protocol model, procedural audio layer | licensed voice kits, adaptive mix; sound is a UI system in this game and is spec'd as such |

## 3.5 Audio Direction
Radio-first squad voice system (barks from personality kits, stress-tier pitch/rate, 4-level fallback chain), weapons/armor sounds at the material-acoustic layer, diegetic sound priority: the HUD doesn't tell you what the squadmate couldn't. The simulation is loud enough for players to wiki it.

## 3.6 Writing & Worldbuilding
**The setting (the resonance):** a mid-2030s near-future state-of-the-art regional war where a small professional force becomes the only thing that *remembers what the escalation actually was*. VEVE (Volunteer Expeditionary Veteran Element) is that force; the operation's codename — the Resonance — is a system that measures how much a battle network has learned about you. The enemy is smart *because the systems are honest*, the war remembers *because consequence is data*, the operators matter *because the simulation doesn't fake them*.

**Narrative pipeline:** ops (catalog, designed) + legacy (earned) + world posture (emergent) — authored at content level, emergent at system level, never a contradiction — writing *uses* the simulation rather than overriding it.

## 3.7 Player Progression & Meta
Rank (account) gates **options**, not math (armor plates, high zoom, roster slots). Family proficiency is **earned through play, reflected through mechanics** (faster target acquisition, not "damage +12%"). Operator traits unlock by level, legacy grants XP/skill floors — death means *forward*, never *backwards*. Intel points are the spend on mission options (route, insertion, gear restock) — the game's currency buys *more interesting choices*, never more power.

## 3.8 Accessibility & Difficulty Honesty
Four death-mode presets are difficulty *philosophies* not "easy mode" (training, wound-wounds, full permadeath, no-HUD); difficulty tracks tune AI reaction/patrol density/par budget, never hidden multipliers. Diegetic-first HUD + assistive options layer is the same one the hardcore demands and the newcomer keeps — one spec, two surfaces.

## 3.9 Community/Monetization Notes
Tier-listing culture is built-in (cert tables, published optics, gunsmith math). Finishes/camo are cosmetic DLC that our *sim already models for free* (IR signature realism) — sell the beautiful, never the advantage. Campaign expansion = new ops catalog packs (pipeline already data-driven); price as full-price premium with a single-expansion roadmap.

## 3.10 Production Plan (funded scope)
| Phase | Months | Definition |
|---|---|---|
| Pre-production / vertical slice | 0-3 | the existing systems as a polished 45-min campaign slice; art pass one; audio pass one |
| Production | 3-18 | 35–45 authored ops across 5 biomes; per-weapon gunsmith polish; netcode session stability; animation/audio full; UI art layer |
| Alpha | 18-21 | feature & content lock on simulation-critical paths, perf/console targets |
| Beta/Certification | 21-26 | console cert, localization, balance live-data pass |
| Launch + Post | 26+ | campaign expansion packs, community tier-list season pass (data, not power) |

**Team shape (core):** design 2, engineering 4 (the present sim architecture is owned by <1 person per domain), art 12, anim 3, audio 2, QA/tools 2, production 1. **Risk register:** content volume (mitigated: the pipeline ships designer-editable data; the graybox is the machine, art the skin); netcode session complexity (mitigated: determinism already at physics layer, parity gated; console transport = standard NGO path); realism-presentation friction (mitigated: diegetic UI + difficulty philosophies).

---

# APPENDIX A — Engineering Culture Proof (for publisher technical due-diligence)
- Test regime: every rule shipped as pure function first, then wired.
- Gate discipline: single native gate (full editor test-suite runs on every candidate step) — stale-cache traps caught by re-run protocol, never "assumed green".
- Honest bug reports: real defects caught at gate during this build period included an armor/ammo unit bug, a netcode reorder flaw in our own mirror, a serializer delimiter collision. The culture: fix the code, never relax the test.
- Determinism as contract: offline = host = client mirror (proved), console-transport risk reduced to packaging.

# APPENDIX B — Glossary
**TTA** time-to-acquire · **PBR** point-blank zeroing window · **journal/mirror** authoritative facts stream + client replay · **scene rig/pawn** authored-scene player vs per-connection networked pawn · **posture** next-mission enemy response state · **diegesis** the information the operator physically has.

---
*Confidential — VEVE: The Resonance — systems verified; content funded-to-scale. This document's technical claims are traceable to the current repository: every number, every standard, and every claim marked "implemented" corresponds to passing automated tests in today's build.*
