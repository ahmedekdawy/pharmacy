import { Component, OnDestroy, computed, inject, signal } from '@angular/core';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { Subscription, filter } from 'rxjs';
import { AuthService } from '../core/services/auth.service';
import { LocalizationService } from '../core/services/localization.service';
import { ThemeService } from '../core/services/theme.service';
import { SyncService } from '../core/offline/sync.service';

const SIDEBAR_KEY = 'pharmacy.sidebar_collapsed';
const MOBILE_QUERY = '(max-width: 900px)';

@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './shell.html',
  styleUrl: './shell.scss'
})
export class AppShell implements OnDestroy {
  readonly auth = inject(AuthService);
  readonly i18n = inject(LocalizationService);
  readonly theme = inject(ThemeService);
  readonly sync = inject(SyncService);
  private readonly router = inject(Router);
  private readonly navSub: Subscription;
  private readonly media = window.matchMedia(MOBILE_QUERY);
  private readonly onMediaChange = (e: MediaQueryListEvent) => this.isMobile.set(e.matches);

  readonly collapsed = signal(localStorage.getItem(SIDEBAR_KEY) === '1');
  readonly isMobile = signal(this.media.matches);
  readonly mobileOpen = signal(false);
  /** Desktop: collapsed sidebar hides labels. Mobile: the drawer always shows labels. */
  readonly showLabels = computed(() => this.isMobile() || !this.collapsed());
  readonly desktopCollapsed = computed(() => !this.isMobile() && this.collapsed());

  readonly userLabel = computed(() => {
    const user = this.auth.user();
    if (!user) {
      return '';
    }
    return this.i18n.language() === 'ar' ? user.fullNameAr : user.fullNameEn;
  });

  readonly navItems = computed(() => {
    const lang = this.i18n.language();
    return [...this.auth.pages()]
      .sort((a, b) => a.sortOrder - b.sortOrder)
      .map((page) => ({
        ...page,
        label: lang === 'ar' ? page.nameAr : page.nameEn,
        icon: iconFor(page.code)
      }));
  });

  readonly syncLabel = computed(() => {
    if (this.sync.syncing()) return this.i18n.pick('Syncing…', 'مزامنة…');
    if (!this.sync.online()) return this.i18n.pick('Offline', 'بدون إنترنت');
    if (this.sync.failedCount()) return this.i18n.pick('Sync issues', 'مشاكل مزامنة');
    if (this.sync.pendingCount()) return this.i18n.pick('Pending', 'معلّق');
    return this.i18n.pick('Synced', 'متزامن');
  });

  constructor() {
    this.media.addEventListener('change', this.onMediaChange);
    this.navSub = this.router.events
      .pipe(filter((e) => e instanceof NavigationEnd))
      .subscribe(() => this.mobileOpen.set(false));
    void this.sync.init();
  }

  ngOnDestroy(): void {
    this.media.removeEventListener('change', this.onMediaChange);
    this.navSub.unsubscribe();
  }

  t(key: string): string {
    return this.i18n.translate(key);
  }

  toggleSidebar(): void {
    if (this.isMobile()) {
      this.mobileOpen.update((v) => !v);
      return;
    }
    const next = !this.collapsed();
    this.collapsed.set(next);
    localStorage.setItem(SIDEBAR_KEY, next ? '1' : '0');
  }

  logout(): void {
    const pending = this.sync.pendingCount() + this.sync.failedCount();
    if (
      pending > 0 &&
      !confirm(
        this.i18n.pick(
          `${pending} operation(s) are not uploaded yet. They stay saved on this device and will upload after you sign in again. Sign out?`,
          `يوجد ${pending} عملية لم تُرفع بعد. ستبقى محفوظة على الجهاز وتُرفع بعد تسجيل الدخول مرة أخرى. تسجيل الخروج؟`
        )
      )
    ) {
      return;
    }
    void this.sync.reset();
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
    case 'shortages':
      return 'inventory';
    case 'suppliers':
    case 'purchases':
    case 'pos':
      return 'products';
    case 'customers':
    case 'sales':
    case 'returns':
    case 'debts':
      return 'users';
    case 'cash-shifts':
    case 'expenses':
    case 'reports':
    case 'daily-report':
    case 'audit':
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
