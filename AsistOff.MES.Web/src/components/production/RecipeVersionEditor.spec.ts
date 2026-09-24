import { beforeEach, describe, expect, it, vi } from 'vitest';
import { flushPromises, mount } from '@vue/test-utils';
import { createPinia, setActivePinia } from 'pinia';
import RecipeVersionEditor from './RecipeVersionEditor.vue';
import { recipeVersionService } from '../../services/recipeVersionService';
import { RecipeVersionStatus } from '../../services/recipeService';
import { useToastStore } from '../../stores/toastStore';

// Issue #88 finding 3: releasing an empty recipe version returns 400 with
// "A recipe version must contain at least one operation..." and the UI must
// surface that backend message both inline (role=alert) and via toast.error.
vi.mock('../../services/recipeVersionService', () => ({
  recipeVersionService: {
    release: vi.fn(),
    addOperation: vi.fn(),
    updateOperation: vi.fn(),
    deleteOperation: vi.fn(),
    setDependencies: vi.fn(),
    addBomItem: vi.fn(),
    updateBomItem: vi.fn(),
    removeBomItem: vi.fn(),
    addOutput: vi.fn(),
    updateOutput: vi.fn(),
    removeOutput: vi.fn(),
    addResource: vi.fn(),
    removeResource: vi.fn(),
    remove: vi.fn()
  },
  OperationDependencyType: { FinishToStart: 1, StartToStart: 2, FinishToFinish: 3, StartToFinish: 4 },
  BomQuantityType: { PerUnit: 1, PerBatch: 2, Fixed: 3 },
  OperationOutputType: { Product: 1, ByProduct: 2, Waste: 3, Sample: 4 },
  RunTimeMode: { PerUnitSeconds: 1, PerBatchMinutes: 2 }
}));

vi.mock('../../services/productService', () => ({
  productService: { browse: vi.fn().mockResolvedValue({ items: [] }) }
}));

vi.mock('../../services/skillService', () => ({
  skillService: { browse: vi.fn().mockResolvedValue({ items: [] }) }
}));

vi.mock('../../services/operationTemplateService', () => ({
  operationTemplateService: { browse: vi.fn().mockResolvedValue({ items: [] }) }
}));

vi.mock('vue-i18n', () => ({
  useI18n: (): { t: (key: string) => string } => ({ t: (key: string): string => key })
}));

const backendMessage = 'A recipe version must contain at least one operation before it can be released';

function backendError(): unknown {
  return { response: { data: { detail: backendMessage } } };
}

function mountEditor(): ReturnType<typeof mount> {
  return mount(RecipeVersionEditor, {
    props: {
      version: {
        id: 'version-1',
        recipeId: 'recipe-1',
        versionNumber: 1,
        status: RecipeVersionStatus.Draft,
        operations: []
      },
      recipeId: 'recipe-1'
    },
    global: {
      plugins: [createPinia()],
      stubs: { AttachmentsPanel: true },
      mocks: { $t: (key: string): string => key }
    }
  }) as ReturnType<typeof mount>;
}

async function clickRelease(wrapper: ReturnType<typeof mount>): Promise<void> {
  const releaseButton = wrapper
    .findAll('button')
    .find((b) => b.text().includes('recipes.detail.release'));
  expect(releaseButton).toBeDefined();
  await releaseButton?.trigger('click');
  await flushPromises();
}

describe('RecipeVersionEditor release error', () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
  });

  it('shows the backend error inline and in a toast when release fails', async () => {
    // Arrange
    vi.mocked(recipeVersionService.release).mockRejectedValueOnce(backendError());
    const wrapper = mountEditor();
    await flushPromises();
    const toast = useToastStore();
    const errorSpy = vi.spyOn(toast, 'error');

    // Act
    await clickRelease(wrapper);

    // Assert
    const alert = wrapper.find('[role="alert"]');
    expect(alert.exists()).toBe(true);
    expect(alert.text()).toContain(backendMessage);
    expect(errorSpy).toHaveBeenCalledWith(backendMessage);
    expect(wrapper.emitted('refresh')).toBeUndefined();
  });

  it('clears the error and emits refresh when release succeeds', async () => {
    // Arrange
    vi.mocked(recipeVersionService.release)
      .mockRejectedValueOnce(backendError())
      .mockResolvedValueOnce(undefined);
    const wrapper = mountEditor();
    await flushPromises();
    const toast = useToastStore();
    const successSpy = vi.spyOn(toast, 'success');
    await clickRelease(wrapper);
    expect(wrapper.find('[role="alert"]').exists()).toBe(true);

    // Act
    await clickRelease(wrapper);

    // Assert
    expect(wrapper.find('[role="alert"]').exists()).toBe(false);
    expect(successSpy).toHaveBeenCalled();
    expect(wrapper.emitted('refresh')).toBeDefined();
  });
});
