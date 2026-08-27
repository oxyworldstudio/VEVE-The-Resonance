# VEVÈ: The Resonance — I 10 Agenti Specializzati

Ogni agente lavora SOLO nel proprio perimetro. Il Coordinatore (`COORDINATOR.md`) assegna, sincronizza e verifica.
Fonte di verità condivisa: `PROJECT_BRIEF.md`. Stato lavori: `BOARD.md`.

---

## AGENT 01 — Gameplay Core & Player
- **Perimetro**: `PlayerController.cs`, `MovementSimulation.cs`, `LookController.cs`, `TerrainProfile.cs`
- **Mandato**: movimento con inerzia, postura, terreno, carico; mai movimento arcade.
- **Vietato**: toccare arma, IA, UI, persistenza.

## AGENT 02 — Armi & Manutenzione
- **Perimetro**: `Weapon.cs`, `WeaponDefinition.cs`, `Maintenance.cs`, `CarbineDefinition.asset`
- **Mandato**: handling, rinculo, calore, usura, sporcizia, inceppamenti causati, ricarica normale/tattica.
- **Vietato**: modificare balistica o fisiologia.

## AGENT 03 — Balistica & Materiali
- **Perimetro**: `Ballistics.cs`, `MaterialDefinition.cs`, `CoverVolume` (in `CombatState.cs`)
- **Mandato**: energia, penetrazione, deviazione, frammentazione, copertura vs occultamento.
- **Vietato**: duplicare logica in Weapon (Weapon consuma Ballistics).

## AGENT 04 — Fisiologia & Medicina
- **Perimetro**: `PhysiologyState.cs`, `Damageable.cs`, `FieldMedic` (in `CombatState.cs`)
- **Mandato**: ferite localizzate, emorragia, dolore, fratture, battito, respirazione; cure che stabilizzano.
- **Vietato**: introdurre barre salute.

## AGENT 05 — IA Tattica
- **Perimetro**: `EnemyAwareness.cs`, `CombatState.cs` (stati IA), `SoundPropagation.cs`, `TacticalSound.cs`
- **Mandato**: percezione imperfetta (vista+udito), memoria, stati (pattuglia/investigazione/ingaggio/soppressione), errori plausibili.
- **Vietato**: conoscenza onnisciente del giocatore.

## AGENT 06 — Ambiente & Percezione Sensoriale
- **Perimetro**: `EnvironmentSimulation.cs`, `SmokeVolume.cs`, `AudioOcclusion.cs`, `DiegeticReadout.cs`
- **Mandato**: meteo, luce, fumo, occlusione audio, display diegetico a strati (Simulazione/Standard/Accessibilità).
- **Vietato**: HUD tradizionale.

## AGENT 07 — Logistica & Inventario
- **Perimetro**: `Inventory.cs`, `Maintenance.cs` (aspetti carico), batterie, munizioni
- **Mandato**: volume/peso reali, accessibilità tasche, deterioramento, distribuzione carico.
- **Vietato**: griglie astratte senza conseguenze fisiche.

## AGENT 08 — Persistenza & Campagna
- **Perimetro**: `MissionPersistence.cs`, `CampaignSystem.cs`, `MissionRuntime`
- **Mandato**: salvataggio atomico versionato, eventi significativi, operatori, permadeath (3 modalità).
- **Vietato**: persistere dettagli non strategici.

## AGENT 09 — Grafica & Ottimizzazione
- **Perimetro**: materiali, luci, qualità, profili prestazioni, `WebGLBuilder.cs`
- **Mandato**: fallback scalabile (raster → RT ibrido → path), profili qualità, benchmark misurabili.
- **Vietato**: effetti che non producono conseguenze percepibili.

## AGENT 10 — QA & Verifica
- **Perimetro**: `Assets/VEVE/Tests/`, diagnostica, `SimulationDiagnostics.cs`
- **Mandato**: test deterministici per ogni sistema, audit riferimenti, regressioni, misurazioni.
- **Vietato**: cambiare gameplay (solo hook diagnostici minimi).

---

## Regole comuni a tutti
1. Analizza i file esistenti prima di modificare.
2. Non duplicare sistemi esistenti.
3. Resta nel perimetro.
4. Dati configurabili senza ricompilare.
5. Test per comportamenti deterministici.
6. Valida: compilazione + test + build + scena integra.
7. Dichiara file toccati, dipendenze, test, limiti.
8. Priorità: coerenza → leggibilità → prestazioni → verificabilità.