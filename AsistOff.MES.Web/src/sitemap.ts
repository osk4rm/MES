export interface NavItem {
  /** i18n key or literal label */
  label: string;
  icon: string;
  route?: string;
  children?: NavItem[];
}

export const sitemap: NavItem[] = [
  { label: 'nav.dashboard', icon: 'pi pi-chart-pie', route: '/dashboard' },
  {
    label: 'nav.production',
    // Factory mark (issue #337): the gear was shared with the Machines leaf
    // and the Settings group, so the Production group gets its own icon.
    icon: 'pi pi-factory',
    // Base/overview route (issue #314): the group highlights while any
    // descendant — including detail pages with no own entry — is active.
    route: '/production',
    // Workflow order (issue #337): plan (orders → recipes → kanban) then
    // track (lots → SPC → scrap → downtime → Andon) then monitor
    // (telemetry stack). The dispatch board moved to the Schedule group.
    children: [
      { label: 'nav.productionOrders', icon: 'pi pi-list', route: '/production/orders' },
      // Operator panel (issue #336): touch-friendly shift queue for operators.
      { label: 'nav.operatorPanel', icon: 'pi pi-tablet', route: '/production/operator-panel' },
      { label: 'nav.productionRecipes', icon: 'pi pi-book', route: '/production/recipes' },
      { label: 'nav.productionKanban', icon: 'pi pi-th-large', route: '/production/kanban' },
      { label: 'nav.productionLots', icon: 'pi pi-box', route: '/production/lots' },
      { label: 'nav.spcCharacteristics', icon: 'pi pi-chart-line', route: '/production/spc-characteristics' },
      { label: 'nav.productionScrap', icon: 'pi pi-trash', route: '/production/scrap' },
      { label: 'nav.productionDowntime', icon: 'pi pi-pause-circle', route: '/production/downtime' },
      { label: 'nav.productionAndon', icon: 'pi pi-bell', route: '/production/andon' },
      { label: 'nav.productionTelemetry', icon: 'pi pi-wave-pulse', route: '/production/telemetry' },
      { label: 'nav.productionOpcUaConnections', icon: 'pi pi-link', route: '/production/opcua-connections' }
    ]
  },
  {
    label: 'nav.schedule',
    icon: 'pi pi-calendar',
    // Schedule group (issue #337): the Gantt view and the dispatch board
    // share the /schedule/* path prefix but used to live in different nav
    // areas (top-level leaf vs Production child). Both now sit under one
    // group so the breadcrumb trail and the highlight agree with the URL.
    // The group itself has no overview route: the Gantt leaf owns `/schedule`
    // so the published map keeps one row per path and `/schedule` resolves
    // to the canonical trail [nav.schedule, nav.gantt].
    children: [
      { label: 'nav.gantt', icon: 'pi pi-bars', route: '/schedule' },
      { label: 'nav.dispatchBoard', icon: 'pi pi-truck', route: '/schedule/dispatch' }
    ]
  },
  {
    label: 'nav.reports',
    icon: 'pi pi-chart-bar',
    route: '/reports',
    children: [
      { label: 'nav.oeeDashboard', icon: 'pi pi-chart-bar', route: '/reports/oee' },
      { label: 'nav.reliabilityDashboard', icon: 'pi pi-wrench', route: '/reports/reliability' },
      // Desktop mark (issue #337): the chart-line icon was shared with the
      // SPC characteristics leaf. Placement (issue #382, F-16): every
      // dashboard lives under Reports, so KPI (OEE/reliability) versus live
      // telemetry is one group instead of a Production/Reports split.
      { label: 'nav.productionTelemetryDashboard', icon: 'pi pi-desktop', route: '/reports/telemetry' }
    ]
  },
  {
    label: 'nav.configuration',
    icon: 'pi pi-sliders-h',
    route: '/configuration',
    children: [
      // Tag mark (issue #337): the box icon was shared with the Lots leaf.
      { label: 'nav.products', icon: 'pi pi-tag', route: '/configuration/products' },
      { label: 'nav.productGroups', icon: 'pi pi-tags', route: '/configuration/product-groups' },
      { label: 'nav.measureUnits', icon: 'pi pi-percentage', route: '/configuration/measure-units' },
      { label: 'nav.warehouses', icon: 'pi pi-building', route: '/configuration/warehouses' },
      { label: 'nav.departments', icon: 'pi pi-sitemap', route: '/configuration/departments' },
      { label: 'nav.machines', icon: 'pi pi-cog', route: '/configuration/machines' },
      { label: 'nav.operators', icon: 'pi pi-id-card', route: '/configuration/operators' },
      { label: 'nav.skills', icon: 'pi pi-star', route: '/configuration/skills' },
      { label: 'nav.shifts', icon: 'pi pi-clock', route: '/configuration/shifts' },
      { label: 'nav.reasonCodes', icon: 'pi pi-exclamation-circle', route: '/configuration/reason-codes' },
      { label: 'nav.operationTemplates', icon: 'pi pi-copy', route: '/configuration/operation-templates' },
      { label: 'nav.maintenance', icon: 'pi pi-wrench', route: '/configuration/maintenance' },
      { label: 'nav.maintenancePlans', icon: 'pi pi-calendar-clock', route: '/configuration/maintenance-plans' }
    ]
  },
  {
    label: 'nav.settings',
    icon: 'pi pi-cog',
    // The `/settings` overview stub (ComingSoonView) has no own leaf entry;
    // the base route keeps the group highlighted while it is open.
    route: '/settings',
    children: [
      { label: 'nav.roles', icon: 'pi pi-lock', route: '/settings/roles' }
    ]
  }
];
