# ROADMAP DI SVILUPPO AAA — VEVÈ: The Resonance

**Documento**: Piano di trasformazione da vertical slice a tactical FPS AAA competitivo  
**Target**: Mercato FPS tattico di nuova generazione  
**Motore**: Unity 6000.3.10f1 / URP  
**Standard riferimento**: COD, Rainbow Six Siege, Escape from Tarkov, Battlefield  
**Formato**: Piano tecnico dettagliato con implementazioni Unity specifiche, metriche, fasi e milestone

---

## 1. ANALISI DELLO STATO ATTUALE

### 1.1 Architettura esistente
- `VEVE.Runtime`: 24 script, balistica, fisiologia, IA, audio, inventario, persistenza
- `VEVE.Tests`: 14 test deterministici
- `SceneBuilder`: scena milestone generata automaticamente
- `WebGLBuilder`: build WebGL funzionante
- Shader RT placeholder e materiali base

### 1.2 Punti di forza
- Balistica realistica con penetrazione materiali
- Fisiologia senza barra salute
- IA con imperfezione percettiva
- Audio spaziale base
- Inventario volumetrico
- Pipeline di build automatizzata

### 1.3 Lacune critiche
- Animazioni assenti
- Grafica RT solo placeholder
- Audio senza middleware
- Nessun contenuto narrativo
- Ottimizzazione limitata

---

## 2. PROFONDITÀ GAMEPLAY

### 2.1 Balistica proiettile fisica
Sostituire hitscan con proiettili fisici:
- `BallisticProjectile` con `Rigidbody`
- Traiettoria: gravità, drag, vento, Coriolis, Magnus
- TrailRenderer basato su energia residua
- Ricochet con angolo critico
- Frammentazione su impatto duro

### 2.2 Sistema armi modulare
- 50+ armi configurabili
- 200+ accessori compatibili
- Inceppamenti causali
- Manutenzione e pulizia
- Malfunzionamenti realistici

### 2.3 Movimento e biomeccanica
- Inertia-based movement
- Slide, crouch jump, mantle
- Postura: in piedi, accovacciato, prono
- Rumore basato su velocità/postura/terreno
- Fatica e consumo energetico

### 2.4 Medicina avanzata
- Tourniquet, emostatico, bendaggio
- Sistema ferite anatomico
- Trattamento con tempo reale
- Classi medico specializzate

---

## 3. FEDELTÀ GRAFICA E TECNICA

### 3.1 Rendering pipeline
- URP ibrido Forward+/Deferred
- Ray tracing configurabile
- Shader PBR completi

### 3.2 Materiali PBR
- SSS per pelle, cera, foglie
- Clearcoat per vernici
- Anisotropy per metalli spazzolati
- Parallax Occlusion Mapping
- Tessellation hardware

### 3.3 Illuminazione
- CSM 4 cascade
- Volumetric fog
- Atmospheric scattering
- Reflection probes
- Light probes 2048+

### 3.4 Post-processing
- ACES tonemapping
- SSAO, SSR, SSGI
- Motion blur, DOF, bloom
- Lens flare, grain, vignette
- Color grading LUT

### 3.5 LOD e culling
- 5 livelli LOD
- Occlusion culling hardware
- Frustum culling
- Billboard per oggetti distanti

---

## 4. COMPLESSITÀ NARRATIVA

### 4.1 Struttura narrativa
- Micro: radio chatter, banter
- Meso: briefing, decisioni, intelligenza
- Macro: arc personaggi, politica, world state

### 4.2 Sistema dialogo
- DialogueNode ScriptableObject
- Condizioni e conseguenze
- Lip-sync viseme-based
- Camera framing

### 4.3 Conseguenze
- Immediate: dialogo, reazione IA
- Mission-level: obiettivi, sblocchi
- Campaign-level: world state persistente

---

## 5. PROGRESSIONE AVANZATA

### 5.1 Progressione operatore
- XP basato su performance
- Skill tree: combattimento, medicina, tecnico, leadership
- Specializzazioni: medic, scout, demolizioni, comms
- Reputazione fazioni

