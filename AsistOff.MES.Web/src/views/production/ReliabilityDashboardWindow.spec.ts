import { beforeEach, describe, expect, it, vi } from 'vitest';
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { createPinia, setActivePinia } from 'pinia';
import ReliabilityDashboardView from './ReliabilityDashboardView.vue';
import { reliabilityService } from '../../services/reliabilityService';
import { machineService, type MachineResponse } from '../../services/machineService';

// Slice (3/3) F-18: an invalid reliability window surfaces inline via
// AppFormField error on the from/to fields in addition to the toast, so the
// offending fields are marked. All filter controls already carry labels.

vi.mock('../../services/reliabilityService', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../services/reliabilityService')>();
  return {
    ...actual,
    reliabilityService: {
      getSnapshot: vi.fn(),
      getTrend: vi.fn(),
      getFleet: vi.fn()
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

function mountReliability(): VueWrapper {
  return mount(ReliabilityDashboardView, {
    global: {
      plugins: [createPinia()],
      mocks: { $t: (key: string): string => key }
    }
  }) as unknown as VueWrapper;
}

describe('ReliabilityDashboardView window error (F-18)', () => {
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
    vi.mocked(reliabilityService.getSnapshot).mockResolvedValue(null as never);
    vi.mocked(reliabilityService.getTrend).mockResolvedValue(null as never);
    vi.mocked(reliabilityService.getFleet).mockResolvedValue([]);
  });

  it('labels every filter control', async () => {
    const wrapper = mountReliability();
    await flushPromises();

    const text = wrapper.text();
    expect(text).toContain('reliabilityDashboard.workCenter');
    expect(text).toContain('reliabilityDashboard.preset');
    expect(text).toContain('reliabilityDashboard.from');
    expect(text).toContain('reliabilityDashboard.to');
    expect(text).toContain('reliabilityDashboard.bucket');
  });

  it('marks the window fields inline when To precedes From', async () => {
    const wrapper = mountReliability();
    await flushPromises();

    const inputs = wrapper.findAll('.app-filter-bar input[type="datetime-local"]');
    expect(inputs.length).toBe(2);
    await inputs[0]?.setValue('2026-09-24T14:00');
    await inputs[1]?.setValue('2026-09-24T06:00');

    const apply = wrapper.findAll('button').find((b) => b.text().includes('reliabilityDashboard.apply'));
    expect(apply).toBeDefined();
    vi.mocked(reliabilityService.getSnapshot).mockClear();
    await apply?.trigger('click');
    await flushPromises();

    // Inline field error (not just the toast) marks the offending fields.
    expect(wrapper.text()).toContain('reliabilityDashboard.invalidWindow');
    // No request leaves for input the backend would reject with 400.
    expect(vi.mocked(reliabilityService.getSnapshot)).not.toHaveBeenCalled();
  });
});
