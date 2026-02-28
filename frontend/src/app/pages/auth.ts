import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from '../services/auth';

type AuthMode = 'signin' | 'signup' | 'confirm';

@Component({
  selector: 'app-auth-page',
  imports: [CommonModule, FormsModule],
  templateUrl: './auth.html',
  styleUrl: './auth.scss',
})
export class AuthPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly mode = signal<AuthMode>('signin');
  readonly working = signal(false);
  readonly error = signal('');
  readonly notice = signal('');
  readonly authEnabled = this.auth.enabled;

  email = '';
  password = '';
  confirmPassword = '';
  confirmationCode = '';

  setMode(mode: AuthMode): void {
    this.mode.set(mode);
    this.error.set('');
    this.notice.set('');
  }

  async submit(): Promise<void> {
    this.error.set('');
    this.notice.set('');

    if (!this.authEnabled) {
      this.notice.set('Cognito non configurato in questo ambiente. In locale puoi usare il backend senza login.');
      return;
    }

    this.working.set(true);

    try {
      const email = this.email.trim().toLowerCase();

      if (this.mode() === 'signin') {
        await this.auth.signIn(email, this.password);
        await this.router.navigateByUrl(this.route.snapshot.queryParamMap.get('redirect') || '/dashboard');
        return;
      }

      if (this.mode() === 'signup') {
        if (this.password !== this.confirmPassword) {
          throw new Error('Le password non coincidono.');
        }

        const signUpComplete = await this.auth.signUp(email, this.password);
        if (signUpComplete) {
          this.notice.set('Account creato. Ora puoi accedere.');
          this.setMode('signin');
          return;
        }

        this.notice.set('Codice inviato via email. Conferma l’account per completare la registrazione.');
        this.setMode('confirm');
        return;
      }

      await this.auth.confirmSignUp(email, this.confirmationCode.trim());
      this.notice.set('Account confermato. Ora puoi accedere.');
      this.setMode('signin');
    } catch (error) {
      this.error.set(error instanceof Error ? error.message : 'Operazione non riuscita.');
    } finally {
      this.working.set(false);
    }
  }
}
