import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, inject, input, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { AuthStore } from '../../core/accounts/auth-store';
import {
  BillingAdminApi,
  ManagedPlan,
  PlanFeatureSelection,
  PlanMetadataChange,
} from '../../core/billing/billing-admin-api';

@Component({
  selector: 'app-plan-metadata-editor',
  imports: [ReactiveFormsModule],
  templateUrl: './plan-metadata-editor.html',
  styleUrl: './plan-price-schedule.scss',
})
export class PlanMetadataEditor {
  readonly plan = input<ManagedPlan>();
  readonly saved = output<void>();
  private readonly api = inject(BillingAdminApi);
  private readonly lifetime = inject(DestroyRef);
  private readonly auth = inject(AuthStore);
  private readonly builder = inject(FormBuilder).nonNullable;
  protected readonly form = this.builder.group({
    code: ['', [Validators.maxLength(24)]],
    name: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(80)]],
    reason: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(500)]],
    currentPassword: ['', [Validators.required, Validators.maxLength(128)]],
  });
  protected readonly busy = signal(false);
  protected readonly message = signal('');
  protected readonly featureOptions = signal<ManagedPlan['features'] | null>(null);
  protected readonly changeAvailability = signal(false);
  protected readonly selectedFeatures = signal<Record<number, number | null>>({});
  protected readonly pending = signal<
    | (Omit<PlanMetadataChange, 'currentPassword'> & {
        code: string;
        features?: PlanFeatureSelection[];
        expectedFeatures?: PlanFeatureSelection[];
        isActive?: boolean;
        expectedIsActive?: boolean;
      })
    | null
  >(null);

  protected async openFeatures(): Promise<void> {
    const plan = this.plan();
    const owner = this.auth.user()?.id;
    if (!plan || !owner || this.busy() || this.pending() || this.featureOptions()) return;
    this.busy.set(true);
    this.message.set('');
    try {
      const options = await this.api.planFeatures();
      if (!this.lifetime.destroyed && this.auth.user()?.id === owner) {
        this.selectedFeatures.set(
          Object.fromEntries(plan.features.map((item) => [item.featureId, item.limitValue])),
        );
        if (!this.form.controls.name.value) this.form.controls.name.setValue(plan.name);
        this.featureOptions.set(options);
      }
    } catch {
      if (!this.lifetime.destroyed && this.auth.user()?.id === owner)
        this.message.set('The feature catalogue could not load. Try again.');
    } finally {
      if (!this.lifetime.destroyed && this.auth.user()?.id === owner) this.busy.set(false);
    }
  }

  protected selectFeature(id: number, checked: boolean): void {
    const selection = { ...this.selectedFeatures() };
    if (checked) selection[id] = null;
    else delete selection[id];
    this.selectedFeatures.set(selection);
  }

  protected featureSelected(id: number): boolean {
    return Object.hasOwn(this.selectedFeatures(), id);
  }

  protected setAvailability(checked: boolean): void {
    this.changeAvailability.set(checked);
    const plan = this.plan();
    if (checked && plan && !this.form.controls.name.value)
      this.form.controls.name.setValue(plan.name);
  }

  protected setLimit(id: number, input: HTMLInputElement): void {
    this.selectedFeatures.update((selection) => ({
      ...selection,
      [id]: input.value ? input.valueAsNumber : null,
    }));
  }

  protected async save(): Promise<void> {
    const plan = this.plan();
    const owner = this.auth.user()?.id;
    if (!owner || this.busy() || this.form.invalid) return;
    const { code, name, reason, currentPassword } = this.form.getRawValue();
    if (!plan && !/^[a-z\d_]{2,24}$/i.test(code.trim())) {
      this.message.set('Use a plan code with letters, digits or underscores.');
      return;
    }
    const features =
      plan && this.featureOptions()
        ? Object.entries(this.selectedFeatures()).map(([id, limitValue]) => ({
            featureId: Number(id),
            limitValue,
          }))
        : null;
    if (
      features?.some(
        ({ limitValue }) =>
          limitValue !== null &&
          (!Number.isInteger(limitValue) || limitValue < 1 || limitValue > 2_147_483_647),
      )
    ) {
      this.message.set('Use a positive whole number for each limit, or leave it blank.');
      return;
    }
    const change = this.pending() ?? {
      code,
      name,
      reason,
      idempotencyKey: crypto.randomUUID(),
      ...(plan && features
        ? {
            features,
            expectedFeatures: plan.features.map(({ featureId, limitValue }) => ({
              featureId,
              limitValue,
            })),
          }
        : {}),
      ...(plan && this.changeAvailability()
        ? { isActive: !plan.isActive, expectedIsActive: plan.isActive }
        : {}),
    };
    this.pending.set(change);
    this.busy.set(true);
    this.message.set('');
    this.form.controls.currentPassword.reset();
    const current = () => !this.lifetime.destroyed && this.auth.user()?.id === owner;
    try {
      const { code: draftCode, ...details } = change;
      const body = { ...details, currentPassword };
      if (plan) await this.api.renamePlan(plan.id, { ...body, expectedName: plan.name });
      else await this.api.createPlan({ ...body, code: draftCode });
      if (current()) {
        this.form.reset();
        this.changeAvailability.set(false);
        this.pending.set(null);
        this.saved.emit();
      }
    } catch (error) {
      if (!current()) return;
      if (error instanceof HttpErrorResponse && [400, 403, 404, 409].includes(error.status)) {
        this.pending.set(null);
        this.message.set(
          error.error?.title || 'The plan could not be saved. Reload the catalogue.',
        );
      } else {
        this.message.set('The result is unknown. Enter your password and retry the same change.');
      }
    } finally {
      if (current()) this.busy.set(false);
    }
  }
}
