import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export interface StaffMedia {
  id: string;
  languageId: number | null;
  mimeType: string;
  byteSize: number;
  durationMilliseconds: number | null;
  source: string;
  license: string;
  uploadedBy: string;
  isVerified: boolean;
  storageState: 'Pending' | 'Ready' | 'Deleting';
  createdAt: string;
  verifiedBy: string | null;
  reviewComment: string | null;
}

@Injectable({ providedIn: 'root' })
export class MediaApi {
  private readonly http = inject(HttpClient);
  private readonly url = '/api/v1/manage/media';

  list(page: number): Promise<readonly StaffMedia[]> {
    return firstValueFrom(this.http.get<StaffMedia[]>(this.url, { params: { page } }));
  }

  upload(file: File, languageId: number, source: string, license: string): Promise<{ id: string }> {
    const form = new FormData();
    form.append('file', file);
    form.append('languageId', String(languageId));
    form.append('source', source);
    form.append('license', license);
    return firstValueFrom(this.http.post<{ id: string }>(this.url, form));
  }

  preview(id: string): Promise<{ url: string; expiresInSeconds: number }> {
    return firstValueFrom(
      this.http.get<{ url: string; expiresInSeconds: number }>(this.path(id) + '/preview'),
    );
  }

  review(id: string, decision: 'approve' | 'reject', comment: string): Promise<unknown> {
    return firstValueFrom(this.http.post(this.path(id) + '/reviews', { decision, comment }));
  }

  remove(id: string): Promise<unknown> {
    return firstValueFrom(this.http.delete(this.path(id)));
  }
  finishDeletion(id: string): Promise<unknown> {
    return firstValueFrom(this.http.post(this.path(id) + '/finish-deletion', {}));
  }

  private path(id: string): string {
    return this.url + '/' + encodeURIComponent(id);
  }
}
