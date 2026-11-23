import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export interface HeartStatus {
  unlimited?: boolean;
  count: number;
  maximum: number;
  refillAt: string | null;
  serverNow: string;
}

@Injectable({ providedIn: 'root' })
export class HeartsApi {
  private readonly http = inject(HttpClient);
  status(): Promise<HeartStatus> {
    return firstValueFrom(this.http.get<HeartStatus>('/api/v1/me/hearts'));
  }
}
