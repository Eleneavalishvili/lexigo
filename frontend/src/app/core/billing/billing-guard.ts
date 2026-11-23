import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthStore } from '../accounts/auth-store';

export const billingGuard: CanActivateFn = async () => {
  const auth = inject(AuthStore);
  const router = inject(Router);
  await auth.restore();
  const user = auth.user();
  return (
    (!!user &&
      (user.roles.includes('SuperAdmin') ||
        (user.roles.includes('Admin') && !!user.permissions?.includes('BillingManage')))) ||
    router.createUrlTree(['/account'])
  );
};
