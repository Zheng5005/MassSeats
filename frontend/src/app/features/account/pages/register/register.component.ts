import { Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';

import { ApiError } from '../../../../core/api/error.model';
import { AuthService } from '../../../../core/auth/auth.service';
import { CreateUserRequest } from '../../../../shared/models/auth.models';
import { errorMessage } from '../../../../shared/utils/format';

const CONFLICT_MESSAGE = 'An account with this email already exists.';
const GENERIC_MESSAGE = 'Registration failed. Please try again.';

function registerErrorMessage(err: unknown): string {
  if (err instanceof ApiError) {
    if (err.status === 409) {
      return err.detail ?? CONFLICT_MESSAGE;
    }
    return err.detail ?? GENERIC_MESSAGE;
  }
  return errorMessage(err);
}

function emptyToNull(value: string | null): string | null {
  const trimmed = value?.trim() ?? '';
  return trimmed.length > 0 ? trimmed : null;
}

@Component({
  selector: 'app-register',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './register.html',
})
export class Register {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly form = new FormGroup({
    firstName: new FormControl('', { validators: [Validators.required] }),
    lastName: new FormControl(''),
    email: new FormControl('', { validators: [Validators.required, Validators.email] }),
    // Client-side choice: the API enforces no minimum length, so keep a sane one here.
    password: new FormControl('', { validators: [Validators.required, Validators.minLength(6)] }),
    nationalId: new FormControl(''),
    phone: new FormControl(''),
  });

  protected readonly error = signal<string | null>(null);

  protected onSubmit(): void {
    if (this.form.invalid) {
      return;
    }
    const raw = this.form.getRawValue();
    const payload: CreateUserRequest = {
      firstName: (raw.firstName ?? '').trim(),
      lastName: emptyToNull(raw.lastName),
      email: (raw.email ?? '').trim(),
      password: raw.password ?? '',
      nationalId: emptyToNull(raw.nationalId),
      phone: emptyToNull(raw.phone),
    };
    this.error.set(null);
    this.auth.register(payload).subscribe({
      next: () => this.router.navigateByUrl('/login'),
      error: (err: unknown) => this.error.set(registerErrorMessage(err)),
    });
  }

  protected emailError(): string | null {
    const control = this.form.controls.email;
    if (!(control.invalid && (control.dirty || control.touched))) {
      return null;
    }
    if (control.hasError('required')) {
      return 'Email is required';
    }
    if (control.hasError('email')) {
      return 'Enter a valid email';
    }
    return null;
  }

  protected passwordError(): string | null {
    const control = this.form.controls.password;
    if (!(control.invalid && (control.dirty || control.touched))) {
      return null;
    }
    if (control.hasError('required')) {
      return 'Password is required';
    }
    if (control.hasError('minlength')) {
      return 'Password must be at least 6 characters';
    }
    return null;
  }

  protected firstNameError(): string | null {
    const control = this.form.controls.firstName;
    if (control.invalid && (control.dirty || control.touched) && control.hasError('required')) {
      return 'First name is required';
    }
    return null;
  }
}
