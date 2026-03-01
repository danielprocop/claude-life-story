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
