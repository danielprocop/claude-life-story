# Digital Person Algorithm

Date: 2026-02-28

## Scope

Questo documento descrive l'algoritmo operativo della memoria-cervello personale implementata nel backend.

## Pipeline end-to-end (step 0..7)

### Step 0 - Preprocess

- input: `entry { userId, text, createdAt }`
- normalizzazione testo e tokenizzazione
- risoluzione tempo:
  - date esplicite `dd/MM(/yyyy)`
  - riferimenti relativi `oggi/ieri/domani`
  - fallback su `createdAt`

### Step 1 - Candidate extraction

- estrazione mention candidate:
  - persone, ruoli familiari, nomi parentetici (`Adi(frattello)`)
- estrazione candidate evento:
  - tipo evento (`cena`, `spesa`, ...)
  - partecipanti
  - segnali denaro (`ho speso`, `devo`, `mi deve`, `ho dato`)
- ogni candidato conserva evidence snippet e confidence euristica

### Step 2 - Candidate retrieval

Per risolvere una mention:

1. exact/alias match su Aurora (deterministico)
2. se non basta: candidate retrieval OpenSearch su `EntityCard`
3. fallback fuzzy locale

OpenSearch usa campi: canonicalName, aliases, anchorKey, entityCard, relationHints.

### Step 3 - Decision policy (link/create/merge)

Regole forti:

- role anchor familiari (`mother_of_user`, `brother_of_user`, ...) hanno priorita massima
- `ROLE + si chiama + NAME` aggiorna la stessa entita role-anchor
- alias match esistente => link diretto

Regole probabilistiche:

- fuzzy (Jaro-Winkler) con contesto compatibile
- supporto typo e varianti (`Felicia`/`Felia`)

Anti-collisione:

- se ambiguita alta, niente merge aggressivo
- preferenza a nuova entita o link incerto

### Step 4 - Graph facts

- conversione candidati in fatti atomici:
  - nodi canonici
  - alias
  - evidence
  - eventi con partecipanti

Pensieri/idee restano tracciati via evidence su entry e nodi; evoluzioni possono essere modellate con nuovi facts nel tempo.

### Step 5 - Ledger normalization

Semantica:

- `EventTotal`: totale gruppo evento
- `MyShare`: quota personale utente
- `Settlement`: debito/credito verso controparte

Regole:

- fatti espliciti (`devo X a Y`) prevalgono sulle inferenze
- in eventi con `ha pagato lui/lei`, `MyShare` preferisce l'importo settlement esplicito
- pagamento successivo (`ho dato X a Y`) chiude/riduce settlement open compatibile
- matching pagamento usa direzione + valuta + importo (non "ultimo aperto")

### Step 6 - Index update

Quando entita/alias/proprieta cambiano:

- rigenerazione `EntityCard`
- proiezione su OpenSearch (upsert/delete/reset user)

### Step 7 - Node view aggregation

Node view server-side (`/api/nodes/{id}`):

- persona: alias, relazioni, timeline eventi, saldo debiti/crediti, settlement
- evento: partecipanti, EventTotal, MyShare, settlement, evidence

## Decision policy - Entity resolution

Ordine di priorita:

1. role anchor
2. exact alias/canonical match
3. OpenSearch candidates + ranking locale
4. fuzzy locale
5. create new entity

Ogni merge conserva evidence e resta idempotente.

## Money semantics

Default query semantics:

- "quanto ho speso" => somma `MyShare`
- "quanto abbiamo speso" => `EventTotal` aggregato
- "a chi devo soldi" => settlement `user_owes` con `status != settled`

## Invarianti

- nessun merge senza evidence
- nessun settlement senza `counterparty + amount + currency`
- operazioni di merge/payment idempotenti su stessa entry
- isolamento completo per userId

## Failure modes monitorati

- ambiguita identita in presenza di nomi simili
- pagamenti non mappati per collisioni importo
- inferenze finanziarie senza policy utente esplicita
- OpenSearch indisponibile (fallback a retrieval relazionale + fuzzy)

## Active learning minimale

Se informazione critica mancante (split non esplicitato):

- viene creata una `ClarificationQuestion`
- una sola domanda mirata
- risposta salva `PersonalPolicy` (`default_split_policy`) per evitare nuove domande ripetitive
