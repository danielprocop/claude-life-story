import { bootstrapApplication } from '@angular/platform-browser';
import { appConfig } from './app/app.config';
import { App } from './app/app';
import { configureAmplifyAuth } from './app/services/auth';

configureAmplifyAuth();

bootstrapApplication(App, appConfig)
  .catch((err) => console.error(err));
