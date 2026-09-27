import type { OperationNodeDto, RecipeVersionDetailResponse } from './recipeVersionService';

/**
 * Version compare for recipes (issue #388, finding R-8). Pure diff between
 * two version-detail responses — no backend call; the detail view already
 * loads both versions.
 *
 * Operations are matched by `code` (cloning mints new ids, codes stay
 * stable along the lineage). BOM items and outputs are matched by
 * `productId`, resource requirements by `requiredCapability ?? id`.
 */
export interface FieldChange {
  field: string;
  before: string;
  after: string;
}

export interface ComparedOperation {
  code: string;
  name: string;
}

export interface OperationChange extends ComparedOperation {
  /** Scalar operation fields (name, sort order, timings, flags). */
  fields: FieldChange[];
  /** BOM deltas keyed by product label. */
  bom: FieldChange[];
  /** Output deltas keyed by product label. */
  outputs: FieldChange[];
  /** Resource requirement deltas keyed by capability. */
  resources: FieldChange[];
  /** Predecessor-code set change, if any. */
  dependencies: FieldChange[];
}

export interface VersionCompareResult {
  added: ComparedOperation[];
  removed: ComparedOperation[];
  changed: OperationChange[];
  unchanged: ComparedOperation[];
}

const MISSING = '—';

function num(value: number | null | undefined): string {
  return value === null || value === undefined ? MISSING : String(value);
}

function bool(value: boolean): string {
  return value ? '✓' : MISSING;
}

function scalarFields(a: OperationNodeDto, b: OperationNodeDto): FieldChange[] {
  const fields: FieldChange[] = [];
  const push = (field: string, before: string, after: string) => {
    if (before !== after) fields.push({ field, before, after });
  };
  push('name', a.name, b.name);
  push('sortIndex', String(a.sortIndex), String(b.sortIndex));
  push('operationType', a.operationType ?? MISSING, b.operationType ?? MISSING);
  push('setupTimeMinutes', num(a.setupTimeMinutes), num(b.setupTimeMinutes));
  push('runTimePerUnitSeconds', num(a.runTimePerUnitSeconds), num(b.runTimePerUnitSeconds));
  push('runTimePerBatchMinutes', num(a.runTimePerBatchMinutes), num(b.runTimePerBatchMinutes));
  push('teardownTimeMinutes', num(a.teardownTimeMinutes), num(b.teardownTimeMinutes));
  push('queueTimeMinutes', num(a.queueTimeMinutes), num(b.queueTimeMinutes));
  push('isOptional', bool(a.isOptional), bool(b.isOptional));
  push('allowParallelExecution', bool(a.allowParallelExecution), bool(b.allowParallelExecution));
  push('expectedQuantity', num(a.expectedQuantity), num(b.expectedQuantity));
  return fields;
}

/**
 * @param labelOf resolves a product id to a display label (e.g. `code — name`).
 */
export function compareRecipeVersions(
  a: RecipeVersionDetailResponse,
  b: RecipeVersionDetailResponse,
  labelOf: (productId: string) => string = (id) => id
): VersionCompareResult {
  const byCodeA = new Map(a.operations.map((o) => [o.code, o]));
  const byCodeB = new Map(b.operations.map((o) => [o.code, o]));
  const codeOfA = new Map(a.operations.map((o) => [o.id, o.code] as const));
  const codeOfB = new Map(b.operations.map((o) => [o.id, o.code] as const));

  const added: ComparedOperation[] = [];
  const removed: ComparedOperation[] = [];
  const changed: OperationChange[] = [];
  const unchanged: ComparedOperation[] = [];

  for (const op of a.operations) {
    if (!byCodeB.has(op.code)) removed.push({ code: op.code, name: op.name });
  }
  for (const op of b.operations) {
    const counterpart = byCodeA.get(op.code);
    if (!counterpart) {
      added.push({ code: op.code, name: op.name });
      continue;
    }
    const change = diffOperation(
      counterpart,
      op,
      labelOf,
      (id) => codeOfA.get(id) ?? id,
      (id) => codeOfB.get(id) ?? id
    );
    if (
      change.fields.length === 0 &&
      change.bom.length === 0 &&
      change.outputs.length === 0 &&
      change.resources.length === 0 &&
      change.dependencies.length === 0
    ) {
      unchanged.push({ code: op.code, name: op.name });
    } else {
      changed.push(change);
    }
  }

  return { added, removed, changed, unchanged };
}

