import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import {
  FormArray,
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { OrderApiService } from '../../../../core/api/order-api.service';
import { ApiError } from '../../../../core/models/order.models';

type LineForm = FormGroup<{
  sku: FormControl<string>;
  name: FormControl<string>;
  quantity: FormControl<number>;
  unitPrice: FormControl<number>;
}>;

@Component({
  selector: 'app-create-order-page',
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  templateUrl: './create-order-page.html',
  styleUrl: './create-order-page.css',
})
export class CreateOrderPage {
  private readonly api = inject(OrderApiService);
  private readonly router = inject(Router);

  readonly submitting = signal(false);
  readonly banner = signal<string | null>(null);
  /** Server-side field errors keyed by envelope field path, e.g. "lines[0].quantity". */
  readonly serverErrors = signal<Record<string, string>>({});

  readonly form = new FormGroup({
    externalReference: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(64)] }),
    customerName: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(200)] }),
    customerCode: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(50)] }),
    currency: new FormControl('ZAR', { nonNullable: true, validators: [Validators.required] }),
    notes: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(1000)] }),
    lines: new FormArray<LineForm>([]),
  });

  constructor() {
    this.addLine();
  }

  get lines(): FormArray<LineForm> {
    return this.form.controls.lines;
  }

  addLine(): void {
    this.lines.push(
      new FormGroup({
        sku: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(50)] }),
        name: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(200)] }),
        quantity: new FormControl(1, { nonNullable: true, validators: [Validators.required, Validators.min(1)] }),
        unitPrice: new FormControl(0, { nonNullable: true, validators: [Validators.required, Validators.min(0)] }),
      }),
    );
  }

  removeLine(index: number): void {
    if (this.lines.length > 1) {
      this.lines.removeAt(index);
    }
  }

  /** Inline message for a server-reported field path (FR-08.5). */
  serverError(path: string): string | null {
    return this.serverErrors()[path] ?? null;
  }

  submit(): void {
    this.banner.set(null);
    this.serverErrors.set({});

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.banner.set('Please correct the highlighted fields before submitting.');
      return;
    }

    const value = this.form.getRawValue();
    this.submitting.set(true);

    this.api
      .createOrder({
        externalReference: value.externalReference,
        customerName: value.customerName,
        customerCode: value.customerCode || null,
        currency: value.currency,
        notes: value.notes || null,
        lines: value.lines,
      })
      .subscribe({
        next: (outcome) => {
          if (!outcome.wasCreated) {
            // §33: a safe replay is not an error to the user.
            this.banner.set('This order was already received. Showing the existing order.');
          }
          this.router.navigate(['/orders', outcome.order.id]);
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
            this.banner.set(apiError?.message ?? 'Submitting the order failed. Is the API running?');
          }
        },
      });
  }
}
