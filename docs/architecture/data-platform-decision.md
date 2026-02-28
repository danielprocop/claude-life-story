# Data Platform Decision

Date: 2026-02-28

## Decision

The application will keep a relational primary store and evolve toward:

- Aurora PostgreSQL as the system of record
- OpenSearch as the search and retrieval layer
- S3 as the raw memory and attachment store
- Cognito for identity and user isolation
- Bedrock or equivalent AI orchestration above these stores

DynamoDB is not the primary database for the product at this stage.

This decision is aligned with the product goal: a personal memory graph with evolving queries, conversational corrections, agent retrieval and long-term timeline analysis.

## Why

The product is not a simple append-only event log. It contains:

- user-isolated journal entries
- goals and sub-goals
- graph-like concepts and connections
- corrections and conversational CRUD
- summaries, reviews, and derived insights

These patterns require:

- transactional consistency
- flexible filtering and reporting
- evolving query shapes
- manageable schema evolution

PostgreSQL fits these needs better than a full rewrite to DynamoDB.

## Role of Each Store

## Aurora PostgreSQL

Use for canonical product data:

- users mirrored from Cognito identities
- entries
- concepts
- connections
- goal items
- chat history
- insights
- energy and stress logs
- audit trails and correction history

## OpenSearch

Use for retrieval and AI-facing search:

- full-text search across entries and summaries
- faceted search by concept, project, person, or period
- vector search for semantic retrieval
- hybrid retrieval for agents and review generation

OpenSearch is a projection layer, not the source of truth.

## S3

Use for durable raw content:

- audio
- images
- exported archives
- raw AI extraction payloads if retained
- future event snapshots for re-indexing

## DynamoDB

Use only for technical high-throughput support cases if needed:

- idempotency keys
- transient workflow state
- ingestion checkpoints
- agent job state
- cache-like access patterns

Do not move the core memory model to DynamoDB now.

## Practical Consequences

- Keep the current EF Core relational model as the primary path.
- Prepare a projection pipeline from PostgreSQL to OpenSearch.
- Keep user isolation centered on Cognito identity plus relational `UserId`.
- Optimize the read path through search projections, not by replacing the write model.

## Migration Path

### Phase 1

- complete multi-user isolation end to end
- keep current PostgreSQL deployment stable
- secure runtime secrets and deployment pipeline

### Phase 2

- move from standalone RDS PostgreSQL to Aurora PostgreSQL
- add projection jobs for OpenSearch indexing
- index entries, concepts, goals, and summaries

### Phase 3

- add semantic retrieval for agents
- add hybrid search and ranking
- add re-index pipelines and backfills

## Rejected Alternative

### Full DynamoDB Rewrite

Rejected for now because it would:

- force access-pattern-first redesign too early
- increase modeling complexity for graph and correction flows
- slow down feature evolution while the domain is still moving
- add operational burden without solving the main current bottleneck

The scaling problem here is more about retrieval and projections than about replacing the transactional store.
