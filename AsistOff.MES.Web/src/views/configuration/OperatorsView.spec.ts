import { beforeEach, describe, expect, it, vi } from 'vitest';
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { createPinia, setActivePinia } from 'pinia';
import OperatorsView from './OperatorsView.vue';
import { operatorService, type OperatorResponse } from '../../services/operatorService';
import { departmentService } from '../../services/departmentService';
import { shiftService, type ShiftResponse } from '../../services/shiftService';
import {
  operatorShiftAssignmentService,
  type OperatorShiftAssignmentResponse
} from '../../services/operatorShiftAssignmentService';
import { useToastStore } from '../../stores/toastStore';

// Verifier gap for #167 / PR #169 (TESTS_INSUFFICIENT): the backend unit +
// endpoint suites proved the roster API contract, but the Operators-view
// roster (list for the selected date, assign, delete) had no committed
// coverage and the PR body deferred the click-through to the e2e stage.
// These component tests close that gap by driving the real roster UI:
// pick a date -> see the listed assignment, assign -> see it reloaded,
// delete -> see it removed. JWT attachment itself lives in the shared `http`
// interceptor (asserted via the service spec); here we prove the roster flow
// the issue test plan requires.

vi.mock('../../services/operatorService', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../services/operatorService')>();
  return {
    ...actual,
    operatorService: {
      ...actual.operatorService,
      browse: vi.fn(),
      create: vi.fn(),
      update: vi.fn(),
      remove: vi.fn()
    }
  };
});

