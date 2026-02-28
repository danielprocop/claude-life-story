# Search And Indexing Plan

## Obiettivo

Aggiungere un layer di retrieval serio senza spostare il source of truth fuori dal database relazionale.

## Decisione

OpenSearch verra usato come read model di ricerca e retrieval.

Non sostituisce PostgreSQL. Lo affianca.

## Casi d'uso

- ricerca full-text nelle entry
- ricerca filtrata per periodo, progetto, persona, goal, tema
- retrieval semantico per chat e review
- hybrid retrieval per agenti
- ranking di contesto per summary e insight

## Documenti da indicizzare

### Entry document

Campi previsti:

- id
- userId
- createdAt
- content
- extracted concepts
- linked goals
- energy and stress summary
- embeddings

### Concept document

Campi previsti:

- id
- userId
- label
- type
- frequency
- firstSeenAt
- lastSeenAt
- linked entry ids

### Goal document

Campi previsti:

- id
- userId
- title
- description
- status
- hierarchy context
- related concepts
- latest progress markers

### Insight and summary document

Campi previsti:

- id
- userId
- type
- generatedAt
- text
- linked sources

## Indicizzazione

Il pattern consigliato e:

1. write canonica su PostgreSQL
2. evento applicativo o job asincrono
3. projection builder
4. upsert su OpenSearch

## Stato applicativo attuale

Il codice backend espone gia un'astrazione `ISearchProjectionService`.

Al momento l'implementazione e `NoOp`, cosi:

- il dominio applicativo non dipende ancora da OpenSearch
- controller e pipeline AI hanno gia un punto di aggancio stabile
- il passaggio futuro a una projection reale richiedera sostituire il servizio, non riscrivere i flussi

## Query model

Ogni query deve essere:

1. filtrata per `userId`
2. filtrata per tempo o tipo se disponibile
3. ranked via full-text, semantic search o hybrid search

## Non obiettivi

- non usare OpenSearch come database transazionale
- non salvare solo su OpenSearch
- non introdurre subito sincronizzazione troppo complessa

## Primo incremento consigliato

Partire con:

- entry index
- keyword search
- filtri per utente e periodo

Poi aggiungere:

- embeddings
- hybrid retrieval
- documenti goal e concept

## Dipendenze

- source of truth stabile
- `UserId` coerente su tutti i dati
- pipeline job affidabile
- strategia di reindex