### 5.2 Progressione equipaggiamento
- Armi sbloccabili per livello
- Accessori configurabili
- Personalizzazione estetica
- Durabilità e deterioramento

### 5.3 Sistema achievements
- Obiettivi tattici
- Sfide arma-specifiche
- Record prestazioni

---

## 6. OTTIMIZZAZIONE

### 6.1 Performance budget
- 60 FPS target, 16.67ms frame
- Game logic: 2.0ms
- Animation: 1.5ms
- Physics: 2.0ms
- GPU: shadow 3ms, geometry 4ms, lighting 2ms, post 1.5ms

### 6.2 Ottimizzazioni tecniche
- SRP Batcher
- GPU Instancing
- Texture streaming 512MB
- Shader variant limit: 200 per tipo
- Job System + Burst

---

## 7. PIPELINE TEXTURE E ASSET

### 7.1 Texture workflow
- PBR: albedo, normal, roughness, metallic, AO
- Compression: BC7 colore, BC5 normal
- Mipmaps automatici
- Virtual texturing futuro

### 7.2 Asset organization
- Cartelle: Materials, Textures, Models, Animations, Audio
- Naming convention: `VEVE_Type_AssetName_Variant`
- Metadata: author, date, quality level

---

## 8. SOUND DESIGN

### 8.1 Middleware audio
- FMOD Studio o Wwise
- Banco eventi: 500+ eventi
- Mix real-time: combattimento, stealth, ambient

### 8.2 Audio 3D
- HRTF per headphone
- Convolution reverb
- Occlusione materiale
- Attenuazione distanza
- Doppping ambientale

### 8.3 Weapon audio
- Suoni differenziati per ambiente
- Indoor/outdoor
- Distanza: vicino, medio, lontano
- Alterazioni udito temporanee

---

## 9. CONTENUTO POST-GAME

### 9.1 Modalità Endgame
- Missioni dinamiche generati
- Difese base
- Sfide tempo
- Score attack

### 9.2 Multiplayer foundation
- Architettura netcode-ready
- State synchronization
- Lag compensation
- Dedicated server support

### 9.3 Mod support
- SDK documentato
- Workshop integration
- Custom content pipeline

---

## 10. ROADMAP TEMPORALE

### Fase 1: Fondamenta tecniche (Settimane 1-4)
- Configurazione pipeline avanzata
- Shader PBR completi
- Sistema animazione base
- Audio middleware integration
- Test e profiling

### Fase 2: Gameplay avanzato (Settimane 5-8)
- Proiettili fisici
- Armi modulari complete
- Animazioni gameplay
- IA navigazione e copertura
- Medicina avanzata

### Fase 3: Contenuti (Settimane 9-12)
- Mappa estesa
- Missioni campagna
- Narrazione e dialoghi
- Voice over
- Cutscene

### Fase 4: Polish e ottimizzazione (Settimane 13-16)
- Ottimizzazione rendering
- Bug fixing
- QA esteso
- Performance profiling
- Accessibility

### Fase 5: Post-game e release (Settimane 17-20)
- Contenuti post-game
- Mod support
- Build finale
- Documentazione
- Release preparation

---

## 11. REQUISITI DI RISORSA

### 11.1 Team minimo
- Technical Director: 1
- Gameplay Programmer: 2
- Graphics Programmer: 1
- AI Programmer: 1
- Audio Designer: 1
- Technical Artist: 1
- Level Designer: 2
- Narrative Designer: 1

### 11.2 Budget stimato
- Sviluppo: 18 mesi
- Personale: 8-10 persone
- Licenze: Unity Pro, middleware, asset store
- Hardware: workstation, server build

---

## 12. ANALISI DEI RISCHI

### 12.1 Rischi tecnici
- Ray tracing performance: soluzione ibrida con fallback
- Complessità simulazione: modularità e test
- Ottimizzazione mobile: feature detection

### 12.2 Rischi progetto
- Scope creep: milestone strette
- Dipendenze esterne: asset store vs custom
- Testing: automazione continua

---

*Documento generato per VEVÈ: The Resonance*  
*Versione: 1.0*  
*Data: 2026-08-29*
