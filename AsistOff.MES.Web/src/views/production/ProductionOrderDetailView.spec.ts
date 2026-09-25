import { beforeEach, describe, expect, it, vi } from 'vitest';
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { createPinia, setActivePinia } from 'pinia';
import ProductionOrderDetailView from './ProductionOrderDetailView.vue';
import { productionOrderService, ProductionOrderStatus } from '../../services/productionOrderService';
import { productionConfirmationService } from '../../services/productionConfirmationService';
import { machineService } from '../../services/machineService';
import { operatorService } from '../../services/operatorService';
import { productService } from '../../services/productService';
import { warehouseService } from '../../services/warehouseService';
import { lotService } from '../../services/lotService';

// Component tests for issue #221: the confirmation modal records produced and
// consumed lots so every confirmed run leaves an auditable genealogy trace.
// Backend edge posting itself is covered by CreateProductionConfirmationGenealogyTests
// (unit) and ProductionConfirmationGenealogyEndpointTests (endpoint); here we
// prove the UI collects the lot references, validates them inline, and forwards
// them through productionConfirmationService.create.

vi.mock('../../services/productionOrderService', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../services/productionOrderService')>();
  return {
    ...actual,
    productionOrderService: {
      get: vi.fn(),
      release: vi.fn(),
      complete: vi.fn(),
      close: vi.fn(),
      getMovements: vi.fn()
    }
  };
});

vi.mock('../../services/productionConfirmationService', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../services/productionConfirmationService')>();
  return {
    ...actual,
    productionConfirmationService: {
      browse: vi.fn(),
      get: vi.fn(),
      create: vi.fn(),
      remove: vi.fn(),
      getMovements: vi.fn()
    }
  };
});

vi.mock('../../services/machineService', () => ({ machineService: { browse: vi.fn() } }));
vi.mock('../../services/operatorService', () => ({ operatorService: { browse: vi.fn() } }));
vi.mock('../../services/productService', () => ({ productService: { browse: vi.fn() } }));
vi.mock('../../services/warehouseService', () => ({ warehouseService: { browse: vi.fn() } }));

vi.mock('../../services/lotService', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../services/lotService')>();
  return {
    ...actual,
    lotService: {
      browse: vi.fn(),
      get: vi.fn(),
      getByCode: vi.fn(),
      create: vi.fn(),
      update: vi.fn(),
      changeStatus: vi.fn(),
      remove: vi.fn()
    }
  };
});

vi.mock('vue-router', () => ({
  useRoute: (): { params: Record<string, unknown> } => ({ params: { id: 'order-1' } }),
  useRouter: (): { push: (...args: unknown[]) => void } => ({ push: vi.fn() })
}));

vi.mock('vue-i18n', () => ({
  useI18n: (): { t: (key: string) => string } => ({
    t: (key: string): string => key
  })
}));

const orderGetMock = vi.mocked(productionOrderService.get);
const orderMovementsMock = vi.mocked(productionOrderService.getMovements);
const confirmBrowseMock = vi.mocked(productionConfirmationService.browse);
const confirmCreateMock = vi.mocked(productionConfirmationService.create);
const confirmMovementsMock = vi.mocked(productionConfirmationService.getMovements);
const machineBrowseMock = vi.mocked(machineService.browse);
const operatorBrowseMock = vi.mocked(operatorService.browse);
const productBrowseMock = vi.mocked(productService.browse);
const warehouseBrowseMock = vi.mocked(warehouseService.browse);
const lotBrowseMock = vi.mocked(lotService.browse);

function releasedOrder(): Record<string, unknown> {
  return {
    id: 'order-1',
    code: 'PO-001',
    productId: 'product-1',
    recipeId: 'recipe-1',
    recipeVersionId: 'version-1',
    plannedQuantity: 100,
    measureUnitId: null,
    priority: 0,
    dueDate: null,
    status: ProductionOrderStatus.Released,
    releasedAt: new Date('2026-09-24T08:00:00Z').toISOString(),
    notes: null,
    createdAt: new Date('2026-09-24T08:00:00Z').toISOString(),
    producedQuantity: 0,
    scrappedQuantity: 0,
    remainingQuantity: 100,
    confirmationsCount: 0
  };
}

