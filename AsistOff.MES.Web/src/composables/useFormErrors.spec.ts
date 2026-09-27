import { describe, expect, it } from 'vitest';
import {
  extractFieldErrors,
  focusFirstInvalid,
  normalizeFieldKey,
  useFormErrors
} from './useFormErrors';

// Covers the shared form-error helper (issue #325, slice 2/3): touched vs
// submit-time visibility, server 400 mapping onto fields, and
// focus-first-invalid so no submit leaves the operator without feedback.
describe('normalizeFieldKey', () => {
  it('lowercases the first letter of PascalCase backend keys', () => {
    expect(normalizeFieldKey('Code')).toBe('code');
    expect(normalizeFieldKey('PlannedQuantity')).toBe('plannedQuantity');
    expect(normalizeFieldKey('code')).toBe('code');
  });

  it('strips JSON-path prefixes and collection indices', () => {
    expect(normalizeFieldKey('$.code')).toBe('code');
    expect(normalizeFieldKey('entries[0].code')).toBe('code');
    expect(normalizeFieldKey('ConsumedLots[2].Quantity')).toBe('quantity');
  });

  it('returns empty for blank keys', () => {
    expect(normalizeFieldKey('  ')).toBe('');
  });
});

describe('extractFieldErrors', () => {
  it('reads the ValidationProblemDetails errors dictionary', () => {
    const err = {
      response: {
        data: {
          errors: {
            Code: ['Code is required.'],
            PlannedQuantity: ['Must be positive.', 'Second message.']
          }
        }
      }
    };

    expect(extractFieldErrors(err)).toEqual({
      code: ['Code is required.'],
      plannedQuantity: ['Must be positive.', 'Second message.']
    });
  });

  it('tolerates plain string values', () => {
    const err = { response: { data: { errors: { Name: 'Too short' } } } };

    expect(extractFieldErrors(err)).toEqual({ name: ['Too short'] });
  });

  it('returns empty when there is no per-field dictionary', () => {
    expect(extractFieldErrors(null)).toEqual({});
    expect(extractFieldErrors(new Error('boom'))).toEqual({});
    expect(extractFieldErrors({ response: { data: { title: 'Bad' } } })).toEqual({});
    expect(extractFieldErrors({ response: { data: null } })).toEqual({});
  });
});

describe('useFormErrors visibility', () => {
  it('hides client errors until the field is touched or the form is submitted', () => {
    const form = useFormErrors();
    form.setErrors({ code: 'Required' });

    expect(form.fieldError('code')).toBeNull();

    form.touch('code');
    expect(form.fieldError('code')).toBe('Required');
  });

  it('reveals every error on submit and reports validity', () => {
    const form = useFormErrors();

    const valid = form.submitWith({ code: 'Required', name: null });

    expect(valid).toBe(false);
    expect(form.submitted.value).toBe(true);
    expect(form.fieldError('code')).toBe('Required');
    expect(form.fieldError('name')).toBeNull();
    expect(form.hasErrors()).toBe(true);
  });

  it('returns true for a clean map', () => {
    const form = useFormErrors();

    expect(form.submitWith({ code: null, name: undefined })).toBe(true);
    expect(form.hasErrors()).toBe(false);
  });

  it('clears a single field and resets the whole form', () => {
    const form = useFormErrors();
    form.submitWith({ code: 'Required', name: 'Required' });

    form.clearField('code');
    expect(form.fieldError('code')).toBeNull();
    expect(form.fieldError('name')).toBe('Required');

    form.reset();
    expect(form.fieldError('name')).toBeNull();
    expect(form.submitted.value).toBe(false);
  });
});

describe('useFormErrors server mapping', () => {
  it('maps PascalCase 400 keys onto camelCase fields and submits the form', () => {
    const form = useFormErrors();
    const err = {
      response: { data: { errors: { Code: ['Taken.'], PlannedQuantity: ['Must be positive.'] } } }
    };

    const mapped = form.applyServerErrors(err);

    expect(mapped).toBe(true);
    expect(form.fieldError('code')).toBe('Taken.');
    expect(form.fieldError('plannedQuantity')).toBe('Must be positive.');
  });

  it('honours explicit aliases for keys normalization misses', () => {
    const form = useFormErrors({ aliases: { operatorId: 'operator' } });
    const err = { response: { data: { errors: { OperatorId: ['Required.'] } } } };

    expect(form.applyServerErrors(err)).toBe(true);
    expect(form.fieldError('operator')).toBe('Required.');
  });

  it('returns false when the failure carries no field dictionary', () => {
    const form = useFormErrors();

    expect(form.applyServerErrors(new Error('timeout'))).toBe(false);
    expect(form.submitted.value).toBe(false);
  });
});

describe('focusFirstInvalid', () => {
  function container(html: string): HTMLDivElement {
    const root = document.createElement('div');
    root.innerHTML = html;
    document.body.appendChild(root);
    return root;
  }

  it('focuses the first control inside an invalid field and returns its id', () => {
    const root = container(
      '<form>' +
        '<div class="app-field"><input id="ok" /></div>' +
        '<div class="app-field app-field--invalid"><input id="first-bad" /></div>' +
        '<div class="app-field app-field--invalid"><select id="second-bad"></select></div>' +
        '</form>'
    );

    try {
      const focused = focusFirstInvalid(root);

      expect(focused).toBe('first-bad');
      expect(document.activeElement?.id).toBe('first-bad');
    } finally {
      root.remove();
    }
  });

  it('returns null when nothing is invalid and never throws on empty roots', () => {
    const root = container('<form><div class="app-field"><input id="ok" /></div></form>');

    try {
      expect(focusFirstInvalid(root)).toBeNull();
    } finally {
      root.remove();
    }
    expect(focusFirstInvalid(null)).toBeNull();
  });
});
