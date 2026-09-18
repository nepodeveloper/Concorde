import { HttpClient, HttpParams, HttpResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import {
  CreateOrderRequest,
  Order,
  OrderStatus,
  OrderSummary,
  PagedResult,
} from '../models/order.models';

export interface CreateOrderOutcome {
  order: Order;
  /** False when the API returned 200 (idempotent replay of an existing reference). */
  wasCreated: boolean;
}

@Injectable({ providedIn: 'root' })
export class OrderApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = 'http://localhost:5000/api/v1/orders';

  createOrder(request: CreateOrderRequest): Observable<CreateOrderOutcome> {
    return this.http
      .post<Order>(this.baseUrl, request, { observe: 'response' })
      .pipe(
        map((response: HttpResponse<Order>) => ({
          order: response.body!,
          wasCreated: response.status === 201,
        })),
      );
  }

  getOrders(page = 1, pageSize = 20, status?: OrderStatus): Observable<PagedResult<OrderSummary>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (status) {
      params = params.set('status', status);
    }
    return this.http.get<PagedResult<OrderSummary>>(this.baseUrl, { params });
  }

  getOrder(id: string): Observable<Order> {
    return this.http.get<Order>(`${this.baseUrl}/${id}`);
  }

  changeStatus(id: string, status: OrderStatus): Observable<Order> {
    return this.http.patch<Order>(`${this.baseUrl}/${id}/status`, { status });
  }
}
