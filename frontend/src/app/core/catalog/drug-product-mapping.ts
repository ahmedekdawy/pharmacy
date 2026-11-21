import { EgyptianDrug } from '../services/api.service';

/**
 * Code from the whole commercial name (pack size included) so sizes of the same drug get different codes:
 * "Panadol Advance 500mg 24 tab" → EG-PANADOL-ADVANCE-500MG-24-TAB. Backend limit is 64 chars.
 */
export function buildProductCode(drug: EgyptianDrug): string {
  const source = (drug.commercialNameEn || drug.scientificName || 'DRUG').toUpperCase();
  const slug = source.replace(/[^A-Z0-9]+/g, '-').replace(/^-+|-+$/g, '').slice(0, 56).replace(/-+$/, '') || 'DRUG';
  return `EG-${slug}`;
}

/** Appends -2, -3… when the code is already taken. */
export function uniqueCode(code: string, existing: string[]): string {
  const taken = new Set(existing.map((c) => c.toUpperCase()));
  if (!taken.has(code.toUpperCase())) return code;
  for (let i = 2; ; i++) {
    const candidate = `${code}-${i}`;
    if (!taken.has(candidate.toUpperCase())) return candidate;
  }
}

/**
 * Reads the pack size from the drug name ("24 f.c. tabs", "30 caps", "علبة 20 قرص").
 * Blister strips are usually 10 units, so 20/30 → 2/3 strips of 10; other counts stay as one strip.
 */
export function guessPack(drug: EgyptianDrug): { total: number; strips: number; perStrip: number } | null {
  const text = `${drug.commercialNameEn} ${drug.commercialNameAr}`;
  const match =
    /(\d{1,4})\s*(?:f\.?\s*c\.?\s*)?(?:tabs?|tablets?|caps?|capsules?|cap\.|sachets?|supp)\b/i.exec(text) ??
    /(\d{1,4})\s*(?:قرص|أقراص|اقراص|كبسول|كبسولة|كيس|لبوس)/.exec(text);
  const total = match ? Number(match[1]) : 0;
  if (!total || total > 1000) return null;
  const strips = total > 10 && total % 10 === 0 ? total / 10 : 1;
  return { total, strips, perStrip: total / strips };
}
