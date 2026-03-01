import { Injectable, signal } from '@angular/core';

interface BeforeInstallPromptEvent extends Event {
  prompt(): Promise<void>;
  userChoice: Promise<{ outcome: 'accepted' | 'dismissed'; platform: string }>;
}

export interface InstallPromptState {
  canInstall: boolean;
  showIosHint: boolean;
  installed: boolean;
  dismissed: boolean;
}

const DISMISS_STORAGE_KEY = 'diario_install_prompt_dismissed_v1';

@Injectable({
  providedIn: 'root',
})
export class InstallPromptService {
  readonly canInstall = signal(false);
  readonly showIosHint = signal(false);
  readonly installed = signal(false);
  readonly dismissed = signal(false);

  private deferredPrompt: BeforeInstallPromptEvent | null = null;

  constructor() {
    if (typeof window === 'undefined') {
      return;
    }

    this.dismissed.set(window.localStorage.getItem(DISMISS_STORAGE_KEY) === '1');
    this.bindInstallEvents();
  }

  async promptInstall(): Promise<void> {
    if (!this.deferredPrompt) {
      return;
    }

    await this.deferredPrompt.prompt();
    const choice = await this.deferredPrompt.userChoice;
    if (choice.outcome === 'accepted') {
      this.installed.set(true);
      this.clearPrompt();
      return;
    }

    this.dismiss();
  }

  dismiss(): void {
    if (typeof window !== 'undefined') {
      window.localStorage.setItem(DISMISS_STORAGE_KEY, '1');
    }

    this.dismissed.set(true);
    this.canInstall.set(false);
    this.showIosHint.set(false);
  }

  private bindInstallEvents(): void {
    const standalone =
      window.matchMedia('(display-mode: standalone)').matches ||
      ((window.navigator as Navigator & { standalone?: boolean }).standalone ?? false);

    if (standalone) {
      this.installed.set(true);
      return;
    }

    const ios = /iphone|ipad|ipod/i.test(window.navigator.userAgent);
    if (ios && !this.dismissed()) {
      this.showIosHint.set(true);
    }

    window.addEventListener('beforeinstallprompt', (event) => {
      event.preventDefault();
      this.deferredPrompt = event as BeforeInstallPromptEvent;
      if (!this.dismissed()) {
        this.canInstall.set(true);
      }
    });

    window.addEventListener('appinstalled', () => {
      this.installed.set(true);
      this.clearPrompt();
    });
  }

  private clearPrompt(): void {
    this.deferredPrompt = null;
    this.canInstall.set(false);
    this.showIosHint.set(false);
  }
}
