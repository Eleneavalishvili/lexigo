import { Component, inject } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { NotificationsStore } from '../../core/accounts/notifications-store';
import { BrandLogo } from '../../shared/brand-logo/brand-logo';

@Component({
  selector: 'app-notifications',
  imports: [DatePipe, RouterLink, BrandLogo],
  providers: [NotificationsStore],
  templateUrl: './notifications.html',
  styleUrls: ['../billing/subscription.scss', './notifications.scss'],
})
export class Notifications {
  protected readonly store = inject(NotificationsStore);
}
