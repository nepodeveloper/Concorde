export type OrderStatus = 'Pending' | 'Confirmed' | 'Fulfilled' | 'Cancelled';

export interface OrderLine {
  sku: string;
  name: string;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
}

export interface Order {
  id: string;
  externalReference: string;
  customerName: string;
  customerCode: string | null;
  currency: string;
  notes: string | null;
  status: OrderStatus;
  statusReason: string | null;
  subtotal: number;
  total: number;
  createdAtUtc: string;
  updatedAtUtc: string;
  lines: OrderLine[];
}

export interface OrderSummary {
  id: string;
  externalReference: string;
  customerName: string;
  createdAtUtc: string;
  status: OrderStatus;
  currency: string;
  total: number;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

/** US-08 error envelope returned by the API for all non-2xx responses. */
export interface ApiError {
  code: string;
  message: string;
  details: ApiErrorDetail[];
  traceId: string;
}

export interface ApiErrorDetail {
  field: string | null;
  code: string;
  message: string;
}

export interface CreateOrderRequest {
  externalReference: string;
  customerName: string;
  customerCode: string | null;
  currency: string;
  notes: string | null;
  lines: {
    sku: string;
    name: string;
    quantity: number;
    unitPrice: number;
  }[];
}

/** Amend a pending order; the external reference is immutable. */
export type UpdateOrderRequest = Omit<CreateOrderRequest, 'externalReference'>;

/** FR-07.7: the UI only offers transitions valid for the current status. */
export const VALID_TRANSITIONS: Record<OrderStatus, OrderStatus[]> = {
  Pending: ['Confirmed', 'Cancelled'],
  Confirmed: ['Fulfilled', 'Cancelled'],
  Fulfilled: ['Cancelled'],
  Cancelled: [],
};
