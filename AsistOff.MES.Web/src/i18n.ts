import { createI18n } from 'vue-i18n';

const pl = {
  common: {
    cancel: 'Anuluj',
    confirm: 'Potwierdź',
    save: 'Zapisz',
    create: 'Utwórz',
    update: 'Aktualizuj',
    delete: 'Usuń',
    edit: 'Edytuj',
    close: 'Zamknij',
    add: 'Dodaj',
    refresh: 'Odśwież',
    search: 'Szukaj…',
    filter: 'Filtruj',
    clear: 'Wyczyść',
    loading: 'Ładowanie…',
    noData: 'Brak danych',
    yes: 'Tak',
    no: 'Nie',
    active: 'Aktywny',
    inactive: 'Nieaktywny',
    actions: 'Akcje',
    optional: 'opcjonalne',
    required: 'wymagane',
    back: 'Wróć',
    open: 'Otwórz',
    empty: 'Brak wpisów',
    status: 'Status',
    notFound: 'Nie znaleziono',
    signedIn: 'Zalogowany',
    signOut: 'Wyloguj',
    notifications: 'Powiadomienia',
    pagination: {
      showing: 'Pokazano {from}–{to} z {total}',
      pageSize: 'Wierszy:'
    }
  },
  nav: {
    dashboard: 'Pulpit',
    production: 'Produkcja',
    productionOrders: 'Zlecenia produkcyjne',
    productionRecipes: 'Receptury',
    schedule: 'Harmonogram',
    reports: 'Raporty',
    configuration: 'Konfiguracja',
    products: 'Produkty',
    productGroups: 'Grupy produktów',
    measureUnits: 'Jednostki miary',
    warehouses: 'Magazyny',
    departments: 'Działy',
    machines: 'Maszyny',
    shifts: 'Zmiany',
    operators: 'Operatorzy',
    skills: 'Umiejętności',
    reasonCodes: 'Kody przyczyn',
    operationTemplates: 'Wzorcowe operacje',
    settings: 'Ustawienia'
  },
  auth: {
    signInTitle: 'Logowanie do AsistOff MES',
    signInSubtitle: 'Wpisz dane aby kontynuować',
    email: 'Adres e-mail',
    password: 'Hasło',
    signIn: 'Zaloguj się',
    signInSuccess: 'Zalogowano pomyślnie',
    signInError: 'Nie udało się zalogować',
    signUpTitle: 'Utwórz organizację',
    signUpSubtitle: 'Załóż nową organizację i konto administratora',
    tenantName: 'Nazwa wewnętrzna',
    tenantDisplayName: 'Nazwa wyświetlana',
    contactEmail: 'E-mail kontaktowy',
    confirmPassword: 'Powtórz hasło',
    register: 'Zarejestruj organizację',
    backToLogin: 'Wróć do logowania',
    goToRegister: 'Utwórz organizację',
    passwordsMismatch: 'Hasła nie są identyczne',
    registerSuccess: 'Organizacja utworzona. Zaloguj się.',
    registerError: 'Nie udało się utworzyć organizacji'
  },
  dashboard: {
    title: 'Pulpit',
    subtitle: 'Monitoring kluczowych wskaźników produkcji',
    placeholderNote: 'Wskaźniki są symulowane — zostaną podłączone do backendu po udostępnieniu endpointów metryk.',
    activeOrders: 'Aktywne zlecenia',
    oee: 'OEE',
    operatorsOnline: 'Operatorzy on-line',
    warehouses: 'Magazyny'
  },
  products: {
    title: 'Produkty',
    subtitle: 'Zarządzaj katalogiem produktów',
    create: 'Nowy produkt',
    code: 'Kod',
    name: 'Nazwa',
    description: 'Opis',
    ean: 'EAN',
    barcode: 'Kod kreskowy',
    scanBy: 'Sposób skanowania',
    group: 'Grupa',
    isActive: 'Aktywny',
    syncId: 'Sync ID',
    filters: { code: 'Kod zawiera…', name: 'Nazwa zawiera…', group: 'Grupa', isActive: 'Stan' }
  },
  productGroups: {
    title: 'Grupy produktów',
    subtitle: 'Hierarchia grup katalogu',
    create: 'Nowa grupa',
    code: 'Kod',
    name: 'Nazwa',
    description: 'Opis',
    parent: 'Grupa nadrzędna',
    isActive: 'Aktywna',
    syncId: 'Sync ID'
  },
  measureUnits: {
    title: 'Jednostki miary',
    subtitle: 'Jednostki i współczynniki konwersji',
    create: 'Nowa jednostka',
    name: 'Nazwa',
    symbol: 'Symbol',
    type: 'Typ',
    baseUnit: 'Jednostka bazowa',
    conversionFactor: 'Współczynnik',
    isActive: 'Aktywna',
    description: 'Opis',
    syncId: 'Sync ID',
    types: { 1: 'Produkt' }
  },
  warehouses: {
    title: 'Magazyny',
    subtitle: 'Konfiguracja lokalizacji magazynowych',
    create: 'Nowy magazyn',
    name: 'Nazwa',
    syncId: 'Sync ID',
    filters: { name: 'Nazwa zawiera…' }
  },
  departments: {
    title: 'Działy',
    subtitle: 'Struktura organizacyjna działów',
    create: 'Nowy dział',
    code: 'Kod',
    name: 'Nazwa',
    filters: { code: 'Kod zawiera…', name: 'Nazwa zawiera…' }
  },
  operators: {
    title: 'Operatorzy',
    subtitle: 'Osoby wykonujące pracę na hali',
    create: 'Nowy operator',
    identifier: 'Identyfikator',
    firstName: 'Imię',
    lastName: 'Nazwisko',
    ratePerHour: 'Stawka / h',
    department: 'Dział',
    filters: {
      identifier: 'Identyfikator…',
      firstName: 'Imię…',
      lastName: 'Nazwisko…',
      rateFrom: 'Stawka od',
      rateTo: 'Stawka do',
      department: 'Dział'
    }
  },
  machines: {
    title: 'Maszyny',
    subtitle: 'Wyposażenie na hali produkcyjnej',
    create: 'Nowa maszyna',
    code: 'Kod',
    name: 'Nazwa',
    description: 'Opis',
    calendar: 'Kalendarz',
    filters: { code: 'Kod zawiera…', name: 'Nazwa zawiera…' }
  },
  shifts: {
    title: 'Zmiany',
    subtitle: 'Słownik zmian roboczych',
    create: 'Nowa zmiana',
    code: 'Kod',
    name: 'Nazwa',
    description: 'Opis',
    startTime: 'Początek',
    endTime: 'Koniec',
    workingHours: 'Godziny pracy',
    filters: { code: 'Kod zawiera…', name: 'Nazwa zawiera…' }
  },
  calendar: {
    title: 'Kalendarz stanowiska',
    hint: 'Tygodniowy rozkład czasu pracy. Wpis z godziną końca wcześniejszą niż początku przechodzi na następny dzień.',
    working: 'Czas pracy',
    noShift: 'Bez zmiany',
    days: {
      monday: 'Poniedziałek',
      tuesday: 'Wtorek',
      wednesday: 'Środa',
      thursday: 'Czwartek',
      friday: 'Piątek',
      saturday: 'Sobota',
      sunday: 'Niedziela'
    }
  },
  recipes: {
    title: 'Receptury',
    subtitle: 'Definicje procesu wytwarzania produktów',
    create: 'Nowa receptura',
    code: 'Kod',
    name: 'Nazwa',
    description: 'Opis',
    primaryProduct: 'Produkt główny',
    primaryProductPlaceholder: 'Wybierz produkt…',
    filters: { code: 'Kod zawiera…', name: 'Nazwa zawiera…' },
    versionStatus: { draft: 'Szkic', released: 'Wydana', obsolete: 'Wycofana' },
    dependencyType: {
      finishToStart: 'Zakończ → Rozpocznij',
      startToStart: 'Rozpocznij → Rozpocznij',
      finishToFinish: 'Zakończ → Zakończ',
      startToFinish: 'Rozpocznij → Zakończ'
    },
    quantityType: { perUnit: 'Na sztukę', perBatch: 'Na partię', fixed: 'Stała' },
    outputType: { product: 'Produkt', byProduct: 'Produkt uboczny', waste: 'Odpad', sample: 'Próbka' },
    detail: {
      subtitle: 'Wersje receptury i ich zawartość',
      versions: 'Wersje',
      newVersion: 'Nowa wersja',
      release: 'Wydaj wersję',
      releasedToast: 'Wersja wydana',
      deleteVersion: 'Usuń wersję',
      confirmDeleteVersion: 'Czy na pewno usunąć tę wersję?',
      noVersionsLeftTitle: 'Receptura nie ma już żadnych wersji',
      noVersionsLeftPrompt: 'Co chcesz zrobić z tą recepturą?',
      noVersionsLeftDeleteHint: 'Receptura nie była nigdy wydana — można ją bezpiecznie usunąć.',
      noVersionsLeftDeactivateHint: 'Receptura była już wydana — zalecana dezaktywacja zamiast usunięcia.',
      deleteRecipe: 'Usuń recepturę',
      deactivateRecipe: 'Dezaktywuj recepturę',
      keepRecipe: 'Zachowaj receptury',
      recipeDeleted: 'Receptura usunięta',
      recipeDeactivated: 'Receptura dezaktywowana',
      operations: 'Operacje',
      noOperations: 'Brak operacji',
      selectOperation: 'Wybierz operację aby edytować szczegóły',
      addOperation: 'Dodaj operację',
      opCode: 'Kod operacji',
      opName: 'Nazwa operacji',
      opDescription: 'Opis',
      setupTimeMinutes: 'Setup (min)',
      runTimePerUnitSeconds: 'Czas / szt (s)',
      confirmDeleteOperation: 'Czy na pewno usunąć tę operację?',
      tabs: {
        dependencies: 'Zależności',
        bom: 'BOM',
        outputs: 'Produkty',
        resources: 'Zasoby',
        attachments: 'Załączniki'
      },
      dependenciesHelp: 'Operacje poprzedzające, które muszą być wykonane przed tą operacją.',
      dependencyGraph: 'Graf zależności operacji',
      dependencyNodeMeta: 'Poprzedza: {predecessors}, następne: {successors}',
      predecessor: 'Operacja poprzedzająca',
      product: 'Produkt',
      productId: 'ID produktu',
      quantity: 'Ilość',
      quantityType: 'Typ ilości',
      outputType: 'Typ wyniku',
      capability: 'Wymagana umiejętność',
      operatorCount: 'Liczba operatorów',
      role: 'Rola',
      fromTemplate: 'Wzorcowa operacja',
      fromTemplatePlaceholder: 'Wybierz wzorzec (opcjonalne)…'
    }
  },
  attachments: {
    title: 'Załączniki',
    upload: 'Wgraj plik',
    empty: 'Brak załączników',
    confirmDelete: 'Czy na pewno usunąć ten załącznik?'
  },
  skills: {
    title: 'Umiejętności',
    subtitle: 'Słownik umiejętności operatorów',
    create: 'Nowa umiejętność',
    code: 'Kod',
    name: 'Nazwa',
    description: 'Opis',
    filters: { code: 'Kod zawiera…', name: 'Nazwa zawiera…' }
  },
  operationTemplates: {
    title: 'Wzorcowe operacje',
    subtitle: 'Szablony do ponownego użycia podczas tworzenia receptur',
    create: 'Nowy wzorzec',
    code: 'Kod',
    name: 'Nazwa',
    description: 'Opis',
    filters: { code: 'Kod zawiera…', name: 'Nazwa zawiera…' }
  },
  reasonCodes: {
    title: 'Kody przyczyn',
    subtitle: 'Słownik przyczyn przestojów i braków',
    create: 'Nowy kod przyczyny',
    code: 'Kod',
    name: 'Nazwa',
    description: 'Opis',
    category: 'Kategoria',
    sortIndex: 'Kolejność',
    isActive: 'Aktywny',
    filters: {
      code: 'Kod zawiera…',
      name: 'Nazwa zawiera…',
      category: 'Kategoria',
      status: 'Status'
    },
    categories: {
      1: 'Przestój',
      2: 'Braki',
      3: 'Jakość',
      4: 'Przygotowanie',
      5: 'Inne'
    }
  },
  scanBy: { 1: 'EAN', 2: 'Kod' },
  stubs: {
    title: 'Moduł w przygotowaniu',
    description: 'Ten obszar zostanie udostępniony po implementacji odpowiednich endpointów na backendzie.'
  },
  validation: {
    required: 'Pole jest wymagane',
    tooShort: 'Wartość jest za krótka',
    invalidEmail: 'Nieprawidłowy adres e-mail'
  },
  errors: {
    generic: 'Wystąpił nieoczekiwany błąd',
    loadFailed: 'Nie udało się załadować danych',
    saveFailed: 'Nie udało się zapisać',
    deleteFailed: 'Nie udało się usunąć',
    unauthorized: 'Sesja wygasła, zaloguj się ponownie'
  },
  toasts: {
    created: 'Utworzono',
    updated: 'Zapisano zmiany',
    deleted: 'Usunięto'
  }
};

