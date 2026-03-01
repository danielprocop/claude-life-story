import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Api, NodeSearchResponse } from '../services/api';

@Component({
  selector: 'app-graph',
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './graph.html',
  styleUrl: './graph.scss',
})
export class Graph implements OnInit {
  private readonly api = inject(Api);

  readonly query = signal('');
  readonly loading = signal(true);
  readonly response = signal<NodeSearchResponse | null>(null);
  readonly selectedKind = signal('all');

  ngOnInit(): void {
    this.loadNodes();
  }

  submit(): void {
    this.loadNodes();
  }

  loadNodes(): void {
    this.loading.set(true);
    this.api.getNodes(this.query(), 60).subscribe({
      next: (res) => {
        this.response.set(res);
        if (this.selectedKind() !== 'all' && !res.kindCounts.some((x) => x.kind === this.selectedKind())) {
          this.selectedKind.set('all');
        }
        this.loading.set(false);
      },
      error: () => {
        this.response.set({ query: this.query(), items: [], totalCount: 0, kindCounts: [] });
        this.selectedKind.set('all');
        this.loading.set(false);
      },
    });
  }

  setKindFilter(kind: string): void {
    this.selectedKind.set(kind);
  }

  filteredItems() {
    const items = this.response()?.items ?? [];
    const activeKind = this.selectedKind();
    if (activeKind === 'all') {
      return items;
    }

    return items.filter((item) => item.kind === activeKind);
  }

  kindFilters() {
    const response = this.response();
    if (!response) {
      return [];
    }

    const items = response.kindCounts.map((item) => ({
      kind: item.kind,
      count: item.count,
    }));

    return [{ kind: 'all', count: response.totalCount }, ...items];
  }

  colorForKind(kind: string): string {
    const palette: Record<string, string> = {
      person: '#E8F2FF',
      place: '#E9FFF1',
      team: '#EAF1FF',
      organization: '#EEF2F4',
      goal: '#FFF4E8',
      project: '#F3EEFF',
      activity: '#FFFBE8',
      idea: '#E8FBFF',
      emotion: '#FFEAF0',
      problem: '#FFEFEF',
      finance: '#EEFFF8',
      object: '#F3F5F7',
      vehicle: '#EDF0F4',
      brand: '#F7EEFF',
      product_model: '#F0EEFF',
      event: '#F2F4F8'
    };

    return palette[kind.toLowerCase()] ?? '#F4F7FA';
  }
}
