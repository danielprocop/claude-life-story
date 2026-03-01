# Conversation Log

## 2026-02-28

### Sintesi

Questa conversazione ha consolidato la direzione del progetto.

### Decisioni prodotto

- l'app non e un semplice diario
- il target e un cognitive success engine personale
- il sistema deve costruire una memoria tipo cervello con nodi e connessioni che crescono nel tempo
- gli obiettivi devono essere trasformati in percorsi e micro-step

### Decisioni architetturali

- Cognito e lo strato di auth e identity
- ogni utente deve vedere solo i propri dati
- PostgreSQL resta il source of truth
- il target di evoluzione e Aurora PostgreSQL
- OpenSearch sara il layer di retrieval e ricerca
- DynamoDB non verra usato come database principale del prodotto in questa fase

### Decisioni operative

- deploy AWS impostato con Amplify + App Runner
- workflow GitHub Actions configurato con OIDC
- segreti runtime spostati su SSM Parameter Store
- la documentazione deve essere aggiornata a ogni conversazione rilevante

### Next steps concordati

- consolidare la documentazione viva del progetto
- completare la stabilizzazione multiutente
- preparare il sistema per Aurora PostgreSQL e OpenSearch

### Aggiornamento successivo nella stessa giornata

- il workflow GitHub `Deploy to AWS` e stato confermato presente su `main`
- GitHub Actions ha eseguito il run per il commit `d36609e`
- il job backend e riuscito
- il job frontend e fallito
- decisione operativa: GitHub Actions per backend, Amplify native CI/CD per frontend
- la documentazione e stata estesa con modello di isolamento utente, piano search/indexing e runbook di rilascio

### Aggiornamento successivo

- il push `ac989b5` ha avviato un nuovo run GitHub Actions
- App Runner ha completato con successo una nuova `START_DEPLOYMENT`
- il backend ora espone una astrazione `ISearchProjectionService`
- l'implementazione attuale e `NoOp`, per preparare l'integrazione futura con OpenSearch senza cambiare i flussi applicativi
- e stata aggiunta una ricerca globale per utente su entry, concetti e goal come fallback applicativo prima di OpenSearch
- le review ora espongono anche le entry sorgente usate come base, per aumentare verificabilita e fiducia nell'output AI
- e stata aggiunta una pagina dettaglio entry e la navigazione dalle fonti di chat, review, timeline e ricerca verso la singola entry
- la pagina dettaglio entry ora mostra anche entry correlate basate su concetti condivisi, rafforzando il comportamento di memoria connessa

### Aggiornamento successivo

- le entry ora supportano modifica e cancellazione via API e frontend
- quando una entry cambia o viene eliminata, il sistema mette in coda un rebuild user-scoped della memoria derivata
- il rebuild cancella e ricostruisce concepts, connections, insights, energy logs ed embedding per il solo utente coinvolto
- il dettaglio entry ora espone azioni di modifica ed eliminazione con messaggio esplicito di ricalcolo in background
- il seed dell'utente demo viene creato solo in ambienti senza Cognito configurato
- rischio residuo esplicitato: le code di processing e rebuild sono ancora in-memory e vanno rese persistenti o recuperabili

### Aggiornamento successivo

- all'avvio dell'API ora viene eseguito uno startup recovery dei job persi in memoria
- il recovery ricostruisce sia entry processing non completati sia rebuild utente rimasti sospesi dopo restart o deploy
- il rischio residuo si riduce ma non e chiuso: le code non sono ancora persistenti, sono solo recuperabili dal source of truth

### Aggiornamento successivo

- le API di lettura entry, dashboard e search ora trattano le entry con rebuild pendente come `pending derived data`
- in questo stato non vengono esposti concetti o conteggi stantii per evitare incoerenze subito dopo una correzione utente

### Aggiornamento successivo

