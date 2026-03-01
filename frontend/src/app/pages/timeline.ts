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
  private readonly edgeLoadThresholdPx = 260;

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
  readonly loadingMorePrevious = signal(false);
  readonly loadingMoreNext = signal(false);
  readonly activeRangeLabel = signal('');
  readonly activeVisibleEntries = signal(0);
  readonly availableViews: TimelineViewMode[] = ['day', 'week', 'month', 'year'];
  readonly totalEntriesLoaded = computed(() =>
    (this.timeline()?.buckets ?? []).reduce((acc, bucket) => acc + bucket.entryCount, 0)
  );
  readonly windowLabel = computed(() => {
    if (this.activeRangeLabel()) {
      return this.activeRangeLabel();
    }

    const buckets = this.timeline()?.buckets ?? [];
    if (buckets.length === 0) {
      return '';
    }

    const first = buckets[0].label;
    const last = buckets[buckets.length - 1].label;
    return first === last ? first : `${first} → ${last}`;
  });
  readonly windowEntriesCount = computed(() =>
    this.activeVisibleEntries() > 0 ? this.activeVisibleEntries() : this.totalEntriesLoaded()
  );

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
    this.timeline.set(null);
    this.activeRangeLabel.set('');
    this.activeVisibleEntries.set(0);
    this.loadTimeline();
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
    const timeline = this.timeline();
    if (!element) {
      return;
    }

    if (direction === 'left' && element.scrollLeft < 12 && timeline?.hasPrevious) {
      this.loadMore('previous');
      return;
    }

    const rightRemaining = element.scrollWidth - element.clientWidth - element.scrollLeft;
    if (direction === 'right' && rightRemaining < 12 && timeline?.hasNext) {
      this.loadMore('next');
      return;
    }

    const delta = Math.max(260, Math.floor(element.clientWidth * 0.72));
    element.scrollBy({ left: direction === 'left' ? -delta : delta, behavior: 'smooth' });
  }

  onTimelineScrolled(): void {
    this.updateScrollButtons();
    this.updateVisibleWindowMeta();
    this.maybeLoadMoreFromScroll();
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
          this.loadingMorePrevious.set(false);
          this.loadingMoreNext.set(false);

          requestAnimationFrame(() => {
            this.centerCurrentBucket();
            this.updateScrollButtons();
            this.updateVisibleWindowMeta();
            this.maybeLoadMoreFromScroll();
          });
        },
        error: () => {
          this.error.set('Impossibile caricare la timeline in questo momento.');
          this.timeline.set(null);
          this.loading.set(false);
          this.loadingMorePrevious.set(false);
          this.loadingMoreNext.set(false);
          this.updateScrollButtons();
        },
      });
  }

  private loadMore(direction: 'previous' | 'next'): void {
    const timeline = this.timeline();
    const element = this.timelineScrollEl?.nativeElement;
    if (!timeline || !element || this.loading()) {
      return;
    }

    if (direction === 'previous') {
      if (this.loadingMorePrevious() || !timeline.hasPrevious) {
        return;
      }
      this.loadingMorePrevious.set(true);
    }
    else
    {
      if (this.loadingMoreNext() || !timeline.hasNext) {
        return;
      }
      this.loadingMoreNext.set(true);
    }

    const cursor = direction === 'previous'
      ? timeline.previousCursorUtc ?? this.deriveCursor(timeline, 'previous')
      : timeline.nextCursorUtc ?? this.deriveCursor(timeline, 'next');

    if (!cursor) {
      if (direction === 'previous') this.loadingMorePrevious.set(false);
      else this.loadingMoreNext.set(false);
      return;
    }

    const beforeScrollWidth = element.scrollWidth;
    const beforeScrollLeft = element.scrollLeft;
    const config = this.viewConfig[this.view()];

    this.api
      .getEntriesTimeline(
        this.view(),
        cursor,
        config.bucketCount,
        config.entriesPerBucket,
        new Date().getTimezoneOffset()
      )
      .subscribe({
        next: (res) => {
          this.timeline.update((current) => this.mergeTimeline(current, res, direction));
          requestAnimationFrame(() => {
            if (direction === 'previous') {
              const delta = element.scrollWidth - beforeScrollWidth;
              element.scrollLeft = beforeScrollLeft + Math.max(0, delta);
            }

            this.updateScrollButtons();
            this.updateVisibleWindowMeta();
            this.maybeLoadMoreFromScroll();
          });
        },
        error: () => {
          if (direction === 'previous') this.loadingMorePrevious.set(false);
          else this.loadingMoreNext.set(false);
        },
        complete: () => {
          if (direction === 'previous') this.loadingMorePrevious.set(false);
          else this.loadingMoreNext.set(false);
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
    const timeline = this.timeline();
    if (!element) {
      this.scrollLeftVisible.set(false);
      this.scrollRightVisible.set(false);
      return;
    }

    const left = element.scrollLeft;
    const rightRemaining = element.scrollWidth - element.clientWidth - left;
    this.scrollLeftVisible.set(left > 8 || !!timeline?.hasPrevious);
    this.scrollRightVisible.set(rightRemaining > 8 || !!timeline?.hasNext);
  }

  private updateVisibleWindowMeta(): void {
    const element = this.timelineScrollEl?.nativeElement;
    const timeline = this.timeline();
    if (!element || !timeline || timeline.buckets.length === 0) {
      this.activeRangeLabel.set('');
      this.activeVisibleEntries.set(0);
      return;
    }

    const bucketEls = Array.from(element.querySelectorAll<HTMLElement>('.timeline-bucket'));
    if (bucketEls.length === 0) {
      this.activeRangeLabel.set('');
      this.activeVisibleEntries.set(0);
      return;
    }

    const viewportLeft = element.scrollLeft;
    const viewportRight = viewportLeft + element.clientWidth;
    let firstVisible = -1;
    let lastVisible = -1;

    for (let index = 0; index < bucketEls.length; index++) {
      const bucketEl = bucketEls[index];
      const start = bucketEl.offsetLeft;
      const end = start + bucketEl.offsetWidth;
      if (end >= viewportLeft + 2 && start <= viewportRight - 2) {
        if (firstVisible < 0) firstVisible = index;
        lastVisible = index;
      }
    }

    if (firstVisible < 0 || lastVisible < 0) {
      const center = viewportLeft + element.clientWidth / 2;
      let nearestIdx = 0;
      let nearestDistance = Number.MAX_VALUE;
      for (let index = 0; index < bucketEls.length; index++) {
        const bucketEl = bucketEls[index];
        const bucketCenter = bucketEl.offsetLeft + bucketEl.offsetWidth / 2;
        const distance = Math.abs(bucketCenter - center);
        if (distance < nearestDistance) {
          nearestDistance = distance;
          nearestIdx = index;
        }
      }

      firstVisible = nearestIdx;
      lastVisible = nearestIdx;
    }

    const safeFirst = Math.max(0, Math.min(firstVisible, timeline.buckets.length - 1));
    const safeLast = Math.max(safeFirst, Math.min(lastVisible, timeline.buckets.length - 1));
    const firstLabel = timeline.buckets[safeFirst].label;
    const lastLabel = timeline.buckets[safeLast].label;
    this.activeRangeLabel.set(firstLabel === lastLabel ? firstLabel : `${firstLabel} → ${lastLabel}`);
    this.activeVisibleEntries.set(
      timeline.buckets
        .slice(safeFirst, safeLast + 1)
        .reduce((acc, bucket) => acc + bucket.entryCount, 0)
    );
  }

  private maybeLoadMoreFromScroll(): void {
    const element = this.timelineScrollEl?.nativeElement;
    const timeline = this.timeline();
    if (!element || !timeline || this.loading()) {
      return;
    }

    const leftGap = element.scrollLeft;
    const rightGap = element.scrollWidth - element.clientWidth - element.scrollLeft;

    if (leftGap < this.edgeLoadThresholdPx && timeline.hasPrevious && !this.loadingMorePrevious()) {
      this.loadMore('previous');
      return;
    }

    if (rightGap < this.edgeLoadThresholdPx && timeline.hasNext && !this.loadingMoreNext()) {
      this.loadMore('next');
    }
  }

  private mergeTimeline(
    current: EntriesTimelineResponse | null,
    incoming: EntriesTimelineResponse,
    direction: 'previous' | 'next'
  ): EntriesTimelineResponse {
    if (!current) {
      return incoming;
    }

    const existing = new Set(current.buckets.map((bucket) => bucket.bucketKey));
    const uniqueIncoming = incoming.buckets.filter((bucket) => !existing.has(bucket.bucketKey));
    const mergedBuckets =
      direction === 'previous'
        ? [...uniqueIncoming, ...current.buckets]
        : [...current.buckets, ...uniqueIncoming];

    const firstBucket = mergedBuckets[0];
    const lastBucket = mergedBuckets[mergedBuckets.length - 1];

    return {
      ...current,
      rangeStartUtc: firstBucket?.startUtc ?? current.rangeStartUtc,
      rangeEndUtc: lastBucket?.endUtc ?? current.rangeEndUtc,
      buckets: mergedBuckets,
      hasPrevious: direction === 'previous' ? incoming.hasPrevious : current.hasPrevious,
      hasNext: direction === 'next' ? incoming.hasNext : current.hasNext,
      previousCursorUtc: direction === 'previous' ? incoming.previousCursorUtc : current.previousCursorUtc,
      nextCursorUtc: direction === 'next' ? incoming.nextCursorUtc : current.nextCursorUtc,
    };
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
