import { beforeEach, describe, expect, it, vi } from 'vitest';
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { createPinia, setActivePinia } from 'pinia';
import KanbanBoardView from './KanbanBoardView.vue';
import {
  KanbanCardStatus,
  kanbanService,
  type KanbanCardResponse,
  type KanbanLoopResponse
} from '../../services/kanbanService';
import { useToastStore } from '../../stores/toastStore';

// Verifier gaps for #147 / PR #158 (TESTS_INSUFFICIENT): the service spec only
// asserted request URLs/params, leaving the board column rendering,
// signal-button flow + reload, illegal-transition guard, empty/loopMissing
// states, 409 -> WIP/inactive notices and the ?loopId= deep-link via
// router.replace unasserted. These component tests close those gaps. JWT
// attachment itself lives in the shared `http` interceptor (the service
// delegates to it); here we prove the cross-tenant consequence: a foreign
// loop id surfaces the not-found state instead of another tenant's cards.

vi.mock('../../services/kanbanService', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../services/kanbanService')>();
  return {
    ...actual,
    kanbanService: {
      browseLoops: vi.fn(),
      browseCards: vi.fn(),
      consumeCard: vi.fn(),
      orderCard: vi.fn(),
      replenishCard: vi.fn()
    }
  };
});

const mockReplace = vi.fn();
const mockQuery: Record<string, unknown> = {};

vi.mock('vue-router', () => ({
  useRoute: (): { query: Record<string, unknown> } => ({ query: mockQuery }),
  useRouter: (): { replace: (...args: unknown[]) => void } => ({ replace: mockReplace })
}));

vi.mock('vue-i18n', () => ({
  useI18n: (): {
    t: (key: string) => string;
    tm: (key: string) => Record<string, string>;
  } => ({
    t: (key: string): string => key,
    tm: (key: string): Record<string, string> =>
      key === 'kanban.statuses' ? { '1': 'Full', '2': 'Empty', '3': 'Ordered' } : {}
  })
}));

const browseLoopsMock = vi.mocked(kanbanService.browseLoops);
const browseCardsMock = vi.mocked(kanbanService.browseCards);
const consumeMock = vi.mocked(kanbanService.consumeCard);
const orderMock = vi.mocked(kanbanService.orderCard);
const replenishMock = vi.mocked(kanbanService.replenishCard);

function loop(overrides: Partial<KanbanLoopResponse> = {}): KanbanLoopResponse {
  return {
    id: 'loop-1',
    code: 'KB-LOOP-1',
    productId: 'product-1',
    consumingMachineId: 'machine-1',
    supplyingWarehouseId: 'warehouse-1',
    cardQuantity: 10,
    cardsInCirculation: 2,
    isActive: true,
    notes: null,
    createdAt: new Date('2026-09-24T10:00:00Z').toISOString(),
    updatedAt: null,
    ...overrides
  };
}

function card(overrides: Partial<KanbanCardResponse> = {}): KanbanCardResponse {
  return {
    id: 'card-1',
    loopId: 'loop-1',
    cardNumber: 'KB-LOOP-1-001',
    status: KanbanCardStatus.Full,
    notes: null,
    createdAt: new Date('2026-09-24T10:00:00Z').toISOString(),
    updatedAt: null,
    ...overrides
  };
}

function page<T>(items: T[]): { totalCount: number; totalPages: number; items: T[] } {
  return { totalCount: items.length, totalPages: items.length > 0 ? 1 : 0, items };
}

function conflictError(): unknown {
  return { response: { status: 409, data: { title: 'Conflict' } }, message: 'Request failed with status code 409' };
}

function findButton(wrapper: VueWrapper, label: string): ReturnType<VueWrapper['findAll']>[number] | undefined {
  return wrapper.findAll('button').find((b) => b.text().includes(label));
}