- e stato introdotto un nuovo layer cognitivo canonico separato dai soli `Concept`
- il backend ora modella `CanonicalEntity`, alias, evidence, eventi e settlement finanziari
- i ruoli familiari sono trattati come role anchors stabili, per esempio `mother_of_user` e `brother_of_user`
- il caso `mia madre` -> `Felicia` -> `Felia` viene fuso sulla stessa entita canonica
- il caso `Adi(frattello)` viene fuso su un solo nodo persona con anchor di fratello
- il ledger base ora supporta evento + `EventTotal` + `MyShare` + `Settlement` + chiusura tramite pagamento successivo
- e stata aggiunta una `Node View` server-side user-scoped per persona ed evento
- e stato creato `docs/cognitive-graph.md` come documento di riferimento del nuovo modello
- l'utente ha confermato che in questa fase non serve preservare i dati attuali: rollout e reset pulito sono accettabili

### Aggiornamento successivo

- verifica AWS fatta dopo il refactor cognitivo
- nell'account non risultano domini OpenSearch classici attivi
- OpenSearch Serverless non e attualmente raggiungibile da questa macchina
- conseguenza operativa: il nuovo layer `EntityCard` e pronto lato dominio, ma il wiring reale del retrieval richiede prima una risorsa OpenSearch disponibile

### Aggiornamento successivo

- richiesto fix definitivo per rilascio frontend e indicazioni concrete per risorsa OpenSearch
- pipeline GitHub Actions aggiornata: frontend ora viene buildato, zippato e pubblicato su Amplify via `create-deployment` + upload + `start-deployment`
- validazione fatta end-to-end con job Amplify `4` in stato `SUCCEED`
- repo connection Amplify resta assente ma non blocca piu il rilascio
- aggiunto documento operativo `docs/operations/opensearch-provisioning.md` con comando CLI minimo per creare il primo domain OpenSearch in `eu-west-1`

### Aggiornamento successivo

- push del fix workflow: run GitHub `#12` ha mostrato failure frontend al passo `CreateDeployment`
- root cause confermata da CloudTrail: AccessDenied su resource Amplify `.../branches/main/deployments/*`
- aggiornato il ruolo `GitHubActionsDiarioDeployRole` con permessi su `deployments/*` e `jobs/*`, inclusi `amplify:GetJob` e `amplify:ListJobs`
- previsto nuovo push per verificare il rilascio completo backend + frontend in un unico workflow

### Aggiornamento successivo

- push di verifica eseguito dopo il fix IAM
- run GitHub `#13` completato con successo su entrambi i job (`Deploy Backend`, `Deploy Frontend`)
- frontend deploy automatico confermato via Amplify job `5` in stato `SUCCEED`

### Aggiornamento successivo

- OpenSearch abilitato a livello account: avviata creazione del domain `diario-search-dev` in `eu-west-1` (engine `OpenSearch_2.19`)
- fix critico recovery: introdotto `EntryProcessingState` persistente per tracciare il completamento reale del processing entry
- startup recovery ora usa lo stato persistente e non dipende piu da presenza API key / embedding / energy log
- fix ledger: pagamento non viene piu applicato in modo cieco all'ultimo debito aperto; ora usa matching deterministico e salta i casi ambigui
- fix entity/settlement extraction: `devo X a Y` risolve Y anche senza clausola `con ...`
- estesa la suite test con casi su recovery startup e su matching debt/payment

### Aggiornamento successivo

- eseguito reset completo dei dati su PostgreSQL RDS via `TRUNCATE ... RESTART IDENTITY CASCADE` su tutte le tabelle applicative
- retrieval aggiornato: `Search` ora include anche entita canoniche oltre a entry/concetti/goal
- esposta ricerca nodi canonici user-scoped via `GET /api/nodes`
- frontend aggiornato con Node View dedicata (`/nodes/:id`) e link diretti da ricerca e mappa
- pagina `Graph` rifatta come mappa cognitiva canonica (filtro per nome/alias/anchor + apertura nodo)
- introdotto modello personale utente via `GET /api/profile` con contesto compatto, segnali personalita, focus e micro-step adattivi
- dashboard aggiornata per mostrare il modello personale e i micro-step suggeriti
- robustezza NLP migliorata: supporto `si chima` e nomi in minuscolo a inizio frase per merge entita
- test backend portati a 12/12 verdi dopo i nuovi casi (merge typo, node search, personal model)
- build frontend `ng build` completata con successo

### Aggiornamento successivo

