import { describe, expect, it, vi } from 'vitest';
import { defineComponent, h } from 'vue';
import { mount } from '@vue/test-utils';
import { CanceledError } from 'axios';
import { useCrudPage } from './useCrudPage';
import type { IPagedResponse } from '../models/pagedModels';
import type { HttpRequestInit } from '../services/http';

interface Item {
  id: string;
}

type FetchFn = (req: object, init?: HttpRequestInit) => Promise<IPagedResponse<Item>>;

function emptyPage(): IPagedResponse<Item> {
  return { items: [], totalCount: 0, totalPages: 0 };
}

function mountPage(fetchImpl: FetchFn) {
  let table: ReturnType<typeof useCrudPage<Item>> | undefined;
  const Probe = defineComponent({
    setup() {
      table = useCrudPage<Item>({ fetch: fetchImpl, errorFallback: 'List failed' });
      return () => h('div');
    }
  });
  const wrapper = mount(Probe);
  return { wrapper, table: () => table as NonNullable<typeof table> };
}

function tick(ms = 10): Promise<void> {
  return new Promise<void>((resolve) => {
    setTimeout(resolve, ms);
  });
}

// Covers the useCrudPage error state of issue #273: a failed list fetch
// surfaces an error with a working retry, aborts are silent, unmounting
// cancels the in-flight request, and stale responses never commit state.
describe('useCrudPage error and retry', () => {
  it('exposes error alongside loading and refetches on retry', async () => {
    const fetchImpl = vi.fn<FetchFn>();
    fetchImpl.mockRejectedValueOnce(new Error('boom'));
    fetchImpl.mockResolvedValueOnce({ items: [{ id: 'a' }], totalCount: 1, totalPages: 1 });
    const { table } = mountPage(fetchImpl);

    await table().fetch();

    expect(table().error.value).toBe('boom');
    expect(table().loading.value).toBe(false);
    expect(table().items.value).toEqual([]);

    await table().retry();

    expect(fetchImpl).toHaveBeenCalledTimes(2);
    expect(table().error.value).toBeNull();
    expect(table().items.value).toEqual([{ id: 'a' }]);
  });

  it('uses the fallback message when the failure carries none', async () => {
    const { table } = mountPage(() => Promise.reject(new Error()));

    await table().fetch();

    expect(table().error.value).toBe('List failed');
  });

  it('keeps aborts silent instead of raising the error banner', async () => {
    const { table } = mountPage(() => Promise.reject(new CanceledError()));

    await table().fetch();

    expect(table().error.value).toBeNull();
    expect(table().loading.value).toBe(false);
  });

  it('aborts the in-flight request on unmount with no state update', async () => {
    let captured: AbortSignal | undefined;
    let resolveFetch!: (page: IPagedResponse<Item>) => void;
    const fetchImpl = vi.fn<FetchFn>((_req, init) => {
      captured = init?.signal;
      return new Promise<IPagedResponse<Item>>((resolve) => {
        resolveFetch = resolve;
      });
    });
    const { wrapper, table } = mountPage(fetchImpl);

    void table().fetch();
    await tick();
    expect(captured).toBeDefined();

    wrapper.unmount();
    expect(captured?.aborted).toBe(true);

    resolveFetch({ items: [{ id: 'late' }], totalCount: 1, totalPages: 1 });
    await tick();

    expect(table().items.value).toEqual([]);
    expect(table().error.value).toBeNull();
  });

  it('aborts the previous fetch so only the latest response commits', async () => {
    const signals: Array<AbortSignal | undefined> = [];
    const resolvers: Array<(page: IPagedResponse<Item>) => void> = [];
    const fetchImpl = vi.fn<FetchFn>((_req, init) => {
      signals.push(init?.signal);
      return new Promise<IPagedResponse<Item>>((resolve) => {
        resolvers.push(resolve);
      });
    });
    const { table } = mountPage(fetchImpl);

    void table().fetch();
    await tick();
    void table().fetch();
    await tick();

    expect(signals[0]?.aborted).toBe(true);

    resolvers[1]({ items: [{ id: 'fresh' }], totalCount: 1, totalPages: 1 });
    resolvers[0]({ items: [{ id: 'stale' }], totalCount: 1, totalPages: 1 });
    await tick();

    expect(table().items.value).toEqual([{ id: 'fresh' }]);
    expect(table().error.value).toBeNull();
    expect(table().loading.value).toBe(false);
  });

  it('starts clean without an initial error', () => {
    const { table } = mountPage(() => Promise.resolve(emptyPage()));

    expect(table().error.value).toBeNull();
    expect(table().loading.value).toBe(false);
  });
});
