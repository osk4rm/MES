# Getting started

This chapter takes a new user from an empty system to the master data
and recipes views. Follow it top to bottom on a local stack.

## 1. Concepts you need first

| Term | Meaning in AsistOff MES |
|------|-------------------------|
| Tenant | Your company workspace. Every record belongs to exactly one tenant and tenants cannot see each other. |
| User | A person who logs into the back-office UI (email + password). |
| Operator | A person who executes work on the shopfloor, identified by code or RFID tag. An Operator is master data, not a login account. |
| Production Order | An instruction to manufacture a quantity of a product by a due date. Covered in slice 2. |
| BOM | Bill of Materials: the list of materials and quantities needed to produce one unit. Covered in [Routing, BOM and resources](06-recipes-routing-bom.md). |
| Routing | The ordered sequence of Operations needed to produce a product. Covered in [Routing, BOM and resources](06-recipes-routing-bom.md). |
| Operation | A single step in a routing, performed on a Work Center. |
| Work Center | A resource on the shopfloor where operations happen. In this system a Machine is the Work Center. |
| Shift | A defined working time window, for example 06:00–14:00. |
| Unit of Measure | How a quantity is measured: pieces, kilograms, meters, liters. |
| EAN / GTIN | The barcode identifying a tradeable product, stored on the Product. |

## 2. Sign up a tenant

A tenant is self-registered; no administrator invitation is needed.

1. Open `/register` in the browser.
2. Fill in the tenant name, display name, contact email, password and
   password confirmation, then submit.
3. The backend (`POST /api/tenants`, anonymous) creates the tenant and
   provisions the first user account from the contact email and
   password. The response carries only the tenant id, name and active
   status — never secrets or settings.
4. Continue to the login page.

Validation rules: the tenant name must not be empty (max 200
characters), the contact email must be a valid email address, and the
password and confirmation must match. Failures return `400` with a
message naming the field.

## 3. Log in and out

1. Open `/login` and enter the contact email and password from signup.
2. The backend (`POST /api/auth/sign-in`, anonymous) sets two `httpOnly`
   cookies — access token and refresh token — and returns an empty
   token body plus the user claims. The browser stores nothing in
   `localStorage`; the cookies are the session.
3. After login the app redirects to `/dashboard` (or to the page stored
   in the `redirect` query parameter when a protected page bounced you
   to login).
4. Reloading the page does not log you out: the app silently restores
   the session from the cookies and re-reads your permissions.
5. To log out, use the sign-out action. The backend
   (`POST /api/auth/sign-out`, authenticated) revokes the refresh token
   and clears both cookies.

Error cases:

| Situation | Result |
|-----------|--------|
| Wrong email or password | `401`, stay on the login page |
| Inactive tenant | `401` |
| Expired access token mid-session | The app refreshes it from the refresh cookie (`POST /api/auth/refresh`, anonymous); if that fails you return to `/login` |

## 4. Roles and permissions overview

Permissions are short codes attached to your session at sign-in.
Reads never need a permission beyond being signed in; writes are gated
per module and the backend rejects gated requests with `403`.

| Permission | Grants write access to | Managed in |
|------------|------------------------|------------|
| `configuration.write` | All master data in this slice: products, groups, units, warehouses, departments, machines, operators, skills, shifts | Roles view |
| `production.write` | Recipes, versions, routing, BOM, resources, operation templates | Roles view |
| `tenant.admin` | Roles and permission assignment themselves | Fixed to administrators |

Open `/settings/roles` (requires `tenant.admin`; other users are
redirected to the dashboard with an "access denied" message) to grant
or revoke these permissions per role.

## 5. Navigation map

After login the sidebar groups every view. The breadcrumb trail and the
browser tab title always follow the same map.

| Group | Views (route) |
|-------|---------------|
| Dashboard | Dashboard (`/dashboard`) |
| Production | Production Orders (`/production/orders`), Recipes (`/production/recipes`), Kanban (`/production/kanban`), Lots (`/production/lots`), SPC characteristics (`/production/spc-characteristics`), Scrap (`/production/scrap`), Downtime (`/production/downtime`), Andon (`/production/andon`), Telemetry (`/production/telemetry`), OPC UA connections (`/production/opcua-connections`), Telemetry dashboard (`/production/telemetry-dashboard`) |
| Schedule | Gantt (`/schedule`), Dispatch board (`/schedule/dispatch`) |
| Reports | OEE dashboard (`/reports/oee`), Reliability dashboard (`/reports/reliability`) |
| Configuration (master data) | Products (`/configuration/products`), Product groups (`/configuration/product-groups`), Measure units (`/configuration/measure-units`), Warehouses (`/configuration/warehouses`), Departments (`/configuration/departments`), Machines (`/configuration/machines`), Operators (`/configuration/operators`), Skills (`/configuration/skills`), Shifts (`/configuration/shifts`), Reason codes (`/configuration/reason-codes`), Operation templates (`/configuration/operation-templates`), Maintenance (`/configuration/maintenance`), Maintenance plans (`/configuration/maintenance-plans`) |
| Settings | Settings (`/settings`), Roles (`/settings/roles`, needs `tenant.admin`) |

Unknown URLs render a not-found page inside the shell with a way back.

## 6. Where to go next

- To describe what you make and store: [Products, groups, units and scan lookup](02-master-data-products.md).
- To describe where you store and who owns what: [Warehouses and departments](03-master-data-organisation.md).
- To describe who and what does the work: [Work Centers, operators, rosters and skills](04-master-data-resources.md).
- To engineer how a product is made: [Recipes and versions](05-recipes.md).
