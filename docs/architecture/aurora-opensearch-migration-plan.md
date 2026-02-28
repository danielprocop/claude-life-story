# Aurora And OpenSearch Migration Plan

## Obiettivo

Preparare l'evoluzione della piattaforma dati senza interrompere il prodotto e senza riscrivere il dominio applicativo.

## Stato di partenza

- backend su PostgreSQL
- modello EF Core gia esistente
- deploy backend su App Runner
- multiutente gia impostato con Cognito

## Target

- Aurora PostgreSQL come source of truth gestito e piu solido
- OpenSearch come read model per retrieval e ricerca AI

## Strategia

### Fase 1. Stabilizzazione

- congelare il write model relazionale
- verificare che tutte le entita canoniche usino `UserId`
- rimuovere dipendenze residue dal profilo demo

### Fase 2. Aurora readiness

- parametrizzare connection string e ambiente senza assunzioni locali
- verificare compatibilita schema EF Core con Aurora PostgreSQL
- preparare strategia di migration e bootstrap
- validare connessioni App Runner -> Aurora

### Fase 3. Search projection

- definire documenti di indice
- definire job di projection
- definire backfill iniziale da PostgreSQL a OpenSearch
- definire reindex controllato

### Fase 4. Agent retrieval

- usare OpenSearch per full-text e semantic retrieval
- usare PostgreSQL per dati canonici e aggiornamenti
- unire i due livelli in servizi applicativi e AI orchestration

## Principi

- no big bang migration
- source of truth unico
- projection asincrona e idempotente
- user isolation garantita anche negli indici

## Primo deliverable tecnico consigliato

- creare servizi di proiezione entry -> search document
- non attivare ancora OpenSearch in produzione
- preparare solo il contratto interno e il piano di backfill
