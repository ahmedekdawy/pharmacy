import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { LocalizationService } from '../services/localization.service';

/** Tells the API which language to answer in, so backend error/validation messages match the UI. */
export const languageInterceptor: HttpInterceptorFn = (req, next) => {
  const lang = inject(LocalizationService).language();
  return next(req.clone({ setHeaders: { 'Accept-Language': lang } }));
};
