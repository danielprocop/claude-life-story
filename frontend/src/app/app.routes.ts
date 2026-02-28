import { Routes } from '@angular/router';
import { Layout } from './components/layout';
import { authGuard, guestGuard } from './guards/auth-guard';

export const routes: Routes = [
  {
    path: 'auth',
    canActivate: [guestGuard],
    loadComponent: () => import('./pages/auth').then(m => m.AuthPage),
  },
  {
    path: '',
    component: Layout,
    canActivate: [authGuard],
    children: [
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
      { path: 'dashboard', loadComponent: () => import('./pages/dashboard').then(m => m.Dashboard) },
      { path: 'entries/:id', loadComponent: () => import('./pages/entry-detail').then(m => m.EntryDetailPage) },
      { path: 'write', loadComponent: () => import('./pages/write').then(m => m.Write) },
      { path: 'chat', loadComponent: () => import('./pages/chat').then(m => m.Chat) },
      { path: 'search', loadComponent: () => import('./pages/search').then(m => m.SearchPage) },
      { path: 'timeline', loadComponent: () => import('./pages/timeline').then(m => m.Timeline) },
      { path: 'graph', loadComponent: () => import('./pages/graph').then(m => m.Graph) },
      { path: 'goals', loadComponent: () => import('./pages/goals').then(m => m.Goals) },
      { path: 'energy', loadComponent: () => import('./pages/energy').then(m => m.Energy) },
      { path: 'review', loadComponent: () => import('./pages/review').then(m => m.Review) },
      { path: 'insights', loadComponent: () => import('./pages/insights').then(m => m.Insights) },
      { path: 'themes', loadComponent: () => import('./pages/themes').then(m => m.Themes) },
    ],
  },
  { path: '**', redirectTo: 'dashboard' },
];
