import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { Api, ClarificationQuestionResponse, DashboardResponse, OpenDebtResponse, PersonalModelResponse } from '../services/api';

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
  loading = signal(true);
  profileLoading = signal(true);
  operationsBusy = signal(false);
  operationsMessage = signal('');

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

    this.api.getProfileQuestions().subscribe({
      next: (questions) => this.questions.set(questions),
    });
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
    this.operationsMessage.set('Rebuild in coda. La memoria verra rigenerata in background.');

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
    this.operationsMessage.set('Reindex in corso...');

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
}