- implementata integrazione OpenSearch reale:
  - `OpenSearchProjectionService` per upsert/delete/reset di entry, entity card e goal
  - `OpenSearchEntityRetrievalService` per candidate retrieval su entita
  - wiring dinamico via config/env (`Search__Enabled`, `Search__Endpoint`, `Search__Region`)
- `CognitiveGraphService` ora usa candidate retrieval OpenSearch anche in entity resolution e ranking node search
- aggiunti nuovi endpoint ledger query:
  - `GET /api/ledger/debts`
  - `GET /api/ledger/debts/{counterparty}`
  - `GET /api/ledger/spending/my`
  - `GET /api/ledger/spending/events`
- introdotto active learning minimale:
  - tabella `ClarificationQuestions`
  - tabella `PersonalPolicies`
  - endpoint `GET /api/profile/questions` e `POST /api/profile/questions/{id}/answer`
  - policy `default_split_policy` applicata automaticamente nelle inferenze successive
- aggiunto endpoint operativo `POST /api/operations/reindex/entities` per backfill/reindex entity cards
- aggiunti test su ledger query e clarification/policy flow
- suite backend aggiornata a 14 test verdi (`dotnet test`)

### Aggiornamento successivo

- migliorata la creazione tipi nodo nel grafo canonico:
  - non solo `person`, ma anche `place`, `goal`, `project`, `activity`, `idea`, `emotion`, `problem`, `finance` e tipi dinamici derivati da concept type
- aggiunta estrazione euristica luoghi da testo (`in Milano`, `a Roma`, ecc.) per evitare mappa "tutto persona"
- migliorata resa frontend mappa: pill tipo nodo con colore dinamico per distinguere categorie
- aggiunto endpoint `POST /api/operations/rebuild/memory` per rigenerare la memoria utente con la nuova logica tipi nodo
- aggiunto test dedicato per validare la creazione multi-tipo dei nodi (`place/project/idea`)
- suite backend aggiornata a 15 test verdi

### Aggiornamento successivo

- dashboard aggiornata con card `Operazioni memoria` per trigger manuale di:
  - `Rebuild Memory`
  - `Reindex Entities`
  - `Rebuild + Reindex`
- frontend API client esteso con metodi typed per:
  - `POST /api/operations/rebuild/memory`
  - `POST /api/operations/reindex/entities`
- build frontend (`npm run build`) completata con successo
- suite backend riconfermata verde (`dotnet test`, 15 test passati)

### Aggiornamento successivo

- retrieval UX migliorata:
  - pagina `Search` ora combina `api/search` + `api/nodes` in parallelo, usando il ranking nodi canonici come fonte primaria per le entita
  - aggiunti filtri dinamici per tipo entita (`all`, `person`, `place`, `goal`, ecc.) in search
- mappa cognitiva migliorata:
  - filtri dinamici per tipo nodo con stato attivo
  - conteggio `visibili` vs `totali`
- backend search nodi esteso con `kindCounts` nel payload (`NodeSearchResponse`) per supportare filtri UI dinamici
- fix classificazione nodi:
  - se un token e gia riconosciuto come luogo standalone, non viene creato come nuova persona (evita duplicati `Milano` sia person che place)
- test backend estesi con caso specifico `place != person` su mention standalone
- suite aggiornata: `dotnet test` verde con 16 test; `npm run build` frontend verde

### Aggiornamento successivo

- fix duplicati cross-tipo in retrieval:
  - `SearchNodesAsync` ora sopprime i nodi `person` non-anchor quando esiste un nodo `place` con lo stesso nome normalizzato
  - effetto pratico: casi come `Bressana` non appaiono piu come doppio `person + place` in mappa/ricerca, viene privilegiato `place`
- aggiunto test di regressione `Search_Suppresses_Person_When_Same_Name_Place_Exists`
- suite backend aggiornata a 17 test verdi

### Aggiornamento successivo

- implementato workstream algoritmo + mobile installabile (PWA) con priorita su precisione entity merge
- nuovo endpoint operativo backend:
  - `POST /api/operations/normalize/entities`
  - risposta: `normalized`, `merged`, `suppressed`, `ambiguous`, `reindexed`
