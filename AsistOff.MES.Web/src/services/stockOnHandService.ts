import http from './http';

/**
 * One signed stock balance from `GET /api/stock-on-hand`: the sum of PW
 * receipt quantities minus the sum of RW issue quantities for a single
 * product and warehouse pair. A null `warehouseId` is the unassigned bucket
 * (ledger lines without a warehouse hint).
 */
export interface StockOnHandBalance {
  productId: string;
  warehouseId: string | null;
  quantityOnHand: number;
}

/** Both filters are optional; omitted or empty values are stripped. */
export interface BrowseStockOnHandRequest {
  productId?: string;
  warehouseId?: string;
}

const BASE = '/api/stock-on-hand';

export const stockOnHandService = {
  /** Read-only stock balances grouped by product and warehouse. */
  async browse(req: BrowseStockOnHandRequest = {}): Promise<StockOnHandBalance[]> {
    const params: Record<string, string> = {};
    if (req.productId) params.productId = req.productId;
    if (req.warehouseId) params.warehouseId = req.warehouseId;
    const { data } = await http.get<StockOnHandBalance[]>(BASE, { params });
    return data;
  }
};
