# Verification Guide

Date: 2026-02-28

## Test commands

Backend:

```powershell
dotnet test backend/DiarioIntelligente.sln
```

Frontend build:

```powershell
cd frontend
npm run build
```

## Golden scenario checks

### 1) Merge madre/Felicia/Felia

1. crea tre entry:
   - `Le mie figlie sono da mia madre`
   - `Mia madre si chiama Felicia`
   - `Felia oggi...`
2. cerca `madre` su `/api/search` o `/api/nodes?q=madre`
3. verifica un solo nodo persona anchor `mother_of_user` con alias `Felia`

### 2) Evento + soldi + debito

Entry:

`Sono andato a cena con Adi (fratello) e ho speso 100 euro e devo dargli 50 perché ha pagato lui`

Verifiche:

- `/api/nodes?q=adi` -> nodo unico persona con anchor fratello
- `/api/ledger/debts` -> Adi con importo open 50
- node view evento -> `EventTotal=100`, `MyShare=50`

### 3) Query finanziarie

- `GET /api/ledger/debts`
- `GET /api/ledger/debts/Adi`
- `GET /api/ledger/spending/my?from=2026-02-01&to=2026-02-28`
- `GET /api/ledger/spending/events?eventType=cena&from=2026-02-01&to=2026-02-28`

### 4) Feedback implicito

Entry:

`Ho dato 50 ad Adi`

Verifica:

- il settlement precedente passa a `settled` (o `partially_paid` se parziale)
- `/api/ledger/debts/Adi` riduce/azzera importo open

### 5) Active learning minimale

In caso di evento con split non esplicitato:

- `GET /api/profile/questions` mostra al massimo poche domande aperte
- `POST /api/profile/questions/{id}/answer` salva policy personale
- nuova entry simile usa policy senza richiedere nuovamente conferma

### 6) Feedback system admin

UI check rapido:

- apri `/nodes/{entityId}` e usa `Feedback nodo (admin)` con template `T4` o `T5`
- esegui `Preview impatto`, poi `Apply feedback`
- verifica aggiornamento in `Audit / Explainability` e in `/feedback-admin`
- nota: il vecchio endpoint feedback entry-level e stato rimosso; usare solo flow admin/template

1. preview:

```http
POST /api/admin/feedback/cases/preview
{
  "templateId": "T1",
  "templatePayload": {
    "token": "inoltre",
    "applies_to": "PERSON",
    "classification": "CONNECTIVE"
  },
  "targetUserId": "<USER_ID>"
}
```

2. apply:

```http
POST /api/admin/feedback/cases/apply
{
  "templateId": "T1",
  "templatePayload": {
    "token": "inoltre",
    "applies_to": "PERSON",
    "classification": "CONNECTIVE"
  },
  "targetUserId": "<USER_ID>",
  "apply": true
}
```

3. verifica policy/debug:

- `GET /api/admin/policy/version`
- `GET /api/admin/policy/summary?userId=<USER_ID>`
- `GET /api/admin/review-queue?userId=<USER_ID>`
- `GET /api/admin/entities/search?q=inoltre&userId=<USER_ID>`
- `GET /api/admin/entities/{id}/debug?userId=<USER_ID>`

4. revert:

- `POST /api/admin/feedback/cases/{caseId}/revert`
