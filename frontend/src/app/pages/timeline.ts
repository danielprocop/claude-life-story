import { Component, OnInit, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { Api, EntryListResponse } from '../services/api';

@Component({
  selector: 'app-timeline',
  imports: [DatePipe, RouterLink],
  templateUrl: './timeline.html',
  styleUrl: './timeline.scss',
})
export class Timeline implements OnInit {
  entries = signal<EntryListResponse[]>([]);
  loading = signal(true);
  page = signal(1);
  totalCount = signal(0);

  constructor(private api: Api) {}

  ngOnInit() {
    this.loadEntries();
  }

  loadEntries() {
    this.loading.set(true);
    this.api.getEntries(this.page(), 20).subscribe({
      next: (res) => {
        this.entries.set(res.items);
        this.totalCount.set(res.totalCount);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  nextPage() {
    this.page.update(p => p + 1);
    this.loadEntries();
  }

  prevPage() {
    this.page.update(p => Math.max(1, p - 1));
    this.loadEntries();
  }
}
