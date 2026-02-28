# Product Roadmap

## Stato corrente

Gia presenti o impostati:

- journaling/chat frontend
- backend API .NET
- AI extraction pipeline di base
- grafo concettuale iniziale
- dashboard, review, chat, goals, insights
- Cognito login e registrazione
- isolamento dati per utente nel backend
- deploy AWS con Amplify e App Runner

## Cosa manca

## Fase 1. Stabilizzazione prodotto

- verificare end-to-end che ogni endpoint usi sempre il contesto utente corretto
- rimuovere gli ultimi fallback demo dove non piu necessari
- consolidare documentazione viva del progetto
- ruotare segreti esposti in precedenza
- portare tutte le modifiche locali su `main`

## Fase 2. Data platform readiness

- preparare il passaggio da RDS PostgreSQL a Aurora PostgreSQL
- definire modello di proiezione verso OpenSearch
- identificare i documenti da indicizzare: entry, concepts, goals, insights, summaries
- definire chiavi e campi di retrieval per user-scoped search
- definire strategia di reindex e backfill
- definire job di projection affidabili

## Fase 3. Search e AI retrieval

- aggiungere full-text search serio
- aggiungere semantic retrieval
- aggiungere hybrid retrieval per agenti
- costruire recap e review usando anche fonti citate e non solo testo generato

## Fase 4. Goal engine

- rendere i goal percorsi navigabili
- introdurre micro-step suggeriti
- tenere traccia di blocchi, rilanci e route alternative
- far emergere il "best next step" in base a tempo, energia e contesto

## Fase 5. Memory engine avanzato

- rafforzamento nodi e connessioni nel tempo
- gestione conflitti tra fatti e inferenze
- timeline coerente con revisioni e correzioni
- agent workflows piu complessi su memoria personale

## Priorita immediata

L'ordine corretto oggi e:

1. stabilizzare multiutente e deploy
2. consolidare source of truth relazionale
3. introdurre OpenSearch come read/search layer
4. solo dopo aggiungere ottimizzazioni o nuovi datastore tecnici
