import { Component, ElementRef, OnInit, afterNextRender, inject, input, output, signal, viewChild } from '@angular/core';
import { LocalizationService } from '../../core/services/localization.service';
import { SyncService } from '../../core/offline/sync.service';
import { PosProduct, newId } from '../../core/offline/models';

/**
 * Small dialog to record a shortage (ناقص) without leaving the current screen, e.g. while selling.
 * Goes through the sync outbox, so it also works offline. Enter saves, Esc closes.
 */
@Component({
  selector: 'app-shortage-quick-add',
  standalone: true,
  template: `
    <div class="backdrop" (click)="closed.emit()"></div>
    <form class="sheet" role="dialog" aria-modal="true" [attr.aria-label]="pick('Add to shortages', 'إضافة للنواقص')"
          (submit)="$event.preventDefault(); save()" (keydown.escape)="closed.emit()">
      <h2>{{ pick('Add to shortages', 'إضافة للنواقص') }}</h2>

      <label class="field">
        <span class="field-label">{{ pick('Item', 'الصنف') }} <span class="req">*</span></span>
        @if (product(); as p) {
          <span class="item"><bdi>{{ name(p) }}</bdi> <small dir="ltr">{{ p.code }}</small></span>
        } @else {
          <input #nameBox dir="auto" [value]="itemName()" (input)="itemName.set($any($event.target).value)"
                 [placeholder]="pick('Item name as the customer asked for it', 'اسم الصنف كما طلبه العميل')" />
        }
      </label>

      <div class="row">
        <label class="field">
          <span class="field-label">{{ pick('Quantity (boxes)', 'الكمية (علبة)') }} <span class="req">*</span></span>
          <input #qtyBox type="number" min="1" step="1" inputmode="numeric" [value]="quantity()"
                 (input)="quantity.set(+$any($event.target).value)" />
        </label>
        <label class="field">
          <span class="field-label">{{ pick('Customer phone', 'هاتف العميل') }}</span>
          <input dir="ltr" inputmode="tel" [value]="customerPhone()" (input)="customerPhone.set($any($event.target).value)"
                 [placeholder]="pick('To call when it arrives', 'للاتصال عند توفره')" />
        </label>
      </div>

      <label class="field">
        <span class="field-label">{{ pick('Customer name', 'اسم العميل') }}</span>
        <input [value]="customerName()" (input)="customerName.set($any($event.target).value)" />
      </label>
      <label class="field">
        <span class="field-label">{{ pick('Notes', 'ملاحظات') }}</span>
        <input [value]="notes()" (input)="notes.set($any($event.target).value)"
               [placeholder]="pick('e.g. alternative brand is OK', 'مثال: يقبل بديل')" />
      </label>

      @if (error()) { <p class="error">{{ error() }}</p> }

      <div class="actions">
        <button type="button" class="btn" (click)="closed.emit()">{{ pick('Cancel', 'إلغاء') }}</button>
        <button type="submit" class="btn primary" [disabled]="busy()">
          {{ busy() ? pick('Saving…', 'جاري الحفظ…') : pick('Add to shortages', 'أضف للنواقص') }}
        </button>
      </div>
    </form>
  `,
  styles: [`
    :host { position:fixed; inset:0; z-index:60; display:grid; place-items:center; padding:1rem; }
    .backdrop { position:absolute; inset:0; background:rgba(0,0,0,.45); }
    .sheet { position:relative; background:var(--surface); color:var(--text); border-radius:.8rem; padding:1.1rem 1.25rem;
      width:min(28rem,100%); display:grid; gap:.65rem; box-shadow:0 12px 40px rgba(0,0,0,.25); }
    h2 { margin:0; font-size:1.1rem; }
    .row { display:grid; grid-template-columns:1fr 1fr; gap:.6rem; }
    input { width:100%; padding:.6rem .7rem; border:1px solid var(--border); border-radius:.45rem; background:var(--surface); color:var(--text); font:inherit; }
    .item { padding:.55rem .7rem; border:1px dashed var(--border); border-radius:.45rem; small { color:var(--muted); margin-inline-start:.35rem; } }
    .actions { display:flex; justify-content:flex-end; gap:.5rem; margin-top:.25rem; }
    .btn { border:1px solid var(--border); background:var(--surface-muted); color:var(--text); border-radius:.45rem; padding:.55rem .9rem; font:inherit; cursor:pointer; }
    .btn.primary { background:var(--accent); color:#fff; border-color:transparent; font-weight:600; }
    .error { color:#c45c4a; margin:0; }
    @media (max-width:480px) { .row { grid-template-columns:1fr; } }
  `]
})
export class ShortageQuickAdd implements OnInit {
  private readonly i18n = inject(LocalizationService);
  private readonly sync = inject(SyncService);
  private readonly nameBox = viewChild<ElementRef<HTMLInputElement>>('nameBox');
  private readonly qtyBox = viewChild<ElementRef<HTMLInputElement>>('qtyBox');

  /** Catalog product, or null for an item the pharmacy does not stock. */
  readonly product = input<PosProduct | null>(null);
  /** Prefilled name for free-text items (e.g. what was typed in the POS search). */
  readonly initialName = input('');
  readonly locationId = input<string | null>(null);

  readonly closed = output<void>();
  /** Emits a short confirmation message for the host screen to show. */
  readonly saved = output<string>();

  readonly itemName = signal('');
  readonly quantity = signal(1);
  readonly customerName = signal('');
  readonly customerPhone = signal('');
  readonly notes = signal('');
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);

  constructor() {
    afterNextRender(() => (this.nameBox() ?? this.qtyBox())?.nativeElement.focus());
  }

  ngOnInit(): void {
    this.itemName.set(this.initialName().trim());
  }

  pick(en: string, ar: string): string {
    return this.i18n.pick(en, ar);
  }

  name(p: PosProduct): string {
    return this.i18n.language() === 'ar' ? p.nameAr || p.nameEn : p.nameEn || p.nameAr;
  }

  async save(): Promise<void> {
    const product = this.product();
    const itemName = product ? '' : this.itemName().trim();
    const quantity = Number(this.quantity());
    if (!product && !itemName) {
      this.error.set(this.pick('Enter the item name.', 'اكتب اسم الصنف.'));
      return;
    }
    if (!(quantity > 0)) {
      this.error.set(this.pick('Quantity must be at least 1.', 'الكمية يجب أن تكون 1 على الأقل.'));
      return;
    }

    this.busy.set(true);
    this.error.set(null);
    const label = product ? this.name(product) : itemName;
    try {
      const outcome = await this.sync.submit(
        'shortage.create',
        {
          id: newId(),
          productId: product?.id ?? null,
          itemName: product ? null : itemName,
          quantity,
          locationId: this.locationId() || null,
          customerName: this.customerName().trim() || null,
          customerPhone: this.customerPhone().trim() || null,
          notes: this.notes().trim() || null,
          source: 'POS'
        },
        `${this.pick('Shortage', 'نواقص')}: ${label}`
      );
      if (outcome.kind === 'rejected') {
        this.error.set(outcome.error);
        return;
      }
      this.saved.emit(
        outcome.kind === 'queued'
          ? this.pick(`"${label}" saved to shortages on this device; it will upload when online.`, `تم حفظ «${label}» في النواقص على الجهاز وسيُرفع عند عودة الإنترنت.`)
          : this.pick(`"${label}" added to shortages.`, `تمت إضافة «${label}» إلى النواقص.`)
      );
    } finally {
      this.busy.set(false);
    }
  }
}
