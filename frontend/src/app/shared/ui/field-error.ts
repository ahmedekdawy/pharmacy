import { Component, inject, input } from '@angular/core';
import { AbstractControl } from '@angular/forms';
import { LocalizationService } from '../../core/services/localization.service';

/** Shows the first validation message for a control once the user has touched it or tried to submit. */
@Component({
  selector: 'app-field-error',
  standalone: true,
  template: `
    @if (message(); as msg) {
      <small class="field-error" role="alert">{{ msg }}</small>
    }
  `,
  styles: [
    `
      .field-error {
        color: #c45c4a;
        font-size: 0.82rem;
      }
    `
  ]
})
export class FieldError {
  private readonly i18n = inject(LocalizationService);
  readonly control = input.required<AbstractControl | null>();
  /** Field name used in messages, e.g. "Password". */
  readonly label = input<string>('');

  message(): string | null {
    const c = this.control();
    if (!c || !c.invalid || !(c.touched || c.dirty)) {
      return null;
    }
    const e = c.errors ?? {};
    const name = this.label();
    if (e['required']) {
      return name ? this.i18n.pick(`${name} is required.`, `${name} مطلوب.`) : this.i18n.pick('This field is required.', 'هذا الحقل مطلوب.');
    }
    if (e['email']) {
      return this.i18n.pick('Enter a valid email address.', 'أدخل بريدًا إلكترونيًا صحيحًا.');
    }
    if (e['minlength']) {
      const n = e['minlength'].requiredLength;
      return this.i18n.pick(`Must be at least ${n} characters.`, `يجب ألا يقل عن ${n} أحرف.`);
    }
    if (e['maxlength']) {
      const n = e['maxlength'].requiredLength;
      return this.i18n.pick(`Must be at most ${n} characters.`, `يجب ألا يزيد عن ${n} حرفًا.`);
    }
    if (e['min']) {
      return this.i18n.pick(`Must be at least ${e['min'].min}.`, `يجب ألا يقل عن ${e['min'].min}.`);
    }
    if (e['max']) {
      return this.i18n.pick(`Must be at most ${e['max'].max}.`, `يجب ألا يزيد عن ${e['max'].max}.`);
    }
    if (e['pattern']) {
      return this.i18n.pick('Invalid format.', 'صيغة غير صحيحة.');
    }
    return this.i18n.pick('Invalid value.', 'قيمة غير صحيحة.');
  }
}
