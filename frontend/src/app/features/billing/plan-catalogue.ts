import { Component, inject, signal } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { PlanCatalogueStore } from '../../core/billing/plan-catalogue-store';
import { BrandLogo } from '../../shared/brand-logo/brand-logo';
import { PlanPriceSchedule } from './plan-price-schedule';
import { PlanMetadataEditor } from './plan-metadata-editor';

@Component({
  selector: 'app-plan-catalogue',
  imports: [RouterLink, CurrencyPipe, DatePipe, BrandLogo, PlanPriceSchedule, PlanMetadataEditor],
  providers: [PlanCatalogueStore],
  templateUrl: './plan-catalogue.html',
  styleUrls: ['./subscription.scss', './plan-catalogue.scss'],
})
export class PlanCatalogue {
  protected readonly store = inject(PlanCatalogueStore);
  protected readonly notice = signal('');
  constructor() {
    void this.store.load();
  }
  protected filter(value: string): void {
    this.notice.set('');
    void this.store.load(1, value === 'all' ? undefined : value === 'active');
  }
  protected saved(page: number, draft: boolean): void {
    this.notice.set(
      draft ? 'Draft plan saved. Find it under Inactive plans.' : 'Plan changes saved.',
    );
    void this.store.load(page);
  }
}
