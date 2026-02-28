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
- migliorare il read model di search con ranking ibrido testo+vettori (ora testo+fuzzy+candidate retrieval e attivo)
- rendere persistenti le code di processing e rebuild oggi solo recuperabili via startup scan

## Medio

- stabilizzare projection pipeline OpenSearch con monitoring e retry su failure transienti
- definire summary document e insight document per retrieval
- migliorare ranking del contesto in chat
- preparare passaggio da RDS PostgreSQL a Aurora PostgreSQL
- arricchire ancora la pagina dettaglio entry con audit e metadata di rielaborazione
- introdurre active learning minimale quando manca informazione critica su split e settlement

## Futuro

- wearable and calendar integrations
- alert pattern intelligenti
- agent workflows multi-step
- personal strategy memory
