import { beforeEach, describe, expect, it, vi } from 'vitest';
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { createPinia, setActivePinia } from 'pinia';
import OeeDashboardView from './OeeDashboardView.vue';
import { oeeService } from '../../services/oeeService';
import { machineService, type MachineResponse } from '../../services/machineService';

// Slice (3/3) F-18: an invalid OEE window surfaces inline via AppFormField
// error on the from/to fields in addition to the toast, so the offending
// fields are marked. All filter controls already carry visible labels.

vi.mock('../../services/oeeService', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../services/oeeService')>();
  return {
    ...actual,
    oeeService: {
      getSnapshot: vi.fn(),
      getTrend: vi.fn(),
      getLosses: vi.fn()
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

const mockReplace = vi.fn();
const mockQuery: Record<string, unknown> = {};

vi.mock('vue-router', () => ({
  useRoute: (): { query: Record<string, unknown> } => ({ query: mockQuery }),
  useRouter: (): { replace: (...args: unknown[]) => void } => ({ replace: mockReplace })
}));

vi.mock('vue-i18n', () => ({
  useI18n: (): { t: (key: string) => string } => ({
    t: (key: string): string => key
  })
}));

function mountOee(): VueWrapper {
  return mount(OeeDashboardView, {
    global: {
      plugins: [createPinia()],
      mocks: { $t: (key: string): string => key }
    }
  }) as unknown as VueWrapper;
}

describe('OeeDashboardView window error (F-18)', () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
    for (const key of Object.keys(mockQuery)) delete mockQuery[key];
    mockQuery.machineId = 'machine-1';
    vi.mocked(machineService.browse).mockResolvedValue({
      items: [
        {
          id: 'machine-1',
          code: 'WC-1',
          name: 'Work Center 1',
          description: null,
          departmentId: null,
          isActive: true,
          capacity: 1,
          efficiencyFactor: 1
        } as MachineResponse
      ],
      totalCount: 1,
      totalPages: 1
    });
    vi.mocked(oeeService.getSnapshot).mockResolvedValue(null as never);
    vi.mocked(oeeService.getTrend).mockResolvedValue(null as never);
    vi.mocked(oeeService.getLosses).mockResolvedValue(null as never);
  });

  it('labels every filter control', async () => {
    const wrapper = mountOee();
    await flushPromises();

    const text = wrapper.text();
    expect(text).toContain('oeeDashboard.workCenter');
    expect(text).toContain('oeeDashboard.from');
    expect(text).toContain('oeeDashboard.to');
    expect(text).toContain('oeeDashboard.idealCycleTime');
    expect(text).toContain('oeeDashboard.bucket');
  });

  it('marks the window fields inline when To precedes From', async () => {
    const wrapper = mountOee();
    await flushPromises();

    const inputs = wrapper.findAll('.app-filter-bar input[type="datetime-local"]');
    expect(inputs.length).toBe(2);
    await inputs[0]?.setValue('2026-09-24T14:00');
    await inputs[1]?.setValue('2026-09-24T06:00');

    const apply = wrapper.findAll('button').find((b) => b.text().includes('oeeDashboard.apply'));
    expect(apply).toBeDefined();
    vi.mocked(oeeService.getSnapshot).mockClear();
    await apply?.trigger('click');
    await flushPromises();

    // Inline field error (not just the toast) marks the offending fields.
    expect(wrapper.text()).toContain('oeeDashboard.invalidWindow');
    // No request leaves for input the backend would reject with 400.
    expect(vi.mocked(oeeService.getSnapshot)).not.toHaveBeenCalled();
  });
});
