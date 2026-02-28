import { CommonModule, DatePipe } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Api, EntryResponse, RelatedEntryResponse } from '../services/api';

@Component({
  selector: 'app-entry-detail',
  imports: [CommonModule, DatePipe, FormsModule, RouterLink],
  templateUrl: './entry-detail.html',
  styleUrl: './entry-detail.scss',
})
export class EntryDetailPage implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly api = inject(Api);

  readonly entry = signal<EntryResponse | null>(null);
  readonly relatedEntries = signal<RelatedEntryResponse[]>([]);
  readonly loading = signal(true);
  readonly error = signal('');
  readonly editing = signal(false);
  readonly draftContent = signal('');
  readonly saving = signal(false);
  readonly deleting = signal(false);
  readonly statusMessage = signal('');

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.error.set('Entry non trovata.');
      this.loading.set(false);
      return;
    }

    this.api.getEntry(id).subscribe({
      next: (entry) => {
        this.entry.set(entry);
        this.draftContent.set(entry.content);
        this.loading.set(false);
        this.loadRelatedEntries(id);
      },
      error: () => {
        this.error.set('Impossibile caricare questa entry.');
        this.loading.set(false);
      },
    });
  }

  private loadRelatedEntries(id: string): void {
    this.api.getRelatedEntries(id).subscribe({
      next: (entries) => this.relatedEntries.set(entries),
    });
  }

  startEditing(): void {
    const entry = this.entry();
    if (!entry) return;

    this.draftContent.set(entry.content);
    this.statusMessage.set('');
    this.editing.set(true);
  }

  cancelEditing(): void {
    const entry = this.entry();
    this.draftContent.set(entry?.content ?? '');
    this.editing.set(false);
    this.statusMessage.set('');
  }

  save(): void {
    const entry = this.entry();
    const content = this.draftContent().trim();
    if (!entry || !content || this.saving()) return;

    this.saving.set(true);
    this.statusMessage.set('');

    this.api.updateEntry(entry.id, content).subscribe({
      next: (updated) => {
        this.entry.set(updated);
        this.draftContent.set(updated.content);
        this.relatedEntries.set([]);
        this.editing.set(false);
        this.saving.set(false);
        this.statusMessage.set(
          'Entry aggiornata. Concetti, connessioni e insight verranno ricalcolati in background.'
        );
        this.loadRelatedEntries(updated.id);
      },
      error: () => {
        this.saving.set(false);
        this.statusMessage.set('Impossibile aggiornare questa entry.');
      },
    });
  }

  deleteEntry(): void {
    const entry = this.entry();
    if (!entry || this.deleting()) return;

    const confirmed = window.confirm(
      'Vuoi eliminare questa entry? I dati derivati verranno ricalcolati in background.'
    );
    if (!confirmed) return;

    this.deleting.set(true);
    this.statusMessage.set('');

    this.api.deleteEntry(entry.id).subscribe({
      next: () => {
        this.router.navigate(['/timeline'], {
          state: {
            notice: 'Entry eliminata. La memoria derivata verra riallineata in background.',
          },
        });
      },
      error: () => {
        this.deleting.set(false);
        this.statusMessage.set('Impossibile eliminare questa entry.');
      },
    });
  }
}
