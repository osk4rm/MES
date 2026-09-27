import { beforeEach, describe, expect, it, vi } from 'vitest';
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { createPinia, setActivePinia } from 'pinia';
import RecipeDetailView from './RecipeDetailView.vue';
import { recipeService, RecipeVersionStatus, type RecipeResponse } from '../../services/recipeService';
import { recipeVersionService, type RecipeVersionDetailResponse } from '../../services/recipeVersionService';

// Slice (2/3) F-04: version selection renders design-system AppButton chips
// with AppBadge status signals (token-themed, icon + text) instead of raw
// <button> pills with hardcoded hex that skipped the dark theme.

vi.mock('../../services/recipeService', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../services/recipeService')>();
  return {
    ...actual,
    recipeService: {
      get: vi.fn(),
      remove: vi.fn(),
      update: vi.fn()
    }
  };
});

vi.mock('../../services/recipeVersionService', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../services/recipeVersionService')>();
  return {
    ...actual,
    recipeVersionService: {
      get: vi.fn(),
      create: vi.fn(),
      clone: vi.fn()
    }
  };
});

const mockPush = vi.fn();

vi.mock('vue-router', () => ({
  useRoute: (): { params: Record<string, string> } => ({ params: { id: 'recipe-1' } }),
  useRouter: (): { push: (...args: unknown[]) => void } => ({ push: mockPush })
}));

vi.mock('vue-i18n', () => ({
  useI18n: (): { t: (key: string) => string } => ({
    t: (key: string): string => key
  })
}));

const recipeGetMock = vi.mocked(recipeService.get);
const versionGetMock = vi.mocked(recipeVersionService.get);

function recipeFixture(): RecipeResponse {
  return {
    id: 'recipe-1',
    code: 'RX-1',
    name: 'Recipe 1',
    description: null,
    isActive: true,
    primaryProductId: null,
    currentVersionId: 'version-2',
    versions: [
      { id: 'version-1', versionNumber: 1, status: RecipeVersionStatus.Obsolete },
      { id: 'version-2', versionNumber: 2, status: RecipeVersionStatus.Released }
    ]
  };
}

function versionDetail(id: string, versionNumber: number): RecipeVersionDetailResponse {
  return {
    id,
    recipeId: 'recipe-1',
    versionNumber,
    status: RecipeVersionStatus.Released,
    operations: []
  };
}

function mountDetail(): VueWrapper {
  return mount(RecipeDetailView, {
    global: {
      plugins: [createPinia()],
      mocks: { $t: (key: string): string => key },
      stubs: { RecipeVersionEditor: { template: '<div class="version-editor-stub" />' } }
    }
  }) as unknown as VueWrapper;
}

describe('RecipeDetailView', () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
    recipeGetMock.mockResolvedValue(recipeFixture());
    versionGetMock.mockImplementation(async (id: string) => versionDetail(id, id === 'version-2' ? 2 : 1));
  });

  it('renders version chips as design-system buttons with badge signals, never raw pills', async () => {
    const wrapper = mountDetail();
    await flushPromises();

    const chips = wrapper.findAll('.versions .app-btn');
    expect(chips).toHaveLength(2);
    expect(wrapper.find('.versions button.version-chip').exists()).toBe(false);
    expect(wrapper.find('.versions .pill--ok').exists()).toBe(false);
    expect(wrapper.find('.versions .pill--info').exists()).toBe(false);
    expect(wrapper.find('.versions .pill--muted').exists()).toBe(false);
    // Status signals ride on token-themed badges with text labels.
    expect(wrapper.findAll('.versions .app-badge')).toHaveLength(2);
    expect(wrapper.text()).toContain('recipes.versionStatus.released');
    expect(wrapper.text()).toContain('recipes.versionStatus.obsolete');
  });

  it('carries no inline hex styling on the version chips', async () => {
    const wrapper = mountDetail();
    await flushPromises();

    for (const chip of wrapper.findAll('.versions .app-btn')) {
      expect(chip.attributes('style') ?? '').not.toMatch(/#[0-9a-fA-F]{3,8}/);
    }
  });

  it('switches the loaded version when a chip is clicked', async () => {
    const wrapper = mountDetail();
    await flushPromises();
    expect(versionGetMock).toHaveBeenCalledWith('version-2');

    const chips = wrapper.findAll('.versions .app-btn');
    await chips[0]?.trigger('click');
    await flushPromises();

    expect(versionGetMock).toHaveBeenCalledWith('version-1');
  });
});
