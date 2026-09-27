import type { RecipeVersionDetailResponse } from './recipeVersionService';
import type { ProductResponse } from './productService';

/**
 * Client-side mirror of the server release preflight rules
 * (`RecipeReleasePreflight` in Production.Application, issue #388 R-7).
 * Used by the release checklist dialog: fail-state rules block the release
 * button, warn-state rules are shown but do not block. The server rechecks
 * authoritatively on `POST /api/recipe-versions/{id}/release`.
 *
 * Uncertainty is always warn, never fail: products or warehouses outside the
 * loaded lookup pages cannot be verified locally, so they warn and let the
 * server decide.
 */
export const releaseCheckRules = [
  'operations',
  'outputs',
  'products',
  'warehouses',
  'validity',
  'dependencies'
] as const;
export type ReleaseCheckRule = (typeof releaseCheckRules)[number];

export type ReleaseCheckState = 'pass' | 'warn' | 'fail';

export interface ReleaseCheck {
  rule: ReleaseCheckRule;
  state: ReleaseCheckState;
  /** i18n key under `recipes.checklist.msg.*` for the human-readable detail. */
  messageKey: string;
  /** Interpolation params for the message key. */
  params: Record<string, string | number>;
}

export function evaluateReleaseChecklist(
  version: RecipeVersionDetailResponse,
  products: ProductResponse[],
  warehouseIds: Set<string>
): ReleaseCheck[] {
  return [
    checkOperations(version),
    checkOutputs(version),
    checkValidity(version),
    checkDependencies(version),
    checkProducts(version, products),
    checkWarehouses(version, warehouseIds)
  ];
}

export function hasBlockingCheck(checks: ReleaseCheck[]): boolean {
  return checks.some((c) => c.state === 'fail');
}

function checkOperations(version: RecipeVersionDetailResponse): ReleaseCheck {
  const count = version.operations.length;
  return count === 0
    ? { rule: 'operations', state: 'fail', messageKey: 'recipes.checklist.msg.operationsFail', params: {} }
    : {
        rule: 'operations',
        state: 'pass',
        messageKey: 'recipes.checklist.msg.operationsPass',
        params: { count }
      };
}

function checkOutputs(version: RecipeVersionDetailResponse): ReleaseCheck {
  const count = version.operations.reduce((sum, o) => sum + o.outputs.length, 0);
  return count === 0
    ? { rule: 'outputs', state: 'warn', messageKey: 'recipes.checklist.msg.outputsWarn', params: {} }
    : {
        rule: 'outputs',
        state: 'pass',
        messageKey: 'recipes.checklist.msg.outputsPass',
        params: { count }
      };
}

function checkValidity(version: RecipeVersionDetailResponse): ReleaseCheck {
  if (version.validFrom && version.validTo && version.validFrom > version.validTo) {
    return {
      rule: 'validity',
      state: 'fail',
      messageKey: 'recipes.checklist.msg.validityFail',
      params: {}
    };
  }
  return { rule: 'validity', state: 'pass', messageKey: 'recipes.checklist.msg.validityPass', params: {} };
}

function checkDependencies(version: RecipeVersionDetailResponse): ReleaseCheck {
  const opIds = new Set(version.operations.map((o) => o.id));

  for (const op of version.operations) {
    for (const dep of op.dependencies) {
      if (dep.predecessorOperationId === op.id) {
        return {
          rule: 'dependencies',
          state: 'fail',
          messageKey: 'recipes.checklist.msg.dependenciesFailSelf',
          params: { code: op.code }
        };
      }
      if (!opIds.has(dep.predecessorOperationId)) {
        return {
          rule: 'dependencies',
          state: 'fail',
          messageKey: 'recipes.checklist.msg.dependenciesFailUnknown',
          params: { code: op.code }
        };
      }
    }
  }

  // DFS cycle detection over predecessor -> successor edges.
  const successors = new Map<string, string[]>();
  for (const op of version.operations) {
    for (const dep of op.dependencies) {
      const list = successors.get(dep.predecessorOperationId) ?? [];
      list.push(op.id);
      successors.set(dep.predecessorOperationId, list);
    }
  }
  const state = new Map<string, number>();
  const visiting = 1;
  const visited = 2;
  const dfs = (node: string): boolean => {
    state.set(node, visiting);
    for (const next of successors.get(node) ?? []) {
      const s = state.get(next);
      if (s === undefined) {
        if (dfs(next)) return true;
      } else if (s === visiting) {
        return true;
      }
    }
    state.set(node, visited);
    return false;
  };
  for (const op of version.operations) {
    if (state.get(op.id) === undefined && dfs(op.id)) {
      return {
        rule: 'dependencies',
        state: 'fail',
        messageKey: 'recipes.checklist.msg.dependenciesFailCycle',
        params: {}
      };
    }
  }

  return {
    rule: 'dependencies',
    state: 'pass',
    messageKey: 'recipes.checklist.msg.dependenciesPass',
    params: {}
  };
}

function checkProducts(
  version: RecipeVersionDetailResponse,
  products: ProductResponse[]
): ReleaseCheck {
  const byId = new Map(products.map((p) => [p.id, p]));
  const referenced = new Set<string>();
  for (const op of version.operations) {
    for (const item of op.bomItems) referenced.add(item.productId);
    for (const out of op.outputs) referenced.add(out.productId);
  }

  const inactive: string[] = [];
  let unverified = 0;
  for (const id of referenced) {
    const product = byId.get(id);
    if (!product) {
      unverified++;
    } else if (!product.isActive) {
      inactive.push(`${product.code}`);
    }
  }

  if (inactive.length > 0) {
    return {
      rule: 'products',
      state: 'fail',
      messageKey: 'recipes.checklist.msg.productsFail',
      params: { detail: inactive.join(', ') }
    };
  }
  if (unverified > 0) {
    return {
      rule: 'products',
      state: 'warn',
      messageKey: 'recipes.checklist.msg.productsWarn',
      params: { count: unverified }
    };
  }
  return { rule: 'products', state: 'pass', messageKey: 'recipes.checklist.msg.productsPass', params: {} };
}

function checkWarehouses(
  version: RecipeVersionDetailResponse,
  warehouseIds: Set<string>
): ReleaseCheck {
  let unset = 0;
  let unknown = 0;
  for (const op of version.operations) {
    const refs = [
      ...op.bomItems.map((b) => b.preferredWarehouseId),
      ...op.outputs.map((o) => o.preferredWarehouseId)
    ];
    for (const ref of refs) {
      if (!ref) unset++;
      else if (!warehouseIds.has(ref)) unknown++;
    }
  }

  // Unknown ids warn (not fail): the loaded warehouse lookup may be capped,
  // so only the server can decide authoritatively.
  if (unset > 0 || unknown > 0) {
    return {
      rule: 'warehouses',
      state: 'warn',
      messageKey: 'recipes.checklist.msg.warehousesWarn',
      params: { unset, unknown }
    };
  }
  return {
    rule: 'warehouses',
    state: 'pass',
    messageKey: 'recipes.checklist.msg.warehousesPass',
    params: {}
  };
}
