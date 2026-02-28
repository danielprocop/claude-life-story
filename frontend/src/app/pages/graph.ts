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
        this.loading.set(false);
      },
      error: () => {
        this.response.set({ query: this.query(), items: [], totalCount: 0 });
        this.loading.set(false);
      },
    });
  }

  groupedByKind(): Record<string, number> {
    const items = this.response()?.items ?? [];
    const map: Record<string, number> = {};
    for (const item of items) {
      map[item.kind] = (map[item.kind] ?? 0) + 1;
    }
    return map;
  }
}