- introdotto `EntityNormalizationService` user-scoped:
  - normalizza conflitti `place/person` su stesso `NormalizedCanonicalName`
  - non elimina anchor
  - preserva casi con legami forti come ambigui
  - merge sicuro alias/evidence verso `place` e soppressione `person` debole (`person_suppressed`)
  - idempotenza verificata via test
- hardening ingestion persone/luoghi:
  - aggiunti strong-person hints
  - mention deboli senza segnali forti non creano nuove persone
- `NodeSearchItemResponse` esteso con `resolutionState` (`normal|ambiguous|suppressed_candidate`)
- `NodeViewResponse` esteso con `resolutionNotes` per audit/debug
- dashboard operazioni aggiornata:
  - aggiunto bottone `Normalize Entities`
  - guida esplicita su quando usare `Normalize`, `Reindex`, `Rebuild+Reindex`
- active learning chiuso lato UI:
  - sezione domande rapide ora supporta risposta inline
  - chiamata a `POST /api/profile/questions/{id}/answer` e refresh automatico di profilo/domande
- PWA mobile installabile implementata:
  - aggiunti Service Worker Angular e manifest web app
  - aggiunte icone PWA (`192/512` incluse) e `apple-touch-icon`
  - metadati mobile in `index.html` (`theme-color`, apple web app meta)
  - install flow Android (`beforeinstallprompt`) + fallback istruzioni iOS
  - nuovo servizio frontend `InstallPromptService`
  - layout responsive con topbar mobile + bottom nav core routes + safe-area support
- test e build:
  - `dotnet test` backend verde: 20 test passati
  - `npm run build` frontend verde (resta warning budget SCSS dashboard)

## 2026-03-01

### Aggiornamento successivo

- implementato feedback system admin template-based con persistenza e versionamento policy:
  - nuove tabelle: `FeedbackCases`, `FeedbackActions`, `PolicyVersions`, `EntityRedirects`, `FeedbackReplayJobs`
  - bootstrap schema aggiornato per PostgreSQL e Sqlite
- introdotti servizi:
  - `FeedbackPolicyService` (policy compiler + cache per `policyVersion/userId`)
  - `FeedbackAdminService` (preview/apply/revert/review/debug)
  - `FeedbackReplayService` + queue per replay/backfill mirato
- nuovi endpoint admin:
  - `POST /api/admin/feedback/cases/preview`
  - `POST /api/admin/feedback/cases/apply`
  - `GET /api/admin/feedback/cases`
  - `GET /api/admin/feedback/cases/{id}`
  - `POST /api/admin/feedback/cases/{id}/revert`
  - `GET /api/admin/review-queue`
  - `GET /api/admin/entities/search`
  - `GET /api/admin/entities/{id}/debug`
  - `GET /api/admin/policy/version`
  - `GET /api/admin/policy/summary`
- integrazione ruleset nella pipeline:
  - pre-filter token bloccati in entry processing
  - force-link/alias override in entity resolution
  - canonicalizzazione redirect su search/node-view
- nuova documentazione: `docs/feedback-system.md`
- test aggiunti: `FeedbackSystemTests` con scenari G1..G5 (block token, merge redirect, change type, alias linking, auditability)
- suite backend aggiornata: `dotnet test` verde con 25 test passati

### Aggiornamento successivo

- feedback spostato lato frontend da entry-level a node-level:
  - rimossa UI `Correggi estrazione AI` da `entry-detail`
  - aggiunto pannello `Feedback nodo (admin)` in `/nodes/:id` con template `T1/T3/T4/T5/T6`, preview e apply
- aggiunta console operativa `/feedback-admin`:
  - policy state (`version/summary`)
  - assist template (precompile)
  - review queue -> prefill payload
  - preview/apply case
  - case history + revert
  - entity search + debug explainability
- client API frontend esteso con tutti gli endpoint admin feedback/policy/debug
- routing/nav aggiornati con voce `Feedback`
- documentazione aggiornata:
  - `docs/feedback-system.md` (workflow UI node-level + admin console)
  - `docs/operations/verification-guide.md` (check UI feedback node)

### Aggiornamento successivo

- eseguito audit post-pull su branch `main`:
  - working tree pulito
  - nessun marker di conflitto (`<<<<<<<`, `=======`, `>>>>>>>`)
  - nessun conflitto git aperto tra lavoro feedback precedente e attuale
