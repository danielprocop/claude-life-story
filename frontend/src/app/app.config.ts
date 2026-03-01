import {
  APP_INITIALIZER,
  ApplicationConfig,
  provideBrowserGlobalErrorListeners,
  isDevMode,
} from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';

import { routes } from './app.routes';
import { AuthService } from './services/auth';
import { authInterceptor } from './services/auth-interceptor';
import { provideServiceWorker } from '@angular/service-worker';
import { PwaUpdateService } from './services/pwa-update';

function initializeAuth(authService: AuthService): () => Promise<void> {
  return () => authService.initialize();
}

function initializePwaUpdates(pwaUpdateService: PwaUpdateService): () => void {
  return () => pwaUpdateService.start();
}

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideHttpClient(withInterceptors([authInterceptor])),
    {
      provide: APP_INITIALIZER,
      useFactory: initializeAuth,
      deps: [AuthService],
      multi: true,
    },
    {
      provide: APP_INITIALIZER,
      useFactory: initializePwaUpdates,
      deps: [PwaUpdateService],
      multi: true,
    },
    provideServiceWorker('ngsw-worker.js', {
      enabled: !isDevMode(),
      registrationStrategy: 'registerWhenStable:30000',
    }),
  ],
};
