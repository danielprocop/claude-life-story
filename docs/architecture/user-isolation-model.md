# User Isolation Model

## Obiettivo

Ogni utente deve avere memoria, retrieval, report, goal e storico completamente separati.

Questa non e una feature opzionale. E una regola di base del sistema.

## Principio

L'identita esterna arriva da Cognito.
Il backend la converte in una identita interna stabile.

Da quel punto in poi ogni operazione applicativa e scoped per `UserId`.

## Flusso

1. L'utente effettua login tramite Cognito.
2. Il frontend invia il token Bearer alle API.
3. Il backend valida il JWT Cognito.
4. Il backend risolve il `sub` Cognito in uno `UserId` interno stabile.
5. Se l'utente non esiste nel database, viene creato.
6. Tutte le query e le scritture successive usano lo `UserId` interno.

## Cosa deve essere sempre user-scoped

- entries
- concepts
- connections
- goal items
- chat history
- insights
- energy logs
- review inputs
- search index documents
- future vector embeddings

## Regole pratiche

### API

Ogni controller che legge o scrive dati utente deve derivare dal controller autenticato o usare lo stesso meccanismo di contesto.

### Database

Ogni tabella canonica che contiene memoria utente deve avere `UserId`.

### Search

Ogni documento indicizzato deve includere `UserId` come chiave di filtro obbligatoria.

### AI Retrieval

Ogni retrieval per agenti o review deve filtrare per `UserId` prima di applicare ranking semantico o testuale.

## Failure modes da evitare

- fallback silenzioso su utente demo in produzione
- query di report non filtrate per utente
- documenti OpenSearch senza `UserId`
- merge di dati cross-user in prompt o context windows

## Stato attuale

Il backend ha gia il contesto utente Cognito -> `UserId` interno.

Serve ancora:

- validazione completa end-to-end su tutti gli endpoint
- garantire la stessa regola anche nel futuro layer OpenSearch
