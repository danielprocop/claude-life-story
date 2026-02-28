import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { Api, DashboardResponse, PersonalModelResponse } from '../services/api';

@Component({
  selector: 'app-dashboard',
  imports: [CommonModule, RouterLink],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.scss',
})
export class Dashboard implements OnInit {
  data = signal<DashboardResponse | null>(null);
  profile = signal<PersonalModelResponse | null>(null);
  loading = signal(true);
  profileLoading = signal(true);

  constructor(private api: Api) {}

  ngOnInit() {
    this.api.getDashboard().subscribe({
      next: (res) => {
        this.data.set(res);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });

    this.api.getProfile().subscribe({
      next: (profile) => {
        this.profile.set(profile);
        this.profileLoading.set(false);
      },
      error: () => this.profileLoading.set(false),
    });
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
