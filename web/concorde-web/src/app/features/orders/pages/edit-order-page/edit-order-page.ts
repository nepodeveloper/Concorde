import { CommonModule } from '@angular/common';
import { Component, effect, inject, input, signal } from '@angular/core';
import {
  FormArray,
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { OrderApiService } from '../../../../core/api/order-api.service';
import { ApiError, Order } from '../../../../core/models/order.models';

type LineForm = FormGroup<{
  sku: FormControl<string>;
  name: FormControl<string>;
  quantity: FormControl<number>;
  unitPrice: FormControl<number>;
}>;

@Component({
  selector: 'app-edit-order-page',
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  templateUrl: './edit-order-page.html',
  styleUrl: './edit-order-page.css',
})
export class EditOrderPage {
  private readonly api = inject(OrderApiService);
  private readonly router = inject(Router);

  /** Route param, bound via withComponentInputBinding. */
  readonly id = input.required<string>();

  readonly order = signal<Order | null>(null);
  readonly loading = signal(false);
  readonly notFound = signal(false);
  readonly submitting = signal(false);
  readonly banner = signal<string | null>(null);
  readonly serverErrors = signal<Record<string, string>>({});

  readonly form = new FormGroup({
    customerName: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(200)] }),
    customerCode: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(50)] }),
    currency: new FormControl('ZAR', { nonNullable: true, validators: [Validators.required] }),
    notes: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(1000)] }),
    lines: new FormArray<LineForm>([]),
  });

  constructor() {
    effect(() => this.load(this.id()));
  }

  get lines(): FormArray<LineForm> {
    return this.form.controls.lines;
  }

  private load(id: string): void {
    this.loading.set(true);
    this.notFound.set(false);
    this.banner.set(null);

    this.api.getOrder(id).subscribe({
      next: (order) => {
        this.order.set(order);
        this.loading.set(false);
        if (order.status === 'Fulfilled' || order.status === 'Cancelled') {
          this.banner.set(`A ${order.status.toLowerCase()} order can no longer be edited.`);
          return;
        }
        this.form.patchValue({
          customerName: order.customerName,
          customerCode: order.customerCode ?? '',
          currency: order.currency,
          notes: order.notes ?? '',
        });
        this.lines.clear();
        for (const line of order.lines) {
          this.addLine(line.sku, line.name, line.quantity, line.unitPrice);
        }
      },
      error: (err) => {
        this.loading.set(false);
        if (err.status === 404) {
          this.notFound.set(true);
        } else {
          const apiError = err.error as ApiError | undefined;
          this.banner.set(apiError?.message ?? 'Could not load the order.');
        }
      },
    });
  }

  addLine(sku = '', name = '', quantity = 1, unitPrice = 0): void {
    this.lines.push(
      new FormGroup({
        sku: new FormControl(sku, { nonNullable: true, validators: [Validators.required, Validators.maxLength(50)] }),
        name: new FormControl(name, { nonNullable: true, validators: [Validators.required, Validators.maxLength(200)] }),
        quantity: new FormControl(quantity, { nonNullable: true, validators: [Validators.required, Validators.min(1)] }),
        unitPrice: new FormControl(unitPrice, { nonNullable: true, validators: [Validators.required, Validators.min(0)] }),
      }),
    );
  }

  removeLine(index: number): void {
    if (this.lines.length > 1) {
      this.lines.removeAt(index);
    }
  }

  get editable(): boolean {
    const status = this.order()?.status;
    return status === 'Pending' || status === 'Confirmed';
  }

  serverError(path: string): string | null {
    return this.serverErrors()[path] ?? null;
  }

  submit(): void {
    const order = this.order();
    if (!order || !this.editable) return;

    this.banner.set(null);
    this.serverErrors.set({});

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.banner.set('Please correct the highlighted fields before saving.');
      return;
    }

    const value = this.form.getRawValue();
    this.submitting.set(true);

    this.api
      .updateOrder(order.id, {
        customerName: value.customerName,
        customerCode: value.customerCode || null,
        currency: value.currency,
        notes: value.notes || null,
        lines: value.lines,
      })
      .subscribe({
        next: (updated) => {
          this.router.navigate(['/orders', updated.id]);
        },
        error: (err) => {
          this.submitting.set(false);
          const apiError = err.error as ApiError | undefined;
          if (apiError?.code === 'VALIDATION_FAILED') {
            const fieldErrors: Record<string, string> = {};
            for (const detail of apiError.details) {
              if (detail.field) fieldErrors[detail.field] = detail.message;
            }
            this.serverErrors.set(fieldErrors);
            this.banner.set(apiError.message);
          } else {
            this.banner.set(apiError?.message ?? 'Saving the order failed. Is the API running?');
          }
        },
      });
  }
}
