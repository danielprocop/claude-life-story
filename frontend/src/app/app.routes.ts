import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', redirectTo: 'write', pathMatch: 'full' },
  { path: 'write', loadComponent: () => import('./pages/write').then(m => m.Write) },
  { path: 'timeline', loadComponent: () => import('./pages/timeline').then(m => m.Timeline) },
  { path: 'graph', loadComponent: () => import('./pages/graph').then(m => m.Graph) },
  { path: 'goals', loadComponent: () => import('./pages/goals').then(m => m.Goals) },
  { path: 'insights', loadComponent: () => import('./pages/insights').then(m => m.Insights) },
  { path: 'themes', loadComponent: () => import('./pages/themes').then(m => m.Themes) },
];
