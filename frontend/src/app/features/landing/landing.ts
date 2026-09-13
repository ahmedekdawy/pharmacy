import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { LocalizationService } from '../../core/services/localization.service';

@Component({
  selector: 'app-landing',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './landing.html',
  styleUrl: './landing.scss'
})
export class Landing {
  readonly i18n = inject(LocalizationService);

  t(key: string): string {
    return this.i18n.translate(key);
  }

  setLang(lang: 'en' | 'ar'): void {
    this.i18n.setLanguage(lang);
  }
}