- analizzato failure GitHub Actions backend su run `22544822066` (e run precedente `22544008031`):
  - job `Deploy Backend` fallito al passo `Deploy to App Runner`
  - frontend nello stesso run completato con successo
  - pattern compatibile con deploy ravvicinati su App Runner (servizio occupato durante `start-deployment`)
- applicato hardening a `.github/workflows/deploy.yml`:
  - aggiunta `concurrency` (`deploy-main`, `cancel-in-progress: true`)
  - step backend `Deploy to App Runner` con wait su operazioni `IN_PROGRESS`
  - retry con backoff su errori transient/busy (`InvalidStateException`/`ConflictException`)

### Aggiornamento successivo

- rimosso definitivamente il feedback entry-level legacy dal backend:
  - eliminato endpoint `POST /api/entries/{id}/feedback/entity`
  - eliminati DTO `EntryEntityFeedbackRequest/Response`
  - eliminato test legacy `EntriesControllerFeedbackTests`
- rimosso anche l'override legacy `entity_kind_override` nel sanitizer di ingestione:
  - `EntryAnalysisSanitizer.SanitizeAsync` non legge piu `PersonalPolicies`
  - la pipeline usa solo il modello feedback admin template-based (`T1..T8`) + ruleset compiler

### Aggiornamento successivo

- completata hardening admin feedback + operations:
  - endpoint `GET /api/admin/feedback/replay-jobs` per monitoraggio replay/backfill
  - UI `/feedback-admin` estesa con sezione `Replay jobs` e warning su job falliti
  - pagina nodo (`/nodes/:id`) aggiornata con stato replay post-apply
- controllo accessi frontend rafforzato:
  - `adminGuard` su route `/feedback-admin`
  - voce menu `Feedback` visibile solo a ruoli `ADMIN|DEV|ANNOTATOR`
  - azioni feedback/debug nella node view bloccate per non-admin
- nuove operation API backend:
  - `GET /api/operations/search/health`
  - `POST /api/operations/search/bootstrap`
  - `POST /api/operations/cleanup/legacy-feedback-policies`
- introdotti servizi diagnostica search:
  - `ISearchDiagnosticsService`
  - `OpenSearchDiagnosticsService` / `NoOpSearchDiagnosticsService`
- aggiunto workflow GitHub `.github/workflows/deploy-alert.yml`:
  - su failure del workflow `Deploy to AWS` apre/aggiorna issue automatica (`ci`, `deploy`, `incident`)
- estesi i test backend:
  - `EntityNormalizationServiceTests`: blocco merge quando `person` e `EventParticipant`
  - `FeedbackSystemTests`: verifica presenza replay jobs dopo apply
- validazione locale:
  - `dotnet test backend/DiarioIntelligente.sln` verde (31 test)
  - `npm run build` frontend verde (restano warning budget SCSS non bloccanti)

### Aggiornamento successivo

- introdotto meccanismo anti-stale cache per frontend PWA:
  - nuovo servizio `PwaUpdateService` (Angular `SwUpdate`)
  - check aggiornamenti periodico ogni 5 minuti
  - check iniziale all'avvio app
  - su `VERSION_READY` attiva nuova versione e forza reload pagina
  - guard anti-loop reload (`localStorage` con finestra 60s)
- wiring via `APP_INITIALIZER` in `app.config.ts` per avvio automatico senza interventi utente

### Aggiornamento successivo

- abilitata modalita temporanea "all users admin" per tools feedback:
  - backend `AdminAuthenticatedController`: ogni utente autenticato viene autorizzato come `ADMIN`
  - flag runtime: `Admin__AllowAllUsers` (default attivo se non impostato)
  - frontend `AuthService.isAdmin`: accesso admin per tutti gli utenti autenticati
  - flag frontend: `environment.admin.allowAllUsers=true`
- effetto pratico: funzionalita feedback/admin disponibili subito anche per utenti esistenti e nuovi senza gestione Cognito groups

### Aggiornamento successivo

