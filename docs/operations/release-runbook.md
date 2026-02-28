# Release Runbook

## Scopo

Descrivere il percorso standard di rilascio e i controlli minimi da fare.

## Trigger standard

Il trigger atteso e:

- commit
- push su `main`
- GitHub Actions per backend
- Amplify native CI/CD per frontend
- deploy backend
- deploy frontend

## Backend release path

1. build immagine Docker
2. push su ECR
3. trigger `apprunner start-deployment`
4. verifica `/health`

## Frontend release path

1. push sul branch collegato ad Amplify
2. Amplify avvia build e deploy
3. verificare il job del branch `main`
4. verifica deploy `SUCCEED`

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

- verificare il job branch `main`
- verificare la connessione repo -> Amplify
- verificare che branch auto build sia abilitato

## Sicurezza release

- non usare access key statiche se OIDC e disponibile
- non tenere segreti in chiaro nelle env runtime
- ruotare subito i segreti se sono stati esposti
