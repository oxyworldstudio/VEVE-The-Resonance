# C4 Transport + Mission Network Design (approved draft)
**VEVE: The Resonance - PvE 2-4 player cooperative campaign - host-authoritative**

## Why the journal IS the protocol
Gameplay = (MissionSession tally facts) -> (pure deterministic scoring/escalation legacy). The netcode does not "synchronize state": it **distributes facts**. Facts ordered by `seq` on the host => every mirror replays and lands on bitwise the same score breakdown that C4 proved (parity test = acceptance gate). Radio barks cross the wire as ints + rebuilt client-side from VoiceKit: diegetic, zero string payloads, zero GC.

## Session topology
- `MissionTransportAdapter` sits on the NetworkObject (auto prefab generated). Offline mode (no NetworkManager) routes straight into journal + mirror, therefore single player **is** the same software path (no divergent code branches - AAA hygiene).
- `CampaignLoopController.CommandSink` = all authoritative facts are injected once (start/alert/shots/intel/contacts/malfunctions/squad/end). Adding a new gameplay fact = one line in loop + one NetCommandType + one mirror case: the protocol never drifts.
- Host election: Unity Transport; a local-host (listen server) for 2 players; dedicated server later reuses exactly the same code (`IsHost` branch is the server side).

## Anti-cheat (already built in)
Host = only sequence assigner. A client's intent reaches the host via `EnqueueServerRpc` (RequireOwnership false only for lobby join pre-spawn) and then is **journal-rewritten** with canonical sender/frame. Malformed seq (<=applied/ duplicate -> guarded, > expected -> reorder buffered to dead-> resend path).

## Next wire-up in order (C4c, small)
1. `GameFlow`: boot -> NetworkManager StartHost for 2p, start session = first MissionCommand of `MissionStart` (host), join-in-progress via late-join replay `ApplyThrough(Entries, LastSequence)` (parity already proven in protocol tests).
2. PlayerController input ownership RPCs (ShotFired from local weapon -> journal; server-side authority remains in Weapon sim).
3. AI (EnemyAwareness/SquadManager/AgentBridge) run **on host only**, clients receive the outcome (already: ShotResolved + radio).
4. Extraction = MissionEnd(success) + EndCurrentMission on host; debrief UI opens identically on both (mirror FinalBreakdown == host breakdown).

## Risks
- NGO version pin: keep 2.13.2 (tested green here); upgrade only with a re-run of MissionNetProtocolTests parity as merge gate.
- Console: NGO ships UTP; on PS/XSX requires platform transport adapter - keep the loop clean since NetCommand is transport-agnostic by design.
