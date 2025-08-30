export interface NavItem {
  label: string;
  icon: string;
  route?: string;
  children?: NavItem[];
}

export const sitemap: NavItem[] = [
  { label: 'Dashboard', icon: 'pi pi-home', route: '/dashboard' },
  { label: 'Production Orders', icon: 'pi pi-box', route: '/production-orders' },
  { label: 'Production Recipes', icon: 'pi pi-book', route: '/production-recipes' },
  { label: 'Schedule', icon: 'pi pi-calendar', route: '/schedule' },
  { label: 'Report', icon: 'pi pi-chart-bar', route: '/report' },
  {
    label: 'Configuration',
    icon: 'pi pi-cog',
    children: [
      { label: 'Warehouses', icon: 'pi pi-building', route: '/configuration/warehouses' },
      { label: 'Departments', icon: 'pi pi-sitemap', route: '/configuration/departments' }
    ]
  }
];
