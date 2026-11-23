import { Component, DestroyRef, computed, effect, inject, signal, untracked } from '@angular/core';
import { RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { AuthStore } from '../../core/accounts/auth-store';
import {
  AchievementInput,
  ChallengeInput,
  ManagedAchievement,
  ManagedChallenge,
  RewardCatalogApi,
  RewardResource,
} from '../../core/content/reward-catalog-api';
import { BrandLogo } from '../../shared/brand-logo/brand-logo';
import { AchievementEditor } from './achievement-editor';
import { ChallengeEditor } from './challenge-editor';

@Component({
  selector: 'app-reward-catalog',
  imports: [RouterLink, BrandLogo, AchievementEditor, ChallengeEditor],
  templateUrl: './reward-catalog.html',
  styleUrl: './reward-catalog.scss',
})
export class RewardCatalog {
  private readonly auth = inject(AuthStore);
  private readonly api = inject(RewardCatalogApi);
  private readonly destroy = inject(DestroyRef);
  private generation = 0;
  protected readonly resource = signal<RewardResource>('achievements');
  protected readonly records = signal<readonly (ManagedAchievement | ManagedChallenge)[]>([]);
  protected readonly selected = signal<ManagedAchievement | ManagedChallenge | null>(null);
  protected readonly creating = signal(false);
  protected readonly page = signal(1);
  protected readonly pageSize = signal(20);
  protected readonly search = signal('');
  protected readonly totalCount = signal(0);
  protected readonly more = signal(false);
  protected readonly loading = signal(false);
  protected readonly working = signal(false);
  protected readonly error = signal('');
  protected readonly notice = signal('');
  protected readonly canManage = computed(
    () =>
      this.auth.user()?.roles.some((role) => role === 'Admin' || role === 'SuperAdmin') ?? false,
  );
  protected readonly achievement = computed(() => {
    const item = this.selected();
    return item && 'threshold' in item ? item : null;
  });
  protected readonly challenge = computed(() => {
    const item = this.selected();
    return item && 'rewardXp' in item ? item : null;
  });

  constructor() {
    effect(() => {
      this.auth.user();
      untracked(() => {
        this.page.set(1);
        this.search.set('');
        void this.load();
      });
    });
  }

  private current(generation: number, owner: string): boolean {
    return (
      !this.destroy.destroyed && generation === this.generation && this.auth.user()?.id === owner
    );
  }

  protected async load(preserveNotice = false): Promise<void> {
    const generation = ++this.generation;
    const user = this.auth.user();
    this.records.set([]);
    this.selected.set(null);
    this.creating.set(false);
    this.error.set('');
    if (!preserveNotice) this.notice.set('');
    this.more.set(false);
    this.totalCount.set(0);
    this.working.set(false);
    this.loading.set(false);
    if (!user?.roles.some((role) => role === 'Admin' || role === 'SuperAdmin')) return;
    this.loading.set(true);
    try {
      const result =
        this.resource() === 'achievements'
          ? await this.api.achievements(this.page(), this.pageSize(), this.search())
          : await this.api.challenges(this.page(), this.pageSize(), this.search());
      if (!this.current(generation, user.id)) return;
      this.records.set(result.items);
      this.more.set(result.hasMore);
      this.totalCount.set(result.totalCount);
    } catch {
      if (this.current(generation, user.id))
        this.error.set(
          'Catalog unavailable. Verified administrator access is required; refresh to try again.',
        );
    } finally {
      if (this.current(generation, user.id)) this.loading.set(false);
    }
  }

  protected choose(resource: RewardResource): void {
    if (this.working()) return;
    this.resource.set(resource);
    this.page.set(1);
    this.notice.set('');
    void this.load();
  }
  protected changePage(direction: number): void {
    if (this.loading() || this.working()) return;
    this.page.update((value) => Math.max(1, value + direction));
    void this.load();
  }
  protected filter(event?: Event): void {
    event?.preventDefault();
    if (this.loading() || this.working() || this.search().length > 100) return;
    this.page.set(1);
    void this.load();
  }
  protected changeSize(value: string): void {
    this.pageSize.set(Number(value));
    this.filter();
  }
  protected edit(item: ManagedAchievement | ManagedChallenge): void {
    if (this.working()) return;
    this.selected.set(item);
    this.creating.set(false);
    this.error.set('');
    this.notice.set('');
  }
  protected create(): void {
    this.selected.set(null);
    this.creating.set(true);
    this.error.set('');
    this.notice.set('');
  }
  protected saveAchievement(input: AchievementInput): void {
    if (this.resource() !== 'achievements') return;
    const selected = this.achievement();
    void this.write(() => this.api.saveAchievement(input, selected), 'Badge saved.');
  }
  protected saveChallenge(input: ChallengeInput): void {
    if (this.resource() !== 'challenges') return;
    const selected = this.challenge();
    void this.write(() => this.api.saveChallenge(input, selected), 'Challenge saved.');
  }
  protected archive(reason: string): void {
    const selected = this.selected();
    if (!selected) return;
    const resource = this.resource();
    void this.write(
      () => this.api.archive(resource, selected, reason),
      'Record retired. Existing learner history is preserved.',
    );
  }

  private async write(action: () => Promise<unknown>, message: string): Promise<void> {
    const owner = this.auth.user()?.id;
    if (!owner || this.working() || this.loading()) return;
    const generation = this.generation;
    this.working.set(true);
    this.error.set('');
    this.notice.set('');
    try {
      await action();
      if (!this.current(generation, owner)) return;
      this.notice.set(message);
      await this.load(true);
    } catch (error) {
      if (!this.current(generation, owner)) return;
      this.error.set(
        error instanceof HttpErrorResponse && error.status === 409
          ? (error.error?.title ?? 'This record changed. Refresh before editing again.')
          : 'The change could not be confirmed. Refresh the catalog before retrying.',
      );
    } finally {
      if (this.current(generation, owner)) this.working.set(false);
    }
  }

  protected summary(item: ManagedAchievement | ManagedChallenge): string {
    return 'threshold' in item
      ? item.code + ' · target ' + item.threshold
      : 'Target ' + item.target + ' · +' + item.rewardXp + ' XP';
  }
}
