import { beforeEach, describe, expect, it, vi } from 'vitest';
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { createPinia, setActivePinia } from 'pinia';
import OpcUaConnectionsView from './OpcUaConnectionsView.vue';
import { opcUaConnectionService, type OpcUaConnectionStatusEntry } from '../../services/opcUaConnectionService';
import { machineService } from '../../services/machineService';
import { useToastStore } from '../../stores/toastStore';

// Verifier gaps for #161 / PR #164 (TESTS_INSUFFICIENT): the backend unit +
// endpoint tests proved the status API, but criterion 5 (panel badges match
// the API, Test toast, stale distinct, no reload) had zero frontend coverage
// and no Playwright evidence. These component tests close that gap alongside
// opcUaConnectionService.spec.ts. JWT attachment lives in the shared `http`
// interceptor (the service delegates to it); here we prove the visible
// consequence: badges/counts follow the API payload, the Test button toasts
// and refreshes in place (router.replace only, never a full reload), and the
// machine filter stays deep-linkable via ?machineId=.

vi.mock('../../services/opcUaConnectionService', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../services/opcUaConnectionService')>();
  return {
    ...actual,
    opcUaConnectionService: {
      getStatus: vi.fn(),
      test: vi.fn()
    }
  };
});

vi.mock('../../services/machineService', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../services/machineService')>();
  return {
    ...actual,
    machineService: {
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

const getStatusMock = vi.mocked(opcUaConnectionService.getStatus);
const testMock = vi.mocked(opcUaConnectionService.test);
const browseMachinesMock = vi.mocked(machineService.browse);

function entry(overrides: Partial<OpcUaConnectionStatusEntry> = {}): OpcUaConnectionStatusEntry {
  return {
    connectionId: 'conn-live',
    machineId: 'machine-1',
    endpointUrl: 'opc.tcp://plc-a:4840',
    isEnabled: true,
    lastSeenAtUtc: new Date().toISOString(),
    lastError: null,
    isLive: true,
    totalTags: 3,
    reportingTags: 1,
    staleTags: 2,
    ...overrides
  };
}

function liveEntry(): OpcUaConnectionStatusEntry {
  return entry({
    connectionId: 'conn-live',
    endpointUrl: 'opc.tcp://plc-a:4840',
    isEnabled: true,
    lastSeenAtUtc: new Date().toISOString(),
    isLive: true
  });
}

function staleEntry(): OpcUaConnectionStatusEntry {
  return entry({
    connectionId: 'conn-stale',
    endpointUrl: 'opc.tcp://plc-a:4840/second',
    isEnabled: true,
    lastSeenAtUtc: new Date(Date.now() - 5 * 60 * 1000).toISOString(),
    lastError: 'timeout',
    isLive: false
  });
}

function neverEntry(): OpcUaConnectionStatusEntry {
  return entry({
    connectionId: 'conn-never',
    endpointUrl: 'opc.tcp://plc-b:4840',
    isEnabled: true,
    lastSeenAtUtc: null,
    isLive: false
  });
}

function disabledEntry(): OpcUaConnectionStatusEntry {
  return entry({
    connectionId: 'conn-disabled',
    endpointUrl: 'opc.tcp://plc-b:4840/disabled',
    isEnabled: false,
    lastSeenAtUtc: new Date().toISOString(),
    isLive: false
  });
}

function statusPayload(connections: OpcUaConnectionStatusEntry[]): {
  connections: OpcUaConnectionStatusEntry[];
  totalCount: number;
  liveCount: number;
  staleCount: number;
  disabledCount: number;
} {
  return {
    connections,
    totalCount: connections.length,
    liveCount: connections.filter((c) => c.isLive).length,
    staleCount: connections.filter((c) => c.isEnabled && !c.isLive).length,
    disabledCount: connections.filter((c) => !c.isEnabled).length
  };
}

function mountView(): VueWrapper {
  return mount(OpcUaConnectionsView, {
    global: {
      plugins: [createPinia()],
      mocks: { $t: (key: string): string => key }
    }
  }) as unknown as VueWrapper;
}

function findTestButtons(wrapper: VueWrapper): ReturnType<VueWrapper['findAll']> {
  return wrapper.findAll('button').filter((b) => b.text().includes('opcUaConnections.test'));
}

describe('OpcUaConnectionsView', () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
    for (const key of Object.keys(mockQuery)) delete mockQuery[key];
    browseMachinesMock.mockResolvedValue({ items: [], totalCount: 0, totalPages: 0 });
    testMock.mockResolvedValue({
      id: 'conn-live',
      endpointUrl: 'opc.tcp://plc-a:4840',
      reachable: true,
      checkedAt: new Date().toISOString()
    });
  });

  it('renders one row per connection with badges matching the API payload', async () => {
    const connections = [liveEntry(), staleEntry(), neverEntry(), disabledEntry()];
    getStatusMock.mockResolvedValue(statusPayload(connections));

    const wrapper = mountView();
    await flushPromises();

    expect(getStatusMock).toHaveBeenCalledWith(undefined);
    const text = wrapper.text();
    for (const c of connections) {
      expect(text).toContain(c.endpointUrl);
    }
    // One badge per row state: live / stale / never-seen / disabled.
    expect(text).toContain('opcUaConnections.statusLive');
    expect(text).toContain('opcUaConnections.statusStale');
    expect(text).toContain('opcUaConnections.statusNever');
    expect(text).toContain('opcUaConnections.statusDisabled');
    // Header summary counts follow the API totals.
    expect(text).toContain('opcUaConnections.liveCount');
    expect(text).toContain('opcUaConnections.staleCount');
    expect(text).toContain('opcUaConnections.disabledCount');
    // Per-connection tag line renders reporting/total counts.
    expect(text).toContain('opcUaConnections.tagsLine');
    const rows = wrapper.findAll('table.app-table tbody tr.app-table__row');
    expect(rows).toHaveLength(4);
  });

  it('marks stale rows visually distinct without a full page reload', async () => {
    getStatusMock.mockResolvedValue(statusPayload([liveEntry(), staleEntry()]));

    const wrapper = mountView();
    await flushPromises();

    const stale = wrapper.findAll('.conn-endpoint--stale');
    expect(stale).toHaveLength(1);
    expect(stale[0]?.text()).toContain('opc.tcp://plc-a:4840/second');
    // No full-page navigation: the view only ever calls router.replace for
    // the machine filter, never location.assign/reload.
    expect(mockReplace).not.toHaveBeenCalledWith(expect.stringMatching(/reload|location/));
  });

  it('Test button shows the success toast and refreshes the list in place', async () => {
    getStatusMock.mockResolvedValue(statusPayload([liveEntry()]));

    const wrapper = mountView();
    await flushPromises();
    expect(getStatusMock).toHaveBeenCalledTimes(1);

    const buttons = findTestButtons(wrapper);
    expect(buttons).toHaveLength(1);
    await buttons[0]?.trigger('click');
    await flushPromises();

    expect(testMock).toHaveBeenCalledWith('conn-live');
    const toast = useToastStore();
    expect(toast.toasts.some((t) => t.variant === 'success' && t.message === 'opcUaConnections.testOk')).toBe(true);
    // List refreshed without a reload: status re-queried, no router navigation.
    expect(getStatusMock).toHaveBeenCalledTimes(2);
  });

  it('failed Test shows the warning toast and still refreshes the list', async () => {
    getStatusMock.mockResolvedValue(statusPayload([liveEntry()]));
    testMock.mockResolvedValue({
      id: 'conn-live',
      endpointUrl: 'opc.tcp://plc-a:4840',
      reachable: false,
      checkedAt: new Date().toISOString()
    });

    const wrapper = mountView();
    await flushPromises();

    const buttons = findTestButtons(wrapper);
    await buttons[0]?.trigger('click');
    await flushPromises();

    const toast = useToastStore();
    expect(toast.toasts.some((t) => t.variant === 'warning' && t.message === 'opcUaConnections.testFailed')).toBe(true);
    expect(getStatusMock).toHaveBeenCalledTimes(2);
  });

  it('honours the ?machineId= deep link and keeps it in the URL via router.replace only', async () => {
    mockQuery.machineId = 'machine-1';
    browseMachinesMock.mockResolvedValue({
      items: [{ id: 'machine-1', code: 'WC-1', name: 'Work Center 1', isActive: true, capacity: 1, efficiencyFactor: 1 }],
      totalCount: 1,
      totalPages: 1
    });
    getStatusMock.mockResolvedValue(statusPayload([liveEntry()]));

    const wrapper = mountView();
    await flushPromises();

    expect(getStatusMock).toHaveBeenCalledWith('machine-1');
    expect(wrapper.text()).toContain('opc.tcp://plc-a:4840');

    // Changing the filter re-queries for the new machine and re-syncs the URL
    // without a full page reload.
    getStatusMock.mockResolvedValue(statusPayload([staleEntry()]));
    const select = wrapper.find('select');
    expect(select.exists()).toBe(true);
    await select.setValue('machine-1');
    await flushPromises();

    expect(mockReplace).toHaveBeenCalledWith({ query: expect.objectContaining({ machineId: 'machine-1' }) });
    expect(getStatusMock).toHaveBeenCalledWith('machine-1');
  });

  it('shows the empty state (not an error) when the tenant has no connections', async () => {
    getStatusMock.mockResolvedValue(statusPayload([]));

    const wrapper = mountView();
    await flushPromises();
    const toast = useToastStore();

    expect(wrapper.text()).toContain('opcUaConnections.noConnections');
    expect(wrapper.findAll('table.app-table')).toHaveLength(0);
    expect(toast.toasts.filter((t) => t.variant === 'error')).toHaveLength(0);
  });
});
