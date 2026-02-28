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
