import { Component, OnInit, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { Api, InsightResponse } from '../services/api';

@Component({
  selector: 'app-insights',
  imports: [DatePipe],
  templateUrl: './insights.html',
  styleUrl: './insights.scss',
})
export class Insights implements OnInit {
  insights = signal<InsightResponse[]>([]);
  loading = signal(true);

  constructor(private api: Api) {}

  ngOnInit() {
    this.api.getInsights().subscribe({
      next: (res) => {
        this.insights.set(res);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }
}
