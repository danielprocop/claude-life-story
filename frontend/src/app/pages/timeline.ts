import {
  AfterViewInit,
  Component,
  ElementRef,
  OnInit,
  ViewChild,
  computed,
  inject,
  signal,
} from '@angular/core';
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
export class Timeline implements OnInit, AfterViewInit {
  private readonly router = inject(Router);
  @ViewChild('timelineScrollEl') private readonly timelineScrollEl?: ElementRef<HTMLElement>;

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
  readonly scrollLeftVisible = signal(false);
  readonly scrollRightVisible = signal(false);
  readonly availableViews: TimelineViewMode[] = ['day', 'week', 'month', 'year'];
  readonly totalEntriesInWindow = computed(() =>
    (this.timeline()?.buckets ?? []).reduce((acc, bucket) => acc + bucket.entryCount, 0)
  );
  readonly canLoadPrevious = computed(() => {
    const timeline = this.timeline();
    if (!timeline || this.loading()) return false;
    return timeline.hasPrevious || timeline.buckets.some((bucket) => bucket.entryCount > 0);
  });
  readonly canLoadNext = computed(() => {
    const timeline = this.timeline();
    if (!timeline || this.loading()) return false;
    return timeline.hasNext;
  });
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

  ngAfterViewInit(): void {
    this.updateScrollButtons();
  }

  setView(view: TimelineViewMode): void {
    if (this.view() === view) {
      return;
    }

    this.view.set(view);
    this.loadTimeline();
  }

  loadPrevious(): void {
    const timeline = this.timeline();
    if (!timeline || this.loading()) {
      return;
    }

    const cursor = timeline.previousCursorUtc ?? this.deriveCursor(timeline, 'previous');
    if (!cursor) {
      return;
    }

    this.loadTimeline(cursor);
  }

  loadNext(): void {
    const timeline = this.timeline();
    const cursor = timeline?.nextCursorUtc;
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

  scrollTimeline(direction: 'left' | 'right'): void {
    const element = this.timelineScrollEl?.nativeElement;
    if (!element) {
      return;
    }

    const delta = Math.max(260, Math.floor(element.clientWidth * 0.72));
    element.scrollBy({ left: direction === 'left' ? -delta : delta, behavior: 'smooth' });
  }

  onTimelineScrolled(): void {
    this.updateScrollButtons();
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

          requestAnimationFrame(() => {
            this.centerCurrentBucket();
            this.updateScrollButtons();
          });
        },
        error: () => {
          this.error.set('Impossibile caricare la timeline in questo momento.');
          this.timeline.set(null);
          this.loading.set(false);
          this.updateScrollButtons();
        },
      });
  }

  private centerCurrentBucket(): void {
    const element = this.timelineScrollEl?.nativeElement;
    const timeline = this.timeline();
    if (!element || !timeline || timeline.buckets.length === 0) {
      return;
    }

    const currentBucket = timeline.buckets.find(
      (bucket) => bucket.startUtc === timeline.currentBucketStartUtc
    );

    const target = currentBucket
      ? (element.querySelector<HTMLElement>(`[data-bucket-key="${currentBucket.bucketKey}"]`) ?? null)
      : null;

    if (!target) {
      return;
    }

    const targetLeft = target.offsetLeft - element.clientWidth / 2 + target.clientWidth / 2;
    const bounded = Math.max(0, Math.min(targetLeft, element.scrollWidth - element.clientWidth));
    element.scrollTo({ left: bounded, behavior: 'smooth' });
  }

  private updateScrollButtons(): void {
    const element = this.timelineScrollEl?.nativeElement;
    if (!element) {
      this.scrollLeftVisible.set(false);
      this.scrollRightVisible.set(false);
      return;
    }

    const left = element.scrollLeft;
    const rightRemaining = element.scrollWidth - element.clientWidth - left;
    this.scrollLeftVisible.set(left > 8);
    this.scrollRightVisible.set(rightRemaining > 8);
  }

  private deriveCursor(timeline: EntriesTimelineResponse, direction: 'previous' | 'next'): string | null {
    const source = direction === 'previous' ? timeline.buckets[0] : timeline.buckets[timeline.buckets.length - 1];
    if (!source) return null;

    const start = new Date(source.startUtc);
    if (Number.isNaN(start.getTime())) return null;

    if (timeline.view === 'day') start.setUTCDate(start.getUTCDate() + (direction === 'previous' ? -1 : 1));
    else if (timeline.view === 'week') start.setUTCDate(start.getUTCDate() + (direction === 'previous' ? -7 : 7));
    else if (timeline.view === 'month') start.setUTCMonth(start.getUTCMonth() + (direction === 'previous' ? -1 : 1));
    else start.setUTCFullYear(start.getUTCFullYear() + (direction === 'previous' ? -1 : 1));

    return start.toISOString();
  }
}
