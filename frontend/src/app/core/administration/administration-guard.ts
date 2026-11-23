import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthStore } from '../accounts/auth-store';

export const administrationGuard: CanActivateFn = async (route) => {
  const auth = inject(AuthStore);
  const router = inject(Router);
  await auth.restore();
  const roles = auth.user()?.roles;
  return (
    roles?.includes('SuperAdmin') ||
    (route.data['superAdminOnly'] !== true && roles?.includes('Admin')) ||
    router.createUrlTree(['/account'])
  );
};
