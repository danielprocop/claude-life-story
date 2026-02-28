# Product Backlog

## Critico

- verificare che tutti gli endpoint usino davvero il contesto utente corretto
- ruotare la chiave OpenAI precedentemente esposta
- portare su `main` le ultime modifiche locali ancora non pushate
- confermare il comportamento reale del deploy frontend via Amplify native CI/CD

## Alto

- introdurre source citations anche per risposte chat AI, non solo per review
- aggiungere audit delle correzioni utente
- migliorare il modello goal -> micro-step
- definire read model per search e retrieval
- evolvere la nuova ricerca globale applicativa in retrieval serio con ranking e filtri

## Medio

- introdurre projection pipeline verso OpenSearch
- definire summary document e insight document per retrieval
- migliorare ranking del contesto in chat
- preparare passaggio da RDS PostgreSQL a Aurora PostgreSQL

## Futuro

- wearable and calendar integrations
- alert pattern intelligenti
- agent workflows multi-step
- personal strategy memory
