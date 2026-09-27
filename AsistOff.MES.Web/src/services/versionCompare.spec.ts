import { describe, expect, it } from 'vitest';
import { compareRecipeVersions } from './versionCompare';
import {
  BomQuantityType,
  ConsumptionTiming,
  OperationDependencyType,
  OperationOutputType,
  RunTimeMode,
  type OperationNodeDto,
  type RecipeVersionDetailResponse
} from './recipeVersionService';

function op(id: string, code: string, overrides: Partial<OperationNodeDto> = {}): OperationNodeDto {
  return {
    id,
    recipeVersionId: 'v',
    code,
    name: code,
    description: null,
    operationType: null,
    sortIndex: 0,
    setupTimeMinutes: null,
    runTimeMode: RunTimeMode.PerUnitSeconds,
    runTimePerUnitSeconds: 30,
    runTimePerBatchMinutes: null,
    teardownTimeMinutes: null,
    queueTimeMinutes: null,
    isOptional: false,
    allowParallelExecution: false,
    expectedQuantity: null,
    dependencies: [],
    bomItems: [],
    outputs: [],
    resourceRequirements: [],
    ...overrides
  };
}

function version(id: string, n: number, operations: OperationNodeDto[]): RecipeVersionDetailResponse {
  return {
    id,
    recipeId: 'r1',
    versionNumber: n,
    status: 2,
    releasedAt: null,
    validFrom: null,
    validTo: null,
    changeNotes: null,
    operations
  };
}

describe('compareRecipeVersions', () => {
  it('reports added, removed and unchanged operations matched by code', () => {
    const a = version('va', 1, [op('a1', 'OP-10'), op('a2', 'OP-20')]);
    const b = version('vb', 2, [op('b1', 'OP-10'), op('b3', 'OP-30')]);

    const result = compareRecipeVersions(a, b);

    expect(result.added.map((o) => o.code)).toEqual(['OP-30']);
    expect(result.removed.map((o) => o.code)).toEqual(['OP-20']);
    expect(result.unchanged.map((o) => o.code)).toEqual(['OP-10']);
    expect(result.changed).toHaveLength(0);
  });

  it('reports scalar timing changes on a kept operation', () => {
    const a = version('va', 1, [op('a1', 'OP-10', { runTimePerUnitSeconds: 30 })]);
    const b = version('vb', 2, [op('b1', 'OP-10', { runTimePerUnitSeconds: 45 })]);

    const result = compareRecipeVersions(a, b);

    expect(result.unchanged).toHaveLength(0);
    expect(result.changed).toHaveLength(1);
    const fields = result.changed[0].fields;
    expect(fields).toContainEqual({
      field: 'runTimePerUnitSeconds',
      before: '30',
      after: '45'
    });
  });

  it('reports BOM quantity changes and added materials', () => {
    const bomA = {
      id: 'b1',
      productId: 'p1',
      measureUnitId: null,
      quantity: 2,
      quantityType: BomQuantityType.PerUnit,
      scrapPercentage: null,
      isOptional: false,
      preferredWarehouseId: null,
      consumptionTiming: ConsumptionTiming.OnCompletion,
      notes: null,
      sortIndex: 0
    };
    const a = version('va', 1, [op('a1', 'OP-10', { bomItems: [bomA] })]);
    const b = version('vb', 2, [
      op('b1', 'OP-10', {
        bomItems: [
          { ...bomA, id: 'b2', quantity: 3 },
          { ...bomA, id: 'b3', productId: 'p2', quantity: 1 }
        ]
      })
    ]);

    const result = compareRecipeVersions(a, b, (id) => `label-${id}`);

    expect(result.changed).toHaveLength(1);
    const bom = result.changed[0].bom;
    expect(bom.some((c) => c.field === 'bom:label-p1' && c.before !== c.after)).toBe(true);
    expect(
      bom.some((c) => c.field === 'bom:label-p2' && c.before === '—' && c.after !== '—')
    ).toBe(true);
  });

  it('reports dependency predecessor changes', () => {
    const a = version('va', 1, [
      op('a1', 'OP-10'),
      op('a2', 'OP-20', {
        dependencies: [
          {
            predecessorOperationId: 'a1',
            dependencyType: OperationDependencyType.FinishToStart,
            lagMinutes: null
          }
        ]
      })
    ]);
    const b = version('vb', 2, [op('b1', 'OP-10'), op('b2', 'OP-20')]);

    const result = compareRecipeVersions(a, b);

    expect(result.changed).toHaveLength(1);
    expect(result.changed[0].dependencies).toHaveLength(1);
    expect(result.changed[0].dependencies[0].before).toContain('OP-10');
  });

  it('returns empty deltas for identical versions', () => {
    const ops = [op('a1', 'OP-10')];
    const result = compareRecipeVersions(version('va', 1, ops), version('vb', 2, ops));

    expect(result.added).toHaveLength(0);
    expect(result.removed).toHaveLength(0);
    expect(result.changed).toHaveLength(0);
    expect(result.unchanged.map((o) => o.code)).toEqual(['OP-10']);
  });

  it('reports removed outputs on a kept operation', () => {
    const out = {
      id: 'o1',
      productId: 'p1',
      measureUnitId: null,
      quantity: 1,
      quantityType: BomQuantityType.PerUnit,
      outputType: OperationOutputType.Product,
      preferredWarehouseId: null,
      notes: null,
      sortIndex: 0
    };
    const a = version('va', 1, [op('a1', 'OP-10', { outputs: [out] })]);
    const b = version('vb', 2, [op('b1', 'OP-10')]);

    const result = compareRecipeVersions(a, b);

    expect(result.changed).toHaveLength(1);
    expect(result.changed[0].outputs).toHaveLength(1);
    expect(result.changed[0].outputs[0].after).toBe('—');
  });
});
