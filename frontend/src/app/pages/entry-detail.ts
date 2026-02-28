import { CommonModule, DatePipe } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Api, EntryResponse, RelatedEntryResponse } from '../services/api';

@Component({
  selector: 'app-entry-detail',
  imports: [CommonModule, DatePipe, RouterLink],
  templateUrl: './entry-detail.html',
  styleUrl: './entry-detail.scss',
})
export class EntryDetailPage implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly api = inject(Api);

  readonly entry = signal<EntryResponse | null>(null);
  readonly relatedEntries = signal<RelatedEntryResponse[]>([]);
  readonly loading = signal(true);
  readonly error = signal('');

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
}
