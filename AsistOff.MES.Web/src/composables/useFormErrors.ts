import { ref } from 'vue';

/**
 * Shared form-error state (issue #325, slice 2/3).
 *
 * Every create/edit form owns one `useFormErrors()` instance that unifies
 * three error sources behind the per-field display of `AppFormField`:
 *
 * - client rules: the view builds a `{ field: message | null }` map and
 *   applies it via `submitWith()` (submit-time) — messages stay hidden until
 *   the field is touched (`touch()` on blur) or the form is submitted;
 * - server 400s: `applyServerErrors()` maps an ASP.NET Core
 *   `ValidationProblemDetails.errors` dictionary onto form fields
 *   (PascalCase backend keys are normalized to the camelCase field names);
 * - submit feedback: on an invalid submit the caller toasts
 *   `validation.formHasErrors` and calls `focusFirstInvalidIn()` so focus
 *   (and scroll) lands on the first invalid control, which `AppFormField`
 *   already announces via `role="alert"`.
 */
export type ClientErrorMap = Record<string, string | null | undefined>;

/** Normalized server key -> form field name (for keys normalization misses). */
export type FieldAliases = Record<string, string>;

/**
 * Normalizes one backend error key to a form field name:
 * `Code` -> `code`, `PlannedQuantity` -> `plannedQuantity`,
 * `$.code` -> `code`, `entries[0].code` -> `code`.
 */
export function normalizeFieldKey(raw: string): string {
  let key = raw.trim();
  if (key.startsWith('$.')) key = key.slice(2);
  const segments = key.split('.');
  key = segments[segments.length - 1] ?? key;
  key = key.replace(/\[[^\]]*\]/g, '').trim();
  if (!key) return '';
  return key.charAt(0).toLowerCase() + key.slice(1);
}

interface AxiosLikeError {
  response?: {
    data?: {
      errors?: Record<string, string[] | string> | null;
    } | null;
  } | null;
}

/**
 * Extracts the per-field dictionary from a failed request. Understands the
 * `ValidationProblemDetails` envelope (`response.data.errors`) and tolerates
 * both `string[]` and plain `string` values. Returns normalized field names.
 */
export function extractFieldErrors(err: unknown): Record<string, string[]> {
  const out: Record<string, string[]> = {};
  if (!err || typeof err !== 'object') return out;
  const data = (err as AxiosLikeError).response?.data;
  const raw = data?.errors;
  if (!raw || typeof raw !== 'object') return out;
  for (const [key, value] of Object.entries(raw)) {
    const field = normalizeFieldKey(key);
    if (!field) continue;
    const messages = Array.isArray(value) ? value : [value];
    const texts = messages
      .filter((m): m is string => typeof m === 'string' && m.trim() !== '')
      .map((m) => String(m));
    if (texts.length > 0) out[field] = texts;
  }
  return out;
}

type FocusRoot = Pick<ParentNode, 'querySelector'>;

/**
 * Moves focus (and scroll) to the first control inside an invalid
 * `AppFormField` (`.app-field--invalid`). Returns the focused element id,
 * or null when there is nothing invalid to focus. Never throws — safe to
 * call from a submit handler even when the form is not mounted.
 */
export function focusFirstInvalid(root?: FocusRoot | null): string | null {
  const scope: FocusRoot | null | undefined =
    root ?? (typeof document !== 'undefined' ? document : null);
  if (!scope) return null;
  const target = scope.querySelector(
    '.app-field--invalid input, .app-field--invalid select, .app-field--invalid textarea'
  ) as (HTMLElement & { focus?: () => void; scrollIntoView?: (arg?: unknown) => void }) | null;
  if (!target) return null;
  try {
    if (typeof target.scrollIntoView === 'function') {
      target.scrollIntoView({ block: 'nearest', behavior: 'smooth' });
    }
  } catch {
    /* ignore - focus matters, scrolling is best-effort */
  }
  try {
    target.focus?.();
  } catch {
    /* ignore - a missing focus must never break submit */
  }
  return target.id || null;
}

export interface FormErrorsOptions {
  /** Extra server-key -> field overrides merged over per-call aliases. */
  aliases?: FieldAliases;
}

export function useFormErrors(options: FormErrorsOptions = {}): {
  errors: ReturnType<typeof ref<Record<string, string>>>;
  submitted: ReturnType<typeof ref<boolean>>;
  fieldError: (field: string) => string | null;
  setErrors: (map: ClientErrorMap) => boolean;
  touch: (field: string) => void;
  clearField: (field: string) => void;
  markSubmitted: () => void;
  submitWith: (map: ClientErrorMap) => boolean;
  applyServerErrors: (err: unknown, extraAliases?: FieldAliases) => boolean;
  hasErrors: () => boolean;
  reset: () => void;
  focusFirstInvalidIn: (root?: FocusRoot | null) => string | null;
} {
  const errors = ref<Record<string, string>>({});
  const touched = ref<Record<string, boolean>>({});
  const submitted = ref(false);

  /** Visible message for a field: only after touch or submit (slice 2/3). */
  function fieldError(field: string): string | null {
    const message = errors.value[field];
    if (!message) return null;
    if (submitted.value || touched.value[field]) return message;
    return null;
  }

  /** Replaces client errors; returns true when the map holds no messages. */
  function setErrors(map: ClientErrorMap): boolean {
    const next: Record<string, string> = {};
    for (const [field, message] of Object.entries(map)) {
      if (typeof message === 'string' && message.trim() !== '') next[field] = message;
    }
    errors.value = next;
    return Object.keys(next).length === 0;
  }

  function touch(field: string): void {
    touched.value[field] = true;
  }

  function clearField(field: string): void {
    delete errors.value[field];
  }

  function markSubmitted(): void {
    submitted.value = true;
  }

  /** Submit-time validation: marks submitted, applies the client map. */
  function submitWith(map: ClientErrorMap): boolean {
    submitted.value = true;
    return setErrors(map);
  }

  /**
   * Maps a server 400 onto fields and marks the form submitted so the
   * messages show immediately. Returns false when the error carries no
   * per-field dictionary (caller falls back to a generic toast).
   */
  function applyServerErrors(err: unknown, extraAliases: FieldAliases = {}): boolean {
    const raw = extractFieldErrors(err);
    const aliases: FieldAliases = { ...options.aliases, ...extraAliases };
    const next: Record<string, string> = {};
    for (const [field, messages] of Object.entries(raw)) {
      const mapped = aliases[field] ?? field;
      const first = messages[0];
      if (first) next[mapped] = first;
    }
    if (Object.keys(next).length === 0) return false;
    errors.value = { ...errors.value, ...next };
    submitted.value = true;
    return true;
  }

  function hasErrors(): boolean {
    return Object.keys(errors.value).length > 0;
  }

  function reset(): void {
    errors.value = {};
    touched.value = {};
    submitted.value = false;
  }

  function focusFirstInvalidIn(root?: FocusRoot | null): string | null {
    return focusFirstInvalid(root);
  }

  return {
    errors,
    submitted,
    fieldError,
    setErrors,
    touch,
    clearField,
    markSubmitted,
    submitWith,
    applyServerErrors,
    hasErrors,
    reset,
    focusFirstInvalidIn
  };
}

export type FormErrors = ReturnType<typeof useFormErrors>;
