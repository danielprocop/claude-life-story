import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Api, ReviewResponse } from '../services/api';

@Component({
  selector: 'app-review',
  imports: [CommonModule],
  templateUrl: './review.html',
  styleUrl: './review.scss',
})
export class Review {
  review = signal<ReviewResponse | null>(null);
  loading = signal(false);
  selectedPeriod = signal('weekly');

  constructor(private api: Api) {}

  generate() {
    this.loading.set(true);
    this.review.set(null);
    this.api.getReview(this.selectedPeriod()).subscribe({
      next: (res) => {
        this.review.set(res);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  setPeriod(period: string) {
    this.selectedPeriod.set(period);
  }
}
