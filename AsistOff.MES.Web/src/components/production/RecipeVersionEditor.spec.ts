import { beforeEach, describe, expect, it, vi } from 'vitest';
import { flushPromises, mount, type DOMWrapper } from '@vue/test-utils';
import { createPinia, setActivePinia } from 'pinia';
import { defineComponent } from 'vue';
import RecipeVersionEditor from './RecipeVersionEditor.vue';
import { recipeVersionService, RunTimeMode } from '../../services/recipeVersionService';
import { RecipeVersionStatus } from '../../services/recipeService';
import { useToastStore } from '../../stores/toastStore';

// Issue #88 finding 3: releasing a recipe version that the server rejects
// (400 with "A recipe version must contain at least one operation...") must
// surface that backend message both inline (role=alert) and via toast.error.
// Since issue #388 the release button opens a preflight checklist dialog and
// the backend call happens on confirm, the contract is driven through the
// dialog: open -> confirm -> backend rejects -> inline alert + toast, no
// refresh; a later successful confirm clears the error and emits refresh.
// AppModal teleports to document.body, so it is stubbed with an inline
// passthrough to keep the dialog content queryable via the wrapper.
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

vi.mock('../../services/warehouseService', () => ({
  warehouseService: { browse: vi.fn().mockResolvedValue({ items: [] }) }
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

const ModalStub = defineComponent({
  props: { open: { type: Boolean, default: false } },
  template: '<div v-if="open" class="modal-stub"><slot /><slot name="footer" /></div>'
});

const backendMessage = 'A recipe version must contain at least one operation before it can be released';

function backendError(): unknown {
  return { response: { data: { detail: backendMessage } } };
}

interface OperationFixture {
  id: string;
  recipeVersionId: string;
  code: string;
  name: string;
  sortIndex: number;
  runTimeMode: RunTimeMode;
  isOptional: boolean;
  allowParallelExecution: boolean;
  dependencies: never[];
  bomItems: never[];
  outputs: never[];
  resourceRequirements: never[];
}

interface VersionFixture {
  id: string;
  recipeId: string;
  versionNumber: number;
  status: RecipeVersionStatus;
  operations: OperationFixture[];
}

// One bare operation, no outputs/BOM/warehouses/validity issues: the only
// checklist finding is the non-blocking outputs warning, so confirm is enabled.
function releasableVersion(): VersionFixture {
  return {
    id: 'version-1',
    recipeId: 'recipe-1',
    versionNumber: 1,
    status: RecipeVersionStatus.Draft,
    operations: [
      {
        id: 'op-1',
        recipeVersionId: 'version-1',
        code: 'OP10',
        name: 'Cutting',
        sortIndex: 0,
        runTimeMode: RunTimeMode.PerUnitSeconds,
        isOptional: false,
        allowParallelExecution: false,
        dependencies: [],
        bomItems: [],
        outputs: [],
        resourceRequirements: []
      }
    ]
  };
}

function emptyVersion(): VersionFixture {
  return {
    id: 'version-1',
    recipeId: 'recipe-1',
    versionNumber: 1,
    status: RecipeVersionStatus.Draft,
    operations: []
  };
}

function mountEditor(version: VersionFixture): ReturnType<typeof mount> {
  return mount(RecipeVersionEditor, {
    props: {
      version,
      recipeId: 'recipe-1'
    },
    global: {
      plugins: [createPinia()],
      stubs: { AttachmentsPanel: true, AppModal: ModalStub },
      mocks: { $t: (key: string): string => key }
    }
  }) as ReturnType<typeof mount>;
}

function findButton(wrapper: ReturnType<typeof mount>, text: string): DOMWrapper<HTMLButtonElement> | undefined {
  return wrapper.findAll('button').find((b) => b.text().includes(text));
}

async function clickRelease(wrapper: ReturnType<typeof mount>): Promise<void> {
  const releaseButton = findButton(wrapper, 'recipes.detail.release');
  expect(releaseButton).toBeDefined();
  await releaseButton?.trigger('click');
  await flushPromises();
}

async function clickConfirm(wrapper: ReturnType<typeof mount>): Promise<void> {
  const confirmButton = findButton(wrapper, 'recipes.checklist.release');
  expect(confirmButton).toBeDefined();
  await confirmButton?.trigger('click');
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
    const wrapper = mountEditor(releasableVersion());
    await flushPromises();
    const toast = useToastStore();
    const errorSpy = vi.spyOn(toast, 'error');

    // Act
    await clickRelease(wrapper);
    await clickConfirm(wrapper);

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
    const wrapper = mountEditor(releasableVersion());
    await flushPromises();
    const toast = useToastStore();
    const successSpy = vi.spyOn(toast, 'success');
    await clickRelease(wrapper);
    await clickConfirm(wrapper);
    expect(wrapper.find('[role="alert"]').exists()).toBe(true);

    // Act
    await clickConfirm(wrapper);

    // Assert
    expect(wrapper.find('[role="alert"]').exists()).toBe(false);
    expect(successSpy).toHaveBeenCalled();
    expect(wrapper.emitted('refresh')).toBeDefined();
  });

  it('blocks the confirm button while a fail-state check is red', async () => {
    // Arrange: empty operations -> operations rule fails -> hasBlocking.
    const wrapper = mountEditor(emptyVersion());
    await flushPromises();

    // Act
    await clickRelease(wrapper);

    // Assert: dialog opened with the blocked hint, confirm disabled, no call.
    expect(wrapper.find('.checklist__hint').exists()).toBe(true);
    const confirmButton = findButton(wrapper, 'recipes.checklist.release');
    expect(confirmButton).toBeDefined();
    expect(confirmButton?.element.disabled).toBe(true);
    expect(vi.mocked(recipeVersionService.release)).not.toHaveBeenCalled();
  });
});
