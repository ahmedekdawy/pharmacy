import { HttpErrorResponse } from '@angular/common/http';

/**
 * Extracts a user-facing message from an API failure.
 * Reads the backend `ApiResponse.errors` list (camel or Pascal case), plain-text bodies,
 * and falls back to a friendly message instead of Angular's "Http failure response…" text.
 */
export function apiErrorMessage(err: unknown, fallback: string): string {
  if (err instanceof HttpErrorResponse) {
    if (err.status === 0) {
      return 'Unable to reach the server. Check your connection and try again.';
    }
    const body = err.error;
    if (typeof body === 'string' && body.trim()) {
      return body.trim();
    }
    const errors: unknown = body?.errors ?? body?.Errors;
    if (Array.isArray(errors) && errors.length) {
      return errors.filter((e) => typeof e === 'string' && e.trim()).join('\n') || fallback;
    }
    // ASP.NET ProblemDetails (model-binding validation) shape.
    if (errors && typeof errors === 'object') {
      const messages = Object.values(errors as Record<string, unknown>).flat().filter((e) => typeof e === 'string');
      if (messages.length) {
        return messages.join('\n');
      }
    }
    if (typeof body?.title === 'string') {
      return body.title;
    }
    return fallback;
  }
  if (err instanceof Error && err.message) {
    return err.message;
  }
  return fallback;
}
