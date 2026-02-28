import { CommonModule, DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Api, NodeViewResponse } from '../services/api';

@Component({
  selector: 'app-node-page',
  imports: [CommonModule, DatePipe, RouterLink],
  templateUrl: './node.html',
  styleUrl: './node.scss',
})
export class NodePage implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly api = inject(Api);

  readonly node = signal<NodeViewResponse | null>(null);
  readonly loading = signal(true);
  readonly error = signal('');

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.error.set('Nodo non trovato.');
      this.loading.set(false);
      return;
    }

    this.api.getNode(id).subscribe({
      next: (result) => {
        this.node.set(result);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Impossibile caricare il nodo richiesto.');
        this.loading.set(false);
      },
    });
  }
}
