# System Overview

## Obiettivo architetturale

L'architettura deve supportare:

- utenti multipli con isolamento rigoroso
- input libero ad alto volume nel tempo
- estrazione strutturata
- memoria persistente
- retrieval per AI agents
- crescita graduale senza rifare tutto a ogni fase

## Stack deciso

### Frontend

- Angular
- deploy su AWS Amplify Hosting

### Identity

- Amazon Cognito
- ogni richiesta API deve essere associata a una identita utente

### Backend

- ASP.NET Core .NET 8
- API deployata su AWS App Runner

### Source of truth dati

- PostgreSQL oggi
- target evolutivo: Aurora PostgreSQL

### Search e retrieval

- OpenSearch come proiezione di lettura e ricerca

### File e raw memory

- S3

### AI layer

- Bedrock o equivalente
- orchestrazione sopra source of truth + search projection

## Principio chiave

Write model e read model non devono essere la stessa cosa per forza.

La scrittura canonica va su store relazionale.
La ricerca avanzata va su indice dedicato.

## Flussi principali

## 1. Ingestion

Chat o journal entry ->
API backend ->
salvataggio entry utente ->
pipeline AI ->
estrazione concetti, goal signals, insight, connessioni ->
aggiornamento dati canonici

## 2. Retrieval conversazionale

Richiesta utente ->
contesto Cognito ->
recupero dati per utente ->
query full-text / semantic su OpenSearch ->
merge con dati canonici ->
risposta AI con contesto reale

## 3. Review e report

Range temporale ->
entry + concepts + energy + goals per utente ->
retrieval e aggregazione ->
output narrativo e operativo

## Isolamento per utente

Regola non negoziabile:

- Cognito autentica
- backend risolve una identita utente interna stabile
- ogni read e write usa quel `UserId`
- search projection deve essere anche essa user-scoped

## Decisione su DynamoDB

DynamoDB non e il database principale del prodotto.

Se verrra usato, sara per casi tecnici specifici:

- stato job
- idempotency
- cache
- workflow ad alta frequenza ma basso valore relazionale
