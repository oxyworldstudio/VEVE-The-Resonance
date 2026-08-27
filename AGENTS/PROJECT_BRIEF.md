# VEVÈ: The Resonance — Brief di Progetto (fonte di verità)

## Principio dominante: REALISMO CAUSALE
Ogni conseguenza deriva da una causa simulata, percepibile e comprensibile.
Sequenza fondamentale: **causa → percezione → decisione → conseguenza → adattamento**.
Vietata la casualità arbitraria e i sistemi decorativi senza valore di gameplay.

## Definizione
Il simulatore di combattimento tattico in cui ogni decisione è limitata dal corpo,
dall'equipaggiamento, dall'ambiente e dalle informazioni realmente disponibili.

## 12 pilastri (sintesi operativa)
1. **Fisiologia senza barra salute**: emorragia, trauma, dolore, fratture, mobilità arti, respirazione, battito, stress, coscienza, temperatura, idratazione. Le cure stabilizzano, non riparano.
2. **Armi fisiche**: massa, ingombro, calore, usura, sporcizia, tolleranze, otturatore, camera, sicura, caricatore. Inceppamenti sempre causati, mai casuali.
3. **Balistica materiale**: energia, distanza, materiale, spessore, penetrazione, deviazione, frammentazione. Copertura ≠ occultamento.
4. **Biomeccanica**: peso, inerzia, frenata, postura, terreno, fatica, battito, respirazione → velocità, rumore, stabilità mira.
5. **Percezione diegetica**: niente HUD invasivo; display al polso, ottiche, suoni, postura, tracce. Tre modalità: Simulazione / Standard / Accessibilità.
6. **Audio tattico**: occlusione, riverbero, attenuazione, materiali, protezione acustica, alterazioni udito rare e giocabili.
7. **IA con informazioni imperfette**: percezione (vista, rumore, tracce), memoria, comunicazione, copertura, ritirata, errori plausibili. Mai conoscenza onnisciente.
8. **Inventario volumetrico**: volume, peso, accessibilità, distribuzione carico, deterioramento, batterie, munizioni.
9. **Persistenza a 3 livelli**: missione / teatro operativo / campagna. Solo conseguenze strategiche. Salvataggio atomico e versionato.
10. **Grafica al servizio del gameplay**: ray/path tracing scalabile (path → ibrido → raster fallback) per riflessi tattici, ombre, volumetrica, visori.
11. **Interfaccia diegetica**: vietati minimappa, barre salute, numeri danno, marker onniscienti.
12. **Permadeath**: modalità Realistica definitiva; modalità Test e Assistita per sviluppo.

## Milestone (ordine obbligatorio)
1. Vertical slice ✅ (completata e pubblicata)
2. Simulazione combattimento ✅ (base: balistica, rinculo, ferite, IA percettiva, malfunzionamenti)
3. Ambiente e percezione ✅ (base: meteo, luce, fumo, occlusione audio)
4. Logistica ✅ (base: inventario volumetrico, manutenzione, persistenza missione)
5. Campagna ✅ (base: operatori, sostituzione, permadeath, salvataggi)
6. **PROSSIMA**: distruzione persistente + approfondimento dei sistemi esistenti

## Criteri di accettazione (una funzionalità è completa solo se)
- funziona in scena riproducibile;
- input/output/stati definiti;
- feedback comprensibile senza onniscienza;
- testata (test deterministici);
- prestazioni misurate;
- non rompe l'esistente;
- salvataggio/caricamento se necessario.