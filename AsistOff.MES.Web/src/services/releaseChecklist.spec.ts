import { describe, expect, it } from 'vitest';
import {
  evaluateReleaseChecklist,
  hasBlockingCheck,
  type ReleaseCheck
} from './releaseChecklist';
import type { ProductResponse } from './productService';
import {
  BomQuantityType,
  ConsumptionTiming,
  OperationDependencyType,
  OperationOutputType,
  RunTimeMode,
  type OperationNodeDto,
  type RecipeVersionDetailResponse
} from './recipeVersionService';

function op(
  id: string,
  code: string,
  overrides: Partial<OperationNodeDto> = {}
): OperationNodeDto {
  return {
    id,
    recipeVersionId: 'v1',
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

function version(operations: OperationNodeDto[], extra = {}): RecipeVersionDetailResponse {
  return {
    id: 'v1',
    recipeId: 'r1',
    versionNumber: 1,
    status: 1,
    releasedAt: null,
    validFrom: null,
    validTo: null,
    changeNotes: null,
    operations,
    ...extra
  };
}

function product(id: string, isActive: boolean): ProductResponse {
  return {
    id,
    code: `P-${id}`,
    name: `Product ${id}`,
    scanBy: 2,
    isActive
  };
}

function checkOf(checks: ReleaseCheck[], rule: string): ReleaseCheck {
  const found = checks.find((c) => c.rule === rule);
  expect(found).toBeDefined();
  return found as ReleaseCheck;
}

describe('evaluateReleaseChecklist', () => {
  it('fails operations for an empty version and blocks release', () => {
    const checks = evaluateReleaseChecklist(version([]), [], new Set());

    expect(checkOf(checks, 'operations').state).toBe('fail');
    expect(hasBlockingCheck(checks)).toBe(true);
  });

  it('passes everything for a healthy version', () => {
    const v = version([
      op('op1', 'OP-10', {
        bomItems: [
          {
            id: 'b1',
            productId: 'p1',
            measureUnitId: null,
            quantity: 2,
            quantityType: BomQuantityType.PerUnit,
            scrapPercentage: null,
            isOptional: false,
            preferredWarehouseId: 'w1',
            consumptionTiming: ConsumptionTiming.OnCompletion,
            notes: null,
            sortIndex: 0
          }
        ],
        outputs: [
          {
            id: 'o1',
            productId: 'p1',
            measureUnitId: null,
            quantity: 1,
            quantityType: BomQuantityType.PerUnit,
            outputType: OperationOutputType.Product,
            preferredWarehouseId: 'w1',
            notes: null,
            sortIndex: 0
          }
        ]
      })
    ]);

    const checks = evaluateReleaseChecklist(v, [product('p1', true)], new Set(['w1']));

    expect(checks.every((c) => c.state === 'pass')).toBe(true);
    expect(hasBlockingCheck(checks)).toBe(false);
  });

  it('warns (not fail) when no outputs are defined', () => {
    const checks = evaluateReleaseChecklist(version([op('op1', 'OP-10')]), [], new Set());

    expect(checkOf(checks, 'outputs').state).toBe('warn');
    expect(hasBlockingCheck(checks)).toBe(false);
  });

  it('fails validity when ValidFrom is later than ValidTo', () => {
    const v = version([op('op1', 'OP-10')], {
      validFrom: '2026-06-01T00:00:00Z',
      validTo: '2026-01-01T00:00:00Z'
    });

    const checks = evaluateReleaseChecklist(v, [], new Set());

    expect(checkOf(checks, 'validity').state).toBe('fail');
    expect(hasBlockingCheck(checks)).toBe(true);
  });

  it('fails dependencies on a cycle', () => {
    const a = op('a', 'OP-A');
    const b = op('b', 'OP-B');
    a.dependencies = [
      { predecessorOperationId: 'b', dependencyType: OperationDependencyType.FinishToStart, lagMinutes: null }
    ];
    b.dependencies = [
      { predecessorOperationId: 'a', dependencyType: OperationDependencyType.FinishToStart, lagMinutes: null }
    ];

    const checks = evaluateReleaseChecklist(version([a, b]), [], new Set());

    expect(checkOf(checks, 'dependencies').state).toBe('fail');
    expect(hasBlockingCheck(checks)).toBe(true);
  });

  it('fails dependencies on a self reference', () => {
    const a = op('a', 'OP-A');
    a.dependencies = [
      { predecessorOperationId: 'a', dependencyType: OperationDependencyType.FinishToStart, lagMinutes: null }
    ];

    const checks = evaluateReleaseChecklist(version([a]), [], new Set());

    expect(checkOf(checks, 'dependencies').state).toBe('fail');
  });

  it('fails products for a known-inactive product', () => {
    const v = version([
      op('op1', 'OP-10', {
        outputs: [
          {
            id: 'o1',
            productId: 'p1',
            measureUnitId: null,
            quantity: 1,
            quantityType: BomQuantityType.PerUnit,
            outputType: OperationOutputType.Product,
            preferredWarehouseId: null,
            notes: null,
            sortIndex: 0
          }
        ]
      })
    ]);

    const checks = evaluateReleaseChecklist(v, [product('p1', false)], new Set());

    expect(checkOf(checks, 'products').state).toBe('fail');
    expect(hasBlockingCheck(checks)).toBe(true);
  });

  it('warns products when a referenced product is outside the loaded lookup', () => {
    const v = version([
      op('op1', 'OP-10', {
        outputs: [
          {
            id: 'o1',
            productId: 'p-missing',
            measureUnitId: null,
            quantity: 1,
            quantityType: BomQuantityType.PerUnit,
            outputType: OperationOutputType.Product,
            preferredWarehouseId: null,
            notes: null,
            sortIndex: 0
          }
        ]
      })
    ]);

    const checks = evaluateReleaseChecklist(v, [], new Set());

    // Unknown locally must not block — the server decides authoritatively.
    expect(checkOf(checks, 'products').state).toBe('warn');
    expect(hasBlockingCheck(checks)).toBe(false);
  });

  it('warns warehouses when preferred warehouses are unset', () => {
    const v = version([
      op('op1', 'OP-10', {
        outputs: [
          {
            id: 'o1',
            productId: 'p1',
            measureUnitId: null,
            quantity: 1,
            quantityType: BomQuantityType.PerUnit,
            outputType: OperationOutputType.Product,
            preferredWarehouseId: null,
            notes: null,
            sortIndex: 0
          }
        ]
      })
    ]);

    const checks = evaluateReleaseChecklist(v, [product('p1', true)], new Set());

    expect(checkOf(checks, 'warehouses').state).toBe('warn');
    expect(hasBlockingCheck(checks)).toBe(false);
  });
});
