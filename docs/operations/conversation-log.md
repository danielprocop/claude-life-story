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
