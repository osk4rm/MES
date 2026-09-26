import { describe, expect, it } from 'vitest';

// Guards the E2E_FAIL on PR #277 (issue #274): lookup browses in views and
// shared components must respect the backend Browse*Request MaxPageSize caps.
// A pre-existing `pageSize: 500` product lookup made GET /api/products return
// 400 once BrowseProductsRequestValidator capped PageSize at 100, surfacing
// an error toast on /production/recipes. These tests scan the Vue sources so
// a future lookup that exceeds its cap fails in CI instead of in the browser.

// Server caps mirror the backend Browse*Request.MaxPageSize values.
const serviceCaps: Record<string, number> = {
  productService: 100,
  productGroupService: 100,
  machineService: 100,
  operatorService: 100,
  warehouseService: 100,
  skillService: 100,
  recipeService: 100,
  measureUnitService: 100,
  reasonCodeService: 100,
  productionOrderService: 100,
  operationTemplateService: 200,
  lotService: 200,
  shiftService: 100,
  departmentService: 100,
  operatorShiftAssignmentService: 100,
  andonSignalService: 200,
  spcMeasurementService: 200,
  spcCharacteristicService: 100,
  maintenanceWorkOrderService: 100,
  machineTelemetryTagService: 200,
  telemetryReadingService: 200,
  opcUaConnectionService: 200,
  kanbanService: 200,
  downtimeEventService: 200,
  scrapEventService: 200,
  productionConfirmationService: 200,
  auditEventService: 100
};

// Backend maximum across all browse endpoints; unknown services must still
// stay within it.
const globalCeiling = 200;

// Raw Vue sources via Vite (no node:fs so vue-tsc stays happy without
// @types/node). Paths are relative to this file in src/services.
const viewSources = import.meta.glob<string>('../views/**/*.vue', {
  eager: true,
  query: '?raw',
  import: 'default'
});
const componentSources = import.meta.glob<string>('../components/**/*.vue', {
  eager: true,
  query: '?raw',
  import: 'default'
});

interface BrowseCall {
  file: string;
  service: string;
  pageSizeText: string;
}

function allSources(): Array<[string, string]> {
  return [...Object.entries(viewSources), ...Object.entries(componentSources)];
}

function collectBrowseCalls(): BrowseCall[] {
  const calls: BrowseCall[] = [];
  // Matches `<service>.browse({...})` and `<service>.browseLoops({...})`
  // across line breaks; the argument is a flat object literal.
  const browsePattern = /(\w+)\.browse\w*\(\s*\{([^}]*)\}/gs;
  const pageSizePattern = /pageSize\s*:\s*([A-Za-z_][A-Za-z0-9_]*|\d+)/;
  for (const [file, text] of allSources()) {
    for (const match of text.matchAll(browsePattern)) {
      const service: string = match[1] ?? '';
      const args: string = match[2] ?? '';
      const pageSizeMatch = pageSizePattern.exec(args);
      if (pageSizeMatch !== null && pageSizeMatch[1] !== undefined) {
        calls.push({ file, service, pageSizeText: pageSizeMatch[1] });
      }
    }
  }
  return calls;
}

function sourceByFile(file: string): string {
  return viewSources[file] ?? componentSources[file] ?? '';
}

function resolvePageSize(file: string, text: string): number | null {
  const numeric = Number(text);
  if (Number.isInteger(numeric)) {
    return numeric;
  }
  // Named constant (e.g. LOOP_PAGE_SIZE): resolve `const NAME = 123` in the
  // same file so constant-driven lookups are pinned too.
  const source = sourceByFile(file);
  const constPattern = new RegExp(`const\\s+${text}\\s*[:=][^;\\n]*?([0-9]+)`);
  const found = constPattern.exec(source);
  if (found !== null && found[1] !== undefined) {
    return Number(found[1]);
  }
  return null;
}

describe('lookup browse pageSize caps (issue #274 E2E_FAIL regression)', () => {
  it('has no pageSize: 500 lookup remaining in views or components', () => {
    const offenders: string[] = [];
    for (const [file, text] of allSources()) {
      if (/pageSize\s*:\s*500\b/.test(text)) {
        offenders.push(file);
      }
    }
    expect(offenders).toEqual([]);
  });

  it('keeps every lookup browse within its server MaxPageSize', () => {
    const calls = collectBrowseCalls();
    // The PR touches a dozen views; an empty scan would make this vacuous.
    expect(calls.length).toBeGreaterThan(0);
    const violations: string[] = [];
    for (const call of calls) {
      const size = resolvePageSize(call.file, call.pageSizeText);
      if (size === null) {
        violations.push(`${call.file}: ${call.service} has unresolvable pageSize '${call.pageSizeText}'`);
        continue;
      }
      const cap: number = serviceCaps[call.service] ?? globalCeiling;
      if (size > cap) {
        violations.push(`${call.file}: ${call.service} pageSize ${size} exceeds cap ${cap}`);
      }
      if (size > globalCeiling) {
        violations.push(`${call.file}: ${call.service} pageSize ${size} exceeds global ceiling ${globalCeiling}`);
      }
    }
    expect(violations).toEqual([]);
  });
});
