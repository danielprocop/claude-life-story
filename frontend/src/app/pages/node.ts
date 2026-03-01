import { CommonModule, DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import {
  Api,
  EntityDebugResponse,
  FeedbackApplyResponse,
  FeedbackCaseRequest,
  FeedbackPreviewResponse,
  NodeSearchItemResponse,
  NodeViewResponse,
} from '../services/api';

type FeedbackTemplateMode = 'type' | 'alias_add' | 'alias_remove' | 'merge' | 'force_link' | 'block_token';

@Component({
  selector: 'app-node-page',
  imports: [CommonModule, DatePipe, FormsModule, RouterLink],
  templateUrl: './node.html',
  styleUrl: './node.scss',
})
export class NodePage implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly api = inject(Api);

  readonly node = signal<NodeViewResponse | null>(null);
  readonly entityDebug = signal<EntityDebugResponse | null>(null);
  readonly loading = signal(true);
  readonly error = signal('');
  readonly debugError = signal('');
  readonly debugLoading = signal(false);

  readonly feedbackMode = signal<FeedbackTemplateMode>('type');
  readonly feedbackReason = signal('');
  readonly feedbackTypeValue = signal('person');
  readonly aliasValue = signal('');
  readonly mergeQuery = signal('');
  readonly mergeCandidates = signal<NodeSearchItemResponse[]>([]);
  readonly mergeSearchLoading = signal(false);
  readonly mergeTargetId = signal('');
  readonly mergeKeepCurrentCanonical = signal(true);
  readonly forcePatternKind = signal<'EXACT' | 'NORMALIZED' | 'REGEX'>('NORMALIZED');
  readonly forcePatternValue = signal('');
  readonly forceNearTokens = signal('');
  readonly forceWithinChars = signal<number | null>(null);
  readonly blockTokenValue = signal('');
  readonly blockAppliesTo = signal<'ANY' | 'PERSON' | 'GOAL'>('PERSON');

  readonly previewLoading = signal(false);
  readonly applyLoading = signal(false);
  readonly previewResponse = signal<FeedbackPreviewResponse | null>(null);
  readonly applyResponse = signal<FeedbackApplyResponse | null>(null);
  readonly feedbackError = signal('');
  readonly feedbackSuccess = signal('');

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.error.set('Nodo non trovato.');
      this.loading.set(false);
      return;
    }

    this.api.getNode(id).subscribe({
      next: (result) => {
        this.node.set(result);
        this.feedbackTypeValue.set(result.kind || 'person');
        this.blockTokenValue.set(result.canonicalName);
        this.loading.set(false);
        this.loadDebug(result.id);
      },
      error: () => {
        this.error.set('Impossibile caricare il nodo richiesto.');
        this.loading.set(false);
      },
    });
  }

  loadDebug(entityId?: string): void {
    const resolvedEntityId = entityId ?? this.node()?.id;
    if (!resolvedEntityId) return;

    this.debugLoading.set(true);
    this.debugError.set('');
    this.api.getEntityDebug(resolvedEntityId).subscribe({
      next: (debug) => {
        this.entityDebug.set(debug);
        this.debugLoading.set(false);
      },
      error: () => {
        this.entityDebug.set(null);
        this.debugLoading.set(false);
        this.debugError.set('Debug admin non disponibile (ruolo ADMIN/DEV/ANNOTATOR richiesto).');
      },
    });
  }

  onModeChange(mode: FeedbackTemplateMode): void {
    this.feedbackMode.set(mode);
    this.previewResponse.set(null);
    this.applyResponse.set(null);
    this.feedbackError.set('');
    this.feedbackSuccess.set('');
  }

  searchMergeCandidates(): void {
    const query = this.mergeQuery().trim();
    if (query.length < 2 || this.mergeSearchLoading()) {
      this.mergeCandidates.set([]);
      return;
    }

    this.mergeSearchLoading.set(true);
    this.api.adminSearchEntities(query).subscribe({
      next: (items) => {
        const currentId = this.node()?.id;
        this.mergeCandidates.set(items.filter(item => item.id !== currentId));
        this.mergeSearchLoading.set(false);
      },
      error: () => {
        this.mergeCandidates.set([]);
        this.mergeSearchLoading.set(false);
      },
    });
  }

  selectMergeCandidate(candidate: NodeSearchItemResponse): void {
    this.mergeTargetId.set(candidate.id);
  }

  previewFeedback(): void {
    if (this.previewLoading() || this.applyLoading()) return;

    const request = this.buildFeedbackRequest();
    if (!request) return;

    this.previewLoading.set(true);
    this.previewResponse.set(null);
    this.applyResponse.set(null);
    this.feedbackError.set('');
    this.feedbackSuccess.set('');

    this.api.previewFeedbackCase(request).subscribe({
      next: (response) => {
        this.previewResponse.set(response);
        this.previewLoading.set(false);
      },
      error: () => {
        this.previewLoading.set(false);
        this.feedbackError.set('Preview feedback fallita. Verifica ruolo admin e payload.');
      },
    });
  }

  applyFeedback(): void {
    if (this.previewLoading() || this.applyLoading()) return;

    const request = this.buildFeedbackRequest();
    if (!request) return;

    this.applyLoading.set(true);
    this.feedbackError.set('');
    this.feedbackSuccess.set('');

    this.api.applyFeedbackCase(request).subscribe({
      next: (response) => {
        this.applyResponse.set(response);
        this.applyLoading.set(false);
        this.feedbackSuccess.set(
          `Feedback applicato. Policy v${response.policyVersion}, replay job ${response.replayJob.id}.`
        );
        this.loadDebug();
      },
      error: () => {
        this.applyLoading.set(false);
        this.feedbackError.set('Apply feedback fallita. Controlla i dati obbligatori.');
      },
    });
  }

  private buildFeedbackRequest(): FeedbackCaseRequest | null {
    const node = this.node();
    if (!node) return null;

    const reason = this.feedbackReason().trim() || `node_feedback_${this.feedbackMode()}`;
    const references = { entityIds: [node.id] };

    switch (this.feedbackMode()) {
      case 'type': {
        const newType = this.feedbackTypeValue().trim().toLowerCase();
        if (!newType) {
          this.feedbackError.set('Inserisci un tipo nodo valido.');
          return null;
        }

        return {
          templateId: 'T4',
          templatePayload: {
            entity_id: node.id,
            new_type: newType,
            reason,
          },
          references,
          reason,
        };
      }
      case 'alias_add':
      case 'alias_remove': {
        const alias = this.aliasValue().trim();
        if (!alias) {
          this.feedbackError.set('Inserisci alias.');
          return null;
        }

        return {
          templateId: 'T5',
          templatePayload: {
            entity_id: node.id,
            alias,
            op: this.feedbackMode() === 'alias_add' ? 'ADD' : 'REMOVE',
          },
          references,
          reason,
        };
      }
      case 'merge': {
        const targetId = this.mergeTargetId().trim();
        if (!targetId) {
          this.feedbackError.set('Seleziona un nodo target per merge.');
          return null;
        }

        const canonicalId = this.mergeKeepCurrentCanonical() ? node.id : targetId;
        const sourceId = canonicalId === node.id ? targetId : node.id;

        return {
          templateId: 'T3',
          templatePayload: {
            entity_a_id: sourceId,
            entity_b_id: canonicalId,
            canonical_id: canonicalId,
            migrate_alias: true,
            migrate_edges: true,
            migrate_evidence: true,
            reason,
          },
          references: {
            ...references,
            mergeTargetId: targetId,
          },
          reason,
        };
      }
      case 'force_link': {
        const patternValue = this.forcePatternValue().trim();
        if (!patternValue) {
          this.feedbackError.set('Inserisci un pattern di link.');
          return null;
        }

        const nearTokens = this.forceNearTokens()
          .split(',')
          .map(item => item.trim())
          .filter(item => item.length > 0);

        const constraints: Record<string, unknown> = {};
        if (nearTokens.length > 0) constraints['near_tokens'] = nearTokens;
        const withinChars = this.forceWithinChars();
        if (typeof withinChars === 'number' && withinChars > 0) constraints['within_chars'] = withinChars;

        return {
          templateId: 'T6',
          templatePayload: {
            pattern_kind: this.forcePatternKind(),
            pattern_value: patternValue,
            entity_id: node.id,
            constraints,
          },
          references,
          reason,
        };
      }
      case 'block_token': {
        const token = this.blockTokenValue().trim();
        if (!token) {
          this.feedbackError.set('Inserisci token da bloccare.');
          return null;
        }

        return {
          templateId: 'T1',
          templatePayload: {
            token,
            applies_to: this.blockAppliesTo(),
            classification: 'CONNECTIVE',
          },
          references,
          reason,
        };
      }
      default:
        this.feedbackError.set('Template non supportato.');
        return null;
    }
  }
}
