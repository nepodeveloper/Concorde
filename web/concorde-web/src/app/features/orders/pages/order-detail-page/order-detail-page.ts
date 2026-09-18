import { CommonModule } from '@angular/common';
import { Component, inject, input, signal, computed, effect } from '@angular/core';
import { RouterLink } from '@angular/router';
import { OrderApiService } from '../../../../core/api/order-api.service';
import { ApiError, Order, OrderStatus, VALID_TRANSITIONS } from '../../../../core/models/order.models';

@Component({
  selector: 'app-order-detail-page',
  imports: [CommonModule, RouterLink],
  templateUrl: './order-detail-page.html',
  styleUrl: './order-detail-page.css',
})
export class OrderDetailPage {
  private readonly api = inject(OrderApiService);

  /** Route param, bound via withComponentInputBinding. */
  readonly id = input.required<string>();

  readonly order = signal<Order | null>(null);
  readonly loading = signal(false);
  readonly notFound = signal(false);
  readonly error = signal<string | null>(null);
  readonly changingStatus = signal(false);

  readonly availableTransitions = computed<OrderStatus[]>(() => {
    const order = this.order();
    return order ? VALID_TRANSITIONS[order.status] : [];
  });

  constructor() {
    effect(() => this.load(this.id()));
  }

  private load(id: string): void {
    this.loading.set(true);
    this.notFound.set(false);
    this.error.set(null);

    this.api.getOrder(id).subscribe({
      next: (order) => {
        this.order.set(order);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        if (err.status === 404) {
          this.notFound.set(true);
        } else {
          const apiError = err.error as ApiError | undefined;
          this.error.set(apiError?.message ?? 'Could not load the order.');
        }
      },
    });
  }

  changeStatus(status: OrderStatus): void {
    const order = this.order();
    if (!order) return;

    this.changingStatus.set(true);
    this.error.set(null);

    this.api.changeStatus(order.id, status).subscribe({
      next: (updated) => {
        this.order.set(updated);
        this.changingStatus.set(false);
      },
      error: (err) => {
        const apiError = err.error as ApiError | undefined;
        this.error.set(apiError?.message ?? 'Status change failed.');
        this.changingStatus.set(false);
      },
    });
  }
}
