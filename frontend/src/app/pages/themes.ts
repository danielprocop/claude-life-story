import { Component, OnInit, signal } from '@angular/core';
import { Api, ConceptResponse } from '../services/api';

interface Theme {
  type: string;
  label: string;
  concepts: ConceptResponse[];
}

@Component({
  selector: 'app-themes',
  imports: [],
  templateUrl: './themes.html',
  styleUrl: './themes.scss',
})
export class Themes implements OnInit {
  themes = signal<Theme[]>([]);
  loading = signal(true);

  private typeLabels: Record<string, string> = {
    person: 'Persone',
    place: 'Luoghi',
    team: 'Team',
    organization: 'Organizzazioni',
    desire: 'Desideri',
    goal: 'Obiettivi',
    activity: 'Attivita',
    emotion: 'Emozioni',
    idea: 'Idee',
    problem: 'Problemi',
    finance: 'Finanza',
    object: 'Oggetti',
    vehicle: 'Veicoli',
    brand: 'Marche',
    product_model: 'Modelli',
    year: 'Anni',
    date: 'Date',
    time: 'Orari',
    amount: 'Importi',
  };

  constructor(private api: Api) {}

  ngOnInit() {
    this.api.getConcepts().subscribe({
      next: (concepts) => {
        const grouped = new Map<string, ConceptResponse[]>();
        for (const c of concepts) {
          const list = grouped.get(c.type) || [];
          list.push(c);
          grouped.set(c.type, list);
        }

        this.themes.set(
          Array.from(grouped.entries()).map(([type, items]) => ({
            type,
            label: this.typeLabels[type] || type,
            concepts: items.sort((a, b) => b.entryCount - a.entryCount)
          }))
        );
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }
}
