import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { LocalizationService } from '../../core/services/localization.service';
import { TenantPublicService } from '../../core/services/tenant-public.service';

const TENANT_CODE = 'HAGAR';
const FALLBACK_WHATSAPP = '201122440801';

interface HagarCopy {
  brand: string;
  tagline: string;
  headline: string;
  subtitle: string;
  ctaLogin: string;
  ctaContact: string;
  ctaWhatsApp: string;
  loginLabel: string;
}

const COPY: Record<'en' | 'ar', HagarCopy> = {
  en: {
    brand: 'Hagar Pharmacy',
    tagline: 'صيدلية هاجر',
    headline: 'Your trusted pharmacy, now online',
    subtitle:
      'Browse our services, manage your prescriptions, and reach our team — all from one place built for Hagar Pharmacy.',
    ctaLogin: 'Staff Login',
    ctaContact: 'Contact Us',
    ctaWhatsApp: 'Chat on WhatsApp',
    loginLabel: 'Login'
  },
  ar: {
    brand: 'صيدلية هاجر',
    tagline: 'Hagar Pharmacy',
    headline: 'صيدليتك الموثوقة، الآن على الإنترنت',
    subtitle: 'تصفح خدماتنا، وتابع روشتاتك، وتواصل مع فريقنا — كل ذلك من مكان واحد مخصص لصيدلية هاجر.',
    ctaLogin: 'دخول الموظفين',
    ctaContact: 'تواصل معنا',
    ctaWhatsApp: 'تواصل عبر واتساب',
    loginLabel: 'تسجيل الدخول'
  }
};

@Component({
  selector: 'app-hagar-landing',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './hagar-landing.html',
  styleUrl: './hagar-landing.scss'
})
export class HagarLanding {
  readonly i18n = inject(LocalizationService);
  private readonly tenantPublic = inject(TenantPublicService);

  readonly logoUrl = '/tenants/hagar/storefront.png';
  readonly bannerUrl = '/tenants/hagar/banner.png';
  readonly whatsAppNumber = signal(FALLBACK_WHATSAPP);

  constructor() {
    this.tenantPublic.getPublicInfo(TENANT_CODE).subscribe({
      next: (res) => {
        if (res.success && res.data?.whatsApp) {
          this.whatsAppNumber.set(res.data.whatsApp);
        }
      },
      error: () => {
        // Keep the fallback WhatsApp number if the public-info lookup fails.
      }
    });
  }

  get copy(): HagarCopy {
    return COPY[this.i18n.language()];
  }

  get whatsAppLink(): string {
    return `https://wa.me/${this.whatsAppNumber()}`;
  }

  setLang(lang: 'en' | 'ar'): void {
    this.i18n.setLanguage(lang);
  }
}
