import { Component, DestroyRef, effect, inject, signal, untracked } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { AuthStore } from '../../core/accounts/auth-store';

const avatarPath = '/api/v1/me/avatar';
const side = 128;
const maxBytes = 5 * 1024 * 1024;

@Component({
  selector: 'app-profile-avatar',
  templateUrl: './profile-avatar.html',
  styleUrl: './profile-avatar.scss',
})
export class ProfileAvatar {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthStore);
  private readonly destroy = inject(DestroyRef);
  private generation = 0;
  private pixels: Blob | null = null;
  protected readonly photo = signal<string | null>(null);
  protected readonly preview = signal<string | null>(null);
  protected readonly busy = signal(false);
  protected readonly error = signal('');
  protected readonly notice = signal('');

  constructor() {
    effect(() => {
      const owner = this.auth.user()?.id;
      const generation = ++this.generation;
      this.photo.set(null);
      untracked(() => this.clearPreview());
      this.error.set('');
      this.notice.set('');
      this.busy.set(false);
      if (owner) void this.read(owner, generation);
    });
    this.destroy.onDestroy(() => this.clearPreview());
  }

  private async read(owner: string, generation: number): Promise<void> {
    try {
      const result = await firstValueFrom(this.http.get<{ url: string } | null>(avatarPath));
      if (this.current(owner, generation)) this.photo.set(result?.url ?? null);
    } catch {
      if (this.current(owner, generation)) this.error.set('We couldn’t load your profile photo.');
    }
  }

  protected async pick(input: HTMLInputElement): Promise<void> {
    const file = input.files?.[0];
    input.value = '';
    const owner = this.auth.user()?.id;
    if (!owner || !file || this.busy()) return;
    const generation = ++this.generation;
    this.clearPreview();
    this.error.set('');
    this.notice.set('');
    if (file.size > maxBytes || !['image/png', 'image/jpeg', 'image/webp'].includes(file.type)) {
      this.error.set('Choose a PNG, JPEG, or WebP image under 5 MB.');
      return;
    }
    this.busy.set(true);
    let bitmap: ImageBitmap | undefined;
    try {
      bitmap = await createImageBitmap(file);
      if (!this.current(owner, generation)) return;
      const crop = Math.min(bitmap.width, bitmap.height);
      const canvas = document.createElement('canvas');
      canvas.width = canvas.height = side;
      const context = canvas.getContext('2d');
      if (!context || crop < 1) throw new Error('Image could not be cropped');
      context.drawImage(
        bitmap,
        (bitmap.width - crop) / 2,
        (bitmap.height - crop) / 2,
        crop,
        crop,
        0,
        0,
        side,
        side,
      );
      const rgba = new Uint8Array(context.getImageData(0, 0, side, side).data);
      const preview = await new Promise<Blob | null>((resolve) =>
        canvas.toBlob(resolve, 'image/png'),
      );
      if (!preview) throw new Error('Image could not be previewed');
      if (!this.current(owner, generation)) return;
      this.pixels = new Blob([rgba], { type: 'application/octet-stream' });
      this.preview.set(URL.createObjectURL(preview));
    } catch {
      if (this.current(owner, generation))
        this.error.set('That image couldn’t be opened. Try another one.');
    } finally {
      bitmap?.close();
      if (this.current(owner, generation)) this.busy.set(false);
    }
  }

  protected clearPreview(): void {
    const url = this.preview();
    if (url) URL.revokeObjectURL(url);
    this.preview.set(null);
    this.pixels = null;
  }

  protected async save(): Promise<void> {
    const pixels = this.pixels;
    if (pixels) await this.change('save', pixels);
  }

  protected async remove(): Promise<void> {
    await this.change('remove');
  }

  private async change(action: 'save' | 'remove', pixels?: Blob): Promise<void> {
    const owner = this.auth.user()?.id;
    if (!owner || this.busy()) return;
    const generation = ++this.generation;
    this.busy.set(true);
    this.error.set('');
    this.notice.set('');
    try {
      await firstValueFrom(
        action === 'save' ? this.http.put(avatarPath, pixels) : this.http.delete(avatarPath),
      );
      if (!this.current(owner, generation)) return;
      this.clearPreview();
      this.photo.set(null);
      this.notice.set(action === 'save' ? 'Profile photo saved.' : 'Profile photo removed.');
      if (action === 'save') await this.read(owner, generation);
    } catch {
      if (this.current(owner, generation))
        this.error.set('We couldn’t confirm that change. Try again.');
    } finally {
      if (this.current(owner, generation)) this.busy.set(false);
    }
  }

  private current(owner: string, generation: number): boolean {
    return (
      !this.destroy.destroyed && this.generation === generation && this.auth.user()?.id === owner
    );
  }
}