function diffOperation(
  a: OperationNodeDto,
  b: OperationNodeDto,
  labelOf: (productId: string) => string,
  codeOfA: (id: string) => string,
  codeOfB: (id: string) => string
): OperationChange {
  return {
    code: b.code,
    name: b.name,
    fields: scalarFields(a, b),
    bom: diffBom(a, b, labelOf),
    outputs: diffOutputs(a, b, labelOf),
    resources: diffResources(a, b),
    dependencies: diffDependencies(a, b, codeOfA, codeOfB)
  };
}

function describeQuantity(quantity: number, quantityType: number): string {
  return `${quantity} (t${quantityType})`;
}

function diffBom(
  a: OperationNodeDto,
  b: OperationNodeDto,
  labelOf: (productId: string) => string
): FieldChange[] {
  const changes: FieldChange[] = [];
  const byProductA = new Map(a.bomItems.map((i) => [i.productId, i]));
  const byProductB = new Map(b.bomItems.map((i) => [i.productId, i]));

  for (const [productId, item] of byProductA) {
    if (!byProductB.has(productId)) {
      changes.push({
        field: `bom:${labelOf(productId)}`,
        before: describeQuantity(item.quantity, item.quantityType),
        after: MISSING
      });
    }
  }
  for (const [productId, item] of byProductB) {
    const prev = byProductA.get(productId);
    const next = describeQuantity(item.quantity, item.quantityType);
    if (!prev) {
      changes.push({ field: `bom:${labelOf(productId)}`, before: MISSING, after: next });
    } else {
      const before = describeQuantity(prev.quantity, prev.quantityType);
      if (before !== next) {
        changes.push({ field: `bom:${labelOf(productId)}`, before, after: next });
      }
    }
  }
  return changes;
}

function diffOutputs(
  a: OperationNodeDto,
  b: OperationNodeDto,
  labelOf: (productId: string) => string
): FieldChange[] {
  const changes: FieldChange[] = [];
  const byProductA = new Map(a.outputs.map((o) => [o.productId, o]));
  const byProductB = new Map(b.outputs.map((o) => [o.productId, o]));

  for (const [productId, out] of byProductA) {
    if (!byProductB.has(productId)) {
      changes.push({
        field: `output:${labelOf(productId)}`,
        before: describeQuantity(out.quantity, out.quantityType),
        after: MISSING
      });
    }
  }
  for (const [productId, out] of byProductB) {
    const prev = byProductA.get(productId);
    const next = describeQuantity(out.quantity, out.quantityType);
    if (!prev) {
      changes.push({ field: `output:${labelOf(productId)}`, before: MISSING, after: next });
    } else {
      const before = describeQuantity(prev.quantity, prev.quantityType);
      if (before !== next) {
        changes.push({ field: `output:${labelOf(productId)}`, before, after: next });
      }
    }
  }
  return changes;
}

function diffResources(a: OperationNodeDto, b: OperationNodeDto): FieldChange[] {
  const changes: FieldChange[] = [];
  const keyOf = (r: { requiredCapability?: string | null; id: string }) =>
    r.requiredCapability && r.requiredCapability.length > 0 ? r.requiredCapability : `#${r.id}`;
  const byKeyA = new Map(a.resourceRequirements.map((r) => [keyOf(r), r]));
  const byKeyB = new Map(b.resourceRequirements.map((r) => [keyOf(r), r]));

  for (const [key, r] of byKeyA) {
    if (!byKeyB.has(key)) {
      changes.push({
        field: `resource:${key}`,
        before: `${r.requiredOperatorCount}×${r.requiredRole ?? MISSING}`,
        after: MISSING
      });
    }
  }
  for (const [key, r] of byKeyB) {
    const prev = byKeyA.get(key);
    const next = `${r.requiredOperatorCount}×${r.requiredRole ?? MISSING}`;
    if (!prev) {
      changes.push({ field: `resource:${key}`, before: MISSING, after: next });
    } else {
      const before = `${prev.requiredOperatorCount}×${prev.requiredRole ?? MISSING}`;
      if (before !== next) changes.push({ field: `resource:${key}`, before, after: next });
    }
  }
  return changes;
}

function predecessorCodes(
  op: OperationNodeDto,
  codeOf: (id: string) => string
): string[] {
  return op.dependencies.map((d) => codeOf(d.predecessorOperationId)).sort();
}

function diffDependencies(
  a: OperationNodeDto,
  b: OperationNodeDto,
  codeOfA: (id: string) => string,
  codeOfB: (id: string) => string
): FieldChange[] {
  const before = predecessorCodes(a, codeOfA);
  const after = predecessorCodes(b, codeOfB);
  if (before.join('|') === after.join('|')) return [];
  return [
    {
      field: 'dependencies',
      before: before.length > 0 ? before.join(', ') : MISSING,
      after: after.length > 0 ? after.join(', ') : MISSING
    }
  ];
}
