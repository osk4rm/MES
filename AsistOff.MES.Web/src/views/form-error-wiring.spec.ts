import { describe, expect, it } from 'vitest';

// Guards the shared form validation wiring (issue #325, slice 2/3): every
// in-scope create/edit form must show per-field errors through AppFormField,
// validate submit-time via the shared useFormErrors helper, announce invalid
// submits through the shared toast, move focus to the first error, and map
// server 400s onto fields. These tests scan the Vue sources so a future
// form (or a refactor that drops a binding) fails in CI instead of silently
// reverting to toast-only feedback.
//
// Raw Vue sources via Vite (no node:fs so vue-tsc stays happy without
// @types/node). Paths are relative to this file in src/views.
const viewSources = import.meta.glob<string>('./**/*.vue', {
  eager: true,
  query: '?raw',
  import: 'default'
});

// Slice 2/3 scope: the seven dictionary/order/recipe forms plus the
// shopfloor Confirmation form on the order detail view.
const inScopeForms = [
  './configuration/WarehousesView.vue',
  './configuration/OperatorsView.vue',
  './configuration/MachinesView.vue',
  './configuration/ReasonCodesView.vue',
  './production/RecipesView.vue',
  './production/ProductionOrdersView.vue',
  './production/ProductionOrderDetailView.vue',
  './production/SpcCharacteristicsView.vue'
];

function inScope(): Array<[string, string]> {
  return inScopeForms.map((file) => {
    const text = viewSources[file];
    expect(text, `${file} is part of the slice`).toBeDefined();
    return [file, text as string] as [string, string];
  });
}

describe('form error wiring (issue #325)', () => {
  it('covers every in-scope form', () => {
    expect(inScope()).toHaveLength(inScopeForms.length);
  });

  it('drives every in-scope form through the shared useFormErrors helper', () => {
    const offenders = inScope()
      .filter(([, text]) => !text.includes('useFormErrors'))
      .map(([file]) => file);
    expect(offenders).toEqual([]);
  });

  it('shows a per-field error next to the field on every in-scope form', () => {
    const offenders = inScope()
      .filter(([, text]) => !text.includes(':error=') || !text.includes('.fieldError('))
      .map(([file]) => file);
    expect(offenders).toEqual([]);
  });

  it('disables native bubbles so the shared errors render consistently', () => {
    const offenders = inScope()
      .filter(([, text]) => !text.includes('novalidate'))
      .map(([file]) => file);
    expect(offenders).toEqual([]);
  });

  it('announces invalid submits through the shared error presentation', () => {
    const offenders = inScope()
      .filter(([, text]) => !text.includes('validation.formHasErrors'))
      .map(([file]) => file);
    expect(offenders).toEqual([]);
  });

  it('focuses the first error on every invalid submit', () => {
    const offenders = inScope()
      .filter(([, text]) => !text.includes('focusFirstInvalidIn'))
      .map(([file]) => file);
    expect(offenders).toEqual([]);
  });

  it('maps server 400 validation errors onto fields on every in-scope form', () => {
    const offenders = inScope()
      .filter(([, text]) => !text.includes('applyServerErrors'))
      .map(([file]) => file);
    expect(offenders).toEqual([]);
  });

  it('styles every in-scope form through App components only (no raw checkboxes)', () => {
    const offenders = inScope()
      .filter(([, text]) => text.includes('<input type="checkbox"'))
      .map(([file]) => file);
    expect(offenders).toEqual([]);
  });
});
