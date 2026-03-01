import { CommonModule, DatePipe } from '@angular/common';
import { Component, OnDestroy, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  Api,
  EntityDebugResponse,
  FeedbackApplyResponse,
  FeedbackCaseSummaryResponse,
  FeedbackPreviewResponse,
  FeedbackReplayJobItemResponse,
  FeedbackReviewQueueItemResponse,
  NodeSearchItemResponse,
  PolicySummaryResponse,
  PolicyVersionResponse,
} from '../services/api';

@Component({
  selector: 'app-feedback-admin',
  imports: [CommonModule, FormsModule, DatePipe],
  templateUrl: './feedback-admin.html',
  styleUrl: './feedback-admin.scss',
})
export class FeedbackAdminPage implements OnInit, OnDestroy {
  readonly loading = signal(true);
  readonly error = signal('');
  readonly feedbackStatus = signal('');

  readonly policyVersion = signal<PolicyVersionResponse | null>(null);
  readonly policySummary = signal<PolicySummaryResponse | null>(null);
  readonly reviewQueue = signal<FeedbackReviewQueueItemResponse[]>([]);
  readonly cases = signal<FeedbackCaseSummaryResponse[]>([]);
  readonly replayJobs = signal<FeedbackReplayJobItemResponse[]>([]);
  readonly replayLoading = signal(false);

  readonly selectedTemplate = signal('T4');
  readonly editMode = signal<'wizard' | 'json'>('wizard');
  readonly payloadText = signal('{\n  \"entity_id\": \"\",\n  \"new_type\": \"place\",\n  \"reason\": \"manual_type_correction\"\n}');
  readonly reason = signal('');

  // Wizard fields (guided mode)
  readonly wizardToken = signal('');
  readonly wizardAppliesTo = signal<'ANY' | 'PERSON' | 'GOAL'>('PERSON');
  readonly wizardClassification = signal<'CONNECTIVE' | 'STOPWORD' | 'OTHER'>('CONNECTIVE');
  readonly wizardForcedTokenType = signal<'STOPWORD' | 'CONNECTIVE' | 'ROLE_TRIGGER' | 'TEMPORAL' | 'OTHER'>('CONNECTIVE');

  readonly wizardEntityQuery = signal('');
  readonly wizardEntityResults = signal<NodeSearchItemResponse[]>([]);
  readonly wizardEntitySearchLoading = signal(false);
  readonly wizardEntityId = signal('');

  readonly wizardNewType = signal('place');
  readonly wizardAlias = signal('');
  readonly wizardAliasOp = signal<'ADD' | 'REMOVE'>('ADD');

  readonly wizardPatternKind = signal<'EXACT' | 'NORMALIZED' | 'REGEX'>('NORMALIZED');
  readonly wizardPatternValue = signal('');
  readonly wizardNearTokens = signal('');
  readonly wizardWithinChars = signal<number | null>(null);

  readonly previewLoading = signal(false);
  readonly applyLoading = signal(false);
  readonly previewResult = signal<FeedbackPreviewResponse | null>(null);
  readonly applyResult = signal<FeedbackApplyResponse | null>(null);

  readonly assistText = signal('');
  readonly assistLoading = signal(false);

  readonly entityQuery = signal('');
  readonly entityResults = signal<NodeSearchItemResponse[]>([]);
  readonly entitySearchLoading = signal(false);
  readonly entityDebug = signal<EntityDebugResponse | null>(null);
  readonly entityDebugLoading = signal(false);
  private replayPollTimer: ReturnType<typeof setTimeout> | null = null;

  constructor(private api: Api) {}

  ngOnInit(): void {
    this.reload();
  }

  ngOnDestroy(): void {
    if (this.replayPollTimer) {
      clearTimeout(this.replayPollTimer);
      this.replayPollTimer = null;
    }
  }

  reload(): void {
    this.loading.set(true);
    this.error.set('');
    this.feedbackStatus.set('');

    let pending = 5;
    const done = () => {
      pending -= 1;
      if (pending <= 0) {
        this.loading.set(false);
      }
    };

    this.api.getPolicyVersion().subscribe({
      next: value => this.policyVersion.set(value),
      error: () => {
        this.error.set('Accesso admin non disponibile o policy non raggiungibile.');
        done();
      },
      complete: done,
    });

    this.api.getPolicySummary().subscribe({
      next: value => this.policySummary.set(value),
      error: () => done(),
      complete: done,
    });

    this.api.getFeedbackReviewQueue().subscribe({
      next: queue => this.reviewQueue.set(queue),
      error: () => done(),
      complete: done,
    });

    this.api.getFeedbackCases(undefined, undefined, 40).subscribe({
      next: cases => this.cases.set(cases),
      error: () => done(),
      complete: done,
    });

    this.loadReplayJobs(done);
  }

