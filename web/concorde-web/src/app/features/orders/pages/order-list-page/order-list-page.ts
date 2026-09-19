import { CommonModule } from '@angular/common';
import { Component, inject, signal, computed } from '@angular/core';
import { RouterLink } from '@angular/router';
import { OrderApiService } from '../../../../core/api/order-api.service';
import { ApiError, OrderStatus, OrderSummary, PagedResult } from '../../../../core/models/order.models';

@Component({
  selector: 'app-order-list-page',
  imports: [CommonModule, RouterLink],
  templateUrl: './order-list-page.html',
  styleUrl: './order-list-page.css',
})
export class OrderListPage {
  private readonly api = inject(OrderApiService);

  readonly statuses: OrderStatus[] = ['Pending', 'Confirmed', 'Fulfilled', 'Cancelled'];

  readonly result = signal<PagedResult<OrderSummary> | null>(null);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly page = signal(1);
  readonly statusFilter = signal<OrderStatus | ''>('');
  readonly searchTerm = signal('');

  readonly totalPages = computed(() => {
    const r = this.result();
    return r ? Math.max(1, Math.ceil(r.totalCount / r.pageSize)) : 1;
  });

  /** Client-side quick search over the loaded page by reference or customer. */
  readonly visibleOrders = computed(() => {
    const r = this.result();
    if (!r) return [];
    const term = this.searchTerm().trim().toLowerCase();
    if (!term) return r.items;
    return r.items.filter(
      (o) =>
        o.externalReference.toLowerCase().includes(term) ||
        o.customerName.toLowerCase().includes(term),
    );
  });

  readonly stats = computed(() => {
    const r = this.result();
    const items = r?.items ?? [];
    // Revenue must never mix currencies; report one figure per currency (no FX conversion in MVP).
    const revenueByCurrency = new Map<string, number>();
    for (const o of items) {
      if (o.status === 'Cancelled') continue;
      revenueByCurrency.set(o.currency, (revenueByCurrency.get(o.currency) ?? 0) + o.total);
    }
    return {
      totalOrders: r?.totalCount ?? 0,
      pending: items.filter((o) => o.status === 'Pending').length,
      revenue: [...revenueByCurrency.entries()]
        .map(([currency, total]) => ({ currency, total }))
        .sort((a, b) => b.total - a.total),
    };
  });

  constructor() {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);
    const status = this.statusFilter() || undefined;

    this.api.getOrders(this.page(), 20, status).subscribe({
      next: (result) => {
        this.result.set(result);
        this.loading.set(false);
      },
      error: (err) => {
        const apiError = err.error as ApiError | undefined;
        this.error.set(apiError?.message ?? 'Could not load orders. Is the API running?');
        this.loading.set(false);
      },
    });
  }

  filterChanged(value: string): void {
    this.statusFilter.set(value as OrderStatus | '');
    this.page.set(1);
    this.load();
  }

  searchChanged(value: string): void {
    this.searchTerm.set(value);
  }

  goToPage(page: number): void {
    if (page < 1 || page > this.totalPages()) return;
    this.page.set(page);
    this.load();
  }
}