function emptyPage<T>(): { totalCount: number; totalPages: number; items: T[] } {
  return { totalCount: 0, totalPages: 0, items: [] };
}

function lot(id: string, code: string): Record<string, unknown> {
  return {
    id,
    code,
    productId: 'product-1',
    measureUnitId: 'unit-1',
    quantity: 100,
    status: 1,
    createdAt: new Date('2026-09-24T08:00:00Z').toISOString()
  };
}

function mountDetail(): VueWrapper {
  return mount(ProductionOrderDetailView, {
    global: {
      plugins: [createPinia()],
      mocks: {
        $t: (key: string): string => key,
        $router: { push: vi.fn() }
      },
      stubs: {
        // Render modal content inline so Teleport does not move it to
        // document.body (unreachable via wrapper.find in jsdom).
        AppModal: {
          props: ['open', 'title'],
          template: '<div v-if="open" class="modal-stub"><slot /><slot name="footer" /></div>'
        },
        AppPageHeader: true,
        AppCard: true,
        AppTable: true,
        AppPagination: true,
        AppEmptyState: true,
        AppConfirmDialog: true,
        AppRowActions: true
      }
    }
  }) as unknown as VueWrapper;
}

async function openReportModal(wrapper: VueWrapper): Promise<void> {
  const report = wrapper.findAll('button').find(b => b.text() === 'productionConfirmations.report');
  expect(report).toBeTruthy();
  await report?.trigger('click');
  await flushPromises();
}

async function fillMachineAndGood(wrapper: VueWrapper): Promise<void> {
  const selects = wrapper.findAll('.modal-stub select');
  await selects[0].setValue('machine-1');
  const numbers = wrapper.findAll('.modal-stub input[type="number"]');
  await numbers[0].setValue(10);
  await numbers[1].setValue(0);
}

