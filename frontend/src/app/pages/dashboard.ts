import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import {
  Api,
  ClarificationQuestionResponse,
  DashboardResponse,
  OpenDebtResponse,
  PersonalModelResponse,
  SearchHealthResponse,
} from '../services/api';

@Component({
  selector: 'app-dashboard',
  imports: [CommonModule, RouterLink],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.scss',
})
export class Dashboard implements OnInit {
  data = signal<DashboardResponse | null>(null);
  profile = signal<PersonalModelResponse | null>(null);
  openDebts = signal<OpenDebtResponse[]>([]);
  questions = signal<ClarificationQuestionResponse[]>([]);
  questionDrafts = signal<Partial<Record<string, string>>>({});
  answeringQuestions = signal<Partial<Record<string, boolean>>>({});
  loading = signal(true);
  profileLoading = signal(true);
  operationsBusy = signal(false);
  operationsMessage = signal('');
  searchHealth = signal<SearchHealthResponse | null>(null);

  constructor(private api: Api) {}

  ngOnInit() {
    this.api.getDashboard().subscribe({
      next: (res) => {
        this.data.set(res);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });

    this.api.getProfile().subscribe({
      next: (profile) => {
        this.profile.set(profile);
        this.profileLoading.set(false);
      },
      error: () => this.profileLoading.set(false),
    });

    this.api.getOpenDebts().subscribe({
      next: (debts) => this.openDebts.set(debts),
    });

    this.reloadQuestions();
  }

  getEnergyColor(level: number): string {
    if (level >= 7) return '#22c55e';
    if (level >= 4) return '#eab308';
    return '#ef4444';
  }

  getStressColor(level: number): string {
    if (level <= 3) return '#22c55e';
    if (level <= 6) return '#eab308';
    return '#ef4444';
  }

  runRebuildMemory(): void {
    if (this.operationsBusy()) return;
    this.operationsBusy.set(true);
    this.operationsMessage.set('Rebuild in coda. Operazione heavy: usa solo dopo cambi algoritmo ingestion.');

    this.api.rebuildMemory().subscribe({
      next: () => {
        this.operationsBusy.set(false);
        this.operationsMessage.set('Rebuild avviato. Attendi 1-2 minuti e aggiorna la pagina.');
      },
      error: () => {
        this.operationsBusy.set(false);
        this.operationsMessage.set('Errore durante il rebuild. Riprova tra pochi secondi.');
      },
    });
  }

  runReindexEntities(): void {
    if (this.operationsBusy()) return;
    this.operationsBusy.set(true);
    this.operationsMessage.set('Reindex in corso (light)...');

    this.api.reindexEntities().subscribe({
      next: (result) => {
        this.operationsBusy.set(false);
        this.operationsMessage.set(`Reindex completato: ${result.reindexed} entita indicizzate.`);
      },
      error: () => {
        this.operationsBusy.set(false);
        this.operationsMessage.set('Errore durante il reindex.');
      },
    });
  }

  runNormalizeEntities(): void {
    if (this.operationsBusy()) return;
    this.operationsBusy.set(true);
    this.operationsMessage.set('Normalize Entities in corso (medium-light)...');

    this.api.normalizeEntities().subscribe({
      next: (result) => {
        this.operationsBusy.set(false);
        this.operationsMessage.set(
          `Normalize completato: gruppi=${result.normalized}, merge=${result.merged}, soppressi=${result.suppressed}, ambigui=${result.ambiguous}, reindexed=${result.reindexed}.`
        );
      },
      error: () => {
        this.operationsBusy.set(false);
        this.operationsMessage.set('Errore durante la normalizzazione entita.');
      },
    });
  }

  runRebuildAndReindex(): void {
    if (this.operationsBusy()) return;
    this.operationsBusy.set(true);
    this.operationsMessage.set('Avvio rebuild + reindex...');

    this.api.rebuildMemory().subscribe({
      next: () => {
        this.api.reindexEntities().subscribe({
          next: (result) => {
            this.operationsBusy.set(false);
            this.operationsMessage.set(
              `Rebuild avviato e reindex completato (${result.reindexed} entita). Aggiorna tra 1-2 minuti.`
            );
          },
          error: () => {
            this.operationsBusy.set(false);
            this.operationsMessage.set('Rebuild avviato, ma reindex fallito.');
          },
        });
      },
      error: () => {
        this.operationsBusy.set(false);
        this.operationsMessage.set('Impossibile avviare il rebuild.');
      },
    });
  }

  runSearchHealth(): void {
    if (this.operationsBusy()) return;
    this.operationsBusy.set(true);
    this.operationsMessage.set('Verifica health OpenSearch in corso...');

    this.api.getSearchHealth().subscribe({
      next: (health) => {
        this.searchHealth.set(health);
        this.operationsBusy.set(false);
        if (!health.enabled) {
          this.operationsMessage.set('Search backend disabilitato in questo ambiente.');
          return;
        }

        this.operationsMessage.set(
          `OpenSearch ping=${health.pingOk ? 'ok' : 'ko'} · entity=${health.entityIndexExists} · entry=${health.entryIndexExists} · goal=${health.goalIndexExists}`
        );
      },
      error: () => {
        this.operationsBusy.set(false);
        this.operationsMessage.set('Errore health check OpenSearch.');
      },
    });
  }

  runSearchBootstrap(): void {
    if (this.operationsBusy()) return;
    this.operationsBusy.set(true);
    this.operationsMessage.set('Bootstrap indici OpenSearch in corso...');

    this.api.bootstrapSearchIndices().subscribe({
      next: (result) => {
        this.operationsBusy.set(false);
        this.operationsMessage.set(
          `Bootstrap completato: creati=${result.createdIndices}, esistenti=${result.existingIndices}, falliti=${result.failedIndices}.`
        );
        this.runSearchHealth();
      },
      error: () => {
        this.operationsBusy.set(false);
        this.operationsMessage.set('Errore durante bootstrap indici OpenSearch.');
      },
    });
  }

  runLegacyFeedbackCleanup(): void {
    if (this.operationsBusy()) return;
    this.operationsBusy.set(true);
    this.operationsMessage.set('Cleanup policy legacy feedback in corso...');

    this.api.cleanupLegacyFeedbackPolicies().subscribe({
      next: (result) => {
        this.operationsBusy.set(false);
        this.operationsMessage.set(`Cleanup completato: ${result.deletedPolicies} policy legacy rimosse.`);
      },
      error: () => {
        this.operationsBusy.set(false);
        this.operationsMessage.set('Errore durante cleanup policy legacy.');
      },
    });
  }

  setQuestionDraft(questionId: string, value: string): void {
    this.questionDrafts.set({
      ...this.questionDrafts(),
      [questionId]: value,
    });
  }

  answerQuestion(questionId: string, suggested?: string): void {
    if (this.answeringQuestions()[questionId]) return;

    const answer = (suggested ?? this.questionDrafts()[questionId] ?? '').trim();
    if (!answer) return;

    this.answeringQuestions.set({
      ...this.answeringQuestions(),
      [questionId]: true,
    });

    this.api.answerProfileQuestion(questionId, answer).subscribe({
      next: () => {
        const drafts = { ...this.questionDrafts() };
        delete drafts[questionId];
        this.questionDrafts.set(drafts);
        this.reloadQuestions();
        this.refreshProfile();
      },
      error: () => {
        this.answeringQuestions.set({
          ...this.answeringQuestions(),
          [questionId]: false,
        });
      },
    });
  }

  private refreshProfile(): void {
    this.profileLoading.set(true);
    this.api.getProfile().subscribe({
      next: (profile) => {
        this.profile.set(profile);
        this.profileLoading.set(false);
      },
      error: () => this.profileLoading.set(false),
    });
  }

  private reloadQuestions(): void {
    this.api.getProfileQuestions().subscribe({
      next: (questions) => {
        this.questions.set(questions);
        const nextState: Partial<Record<string, boolean>> = {};
        for (const question of questions) {
          nextState[question.id] = false;
        }
        this.answeringQuestions.set(nextState);
      },
    });
  }
}
