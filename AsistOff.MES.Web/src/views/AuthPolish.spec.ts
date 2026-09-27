import { describe, expect, it } from 'vitest';

// Slice (3/3) F-09: auth side panels re-theme with design tokens and use
// design-system link buttons; the public-layout exemption (no AppPageHeader
// on /login and /register) stays documented.

describe('auth polish (F-09)', () => {
  it('side-panel gradients reference theme tokens, not hardcoded hex', () => {
    for (const file of ['./LoginView.vue', './RegisterView.vue']) {
      const text = authSources[file] ?? '';
      expect(text.length).toBeGreaterThan(0);
      expect(text).toContain('var(--color-primary)');
      expect(text).not.toContain('#1e3a5f');
      expect(text).not.toContain('#0f1f30');
    }
  });

  it('auth navigation affordances use the AppButton link variant', () => {
    const login = authSources['./LoginView.vue'] ?? '';
    const register = authSources['./RegisterView.vue'] ?? '';
    expect(login).toContain('variant="link"');
    expect(register).toContain('variant="link"');
    // No raw link buttons remain outside the design system.
    expect(login).not.toContain('auth-page__link');
    expect(register).not.toContain('auth-page__link');

    const button = uiSources['../components/ui/AppButton.vue'] ?? '';
    expect(button).toContain("'link'");
    expect(button).toContain('.app-btn--link');
  });

  it('keeps the public-layout exemption documented', () => {
    const login = authSources['./LoginView.vue'] ?? '';
    // No authenticated-shell header by design on public routes (see F-09).
    expect(login).not.toContain('<AppPageHeader');
    expect(login).not.toContain('AppPageHeader.vue');
  });
});

const authSources = import.meta.glob<string>(['./LoginView.vue', './RegisterView.vue'], {
  eager: true,
  query: '?raw',
  import: 'default'
});

const uiSources = import.meta.glob<string>('../components/ui/AppButton.vue', {
  eager: true,
  query: '?raw',
  import: 'default'
});
