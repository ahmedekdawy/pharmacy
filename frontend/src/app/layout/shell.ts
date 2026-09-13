import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../core/services/auth.service';
import { LocalizationService } from '../core/services/localization.service';
import { ThemeService } from '../core/services/theme.service';

const SIDEBAR_KEY = 'pharmacy.sidebar_collapsed';

@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './shell.html',
  styleUrl: './shell.scss'
})
export class AppShell {
  readonly auth = inject(AuthService);
  readonly i18n = inject(LocalizationService);
  readonly theme = inject(ThemeService);

  readonly collapsed = signal(localStorage.getItem(SIDEBAR_KEY) === '1');

  readonly userLabel = computed(() => {
    const user = this.auth.user();
    if (!user) {
      return '';
    }
    return this.i18n.language() === 'ar' ? user.fullNameAr : user.fullNameEn;
  });

  readonly navItems = computed(() => {
    const lang = this.i18n.language();
    return this.auth.pages().map((page) => ({
      ...page,
      label: lang === 'ar' ? page.nameAr : page.nameEn,
      icon: iconFor(page.code)
    }));
  });

  t(key: string): string {
    return this.i18n.translate(key);
  }

  toggleSidebar(): void {
    const next = !this.collapsed();
    this.collapsed.set(next);
    localStorage.setItem(SIDEBAR_KEY, next ? '1' : '0');
  }

  logout(): void {
    this.auth.logout();
  }
}

function iconFor(code: string): string {
  switch (code) {
    case 'dashboard':
      return 'dashboard';
    case 'products':
      return 'products';
    case 'categories':
      return 'categories';
    case 'brands':
      return 'brands';
    case 'locations':
      return 'locations';
    case 'inventory':
      return 'inventory';
    case 'suppliers':
    case 'purchases':
      return 'products';
    case 'customers':
    case 'sales':
    case 'returns':
      return 'users';
    case 'cash-shifts':
    case 'expenses':
    case 'reports':
    case 'settings':
      return 'dashboard';
    case 'users':
      return 'users';
    case 'roles':
      return 'roles';
    default:
      return 'default';
  }
}
