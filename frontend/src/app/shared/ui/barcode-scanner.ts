import { Component, ElementRef, OnDestroy, OnInit, inject, output, signal, viewChild } from '@angular/core';
import { LocalizationService } from '../../core/services/localization.service';

interface DetectedBarcode { rawValue: string; format: string; }
interface BarcodeDetectorLike { detect(source: CanvasImageSource): Promise<DetectedBarcode[]>; }
type BarcodeDetectorCtor = new (opts?: { formats: string[] }) => BarcodeDetectorLike;

const FORMATS = ['ean_13', 'ean_8', 'upc_a', 'upc_e', 'code_128', 'code_39', 'data_matrix', 'qr_code'];

/**
 * Camera barcode scanner (phone/tablet) based on the browser BarcodeDetector API
 * (Chrome / Edge on Android and desktop). Reads EAN-13 and the GS1 DataMatrix printed on medicine packs.
 * USB / Bluetooth scanners don't need this: they type into the focused input and press Enter.
 */
@Component({
  selector: 'app-barcode-scanner',
  standalone: true,
  template: `
    <div class="overlay" role="dialog" aria-modal="true">
      <div class="box">
        @if (error()) {
          <p class="msg">{{ error() }}</p>
        } @else {
          <video #video playsinline muted></video>
          <div class="frame" aria-hidden="true"></div>
          <p class="msg">{{ i18n.pick('Point the camera at the barcode', 'وجّه الكاميرا نحو الباركود') }}</p>
        }
        <button type="button" (click)="closed.emit()">{{ i18n.pick('Close', 'إغلاق') }}</button>
      </div>
    </div>
  `,
  styles: [`
    .overlay { position:fixed; inset:0; z-index:60; background:rgba(0,0,0,.8); display:grid; place-items:center; padding:1rem; }
    .box { position:relative; width:min(32rem,100%); display:grid; gap:.75rem; justify-items:center; color:#fff; }
    video { width:100%; border-radius:.75rem; background:#000; max-height:70vh; object-fit:cover; }
    .frame { position:absolute; top:22%; left:12%; right:12%; height:32%; border:3px solid #48b984; border-radius:.5rem; pointer-events:none; }
    .msg { margin:0; text-align:center; }
    button { border:0; border-radius:.5rem; padding:.7rem 1.4rem; font:inherit; background:#fff; color:#000; cursor:pointer; }
  `]
})
export class BarcodeScanner implements OnInit, OnDestroy {
  readonly i18n = inject(LocalizationService);
  readonly scanned = output<string>();
  readonly closed = output<void>();
  readonly error = signal<string | null>(null);
  private readonly video = viewChild<ElementRef<HTMLVideoElement>>('video');
  private stream: MediaStream | null = null;
  private timer: ReturnType<typeof setTimeout> | null = null;
  private stopped = false;

  static isSupported(): boolean {
    return typeof window !== 'undefined' && 'BarcodeDetector' in window && !!navigator.mediaDevices?.getUserMedia;
  }

  async ngOnInit(): Promise<void> {
    const Ctor = (window as unknown as { BarcodeDetector?: BarcodeDetectorCtor }).BarcodeDetector;
    if (!Ctor || !navigator.mediaDevices?.getUserMedia) {
      this.error.set(
        this.i18n.pick(
          'This browser cannot scan with the camera. Use Chrome on Android, or a USB/Bluetooth barcode scanner.',
          'هذا المتصفح لا يدعم المسح بالكاميرا. استخدم Chrome على أندرويد أو قارئ باركود USB/Bluetooth.'
        )
      );
      return;
    }

    try {
      this.stream = await navigator.mediaDevices.getUserMedia({
        video: { facingMode: { ideal: 'environment' }, width: { ideal: 1280 }, height: { ideal: 720 } },
        audio: false
      });
    } catch {
      this.error.set(this.i18n.pick('Camera permission was denied.', 'تم رفض إذن الكاميرا.'));
      return;
    }

    if (this.stopped) {
      this.stop();
      return;
    }

    // wait for the <video> element to render
    await Promise.resolve();
    const video = this.video()?.nativeElement;
    if (!video) return;
    video.srcObject = this.stream;
    await video.play().catch(() => undefined);

    let supported = FORMATS;
    try {
      const getSupported = (Ctor as unknown as { getSupportedFormats?: () => Promise<string[]> }).getSupportedFormats;
      if (getSupported) supported = (await getSupported()).filter((f) => FORMATS.includes(f));
    } catch {
      /* use defaults */
    }
    const detector = new Ctor({ formats: supported.length ? supported : FORMATS });

    const tick = async () => {
      if (this.stopped) return;
      try {
        if (video.readyState >= 2) {
          const codes = await detector.detect(video);
          if (codes.length && codes[0].rawValue) {
            navigator.vibrate?.(80);
            this.stop();
            this.scanned.emit(codes[0].rawValue);
            return;
          }
        }
      } catch {
        /* keep trying */
      }
      this.timer = setTimeout(tick, 200);
    };
    void tick();
  }

  ngOnDestroy(): void {
    this.stop();
  }

  private stop(): void {
    this.stopped = true;
    if (this.timer) clearTimeout(this.timer);
    this.stream?.getTracks().forEach((t) => t.stop());
    this.stream = null;
  }
}