  useQueueSuggestion(item: FeedbackReviewQueueItemResponse): void {
    this.editMode.set('json');
    this.selectedTemplate.set(item.suggestedTemplateId);
    this.payloadText.set(this.prettyJsonText(item.suggestedPayloadJson));
    this.reason.set(`review_queue:${item.issueType}`);
    this.previewResult.set(null);
    this.applyResult.set(null);
  }

  previewCase(): void {
    const payload = this.editMode() === 'wizard' ? this.buildWizardPayload() : this.parsePayload();
    if (!payload || this.previewLoading()) return;

    this.previewLoading.set(true);
    this.feedbackStatus.set('');
    this.previewResult.set(null);
    this.applyResult.set(null);

    this.api
      .previewFeedbackCase({
        templateId: this.selectedTemplate().trim(),
        templatePayload: payload,
        reason: this.reason().trim() || undefined,
      })
      .subscribe({
        next: result => {
          this.previewResult.set(result);
          this.previewLoading.set(false);
        },
        error: () => {
          this.feedbackStatus.set('Preview fallita: payload non valido o permessi insufficienti.');
          this.previewLoading.set(false);
        },
      });
  }

  applyCase(): void {
    const payload = this.editMode() === 'wizard' ? this.buildWizardPayload() : this.parsePayload();
    if (!payload || this.applyLoading()) return;

    this.applyLoading.set(true);
    this.feedbackStatus.set('');
    this.applyResult.set(null);

    this.api
      .applyFeedbackCase({
        templateId: this.selectedTemplate().trim(),
        templatePayload: payload,
        reason: this.reason().trim() || undefined,
      })
      .subscribe({
        next: result => {
          this.applyResult.set(result);
          this.applyLoading.set(false);
          this.feedbackStatus.set(`Apply completata: case ${result.caseId}, policy v${result.policyVersion}.`);
          this.monitorReplayJob(result.replayJob.id);
          this.reload();
        },
        error: () => {
          this.feedbackStatus.set('Apply fallita: verifica template/payload.');
          this.applyLoading.set(false);
        },
      });
  }

  revertCase(caseId: string): void {
    if (!caseId) return;

    this.feedbackStatus.set('');
    this.api.revertFeedbackCase(caseId).subscribe({
      next: result => {
        this.feedbackStatus.set(
          `Case ${result.caseId} revertita. Policy v${result.policyVersion}, azioni revertite ${result.revertedActions}.`
        );
        this.monitorReplayJob(result.replayJob.id);
        this.reload();
      },
      error: () => {
        this.feedbackStatus.set('Revert fallita.');
      },
    });
  }

  assistTemplate(): void {
    const text = this.assistText().trim();
    if (!text || this.assistLoading()) return;

    this.assistLoading.set(true);
    this.feedbackStatus.set('');

    this.api.assistFeedbackTemplate(text).subscribe({
      next: suggestion => {
        this.editMode.set('json');
        this.selectedTemplate.set(suggestion.suggestedTemplateId);
        this.payloadText.set(this.prettyJsonText(suggestion.suggestedPayloadJson));
        this.feedbackStatus.set(
          `Assist: suggerito ${suggestion.suggestedTemplateId} (confidence ${Math.round(suggestion.confidence * 100)}%).`
        );
        this.assistLoading.set(false);
      },
      error: () => {
        this.feedbackStatus.set('Assist non disponibile.');
        this.assistLoading.set(false);
      },
    });
  }

  searchEntities(): void {
    const query = this.entityQuery().trim();
    if (query.length < 2 || this.entitySearchLoading()) {
      this.entityResults.set([]);
      return;
    }

    this.entitySearchLoading.set(true);
    this.api.adminSearchEntities(query).subscribe({
      next: items => {
        this.entityResults.set(items);
        this.entitySearchLoading.set(false);
      },
      error: () => {
        this.entityResults.set([]);
        this.entitySearchLoading.set(false);
      },
    });
  }

  loadEntityDebug(entityId: string): void {
    if (!entityId || this.entityDebugLoading()) return;

    this.entityDebugLoading.set(true);
    this.entityDebug.set(null);
    this.api.getEntityDebug(entityId).subscribe({
      next: debug => {
        this.entityDebug.set(debug);
        this.entityDebugLoading.set(false);
      },
      error: () => {
        this.feedbackStatus.set('Entity debug non disponibile.');
        this.entityDebugLoading.set(false);
      },
    });
  }

  searchWizardEntities(): void {
    const query = this.wizardEntityQuery().trim();
    if (query.length < 2 || this.wizardEntitySearchLoading()) {
      this.wizardEntityResults.set([]);
      return;
    }

    this.wizardEntitySearchLoading.set(true);
    this.api.adminSearchEntities(query).subscribe({
      next: items => {
        this.wizardEntityResults.set(items);
        this.wizardEntitySearchLoading.set(false);
      },
      error: () => {
        this.wizardEntityResults.set([]);
        this.wizardEntitySearchLoading.set(false);
      },
    });
  }

