import { Component, OnInit, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { Api, GoalResponse } from '../services/api';

@Component({
  selector: 'app-goals',
  imports: [DatePipe],
  templateUrl: './goals.html',
  styleUrl: './goals.scss',
})
export class Goals implements OnInit {
  goals = signal<GoalResponse[]>([]);
  loading = signal(true);

  constructor(private api: Api) {}

  ngOnInit() {
    this.api.getGoals().subscribe({
      next: (res) => {
        this.goals.set(res);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }
}
