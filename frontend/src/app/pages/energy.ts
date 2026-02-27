import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Api, EnergyTrendResponse } from '../services/api';

@Component({
  selector: 'app-energy',
  imports: [CommonModule],
  templateUrl: './energy.html',
  styleUrl: './energy.scss',
})
export class Energy implements OnInit {
  data = signal<EnergyTrendResponse | null>(null);
  loading = signal(true);
  days = signal(30);

  constructor(private api: Api) {}

  ngOnInit() {
    this.loadData();
  }

  loadData() {
    this.loading.set(true);
    this.api.getEnergyTrend(this.days()).subscribe({
      next: (res) => {
        this.data.set(res);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  setDays(d: number) {
    this.days.set(d);
    this.loadData();
  }

  getEnergyColor(level: number): string {
    if (level >= 7) return '#22c55e';
    if (level >= 4) return '#eab308';
    return '#ef4444';
  }

  getStressColor(level: number): string {
    if (level <= 3) return '#22c55e';
    if (level <= 6) return '#eab308';
    return '#ef4444';
  }
}