- assegnato gruppo Cognito reale `ADMIN` a tutti gli utenti esistenti del pool `eu-west-1_GUYadoxnL`
- creato gruppo `ADMIN` (non presente prima) e aggiunti tutti gli utenti confermati correnti
- aggiunto script operativo riusabile:
  - `docs/operations/scripts/cognito-add-all-users-to-admin.ps1`
  - consente bootstrap/riallineamento rapido del gruppo admin da CLI

### Aggiornamento successivo

- rifattorizzata la pagina nodo per ridurre enfasi finanza e renderla relazione-centrica:
  - rimossa sezione "Finanza personale" in evidenza
  - introdotta sezione "Relazione con te" con indicatori contestuali
  - dettagli economici mostrati solo quando presenti (`> 0` o settlement reali)
- migliorata presentazione eventi:
  - eliminati placeholder fuorvianti tipo `€ -`
  - quando mancano importi/settlement il sistema esplicita "dati minimi"
  - aggiunto link diretto a `entry` sorgente in dettaglio evento
- aggiunta lista movimenti economici come sotto-sezione della relazione, non come card principale del nodo

### Aggiornamento successivo

- aggiunto audit ripetibile "data quality" su Aurora (read-only):
  - nuovo tool `backend/DiarioIntelligente.OpsCli` (comandi `audit`/`export`)
  - nuovo script `docs/operations/scripts/audit-data-quality.ps1` per eseguire audit su ambiente AWS
  - nuovo doc `docs/operations/data-quality-audit.md` con istruzioni operative
- audit su Aurora `prod` (01 Mar 2026) ha rilevato nodi/derivati incoerenti:
  - `Entries`: 21, `CanonicalEntities`: 66, `MemoryEvents`: 7, `EventParticipants`: 1, `Settlements`: 0
  - almeno un nodo `PERSON` creato da pronome/stopword (`lei`)
  - collisioni cross-kind (stesso normalized name con kind diversi: `person+place`, `organization+place`, `food+object`)
  - duplicati `event` per la stessa data (`Evento 2026-03-01`) dentro lo stesso utente
  - eventi `expense` senza importi (`EventTotal/MyShare` null) e senza partecipanti reali
- output dettagliato salvato localmente in `.runlogs/data-quality/<timestamp>/audit/*` (non committato) per ispezione e debug

### Aggiornamento successivo

- migliorata la comprensibilita del feedback system in UI:
  - pagina nodo: aggiunta scheda guida per template (cosa fa / esempi / distinzione preview vs apply)
  - preview actions ora leggibili: label + spiegazione + payload JSON formattato
  - pagina `/feedback-admin`: aggiunta modalita **Guidato** (campi form + payload auto-generato) oltre a modalita **JSON**
  - preview/apply ora mostrano anche la lista actions (non solo impatto)
- aggiunto reset dati user-scoped (ripartenza da zero):
  - backend: `POST /api/operations/reset/me` elimina entry + memoria derivata dell'utente autenticato
  - dashboard: bottone **Reset miei dati** con conferma esplicita
  - doc aggiornata: `docs/operations/data-reset-runbook.md` include percorso user-scoped raccomandato
- estesi i test backend (golden regressions):
  - `lei` non deve creare un nodo `PERSON` anche se l'AI lo etichetta come person
  - `a Irina` non deve creare un nodo `place` quando il nome e un strong person hint

### Aggiornamento successivo

- reset utente reso realmente "clean start":
  - endpoint `POST /api/operations/reset/me` ora supporta `?includeFeedback=true` (default attivo)
  - oltre a entry/memoria, pulisce anche artefatti feedback user-scoped:
    - `FeedbackCases` creati dall'utente
    - `FeedbackActions` target utente
    - `EntityRedirects` collegati a quelle action
    - `FeedbackReplayJobs` target utente
    - `PolicyVersions` create dall'utente
    - `EntryProcessingStates` utente
- dashboard aggiornata:
  - bottone rinominato **Reset completo utente**
  - messaggio di esito include contatori feedback/redirect per verificare pulizia reale
- aggiunto script batch operativo per allineamento:
  - `docs/operations/scripts/alignment-loop.ps1`
  - esegue reset completo -> inserimento N entry (default 100) -> normalize -> apply feedback suggeriti da review queue in loop -> summary JSON in `.runlogs/alignment/<timestamp>/summary.json`
