import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth';

export const authGuard: CanActivateFn = async (_, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  await auth.initialize();

  if (!auth.enabled || auth.user()) {
    return true;
  }

  return router.createUrlTree(['/auth'], {
    queryParams: {
      redirect: state.url,
    },
  });
};

export const guestGuard: CanActivateFn = async () => {
  const auth = inject(AuthService);
  const router = inject(Router);

  await auth.initialize();

  if (!auth.enabled) {
    return router.createUrlTree(['/dashboard']);
  }

  if (auth.user()) {
    return router.createUrlTree(['/dashboard']);
  }

  return true;
};
