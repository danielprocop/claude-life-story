# Diario Intelligente

Diario Intelligente e una web app AI-native pensata come memoria personale, motore di chiarezza e sistema operativo di avanzamento personale.

L'obiettivo non e creare un semplice diario o un semplice note manager. Il prodotto deve diventare:

- una memoria persistente per utente
- un grafo personale che collega eventi, persone, obiettivi, problemi e pattern
- un sistema che trasforma input libero in dati utili
- un motore che propone micro-step concreti per aumentare la probabilita di raggiungere un obiettivo

## Stato attuale

Il progetto ha gia:

- frontend Angular deployato su Amplify
- backend .NET 8 deployato su App Runner
- autenticazione Cognito
- separazione dati per utente nel backend
- pipeline di deploy GitHub Actions configurata con AWS OIDC

## Scelta architetturale principale

La direzione approvata al momento e:

- Cognito per auth e identity
- PostgreSQL, con target Aurora PostgreSQL, come source of truth
- OpenSearch come layer di retrieval, full-text e vector search
- S3 per allegati e raw memory
- DynamoDB non come database principale, ma solo per eventuali use case tecnici specifici

## Documentazione

La documentazione principale vive in [docs/README.md](e:/development/personal/claude-life-story/docs/README.md).

## Run locale

Backend:

```powershell
cd backend
dotnet build DiarioIntelligente.sln
dotnet run --project DiarioIntelligente.API
```

Frontend:

```powershell
cd frontend
npm ci
npm run build
npm start
```
