import { CommonModule, DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Api, SearchResponse } from '../services/api';

@Component({
  selector: 'app-search-page',
  imports: [CommonModule, FormsModule, RouterLink, DatePipe],
  templateUrl: './search.html',
  styleUrl: './search.scss',
})
export class SearchPage {
  private readonly api = inject(Api);

  readonly query = signal('');
  readonly loading = signal(false);
  readonly hasSearched = signal(false);
  readonly results = signal<SearchResponse | null>(null);

  submit(): void {
    const value = this.query().trim();
    if (!value) {
      this.hasSearched.set(false);
      this.results.set(null);
      return;
    }

    this.loading.set(true);
    this.hasSearched.set(true);

    this.api.search(value).subscribe({
      next: (response) => {
        this.results.set(response);
        this.loading.set(false);
      },
      error: () => {
        this.results.set({
          query: value,
          entries: [],
          concepts: [],
          goalItems: [],
          entities: [],
        });
        this.loading.set(false);
      },
    });
  }

  totalResults(): number {
    const response = this.results();
    if (!response) {
      return 0;
    }

    return response.entries.length + response.concepts.length + response.goalItems.length + response.entities.length;
  }
}
