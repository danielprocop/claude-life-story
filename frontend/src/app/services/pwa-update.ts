import { Injectable } from '@angular/core';
import { SwUpdate, VersionReadyEvent } from '@angular/service-worker';
import { filter, interval } from 'rxjs';

@Injectable({
  providedIn: 'root',
})
export class PwaUpdateService {
  private readonly checkIntervalMs = 5 * 60 * 1000;
  private readonly reloadGuardKey = 'diario_pwa_last_reload_at';
  private started = false;

  constructor(private readonly swUpdate: SwUpdate) {}

  start(): void {
    if (this.started || !this.swUpdate.isEnabled) {
      return;
    }

    this.started = true;

    this.swUpdate.versionUpdates
      .pipe(filter((event): event is VersionReadyEvent => event.type === 'VERSION_READY'))
      .subscribe(() => this.activateAndReload());

    interval(this.checkIntervalMs).subscribe(() => {
      void this.swUpdate.checkForUpdate().catch(() => undefined);
    });

    void this.swUpdate.checkForUpdate().catch(() => undefined);
  }

  private activateAndReload(): void {
    const now = Date.now();
    const lastReloadRaw = localStorage.getItem(this.reloadGuardKey);
    const lastReload = lastReloadRaw ? Number(lastReloadRaw) : 0;

    if (Number.isFinite(lastReload) && now - lastReload < 60_000) {
      return;
    }

    localStorage.setItem(this.reloadGuardKey, now.toString());

    void this.swUpdate.activateUpdate().finally(() => {
      document.location.reload();
    });
  }
}
