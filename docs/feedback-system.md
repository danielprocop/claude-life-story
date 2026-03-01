# Feedback System

## Scopo

Il feedback system permette a ruoli `ADMIN/DEV/ANNOTATOR` di correggere in modo deterministico la memoria canonica senza affidarsi a testo libero eseguito direttamente.

Principi:

- template guidati come source of truth
- preview impatto prima dell'applicazione
- azioni versionate con audit completo
- replay mirato per riallineare pipeline e indici
- policy compiler user-scoped + globale

## Architettura

- Aurora/PostgreSQL (via EF): source of truth per `feedback_cases`, `feedback_actions`, `policy_versions`, `entity_redirects`, `feedback_replay_jobs`.
- OpenSearch: solo retrieval/indexing.
- Regole attive compilate in `FeedbackPolicyRuleset` con cache per `(policyVersion, userId)`.
- Pipeline ingestion integra il ruleset in:
  - pre-filter token bloccati
  - post-extraction (force-link/alias map)
  - canonicalizzazione write/read via redirect map.

## Template Supportati

Tutti i template producono `FeedbackActions` strutturate.

### T1 `BLOCK TOKEN`

Blocca token che non devono creare entita.

Esempio payload:

```json
{
  "token": "inoltre",
  "applies_to": "PERSON",
  "classification": "CONNECTIVE"
}
```

Output action:

- `BLOCK_TOKEN_GLOBAL`

### T2 `TOKEN TYPE OVERRIDE`

Forza interpretazione linguistica del token.

Esempio payload:

```json
{
  "token": "inoltre",
  "forced_type": "CONNECTIVE"
}
```

Output action:

- `TOKEN_TYPE_OVERRIDE_GLOBAL`

### T3 `MERGE ENTITIES`

Unisce duplicati con redirect canonico non distruttivo.

Esempio payload:

```json
{
  "entity_a_id": "11111111-1111-1111-1111-111111111111",
  "entity_b_id": "22222222-2222-2222-2222-222222222222",
  "canonical_id": "11111111-1111-1111-1111-111111111111",
  "migrate_alias": true,
  "migrate_edges": true,
  "migrate_evidence": true,
  "reason": "duplicate person"
}
```

Output action:

- `MERGE_ENTITIES`

### T4 `CHANGE ENTITY TYPE`

Corregge il tipo canonico.

Esempio payload:

```json
{
  "entity_id": "11111111-1111-1111-1111-111111111111",
  "new_type": "idea",
  "reason": "not a person"
}
```

Output action:

- `ENTITY_TYPE_CORRECTION`

### T5 `ADD/REMOVE ALIAS`

Gestisce varianti lessicali.

Esempio payload add:

```json
{
  "entity_id": "11111111-1111-1111-1111-111111111111",
  "alias": "Felia",
  "op": "ADD"
}
```

Output action:

- `ADD_ALIAS` o `REMOVE_ALIAS`

### T6 `FORCE LINK RULE`

Forza il linking mention -> entity.

Esempio payload:

```json
{
  "pattern_kind": "NORMALIZED",
  "pattern_value": "miamadre",
  "entity_id": "11111111-1111-1111-1111-111111111111",
  "constraints": {
    "language": "it"
  }
}
```

Output action:

- `FORCE_LINK_RULE`

### T7 `UNDO MERGE` (supportato)

Disattiva redirect creati da un merge action.

Output action:

- `UNDO_MERGE`

### T8 `GENERALIZE RULE FROM EXAMPLE` (supportato)

Regola pattern globale.

Output action:

- `PATTERN_RULE_GLOBAL`

## Concetti chiave (glossario)

