import { CommonModule, DatePipe } from '@angular/common';
import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  Api,
  EntityDebugResponse,
  FeedbackApplyResponse,
  FeedbackCaseSummaryResponse,
  FeedbackPreviewResponse,
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
export class FeedbackAdminPage implements OnInit {
  readonly loading = signal(true);
  readonly error = signal('');
  readonly feedbackStatus = signal('');

  readonly policyVersion = signal<PolicyVersionResponse | null>(null);
  readonly policySummary = signal<PolicySummaryResponse | null>(null);
  readonly reviewQueue = signal<FeedbackReviewQueueItemResponse[]>([]);
  readonly cases = signal<FeedbackCaseSummaryResponse[]>([]);

  readonly selectedTemplate = signal('T4');
  readonly payloadText = signal('{\n  "entity_id": "",\n  "new_type": "place",\n  "reason": "manual_type_correction"\n}');
  readonly reason = signal('');

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

  constructor(private api: Api) {}

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.loading.set(true);
    this.error.set('');
    this.feedbackStatus.set('');

    let pending = 4;
    const done = () => {
      pending -= 1;
      if (pending <= 0) {
        this.loading.set(false);
      }
    };

    this.api.getPolicyVersion().subscribe({
      next: value => this.policyVersion.set(value),
      error: () => this.error.set('Accesso admin non disponibile o policy non raggiungibile.'),
      complete: done,
    });

    this.api.getPolicySummary().subscribe({
      next: value => this.policySummary.set(value),
      complete: done,
    });

    this.api.getFeedbackReviewQueue().subscribe({
      next: queue => this.reviewQueue.set(queue),
      complete: done,
    });

    this.api.getFeedbackCases(undefined, undefined, 40).subscribe({
      next: cases => this.cases.set(cases),
      complete: done,
    });
  }

  useQueueSuggestion(item: FeedbackReviewQueueItemResponse): void {
    this.selectedTemplate.set(item.suggestedTemplateId);
    this.payloadText.set(this.prettyJson(item.suggestedPayloadJson));
    this.reason.set(`review_queue:${item.issueType}`);
    this.previewResult.set(null);
    this.applyResult.set(null);
  }

  previewCase(): void {
    const payload = this.parsePayload();
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
    const payload = this.parsePayload();
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
        this.selectedTemplate.set(suggestion.suggestedTemplateId);
        this.payloadText.set(this.prettyJson(suggestion.suggestedPayloadJson));
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

  private prettyJson(raw: string): string {
    try {
      return JSON.stringify(JSON.parse(raw), null, 2);
    } catch {
      return raw;
    }
  }
}