  selectWizardEntity(entity: NodeSearchItemResponse): void {
    this.wizardEntityId.set(entity.id);
    this.wizardEntityQuery.set(`${entity.canonicalName}`);
    this.wizardEntityResults.set([]);
  }

  wizardPayloadPreview(): string {
    const payload = this.buildWizardPayload(false);
    if (!payload) return '';
    return JSON.stringify(payload, null, 2);
  }

  hasReplayFailures(): boolean {
    return this.replayJobs().some(job => job.status.toLowerCase() === 'failed');
  }

  private loadReplayJobs(onDone?: () => void): void {
    this.replayLoading.set(true);
    this.api.getFeedbackReplayJobs(undefined, undefined, 40).subscribe({
      next: jobs => this.replayJobs.set(jobs),
      error: () => {
        this.feedbackStatus.set('Impossibile leggere replay jobs.');
        this.replayLoading.set(false);
        onDone?.();
      },
      complete: () => {
        this.replayLoading.set(false);
        onDone?.();
      },
    });
  }

  private monitorReplayJob(jobId: string): void {
    if (!jobId) {
      return;
    }

    if (this.replayPollTimer) {
      clearTimeout(this.replayPollTimer);
      this.replayPollTimer = null;
    }

    const poll = () => {
      this.api.getFeedbackReplayJobs(undefined, undefined, 20).subscribe({
        next: jobs => {
          this.replayJobs.set(jobs);
          const target = jobs.find(job => job.id === jobId);
          if (!target) {
            return;
          }

          const status = target.status.toLowerCase();
          if (status === 'queued' || status === 'running') {
            this.replayPollTimer = setTimeout(poll, 3000);
            return;
          }

          if (status === 'failed') {
            this.feedbackStatus.set(`Replay job ${jobId} fallito: ${target.error ?? 'errore non specificato'}.`);
          }
        },
      });
    };

    poll();
  }

  private parsePayload(): Record<string, unknown> | null {
    const raw = this.payloadText().trim();
    if (!raw) {
      this.feedbackStatus.set('Inserisci payload JSON.');
      return null;
    }

    try {
      const parsed = JSON.parse(raw) as unknown;
      if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) {
        this.feedbackStatus.set('Payload JSON deve essere un oggetto.');
        return null;
      }

      return parsed as Record<string, unknown>;
    } catch {
      this.feedbackStatus.set('Payload JSON non valido.');
      return null;
    }
  }

  private buildWizardPayload(setStatusOnError = true): Record<string, unknown> | null {
    const templateId = this.selectedTemplate().trim().toUpperCase();

    const fail = (message: string) => {
      if (setStatusOnError) this.feedbackStatus.set(message);
      return null;
    };

    switch (templateId) {
      case 'T1': {
        const token = this.wizardToken().trim();
        if (!token) return fail('Inserisci token da bloccare.');
        return {
          token,
          applies_to: this.wizardAppliesTo(),
          classification: this.wizardClassification(),
        };
      }
      case 'T2': {
        const token = this.wizardToken().trim();
        if (!token) return fail('Inserisci token.');
        return {
          token,
          forced_type: this.wizardForcedTokenType(),
        };
      }
      case 'T4': {
        const entityId = this.wizardEntityId().trim();
        if (!entityId) return fail('Seleziona una entita (entity_id).');
        const newType = this.wizardNewType().trim();
        if (!newType) return fail('Inserisci new_type.');
        return { entity_id: entityId, new_type: newType, reason: this.reason().trim() || undefined };
      }
      case 'T5': {
        const entityId = this.wizardEntityId().trim();
        if (!entityId) return fail('Seleziona una entita (entity_id).');
        const alias = this.wizardAlias().trim();
        if (!alias) return fail('Inserisci alias.');
        return { entity_id: entityId, alias, op: this.wizardAliasOp() };
      }
      case 'T6': {
        const entityId = this.wizardEntityId().trim();
        if (!entityId) return fail('Seleziona una entita (entity_id).');
        const patternValue = this.wizardPatternValue().trim();
        if (!patternValue) return fail('Inserisci pattern_value.');

        const constraints: Record<string, unknown> = {};
        const nearTokens = this.wizardNearTokens()
          .split(',')
          .map(item => item.trim())
          .filter(item => item.length > 0);
        if (nearTokens.length > 0) constraints['near_tokens'] = nearTokens;
        const withinChars = this.wizardWithinChars();
        if (typeof withinChars === 'number' && withinChars > 0) constraints['within_chars'] = withinChars;

        return {
          pattern_kind: this.wizardPatternKind(),
          pattern_value: patternValue,
          entity_id: entityId,
          constraints,
        };
      }
      default:
        return fail('Template non supportato in modalita guidata. Passa a JSON per casi avanzati.');
    }
  }

  prettyJsonText(raw: string): string {
    try {
      return JSON.stringify(JSON.parse(raw), null, 2);
    } catch {
      return raw;
    }
  }
}
