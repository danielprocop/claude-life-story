import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import {
  Api,
  EntriesTimelineResponse,
  TimelineEntryCardResponse,
  TimelineViewMode,
} from '../services/api';

@Component({
  selector: 'app-timeline',
  imports: [CommonModule, DatePipe, RouterLink],
  templateUrl: './timeline.html',
  styleUrl: './timeline.scss',
})
export class Timeline implements OnInit {
  private readonly router = inject(Router);
  private readonly viewConfig: Record<TimelineViewMode, { bucketCount: number; entriesPerBucket: number }> = {
    day: { bucketCount: 10, entriesPerBucket: 8 },
    week: { bucketCount: 10, entriesPerBucket: 10 },
    month: { bucketCount: 12, entriesPerBucket: 10 },
    year: { bucketCount: 8, entriesPerBucket: 12 },
  };

  readonly view = signal<TimelineViewMode>('day');
  readonly timeline = signal<EntriesTimelineResponse | null>(null);
  loading = signal(true);
  readonly error = signal('');
  notice = signal('');
  readonly availableViews: TimelineViewMode[] = ['day', 'week', 'month', 'year'];
  readonly totalEntriesInWindow = computed(() =>
    (this.timeline()?.buckets ?? []).reduce((acc, bucket) => acc + bucket.entryCount, 0)
  );
  readonly windowLabel = computed(() => {
    const buckets = this.timeline()?.buckets ?? [];
    if (buckets.length === 0) {
      return '';
    }

    const first = buckets[0].label;
    const last = buckets[buckets.length - 1].label;
    return first === last ? first : `${first} → ${last}`;
  });

  constructor(private api: Api) {}

  ngOnInit() {
    const notice = this.router.getCurrentNavigation()?.extras.state?.['notice'] as string | undefined;
    if (notice) {
      this.notice.set(notice);
    }
    this.loadTimeline();
  }

  setView(view: TimelineViewMode): void {
    if (this.view() === view) {
      return;
    }

    this.view.set(view);
    this.loadTimeline();
  }

  loadPrevious(): void {
    const cursor = this.timeline()?.previousCursorUtc;
    if (!cursor || this.loading()) {
      return;
    }
    this.loadTimeline(cursor);
  }

  loadNext(): void {
    const cursor = this.timeline()?.nextCursorUtc;
    if (!cursor || this.loading()) {
      return;
    }
    this.loadTimeline(cursor);
  }

  formatEntryMeta(entry: TimelineEntryCardResponse): string {
    const count = entry.conceptCount;
    if (count <= 0) {
      return 'Nessun concetto estratto';
    }
    return `${count} concetti estratti`;
  }

  private loadTimeline(cursorUtc?: string): void {
    this.loading.set(true);
    this.error.set('');

    const config = this.viewConfig[this.view()];
    this.api
      .getEntriesTimeline(
        this.view(),
        cursorUtc,
        config.bucketCount,
        config.entriesPerBucket,
        new Date().getTimezoneOffset()
      )
      .subscribe({
      next: (res) => {
        this.timeline.set(res);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Impossibile caricare la timeline in questo momento.');
        this.timeline.set(null);
        this.loading.set(false);
      }
    });
  }
}