vi.mock('../../services/departmentService', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../services/departmentService')>();
  return {
    ...actual,
    departmentService: {
      ...actual.departmentService,
      browse: vi.fn()
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

vi.mock('../../services/operatorShiftAssignmentService', () => ({
  operatorShiftAssignmentService: {
    browse: vi.fn(),
    get: vi.fn(),
    create: vi.fn(),
    remove: vi.fn()
  }
}));

vi.mock('vue-i18n', () => ({
  useI18n: (): { t: (key: string) => string } => ({
    t: (key: string): string => key
  })
}));

const browseOperatorsMock = vi.mocked(operatorService.browse);
const browseDepartmentsMock = vi.mocked(departmentService.browse);
const browseShiftsMock = vi.mocked(shiftService.browse);
const browseRosterMock = vi.mocked(operatorShiftAssignmentService.browse);
const createRosterMock = vi.mocked(operatorShiftAssignmentService.create);
const removeRosterMock = vi.mocked(operatorShiftAssignmentService.remove);

function operator(overrides: Partial<OperatorResponse> = {}): OperatorResponse {
  return {
    id: 'operator-1',
    identifier: 'OP-001',
    firstName: 'Jan',
    lastName: 'Kowalski',
    ratePerHour: 42,
    department: null,
    ...overrides
  };
}

function shift(overrides: Partial<ShiftResponse> = {}): ShiftResponse {
  return {
    id: 'shift-1',
    code: 'S1',
    name: 'Shift 1',
    description: null,
    startTime: '06:00',
    endTime: '14:00',
    isActive: true,
    ...overrides
  };
}

function assignment(overrides: Partial<OperatorShiftAssignmentResponse> = {}): OperatorShiftAssignmentResponse {
  return {
    id: 'assignment-1',
    operatorId: 'operator-1',
    operatorIdentifier: 'OP-001',
    operatorName: 'Jan Kowalski',
    shiftId: 'shift-1',
    shiftCode: 'S1',
    shiftName: 'Shift 1',
    date: '2026-09-24',
    notes: null,
    ...overrides
  };
}

function page<T>(items: T[]): { totalCount: number; totalPages: number; items: T[] } {
  return { totalCount: items.length, totalPages: items.length > 0 ? 1 : 0, items };
}

function seedLookups(): void {
  browseOperatorsMock.mockResolvedValue(page([operator()]));
  browseDepartmentsMock.mockResolvedValue(page([]));
  browseShiftsMock.mockResolvedValue(page([shift()]));
}

function mountOperators(): VueWrapper {
  return mount(OperatorsView, {
    global: {
      plugins: [createPinia()],
      mocks: { $t: (key: string): string => key }
    }
  }) as unknown as VueWrapper;
}

function rosterCard(wrapper: VueWrapper): ReturnType<VueWrapper['find']> {
  return wrapper.find('.roster-card');
}

describe('OperatorsView roster', () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
    seedLookups();
    browseRosterMock.mockResolvedValue(page([assignment()]));
    createRosterMock.mockResolvedValue(assignment({ id: 'assignment-2' }));
    removeRosterMock.mockResolvedValue(undefined);
  });

  it('lists the assignments for the selected date (open view -> see roster)', async () => {
    const wrapper = mountOperators();
    await flushPromises();

    expect(browseRosterMock).toHaveBeenCalledOnce();
    const [req] = browseRosterMock.mock.calls[0] as [{ date: string }];
    expect(req.date).toMatch(/^\d{4}-\d{2}-\d{2}$/);

    const card = rosterCard(wrapper);
    expect(card.text()).toContain('Jan Kowalski');
    expect(card.text()).toContain('Shift 1');
    expect(card.find('table.app-table').exists()).toBe(true);
  });

  it('assigns the selected operator to the selected shift and reloads the list', async () => {
    const wrapper = mountOperators();
    await flushPromises();
    expect(browseRosterMock).toHaveBeenCalledTimes(1);
    const [initial] = browseRosterMock.mock.calls[0] as [{ date: string }];

    const form = wrapper.find('form.roster-form');
    expect(form.exists()).toBe(true);
    const selects = form.findAll('select');
    expect(selects).toHaveLength(2);
    await selects[0]?.setValue('operator-1');
    await selects[1]?.setValue('shift-1');
    await form.trigger('submit');
    await flushPromises();

    expect(createRosterMock).toHaveBeenCalledWith({
      operatorId: 'operator-1',
      shiftId: 'shift-1',
      date: initial.date,
      notes: null
    });
    expect(browseRosterMock).toHaveBeenCalledTimes(2);
    const toast = useToastStore();
    expect(toast.toasts.some((t) => t.variant === 'success' && t.message === 'operators.roster.assignedToast')).toBe(true);
  });

  it('deletes a roster assignment and reloads the list (delete -> gone)', async () => {
    const wrapper = mountOperators();
    await flushPromises();

    browseRosterMock.mockResolvedValue(page([]));
    const deleteBtn = rosterCard(wrapper).find('table.app-table button[aria-label="common.delete"]');
    expect(deleteBtn.exists()).toBe(true);
    await deleteBtn.trigger('click');
    await flushPromises();

    expect(removeRosterMock).toHaveBeenCalledWith('assignment-1');
    expect(browseRosterMock).toHaveBeenCalledTimes(2);
    const toast = useToastStore();
    expect(toast.toasts.some((t) => t.variant === 'success' && t.message === 'toasts.deleted')).toBe(true);
    expect(rosterCard(wrapper).text()).toContain('operators.roster.empty');
  });

  it('shows the empty state (not an error) when nothing is assigned for the date', async () => {
    browseRosterMock.mockResolvedValue(page([]));

    const wrapper = mountOperators();
    await flushPromises();
    const toast = useToastStore();

    expect(rosterCard(wrapper).text()).toContain('operators.roster.empty');
    expect(rosterCard(wrapper).find('table.app-table').exists()).toBe(false);
    expect(toast.toasts.filter((t) => t.variant === 'error')).toHaveLength(0);
  });

  it('changing the date reloads the roster for that date', async () => {
    const wrapper = mountOperators();
    await flushPromises();
    expect(browseRosterMock).toHaveBeenCalledTimes(1);

    const dateInput = wrapper.find('.roster-controls input[type="date"]');
    expect(dateInput.exists()).toBe(true);
    await dateInput.setValue('2026-09-25');
    await flushPromises();

    expect(browseRosterMock).toHaveBeenCalledTimes(2);
    const [, second] = browseRosterMock.mock.calls as [[{ date: string }], [{ date: string }]];
    expect(second[0].date).toBe('2026-09-25');
  });

  it('a failed assign keeps the list and shows the error toast', async () => {
    createRosterMock.mockRejectedValue({
      response: { status: 409, data: { title: 'Conflict' } },
      message: 'Request failed with status code 409'
    });

    const wrapper = mountOperators();
    await flushPromises();

    const form = wrapper.find('form.roster-form');
    const selects = form.findAll('select');
    await selects[0]?.setValue('operator-1');
    await selects[1]?.setValue('shift-1');
    await form.trigger('submit');
    await flushPromises();

    expect(createRosterMock).toHaveBeenCalledOnce();
    // No reload on failure: the previously listed assignment stays visible.
    expect(browseRosterMock).toHaveBeenCalledTimes(1);
    expect(rosterCard(wrapper).text()).toContain('Jan Kowalski');
    const toast = useToastStore();
    expect(toast.toasts.some((t) => t.variant === 'error' && t.message === 'Conflict')).toBe(true);
  });
});
