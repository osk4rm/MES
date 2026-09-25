import { beforeEach, describe, expect, it, vi } from 'vitest';
import http from './http';
import { stockOnHandService, type StockOnHandBalance } from './stockOnHandService';

vi.mock('./http', () => ({
  default: {
    get: vi.fn()
  }
}));

const getMock = vi.mocked(http.get);

function balance(overrides: Partial<StockOnHandBalance> = {}): StockOnHandBalance {
  return {
    productId: 'product-1',
    warehouseId: 'warehouse-1',
    quantityOnHand: 6,
    ...overrides
  };
}

describe('stockOnHandService', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('browse without filters queries the endpoint with empty params', async () => {
    const expected = [balance(), balance({ productId: 'product-2', warehouseId: null, quantityOnHand: 7 })];
    getMock.mockResolvedValue({ data: expected });

    const result = await stockOnHandService.browse();

    expect(result).toEqual(expected);
    expect(getMock).toHaveBeenCalledOnce();
    expect(getMock).toHaveBeenCalledWith('/api/stock-on-hand', { params: {} });
  });

  it('browse forwards product and warehouse filters', async () => {
    const expected = [balance()];
    getMock.mockResolvedValue({ data: expected });

    const result = await stockOnHandService.browse({ productId: 'product-1', warehouseId: 'warehouse-1' });

    expect(result).toEqual(expected);
    expect(getMock).toHaveBeenCalledWith('/api/stock-on-hand', {
      params: { productId: 'product-1', warehouseId: 'warehouse-1' }
    });
  });

  it('browse strips empty filter values', async () => {
    getMock.mockResolvedValue({ data: [] });

    await stockOnHandService.browse({ productId: '', warehouseId: undefined });

    expect(getMock).toHaveBeenCalledWith('/api/stock-on-hand', { params: {} });
  });

  it('propagates API errors to the caller', async () => {
    const failure = new Error('Request failed with status code 401');
    getMock.mockRejectedValue(failure);

    await expect(stockOnHandService.browse()).rejects.toBe(failure);
  });
});
