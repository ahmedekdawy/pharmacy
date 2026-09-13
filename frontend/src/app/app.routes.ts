import { Routes } from '@angular/router';
import { authGuard, permissionGuard } from './core/guards/auth.guard';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () => import('./features/landing/landing').then((m) => m.Landing)
  },
  {
    path: 'login',
    loadComponent: () => import('./features/auth/login').then((m) => m.Login)
  },
  {
    path: 'contact',
    loadComponent: () => import('./features/landing/landing').then((m) => m.Landing)
  },
  {
    path: 'app',
    canActivate: [authGuard],
    loadComponent: () => import('./layout/shell').then((m) => m.AppShell),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      {
        path: 'dashboard',
        canActivate: [permissionGuard('Report.Sales')],
        loadComponent: () => import('./features/dashboard/dashboard').then((m) => m.DashboardPage)
      },
      {
        path: 'products',
        canActivate: [permissionGuard('Product.View')],
        loadComponent: () => import('./features/products/products').then((m) => m.Products)
      },
      {
        path: 'categories',
        canActivate: [permissionGuard('Category.View')],
        loadComponent: () => import('./features/categories/categories').then((m) => m.CategoriesPage)
      },
      {
        path: 'brands',
        canActivate: [permissionGuard('Brand.View')],
        loadComponent: () => import('./features/brands/brands').then((m) => m.BrandsPage)
      },
      {
        path: 'locations',
        canActivate: [permissionGuard('Location.View')],
        loadComponent: () => import('./features/locations/locations').then((m) => m.LocationsPage)
      },
      {
        path: 'inventory',
        canActivate: [permissionGuard('Inventory.View')],
        loadComponent: () => import('./features/inventory/inventory').then((m) => m.InventoryPage)
      },
      {
        path: 'suppliers',
        canActivate: [permissionGuard('Supplier.View')],
        loadComponent: () => import('./features/suppliers/suppliers').then((m) => m.SuppliersPage)
      },
      {
        path: 'purchases',
        canActivate: [permissionGuard('Purchase.View')],
        loadComponent: () => import('./features/purchases/purchases').then((m) => m.PurchasesPage)
      },
      {
        path: 'customers',
        canActivate: [permissionGuard('Customer.View')],
        loadComponent: () => import('./features/customers/customers').then((m) => m.CustomersPage)
      },
      {
        path: 'sales',
        canActivate: [permissionGuard('Sale.View')],
        loadComponent: () => import('./features/sales/sales').then((m) => m.SalesPage)
      },
      {
        path: 'returns',
        canActivate: [permissionGuard('Sale.View')],
        loadComponent: () => import('./features/returns/returns').then((m) => m.ReturnsPage)
      },
      {
        path: 'cash-shifts',
        canActivate: [permissionGuard('CashShift.View')],
        loadComponent: () => import('./features/cash-shifts/cash-shifts').then((m) => m.CashShiftsPage)
      },
      {
        path: 'expenses',
        canActivate: [permissionGuard('Expense.View')],
        loadComponent: () => import('./features/expenses/expenses').then((m) => m.ExpensesPage)
      },
      {
        path: 'reports',
        canActivate: [permissionGuard('Report.Sales')],
        loadComponent: () => import('./features/reports/reports').then((m) => m.ReportsPage)
      },
      {
        path: 'audit',
        canActivate: [permissionGuard('Audit.View')],
        loadComponent: () => import('./features/audit/audit').then((m) => m.AuditPage)
      },
      {
        path: 'settings',
        canActivate: [permissionGuard('Settings.Manage')],
        loadComponent: () => import('./features/settings/settings').then((m) => m.SettingsPage)
      },
      {
        path: 'users',
        canActivate: [permissionGuard('User.View')],
        loadComponent: () => import('./features/users/users').then((m) => m.UsersPage)
      },
      {
        path: 'roles',
        canActivate: [permissionGuard('Role.View')],
        loadComponent: () => import('./features/roles/roles').then((m) => m.RolesPage)
      }
    ]
  },
  { path: '**', redirectTo: '' }
];
