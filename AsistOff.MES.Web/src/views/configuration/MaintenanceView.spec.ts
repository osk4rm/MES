import { beforeEach, describe, expect, it, vi } from 'vitest';
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { createPinia, setActivePinia } from 'pinia';
import MaintenanceView from './MaintenanceView.vue';
import {
  MaintenanceWorkOrderPriority,
  MaintenanceWorkOrderStatus,
  maintenanceWorkOrderService,
  type MaintenanceWorkOrderResponse
} from '../../services/maintenanceWorkOrderService';
import { machineService, type MachineResponse } from '../../services/machineService';
import { useToastStore } from '../../stores/toastStore';

// The board UI is new in #172: the service spec only asserts request
// URLs/params, leaving the board rendering, filter narrowing, create modal,
// start/complete/cancel action wiring and invalid-transition feedback
// unasserted. These component tests close those gaps with a mocked service.
// JWT attachment itself lives in the shared `http` interceptor (the service
// delegates to it); unauthenticated access is covered by the global router
// guard + 401 interceptor, and cross-tenant isolation by the API query
// filter — here we prove the visible consequence: a 404 surfaces an error
// toast and leaks no foreign row.

vi.mock('../../services/maintenanceWorkOrderService', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../services/maintenanceWorkOrderService')>();
  return {
    ...actual,
    maintenanceWorkOrderService: {
      browse: vi.fn(),
      get: vi.fn(),
      create: vi.fn(),
      start: vi.fn(),
      complete: vi.fn(),
      cancel: vi.fn()
    }
  };
});

vi.mock('../../services/machineService', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../services/machineService')>();
  return {
    ...actual,
    machineService: {
      ...actual.machineService,
      browse: vi.fn()
    }
  };
});

vi.mock('vue-i18n', () => ({
  useI18n: (): { t: (key: string) => string } => ({
    t: (key: string): string => key
  })
}));

const browseMock = vi.mocked(maintenanceWorkOrderService.browse);
const createMock = vi.mocked(maintenanceWorkOrderService.create);
const startMock = vi.mocked(maintenanceWorkOrderService.start);
const completeMock = vi.mocked(maintenanceWorkOrderService.complete);
const cancelMock = vi.mocked(maintenanceWorkOrderService.cancel);
const browseMachinesMock = vi.mocked(machineService.browse);

function order(overrides: Partial<MaintenanceWorkOrderResponse> = {}): MaintenanceWorkOrderResponse {
  return {
    id: 'order-1',
    code: 'WO-1',
    title: 'Fix spindle',
    description: null,
    machineId: 'machine-1',
    machineCode: 'MC-1',
    priority: MaintenanceWorkOrderPriority.High,
    status: MaintenanceWorkOrderStatus.Open,
    reportedAt: new Date('2026-09-24T10:00:00Z').toISOString(),
    startedAt: null,
    completedAt: null,
    resolutionNotes: null,
    ...overrides
  };
}

function machine(overrides: Partial<MachineResponse> = {}): MachineResponse {
  return {
    id: 'machine-1',
    code: 'MC-1',
    name: 'Lathe 1',
    description: null,
    departmentId: null,
    isActive: true,
    ...overrides
  };
}

function page<T>(items: T[]): { totalCount: number; totalPages: number; items: T[] } {
  return { totalCount: items.length, totalPages: items.length > 0 ? 1 : 0, items };
}

