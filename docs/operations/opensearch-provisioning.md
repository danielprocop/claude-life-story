# OpenSearch Provisioning

Date: 2026-02-28

## Goal

Provision an OpenSearch resource that can back `EntityCard` indexing and retrieval for the cognitive graph.

## Current AWS account state

- `aws opensearch list-versions` returns available versions including `OpenSearch_3.3` and `OpenSearch_2.19`.
- OpenSearch Serverless endpoint was not reachable from this environment during checks.
- Domain `diario-search-dev` is now active:
  - domain: `diario-search-dev`
  - region: `eu-west-1`
  - engine: `OpenSearch_2.19`
  - status while writing: `Processing=false`, `DomainProcessingStatus=Active`
  - endpoint: `search-diario-search-dev-a4au7wkolarnygxf7xzyozr7uu.eu-west-1.es.amazonaws.com`

## Recommended first resource

For immediate rollout, use a classic OpenSearch Service domain (not serverless) in `eu-west-1`.

Reasoning:

- deterministic endpoint and networking model
- simplest integration path with current backend services
- enough for hybrid text + vector retrieval in this phase

This recommendation is an architecture inference based on current account/runtime constraints.

## Minimal dev domain (CLI)

```bash
aws opensearch create-domain \
  --region eu-west-1 \
  --domain-name diario-search-dev \
  --engine-version OpenSearch_2.19 \
  --cluster-config InstanceType=t3.small.search,InstanceCount=1,DedicatedMasterEnabled=false,ZoneAwarenessEnabled=false \
  --ebs-options EBSEnabled=true,VolumeType=gp3,VolumeSize=20 \
  --encryption-at-rest-options Enabled=true \
  --node-to-node-encryption-options Enabled=true \
  --domain-endpoint-options EnforceHTTPS=true,TLSSecurityPolicy=Policy-Min-TLS-1-2-2019-07 \
  --access-policies '{"Version":"2012-10-17","Statement":[{"Effect":"Allow","Principal":{"AWS":["arn:aws:iam::388592345191:user/danielp","arn:aws:iam::388592345191:role/DiarioAppRunnerInstanceRole","arn:aws:iam::388592345191:role/GitHubActionsDiarioDeployRole"]},"Action":"es:*","Resource":"arn:aws:es:eu-west-1:388592345191:domain/diario-search-dev/*"}]}'
```

## Verify domain is ready

```bash
aws opensearch describe-domain --region eu-west-1 --domain-name diario-search-dev
```

Wait for:

- `DomainStatus.Processing = false`
- endpoint present

## Cleanup command

```bash
aws opensearch delete-domain --region eu-west-1 --domain-name diario-search-dev
```

## Next integration step

Con endpoint attivo, integrazione applicativa ora implementata:

1. `ISearchProjectionService` reale via `OpenSearchProjectionService`
2. proiezione `EntityCard` per entita canoniche
3. candidate retrieval per entity resolution via `OpenSearchEntityRetrievalService`

## Runtime configuration

Set env vars sul backend:

- `Search__Enabled=true`
- `Search__Endpoint=search-diario-search-dev-a4au7wkolarnygxf7xzyozr7uu.eu-west-1.es.amazonaws.com`
- `Search__Region=eu-west-1`
- `Search__EntityIndex=diario-entities`
- `Search__EntryIndex=diario-entries`
- `Search__GoalIndex=diario-goals`

## Reindex/backfill

Per rigenerare l'indice entita per l'utente corrente:

`POST /api/operations/reindex/entities`

## Runtime operations API

Per controllo rapido in produzione (senza CLI locale):

- `GET /api/operations/search/health` -> stato ping + presenza indici `entity/entry/goal`
- `POST /api/operations/search/bootstrap` -> crea automaticamente indici mancanti
- `POST /api/operations/reindex/entities` -> reindex user-scoped entity cards

## Legacy feedback cleanup

Se servono reset mirati delle vecchie policy entry-level, usare:

- `POST /api/operations/cleanup/legacy-feedback-policies`

Per interventi manuali su DB esiste lo script:

- `docs/operations/scripts/cleanup-legacy-feedback-policies.sql`
