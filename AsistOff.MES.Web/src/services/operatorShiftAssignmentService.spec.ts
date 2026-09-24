import { beforeEach, describe, expect, it, vi } from 'vitest';
import http from './http';
import {
  operatorShiftAssignmentService,
  type OperatorShiftAssignmentResponse
} from './operatorShiftAssignmentService';

vi.mock('./http', () => ({
  default: {
    get: vi.fn(),
    post: vi.fn(),
    delete: vi.fn()
  }
}));

const getMock = vi.mocked(http.get);
const postMock = vi.mocked(http.post);
const deleteMock = vi.mocked(http.delete);

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

describe('operatorShiftAssignmentService', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('browse queries the roster endpoint with the selected date and paging', async () => {
    const page = { totalCount: 1, totalPages: 1, items: [assignment()] };
    getMock.mockResolvedValue({ data: page });

    const result = await operatorShiftAssignmentService.browse({ date: '2026-09-24', pageNumber: 1, pageSize: 100 });

    expect(result).toEqual(page);
    expect(getMock).toHaveBeenCalledOnce();
    expect(getMock).toHaveBeenCalledWith('/api/operator-shift-assignments', {
      params: { date: '2026-09-24', pageNumber: 1, pageSize: 100 }
    });
  });

  it('browse forwards range/operator/shift filters and drops empty ones', async () => {
    getMock.mockResolvedValue({ data: { totalCount: 0, totalPages: 0, items: [] } });

    await operatorShiftAssignmentService.browse({
      date: undefined,
      dateFrom: '2026-09-22',
      dateTo: '2026-09-28',
      operatorId: 'operator-1',
      shiftId: undefined,
      pageNumber: 1,
      pageSize: 100
    });

    const [, config] = getMock.mock.calls[0] as [string, { params: Record<string, unknown> }];
    expect(config.params).toMatchObject({
      dateFrom: '2026-09-22',
      dateTo: '2026-09-28',
      operatorId: 'operator-1',
      pageNumber: 1,
      pageSize: 100
    });
    expect(config.params).not.toHaveProperty('date');
    expect(config.params).not.toHaveProperty('shiftId');
  });

  it('get loads a single assignment by id', async () => {
    const expected = assignment();
    getMock.mockResolvedValue({ data: expected });

    const result = await operatorShiftAssignmentService.get('assignment-1');

    expect(result).toEqual(expected);
    expect(getMock).toHaveBeenCalledWith('/api/operator-shift-assignments/assignment-1');
  });

  it('create posts the operator/shift/date triple with notes', async () => {
    const expected = assignment({ notes: 'cover' });
    postMock.mockResolvedValue({ data: expected });

    const result = await operatorShiftAssignmentService.create({
      operatorId: 'operator-1',
      shiftId: 'shift-1',
      date: '2026-09-24',
      notes: 'cover'
    });

    expect(result).toEqual(expected);
    expect(postMock).toHaveBeenCalledWith('/api/operator-shift-assignments', {
      operatorId: 'operator-1',
      shiftId: 'shift-1',
      date: '2026-09-24',
      notes: 'cover'
    });
  });

  it('remove deletes the assignment by id', async () => {
    deleteMock.mockResolvedValue({ data: undefined });

    await operatorShiftAssignmentService.remove('assignment-1');

    expect(deleteMock).toHaveBeenCalledWith('/api/operator-shift-assignments/assignment-1');
  });

  it('propagates API errors (409 duplicate, 404 cross-tenant) to the caller', async () => {
    const conflict = new Error('Request failed with status code 409');
    postMock.mockRejectedValue(conflict);

    await expect(
      operatorShiftAssignmentService.create({ operatorId: 'operator-1', shiftId: 'shift-1', date: '2026-09-24' })
    ).rejects.toBe(conflict);

    const notFound = new Error('Request failed with status code 404');
    getMock.mockRejectedValue(notFound);

    await expect(operatorShiftAssignmentService.browse({ date: '2026-09-24' })).rejects.toBe(notFound);
  });
});