function mountBoard(): VueWrapper {
  return mount(MaintenanceView, {
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

function errorWithStatus(status: number): unknown {
  return { response: { status, data: { title: status === 404 ? 'Not found' : 'Bad request' } }, message: `Request failed with status code ${status}` };
}

function rowAction(wrapper: VueWrapper, key: string): ReturnType<VueWrapper['findAll']>[number] | undefined {
  return wrapper.findAll('button').find((b) => b.attributes('aria-label') === key);
}

describe('MaintenanceView', () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
    browseMachinesMock.mockResolvedValue(page([machine()]));
    browseMock.mockResolvedValue(page([order()]));
    createMock.mockResolvedValue(order({ id: 'order-9', code: 'WO-9' }));
    startMock.mockImplementation(async (id: string) => order({ id, status: MaintenanceWorkOrderStatus.InProgress }));
    completeMock.mockImplementation(async (id: string, req) =>
      order({ id, status: MaintenanceWorkOrderStatus.Done, resolutionNotes: req.resolutionNotes ?? null }));
    cancelMock.mockImplementation(async (id: string) => order({ id, status: MaintenanceWorkOrderStatus.Cancelled }));
  });

  it('renders seeded board rows with code, title, machine, priority and status', async () => {
    const wrapper = mountBoard();
    await flushPromises();

    expect(browseMock).toHaveBeenCalledOnce();
    expect(wrapper.text()).toContain('WO-1');
    expect(wrapper.text()).toContain('Fix spindle');
    expect(wrapper.text()).toContain('MC-1');
    expect(wrapper.text()).toContain('maintenance.priorities.high');
    expect(wrapper.text()).toContain('maintenance.statuses.open');
  });

  it('narrows the list through the machine and status query parameters', async () => {
    const wrapper = mountBoard();
    await flushPromises();
    expect(browseMock).toHaveBeenCalledTimes(1);

    const selects = wrapper.findAll('select');
    expect(selects.length).toBeGreaterThanOrEqual(2);

    await selects[0]?.setValue('machine-1');
    await flushPromises();

    const machineCalls = browseMock.mock.calls;
    const byMachine = machineCalls[machineCalls.length - 1]?.[0] as Record<string, unknown>;
    expect(byMachine).toMatchObject({ machineId: 'machine-1' });

    await selects[1]?.setValue('2');
    await flushPromises();

    const statusCalls = browseMock.mock.calls;
    const byStatus = statusCalls[statusCalls.length - 1]?.[0] as Record<string, unknown>;
    expect(byStatus).toMatchObject({ machineId: 'machine-1', status: 2 });
  });

  it('creates a work order from the modal and shows the new Open order', async () => {
    const wrapper = mountBoard();
    await flushPromises();

    const createBtn = wrapper.findAll('button').find((b) => b.text().includes('maintenance.create'));
    expect(createBtn).toBeDefined();
    await createBtn?.trigger('click');
    await flushPromises();

    const inputs = wrapper.findAll('.modal-stub input');
    expect(inputs.length).toBeGreaterThanOrEqual(2);
    await inputs[0]?.setValue('WO-9');
    await inputs[1]?.setValue('Fix pump');

    const selects = wrapper.findAll('.modal-stub select');
    expect(selects).toHaveLength(2);
    await selects[0]?.setValue('machine-1');
    await selects[1]?.setValue('3');

    await wrapper.find('#maintenance-create-form').trigger('submit');
    await flushPromises();

    expect(createMock).toHaveBeenCalledWith({
      code: 'WO-9',
      title: 'Fix pump',
      description: null,
      machineId: 'machine-1',
      priority: MaintenanceWorkOrderPriority.High
    });
    // Board reloaded after create.
    expect(browseMock.mock.calls.length).toBeGreaterThan(1);
    const toast = useToastStore();
    expect(toast.toasts.some((t) => t.variant === 'success' && t.message === 'toasts.created')).toBe(true);
  });

  it('start moves an Open order to InProgress and reloads the board', async () => {
    const wrapper = mountBoard();
    await flushPromises();
    const callsBefore = browseMock.mock.calls.length;

    const startBtn = rowAction(wrapper, 'maintenance.start');
    expect(startBtn).toBeDefined();
    await startBtn?.trigger('click');
    await flushPromises();

    expect(startMock).toHaveBeenCalledWith('order-1');
    expect(browseMock.mock.calls.length).toBeGreaterThan(callsBefore);
    const toast = useToastStore();
    expect(toast.toasts.some((t) => t.variant === 'success' && t.message === 'toasts.updated')).toBe(true);
  });

  it('complete with resolution notes moves the order to Done showing the notes', async () => {
    browseMock
      .mockResolvedValueOnce(page([order()]))
      .mockResolvedValue(page([order({ status: MaintenanceWorkOrderStatus.Done, resolutionNotes: 'Replaced bearing' })]));

    const wrapper = mountBoard();
    await flushPromises();

    const completeBtn = rowAction(wrapper, 'maintenance.complete');
    expect(completeBtn).toBeDefined();
    await completeBtn?.trigger('click');
    await flushPromises();

    await wrapper.find('.modal-stub textarea').setValue('Replaced bearing');
    await wrapper.find('#maintenance-complete-form').trigger('submit');
    await flushPromises();

    expect(completeMock).toHaveBeenCalledWith('order-1', { resolutionNotes: 'Replaced bearing' });
    expect(wrapper.text()).toContain('Replaced bearing');
    const toast = useToastStore();
    expect(toast.toasts.some((t) => t.variant === 'success' && t.message === 'toasts.updated')).toBe(true);
  });

  it('cancel moves the order to Cancelled after confirmation', async () => {
    const wrapper = mountBoard();
    await flushPromises();
    const callsBefore = browseMock.mock.calls.length;

    const cancelBtn = rowAction(wrapper, 'maintenance.cancelOrder');
    expect(cancelBtn).toBeDefined();
    await cancelBtn?.trigger('click');
    await flushPromises();

    const confirmBtn = wrapper.findAll('button').find((b) => b.text().includes('common.confirm'));
    expect(confirmBtn).toBeDefined();
    await confirmBtn?.trigger('click');
    await flushPromises();

    expect(cancelMock).toHaveBeenCalledWith('order-1');
    expect(browseMock.mock.calls.length).toBeGreaterThan(callsBefore);
    const toast = useToastStore();
    expect(toast.toasts.some((t) => t.variant === 'success' && t.message === 'toasts.updated')).toBe(true);
  });

  it('surfaces an invalid transition as an error toast and leaves the row unchanged', async () => {
    startMock.mockRejectedValue(errorWithStatus(400));

    const wrapper = mountBoard();
    await flushPromises();
    const callsBefore = browseMock.mock.calls.length;

    const startBtn = rowAction(wrapper, 'maintenance.start');
    expect(startBtn).toBeDefined();
    await startBtn?.trigger('click');
    await flushPromises();

    const toast = useToastStore();
    expect(toast.toasts.some((t) => t.variant === 'error')).toBe(true);
    // No reload: the stale row stays visible.
    expect(browseMock.mock.calls.length).toBe(callsBefore);
    expect(wrapper.text()).toContain('WO-1');
  });

  it('shows an error toast for an unknown machine id without leaking foreign rows', async () => {
    createMock.mockRejectedValue(errorWithStatus(404));

    const wrapper = mountBoard();
    await flushPromises();

    const createBtn = wrapper.findAll('button').find((b) => b.text().includes('maintenance.create'));
    await createBtn?.trigger('click');
    await flushPromises();

    const inputs = wrapper.findAll('.modal-stub input');
    await inputs[0]?.setValue('WO-X');
    await inputs[1]?.setValue('Foreign machine order');
    const selects = wrapper.findAll('.modal-stub select');
    await selects[0]?.setValue('machine-1');
    await selects[1]?.setValue('2');

    await wrapper.find('#maintenance-create-form').trigger('submit');
    await flushPromises();

    const toast = useToastStore();
    expect(toast.toasts.some((t) => t.variant === 'error')).toBe(true);
    // Only the tenant's own seeded row is rendered.
    expect(wrapper.text()).toContain('WO-1');
    expect(wrapper.text()).not.toContain('FOREIGN');
  });
});
