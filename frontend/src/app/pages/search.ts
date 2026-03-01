import { CommonModule, DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { Api, EntitySearchHit, SearchResponse } from '../services/api';

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
  readonly selectedEntityKind = signal('all');
  readonly entityKindCounts = signal<{ kind: string; count: number }[]>([]);

  submit(): void {
    const value = this.query().trim();
    if (!value) {
      this.hasSearched.set(false);
      this.results.set(null);
      this.selectedEntityKind.set('all');
      this.entityKindCounts.set([]);
      return;
    }

    this.loading.set(true);
    this.hasSearched.set(true);

    forkJoin({
      search: this.api.search(value),
      nodes: this.api.getNodes(value, 30),
    }).subscribe({
      next: ({ search, nodes }) => {
        this.results.set({
          ...search,
          entities: nodes.items.map((item) => ({
            id: item.id,
            kind: item.kind,
            canonicalName: item.canonicalName,
            anchorKey: item.anchorKey,
            aliases: item.aliases,
            evidenceCount: item.evidenceCount,
            updatedAt: item.updatedAt,
            resolutionState: item.resolutionState,
          })),
        });
        this.entityKindCounts.set(nodes.kindCounts.map((item) => ({ kind: item.kind, count: item.count })));
        if (this.selectedEntityKind() !== 'all' && !nodes.kindCounts.some((item) => item.kind === this.selectedEntityKind())) {
          this.selectedEntityKind.set('all');
        }
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
        this.entityKindCounts.set([]);
        this.selectedEntityKind.set('all');
        this.loading.set(false);
      },
    });
  }

  setEntityKind(kind: string): void {
    this.selectedEntityKind.set(kind);
  }

  entityFilters(): { kind: string; count: number }[] {
    const results = this.results();
    if (!results) {
      return [];
    }

    const kinds = this.entityKindCounts();
    if (!kinds.length) {
      const fallback = new Map<string, number>();
      for (const entity of results.entities) {
        fallback.set(entity.kind, (fallback.get(entity.kind) ?? 0) + 1);
      }
      const fallbackItems = [...fallback.entries()].map(([kind, count]) => ({ kind, count }));
      return [{ kind: 'all', count: results.entities.length }, ...fallbackItems];
    }

    return [{ kind: 'all', count: results.entities.length }, ...kinds];
  }

  filteredEntities(): EntitySearchHit[] {
    const results = this.results();
    if (!results) {
      return [];
    }

    const activeKind = this.selectedEntityKind();
    if (activeKind === 'all') {
      return results.entities;
    }

    return results.entities.filter((entity) => entity.kind === activeKind);
  }

  totalResults(): number {
    const response = this.results();
    if (!response) {
      return 0;
    }

    return response.entries.length + response.concepts.length + response.goalItems.length + this.filteredEntities().length;
  }
}
