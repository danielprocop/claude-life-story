import { Component, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Api } from '../services/api';

@Component({
  selector: 'app-write',
  imports: [FormsModule],
  templateUrl: './write.html',
  styleUrl: './write.scss',
})
export class Write {
  content = signal('');
  saving = signal(false);
  saved = signal(false);

  constructor(private api: Api) {}

  save() {
    const text = this.content().trim();
    if (!text || this.saving()) return;

    this.saving.set(true);
    this.saved.set(false);

    this.api.createEntry(text).subscribe({
      next: () => {
        this.saving.set(false);
        this.saved.set(true);
        this.content.set('');
        setTimeout(() => this.saved.set(false), 3000);
      },
      error: (err) => {
        this.saving.set(false);
        console.error('Error saving entry:', err);
      }
    });
  }
}
