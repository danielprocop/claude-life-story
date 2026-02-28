# Release Runbook

## Scopo

Descrivere il percorso standard di rilascio e i controlli minimi da fare.

## Trigger standard

Il trigger atteso e:

- commit
- push su `main`
- GitHub Actions per backend
- GitHub Actions per frontend + deploy manuale Amplify via API
- deploy backend
- deploy frontend

## Backend release path

1. build immagine Docker
2. push su ECR
3. trigger `apprunner start-deployment`
4. verifica `/health`

## Frontend release path

1. build frontend in GitHub Actions
2. creare zip da `frontend/dist/frontend/browser`
3. chiamare `amplify create-deployment` su app e branch
4. caricare zip su `zipUploadUrl`
5. chiamare `amplify start-deployment` con `jobId`
6. verificare `get-job` in stato `SUCCEED`

## Prerequisiti

- workflow presente su `main`
- ruolo OIDC GitHub configurato in AWS
- ECR raggiungibile
- App Runner configurato con ruolo corretto
- Amplify app e branch esistenti

## Controlli post-release

- backend `/health` risponde
- login Cognito funziona
- una entry utente si salva correttamente
- la stessa entry non e visibile ad altro utente
- frontend carica senza errori di auth

## Cosa fare se GitHub Action non deploya

- verificare che il workflow sia davvero nel branch `main`
- verificare lo stato del run in GitHub Actions
- verificare che il ruolo OIDC possa essere assunto
- verificare che il job abbia permessi `id-token: write`
- controllare che App Runner mostri nuove operations

## Cosa fare se App Runner non aggiorna

- verificare `list-operations`
- verificare l'immagine `latest` in ECR
- verificare ruolo instance per secrets SSM
- verificare runtime secrets e health check

## Cosa fare se Amplify non aggiorna

- verificare nel run GitHub Actions i passaggi `create-deployment`, upload zip e `start-deployment`
- verificare lo stato del job con `aws amplify get-job --app-id ... --branch-name main --job-id ...`
- verificare che lo zip caricato contenga i file statici nella root (`index.html`, js, css)
- verificare il ruolo OIDC usato dal workflow per chiamare Amplify APIs
- nota: la repo connection Amplify non e necessaria nel percorso attuale

## Sicurezza release

- non usare access key statiche se OIDC e disponibile
- non tenere segreti in chiaro nelle env runtime
- ruotare subito i segreti se sono stati esposti