const en: typeof pl = {
  common: {
    cancel: 'Cancel',
    confirm: 'Confirm',
    save: 'Save',
    create: 'Create',
    update: 'Update',
    delete: 'Delete',
    edit: 'Edit',
    close: 'Close',
    add: 'Add',
    refresh: 'Refresh',
    search: 'Search…',
    filter: 'Filter',
    clear: 'Clear',
    loading: 'Loading…',
    noData: 'No data',
    yes: 'Yes',
    no: 'No',
    active: 'Active',
    inactive: 'Inactive',
    actions: 'Actions',
    optional: 'optional',
    required: 'required',
    back: 'Back',
    open: 'Open',
    empty: 'No entries',
    status: 'Status',
    notFound: 'Not found',
    signedIn: 'Signed in',
    signOut: 'Sign out',
    notifications: 'Notifications',
    pagination: {
      showing: 'Showing {from}–{to} of {total}',
      pageSize: 'Rows:'
    }
  },
  nav: {
    dashboard: 'Dashboard',
    production: 'Production',
    productionOrders: 'Production orders',
    productionRecipes: 'Recipes',
    schedule: 'Schedule',
    reports: 'Reports',
    configuration: 'Configuration',
    products: 'Products',
    productGroups: 'Product groups',
    measureUnits: 'Measure units',
    warehouses: 'Warehouses',
    departments: 'Departments',
    machines: 'Machines',
    shifts: 'Shifts',
    operators: 'Operators',
    skills: 'Skills',
    reasonCodes: 'Reason codes',
    operationTemplates: 'Operation templates',
    settings: 'Settings'
  },
  auth: {
    signInTitle: 'Sign in to AsistOff MES',
    signInSubtitle: 'Enter your credentials to continue',
    email: 'Email address',
    password: 'Password',
    signIn: 'Sign in',
    signInSuccess: 'Signed in successfully',
    signInError: 'Sign-in failed',
    signUpTitle: 'Create an organization',
    signUpSubtitle: 'Bootstrap a new tenant with an administrator account',
    tenantName: 'Internal name',
    tenantDisplayName: 'Display name',
    contactEmail: 'Contact email',
    confirmPassword: 'Confirm password',
    register: 'Create organization',
    backToLogin: 'Back to sign in',
    goToRegister: 'Create an organization',
    passwordsMismatch: 'Passwords do not match',
    registerSuccess: 'Organization created. Please sign in.',
    registerError: 'Organization creation failed'
  },
  dashboard: {
    title: 'Dashboard',
    subtitle: 'Top-level production KPIs',
    placeholderNote: 'Metrics below are placeholders — they will be wired up once metric endpoints are available.',
    activeOrders: 'Active orders',
    oee: 'OEE',
    operatorsOnline: 'Operators online',
    warehouses: 'Warehouses'
  },
  products: {
    title: 'Products',
    subtitle: 'Manage the product catalog',
    create: 'New product',
    code: 'Code',
    name: 'Name',
    description: 'Description',
    ean: 'EAN',
    barcode: 'Barcode',
    scanBy: 'Scan by',
    group: 'Group',
    isActive: 'Active',
    syncId: 'Sync ID',
    filters: { code: 'Code contains…', name: 'Name contains…', group: 'Group', isActive: 'State' }
  },
  productGroups: {
    title: 'Product groups',
    subtitle: 'Catalog group hierarchy',
    create: 'New group',
    code: 'Code',
    name: 'Name',
    description: 'Description',
    parent: 'Parent group',
    isActive: 'Active',
    syncId: 'Sync ID'
  },
  measureUnits: {
    title: 'Measure units',
    subtitle: 'Units and conversion factors',
    create: 'New unit',
    name: 'Name',
    symbol: 'Symbol',
    type: 'Type',
    baseUnit: 'Base unit',
    conversionFactor: 'Conversion factor',
    isActive: 'Active',
    description: 'Description',
    syncId: 'Sync ID',
    types: { 1: 'Product' }
  },
  warehouses: {
    title: 'Warehouses',
    subtitle: 'Configure warehouse locations',
    create: 'New warehouse',
    name: 'Name',
    syncId: 'Sync ID',
    filters: { name: 'Name contains…' }
  },
  departments: {
    title: 'Departments',
    subtitle: 'Organizational department structure',
    create: 'New department',
    code: 'Code',
    name: 'Name',
    filters: { code: 'Code contains…', name: 'Name contains…' }
  },
  operators: {
    title: 'Operators',
    subtitle: 'Shop-floor workers',
    create: 'New operator',
    identifier: 'Identifier',
    firstName: 'First name',
    lastName: 'Last name',
    ratePerHour: 'Rate / h',
    department: 'Department',
    filters: {
      identifier: 'Identifier…',
      firstName: 'First name…',
      lastName: 'Last name…',
      rateFrom: 'Rate from',
      rateTo: 'Rate to',
      department: 'Department'
    }
  },
  machines: {
    title: 'Machines',
    subtitle: 'Shop-floor equipment',
    create: 'New machine',
    code: 'Code',
    name: 'Name',
    description: 'Description',
    calendar: 'Calendar',
    filters: { code: 'Code contains…', name: 'Name contains…' }
  },
  shifts: {
    title: 'Shifts',
    subtitle: 'Shift dictionary',
    create: 'New shift',
    code: 'Code',
    name: 'Name',
    description: 'Description',
    startTime: 'Start',
    endTime: 'End',
    workingHours: 'Working hours',
    filters: { code: 'Code contains…', name: 'Name contains…' }
  },
  calendar: {
    title: 'Work-center calendar',
    hint: 'Weekly working-time schedule. An entry ending earlier than it starts crosses midnight.',
    working: 'Working time',
    noShift: 'No shift',
    days: {
      monday: 'Monday',
      tuesday: 'Tuesday',
      wednesday: 'Wednesday',
      thursday: 'Thursday',
      friday: 'Friday',
      saturday: 'Saturday',
      sunday: 'Sunday'
    }
  },
  recipes: {
    title: 'Recipes',
    subtitle: 'Production process definitions',
    create: 'New recipe',
    code: 'Code',
    name: 'Name',
    description: 'Description',
    primaryProduct: 'Primary product',
    primaryProductPlaceholder: 'Select product…',
    filters: { code: 'Code contains…', name: 'Name contains…' },
    versionStatus: { draft: 'Draft', released: 'Released', obsolete: 'Obsolete' },
    dependencyType: {
      finishToStart: 'Finish → Start',
      startToStart: 'Start → Start',
      finishToFinish: 'Finish → Finish',
      startToFinish: 'Start → Finish'
    },
    quantityType: { perUnit: 'Per unit', perBatch: 'Per batch', fixed: 'Fixed' },
    outputType: { product: 'Product', byProduct: 'By-product', waste: 'Waste', sample: 'Sample' },
    detail: {
      subtitle: 'Versions and their contents',
      versions: 'Versions',
      newVersion: 'New version',
      release: 'Release version',
      releasedToast: 'Version released',
      deleteVersion: 'Delete version',
      confirmDeleteVersion: 'Really delete this version?',
      noVersionsLeftTitle: 'No versions left for this recipe',
      noVersionsLeftPrompt: 'What would you like to do with the recipe?',
      noVersionsLeftDeleteHint: 'The recipe has never been released — it is safe to delete.',
      noVersionsLeftDeactivateHint: 'The recipe was previously released — deactivating is recommended over deletion.',
      deleteRecipe: 'Delete recipe',
      deactivateRecipe: 'Deactivate recipe',
      keepRecipe: 'Keep recipe',
      recipeDeleted: 'Recipe deleted',
      recipeDeactivated: 'Recipe deactivated',
      operations: 'Operations',
      noOperations: 'No operations',
      selectOperation: 'Select an operation to edit details',
      addOperation: 'Add operation',
      opCode: 'Operation code',
      opName: 'Operation name',
      opDescription: 'Description',
      setupTimeMinutes: 'Setup (min)',
      runTimePerUnitSeconds: 'Run time / unit (s)',
      confirmDeleteOperation: 'Really delete this operation?',
      tabs: {
        dependencies: 'Dependencies',
        bom: 'BOM',
        outputs: 'Outputs',
        resources: 'Resources',
        attachments: 'Attachments'
      },
      dependenciesHelp: 'Predecessor operations that must complete before this one.',
      dependencyGraph: 'Operation dependency graph',
      dependencyNodeMeta: 'Prev: {predecessors}, next: {successors}',
      predecessor: 'Predecessor operation',
      product: 'Product',
      productId: 'Product ID',
      quantity: 'Quantity',
      quantityType: 'Qty type',
      outputType: 'Output type',
      capability: 'Required capability',
      operatorCount: 'Operator count',
      role: 'Role',
      fromTemplate: 'Template operation',
      fromTemplatePlaceholder: 'Select a template (optional)…'
    }
  },
  attachments: {
    title: 'Attachments',
    upload: 'Upload file',
    empty: 'No attachments',
    confirmDelete: 'Really delete this attachment?'
  },
  skills: {
    title: 'Skills',
    subtitle: 'Operator skill dictionary',
    create: 'New skill',
    code: 'Code',
    name: 'Name',
    description: 'Description',
    filters: { code: 'Code contains…', name: 'Name contains…' }
  },
  operationTemplates: {
    title: 'Operation templates',
    subtitle: 'Reusable templates for recipe operations',
    create: 'New template',
    code: 'Code',
    name: 'Name',
    description: 'Description',
    filters: { code: 'Code contains…', name: 'Name contains…' }
  },
  reasonCodes: {
    title: 'Reason codes',
    subtitle: 'Dictionary of downtime and scrap causes',
    create: 'New reason code',
    code: 'Code',
    name: 'Name',
    description: 'Description',
    category: 'Category',
    sortIndex: 'Sort index',
    isActive: 'Active',
    filters: {
      code: 'Code contains…',
      name: 'Name contains…',
      category: 'Category',
      status: 'Status'
    },
    categories: {
      1: 'Downtime',
      2: 'Scrap',
      3: 'Quality',
      4: 'Setup',
      5: 'Other'
    }
  },
  scanBy: { 1: 'EAN', 2: 'Code' },
  stubs: {
    title: 'Module coming soon',
    description: 'This area will be enabled once the corresponding backend endpoints ship.'
  },
  validation: {
    required: 'Field is required',
    tooShort: 'Value is too short',
    invalidEmail: 'Invalid email address'
  },
  errors: {
    generic: 'An unexpected error occurred',
    loadFailed: 'Failed to load data',
    saveFailed: 'Failed to save',
    deleteFailed: 'Failed to delete',
    unauthorized: 'Session expired, please sign in again'
  },
  toasts: {
    created: 'Created',
    updated: 'Changes saved',
    deleted: 'Deleted'
  }
};

const messages = { pl, en };

function resolveInitialLocale(): 'pl' | 'en' {
  try {
    const saved = localStorage.getItem('locale');
    if (saved === 'pl' || saved === 'en') return saved;
  } catch { /* ignore */ }
  return 'pl';
}

const i18n = createI18n({
  legacy: false,
  locale: resolveInitialLocale(),
  fallbackLocale: 'en',
  missingWarn: false,
  fallbackWarn: false,
  messages
});

export default i18n;
