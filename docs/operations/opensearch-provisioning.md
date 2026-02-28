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

With endpoint active, next implementation step is:

1. implement real `ISearchProjectionService` with OpenSearch upsert/delete
2. project `EntityCard` documents for canonical entities
3. add query path for node retrieval and semantic candidate search
