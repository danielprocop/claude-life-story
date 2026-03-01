import { CommonModule, DatePipe } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Api, ConceptResponse, EntryResponse, RelatedEntryResponse } from '../services/api';

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
  readonly feedbackTerm = signal('');
  readonly feedbackKind = signal('person');
  readonly feedbackNote = signal('');
  readonly feedbackSubmitting = signal(false);

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

  setFeedbackFromConcept(concept: ConceptResponse): void {
    this.feedbackTerm.set(concept.label);
    this.feedbackKind.set(this.normalizeFeedbackKind(concept.type));
    this.statusMessage.set('');
  }

  sendEntityFeedback(): void {
    const entry = this.entry();
    const term = this.feedbackTerm().trim();
    const kind = this.feedbackKind().trim();

    if (!entry || !term || !kind || this.feedbackSubmitting()) return;

    this.feedbackSubmitting.set(true);
    this.statusMessage.set('');

    this.api.submitEntryEntityFeedback(entry.id, term, kind, this.feedbackNote().trim() || undefined).subscribe({
      next: (response) => {
        this.feedbackSubmitting.set(false);
        this.feedbackNote.set('');
        this.statusMessage.set(response.message);
      },
      error: () => {
        this.feedbackSubmitting.set(false);
        this.statusMessage.set('Impossibile inviare il feedback su questa entry.');
      },
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

  private normalizeFeedbackKind(rawType: string): string {
    const normalized = (rawType ?? '').trim().toLowerCase();
    if (!normalized) return 'person';

    if (normalized === 'location' || normalized === 'city' || normalized === 'country') return 'place';
    if (normalized === 'club') return 'team';
    if (normalized === 'company') return 'organization';
    if (normalized === 'task' || normalized === 'habit') return 'activity';
    if (normalized === 'feeling') return 'emotion';
    if (normalized === 'belief' || normalized === 'philosophy') return 'idea';
    if (normalized === 'blocker') return 'problem';
    if (normalized === 'car' || normalized === 'automobile') return 'vehicle';
    if (normalized === 'marca') return 'brand';
    if (normalized === 'model' || normalized === 'modello' || normalized === 'productmodel') return 'product_model';
    if (normalized === 'anno') return 'year';
    if (normalized === 'data') return 'date';
    if (normalized === 'ora' || normalized === 'orario') return 'time';
    if (normalized === 'prezzo' || normalized === 'importo' || normalized === 'costo') return 'amount';
    if (normalized === 'notentity' || normalized === 'nonentity') return 'not_entity';
    if (normalized === 'notperson' || normalized === 'nonperson') return 'not_person';
    if (
      normalized === 'person' ||
      normalized === 'place' ||
      normalized === 'team' ||
      normalized === 'organization' ||
      normalized === 'project' ||
      normalized === 'activity' ||
      normalized === 'emotion' ||
      normalized === 'idea' ||
      normalized === 'problem' ||
      normalized === 'finance' ||
      normalized === 'object' ||
      normalized === 'vehicle' ||
      normalized === 'brand' ||
      normalized === 'product_model' ||
      normalized === 'year' ||
      normalized === 'date' ||
      normalized === 'time' ||
      normalized === 'amount' ||
      normalized === 'not_entity' ||
      normalized === 'not_person'
    ) {
      return normalized;
    }

    return 'person';
  }
}
