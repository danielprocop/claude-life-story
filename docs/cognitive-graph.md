# Cognitive Graph

## Obiettivo

Trasformare il diario in una memoria canonica tipo cervello, dove:

- ogni nuova entry viene rimappata contro tutta la conoscenza utente esistente
- ruoli, nomi, alias e typo convergono sulla stessa entita
- eventi e soldi generano fatti interrogabili e non solo testo
- tutto resta spiegabile tramite evidence

## Policy di entity resolution

### 1. Canonical entity

Ogni nodo importante viene salvato come `CanonicalEntity` con:

- `Id` stabile
- `Kind`
- `CanonicalName`
- `AnchorKey` opzionale per ruoli forti come `mother_of_user`
- `EntityCard` per retrieval

### 2. Alias

Ogni entita puo avere alias multipli:

- varianti linguistiche
- typo
- ruoli linguistici
- nomi osservati nel tempo

Esempio:

- canonical: `Felicia`
- anchor: `mother_of_user`
- alias: `mia madre`, `madre`, `mamma`, `Felia`

### 3. Evidence

Ogni merge o proprieta aggiunta salva:

- `entryId`
- snippet
- timestamp
- merge reason

Questo evita un grafo "magico" e rende il sistema debuggabile.

### 4. Regole di risoluzione attive

- `Role anchors` vincono sul matching semantico.
- `mia madre`, `mio fratello`, `mia sorella` creano o risolvono entita stabili.
- Se nella stessa entry co-occorrono ruolo e nome, il nome arricchisce l'entita anchor, non crea un duplicato.
- Se compare un nome simile a un alias esistente, viene fuso via fuzzy match.
- Se il nome non supera le soglie e non esiste anchor compatibile, viene creata una nuova entita.

## Ledger semantics

Per eventi finanziari il sistema usa tre concetti distinti:

- `EventTotal`: totale di gruppo dell'evento
- `MyShare`: quota personale dell'utente, che e la metrica default per "quanto ho speso"
- `Settlement`: debito o credito verso una controparte

### Regole minime

- Se c'e una frase esplicita `devo X a Y`, quel settlement e il fatto piu forte.
- Se c'e `ha pagato lui/lei` e il debito esplicito e `50`, `MyShare` diventa `50`, non `EventTotal`.
- I pagamenti successivi tipo `ho dato 50 ad Adi` riducono o chiudono i settlement aperti.

## Node views

Il backend espone una `Node View` user-scoped via `GET /api/nodes/{entityId}`.

Per la consultazione generale dei nodi canonici e disponibile anche `GET /api/nodes?q=...&limit=...`.

### Persona

Restituisce:

- nome canonico
- alias
- anchor/relazioni forti
- evidence
- eventi condivisi
- saldo aperto `devo / mi deve`
- lista settlement

### Evento

Restituisce:

- tipo evento
- titolo
- partecipanti
- `EventTotal`
- `MyShare`
- settlement collegati
- source entry

## Query supportate dal modello

Il modello attuale abilita in modo affidabile:

- chi e mia madre / mio fratello / ecc.
- quali alias puntano alla stessa persona
- con chi ho fatto una certa cena o uscita
- quanto ho speso io vs quanto abbiamo speso in gruppo
- a chi devo soldi / chi deve soldi a me
- quali evidence giustificano un merge o un settlement
- ricerca nodi canonici per nome, alias e anchor (`madre`, `felicia`, `felia`)

## Modello personale utente

Il backend espone `GET /api/profile` per restituire un modello personale user-scoped con:

- contesto compatto aggiornato
- segnali di personalita derivati dalle entry
- temi filosofici ricorrenti
- focus correnti
- micro-step suggeriti
- regole di adattamento operativo

## OpenSearch e Aurora

- Aurora PostgreSQL resta il source of truth canonico.
- OpenSearch deve essere usato come candidate retrieval layer e read model.
- L'indice corretto non e il solo nome, ma la `EntityCard`: nome + alias + anchor + descrizione.

## Stato implementato nel repo

Gia implementato:

- `CanonicalEntity`
- `EntityAlias`
- `EntityEvidence`
- `MemoryEvent`
- `Settlement`
- `SettlementPayment`
- role anchors familiari
- merge madre/Felicia/Felia
- merge `Adi(frattello)` su un solo nodo
- ledger base cena + debito + pagamento successivo
- `Node View` server-side
- ricerca nodi canonici (`GET /api/nodes`)
- modello personale utente (`GET /api/profile`)

Non ancora implementato:

- retrieval reale OpenSearch per candidate search delle entita
- ranking ibrido testo + vettori sulle `EntityCard`
- domande active learning quando i dati sono insufficienti
- projection OpenSearch reale al posto dell'attuale implementazione no-op

## Reset e rollout

Per questa fase non e richiesta migrazione conservativa dei dati esistenti.

La scelta operativa approvata e:

- partire puliti sul nuovo layer cognitivo
- ricostruire da zero i derivati quando serve
- non investire ora in backfill complesso sui vecchi dati sporchi
