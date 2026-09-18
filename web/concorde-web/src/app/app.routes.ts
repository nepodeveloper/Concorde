import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'orders' },
  {
    path: 'orders',
    loadComponent: () =>
      import('./features/orders/pages/order-list-page/order-list-page').then((m) => m.OrderListPage),
  },
  {
    path: 'orders/new',
    loadComponent: () =>
      import('./features/orders/pages/create-order-page/create-order-page').then((m) => m.CreateOrderPage),
  },
  {
    path: 'orders/:id',
    loadComponent: () =>
      import('./features/orders/pages/order-detail-page/order-detail-page').then((m) => m.OrderDetailPage),
  },
  { path: '**', redirectTo: 'orders' },
];

