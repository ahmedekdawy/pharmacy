export enum SaleUnit {
  Box = 1,
  Strip = 2,
  Unit = 3
}

export type PaymentMethod = 'Cash' | 'Card' | 'Wallet';

export interface PosProduct {
  id: string;
  code: string;
  barcode?: string | null;
  nameEn: string;
  nameAr: string;
  sellingPrice: number;
  stripsPerBox: number;
  unitsPerStrip: number;
  allowStripSale: boolean;
  allowUnitSale: boolean;
  stripPrice?: number | null;
  unitPrice?: number | null;
  /** Stock in smallest units (pills). */
  stock: number;
}

export interface PosCustomer {
  id: string;
  code: string;
  nameEn: string;
  nameAr: string;
  phone?: string | null;
  address?: string | null;
  creditLimit: number;
  balance: number;
  isActive: boolean;
}

export interface PosLocation {
  id: string;
  nameEn: string;
  nameAr: string;
}

export interface SyncSnapshot {
  serverTime: string;
  products: PosProduct[];
  customers: PosCustomer[];
  locations: PosLocation[];
}

export type OutboxType =
  | 'sale.create'
  | 'customer.create'
  | 'customerPayment.create'
  | 'product.create'
  | 'openingBalance.create';

export interface OutboxOp {
  opId: string;
  type: OutboxType;
  payload: Record<string, unknown>;
  createdAt: string;
  status: 'pending' | 'failed';
  error?: string | null;
  attempts: number;
  /** Human readable description for the sync panel. */
  label: string;
}

export interface SyncOpResult {
  opId: string;
  status: 'applied' | 'duplicate' | 'failed';
  entityId?: string | null;
  number?: string | null;
  warnings: string[];
  error?: string | null;
}

export interface ReceiptLine {
  productId: string;
  nameEn: string;
  nameAr: string;
  unit: SaleUnit;
  quantity: number;
  unitPrice: number;
  total: number;
}

export interface LocalReceipt {
  id: string;
  number: string;
  soldAt: string;
  locationId: string;
  locationName: string;
  customerId?: string | null;
  customerName?: string | null;
  customerBalanceAfter?: number | null;
  lines: ReceiptLine[];
  subTotal: number;
  discount: number;
  total: number;
  paid: number;
  due: number;
  paymentMethod: PaymentMethod;
  cashier?: string | null;
  synced: boolean;
  offline: boolean;
  warnings?: string[];
}

export function unitsPerBox(p: Pick<PosProduct, 'stripsPerBox' | 'unitsPerStrip'>): number {
  return Math.max(1, p.stripsPerBox || 1) * Math.max(1, p.unitsPerStrip || 1);
}

export function unitFactor(p: Pick<PosProduct, 'stripsPerBox' | 'unitsPerStrip'>, unit: SaleUnit): number {
  switch (unit) {
    case SaleUnit.Box:
      return unitsPerBox(p);
    case SaleUnit.Strip:
      return Math.max(1, p.unitsPerStrip || 1);
    default:
      return 1;
  }
}

export function unitPrice(p: PosProduct, unit: SaleUnit): number {
  switch (unit) {
    case SaleUnit.Box:
      return p.sellingPrice;
    case SaleUnit.Strip:
      return p.stripPrice ?? round2(p.sellingPrice / Math.max(1, p.stripsPerBox || 1));
    default:
      return p.unitPrice ?? round2(p.sellingPrice / unitsPerBox(p));
  }
}

export function allowedUnits(p: PosProduct): SaleUnit[] {
  const units = [SaleUnit.Box];
  if (p.allowStripSale && (p.stripsPerBox || 1) > 1) units.push(SaleUnit.Strip);
  if (p.allowUnitSale && unitsPerBox(p) > 1) units.push(SaleUnit.Unit);
  return units;
}

export function unitLabel(unit: SaleUnit, lang: 'ar' | 'en'): string {
  const ar = { [SaleUnit.Box]: 'علبة', [SaleUnit.Strip]: 'شريط', [SaleUnit.Unit]: 'حبة' };
  const en = { [SaleUnit.Box]: 'Box', [SaleUnit.Strip]: 'Strip', [SaleUnit.Unit]: 'Pill' };
  return (lang === 'ar' ? ar : en)[unit];
}

/** "2 علبة + 1 شريط + 4 حبة" from a stock expressed in smallest units. */
export function formatStock(p: PosProduct, lang: 'ar' | 'en'): string {
  const perBox = unitsPerBox(p);
  const perStrip = Math.max(1, p.unitsPerStrip || 1);
  let rest = Math.round(p.stock * 100) / 100;
  if (rest <= 0) return lang === 'ar' ? 'نفد' : 'Out';
  const boxes = Math.floor(rest / perBox);
  rest -= boxes * perBox;
  const strips = perStrip > 1 || (p.stripsPerBox || 1) > 1 ? Math.floor(rest / perStrip) : 0;
  rest = Math.round((rest - strips * perStrip) * 100) / 100;
  const parts: string[] = [];
  if (boxes) parts.push(`${boxes} ${unitLabel(SaleUnit.Box, lang)}`);
  if (strips) parts.push(`${strips} ${unitLabel(SaleUnit.Strip, lang)}`);
  if (rest) parts.push(`${rest} ${unitLabel(SaleUnit.Unit, lang)}`);
  return parts.join(' + ');
}

export function round2(n: number): number {
  return Math.round((n + Number.EPSILON) * 100) / 100;
}

export function newId(): string {
  if (typeof crypto !== 'undefined' && 'randomUUID' in crypto) {
    return crypto.randomUUID();
  }
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
    const r = (Math.random() * 16) | 0;
    return (c === 'x' ? r : (r & 0x3) | 0x8).toString(16);
  });
}
