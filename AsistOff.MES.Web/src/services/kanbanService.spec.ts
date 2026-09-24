import { beforeEach, describe, expect, it, vi } from 'vitest';
import http from './http';
import {
  KanbanCardStatus,
  kanbanService,
  type KanbanCardResponse,
  type KanbanLoopResponse
} from './kanbanService';

vi.mock('./http', () => ({
  default: {
    get: vi.fn(),
    post: vi.fn()
  }
}));

const getMock = vi.mocked(http.get);
const postMock = vi.mocked(http.post);

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

describe('kanbanService', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('browseLoops queries the loop dictionary endpoint with paging', async () => {
    const page = { totalCount: 1, totalPages: 1, items: [loop()] };
    getMock.mockResolvedValue({ data: page });

    const result = await kanbanService.browseLoops({ pageNumber: 1, pageSize: 100 });

    expect(result).toEqual(page);
    expect(getMock).toHaveBeenCalledOnce();
    expect(getMock).toHaveBeenCalledWith('/api/kanban/loops', {
      params: { pageNumber: 1, pageSize: 100 }
    });
  });

  it('browseLoops drops empty filters so the selector lists every loop', async () => {
    getMock.mockResolvedValue({ data: { totalCount: 0, totalPages: 0, items: [] } });

    await kanbanService.browseLoops({ code: undefined, isActive: undefined });

    const [, config] = getMock.mock.calls[0] as [string, { params: Record<string, unknown> }];
    expect(config.params).not.toHaveProperty('code');
    expect(config.params).not.toHaveProperty('isActive');
  });

  it('browseCards queries the per-loop card registry with the status filter', async () => {
    const page = { totalCount: 1, totalPages: 1, items: [card({ status: KanbanCardStatus.Empty })] };
    getMock.mockResolvedValue({ data: page });

    const result = await kanbanService.browseCards('loop-1', KanbanCardStatus.Empty, { pageSize: 100 });

    expect(result).toEqual(page);
    expect(getMock).toHaveBeenCalledWith('/api/kanban/loops/loop-1/cards', {
      params: { pageSize: 100, status: KanbanCardStatus.Empty }
    });
  });

  it('browseCards without a status loads the whole loop registry', async () => {
    getMock.mockResolvedValue({ data: { totalCount: 0, totalPages: 0, items: [] } });

    await kanbanService.browseCards('loop-1');

    const [, config] = getMock.mock.calls[0] as [string, { params: Record<string, unknown> }];
    expect(config.params).not.toHaveProperty('status');
  });

  it('consumeCard posts the Full -> Empty pull signal', async () => {
    const emptied = card({ status: KanbanCardStatus.Empty });
    postMock.mockResolvedValue({ data: emptied });

    const result = await kanbanService.consumeCard('card-1');

    expect(result).toEqual(emptied);
    expect(postMock).toHaveBeenCalledWith('/api/kanban/cards/card-1/consume', {});
  });

  it('orderCard posts the Empty -> Ordered pull signal', async () => {
    const ordered = card({ status: KanbanCardStatus.Ordered });
    postMock.mockResolvedValue({ data: ordered });

    const result = await kanbanService.orderCard('card-1');

    expect(result).toEqual(ordered);
    expect(postMock).toHaveBeenCalledWith('/api/kanban/cards/card-1/order', {});
  });

  it('replenishCard posts the Ordered -> Full pull signal', async () => {
    const replenished = card({ status: KanbanCardStatus.Full });
    postMock.mockResolvedValue({ data: replenished });

    const result = await kanbanService.replenishCard('card-1');

    expect(result).toEqual(replenished);
    expect(postMock).toHaveBeenCalledWith('/api/kanban/cards/card-1/replenish', {});
  });

  it('propagates API errors (409 WIP limit, 404 cross-tenant) to the caller', async () => {
    const conflict = new Error('Request failed with status code 409');
    postMock.mockRejectedValue(conflict);

    await expect(kanbanService.orderCard('card-1')).rejects.toBe(conflict);

    const notFound = new Error('Request failed with status code 404');
    getMock.mockRejectedValue(notFound);

    await expect(kanbanService.browseCards('foreign-loop')).rejects.toBe(notFound);
  });
});
