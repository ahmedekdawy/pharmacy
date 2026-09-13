import { Component, Input, output } from '@angular/core';

export type AppIcon =
  | 'save'
  | 'edit'
  | 'delete'
  | 'add'
  | 'check'
  | 'ban'
  | 'close'
  | 'login';

@Component({
  selector: 'app-icon-button',
  standalone: true,
  template: `
    <button
      type="{{ type }}"
      class="icon-action"
      [class.danger]="tone === 'danger'"
      [class.success]="tone === 'success'"
      [class.primary]="tone === 'primary'"
      [disabled]="disabled"
      [attr.title]="title || label"
      [attr.aria-label]="label"
      (click)="pressed.emit($event)"
    >
      <svg viewBox="0 0 24 24" aria-hidden="true">
        @switch (icon) {
          @case ('save') {
            <path d="M5 3h11l3 3v15H5V3zm2 2v5h9V5H7zm0 7v6h10v-6H7z" fill="currentColor"/>
          }
          @case ('edit') {
            <path d="M4 17.3V20h2.7L17.8 8.9l-2.7-2.7L4 17.3zM19.7 7c.4-.4.4-1 0-1.4l-1.3-1.3a1 1 0 0 0-1.4 0l-1 1 2.7 2.7 1-1z" fill="currentColor"/>
          }
          @case ('delete') {
            <path d="M7 6V4h10v2h5v2H2V6h5zm2 4h2v8H9v-8zm4 0h2v8h-2v-8zM6 8h12l-1 12H7L6 8z" fill="currentColor"/>
          }
          @case ('add') {
            <path d="M11 5h2v6h6v2h-6v6h-2v-6H5v-2h6V5z" fill="currentColor"/>
          }
          @case ('check') {
            <path d="M9.2 16.6 4.8 12.2l1.4-1.4 3 3 8-8 1.4 1.4-9.4 9.4z" fill="currentColor"/>
          }
          @case ('ban') {
            <path d="M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18zm0 2a7 7 0 0 1 5.3 11.7L6.3 6.7A7 7 0 0 1 12 5zm0 14a7 7 0 0 1-5.3-11.7l11 11A7 7 0 0 1 12 19z" fill="currentColor"/>
          }
          @case ('close') {
            <path d="M7 7l10 10M17 7 7 17" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"/>
          }
          @case ('login') {
            <path d="M11 4h8v16h-8v-2h6V6h-6V4zM3 12l5-5v3h7v4H8v3l-5-5z" fill="currentColor"/>
          }
        }
      </svg>
    </button>
  `,
  styles: [`
    :host { display: inline-flex; }
    .icon-action {
      width: 2.25rem;
      height: 2.25rem;
      border-radius: 0.6rem;
      border: 1px solid var(--border);
      background: var(--surface);
      color: var(--text);
      display: grid;
      place-items: center;
      cursor: pointer;
      padding: 0;
    }
    .icon-action svg { width: 1.05rem; height: 1.05rem; }
    .icon-action:hover:not(:disabled) { background: var(--surface-muted); }
    .icon-action:disabled { opacity: 0.55; cursor: default; }
    .icon-action.primary { background: var(--accent); border-color: transparent; color: #fff; }
    .icon-action.primary:hover:not(:disabled) { background: var(--accent-strong); }
    .icon-action.success { color: var(--accent-strong); }
    .icon-action.danger { color: #c45c4a; }
  `]
})
export class IconButton {
  @Input({ required: true }) icon!: AppIcon;
  @Input() label = '';
  @Input() title = '';
  @Input() type: 'button' | 'submit' = 'button';
  @Input() tone: 'default' | 'primary' | 'danger' | 'success' = 'default';
  @Input() disabled = false;
  readonly pressed = output<MouseEvent>();
}
