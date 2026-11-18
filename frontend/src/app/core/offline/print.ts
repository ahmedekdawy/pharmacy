import { LocalReceipt, unitLabel } from './models';

/** Print arbitrary HTML through a hidden iframe so the app layout is not affected. */
export function printHtml(html: string, opts: { title: string; dir: 'rtl' | 'ltr'; paperWidth?: string }): void {
  const iframe = document.createElement('iframe');
  iframe.style.position = 'fixed';
  iframe.style.width = '0';
  iframe.style.height = '0';
  iframe.style.border = '0';
  iframe.setAttribute('aria-hidden', 'true');
  document.body.appendChild(iframe);

  const width = opts.paperWidth ?? '80mm';
  const doc = iframe.contentDocument!;
  doc.open();
  doc.write(`<!doctype html><html dir="${opts.dir}"><head><meta charset="utf-8"><title>${escapeHtml(opts.title)}</title>
<style>
  @page { size: ${width === '80mm' ? '80mm auto' : 'A4'}; margin: 4mm; }
  * { box-sizing: border-box; }
  body { font-family: Tahoma, "Segoe UI", Arial, sans-serif; font-size: 12px; color: #000; margin: 0; width: ${width}; max-width: 100%; }
  h1 { font-size: 16px; margin: 0 0 4px; text-align: center; }
  h2 { font-size: 13px; margin: 10px 0 4px; }
  .center { text-align: center; }
  .muted { color: #444; font-size: 11px; }
  table { width: 100%; border-collapse: collapse; }
  th, td { padding: 3px 2px; text-align: start; vertical-align: top; }
  th { border-bottom: 1px dashed #000; font-size: 11px; }
  td.num, th.num { text-align: end; white-space: nowrap; }
  .totals td { padding: 2px; }
  .totals tr.grand td { font-weight: bold; font-size: 14px; border-top: 1px dashed #000; }
  hr { border: 0; border-top: 1px dashed #000; margin: 6px 0; }
  .grid { display: grid; grid-template-columns: repeat(2, 1fr); gap: 4px 12px; }
  .box { border: 1px solid #000; padding: 4px 6px; }
</style></head><body>${html}</body></html>`);
  doc.close();

  const win = iframe.contentWindow!;
  const cleanup = () => setTimeout(() => iframe.remove(), 500);
  win.onafterprint = cleanup;
  setTimeout(() => {
    win.focus();
    win.print();
    setTimeout(cleanup, 60_000);
  }, 150);
}

export function escapeHtml(value: unknown): string {
  return String(value ?? '')
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
}

export function money(n: number): string {
  return (Math.round((n + Number.EPSILON) * 100) / 100).toFixed(2);
}

export function receiptHtml(r: LocalReceipt, lang: 'ar' | 'en', pharmacyName: string): string {
  const L = (en: string, ar: string) => (lang === 'ar' ? ar : en);
  const method: Record<string, string> = {
    Cash: L('Cash', 'نقدي'),
    Card: L('Card', 'بطاقة'),
    Wallet: L('Wallet', 'محفظة')
  };
  const rows = r.lines
    .map(
      (l) => `<tr>
        <td>${escapeHtml(lang === 'ar' ? l.nameAr : l.nameEn)}<div class="muted">${l.quantity} ${unitLabel(l.unit, lang)} × ${money(l.unitPrice)}</div></td>
        <td class="num">${money(l.total)}</td>
      </tr>`
    )
    .join('');

  return `
    <h1>${escapeHtml(pharmacyName)}</h1>
    <div class="center muted">${escapeHtml(r.locationName)}</div>
    <hr>
    <div>${L('Invoice', 'فاتورة')}: <b>${escapeHtml(r.number)}</b></div>
    <div class="muted">${new Date(r.soldAt).toLocaleString(lang === 'ar' ? 'ar-EG' : 'en-GB')}</div>
    ${r.customerName ? `<div>${L('Customer', 'العميل')}: ${escapeHtml(r.customerName)}</div>` : ''}
    <hr>
    <table>
      <thead><tr><th>${L('Item', 'الصنف')}</th><th class="num">${L('Total', 'الإجمالي')}</th></tr></thead>
      <tbody>${rows}</tbody>
    </table>
    <hr>
    <table class="totals">
      <tr><td>${L('Subtotal', 'الإجمالي قبل الخصم')}</td><td class="num">${money(r.subTotal)}</td></tr>
      ${r.discount ? `<tr><td>${L('Discount', 'الخصم')}</td><td class="num">-${money(r.discount)}</td></tr>` : ''}
      <tr class="grand"><td>${L('Total', 'الصافي')}</td><td class="num">${money(r.total)}</td></tr>
      <tr><td>${L('Paid', 'المدفوع')} (${method[r.paymentMethod] ?? r.paymentMethod})</td><td class="num">${money(r.paid)}</td></tr>
      ${r.due > 0 ? `<tr><td><b>${L('On account', 'آجل (على الحساب)')}</b></td><td class="num"><b>${money(r.due)}</b></td></tr>` : ''}
      ${r.customerBalanceAfter != null && r.customerName ? `<tr><td>${L('Account balance', 'رصيد الحساب')}</td><td class="num">${money(r.customerBalanceAfter)}</td></tr>` : ''}
    </table>
    <hr>
    <div class="center muted">${r.offline && !r.synced ? L('Recorded offline – pending sync', 'سُجلت بدون إنترنت – في انتظار المزامنة') : ''}</div>
    <div class="center">${L('Get well soon', 'مع تمنياتنا بالشفاء العاجل')}</div>
  `;
}
