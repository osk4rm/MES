import { beforeEach, describe, expect, it, vi } from 'vitest';
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { createPinia, setActivePinia } from 'pinia';
import MachinesView from './MachinesView.vue';
import { machineService, type MachineResponse } from '../../services/machineService';
import { shiftService } from '../../services/shiftService';
import { useToastStore } from '../../stores/toastStore';

// Verifier gap for #204 / PR #206 (TESTS_INSUFFICIENT): backend unit +
// endpoint suites proved the capacity/efficiency API contract, but the
// Machines-view criterion ("displays both fields and an edit persists them
// and survives a page reload") had no committed coverage and the PR body
// deferred the Playwright click-through to the e2e stage. These component
// tests close the automated gap by driving the real view with a mocked
// service: list shows both columns, edit persists via update + reload
// (the jsdom equivalent of reload survival: browse re-fetch returns the
// saved values), and create sends both fields. The full reload
// click-through remains for the e2e stage.

vi.mock('../../services/machineService', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../services/machineService')>();
  return {
    ...actual,
    machineService: {
      ...actual.machineService,
      browse: vi.fn(),
      create: vi.fn(),
      update: vi.fn(),
      remove: vi.fn()
    }
  };
});

vi.mock('../../services/shiftService', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../services/shiftService')>();
  return {
    ...actual,
    shiftService: {
      ...actual.shiftService,
      browse: vi.fn()
    }
  };
});

vi.mock('vue-i18n', () => ({
  useI18n: (): { t: (key: string) => string } => ({
    t: (key: string): string => key
  })
}));

const browseMock = vi.mocked(machineService.browse);
const createMock = vi.mocked(machineService.create);
const updateMock = vi.mocked(machineService.update);
const browseShiftsMock = vi.mocked(shiftService.browse);

function machine(overrides: Partial<MachineResponse> = {}): MachineResponse {
  return {
    id: 'machine-1',
    code: 'WC-1',
    name: 'Work Center 1',
    description: null,
    departmentId: null,
    isActive: true,
    capacity: 2.5,
    efficiencyFactor: 0.85,
    ...overrides
  };
}

function page<T>(items: T[]): { totalCount: number; totalPages: number; items: T[] } {
  return { totalCount: items.length, totalPages: items.length > 0 ? 1 : 0, items };
}

function mountMachines(): VueWrapper {
  return mount(MachinesView, {
    global: {
      plugins: [createPinia()],
      mocks: { $t: (key: string): string => key },
      stubs: {
        // Render modal content inline so Teleport does not move it to
        // document.body (unreachable via wrapper.find in jsdom).
        AppModal: {
          props: ['open', 'title', 'size'],
          template: '<div v-if="open" class="modal-stub"><slot /><slot name="footer" /></div>'
        }
      }
    }
  }) as unknown as VueWrapper;
}

describe('MachinesView capacity and efficiency', () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
    browseShiftsMock.mockResolvedValue(page([]));
    browseMock.mockResolvedValue(page([machine()]));
    createMock.mockResolvedValue(machine({ id: 'machine-9' }));
    updateMock.mockResolvedValue(undefined);
  });

  it('displays the capacity and efficiency columns with their values', async () => {
    const wrapper = mountMachines();
    await flushPromises();

    expect(browseMock).toHaveBeenCalledOnce();
    // Column headers use the same i18n keys as the form.
    expect(wrapper.text()).toContain('machines.capacity');
    expect(wrapper.text()).toContain('machines.efficiencyFactor');
    // Row renders the persisted values unchanged.
    expect(wrapper.text()).toContain('2.5');
    expect(wrapper.text()).toContain('0.85');
  });

  it('edit persists both fields via update and survives a reload (re-fetch)', async () => {
    const wrapper = mountMachines();
    await flushPromises();

    const editBtn = wrapper.findAll('button').find((b) => b.attributes('aria-label') === 'common.edit');
    expect(editBtn).toBeDefined();
    await editBtn?.trigger('click');
    await flushPromises();

    // Modal opens prefilled with the row values.
    const numbers = wrapper.findAll('.modal-stub input[type="number"]');
    expect(numbers).toHaveLength(2);
    expect((numbers[0]?.element as HTMLInputElement).value).toBe('2.5');
    expect((numbers[1]?.element as HTMLInputElement).value).toBe('0.85');

    await numbers[0]?.setValue('4');
    await numbers[1]?.setValue('0.9');
    await wrapper.find('#machine-form').trigger('submit');
    await flushPromises();

    expect(updateMock).toHaveBeenCalledWith(
      'machine-1',
      expect.objectContaining({ id: 'machine-1', capacity: 4, efficiencyFactor: 0.9 })
    );
    const callsAfterSave = browseMock.mock.calls.length;
    expect(callsAfterSave).toBeGreaterThan(1);

    // Reload survival: the next browse returns the saved values and the
    // table renders them.
    browseMock.mockResolvedValue(page([machine({ capacity: 4, efficiencyFactor: 0.9 })]));
    const refreshBtn = wrapper.findAll('button').find((b) => b.text().includes('common.refresh'));
    await refreshBtn?.trigger('click');
    await flushPromises();

    expect(wrapper.text()).toContain('4');
    expect(wrapper.text()).toContain('0.9');
    const toast = useToastStore();
    expect(toast.toasts.some((t) => t.variant === 'success' && t.message === 'toasts.updated')).toBe(true);
  });

  it('create sends both fields to the API', async () => {
    const wrapper = mountMachines();
    await flushPromises();

    const createBtn = wrapper.findAll('button').find((b) => b.text().includes('machines.create'));
    expect(createBtn).toBeDefined();
    await createBtn?.trigger('click');
    await flushPromises();

    const texts = wrapper.findAll('.modal-stub input[type="text"], .modal-stub input:not([type])');
    await texts[0]?.setValue('WC-9');
    await texts[1]?.setValue('Cell 9');
    const numbers = wrapper.findAll('.modal-stub input[type="number"]');
    await numbers[0]?.setValue('2.5');
    await numbers[1]?.setValue('0.85');
    await wrapper.find('#machine-form').trigger('submit');
    await flushPromises();

    expect(createMock).toHaveBeenCalledWith(
      expect.objectContaining({ code: 'WC-9', name: 'Cell 9', capacity: 2.5, efficiencyFactor: 0.85 })
    );
  });
});
