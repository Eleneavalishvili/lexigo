import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthStore } from '../accounts/auth-store';
export const contentGuard: CanActivateFn = async () => {
  const auth = inject(AuthStore);
  const router = inject(Router);
  await auth.restore();
  return (
    auth
      .user()
      ?.roles.some((role) => ['ContentAuthor', 'Reviewer', 'Admin', 'SuperAdmin'].includes(role)) ||
    router.createUrlTree(['/account'])
  );
};