- **Template**: una “ricetta” guidata (T1..T8). Definisce *che cosa* vuoi correggere.
- **Payload**: i campi del template (JSON) che descrivono il caso specifico.
- **Action**: la modifica atomica deterministica che il sistema salva e applica (es: `ADD_ALIAS`, `MERGE_ENTITIES`).
- **Preview**: simula (dry-run). Non salva nulla. Serve per vedere actions + impatto stimato.
- **Apply**: salva il case + le actions in Aurora, incrementa `policy_version`, avvia un replay/backfill mirato.
- **Policy version**: versione monotona delle regole attive. Ogni apply/revert incrementa.
- **Replay job**: job asincrono che riallinea canonicalizzazione e indici (OpenSearch) sulle entita/entry impattate.

## Workflow Operativo

UI raccomandata:

- feedback a livello nodo su pagina `/nodes/:id` (preview/apply template guidati)
- console admin su `/feedback-admin` (review queue, replay jobs, case history, revert, debug)
- il vecchio endpoint feedback entry-level e stato rimosso: il feedback ufficiale passa solo dai template admin/node-level
- la route frontend `/feedback-admin` e la navigazione sidebar sono visibili solo a utenti con ruolo `ADMIN/DEV/ANNOTATOR`

Note UI:

- Su `/nodes/:id` i template sono **guidati** e legati al nodo corrente: e la via piu sicura per correzioni locali.
- Su `/feedback-admin` esistono 2 modalita:
  - **Guidato**: campi form + payload auto-generato (consigliato)
  - **JSON**: textarea raw (solo per casi avanzati)
- Il bottone **Assist** su `/feedback-admin` puo precompilare template + payload da testo libero, ma non applica nulla senza conferma.

Workflow API:

1. `GET /api/admin/review-queue`
2. `POST /api/admin/feedback/cases/preview`
3. `POST /api/admin/feedback/cases/apply`
4. replay job automatico (`feedback_replay_jobs`) + enqueue worker
5. audit su `GET /api/admin/entities/{id}/debug`

Endpoint replay status:

- `GET /api/admin/feedback/replay-jobs?status=&take=`

## Esempi rapidi

### Bloccare `inoltre` come persona

1. preview `T1`
2. apply `T1`
3. verificare debug nodo con `BLOCK_TOKEN_GLOBAL`

### Merge duplicato `Adi`

1. preview `T3`
2. apply `T3`
3. verificare redirect attivo e search senza doppioni

### Correggere goal duplicato

1. review queue segnala `DUPLICATE_GOALS`
2. apply `T3` sul pair candidato

### Alias `Felia` -> `Felicia`

1. apply `T5` add alias
2. nuove mention `Felia` risolte via alias map policy

## Come capire “cosa scrivere” nei campi (regole pratiche)

- `Reason`: una frase breve e stabile (es: `type_correction_place`, `alias_typo`, `merge_duplicate`). Serve per audit.
- `T4 new_type`: usa tipi semplici e coerenti (`person`, `place`, `goal`, `idea`, `organization`, `team`, `project`, `activity`).
- `T5 alias`: metti solo la variante (non una frase). Esempio ok: `Felia`. Esempio NO: `mia madre si chiama Felia`.
- `T6 pattern_kind`:
  - `NORMALIZED` e quasi sempre la scelta migliore (gestisce maiuscole/spazi/punteggiatura).
  - `EXACT` solo se vuoi matchare letteralmente (raro).
  - `REGEX` solo se sai bene cosa stai facendo.
- `T6 constraints`: usale per ridurre falsi positivi (`near_tokens`, `within_chars`).
- `T1 applies_to`: preferisci `PERSON` rispetto ad `ANY` se non sei sicuro.

## Revert / Undo

- Revert case: `POST /api/admin/feedback/cases/{id}/revert`
  - tutte le actions attive del case diventano `REVERTED`
  - nuova `policy_version`
  - nuovo replay job
- Undo merge: template `T7` con `merge_action_id`
  - redirect marcati inattivi

## Scope e Sicurezza

- `GLOBAL`: regole condivise (token block/type override/pattern)
- `USER`: regole user-scoped (merge/type/alias/force-link)
- Accesso endpoint admin limitato a `ADMIN|DEV|ANNOTATOR` (claims/groups o allowlist email).

