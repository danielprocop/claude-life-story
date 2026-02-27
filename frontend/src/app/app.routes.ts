import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
  { path: 'dashboard', loadComponent: () => import('./pages/dashboard').then(m => m.Dashboard) },
  { path: 'write', loadComponent: () => import('./pages/write').then(m => m.Write) },
  { path: 'chat', loadComponent: () => import('./pages/chat').then(m => m.Chat) },
  { path: 'timeline', loadComponent: () => import('./pages/timeline').then(m => m.Timeline) },
  { path: 'graph', loadComponent: () => import('./pages/graph').then(m => m.Graph) },
  { path: 'goals', loadComponent: () => import('./pages/goals').then(m => m.Goals) },
  { path: 'energy', loadComponent: () => import('./pages/energy').then(m => m.Energy) },
  { path: 'review', loadComponent: () => import('./pages/review').then(m => m.Review) },
  { path: 'insights', loadComponent: () => import('./pages/insights').then(m => m.Insights) },
  { path: 'themes', loadComponent: () => import('./pages/themes').then(m => m.Themes) },
];
