import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { Router, RouterOutlet, RouterLink, RouterLinkActive } from '@angular/router';
import { AuthService } from '../services/auth';
import { InstallPromptService } from '../services/install-prompt';

@Component({
  selector: 'app-layout',
  imports: [CommonModule, RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './layout.html',
  styleUrl: './layout.scss',
})
export class Layout {
  readonly auth = inject(AuthService);
  readonly install = inject(InstallPromptService);
  private readonly router = inject(Router);

  async signOut(): Promise<void> {
    await this.auth.signOut();
    await this.router.navigateByUrl('/auth');
  }

  dismissInstallPrompt(): void {
    this.install.dismiss();
  }

  async installApp(): Promise<void> {
    await this.install.promptInstall();
  }
}
