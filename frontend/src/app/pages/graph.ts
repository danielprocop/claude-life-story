import { Component, OnInit, OnDestroy, signal, ElementRef, ViewChild, AfterViewInit } from '@angular/core';
import { Api, GraphResponse, GraphNode } from '../services/api';
import cytoscape from 'cytoscape';

const TYPE_COLORS: Record<string, string> = {
  person: '#e74c3c',
  place: '#3498db',
  desire: '#f39c12',
  goal: '#2ecc71',
  activity: '#9b59b6',
  emotion: '#e91e63',
};

@Component({
  selector: 'app-graph',
  imports: [],
  templateUrl: './graph.html',
  styleUrl: './graph.scss',
})
export class Graph implements OnInit, AfterViewInit, OnDestroy {
  @ViewChild('cyContainer', { static: false }) cyContainer!: ElementRef;

  graph = signal<GraphResponse | null>(null);
  loading = signal(true);
  selectedNode = signal<GraphNode | null>(null);

  private cy: cytoscape.Core | null = null;

  constructor(private api: Api) {}

  ngOnInit() {
    this.api.getGraph().subscribe({
      next: (res) => {
        this.graph.set(res);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  ngAfterViewInit() {
    // Wait for data to load, then render
    const interval = setInterval(() => {
      if (!this.loading() && this.graph() && this.cyContainer) {
        clearInterval(interval);
        this.renderGraph();
      }
    }, 100);

    // Safety timeout
    setTimeout(() => clearInterval(interval), 10000);
  }

  ngOnDestroy() {
    this.cy?.destroy();
  }

  private renderGraph() {
    const data = this.graph();
    if (!data || data.nodes.length === 0) return;

    const nodes = data.nodes.map(n => ({
      data: {
        id: n.id,
        label: n.label,
        type: n.type,
        weight: n.weight,
        color: TYPE_COLORS[n.type] || '#888',
      }
    }));

    const edges = data.edges.map((e, i) => ({
      data: {
        id: `e${i}`,
        source: e.sourceId,
        target: e.targetId,
        strength: e.strength,
      }
    }));

    this.cy = cytoscape({
      container: this.cyContainer.nativeElement,
      elements: [...nodes, ...edges],
      style: [
        {
          selector: 'node',
          style: {
            'label': 'data(label)',
            'background-color': 'data(color)',
            'color': '#1a1a2e',
            'text-valign': 'bottom',
            'text-halign': 'center',
            'font-size': '11px',
            'font-weight': 600,
            'text-margin-y': 8,
            'width': 'mapData(weight, 0, 10, 30, 70)',
            'height': 'mapData(weight, 0, 10, 30, 70)',
            'border-width': 2,
            'border-color': '#fff',
            'text-max-width': '100px',
            'text-wrap': 'ellipsis',
          } as any
        },
        {
          selector: 'node:selected',
          style: {
            'border-width': 4,
            'border-color': '#6c63ff',
            'overlay-opacity': 0.1,
            'overlay-color': '#6c63ff',
          }
        },
        {
          selector: 'edge',
          style: {
            'width': 'mapData(strength, 0, 1, 1, 6)',
            'line-color': '#d0d0d0',
            'curve-style': 'bezier',
            'opacity': 0.6,
          }
        },
        {
          selector: 'edge:selected',
          style: {
            'line-color': '#6c63ff',
            'opacity': 1,
          }
        }
      ],
      layout: {
        name: 'cose',
        animate: true,
        animationDuration: 800,
        nodeRepulsion: () => 8000,
        idealEdgeLength: () => 120,
        gravity: 0.3,
        padding: 40,
      } as any,
      minZoom: 0.3,
      maxZoom: 3,
    });

    // Click handler
    this.cy.on('tap', 'node', (evt) => {
      const nodeData = evt.target.data();
      this.selectedNode.set({
        id: nodeData.id,
        label: nodeData.label,
        type: nodeData.type,
        weight: nodeData.weight,
      });
    });

    // Click background to deselect
    this.cy.on('tap', (evt) => {
      if (evt.target === this.cy) {
        this.selectedNode.set(null);
      }
    });
  }

  getTypeColor(type: string): string {
    return TYPE_COLORS[type] || '#888';
  }

  getTypeLabel(type: string): string {
    const labels: Record<string, string> = {
      person: 'Persona',
      place: 'Luogo',
      desire: 'Desiderio',
      goal: 'Obiettivo',
      activity: 'Attivita',
      emotion: 'Emozione',
    };
    return labels[type] || type;
  }

  closeDetail() {
    this.selectedNode.set(null);
    this.cy?.elements().unselect();
  }
}
