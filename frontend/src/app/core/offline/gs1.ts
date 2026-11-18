/**
 * Client-side GS1 parser (same rules as the server) so scanning works offline.
 * EDA track & trace packs carry a GS1 DataMatrix: (01) GTIN, (17) expiry YYMMDD, (10) batch, (21) serial.
 */
export interface Gs1Data {
  gtin: string | null;
  expiry: string | null; // yyyy-MM-dd
  batch: string | null;
  serial: string | null;
}

const GS = '\u001d';

export function parseGs1(raw: string | null | undefined): Gs1Data {
  const empty: Gs1Data = { gtin: null, expiry: null, batch: null, serial: null };
  if (!raw) return empty;
  let code = stripSymbology(raw.trim());

  if (code.includes('(')) {
    const result = { ...empty };
    for (const m of code.matchAll(/\((\d{2,4})\)([^(]*)/g)) {
      const value = m[2].trim();
      if (m[1] === '01') result.gtin = value;
      if (m[1] === '17') result.expiry = toDate(value);
      if (m[1] === '10') result.batch = value;
      if (m[1] === '21') result.serial = value;
    }
    return result;
  }

  if (!/^01\d{14}/.test(code)) return empty;

  const result = { ...empty };
  let i = 0;
  const takeVar = (max: number) => {
    let end = code.indexOf(GS, i);
    if (end < 0) end = code.length;
    end = Math.min(end, i + max);
    const v = code.slice(i, end);
    i = end;
    return v;
  };
  while (i < code.length) {
    if (code[i] === GS) {
      i++;
      continue;
    }
    const ai = code.slice(i, i + 2);
    i += 2;
    if (ai === '01') {
      result.gtin = code.slice(i, i + 14);
      i += 14;
    } else if (ai === '17' || ai === '15' || ai === '11') {
      const d = code.slice(i, i + 6);
      i += 6;
      if (ai === '17') result.expiry = toDate(d);
    } else if (ai === '10') {
      result.batch = takeVar(20);
    } else if (ai === '21') {
      result.serial = takeVar(20);
    } else {
      takeVar(90);
    }
  }
  code = '';
  return result;
}

/** The barcode as printed on the box: GTIN-14 with a leading 0 becomes EAN-13. */
export function normalizeBarcode(raw: string | null | undefined): string {
  if (!raw) return '';
  const gs1 = parseGs1(raw);
  let value = (gs1.gtin ?? stripSymbology(raw.trim())).split(GS).join('').trim();
  if (/^0\d{13}$/.test(value)) value = value.slice(1);
  return value;
}

export function barcodeCandidates(raw: string): string[] {
  const n = normalizeBarcode(raw);
  const list = [n];
  if (/^\d{13}$/.test(n)) list.push('0' + n);
  return list.filter(Boolean);
}

function stripSymbology(code: string): string {
  return code.length > 3 && code[0] === ']' ? code.slice(3) : code;
}

function toDate(yymmdd: string): string | null {
  if (!/^\d{6}$/.test(yymmdd)) return null;
  const year = 2000 + Number(yymmdd.slice(0, 2));
  const month = Number(yymmdd.slice(2, 4));
  let day = Number(yymmdd.slice(4, 6));
  if (month < 1 || month > 12) return null;
  const last = new Date(year, month, 0).getDate();
  if (day === 0 || day > last) day = last;
  return `${year}-${String(month).padStart(2, '0')}-${String(day).padStart(2, '0')}`;
}
