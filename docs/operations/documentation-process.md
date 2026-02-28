# Documentation Process

## Regola

Ogni conversazione rilevante deve aggiornare la documentazione del repo.

Per "rilevante" si intende una conversazione che cambia almeno una di queste cose:

- visione prodotto
- priorita roadmap
- architettura
- scelta dati o infrastruttura
- stato deploy o sicurezza
- comportamento operativo del sistema

## Obiettivo

Evitare che il contesto resti solo nella chat.

La repo deve contenere sempre il quadro aggiornato di:

- cosa stiamo costruendo
- perche
- con quali scelte
- cosa manca
- qual e il prossimo passo sensato

## Regole pratiche

### Regola 1

Se cambia il prodotto, aggiornare almeno un file sotto `docs/product`.

### Regola 2

Se cambia una scelta tecnica o infrastrutturale, aggiornare almeno un file sotto `docs/architecture` o `docs/operations`.

### Regola 3

Se la conversazione porta una decisione nuova o chiude un dubbio importante, aggiungere una voce al `conversation-log`.

### Regola 4

Se lo stato del deploy o della sicurezza cambia, aggiornare `deploy-status.md`.

## Checklist da seguire a fine conversazione

- la visione e ancora corretta?
- la roadmap va aggiornata?
- e stata presa una decisione architetturale?
- e cambiato lo stato deploy?
- serve una nuova entry nel conversation log?

## File da toccare piu spesso

- `docs/product/vision.md`
- `docs/product/roadmap.md`
- `docs/architecture/system-overview.md`
- `docs/architecture/data-platform-decision.md`
- `docs/operations/deploy-status.md`
- `docs/operations/conversation-log.md`