describe('ProductionOrderDetailView confirmation lots (#221)', () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
    orderGetMock.mockResolvedValue(releasedOrder() as never);
    orderMovementsMock.mockResolvedValue([]);
    confirmBrowseMock.mockResolvedValue(emptyPage());
    confirmMovementsMock.mockResolvedValue([]);
    machineBrowseMock.mockResolvedValue({
      totalCount: 1,
      totalPages: 1,
      items: [{ id: 'machine-1', code: 'WC-1', name: 'Cell 1' }]
    } as never);
    operatorBrowseMock.mockResolvedValue(emptyPage() as never);
    productBrowseMock.mockResolvedValue(emptyPage() as never);
    warehouseBrowseMock.mockResolvedValue(emptyPage() as never);
    lotBrowseMock.mockResolvedValue({
      totalCount: 3,
      totalPages: 1,
      items: [lot('lot-produced', 'LOT-FG'), lot('lot-a', 'LOT-A'), lot('lot-b', 'LOT-B')]
    } as never);
    confirmCreateMock.mockResolvedValue({ id: 'conf-1' } as never);
  });

  it('shows a produced lot picker and a consumed lots editor in the report modal', async () => {
    const wrapper = mountDetail();
    await flushPromises();

    await openReportModal(wrapper);

    expect(lotBrowseMock).toHaveBeenCalled();
    expect(wrapper.text()).toContain('productionConfirmations.producedLot');
    expect(wrapper.text()).toContain('productionConfirmations.consumedLots');
    expect(wrapper.text()).toContain('productionConfirmations.addConsumedLot');
  });

  it('blocks save with inline validation when consumed lots lack a produced lot', async () => {
    const wrapper = mountDetail();
    await flushPromises();
    await openReportModal(wrapper);
    await fillMachineAndGood(wrapper);

    const add = wrapper.findAll('.modal-stub button').find(b => b.text() === 'productionConfirmations.addConsumedLot');
    await add?.trigger('click');
    await flushPromises();

    const selects = wrapper.findAll('.modal-stub select');
    await selects[selects.length - 1].setValue('lot-a');
    const numbers = wrapper.findAll('.modal-stub input[type="number"]');
    await numbers[numbers.length - 1].setValue(5);

    await wrapper.find('#confirmation-form').trigger('submit');
    await flushPromises();

    expect(confirmCreateMock).not.toHaveBeenCalled();
    expect(wrapper.text()).toContain('productionConfirmations.producedLotRequired');
  });

  it('rejects a same-lot self link inline without calling the API', async () => {
    const wrapper = mountDetail();
    await flushPromises();
    await openReportModal(wrapper);
    await fillMachineAndGood(wrapper);

    const selects = wrapper.findAll('.modal-stub select');
    await selects[2].setValue('lot-a');

    const add = wrapper.findAll('.modal-stub button').find(b => b.text() === 'productionConfirmations.addConsumedLot');
    await add?.trigger('click');
    await flushPromises();

    const rowSelects = wrapper.findAll('.modal-stub select');
    await rowSelects[rowSelects.length - 1].setValue('lot-a');
    const numbers = wrapper.findAll('.modal-stub input[type="number"]');
    await numbers[numbers.length - 1].setValue(5);

    await wrapper.find('#confirmation-form').trigger('submit');
    await flushPromises();

    expect(confirmCreateMock).not.toHaveBeenCalled();
    expect(wrapper.text()).toContain('productionConfirmations.lotsMustDiffer');
  });

  it('forwards produced lot and consumed lots to the create payload', async () => {
    const wrapper = mountDetail();
    await flushPromises();
    await openReportModal(wrapper);
    await fillMachineAndGood(wrapper);

    const selects = wrapper.findAll('.modal-stub select');
    await selects[2].setValue('lot-produced');

    const add = wrapper.findAll('.modal-stub button').find(b => b.text() === 'productionConfirmations.addConsumedLot');
    await add?.trigger('click');
    await flushPromises();

    const rowSelects = wrapper.findAll('.modal-stub select');
    await rowSelects[rowSelects.length - 1].setValue('lot-a');
    const numbers = wrapper.findAll('.modal-stub input[type="number"]');
    await numbers[numbers.length - 1].setValue(5);

    await wrapper.find('#confirmation-form').trigger('submit');
    await flushPromises();

    expect(confirmCreateMock).toHaveBeenCalledOnce();
    expect(confirmCreateMock.mock.calls[0][0]).toMatchObject({
      productionOrderId: 'order-1',
      machineId: 'machine-1',
      producedLotId: 'lot-produced',
      consumedLots: [{ lotId: 'lot-a', quantity: 5 }]
    });
  });

  it('supports removing a consumed row so omitted lots post no edges', async () => {
    const wrapper = mountDetail();
    await flushPromises();
    await openReportModal(wrapper);
    await fillMachineAndGood(wrapper);

    const add = wrapper.findAll('.modal-stub button').find(b => b.text() === 'productionConfirmations.addConsumedLot');
    await add?.trigger('click');
    await flushPromises();
    expect(wrapper.findAll('.consumed-lots__row')).toHaveLength(1);

    const remove = wrapper.find('.consumed-lots__row button[aria-label="productionConfirmations.removeConsumedLot"]');
    await remove.trigger('click');
    await flushPromises();
    expect(wrapper.findAll('.consumed-lots__row')).toHaveLength(0);

    await wrapper.find('#confirmation-form').trigger('submit');
    await flushPromises();

    expect(confirmCreateMock).toHaveBeenCalledOnce();
    expect(confirmCreateMock.mock.calls[0][0]).toMatchObject({ producedLotId: null, consumedLots: [] });
  });
});
