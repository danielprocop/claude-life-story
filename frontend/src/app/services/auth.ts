import { Injectable, signal } from '@angular/core';
import { Amplify } from 'aws-amplify';
import {
  confirmSignUp,
  fetchAuthSession,
  getCurrentUser,
  signIn,
  signOut,
  signUp,
} from 'aws-amplify/auth';
import { environment } from '../../environments/environment';

export interface AuthenticatedUser {
  username: string;
  email: string;
}

export function configureAmplifyAuth(): void {
  if (!environment.auth.enabled) {
    return;
  }

  Amplify.configure({
    Auth: {
      Cognito: {
        userPoolId: environment.auth.userPoolId,
        userPoolClientId: environment.auth.userPoolClientId,
        loginWith: {
          email: true,
        },
      },
    },
  });
}

@Injectable({
  providedIn: 'root',
})
export class AuthService {
  readonly user = signal<AuthenticatedUser | null>(null);
  readonly loading = signal(true);
  readonly enabled = environment.auth.enabled;

  private initialized = false;
  private initializePromise: Promise<void> | null = null;

  async initialize(): Promise<void> {
    if (this.initialized) {
      this.loading.set(false);
      return;
    }

    if (this.initializePromise) {
      return this.initializePromise;
    }

    this.loading.set(true);
    this.initializePromise = this.loadCurrentUser();
    return this.initializePromise;
  }

  async signIn(email: string, password: string): Promise<void> {
    if (!this.enabled) {
      throw new Error('Autenticazione Cognito non configurata in questo ambiente.');
    }

    const result = await signIn({
      username: email,
      password,
    });

    if (result.nextStep.signInStep !== 'DONE') {
      throw new Error(`Step di login non supportato: ${result.nextStep.signInStep}`);
    }

    this.initialized = false;
    await this.initialize();
  }

  async signUp(email: string, password: string): Promise<boolean> {
    if (!this.enabled) {
      throw new Error('Autenticazione Cognito non configurata in questo ambiente.');
    }

    const result = await signUp({
      username: email,
      password,
      options: {
        userAttributes: {
          email,
        },
      },
    });

    return result.isSignUpComplete;
  }

  async confirmSignUp(email: string, code: string): Promise<void> {
    if (!this.enabled) {
      throw new Error('Autenticazione Cognito non configurata in questo ambiente.');
    }

    await confirmSignUp({
      username: email,
      confirmationCode: code,
    });
  }

  async signOut(): Promise<void> {
    if (this.enabled) {
      await signOut();
    }

    this.user.set(null);
    this.initialized = false;
    this.loading.set(false);
  }

  async getIdToken(): Promise<string | null> {
    if (!this.enabled) {
      return null;
    }

    const session = await fetchAuthSession();
    return session.tokens?.idToken?.toString() ?? null;
  }

  private async loadCurrentUser(): Promise<void> {
    if (!this.enabled) {
      this.user.set(null);
      this.initialized = true;
      this.loading.set(false);
      this.initializePromise = null;
      return;
    }

    try {
      const currentUser = await getCurrentUser();
      const session = await fetchAuthSession();
      const emailClaim = session.tokens?.idToken?.payload?.['email'];
      const email =
        typeof emailClaim === 'string'
          ? emailClaim
          : currentUser.signInDetails?.loginId ?? currentUser.username;

      this.user.set({
        username: currentUser.username,
        email,
      });
    } catch {
      this.user.set(null);
    } finally {
      this.initialized = true;
      this.loading.set(false);
      this.initializePromise = null;
    }
  }
}