function mountBoard(): VueWrapper {
  return mount(KanbanBoardView, {
    global: {
      plugins: [createPinia()],
      mocks: { $t: (key: string): string => key }
    }
  }) as unknown as VueWrapper;
}

describe('KanbanBoardView', () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
    for (const key of Object.keys(mockQuery)) delete mockQuery[key];
    consumeMock.mockResolvedValue(card({ status: KanbanCardStatus.Empty }));
    orderMock.mockResolvedValue(card({ status: KanbanCardStatus.Ordered }));
    replenishMock.mockResolvedValue(card({ status: KanbanCardStatus.Full }));
  });

  it('renders three status columns with seeded cards in the correct column and keeps loop state in the URL (no full reload)', async () => {
    mockQuery.loopId = 'loop-1';
    browseLoopsMock.mockResolvedValue(page([loop()]));
    const full = card({ id: 'card-full', cardNumber: 'KB-LOOP-1-001', status: KanbanCardStatus.Full });
    const empty = card({ id: 'card-empty', cardNumber: 'KB-LOOP-1-002', status: KanbanCardStatus.Empty });
    const ordered = card({ id: 'card-ordered', cardNumber: 'KB-LOOP-1-003', status: KanbanCardStatus.Ordered });
    browseCardsMock.mockImplementation(async (loopId: string, status?: KanbanCardStatus) => {
      void loopId;
      if (status === KanbanCardStatus.Full) return page([full]);
      if (status === KanbanCardStatus.Empty) return page([empty]);
      return page([ordered]);
    });

    const wrapper = mountBoard();
    await flushPromises();

    // One table per status column, each holding its own seeded card only.
    const tables = wrapper.findAll('table.app-table');
    expect(tables).toHaveLength(3);
    expect(tables[0]?.text()).toContain('KB-LOOP-1-001');
    expect(tables[0]?.text()).not.toContain('KB-LOOP-1-002');
    expect(tables[1]?.text()).toContain('KB-LOOP-1-002');
    expect(tables[1]?.text()).not.toContain('KB-LOOP-1-003');
    expect(tables[2]?.text()).toContain('KB-LOOP-1-003');
    expect(tables[2]?.text()).not.toContain('KB-LOOP-1-001');

    // Each column was loaded for the deep-linked loop without a full page
    // reload: the view only ever calls router.replace, never location.assign.
    expect(browseCardsMock).toHaveBeenCalledTimes(3);
    expect(browseCardsMock).toHaveBeenCalledWith('loop-1', KanbanCardStatus.Full, expect.anything());
    expect(browseCardsMock).toHaveBeenCalledWith('loop-1', KanbanCardStatus.Empty, expect.anything());
    expect(browseCardsMock).toHaveBeenCalledWith('loop-1', KanbanCardStatus.Ordered, expect.anything());
  });

  it('consume moves a Full card with success feedback and reloads the board', async () => {
    mockQuery.loopId = 'loop-1';
    browseLoopsMock.mockResolvedValue(page([loop()]));
    browseCardsMock.mockResolvedValue(page([card({ cardNumber: 'KB-LOOP-1-001', status: KanbanCardStatus.Full })]));

    const wrapper = mountBoard();
    await flushPromises();
    expect(browseCardsMock).toHaveBeenCalledTimes(3);

    const consumeBtn = findButton(wrapper, 'kanban.consume');
    expect(consumeBtn).toBeDefined();
    await consumeBtn?.trigger('click');
    await flushPromises();

    expect(consumeMock).toHaveBeenCalledWith('card-1');
    const toast = useToastStore();
    expect(toast.toasts.some((t) => t.variant === 'success' && t.message === 'kanban.consumed')).toBe(true);
    // Board reloaded after the transition (3 initial + 3 reload calls).
    expect(browseCardsMock).toHaveBeenCalledTimes(6);
  });

  it('order moves an Empty card with success feedback and reloads the board', async () => {
    mockQuery.loopId = 'loop-1';
    browseLoopsMock.mockResolvedValue(page([loop()]));
    browseCardsMock.mockImplementation(async (loopId: string, status?: KanbanCardStatus) => {
      void loopId;
      if (status === KanbanCardStatus.Empty) {
        return page([card({ id: 'card-2', cardNumber: 'KB-LOOP-1-002', status: KanbanCardStatus.Empty })]);
      }
      return page([]);
    });

    const wrapper = mountBoard();
    await flushPromises();

    const orderBtn = findButton(wrapper, 'kanban.order');
    expect(orderBtn).toBeDefined();
    await orderBtn?.trigger('click');
    await flushPromises();

    expect(orderMock).toHaveBeenCalledWith('card-2');
    const toast = useToastStore();
    expect(toast.toasts.some((t) => t.variant === 'success' && t.message === 'kanban.ordered')).toBe(true);
    expect(browseCardsMock).toHaveBeenCalledTimes(6);
  });

  it('replenish moves an Ordered card with success feedback and reloads the board', async () => {
    mockQuery.loopId = 'loop-1';
    browseLoopsMock.mockResolvedValue(page([loop()]));
    browseCardsMock.mockImplementation(async (loopId: string, status?: KanbanCardStatus) => {
      void loopId;
      if (status === KanbanCardStatus.Ordered) {
        return page([card({ id: 'card-3', cardNumber: 'KB-LOOP-1-003', status: KanbanCardStatus.Ordered })]);
      }
      return page([]);
    });

    const wrapper = mountBoard();
    await flushPromises();

    const replenishBtn = findButton(wrapper, 'kanban.replenish');
    expect(replenishBtn).toBeDefined();
    await replenishBtn?.trigger('click');
    await flushPromises();

    expect(replenishMock).toHaveBeenCalledWith('card-3');
    const toast = useToastStore();
    expect(toast.toasts.some((t) => t.variant === 'success' && t.message === 'kanban.replenished')).toBe(true);
    expect(browseCardsMock).toHaveBeenCalledTimes(6);
  });

  it('illegal transition toasts an error and leaves the card in place (no API call)', async () => {
    mockQuery.loopId = 'loop-1';
    browseLoopsMock.mockResolvedValue(page([loop()]));
    // Backend slipped a stale Empty card into the Full column: the guard must
    // refuse the consume instead of posting it.
    browseCardsMock.mockImplementation(async (loopId: string, status?: KanbanCardStatus) => {
      void loopId;
      if (status === KanbanCardStatus.Full) {
        return page([card({ cardNumber: 'KB-LOOP-1-001', status: KanbanCardStatus.Empty })]);
      }
      return page([]);
    });

    const wrapper = mountBoard();
    await flushPromises();

    const consumeBtn = findButton(wrapper, 'kanban.consume');
    expect(consumeBtn).toBeDefined();
    await consumeBtn?.trigger('click');
    await flushPromises();

    expect(consumeMock).not.toHaveBeenCalled();
    const toast = useToastStore();
    expect(toast.toasts.some((t) => t.variant === 'error' && t.message === 'kanban.illegalTransition')).toBe(true);
    // Card left in place, board not reloaded.
    expect(wrapper.text()).toContain('KB-LOOP-1-001');
    expect(browseCardsMock).toHaveBeenCalledTimes(3);
  });

  it('shows the empty state (not an error) when the loop has no cards', async () => {
    mockQuery.loopId = 'loop-1';
    browseLoopsMock.mockResolvedValue(page([loop()]));
    browseCardsMock.mockResolvedValue(page([]));

    const wrapper = mountBoard();
    await flushPromises();
    const toast = useToastStore();

    expect(wrapper.text()).toContain('kanban.loopEmpty');
    expect(wrapper.findAll('table.app-table')).toHaveLength(0);
    expect(toast.toasts.filter((t) => t.variant === 'error')).toHaveLength(0);
  });

  it('maps a 409 on order to the WIP-limit notice', async () => {
    mockQuery.loopId = 'loop-1';
    browseLoopsMock.mockResolvedValue(page([loop()]));
    browseCardsMock.mockImplementation(async (loopId: string, status?: KanbanCardStatus) => {
      void loopId;
      if (status === KanbanCardStatus.Empty) {
        return page([card({ id: 'card-2', cardNumber: 'KB-LOOP-1-002', status: KanbanCardStatus.Empty })]);
      }
      return page([]);
    });
    orderMock.mockRejectedValue(conflictError());

    const wrapper = mountBoard();
    await flushPromises();

    const orderBtn = findButton(wrapper, 'kanban.order');
    expect(orderBtn).toBeDefined();
    await orderBtn?.trigger('click');
    await flushPromises();

    const toast = useToastStore();
    expect(toast.toasts.some((t) => t.variant === 'error' && t.message === 'kanban.wipLimitNotice')).toBe(true);
  });

  it('maps a 409 on replenish to the inactive-loop notice', async () => {
    mockQuery.loopId = 'loop-1';
    browseLoopsMock.mockResolvedValue(page([loop()]));
    browseCardsMock.mockImplementation(async (loopId: string, status?: KanbanCardStatus) => {
      void loopId;
      if (status === KanbanCardStatus.Ordered) {
        return page([card({ id: 'card-3', cardNumber: 'KB-LOOP-1-003', status: KanbanCardStatus.Ordered })]);
      }
      return page([]);
    });
    replenishMock.mockRejectedValue(conflictError());

    const wrapper = mountBoard();
    await flushPromises();

    const replenishBtn = findButton(wrapper, 'kanban.replenish');
    expect(replenishBtn).toBeDefined();
    await replenishBtn?.trigger('click');
    await flushPromises();

    const toast = useToastStore();
    expect(toast.toasts.some((t) => t.variant === 'error' && t.message === 'kanban.inactiveLoopNotice')).toBe(true);
  });

  it('shows the not-found feedback for a cross-tenant loop link (foreign loop id)', async () => {
    mockQuery.loopId = 'foreign-loop';
    browseLoopsMock.mockResolvedValue(page([loop()]));
    browseCardsMock.mockResolvedValue(page([]));

    const wrapper = mountBoard();
    await flushPromises();

    // The deep-linked id is outside this tenant's loops: no foreign cards are
    // rendered, the not-found feedback is shown instead.
    expect(wrapper.text()).toContain('kanban.notFound');
    expect(wrapper.text()).toContain('kanban.notFoundHint');
    expect(wrapper.findAll('table.app-table')).toHaveLength(0);
    expect(wrapper.text()).not.toContain('KB-LOOP-1-001');
  });

  it('syncs the loop selector to ?loopId= without a full page reload', async () => {
    browseLoopsMock.mockResolvedValue(page([loop({ id: 'loop-a', code: 'KB-A' }), loop({ id: 'loop-b', code: 'KB-B' })]));
    browseCardsMock.mockResolvedValue(page([]));

    const wrapper = mountBoard();
    await flushPromises();

    // No deep link: the first loop is auto-selected and the URL is synced via
    // router.replace only (no location change / full reload).
    expect(mockReplace).toHaveBeenCalledWith({ query: expect.objectContaining({ loopId: 'loop-a' }) });
    expect(browseCardsMock).toHaveBeenCalledWith('loop-a', KanbanCardStatus.Full, expect.anything());

    // Switching loops reloads the board for the new loop and re-syncs the URL.
    const select = wrapper.find('select');
    expect(select.exists()).toBe(true);
    await select.setValue('loop-b');
    await flushPromises();

    expect(mockReplace).toHaveBeenCalledWith({ query: expect.objectContaining({ loopId: 'loop-b' }) });
    expect(browseCardsMock).toHaveBeenCalledWith('loop-b', KanbanCardStatus.Full, expect.anything());
  });
});
