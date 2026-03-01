import { CommonModule, DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { forkJoin } from 'rxjs';
import { Api, ConceptResponse, GraphEdge, GraphNode } from '../services/api';

type StoryDirectionTone = 'up' | 'steady' | 'reset';
type StoryPhase = 'origins' | 'growth' | 'direction';

interface StoryNodeBase {
  id: string;
  label: string;
  kind: string;
  entryCount: number;
  weight: number;
  firstSeenMs: number;
  lastSeenMs: number;
  importance: number;
}

interface StoryLane {
  kind: string;
  label: string;
  count: number;
  yPct: number;
}

interface StoryNodeView extends StoryNodeBase {
  xPct: number;
  yPct: number;
  phase: StoryPhase;
  recencyNorm: number;
  edgeInfluence: number;
}

interface StoryEdgePoolItem extends GraphEdge {
  normalizedStrength: number;
}

interface StoryEdgeView {
  key: string;
  sourceId: string;
  targetId: string;
  sourceLabel: string;
  targetLabel: string;
  type: string;
  strength: number;
  normalizedStrength: number;
  x1: number;
  y1: number;
  x2: number;
  y2: number;
  active: boolean;
}

interface StoryDirectionSummary {
  title: string;
  subtitle: string;
  tone: StoryDirectionTone;
}

interface StoryPeriodOption {
  days: number;
  label: string;
}

interface StoryStrengthOption {
  min: number;
  label: string;
}

interface StoryKindOption {
  kind: string;
  label: string;
  count: number;
}

interface StoryNodeConnection {
  key: string;
  otherLabel: string;
  type: string;
  normalizedStrength: number;
}

@Component({
  selector: 'app-story-map',
  imports: [CommonModule, DatePipe],
  templateUrl: './story-map.html',
  styleUrl: './story-map.scss',
})
export class StoryMap implements OnInit {
  private readonly api = inject(Api);
  private readonly preferredKindOrder = [
    'goal',
    'project',
    'person',
    'team',
    'organization',
    'place',
    'activity',
    'event',
    'emotion',
    'problem',
    'object',
    'vehicle',
    'brand',
    'product_model',
    'idea',
    'finance',
  ];

  readonly periodOptions: StoryPeriodOption[] = [
    { days: 30, label: '30g' },
    { days: 90, label: '90g' },
    { days: 180, label: '180g' },
    { days: 365, label: '1 anno' },
  ];

  readonly strengthOptions: StoryStrengthOption[] = [
    { min: 0.15, label: 'Deboli+' },
    { min: 0.35, label: 'Medie+' },
    { min: 0.55, label: 'Forti+' },
    { min: 0.75, label: 'Solo forti' },
  ];

  readonly loading = signal(true);
  readonly error = signal('');
  private readonly concepts = signal<ConceptResponse[]>([]);
  private readonly graphNodes = signal<GraphNode[]>([]);
  private readonly graphEdges = signal<GraphEdge[]>([]);
  private readonly referenceNowMs = signal(Date.now());

  readonly selectedPeriodDays = signal(90);
  readonly minStrength = signal(0.35);
  readonly selectedKind = signal('all');
  readonly selectedNodeId = signal<string | null>(null);
  readonly selectedEdgeKey = signal<string | null>(null);

  private readonly periodStartMs = computed(() =>
    this.referenceNowMs() - this.daysToMs(this.selectedPeriodDays())
  );

  private readonly periodSpanMs = computed(() => this.daysToMs(this.selectedPeriodDays()));

  private readonly allNodes = computed<StoryNodeBase[]>(() => {
    const nowMs = this.referenceNowMs();
    const fallbackFirstMs = nowMs - this.daysToMs(365);
    const conceptById = new Map(this.concepts().map((concept) => [concept.id, concept]));
    const graphNodeById = new Map(this.graphNodes().map((node) => [node.id, node]));
    const allIds = new Set<string>([
      ...this.graphNodes().map((node) => node.id),
      ...this.concepts().map((concept) => concept.id),
    ]);

    const nodes: StoryNodeBase[] = [];
    for (const id of allIds) {
      const concept = conceptById.get(id);
      const graphNode = graphNodeById.get(id);
      const label = (concept?.label ?? graphNode?.label ?? '').trim();
      if (!label) {
        continue;
      }

      const firstSeenMs = this.toTimestamp(concept?.firstSeenAt, fallbackFirstMs);
      const rawLastSeenMs = this.toTimestamp(concept?.lastSeenAt, firstSeenMs);
      const lastSeenMs = Math.max(firstSeenMs, Math.min(rawLastSeenMs, nowMs));
      const entryCount = Math.max(1, concept?.entryCount ?? Math.round(graphNode?.weight ?? 1));
      const weight = Math.max(1, graphNode?.weight ?? entryCount);
      const kind = this.normalizeKind(concept?.type ?? graphNode?.type ?? 'other');
      const importance = Math.log10(entryCount + 1) * 1.9 + Math.log10(weight + 1) * 1.3;

      nodes.push({
        id,
        label,
        kind,
        entryCount,
        weight,
        firstSeenMs,
        lastSeenMs,
        importance,
      });
    }

    return nodes;
  });

  private readonly periodNodes = computed(() => {
    const periodStart = this.periodStartMs();
    return this.allNodes().filter((node) => node.lastSeenMs >= periodStart || node.firstSeenMs >= periodStart);
  });

  readonly kindOptions = computed<StoryKindOption[]>(() => {
    const counts = new Map<string, number>();
    for (const node of this.periodNodes()) {
      counts.set(node.kind, (counts.get(node.kind) ?? 0) + 1);
    }

    const ordered = Array.from(counts.entries())
      .map(([kind, count]) => ({ kind, count }))
      .sort((a, b) => b.count - a.count || a.kind.localeCompare(b.kind))
      .map((item) => ({
        kind: item.kind,
        label: this.kindLabel(item.kind),
        count: item.count,
      }));

    return [{ kind: 'all', label: 'Tutte', count: this.periodNodes().length }, ...ordered];
  });

  private readonly kindFilteredNodes = computed(() => {
    const selectedKind = this.selectedKind();
    const nodes = this.periodNodes();
    if (selectedKind === 'all') {
      return nodes;
    }

    const filtered = nodes.filter((node) => node.kind === selectedKind);
    return filtered.length ? filtered : nodes;
  });

  private readonly maxEdgeStrength = computed(() =>
    Math.max(1, ...this.graphEdges().map((edge) => Math.max(0, edge.strength)))
  );

  private readonly edgePool = computed<StoryEdgePoolItem[]>(() => {
    const nodeSet = new Set(this.kindFilteredNodes().map((node) => node.id));
    const minStrength = this.minStrength();
    const maxStrength = this.maxEdgeStrength();

    return this.graphEdges()
      .map((edge) => ({
        ...edge,
        normalizedStrength: maxStrength > 0 ? edge.strength / maxStrength : 0,
      }))
      .filter((edge) => {
        if (edge.sourceId === edge.targetId) {
          return false;
        }

        return (
          edge.normalizedStrength >= minStrength &&
          nodeSet.has(edge.sourceId) &&
          nodeSet.has(edge.targetId)
        );
      });
  });

  private readonly nodeInfluence = computed(() => {
    const influence = new Map<string, number>();
    for (const edge of this.edgePool()) {
      influence.set(edge.sourceId, (influence.get(edge.sourceId) ?? 0) + edge.normalizedStrength);
      influence.set(edge.targetId, (influence.get(edge.targetId) ?? 0) + edge.normalizedStrength);
    }
    return influence;
  });

  private readonly displayNodes = computed(() => {
    const periodStart = this.periodStartMs();
    const periodSpan = this.periodSpanMs();
    const influence = this.nodeInfluence();

    return [...this.kindFilteredNodes()]
      .sort((nodeA, nodeB) => {
        const recencyA = this.clamp((nodeA.lastSeenMs - periodStart) / periodSpan, 0, 1);
        const recencyB = this.clamp((nodeB.lastSeenMs - periodStart) / periodSpan, 0, 1);
        const scoreA =
          (influence.get(nodeA.id) ?? 0) * 4.2 +
          nodeA.importance * 1.8 +
          recencyA * 2.6 +
          Math.log2(nodeA.entryCount + 1);
        const scoreB =
          (influence.get(nodeB.id) ?? 0) * 4.2 +
          nodeB.importance * 1.8 +
          recencyB * 2.6 +
          Math.log2(nodeB.entryCount + 1);

        return scoreB - scoreA;
      })
      .slice(0, 26);
  });

  readonly laneDefinitions = computed<StoryLane[]>(() => {
    const counts = new Map<string, number>();
    for (const node of this.displayNodes()) {
      counts.set(node.kind, (counts.get(node.kind) ?? 0) + 1);
    }

    const preferredIndex = new Map(this.preferredKindOrder.map((kind, index) => [kind, index]));
    const orderedKinds = Array.from(counts.keys()).sort((kindA, kindB) => {
      const indexA = preferredIndex.get(kindA) ?? 999;
      const indexB = preferredIndex.get(kindB) ?? 999;
      if (indexA !== indexB) {
        return indexA - indexB;
      }

      return (counts.get(kindB) ?? 0) - (counts.get(kindA) ?? 0);
    });

    if (orderedKinds.length === 0) {
      return [];
    }

    const laneCount = orderedKinds.length;
    return orderedKinds.map((kind, index) => ({
      kind,
      label: this.kindLabel(kind),
      count: counts.get(kind) ?? 0,
      yPct: laneCount === 1 ? 50 : 16 + (index / (laneCount - 1)) * 70,
    }));
  });

  readonly positionedNodes = computed<StoryNodeView[]>(() => {
    const periodStart = this.periodStartMs();
    const periodSpan = this.periodSpanMs();
    const influence = this.nodeInfluence();
    const lanes = this.laneDefinitions();
    const nodesByLane = new Map<string, StoryNodeBase[]>();

    for (const node of this.displayNodes()) {
      if (!nodesByLane.has(node.kind)) {
        nodesByLane.set(node.kind, []);
      }
      nodesByLane.get(node.kind)!.push(node);
    }

    const positioned: StoryNodeView[] = [];
    for (const lane of lanes) {
      const laneNodes = (nodesByLane.get(lane.kind) ?? []).sort(
        (nodeA, nodeB) => nodeA.lastSeenMs - nodeB.lastSeenMs
      );
      const laneSize = laneNodes.length;

      laneNodes.forEach((node, index) => {
        const recencyNorm = this.clamp((node.lastSeenMs - periodStart) / periodSpan, 0, 1);
        const xBase = 8 + recencyNorm * 84;
        const xJitter = ((index % 3) - 1) * 1.2;
        const spread = laneSize > 1 ? (index / (laneSize - 1) - 0.5) : 0;
        const yOffset = spread * 11 + ((index % 2 === 0 ? 1 : -1) * 1.7);
        const xPct = this.clamp(xBase + xJitter, 6, 94);
        const yPct = this.clamp(lane.yPct + yOffset, 9, 92);

        let phase: StoryPhase = 'direction';
        if (xPct < 38) {
          phase = 'origins';
        } else if (xPct < 68) {
          phase = 'growth';
        }

        positioned.push({
          ...node,
          xPct,
          yPct,
          phase,
          recencyNorm,
          edgeInfluence: influence.get(node.id) ?? 0,
        });
      });
    }

    return positioned.sort((nodeA, nodeB) => nodeA.xPct - nodeB.xPct);
  });

  private readonly nodeById = computed(() => new Map(this.positionedNodes().map((node) => [node.id, node])));

  readonly positionedEdges = computed<StoryEdgeView[]>(() => {
    const nodeById = this.nodeById();
    const selectedNodeId = this.selectedNodeId();
    const selectedEdgeKey = this.selectedEdgeKey();

    return this.edgePool()
      .filter((edge) => nodeById.has(edge.sourceId) && nodeById.has(edge.targetId))
      .sort((edgeA, edgeB) => edgeB.normalizedStrength - edgeA.normalizedStrength)
      .slice(0, 72)
      .map((edge) => {
        const source = nodeById.get(edge.sourceId)!;
        const target = nodeById.get(edge.targetId)!;
        const key = this.edgeKey(edge.sourceId, edge.targetId, edge.type);
        const active =
          selectedEdgeKey === key ||
          (!!selectedNodeId && (selectedNodeId === edge.sourceId || selectedNodeId === edge.targetId));

        return {
          key,
          sourceId: edge.sourceId,
          targetId: edge.targetId,
          sourceLabel: source.label,
          targetLabel: target.label,
          type: edge.type,
          strength: edge.strength,
          normalizedStrength: edge.normalizedStrength,
          x1: source.xPct,
          y1: source.yPct,
          x2: target.xPct,
          y2: target.yPct,
          active,
        };
      });
  });

  readonly selectedNode = computed(() => {
    const nodeId = this.selectedNodeId();
    if (!nodeId) {
      return null;
    }

    return this.nodeById().get(nodeId) ?? null;
  });

  readonly selectedEdge = computed(() => {
    const edgeKey = this.selectedEdgeKey();
    if (!edgeKey) {
      return null;
    }

    return this.positionedEdges().find((edge) => edge.key === edgeKey) ?? null;
  });

  readonly selectedNodeConnections = computed<StoryNodeConnection[]>(() => {
    const selectedNode = this.selectedNode();
    if (!selectedNode) {
      return [];
    }

    return this.positionedEdges()
      .filter((edge) => edge.sourceId === selectedNode.id || edge.targetId === selectedNode.id)
      .map((edge) => ({
        key: edge.key,
        otherLabel: edge.sourceId === selectedNode.id ? edge.targetLabel : edge.sourceLabel,
        type: edge.type,
        normalizedStrength: edge.normalizedStrength,
      }))
      .sort((itemA, itemB) => itemB.normalizedStrength - itemA.normalizedStrength)
      .slice(0, 6);
  });

  readonly topConnections = computed(() =>
    [...this.positionedEdges()].sort((edgeA, edgeB) => edgeB.normalizedStrength - edgeA.normalizedStrength).slice(0, 6)
  );

  readonly dominantKinds = computed(() =>
    [...this.laneDefinitions()].sort((laneA, laneB) => laneB.count - laneA.count).slice(0, 3)
  );

  readonly directionSummary = computed<StoryDirectionSummary>(() => {
    const nodes = this.positionedNodes();
    if (nodes.length === 0) {
      return {
        title: 'Mappa in costruzione',
        subtitle: 'Aggiungi piu entry per vedere la direzione evolutiva.',
        tone: 'steady',
      };
    }

    const avgX = nodes.reduce((acc, node) => acc + node.xPct, 0) / nodes.length;
    const directionCount = nodes.filter((node) => node.phase === 'direction').length;
    const connectedShare =
      nodes.filter((node) => node.edgeInfluence > 0).length / Math.max(1, nodes.length);

    if (avgX >= 62 && directionCount >= Math.ceil(nodes.length * 0.35)) {
      return {
        title: 'Direzione in espansione',
        subtitle: `Molti segnali recenti e connessioni vive (${Math.round(connectedShare * 100)}% nodi collegati).`,
        tone: 'up',
      };
    }

    if (avgX >= 47) {
      return {
        title: 'Direzione in consolidamento',
        subtitle: 'Stai rinforzando linee gia presenti con nuovi legami nel periodo corrente.',
        tone: 'steady',
      };
    }

    return {
      title: 'Direzione in rifocalizzazione',
      subtitle: 'La mappa e piu ancorata ai segnali storici: utile riallineare priorita e obiettivi.',
      tone: 'reset',
    };
  });

  readonly directionShare = computed(() => {
    const nodes = this.positionedNodes();
    if (nodes.length === 0) {
      return 0;
    }

    const inDirection = nodes.filter((node) => node.phase === 'direction').length;
    return Math.round((inDirection / nodes.length) * 100);
  });

  ngOnInit(): void {
    this.loadStoryData();
  }

  reload(): void {
    this.loadStoryData();
  }

  setPeriod(days: number): void {
    if (this.selectedPeriodDays() === days) {
      return;
    }

    this.selectedPeriodDays.set(days);
    this.ensureSelectedKindExists();
    this.clearSelection();
  }

  setStrength(min: number): void {
    if (this.minStrength() === min) {
      return;
    }

    this.minStrength.set(min);
    this.clearSelection();
  }

  setKind(kind: string): void {
    if (this.selectedKind() === kind) {
      return;
    }

    this.selectedKind.set(kind);
    this.clearSelection();
  }

  selectNode(nodeId: string): void {
    if (this.selectedNodeId() === nodeId) {
      this.clearSelection();
      return;
    }

    this.selectedNodeId.set(nodeId);
    this.selectedEdgeKey.set(null);
  }

  selectEdge(edgeKey: string): void {
    if (this.selectedEdgeKey() === edgeKey) {
      this.clearSelection();
      return;
    }

    this.selectedEdgeKey.set(edgeKey);
    this.selectedNodeId.set(null);
  }

  kindLabel(kind: string): string {
    const labels: Record<string, string> = {
      person: 'Persone',
      place: 'Luoghi',
      team: 'Team',
      organization: 'Organizzazioni',
      goal: 'Obiettivi',
      project: 'Progetti',
      activity: 'Attivita',
      idea: 'Idee',
      emotion: 'Emozioni',
      problem: 'Problemi',
      finance: 'Finanze',
      object: 'Oggetti',
      vehicle: 'Veicoli',
      brand: 'Brand',
      product_model: 'Modelli',
      event: 'Eventi',
    };

    const key = kind.toLowerCase();
    if (labels[key]) {
      return labels[key];
    }

    return kind
      .replaceAll('_', ' ')
      .split(' ')
      .filter(Boolean)
      .map((chunk) => chunk[0].toUpperCase() + chunk.slice(1))
      .join(' ');
  }

  phaseLabel(phase: StoryPhase): string {
    if (phase === 'origins') {
      return 'Origini';
    }
    if (phase === 'growth') {
      return 'Evoluzione';
    }
    return 'Direzione';
  }

  edgeTypeLabel(type: string): string {
    const normalized = type.trim().toLowerCase();
    if (normalized === 'cooccurrence') {
      return 'co-occorrenza';
    }
    if (normalized === 'causal') {
      return 'influenza';
    }
    if (normalized === 'support') {
      return 'supporto';
    }
    if (normalized === 'conflict') {
      return 'conflitto';
    }

    return normalized || 'relazione';
  }

  strengthPercent(value: number): number {
    return Math.round(value * 100);
  }

  private loadStoryData(): void {
    this.loading.set(true);
    this.error.set('');

    forkJoin({
      concepts: this.api.getConcepts(),
      graph: this.api.getGraph(),
    }).subscribe({
      next: ({ concepts, graph }) => {
        this.concepts.set(concepts ?? []);
        this.graphNodes.set(graph?.nodes ?? []);
        this.graphEdges.set(graph?.edges ?? []);
        this.referenceNowMs.set(Date.now());
        this.ensureSelectedKindExists();
        this.clearSelection();
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Impossibile costruire la Story Map in questo momento.');
        this.concepts.set([]);
        this.graphNodes.set([]);
        this.graphEdges.set([]);
        this.loading.set(false);
      },
    });
  }

  private ensureSelectedKindExists(): void {
    if (this.selectedKind() === 'all') {
      return;
    }

    const exists = this.kindOptions().some((option) => option.kind === this.selectedKind());
    if (!exists) {
      this.selectedKind.set('all');
    }
  }

  private clearSelection(): void {
    this.selectedNodeId.set(null);
    this.selectedEdgeKey.set(null);
  }

  private edgeKey(sourceId: string, targetId: string, type: string): string {
    return `${sourceId}::${targetId}::${type}`;
  }

  private normalizeKind(kind: string): string {
    return kind.trim().toLowerCase().replaceAll('-', '_').replaceAll(' ', '_');
  }

  private toTimestamp(value: string | undefined, fallbackMs: number): number {
    if (!value) {
      return fallbackMs;
    }

    const parsed = Date.parse(value);
    if (Number.isNaN(parsed)) {
      return fallbackMs;
    }

    return parsed;
  }

  private daysToMs(days: number): number {
    return days * 24 * 60 * 60 * 1000;
  }

  private clamp(value: number, min: number, max: number): number {
    return Math.max(min, Math.min(max, value));
  }
}
