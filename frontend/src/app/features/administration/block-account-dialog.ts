import { DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { Component, inject } from '@angular/core';

@Component({
  selector: 'app-block-account-dialog',
  template: `
    <h2 id="block-account-title">Block this account?</h2>
    <p id="block-account-description">
      Blocking {{ displayName }} revokes their sessions and prevents them from signing in. You can
      restore their access later.
    </p>
    <div class="actions">
      <button class="button button-outline" type="button" (click)="dialog.close(false)">
        Cancel
      </button>
      <button class="button button-red" type="button" (click)="dialog.close(true)">
        Block account
      </button>
    </div>
  `,
  styles: `
    :host {
      display: block;
      max-height: 85dvh;
      overflow: auto;
      overflow-wrap: anywhere;
      background: var(--surface);
      color: var(--ink);
      border: 1px solid var(--line);
      border-radius: 1rem;
      padding: clamp(1rem, 4vw, 2rem);
    }
    h2 {
      margin-top: 0;
    }
    .actions {
      display: flex;
      gap: 0.75rem;
      flex-wrap: wrap;
      justify-content: flex-end;
    }
  `,
})
export class BlockAccountDialog {
  protected readonly displayName = inject<string>(DIALOG_DATA);
  protected readonly dialog = inject<DialogRef<boolean>>(DialogRef);
}
